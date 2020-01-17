using System.Collections.Generic;
using System.IO;
using UnityEngine;

using BattleTech;
using BattleTech.Save.Test;
using BattleTech.Serialization;

using HBS.Scripting.Attributes;
using HBS.Data;
using HBS.Reflection;


// How do we decide which unit goes next?
enum TurnOrderStrategy
{
    StaticUnitOrder,
    RandomOrder,
    FastestFirst,
    SlowestFirst,
    HighBVFirst,
    LowBVFirst,
    StaticOrderOrder,
    ClosestToEnemyFirst,
    FarthestFromEnemyFirst,
    UnitProvidedOrderPriorityOrder,
}

enum AIPhase
{
    WaitingForMyTurn,
    UnitSelection,
    UnitPlanning,
    UnitExecution
}

[ScriptBinding("AITEAM")]
[SerializableContract("AITeam")]
public class AITeam : Team, IStackSequence
{
    [fastJSON.JsonIgnore, System.NonSerialized]
    public static readonly HBS.Logging.ILog activationLogger =
        HBS.Logging.Logger.GetLogger(HBS.Logging.LoggerNames.COMBATLOG_ACTORACTIVATION, HBS.Logging.LogLevel.Warning);

    IStackSequence currentSequence;
    AbstractActor currentUnit;

    [SerializableMember(SerializationTarget.SaveGame)]
    bool isComplete;

    List<InvocationMessage> pendingInvocations;
    public bool WaitingForNotificationCompletion;

    float planningStartTime;

    public string behaviorTreeLogString;

    public bool runningInterruptPhase;

    public bool ThinksOnThisMachine { get; private set; }

    System.IO.StreamWriter behaviorTreeLogWriter;

    [SerializableMember(SerializationTarget.SaveGame)]
    int lastActivatedRound;

    // calculated chosen target per lance
    // serialization: yes
    public Dictionary<Lance, AbstractActor> DesignatedTargetForLance;

    // fraction (0.0 - 1.0) below inspiration target damage that current target damage must be to use inspire
    [SerializableMember(SerializationTarget.SaveGame)]
    float inspirationWindow;

    // differential damage that we target when selecting a unit to use the inspire ability
    [SerializableMember(SerializationTarget.SaveGame)]
    float inspirationTargetDamage;

    /// <summary>
    /// AI never gets the benefits of "Streak Breaking", which addresses player-perceived unfairness in RNG
    /// </summary>
    public override float StreakBreakingValue { get { return 0.0f; } }
    public override void ProcessRandomRoll(float targetValue, bool succeeded) { }

    public AITeam(string name,
        Color teamColor,
        string GUID,
        bool shareVisionWithAlliance,
        CombatGameState combat,
        bool substitutingforHuman,
        bool isMultiplayer)
        : this(name, teamColor, GUID, shareVisionWithAlliance, combat)
    {
        PlayerControlsTeam = substitutingforHuman; // this probably is not correct
        ThinksOnThisMachine = !isMultiplayer || LocalPlayerControlsTeam; // this is better
    }

    public AITeam(string name, Color teamColor, string GUID, bool shareVisionWithAlliance, CombatGameState combat) : base(name, teamColor, GUID, TeamController.Computer, shareVisionWithAlliance, combat)
    {
        PlayerControlsTeam = false; // FALSE BY DEFAULT
        ThinksOnThisMachine = true; // TRUE BY DEFAULT

        Combat.AIManager.AddAITeam(this);
        Combat.MessageCenter.AddSubscriber(MessageCenterMessageType.OnReadyToMove, OnReadyToMove);
        Combat.MessageCenter.AddSubscriber(MessageCenterMessageType.OnActorDestroyed, OnActorDestroyed);
        Combat.MessageCenter.AddSubscriber(MessageCenterMessageType.OnActorAttacked, OnActorAttacked);
        pendingInvocations = new List<InvocationMessage>();

        behaviorTreeLogWriter = null;

        lastActivatedRound = int.MinValue;

        DesignatedTargetForLance = new Dictionary<Lance, AbstractActor>();
    }

    private AITeam()
    {

    }

    ~AITeam()
    {
        if (behaviorTreeLogWriter != null)
        {
            behaviorTreeLogWriter.Flush();
            behaviorTreeLogWriter.Close();
        }
    }

    public override void InitFromSave()
    {
        base.InitFromSave();

        PlayerControlsTeam = false; // FALSE BY DEFAULT
        ThinksOnThisMachine = true; // TRUE BY DEFAULT

        Combat.AIManager.AddAITeam(this);
        Combat.MessageCenter.AddSubscriber(MessageCenterMessageType.OnReadyToMove, OnReadyToMove);
        Combat.MessageCenter.AddSubscriber(MessageCenterMessageType.OnActorDestroyed, OnActorDestroyed);
        Combat.MessageCenter.AddSubscriber(MessageCenterMessageType.OnActorAttacked, OnActorAttacked);
        pendingInvocations = new List<InvocationMessage>();

        behaviorTreeLogWriter = null;

        lastActivatedRound = int.MinValue;

        DesignatedTargetForLance = new Dictionary<Lance, AbstractActor>();
    }

    /// <summary>
    /// Is this AI team just going to defer its turn?
    /// TODO/DAVE : there are other conditions that need to be added to this helper - patrol routes, etc. Also, rename it if you can think of a better one :)
    /// </summary>
    public bool IsDeferringTurn
    {
        get
        {
            if (PreviouslyDetectedEnemyUnits.Count > 0)
                return false;

            return true;
        }
    }

    System.IO.StreamWriter MakeBehaviorTreeLogWriter()
    {
        BehaviorVariableScope scope = UnityGameInstance.BattleTechGame.BehaviorVariableScopeManager.globalBehaviorVariableScope;
        BehaviorVariableValue val = scope.GetVariable(BehaviorVariableName.Bool_LogBehaviorTreeLogic);
        if ((val == null) || (!val.BoolVal))
        {
            return null;
        }

        string dirName = scope.GetVariable(BehaviorVariableName.String_InfluenceMapCalculationLogDirectory).StringVal;
        dirName = Path.Combine(Application.persistentDataPath, dirName);
        try
        {
            System.IO.Directory.CreateDirectory(dirName);
        }
        catch (System.IO.IOException ioException)
        {
            Debug.LogError(string.Format("Failed to create directory {0} : exception {1}", dirName, ioException));
            return null;
        }

        System.DateTime timeNow = System.DateTime.Now;

        int extraIndex = 0;
        string fullFN = null;

        while (true)
        {
            string filename = string.Format("behavior_tree_logic_{0}_{1:00}_{2:00}_{3:00}_{4:00}_{5:00}_{6:0000}_{7}.txt",
                timeNow.Year, timeNow.Month, timeNow.Day,
                timeNow.Hour, timeNow.Minute, timeNow.Second, extraIndex, Name);

            fullFN = System.IO.Path.Combine(dirName, filename);
            if (!System.IO.File.Exists(fullFN))
            {
                break;
            }
            extraIndex++;
        }
        return new System.IO.StreamWriter(fullFN);
    }

    public void Log(string msg)
    {
        if (behaviorTreeLogWriter == null)
        {
            behaviorTreeLogWriter = MakeBehaviorTreeLogWriter();
            if (behaviorTreeLogWriter == null)
            {
                return;
            }
        }

        System.DateTime timeNow = System.DateTime.Now;

        string timestamp = string.Format("[{0:00}:{1:00}:{2:00}] ",
            timeNow.Hour, timeNow.Minute, timeNow.Second);
        behaviorTreeLogWriter.WriteLine(timestamp + msg);
    }

    public void LogError(string msg)
    {
        Log(msg);
        Debug.LogError(msg);
    }

    // fraction (0.0 - 1.0) below inspiration target damage that current target damage must be to use inspire
    public float GetInspirationWindow()
    {
        return inspirationWindow;
    }

    // differential damage that we target when selecting a unit to use the inspire ability
    public float GetInspirationTargetDamage()
    {
        return inspirationTargetDamage;
    }

    void processRoundStart()
    {
        if (CanInspire)
        {
            if (inspirationWindow < Combat.BattleTechGame.BehaviorVariableScopeManager.GetGlobalScope().GetVariable(BehaviorVariableName.Float_InspirationBaseDamageWindow).FloatVal)
            {
                // first round with the new inspiration
                inspirationWindow = Combat.BattleTechGame.BehaviorVariableScopeManager.GetGlobalScope().GetVariable(BehaviorVariableName.Float_InspirationBaseDamageWindow).FloatVal;
            }
            else
            {
                // make window more open
                inspirationWindow += Combat.BattleTechGame.BehaviorVariableScopeManager.GetGlobalScope().GetVariable(BehaviorVariableName.Float_InspirationIncrementalDamageWindow).FloatVal;
            }
            inspirationTargetDamage = AIUtil.CalcMaxInspirationDelta(units, false);
        }
        else
        {
            // set window to invalid value
            inspirationWindow = float.MinValue;
        }

        ChooseDesignatedTarget();
    }

    void ChooseDesignatedTargetForLance(Lance lance, List<AbstractActor> lanceUnits, List<AbstractActor> hostileUnits)
    {
        System.Text.StringBuilder logStringBuilder = new System.Text.StringBuilder();
        string logFilename = Combat.AILogCache.MakeFilename("des_targ");

        AIUtil.LogAI("Choosing designated target for lance " + lance);
        logStringBuilder.AppendLine("Choosing designated target for lance " + lance);

        float closestSumDistance = float.MaxValue;
        AbstractActor closestUnit = null;

        for (int unitIndex = 0; unitIndex < lanceUnits.Count; ++unitIndex)
        {
            AbstractActor lanceUnit = lanceUnits[unitIndex];
            // calculate sum of distances to hostiles
            float sum = 0;
            for (int hostileIndex = 0; hostileIndex < hostileUnits.Count; ++hostileIndex)
            {
                AbstractActor hostileUnit = hostileUnits[hostileIndex];
                float distance = (hostileUnit.CurrentPosition - lanceUnit.CurrentPosition).magnitude;
                sum += distance;
            }
            if (sum < closestSumDistance)
            {
                closestSumDistance = sum;
                closestUnit = lanceUnit;
            }
        }

        if (closestUnit == null)
        {
            AIUtil.LogAI("no designated target");
            logStringBuilder.AppendLine("Could not find a closest unit. Setting no designated target.");
            Combat.AILogCache.AddLogData(logFilename, logStringBuilder.ToString());
            DesignatedTargetForLance[lance] = null;
            return;
        }

        float mostFirepowerRemoved = float.MinValue;
        float mostDamage = float.MinValue;
        AbstractActor mostDamageUnit = null;

        Weapon w = closestUnit.ImaginaryLaserWeapon;
        List<Weapon> weaponList = new List<Weapon> { w };

        bool useFirepowerTakeaway = closestUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_TargetDesignationUsesFirepowerTakeaway).BoolVal;

        for (int hostileIndex = 0; hostileIndex < hostileUnits.Count; ++hostileIndex)
        {
            AbstractActor hostileUnit = hostileUnits[hostileIndex];

            if (hostileUnit.IsDead || (!closestUnit.HasLOFToTargetUnit(hostileUnit, w)))
            {
                continue;
            }

            float toHit = w.GetToHitFromPosition(hostileUnit, 1, closestUnit.CurrentPosition, hostileUnit.CurrentPosition, true, hostileUnit.IsEvasive);
            float expDmg = w.DamagePerShotFromPosition(MeleeAttackType.NotSet, closestUnit.CurrentPosition, hostileUnit) * w.ShotsWhenFired * toHit;
            float expFirePowerRemoved = useFirepowerTakeaway ? AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(closestUnit, closestUnit.CurrentPosition, hostileUnit, hostileUnit.CurrentPosition, hostileUnit.CurrentRotation, weaponList, MeleeAttackType.NotSet) : 0.0f;
            //AIUtil.LogAI("expFirepowerRemoved: " + expFirePowerRemoved);

            if ((useFirepowerTakeaway && ((expFirePowerRemoved > mostFirepowerRemoved) || ((expFirePowerRemoved == mostFirepowerRemoved) && (expDmg > mostDamage)))) ||
                ((!useFirepowerTakeaway) && (expDmg > mostDamage)))
            {
                mostDamageUnit = hostileUnit;
                mostDamage = expDmg;
                mostFirepowerRemoved = expFirePowerRemoved;
            }
        }
        DesignatedTargetForLance[lance] = mostDamageUnit;
        AIUtil.LogAI("designated target: " + mostDamageUnit);
        if (mostDamageUnit != null)
        {
            logStringBuilder.AppendLine("Setting unit that will take the highest damage as designated target: " + mostDamageUnit.UnitName);
            Combat.AILogCache.AddLogData(logFilename, logStringBuilder.ToString());
        }
    }

    void ChooseDesignatedTarget()
    {
        AIUtil.LogAI("Choosing designated target for round " + Combat.TurnDirector.CurrentRound);
        List<AbstractActor> teamUnits = new List<AbstractActor>();
        List<AbstractActor> hostileUnits = new List<AbstractActor>();
        HashSet<Lance> aiLances = new HashSet<Lance>();
        Dictionary<Lance, List<AbstractActor>> aiLanceUnits = new Dictionary<Lance, List<AbstractActor>>();

        for (int i = 0; i < Combat.AllActors.Count; ++i)
        {
            AbstractActor actor = Combat.AllActors[i];
            if (actor.IsDead)
            {
                continue;
            }
            if (actor.team == this)
            {
                teamUnits.Add(actor);
                Lance lance = actor.lance;
                if (!aiLances.Contains(lance))
                {
                    aiLances.Add(lance);
                }
                if (!aiLanceUnits.ContainsKey(lance))
                {
                    aiLanceUnits[lance] = new List<AbstractActor>();
                }
                aiLanceUnits[lance].Add(actor);
            }
            if (Combat.HostilityMatrix.IsEnemy(actor.team.GUID, this.GUID))
            {
                hostileUnits.Add(actor);
            }
        }

        foreach (Lance lance in aiLances)
        {
            ChooseDesignatedTargetForLance(lance, aiLanceUnits[lance], hostileUnits);
        }
    }

    public override List<IStackSequence> TurnActorProcessActivation()
    {
        List<IStackSequence> sequences = base.TurnActorProcessActivation();
        currentUnit = null;
        isComplete = false;
        sequences.Add(this);

        if (Combat.TurnDirector.CurrentRound > this.lastActivatedRound)
        {
            processRoundStart();
            this.lastActivatedRound = Combat.TurnDirector.CurrentRound;
        }

        planningStartTime = Combat.BattleTechGame.Time;

        behaviorTreeLogString = "";

        if (ThinksOnThisMachine)
        {
            currentUnit = selectCurrentUnit();
            UpdateAloneStatus(currentUnit);
        }

        if (currentUnit == null)
        {
            isComplete = true;
            activationLogger.LogDebug("[TurnActorProcessActivation] AI Team isComplete");
            if (behaviorTreeLogWriter != null)
            {
                behaviorTreeLogWriter.Flush();
            }
            return sequences;
        }

        if (Combat.TurnDirector.IsInterleaved)
        {
            bool calcTargetPhase = false;
            bool hasReservedAlready = HasDoneReserveCalculationThisPhase();
            bool canReserve = false;
            bool enemyIsFullyGhosted = false;

            if (!hasReservedAlready)
            {
                AIUtil.LogAI(string.Format("Reserve Decision: considering reserving for round {0} phase {1} ", Combat.TurnDirector.CurrentRound, Combat.TurnDirector.CurrentPhase), HBS.Logging.LoggerNames.AI_TURNORDER);

                if (CanEntireEnemyTeamBeGhosted() && !DoesTeamHaveSensorLockAvailableThisPhase())
                {
                    this.BehaviorVariables.SetVariable(BehaviorVariableName.Int_ReserveCalculationsLastDoneForRoundNumber, new BehaviorVariableValue(Combat.TurnDirector.CurrentRound));
                    this.BehaviorVariables.SetVariable(BehaviorVariableName.Int_ReserveCalculationsLastDoneForPhaseNumber, new BehaviorVariableValue(Combat.TurnDirector.CurrentPhase));

                    canReserve = true;
                    enemyIsFullyGhosted = true;

                    if (DidEnemyTeamReserve())
                    {
                        AIUtil.LogAI("Reserve Decision: Reserving to counter Enemy ECM Reserve", HBS.Logging.LoggerNames.AI_TURNORDER);
                        calcTargetPhase = true;

                    }
                    else if (!HasAnyHostileGoneBeforeCurrentPhase())
                    {
                        AIUtil.LogAI("Reserve Decision: Reserving to catch up to Enemy in ECM Ghosted", HBS.Logging.LoggerNames.AI_TURNORDER);
                        calcTargetPhase = true;
                    }
                }
                else
                {
                    canReserve = HasPrerequisitesToReserve();
                    AIUtil.LogAI("Reserve Decision: has prerequisites to reserve? " + canReserve, HBS.Logging.LoggerNames.AI_TURNORDER);

                    if (canReserve)
                    {
                        if (IsReserveDieRollSuccessful() || DidEnemyTeamReserve())
                        {
                            AIUtil.LogAI("Reserve Decision: die roll successful", HBS.Logging.LoggerNames.AI_TURNORDER);
                            calcTargetPhase = true;
                        }
                    }
                }
            }

            if (IsReservingThisTurn() && canReserve)
            {
                calcTargetPhase = true;
            }

            if (calcTargetPhase || !canReserve)
            {
                SetReserveTargetPhase(canReserve, findAvgHostilePhase: !enemyIsFullyGhosted);
            }
        }

        AIRoleAssignment.AssignRoleToUnit(currentUnit, units);

        return sequences;
    }

    private bool HasPrerequisitesToReserve()
    {
        if (!currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_ReserveEnabled).BoolVal)
        {
            AIUtil.LogAI("Reserve Decision: not reserving because Bool_ReserveEnabled is false", HBS.Logging.LoggerNames.AI_TURNORDER);
            return false;
        }

        if (!AllTeamUnitsCanReserveThisPhase())
        {
            AIUtil.LogAI("Reserve Decision: a unit cannot reserve", HBS.Logging.LoggerNames.AI_TURNORDER);
            return false;
        }

        int numberOfTeamMechsStillToGo = CountLivingTeamMechsStillToGo();
        int numberOfHostileMechsStillToGo = CountLivingHostileMechsStillToGo();
        //int totalHostileMechsRemaining = CountLivingHostileMechs();

        if ((numberOfTeamMechsStillToGo <= 1) || (numberOfHostileMechsStillToGo == 0))
        {
            return false;
        }

        int vulnerableHostileCount = CountVulnerableHostiles();
        int vulnerableSelfCount = CountVulnerableOnMyTeam();

        if (vulnerableHostileCount > 0)
        {
            AIUtil.LogAI("Reserve Decision: Vulnerable Hostile Unit detected", HBS.Logging.LoggerNames.AI_TURNORDER);
        }

        if (vulnerableSelfCount > 0)
        {
            AIUtil.LogAI("Reserve Decision: Vulnerable Friendly Unit detected", HBS.Logging.LoggerNames.AI_TURNORDER);
        }

        if ((vulnerableHostileCount > 0) || (vulnerableSelfCount > 0))
        {
            return false;
        }

        // if any of my units still to go are sensor locked, let's not reserve
        for (int i = 0; i < units.Count; ++i)
        {
            AbstractActor unit = units[i];
            if ((!unit.IsDead) && (!unit.HasActivatedThisRound) && ((unit as Mech) != null))
            {
                if (unit.IsSensorLocked)
                {
                    return false;
                }
            }
        }

        Debug.Assert(currentUnit != null);
        bool currentUnitIsMech = ((currentUnit as Mech) != null);

        // If the current unit is not a mech, check to see if this is ok.
        if (!currentUnitIsMech)
        {
            if (!currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_ReserveByNonMechs).BoolVal)
            {
                // Looks like we don't want non-mechs to initiate reserving. Skip.
                AIUtil.LogAI("Reserve Decision: current unit is not a mech", HBS.Logging.LoggerNames.AI_TURNORDER);
                return false;
            }
        }

        if (AreHostilesGoingToGetBehindUsIfWeReserve())
        {
            AIUtil.LogAI("Reserve Decision: hostiles could get behind me", HBS.Logging.LoggerNames.AI_TURNORDER);
            return false;
        }

        if (AnyLanceUnitInRangeForLethalHit())
        {
            AIUtil.LogAI("Reserve Decision: me, or one of my lancemates, are in jeopardy of a lethal hit", HBS.Logging.LoggerNames.AI_TURNORDER);
            return false;
        }

        return true;
    }

    private bool AreHostilesGoingToGetBehindUsIfWeReserve()
    {
        List<AbstractActor> myUnitsToMoveThisPhase = new List<AbstractActor>();
        List<AbstractActor> hostileUnitsToMoveThisPhase = new List<AbstractActor>();

        for (int unitIndex = 0; unitIndex < units.Count; ++unitIndex)
        {
            AbstractActor unit = units[unitIndex];
            if (unit.IsAvailableThisPhase)
            {
                myUnitsToMoveThisPhase.Add(unit);
            }
        }
        for (int unitIndex = 0; unitIndex < Combat.AllActors.Count; ++unitIndex)
        {
            AbstractActor unit = Combat.AllActors[unitIndex];
            if ((unit.team.IsEnemy(this)) && (IsUnitAvailableThisPhaseOrNextPhase(unit)))
            {
                hostileUnitsToMoveThisPhase.Add(unit);
            }
        }

        return (CanAnyUnitsFromListGetBehindAnyUnitsFromList(myUnitsToMoveThisPhase, hostileUnitsToMoveThisPhase));
    }

    /// <summary>
    /// Checks to see if any of my units could be killed by hostile units. Used for determining whether or not to reserve.
    /// </summary>
    /// <returns></returns>
    private bool AnyLanceUnitInRangeForLethalHit()
    {
        for (int unitIndex = 0; unitIndex < units.Count; ++unitIndex)
        {
            AbstractActor unit = units[unitIndex];
            if (unit.IsDead)
            {
                continue;
            }

            float hitPoints = AttackEvaluator.MinHitPoints(unit);
            float okffr = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OverkillFactorForReserve).FloatVal;
            float adjustedHitPoints = hitPoints * okffr / 100.0f;

            float totalDmg = 0.0f;

            List<ICombatant> detectedCombatants = AIUtil.GetDetectedUnitsForUnit(unit);
            for (int detectedIndex = 0; detectedIndex < detectedCombatants.Count; ++detectedIndex)
            {
                AbstractActor detectedActor = detectedCombatants[detectedIndex] as AbstractActor;
                if ((detectedActor == null) || (!unit.Combat.HostilityMatrix.IsEnemy(unit.team.GUID, detectedActor.team.GUID)) || (detectedActor.IsDead) || (!detectedActor.IsAvailableThisPhase))
                {
                    continue;
                }
                totalDmg += AIUtil.ExpectedDamageForAttack(detectedActor, AIUtil.AttackType.Shooting, detectedActor.Weapons, unit, detectedActor.CurrentPosition, unit.CurrentPosition, false, unit);
            }

            if (totalDmg > adjustedHitPoints)
            {
                AIUtil.LogAI(string.Format("Reserve Decision: too much expected damage to {0}: {1} hit points: {2} ratio: {3}% okffr: {4}%", unit.DisplayName, totalDmg, hitPoints, 100.0f * totalDmg / hitPoints, okffr), HBS.Logging.LoggerNames.AI_TURNORDER);
                return true;
            }
        }
        return false;
    }

    private bool IsUnitAvailableThisPhaseOrNextPhase(AbstractActor unit)
    {
        return (unit.IsAvailableOnPhase(Combat.TurnDirector.CurrentPhase) ||
            unit.IsAvailableOnPhase(Combat.TurnDirector.CurrentPhase + 1));
    }

    private bool IsReserveDieRollSuccessful()
    {
        int numberOfHostileMechsStillToGo = CountLivingHostileMechsStillToGo();
        int totalHostileMechsRemaining = CountLivingHostileMechs();

        float hostilePercentageRemaining = 100.0f * numberOfHostileMechsStillToGo / ((float)totalHostileMechsRemaining);

        AIUtil.LogAI(string.Format("Reserve Decision: hostile percentage remaining: {0}%", hostilePercentageRemaining), HBS.Logging.LoggerNames.AI_TURNORDER);

        float y = currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ReserveHostilePercentageIncrementY).FloatVal;
        float x = currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ReserveHostilePercentageIncrementX).FloatVal;

        float hostileBonus = (x != 0) ? (hostilePercentageRemaining * y / x) : 0;

        float reservePercentage = currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ReserveBasePercentage).FloatVal;

        float totalReservePercentage = reservePercentage + hostileBonus;

        AIUtil.LogAI(string.Format("Reserve Decision: chance to reserve: {0}%", totalReservePercentage), HBS.Logging.LoggerNames.AI_TURNORDER);

        this.BehaviorVariables.SetVariable(BehaviorVariableName.Int_ReserveCalculationsLastDoneForRoundNumber, new BehaviorVariableValue(Combat.TurnDirector.CurrentRound));
        this.BehaviorVariables.SetVariable(BehaviorVariableName.Int_ReserveCalculationsLastDoneForPhaseNumber, new BehaviorVariableValue(Combat.TurnDirector.CurrentPhase));

        float dieRoll = Random.Range(0, 100.0f);

        AIUtil.LogAI(string.Format("Reserve Decision: reserve random die roll: {0}%", dieRoll), HBS.Logging.LoggerNames.AI_TURNORDER);

        return (dieRoll <= totalReservePercentage);
    }

    /// <summary>
    /// Check to see if we're planning to reserve (further) this turn.
    /// </summary>
    /// <returns></returns>
    private bool IsReservingThisTurn()
    {
        return ((currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_ReserveCalculationsLastDoneForRoundNumber).IntVal == Combat.TurnDirector.CurrentRound) &&
                (currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_ReserveToPhaseNumber).IntVal > Combat.TurnDirector.CurrentPhase));
    }

    /// <summary>
    /// Whether we have done the reserve calculation already this turn. We don't want to decide to reserve more than once per phase (though we can decide to stop reserving if reserving becomes impossible).
    /// </summary>
    /// <returns></returns>
    private bool HasDoneReserveCalculationThisPhase()
    {
        int lastDoneRoundNumber = currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_ReserveCalculationsLastDoneForRoundNumber).IntVal;
        int lastDonePhaseNumber = currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_ReserveCalculationsLastDoneForPhaseNumber).IntVal;

        return ((lastDoneRoundNumber == Combat.TurnDirector.CurrentRound) &&
            (lastDonePhaseNumber == Combat.TurnDirector.CurrentPhase));
    }

    /// <summary>
    /// Sets the internal variables to either reserve down or not, based on the parameter
    /// </summary>
    /// <param name="reserve">whether to reserve or not</param>
    ///  /// <param name="findAvgHostilePhase">whether to find the phase that makes the most sense or simply reserve to the next round</param>
    private void SetReserveTargetPhase(bool reserve, bool findAvgHostilePhase)
    {
        int targetReservePhase;

        if (reserve)
        {
            int possiblePhase;

            if (findAvgHostilePhase)
            {
                float averageHostileMechPhase = GetAverageHostileMechPhase();
                possiblePhase = Mathf.RoundToInt(averageHostileMechPhase + currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ReserveHostileOffset).FloatVal);
            }
            else
            {
                possiblePhase = Combat.TurnDirector.CurrentPhase + 1;
            }

            targetReservePhase = Mathf.Min(Combat.Constants.Phase.PhaseAssault, possiblePhase);
        }
        else
        {
            targetReservePhase = -1; // before any legal phase, will be interpreted as not reserving.
        }

        this.BehaviorVariables.SetVariable(BehaviorVariableName.Int_ReserveToPhaseNumber, new BehaviorVariableValue(targetReservePhase));
    }

    private int CountLivingHostileMechs()
    {
        int n = 0;

        List<AbstractActor> enemies = Combat.GetAllEnemiesOf(this);
        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i];
            if (!enemy.IsDead)
            {
                ++n;
            }
        }

        return n;
    }

    private int CountLivingTeamMechsStillToGo()
    {
        int n = 0;

        for (int i = 0; i < units.Count; ++i)
        {
            AbstractActor unit = units[i];
            if ((!unit.IsDead) && (!unit.HasActivatedThisRound) && ((unit as Mech) != null))
            {
                ++n;
            }
        }

        return n;
    }

    private int CountLivingHostileMechsStillToGo()
    {
        int n = 0;

        List<AbstractActor> enemies = Combat.GetAllEnemiesOf(this);
        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i];
            if ((!enemy.IsDead) && (!enemy.HasActivatedThisRound))
            {
                ++n;
            }
        }

        return n;
    }

    private bool CanEntireEnemyTeamBeGhosted()
    {
        List<AbstractActor> enemies = Combat.GetAllEnemiesOf(this);

        bool anEnemyIsGhosted = false;
        bool thereAreBlips = false;

        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i];

            var visLevel = VisibilityToTarget(enemy);

            if (!enemy.IsDead)
            {
                if (visLevel >= VisibilityLevel.Blip0Minimum)
                {
                    thereAreBlips = true;
                }

                if (visLevel >= VisibilityLevel.BlipGhost && visLevel != VisibilityLevel.LOSFull && enemy.IsGhosted)
                {
                    anEnemyIsGhosted = true;
                }
                else if (visLevel == VisibilityLevel.LOSFull)
                {
                    return false;
                }
            }
        }

        if (anEnemyIsGhosted)
        {
            enemyTeamMayHaveECM = true;
        }

        return anEnemyIsGhosted || (enemyTeamMayHaveECM && thereAreBlips);
    }

    private bool DidEnemyTeamReserve()
    {
        List<AbstractActor> enemies = Combat.GetAllEnemiesOf(this);
        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i];

            if ((!enemy.IsDead) && enemy.team.DeferredThisActivation)
            {
                return true;
            }
        }

        return false;
    }

    private bool DoesTeamHaveSensorLockAvailableThisPhase()
    {
        for (int i = 0; i < units.Count; ++i)
        {
            AbstractActor unit = units[i];
            if ((!unit.IsOperational) || (!unit.IsAvailableThisPhase) || (unit.HasFiredThisRound))
            {
                continue;
            }

            if (AIUtil.HasAbilityAvailable(unit, ActiveAbilityID.SensorLock))
            {
                return true;
            }
        }

        return false;
    }

    private bool AllTeamUnitsCanReserveThisPhase()
    {
        for (int i = 0; i < units.Count; ++i)
        {
            AbstractActor unit = units[i];
            if ((unit.IsDead) || (!unit.IsAvailableThisPhase))
            {
                continue;
            }
            if (!unit.CanDeferUnit)
            {
                return false;
            }
        }
        return true;
    }

    private bool HasAnyHostileGoneBeforeCurrentPhase()
    {
        List<AbstractActor> enemies = Combat.GetAllEnemiesOf(this);
        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i];

            if (!enemy.IsDead && (enemy.Initiative <= Combat.TurnDirector.CurrentPhase || enemy.HasActivatedThisRound))
            {
                return true;
            }
        }

        return false;
    }

    private int CountVulnerableHostiles()
    {
        int n = 0;

        List<AbstractActor> enemies = Combat.GetAllEnemiesOf(this);
        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i];
            Mech enemyMech = enemy as Mech;
            if ((enemyMech != null) && (!enemyMech.HasActivatedThisRound) && (enemyMech.IsVulnerableToCalledShots()))
            {
                ++n;
            }
        }

        return n;
    }

    private int CountVulnerableOnMyTeam()
    {
        int n = 0;

        for (int i = 0; i < this.units.Count; ++i)
        {
            AbstractActor unit = this.units[i];
            Mech mech = unit as Mech;
            if ((mech != null) && (!mech.HasActivatedThisRound) && (mech.IsVulnerableToCalledShots()))
            {
                ++n;
            }
        }

        return n;
    }

    float GetAverageHostileMechPhase()
    {
        float initSum = 0.0f;
        int count = 0;

        List<AbstractActor> enemies = Combat.GetAllEnemiesOf(this);

        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i];
            Mech enemyMech = enemy as Mech;
            if ((enemyMech != null) && (!enemyMech.HasActivatedThisRound))
            {
                initSum += enemy.Initiative;
                ++count;
            }
        }

        return count == 0 ? 0 : initSum / (float)count;
    }

    bool CanAnyHostileGetBehindUnit(AbstractActor movingUnit)
    {
        List<AbstractActor> hostileUnits = new List<AbstractActor>();

        for (int unitIndex = 0; unitIndex < Combat.AllActors.Count; ++unitIndex)
        {
            AbstractActor otherUnit = Combat.AllActors[unitIndex];
            if (otherUnit.team.IsEnemy(movingUnit.team))
            {
                hostileUnits.Add(otherUnit);
            }
        }

        return CanAnyUnitsFromListGetBehindUnit(movingUnit, hostileUnits);
    }

    bool CanAnyUnitsFromListGetBehindAnyUnitsFromList(List<AbstractActor> targetUnits, List<AbstractActor> hostileUnits)
    {
        for (int unitIndex = 0; unitIndex < targetUnits.Count; ++unitIndex)
        {
            AbstractActor targetUnit = targetUnits[unitIndex];
            if (CanAnyUnitsFromListGetBehindUnit(targetUnit, hostileUnits))
            {
                return true;
            }
        }
        return false;
    }

    public static bool CanAnyUnitsFromListGetBehindUnit(AbstractActor targetUnit, List<AbstractActor> hostileUnits)
    {
        for (int unitIndex = 0; unitIndex < hostileUnits.Count; ++unitIndex)
        {
            AbstractActor hostileUnit = hostileUnits[unitIndex];

            if (AIUtil.CanUnitGetBehindUnit(targetUnit, hostileUnit))
            {
                return true;
            }
        }
        return false;
    }

    void OnReadyToMove(MessageCenterMessage msg)
    {
        ReadyToMoveMessage rtmMsg = msg as ReadyToMoveMessage;

        if ((rtmMsg != null) && (rtmMsg.teamGUID == GUID))
        {
            WaitingForNotificationCompletion = false;
        }
    }

    void OnActorDestroyed(MessageCenterMessage message)
    {
        ActorDestroyedMessage msg = message as ActorDestroyedMessage;

        AbstractActor destroyedUnit = UnityGameInstance.BattleTechGame.Combat.FindActorByGUID(msg.DestroyedGuid);

        if (destroyedUnit != null && IsEnemy(destroyedUnit.team) && destroyedUnit.HasECMAbilityInstalled)
        {
            enemyTeamMayHaveECM = false;
        }
    }

    void OnActorAttacked(MessageCenterMessage message)
    {
        ActorAttackedMessage msg = message as ActorAttackedMessage;

        var attacker = UnityGameInstance.BattleTechGame.Combat.FindActorByGUID(msg.AttackedByGUID);
        var attacked = UnityGameInstance.BattleTechGame.Combat.FindActorByGUID(msg.AttackedGuid);

        if (attacked != null && attacker != null && attacked.team.GUID == this.GUID && attacker.ParentECMCarrier != null)
        {
            attackedByEcmProtectedUnits.Add(attacker);
        }
    }

    private List<AbstractActor> GetVulnerableUnusedUnitsForCurrentPhase()
    {
        List<AbstractActor> fallenUnits = new List<AbstractActor>();
        List<AbstractActor> unusedUnits = GetUnusedUnitsForCurrentPhase();

        for (int i = 0; i < unusedUnits.Count; ++i)
        {
            AbstractActor unit = unusedUnits[i];
            Mech mech = unit as Mech;
            if (mech == null)
            {
                continue;
            }

            if (mech.IsVulnerableToCalledShots())
            {
                fallenUnits.Add(mech);
            }
        }

        return fallenUnits;
    }

    private List<AbstractActor> GetUnstableUnusedUnitsForCurrentPhase()
    {
        List<AbstractActor> unstableUnits = new List<AbstractActor>();
        List<AbstractActor> unusedUnits = GetUnusedUnitsForCurrentPhase();

        for (int i = 0; i < unusedUnits.Count; ++i)
        {
            AbstractActor unit = unusedUnits[i];
            Mech mech = unit as Mech;
            if (mech == null)
            {
                continue;
            }

            if (mech.IsUnsteady)
            {
                unstableUnits.Add(mech);
            }
        }

        return unstableUnits;
    }

    class DistanceToVulnerableHostileSorter : IComparer<AbstractActor>
    {
        float GetDistanceToClosestFallenHostile(AbstractActor unit)
        {
            float bestDist = float.MaxValue;
            for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
            {
                ICombatant hostile = unit.BehaviorTree.enemyUnits[i];
                if (hostile.IsDead)
                {
                    continue;
                }
                Mech hostileMech = hostile as Mech;
                if (hostileMech == null)
                {
                    continue;
                }
                if (hostileMech.IsVulnerableToCalledShots())
                {
                    float dist = (unit.CurrentPosition - hostileMech.CurrentPosition).magnitude;
                    bestDist = Mathf.Min(bestDist, dist);
                }
            }
            return bestDist;
        }

        int IComparer<AbstractActor>.Compare(AbstractActor a, AbstractActor b)
        {
            float aDist = GetDistanceToClosestFallenHostile(a);
            float bDist = GetDistanceToClosestFallenHostile(b);
            if (aDist > bDist)
            {
                return 1;
            }
            if (aDist < bDist)
            {
                return -1;
            }
            return 0;
        }
    }

    class SortByDistanceToHostileAscendingSorter : IComparer<AbstractActor>
    {
        float GetDistanceToClosestHostile(AbstractActor unit)
        {
            float bestDist = float.MaxValue;
            List<AbstractActor> hostiles = AIUtil.HostilesToUnit(unit);

            for (int i = 0; i < hostiles.Count; ++i)
            {
                ICombatant hostile = hostiles[i];
                if (hostile.IsDead)
                {
                    continue;
                }
                float dist = (unit.CurrentPosition - hostile.CurrentPosition).magnitude;
                bestDist = Mathf.Min(bestDist, dist);
            }
            return bestDist;
        }

        int IComparer<AbstractActor>.Compare(AbstractActor a, AbstractActor b)
        {
            float aDist = GetDistanceToClosestHostile(a);
            float bDist = GetDistanceToClosestHostile(b);
            if (aDist > bDist)
            {
                return 1;
            }
            if (aDist < bDist)
            {
                return -1;
            }
            return 0;
        }
    }

    class ProgressAlongPatrolRouteSorter : IComparer<AbstractActor>
    {
        public static RouteGameLogic GetPatrolRoute(AbstractActor unit)
        {
            string routeGUID = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_RouteGUID).StringVal;

            if ((routeGUID == null) || (routeGUID.Length == 0))
            {
                return null;
            }

            return RoutingUtil.FindRouteByGUID(unit.BehaviorTree, routeGUID);
        }

        float GetProgressAlongPatrolRoute(AbstractActor unit)
        {
            RouteGameLogic patrolRoute = GetPatrolRoute(unit);
            if (patrolRoute == null)
            {
                return float.MinValue;
            }

            bool routeComplete = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteCompleted).BoolVal;
            if (routeComplete)
            {
                return patrolRoute.routePointList.Length;
            }

            int targetPoint = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_RouteTargetPoint).IntVal;
            bool goingForward = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteFollowingForward).BoolVal;

            float accum = 0;
            if (!goingForward)
            {
                // assume we've looped around
                accum += patrolRoute.routePointList.Length;

                // number of points we've gone since the end
                accum += patrolRoute.routePointList.Length - targetPoint;
            }
            else
            {
                // number of points we've gone since the beginning
                accum += targetPoint;
            }

            int prevPoint = targetPoint + (goingForward ? -1 : 1);

            if (prevPoint < 0)
            {
                prevPoint = 1;
            }
            if (prevPoint >= patrolRoute.routePointList.Length)
            {
                prevPoint = patrolRoute.routePointList.Length - 1;
            }

            float targetDist = (patrolRoute.routePointList[targetPoint].transform.position - unit.CurrentPosition).magnitude;
            float prevDist = (patrolRoute.routePointList[prevPoint].transform.position - unit.CurrentPosition).magnitude;

            float sum = targetDist + prevDist;
            float frac = (sum > 0) ? prevDist / sum : 0.0f;

            return accum + frac;
        }

        int IComparer<AbstractActor>.Compare(AbstractActor a, AbstractActor b)
        {
            float aDist = GetProgressAlongPatrolRoute(a);
            float bDist = GetProgressAlongPatrolRoute(b);
            if (aDist > bDist)
            {
                return -1;
            }
            if (aDist < bDist)
            {
                return 1;
            }
            return 0;
        }
    }

    bool AnyHostilesVulnerable(AbstractActor unit)
    {
        for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
        {
            ICombatant hostile = unit.BehaviorTree.enemyUnits[i];
            if (hostile.IsDead)
            {
                continue;
            }
            Mech hostileMech = hostile as Mech;
            if (hostileMech == null)
            {
                continue;
            }

            if (hostileMech.IsVulnerableToCalledShots())
            {
                return true;
            }
        }
        return false;
    }

    AbstractActor selectCurrentUnit()
    {
        List<AbstractActor> unusedUnits = GetUnusedUnitsForCurrentPhase();
        List<AbstractActor> fallenUnits = GetVulnerableUnusedUnitsForCurrentPhase();
        List<AbstractActor> unstableUnits = GetUnstableUnusedUnitsForCurrentPhase();

        if (fallenUnits.Count != 0)
        {
            unusedUnits = fallenUnits;
        }
        else if (unstableUnits.Count != 0)
        {
            unusedUnits = unstableUnits;
        }

        if (unusedUnits.Count == 0)
        {
            return null;
        }

        if (AnyHostilesVulnerable(unusedUnits[0]))
        {
            DistanceToVulnerableHostileSorter sorter = new DistanceToVulnerableHostileSorter();
            unusedUnits.Sort(sorter);
        }
        else if (AnyUnitsHavePatrolRoutes(unusedUnits))
        {
            ProgressAlongPatrolRouteSorter sorter = new ProgressAlongPatrolRouteSorter();
            unusedUnits.Sort(sorter);
        }
        else if (!Combat.TurnDirector.IsInterleaved)
        {
            SortByDistanceToHostileAscendingSorter sorter = new SortByDistanceToHostileAscendingSorter();
            unusedUnits.Sort(sorter);
            // we actually want to find the one with the highest distance to a hostile, so reverse
            unusedUnits.Reverse();
        }

        AbstractActor currentUnit = GetUnitThatCanReachECM(unusedUnits) ?? unusedUnits[0];
        bool alreadyGoneThisPhase = (
            (currentUnit.BehaviorTree.issuedOrdersOnRound == Combat.TurnDirector.CurrentRound) &&
            (currentUnit.BehaviorTree.issuedOrdersOnPhase == Combat.TurnDirector.CurrentPhase));

        Debug.Assert(!alreadyGoneThisPhase);

        return currentUnit;
    }

    AbstractActor GetUnitThatCanReachECM(List<AbstractActor> unusedUnits)
    {
        if (CanEntireEnemyTeamBeGhosted())
        {
            AbstractActor closestUnit = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < unusedUnits.Count; i++)
            {
                var unit = unusedUnits[i];
                var hostiles = AIUtil.HostilesToUnit(unit);

                for (int j = 0; j < hostiles.Count; j++)
                {
                    var hostileUnit = hostiles[j];
                    if (hostileUnit.HasECMAbilityInstalled)
                    {
                        var lerpValue = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SignalInWeapRngWhenEnemyGhostedWithinMoveDistance).FloatVal;
                        var movementDistance = Mathf.Lerp(unit.MaxWalkDistance, unit.MaxSprintDistance, lerpValue);
                  
                        var range = hostileUnit.AuraComponents[0].componentDef.statusEffects[0].targetingData.range;
                        var distance = Vector3.Distance(unit.CurrentPosition, hostileUnit.CurrentPosition) - range;

                        if (distance <= movementDistance && distance < closestDistance)
                        {
                            closestUnit = unit;
                            closestDistance = distance;
                        }
                    }
                }
            }

            return closestUnit;

        }
            return null;
    }

    bool AnyUnitsHavePatrolRoutes(List<AbstractActor> units)
    {
        for (int unitIndex = 0; unitIndex < units.Count; ++unitIndex)
        {
            AbstractActor actor = units[unitIndex];
            if ((!actor.IsDead) && (ProgressAlongPatrolRouteSorter.GetPatrolRoute(units[unitIndex]) != null))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// If unit is null, first decide what unit should act.
    /// In any case, start a sequence based on the unit's orders, keeping the sequence in currentSequence and the acting
    /// unit in currentUnit.
    /// </summary>
    InvocationMessage getInvocationForCurrentUnit()
    {
        Debug.Assert(currentUnit != null);

        if (currentUnit.BehaviorTree != null)
        {
            List<AbstractActor> enemyActors = currentUnit.Combat.GetAllEnemiesOf(this);
            currentUnit.BehaviorTree.enemyUnits = new List<ICombatant>();
            for (int actorIndex = 0; actorIndex < enemyActors.Count; ++actorIndex)
            {
                AbstractActor enemy = enemyActors[actorIndex];
                ICombatant enemyCombatant = enemy as ICombatant;
                if (enemyCombatant.IsDead)
                {
                    continue;
                }
                if (!currentUnit.BehaviorTree.enemyUnits.Contains(enemyCombatant))
                {
                    currentUnit.BehaviorTree.enemyUnits.Add(enemyCombatant);
                }
            }
        }

        float currentTime = Combat.BattleTechGame.Time;

        // If we've waited too long before making an attack, brace. This should be very rare.
        if (currentTime - planningStartTime > currentUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MaxThinkSeconds).FloatVal)
        {
            Debug.LogError("The AI used too much time. Bracing, instead.");
            AIUtil.LogAI(currentUnit.DisplayName + ": Issuing brace orders after timing out making decision.", currentUnit, HBS.Logging.LoggerNames.AI_TURNORDER);
            currentUnit.BehaviorTree.Reset();
            if (behaviorTreeLogWriter != null)
            {
                behaviorTreeLogWriter.Flush();
            }
            return new ReserveActorInvocation(currentUnit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
        }

        if (IsReservingThisTurn() && AllTeamUnitsCanReserveThisPhase())
        {
            // defer the entire team
            AIUtil.LogAI("Reserving Team on phase " + Combat.TurnDirector.CurrentPhase, currentUnit, HBS.Logging.LoggerNames.AI_TURNORDER);
            if (behaviorTreeLogWriter != null)
            {
                behaviorTreeLogWriter.Flush();
            }
            return new ReserveActorInvocation(this, ReserveActorAction.DEFER, Combat.TurnDirector.CurrentRound);
        }

        float timeBeforeBT = Time.realtimeSinceStartup;
        AIUtil.LogAI(string.Format("evaluating behavior tree for {0} dynamic role: {1}", currentUnit.DisplayName, currentUnit.DynamicUnitRole.ToString()), currentUnit);
        BehaviorTreeResults results;
        try
        {
            results = currentUnit.BehaviorTree.Update();
        }
        catch (System.Exception e)
        {
            Debug.LogError("The AI threw an error while updating the behavior tree. Bracing, instead.");
            Debug.LogError("Exception: " + e.ToString());
            Debug.LogError("BT Log String: " + behaviorTreeLogString);

            if (DebugBridge.TestToolsEnabled)
            {
                string dirName = currentUnit.BehaviorTree
                    .GetBehaviorVariableValue(BehaviorVariableName.String_InfluenceMapCalculationLogDirectory)
                    .StringVal;
                dirName = Path.Combine(Application.persistentDataPath, dirName);
                try
                {
                    System.IO.Directory.CreateDirectory(dirName);
                }
                catch (System.IO.IOException ioException)
                {
                    UnityEngine.Debug.LogError(string.Format("Failed to create directory {0} : exception {1}", dirName,
                        ioException));
                }

                System.DateTime timeNow = System.DateTime.Now;

                string filename = string.Format("updateerror_{0:D4}_{1:D2}_{2:D2}_{3:D2}_{4:D2}_{5:D2}.txt",
                    timeNow.Year, timeNow.Month, timeNow.Day,
                    timeNow.Hour, timeNow.Minute, timeNow.Second);

                string fullFN = System.IO.Path.Combine(dirName, filename);

                // write output to the file
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(fullFN))
                {
                    file.Write(behaviorTreeLogString);
                }

                Debug.LogError("Wrote Update Error trace to " + fullFN);
            }

            if (behaviorTreeLogWriter != null)
            {
                behaviorTreeLogWriter.Flush();
            }
            return new ReserveActorInvocation(currentUnit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
        }

        if (results == null)
        {
            Debug.LogError("The AI returned NULL orders. Bracing, instead.");
            Debug.LogError("BT Log String: " + behaviorTreeLogString);

            if (DebugBridge.TestToolsEnabled)
            {
                string dirName = currentUnit.BehaviorTree
                    .GetBehaviorVariableValue(BehaviorVariableName.String_InfluenceMapCalculationLogDirectory)
                    .StringVal;
                dirName = Path.Combine(Application.persistentDataPath, dirName);
                try
                {
                    System.IO.Directory.CreateDirectory(dirName);
                }
                catch (System.IO.IOException ioException)
                {
                    UnityEngine.Debug.LogError(string.Format("Failed to create directory {0} : exception {1}", dirName,
                        ioException));
                }

                System.DateTime timeNow = System.DateTime.Now;

                string filename = string.Format("nullorders_{0:D4}_{1:D2}_{2:D2}_{3:D2}_{4:D2}_{5:D2}.txt",
                    timeNow.Year, timeNow.Month, timeNow.Day,
                    timeNow.Hour, timeNow.Minute, timeNow.Second);

                string fullFN = System.IO.Path.Combine(dirName, filename);

                // write output to the file
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(fullFN))
                {
                    file.Write(behaviorTreeLogString);
                }

                if (behaviorTreeLogWriter != null)
                {
                    behaviorTreeLogWriter.Flush();
                }
                Debug.LogError("Wrote Null Orders trace to " + fullFN);
            }

            if (behaviorTreeLogWriter != null)
            {
                behaviorTreeLogWriter.Flush();
            }
            return new ReserveActorInvocation(currentUnit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
        }

        float timeAfterBT = Time.realtimeSinceStartup;
        AIUtil.LogAI("Time elapsed in behavior tree: " + (timeAfterBT - timeBeforeBT), currentUnit);
        AIUtil.LogAI(results.debugOrderString, currentUnit, HBS.Logging.LoggerNames.AI_TURNORDER);

        if (results.nodeState == BehaviorNodeState.Success)
        {
            string unitName = currentUnit.DisplayName.Replace(' ', '_');
            string shortGUID = currentUnit.GUID.Substring(0, 4);
            Pilot pilot = currentUnit.GetPilot();
            string pilotName = (pilot != null) ? pilot.Callsign : "(np)";
            string fn = Combat.AILogCache.MakeFilename(string.Format("bh_{0}_{1}_{2}", unitName, pilotName, shortGUID));
            currentUnit.BehaviorTree.behaviorTraceStringBuilder.AppendLine(string.Format("order : {0}", results.debugOrderString));
            Combat.AILogCache.AddLogData(fn, currentUnit.BehaviorTree.behaviorTraceStringBuilder.ToString());
            currentUnit.BehaviorTree.InitBehaviorTraceStringBuilder();

            OrderInfo orderInfo = results.orderInfo;

            //Debug.LogError("AI Order Info: " + orderInfo);

            // It's possible that the entire tree could fail, or somebody be forgetting to issue orders inside
            // the behavior tree. The most reasonable thing to do in this case is to mark this unit done, since
            // they don't have anything better to do.
            if (orderInfo == null)
            {
                Debug.LogError("The AI completed, but did not issue orders. Bracing, instead.");
                Debug.LogError("BT Log String: " + behaviorTreeLogString);

                if (DebugBridge.TestToolsEnabled)
                {
                    string dirName = currentUnit.BehaviorTree
                        .GetBehaviorVariableValue(BehaviorVariableName.String_InfluenceMapCalculationLogDirectory)
                        .StringVal;
                    dirName = Path.Combine(Application.persistentDataPath, dirName);
                    try
                    {
                        System.IO.Directory.CreateDirectory(dirName);
                    }
                    catch (System.IO.IOException ioException)
                    {
                        UnityEngine.Debug.LogError(string.Format("Failed to create directory {0} : exception {1}",
                            dirName, ioException));
                    }

                    System.DateTime timeNow = System.DateTime.Now;

                    string filename = string.Format("badbrace_{0:D4}_{1:D2}_{2:D2}_{3:D2}_{4:D2}_{5:D2}.txt",
                        timeNow.Year, timeNow.Month, timeNow.Day,
                        timeNow.Hour, timeNow.Minute, timeNow.Second);

                    string fullFN = System.IO.Path.Combine(dirName, filename);

                    // write output to the file
                    if (behaviorTreeLogWriter != null)
                    {
                        behaviorTreeLogWriter.Flush();
                    }
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter(fullFN))
                    {
                        file.Write(behaviorTreeLogString);
                    }

                    Debug.LogError("Wrote Bad Brace trace to " + fullFN);
                }

                if (behaviorTreeLogWriter != null)
                {
                    behaviorTreeLogWriter.Flush();
                }
                return new ReserveActorInvocation(currentUnit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
            }
            else
            {
                currentUnit.BehaviorTree.lastOrderDebugString = results.debugOrderString;
                currentUnit.BehaviorTree.issuedOrdersOnRound = Combat.TurnDirector.CurrentRound;
                currentUnit.BehaviorTree.issuedOrdersOnPhase = Combat.TurnDirector.CurrentPhase;
                currentUnit.BehaviorTree.behaviorTraceStringBuilder.AppendLine(string.Format("issued order on round {0} phase {1}", currentUnit.BehaviorTree.issuedOrdersOnRound, currentUnit.BehaviorTree.issuedOrdersOnPhase));
                if (behaviorTreeLogWriter != null)
                {
                    behaviorTreeLogWriter.Flush();
                }
                return makeInvocationFromOrders(currentUnit, orderInfo);
            }
        }
        else if (results.nodeState == BehaviorNodeState.Running)
        {
            AIUtil.LogAI("still running", currentUnit);
        }
        else
        {
            Debug.LogError("Unexpected node state for behavior tree evaluation: " + results.nodeState);
        }
        if (behaviorTreeLogWriter != null)
        {
            behaviorTreeLogWriter.Flush();
        }
        return null;
    }

    InvocationMessage makeInvocationFromOrders(AbstractActor unit, OrderInfo order)
    {
        Debug.Assert(order != null, "order should not be null");

        Mech unitMech = unit as Mech;
        AIUtil.LogAI("****** Invoking order: " + order.OrderType, HBS.Logging.LoggerNames.AI_TURNORDER);
        switch (order.OrderType)
        {
            case OrderType.Move:
                if (!unit.HasMovedThisRound)
                {
                    return makeNormalMoveInvocation(unit, order);
                }
                else
                {
                    LogError("Issuing move orders for unit that has already moved. Passing instead.");
                    return new ReserveActorInvocation(unit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
                }
            case OrderType.SprintMove:
                if (!unit.HasMovedThisRound)
                {
                    return makeSprintMoveInvocation(unit, order);
                }
                else
                {
                    LogError("Issuing sprint move orders for unit that has already moved. Passing instead.");
                    return new ReserveActorInvocation(unit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
                }
            case OrderType.JumpMove:
                if (!unit.HasMovedThisRound)
                {
                    return makeJumpMoveInvocation(unit, order);
                }
                else
                {
                    LogError("Issuing jump move orders for unit that has already moved. Passing instead.");
                    return new ReserveActorInvocation(unit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
                }
            case OrderType.Attack:
                if (!unit.HasFiredThisRound && unit.IsOperational && (unitMech == null || !unitMech.IsProne))
                {
                    return makeAttackInvocation(unit, order);
                }
                else
                {
                    LogError("Issuing attack orders for unit that cannot attack. Passing instead.");
                    return new ReserveActorInvocation(unit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
                }
            case OrderType.Brace:
                if (!unit.HasMovedThisRound)
                {
                    unit.BehaviorTree.IncreaseSprintHysteresisLevel();
                }
                return new ReserveActorInvocation(unit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
            case OrderType.VentCoolant:
                // TODO VentCoolantInvocation?
                return new ReserveActorInvocation(unit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
            case OrderType.Stand:
                return new MechStandInvocation(unitMech);
            case OrderType.StartUp:
                return new MechStartupInvocation(unitMech);
            case OrderType.MultiTargetAttack:
                return makeMultiAttackInvocation(unit, order);
            case OrderType.CalledShotAttack:
                return makeCalledShotAttackInvocation(unit, order);
            case OrderType.ActiveAbility:
                return makeActiveAbilityInvocation(unit, order);
            case OrderType.ClaimInspiration:
                return makeClaimInspirationInvocation(unit);
            case OrderType.ActiveProbe:
                return makeActiveProbeInvocation(order);
            default:
                LogError("Unknown order type: " + order.OrderType + " issued by AI - this is bad. Inserting workaround 'brace' invocation instead.");
                return new ReserveActorInvocation(unit, ReserveActorAction.DONE, Combat.TurnDirector.CurrentRound);
        }
    }

    AbstractActorMovementInvocation makeNormalMoveInvocation(AbstractActor unit, OrderInfo order)
    {
        Debug.Assert(unit != null);
        if (unit == null)
        {
            AIUtil.LogAI("null unit", HBS.Logging.LoggerNames.AI_TURNORDER);
            return null;
        }

        MovementOrderInfo movementOrderInfo = order as MovementOrderInfo;

        MoveType moveType = MoveType.Walking;
        if (movementOrderInfo.IsSprinting)
            moveType = MoveType.Sprinting;
        if (movementOrderInfo.IsReverse)
            moveType = MoveType.Backward;

        unit.Pathing.UpdateAIPath(movementOrderInfo.Destination, movementOrderInfo.LookAt, moveType);

        return new AbstractActorMovementInvocation(unit, false);
    }

    AbstractActorMovementInvocation makeSprintMoveInvocation(AbstractActor unit, OrderInfo order)
    {
        Debug.Assert(unit != null);
        if (unit == null)
        {
            AIUtil.LogAI("null unit", HBS.Logging.LoggerNames.AI_TURNORDER);
            return null;
        }

        Debug.Assert(unit.CanSprint, "Sprint order issued when unit cannot sprint");

        MovementOrderInfo movementOrderInfo = order as MovementOrderInfo;

        unit.Pathing.UpdateAIPath(movementOrderInfo.Destination, movementOrderInfo.LookAt, MoveType.Sprinting);

        return new AbstractActorMovementInvocation(unit, false);
    }

    MechJumpInvocation makeJumpMoveInvocation(AbstractActor unit, OrderInfo order)
    {
        Mech mechUnit = unit as Mech;
        MovementOrderInfo moveOrder = order as MovementOrderInfo;
        Debug.Assert(moveOrder != null);
        Debug.Assert(mechUnit != null);

        Vector3 lookDelta = moveOrder.LookAt - moveOrder.Destination;
        Quaternion lookQuat = Quaternion.LookRotation(lookDelta, Vector3.up);

        return new MechJumpInvocation(mechUnit, moveOrder.Destination, lookQuat, false);
    }

    InvocationMessage makeAttackInvocation(AbstractActor unit, OrderInfo order)
    {
        AttackOrderInfo attackOrder = order as AttackOrderInfo;
        Debug.Assert(attackOrder != null);
        Mech mech = unit as Mech;

        // For melee / DFA sequences, we need to create the whole melee sequence, not just the attack itself
        if (mech != null)
        {
            if ((attackOrder.IsMelee))
            {
                return new MechMeleeInvocation(mech, attackOrder.TargetUnit, attackOrder.Weapons, attackOrder.AttackFromLocation);
            }
            if ((attackOrder.IsDeathFromAbove))
            {
                AbstractActor target = attackOrder.TargetUnit as AbstractActor;
                mech.JumpPathing.SetMeleeTarget(target);
                Vector3 targetPosition = target.CurrentPosition;
                mech.JumpPathing.ResultDestination = attackOrder.AttackFromLocation;
                mech.JumpPathing.ResultAngle = Quaternion.LookRotation((targetPosition - attackOrder.AttackFromLocation), Vector3.up).eulerAngles.y;
                return new MechDFAInvocation(mech, attackOrder.TargetUnit, attackOrder.Weapons);
            }
        }

        AttackInvocation invocation = new AttackInvocation(unit, attackOrder.TargetUnit, attackOrder.Weapons);
        invocation.ventHeatBeforeAttack = attackOrder.VentFirst;

        return invocation;
    }

    InvocationMessage makeMultiAttackInvocation(AbstractActor unit, OrderInfo order)
    {
        MultiTargetAttackOrderInfo attackOrder = order as MultiTargetAttackOrderInfo;
        Debug.Assert(attackOrder != null);
        //Mech mech = unit as Mech;

        AttackOrderInfo firstAttack = attackOrder.SubTargetOrders[0];

        AttackInvocation attack = new AttackInvocation(unit, firstAttack.TargetUnit, firstAttack.Weapons);
        for (int attackIndex = 1; attackIndex < attackOrder.SubTargetOrders.Count; ++attackIndex)
        {
            AttackOrderInfo subAttack = attackOrder.SubTargetOrders[attackIndex];
            for (int si = 0; si < attack.subAttackInvocations.Count; ++si)
            {
                Debug.Assert(subAttack.TargetUnit.GUID != attack.subAttackInvocations[si].targetGUID, "Found duplicate GUIDs in multi-attack order");
            }
            attack.AddSubInvocation(subAttack.TargetUnit, subAttack.Weapons);
        }
        return attack;
    }

    InvocationMessage makeCalledShotAttackInvocation(AbstractActor unit, OrderInfo order)
    {
        CalledShotAttackOrderInfo attackOrder = order as CalledShotAttackOrderInfo;
        Debug.Assert(attackOrder != null);

        AttackInvocation invocation = new AttackInvocation(unit, attackOrder.TargetUnit, attackOrder.Weapons, MeleeAttackType.NotSet, attackOrder.TargetLocation);

        invocation.isMoraleAttack = attackOrder.IsMoraleAttack;

        return invocation;
    }

    InvocationMessage makeActiveAbilityInvocation(AbstractActor unit, OrderInfo order)
    {
        ActiveAbilityOrderInfo abilityOrder = order as ActiveAbilityOrderInfo;
        Debug.Assert(abilityOrder != null);

        ActiveAbilityID abilityID = abilityOrder.GetActiveAbilityID();
        switch (abilityID)
        {
            case ActiveAbilityID.SensorLock:
                return new SensorLockInvocation(unit, abilityOrder.targetUnit);
            default:
                Debug.LogError("unknown Ability ID: " + abilityID);
                return null;
        }
    }

    InvocationMessage makeActiveProbeInvocation(OrderInfo order)
    {
        ActiveProbeOrderInfo activeProbeOrder = order as ActiveProbeOrderInfo;
        Debug.Assert(activeProbeOrder != null);

        return new ActiveProbeInvocation(activeProbeOrder.MovingUnit, activeProbeOrder.Targets);
    }

    InvocationMessage makeClaimInspirationInvocation(AbstractActor unit)
    {
        InspireActorInvocation inspiration = new InspireActorInvocation(unit);
        return inspiration;
    }

    #region IStackSequence
    public bool Contains(IStackSequence sequence) { return this == sequence; }


	void think()
    {
        if (!ThinksOnThisMachine)
        {
            return;
        }

		// See if we can move on.
		CheckWaitingForNotificationCompletionTimeout();

		if (currentUnit != null)
        {
            if (currentUnit.HasActivatedThisRound)
            {
                isComplete = true;
                activationLogger.LogDebug("[think] AI Team isComplete (HasActivatedThisRound)");
                return;
            }
        }

        if ((pendingInvocations != null) && (pendingInvocations.Count != 0))
        {
            if (!WaitingForNotificationCompletion)
            {
                for (int invocationIndex = 0; invocationIndex < pendingInvocations.Count; ++invocationIndex)
                {
                    InvocationMessage invMsg = pendingInvocations[invocationIndex];
                    if ((invMsg as ReserveActorInvocation) != null)
                    {
                        isComplete = true;
                        activationLogger.LogDebug("[think] AI Team isComplete (ReserveActorInvocation)");
                    }
                    Combat.MessageCenter.PublishMessage(invMsg);
                }
                pendingInvocations.Clear();
            }
            return;
        }

        InvocationMessage invocation = getInvocationForCurrentUnit();

        if (invocation != null)
        {
            if (WaitingForNotificationCompletion)
            {
                pendingInvocations.Add(invocation);
            }
            else
            {
                if ((invocation as ReserveActorInvocation) != null)
                {
                    isComplete = true;
                    activationLogger.LogDebug("[think] AI Team isComplete (ReserveActorInvocation)");
                }

                AIUtil.LogAI("AI sending reserve invocation with current phase: " + Combat.TurnDirector.CurrentPhase, HBS.Logging.LoggerNames.AI_TURNORDER);
                Combat.MessageCenter.PublishMessage(invocation);
            }
        }

        float elapsedTime = Combat.BattleTechGame.Time - planningStartTime;
        AIUtil.LogAI(string.Format("AI thinking done after {0} seconds", elapsedTime));
    }

	private float elapsedWaitingForNotificationCompletionTime = 0f;
	private static float elapsedWaitingForNotificationCompletionTimeout = 5f;
	private void CheckWaitingForNotificationCompletionTimeout()
	{
		// If we're waiting for the notification completion
		if (WaitingForNotificationCompletion)
		{
			// bump our time and check the timeout.
			elapsedWaitingForNotificationCompletionTime += Time.deltaTime;
			if (elapsedWaitingForNotificationCompletionTime > elapsedWaitingForNotificationCompletionTimeout)
			{
				WaitingForNotificationCompletion = false;
				elapsedWaitingForNotificationCompletionTime = 0f;
				activationLogger.LogWarning(string.Format("WaitingForNotificationCompletion timed out after {0} seconds", elapsedWaitingForNotificationCompletionTimeout));
			}
		}
		// If not, reset our elapsed time
		else
		{
			elapsedWaitingForNotificationCompletionTime = 0f;
		}
	}

	public bool IsComplete
    {
        get
        {
            return isComplete;
        }
    }

    public int SequenceGUID { get; set; }
    public int MessageIndex { get; set; }
    public int RootSequenceGUID { get; set; }
    public bool IsValid { get; set; }
    public bool IsPaused { get; set; }

    public void OnAdded() { }
    public void OnUpdate()
    {
        think();
    }

    /// <summary>
    /// Call this function when you're starting an interrupt phase. It sets the
    /// runningInterruptPhase flag and clears the selected unit if there was one.
    /// </summary>
    public void InitForInterruptPhase()
    {
        runningInterruptPhase = true;
        ClearInterruptUnit();
    }

    /// <summary>
    /// Clears the selected unit, and prevents the auto-complete at the end
    /// of moving a unit that the AITeam normally performs.
    /// </summary>
    public void ClearInterruptUnit()
    {
        currentUnit = null;
        isComplete = false;
    }

    /// <summary>
    /// The interrupt version of selectCurrentUnit.
    /// </summary>
    /// <returns>An interrupting unit if there is one, otherwise null.</returns>
    AbstractActor selectCurrentInterruptUnit()
    {
        List<AbstractActor> unusedUnits = GetUnusedInterruptUnits();
        if (unusedUnits.Count == 0)
        {
            return null;
        }
        return unusedUnits[0];
    }

    /// <summary>
    /// This is the interrupt version of OnUpdate. InterruptPhaseSequence calls this
    /// from it's position in the stack. Once we've selected an interrupting unit to
    /// move, we call the regular OnUpdate.
    /// </summary>
    /// <returns>True when the AITeam is done moving all of its interrupt units.</returns>
    public bool OnInterruptUpdate()
    {
        // If we don't have an interrupt unit acting...
        if (currentUnit == null)
        {
            // Try to find one.
            currentUnit = selectCurrentInterruptUnit();
            planningStartTime = Combat.BattleTechGame.Time;
            behaviorTreeLogString = "";

            // If there aren't any more left, we're done.
            if (currentUnit == null)
            {
                runningInterruptPhase = false;
            }
            else
            {
                currentUnit.IsInterruptActor = false;
            }
            UpdateAloneStatus(currentUnit);
        }
        // Otherwise, we have a current unit and we should update his actions.
        else
        {
            OnUpdate();
        }

        // If we finished our
        if (isComplete)
            ClearInterruptUnit();

        return !runningInterruptPhase;
    }

    void UpdateAloneStatus(AbstractActor unit)
    {
        int thisRoundNumber = unit.Combat.TurnDirector.CurrentRound;
        BehaviorVariableValue roundNumberValue = new BehaviorVariableValue(thisRoundNumber);
        if (AIUtil.IsExposedToHostileFireAndAlone(unit, unit.Combat))
        {
            unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Int_LastAloneRoundNumber, roundNumberValue);
        }
        else
        {
            unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Int_LastNotAloneRoundNumber, roundNumberValue);
        }
    }

    public void OnSuspend() { IsPaused = true; }

    public void OnResume()
    {
        IsPaused = false;
        //think();
    }

    public void OnComplete()
    {
        if (CompletedCallback != null)
            CompletedCallback();
    }

    public void OnCanceled()
    {
    }

    public SequenceFinished CompletedCallback { get; set; }

    public void OnUndo() { }
    public bool IsValidMultiSequenceChild { get { return false; } }
    public bool AutoCombineSameType { get { return false; } }
    public System.Type DesiredParentType { get { return null; } }
    public bool IsParallelInterruptable { get { return false; } }
    public bool IsCancelable { get { return false; } }

    // The AI Team sequence shouldn't stop the mission from ending.
    public bool CanEndMission()
    {
        return true;
    }

    #endregion IStackSequence

    public override void Hydrate(CombatGameState loadedState, SerializableReferenceContainer RefContainer)
    {
        base.Hydrate(loadedState, RefContainer);
    }

    public override void Dehydrate(SerializableReferenceContainer RefContainer)
    {
        base.Dehydrate(RefContainer);
    }

    // Providing a test for TaggedUnitTargetPriorityRecord
    [ScriptBinding("testTaggedUnitTargetPriority")]
    public static void TestTaggedUnitTargetPriority()
    {
        List<ITaggedItem> teams = UnityGameInstance.BattleTechGame.Combat.ItemRegistry.GetObjectsOfType(TaggedObjectType.Team);

        foreach (ITaggedItem teamItem in teams)
        {
            AITeam aiTeam = teamItem as AITeam;

            if (aiTeam == null)
            {
                continue;
            }

            string[] dummyTags = { "dummy" };
            HBS.Collections.TagSet dummyTagSet = new HBS.Collections.TagSet(dummyTags);
            TaggedUnitTargetPriorityRecord dummyRecord = new TaggedUnitTargetPriorityRecord(dummyTagSet, 1, true);
            aiTeam.BehaviorVariables.TargetPriorityRecords.Add(dummyRecord);
        }
    }
}
