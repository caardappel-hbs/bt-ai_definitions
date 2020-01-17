using UnityEngine;
using System.Collections.Generic;
using BattleTech;
using BattleTech.Serialization;

/*
 * Node ideas:
 *  Cooldown Decorator - succeeds if its child succeeds, but then fails until a cooldown timer expires
 *  Randomized selector / sequencer
 *  Parallel selector / sequencer
 *  Data-driven dynamic selector / sequencer
 */

[SerializableEnum("BehaviorTreeIDEnum")]
public enum BehaviorTreeIDEnum
{
    INVALID_UNSET = -1,
    DoNothingTree = 0,
    CoreAITree = 1,
    //DefensiveAITree = 2,
    DumbAITree = 3,
    FollowRouteAITree = 4,
    FleeAITree = 5,
    // TurretAITree = 6,
    // InfluenceMapTestTree = 7,
    PatrolAndShootAITree = 8,
    TutorialSprintAITree = 9,
    FollowRouteOppFireAITree = 10,
    PanzyrAITree = 11,
}

public enum BehaviorNodeState
{
    Failure = 0,
    Success = 1,
    Running = 2,
    Ready = 3,
};

public enum OrderType // with a numeric priority that might be used by StaticOrderOrder, highest first
{
    Undefined = 0,
    // unused: Pass = 1,
    // unused: Melee = 2,
    Move = 3,
    JumpMove = 4,
    SprintMove = 5,
    Attack = 6,
    Stand = 7,
    StartUp = 8,
    Brace = 9,
    MultiTargetAttack = 10,
    CalledShotAttack = 11,
    ActiveAbility = 12,
    ClaimInspiration = 13,
    VentCoolant = 14,
    ActiveProbe = 15,

    // next available = 16
}

public enum ActiveAbilityID
{
    Undefined = 0,
    SensorLock = 1,

    // next available = 2
}

public enum AIDebugContext
{
    Undefined = 0,
    Shoot = 1,

    // next available = 2
}

public class AIUtil
{
    static public List<ICombatant> GetVisibleUnitsForUnit(AbstractActor unit)
    {
        List<ICombatant> units = new List<ICombatant>();

        List<string> magicallyVisibleUnitGUIDs = unit.GetMagicallyVisibleUnitGUIDs();
        for (int mvuIndex = 0; mvuIndex < magicallyVisibleUnitGUIDs.Count; ++mvuIndex)
        {
            ITaggedItem item = unit.Combat.ItemRegistry.GetItemByGUID(magicallyVisibleUnitGUIDs[mvuIndex]);
            if (item != null)
            {
                ICombatant targetUnit = item as ICombatant;
                if ((targetUnit != null) && (!targetUnit.IsDead))
                {
                    units.Add(targetUnit);
                }
            }
        }

        List<AbstractActor> reallyVisibleUnits = unit.GetVisibleEnemyUnits();

        for (int rvuIndex = 0; rvuIndex < reallyVisibleUnits.Count; ++rvuIndex)
        {
            ICombatant targetUnit = reallyVisibleUnits[rvuIndex];
            if ((!units.Contains(targetUnit)) && (!targetUnit.IsDead))
            {
                units.Add(targetUnit);
            }
        }

        return units;
    }

    public static bool CanUnitGetBehindUnit(AbstractActor targetUnit, Vector3 targetPosition, Quaternion targetRotation, AbstractActor hostileUnit)
    {
        if ((targetUnit as Turret) != null)
        {
            return false;
        }

        Vector3 movingUnitForward = targetUnit.CurrentRotation * Vector3.forward;

        float walkDist = hostileUnit.MaxWalkDistance;

        float maxRange = 0.0f;

        for (int weaponIndex = 0; weaponIndex < hostileUnit.Weapons.Count; ++weaponIndex)
        {
            Weapon w = hostileUnit.Weapons[weaponIndex];
            if (w.CanFire)
            {
                maxRange = Mathf.Max(maxRange, w.MaxRange);
            }
        }

        // We now know the range of attacks [R] we wish to consider - imagine a
        // square (diamond, if you prefer) with one vertex at the
        // movingUnit's location, and two sides along the edges of the rear
        // facing quadrant for that unit. The side length of this square is R.
        // Really, what we care most about is the quarter circle sector with
        // radius R, but calculating distance to that is tricky.
        // So, we've got a square of length R, we can draw a circle
        // external to that square, touching the four corners, which will
        // have a radius R' of R * sqrt(2)/2, and centered a distance R'
        // behind the moving unit.
        // [phew, almost there]
        // Now, extend that circle by W, the walking distance of a hostile,
        // which will give us a circle within which, that hostile can walk
        // behind our unit and execute an attack. Granted, this is overly
        // conservative, but that's what we want.

        float rPrime = maxRange * Mathf.Sqrt(2.0f) / 2.0f;
        float bigCircleRadius = walkDist + rPrime;

        Vector3 center = targetUnit.CurrentPosition - movingUnitForward * rPrime;
        float distance = (hostileUnit.CurrentPosition - center).magnitude;
        if (distance <= bigCircleRadius)
        {
            return true;
        }

        // Now consider melee - if the hostile can walk to us, it can melee us.
        if ((targetUnit.CurrentPosition - hostileUnit.CurrentPosition).magnitude < hostileUnit.MaxWalkDistance)
        {
            return true;
        }

        return false;
    }

    public static bool CanUnitGetBehindUnit(AbstractActor targetUnit, AbstractActor hostileUnit)
    {
        return CanUnitGetBehindUnit(targetUnit, targetUnit.CurrentPosition, targetUnit.CurrentRotation, hostileUnit);
    }

    static public List<ICombatant> GetDetectedUnitsForUnit(AbstractActor unit)
    {
        List<ICombatant> units = new List<ICombatant>();

        List<string> magicallyVisibleUnitGUIDs = unit.GetMagicallyVisibleUnitGUIDs();
        for (int mvuIndex = 0; mvuIndex < magicallyVisibleUnitGUIDs.Count; ++mvuIndex)
        {
            ITaggedItem item = unit.Combat.ItemRegistry.GetItemByGUID(magicallyVisibleUnitGUIDs[mvuIndex]);
            if (item != null)
            {
                ICombatant targetUnit = item as ICombatant;
                if ((targetUnit != null) && (!targetUnit.IsDead))
                {
                    units.Add(targetUnit);
                }
            }
        }

        List<AbstractActor> reallyDetectedUnits = unit.GetDetectedEnemyUnits();

        for (int rvuIndex = 0; rvuIndex < reallyDetectedUnits.Count; ++rvuIndex)
        {
            ICombatant targetUnit = reallyDetectedUnits[rvuIndex];
            if ((!units.Contains(targetUnit)) && (!targetUnit.IsDead))
            {
                units.Add(targetUnit);
            }
        }

        return units;
    }

    static public float GetAcceptableHeatLevelForMech(Mech mech)
    {
        float heatFrac = mech.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_AcceptableHeatLevel).FloatVal;

        if (mech.OverheatWillCauseLocationLoss())
        {
            // if overheating would lose a limb, clamp it to a non-overheating value
            heatFrac = Mathf.Min(1.9f, heatFrac);
        }

        if (heatFrac < 2.0f)
        {
            float param = heatFrac * 0.5f; // mapping this to 0..1
            return param * mech.OverheatLevel;
        }
        else if (heatFrac < 3.0f)
        {
            // between 2.0 and 3.0
            float param = heatFrac - 2.0f;

            // return linear interpolation between overheated and max heat
            int hl2 = mech.OverheatLevel;
            int mh = mech.MaxHeat;

            return param * (mh - hl2) + hl2;
        }
        else
        {
            // clamp to max heat
            return mech.MaxHeat;
        }
    }

    static public float GetAcceptableHeatLevelForMechWithVentCoolant(Mech mech)
    {
        float heatFrac = mech.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_VentCoolantHeatThreshold).FloatVal;

        if (mech.OverheatWillCauseLocationLoss())
        {
            // if overheating would lose a limb, clamp it to a non-overheating value
            heatFrac = Mathf.Min(1.9f, heatFrac);
        }

        if (heatFrac < 2.0f)
        {
            float param = heatFrac * 0.5f; // mapping this to 0..1
            return param * mech.OverheatLevel;
        }
        else if (heatFrac < 3.0f)
        {
            // between 2.0 and 3.0
            float param = heatFrac - 2.0f;

            // return linear interpolation between overheated and max heat
            int hl2 = mech.OverheatLevel;
            int mh = mech.MaxHeat;

            return param * (mh - hl2) + hl2;
        }
        else
        {
            // clamp to max heat
            return mech.MaxHeat;
        }
    }

    static public int HeatForAttack(List<Weapon> weaponList)
    {
        int heat = 0;
        for (int weaponIndex = 0; weaponIndex < weaponList.Count; ++weaponIndex)
        {
            heat += (int)weaponList[weaponIndex].HeatGenerated;
        }
        return heat;
    }

    public enum AttackType
    {
        None = -1,

        Shooting = 0,
        Melee = 1,
        DeathFromAbove = 2,

        Count = 3,
    }

    static public float ExpectedDamageForAttack(AbstractActor unit, AttackType attackType, List<Weapon> weaponList, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus, AbstractActor unitForBVContext)
    {
        Mech mech = unit as Mech;
        AbstractActor targetActor = target as AbstractActor;

        if (attackType == AttackType.Melee)
        {
            if ((targetActor == null) || (mech == null) || (mech.Pathing.GetMeleeDestsForTarget(targetActor).Count == 0))
            {
                return 0.0f;
            }
        }

        if (attackType == AttackType.DeathFromAbove)
        {
            if ((targetActor == null) || (mech == null) || (mech.JumpPathing.GetDFADestsForTarget(targetActor).Count == 0))
            {
                return 0.0f;
            }
        }

        bool targetIsUnsteady = ((targetActor != null) && (targetActor.IsUnsteady));
        bool targetIsBraced = ((targetActor != null) && (targetActor.BracedLastRound));
        bool targetIsEvasive = ((targetActor != null) && (targetActor.IsEvasive));

        // Blow Quality (from guard, cover)
        MeleeAttackType meleeAttackType =
            attackType == AttackType.Melee ? MeleeAttackType.MeleeWeapon :
            (attackType == AttackType.DeathFromAbove ? MeleeAttackType.DFA : MeleeAttackType.NotSet);

        AttackImpactQuality quality = AttackImpactQuality.Solid;
        if (weaponList.Count > 0)
        {
            quality = unit.Combat.ToHit.GetBlowQuality(unit, attackPosition, weaponList[0], target, meleeAttackType, unit.IsUsingBreachingShotAbility(weaponList.Count));
        }

        float targetGuardMultiplier = unit.Combat.ToHit.GetBlowQualityMultiplier(quality);

        float expectedDamage = 0.0f;
        for (int weaponIndex = 0; weaponIndex < weaponList.Count; ++weaponIndex)
        {
            Weapon weapon = weaponList[weaponIndex];
            int numShots = weapon.ShotsWhenFired;
            float toHit = weapon.GetToHitFromPosition(target, 1, attackPosition, targetPosition, true, targetIsEvasive);

            float attackTypeMultiplier = 1.0f;

            switch (attackType)
            {
                case AttackType.Shooting:
                    attackTypeMultiplier = unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                    break;
                case AttackType.Melee:
                    {
                        Mech mechTarget = target as Mech;
                        Mech mechUnit = unit as Mech;
                        if (useRevengeBonus && (mechTarget != null) && (mechUnit != null))
                        {
                            if (mechUnit.IsMeleeRevengeTarget(mechTarget))
                            {
                                attackTypeMultiplier += unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MeleeRevengeBonus).FloatVal;
                            }
                        }

                        if ((mechUnit != null) && (weapon == mechUnit.MeleeWeapon))
                        {
                            attackTypeMultiplier = unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MeleeDamageMultiplier).FloatVal;
                            if (targetIsUnsteady)
                            {
                                attackTypeMultiplier += unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MeleeVsUnsteadyTargetDamageMultiplier).FloatVal;
                            }
                        }
                        else
                        {
                            attackTypeMultiplier = unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                        }
                    }
                    break;
                case AttackType.DeathFromAbove:
                    {
                        Mech mechUnit = unit as Mech;
                        if ((mechUnit != null) && (weapon == mechUnit.DFAWeapon))
                        {
                            attackTypeMultiplier = unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_DFADamageMultiplier).FloatVal;
                        }
                        else
                        {
                            attackTypeMultiplier = unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
                        }
                    }
                    break;
                default:
                    Debug.LogError("unknown attack type: " + attackType);
                    break;
            }

            // When using precision strike, assume all shots have a 100% chance to hit.
            if ((attackType == AttackType.Shooting) &&
                (weaponList.Count == 1) &&
                (unit.HasBreachingShotAbility))
            {
                toHit = 1.0f;
            }

            float damagePerShot = weapon.DamagePerShotFromPosition(meleeAttackType, attackPosition, target);
            float heatDamagePerShot = unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_HeatToDamageRatio).FloatVal * (weapon.HeatDamagePerShot);
            float unsteadinessVirtualDamagePerShot = 0.0f;
            if (targetIsUnsteady)
            {
                unsteadinessVirtualDamagePerShot = weapon.Instability() * unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_UnsteadinessToVirtualDamageConversionRatio).FloatVal;
            }

            float bonusMeleeDamagePerShot = 0.0f;
            bonusMeleeDamagePerShot += ((attackType == AttackType.Melee) && targetIsBraced) ? damagePerShot * unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingBracedTargets).FloatVal : 0.0f;
            bonusMeleeDamagePerShot += ((attackType == AttackType.Melee) && targetIsEvasive) ? damagePerShot * unitForBVContext.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MeleeBonusMultiplierWhenAttackingEvasiveTargets).FloatVal : 0.0f;

            float weaponExpectedDamage = numShots * toHit * (damagePerShot + heatDamagePerShot + unsteadinessVirtualDamagePerShot + bonusMeleeDamagePerShot);

            expectedDamage += weaponExpectedDamage * attackTypeMultiplier;
        }

        return expectedDamage * targetGuardMultiplier;
    }

    static public float ExpectedDamageForMeleeAttack(Mech attacker, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus)
    {
        return ExpectedDamageForMeleeAttackUsingUnitsBVs(attacker, target, attackPosition, targetPosition, useRevengeBonus, attacker);
    }

    static public float ExpectedDamageForMeleeAttackUsingUnitsBVs(Mech attacker, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool useRevengeBonus, AbstractActor unitForBVContext)
    {
        List<Weapon> weapons = new List<Weapon>();
        weapons.Add(attacker.MeleeWeapon);

        Mech attackerMech = attacker as Mech;
        if (attackerMech == null)
        {
            return 0.0f;
        }

        for (int wi = 0; wi < attackerMech.Weapons.Count; ++wi)
        {
            Weapon w = attackerMech.Weapons[wi];
            if (w.CanFire && (w.WeaponCategoryValue.CanUseInMelee))
            {
                weapons.Add(w);
            }
        }

        return ExpectedDamageForAttack(attacker, AttackType.Melee, weapons, target, attackPosition, targetPosition, useRevengeBonus, unitForBVContext);
    }

    static public float LowestHitChance(List<Weapon> weaponList, ICombatant target, Vector3 attackPosition, Vector3 targetPosition, bool targetIsEvasive)
    {
        float lowestChance = float.MaxValue;
        for (int weaponIndex = 0; weaponIndex < weaponList.Count; ++weaponIndex)
        {
            Weapon weapon = weaponList[weaponIndex];
            float toHit = weapon.GetToHitFromPosition(target, 1, attackPosition, targetPosition, true, targetIsEvasive);
            lowestChance = Mathf.Min(lowestChance, toHit);
        }
        return lowestChance;
    }

    static public void LogAI(string info, string loggerName = HBS.Logging.LoggerNames.AI_DECISIONMAKING)
    {
        HBS.Logging.ILog logger = HBS.Logging.Logger.GetLogger(loggerName);
        if (logger.IsDebugEnabled)
        {
            logger.LogDebug(info);
        }
    }

    static public void LogAI(string info, AITeam team, string loggerName = HBS.Logging.LoggerNames.AI_DECISIONMAKING)
    {
        LogAI(info, loggerName);
        team.Log(info);
    }

    static public void LogAI(string info, AbstractActor unit, string loggerName = HBS.Logging.LoggerNames.AI_DECISIONMAKING)
    {
        AITeam team = unit.team as AITeam;
        if (team != null)
        {
            string prefixedMsg = string.Format("<{0}> - {1}", unit.DisplayName, info);
            LogAI(prefixedMsg, team, loggerName);
        }
        else
        {
            LogAI(info, loggerName);
        }

        unit.BehaviorTree.behaviorTraceStringBuilder.AppendLine(info);
    }

    static public void ShowAIDebugUnitFloatie(ICombatant combatant, string msg)
    {
        combatant.Combat.MessageCenter.PublishMessage(new FloatieMessage(combatant.GUID, combatant.GUID, msg, FloatieMessage.MessageNature.AIDebug));
    }

    static public float DistanceToClosestEnemy(AbstractActor unit, Vector3 position)
    {
        float closestDistance = -1.0f;

        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant enemy = unit.BehaviorTree.enemyUnits[enemyIndex];
            float distance = (enemy.CurrentPosition - position).magnitude;
            if ((closestDistance < 0.0f) ||
                (distance < closestDistance))
            {
                closestDistance = distance;
            }
        }
        return closestDistance;
    }

    /// <summary>
    /// Can a given unit (or any of their allies) see a given target from their current position?
    /// </summary>
    static public bool UnitHasVisibilityToTargetFromCurrentPosition(AbstractActor attacker, ICombatant target)
    {
        return attacker.VisibilityToTargetUnit(target) == VisibilityLevel.LOSFull;
    }

    /// <summary>
    /// Can a given unit (or any of their allies) detect a target from their current position?
    /// </summary>
    static public bool UnitHasDetectionToTargetFromCurrentPosition(AbstractActor attacker, ICombatant target)
    {
        return attacker.VisibilityToTargetUnit(target) == VisibilityLevel.LOSFull;
    }

    /// <summary>
    /// Can a given unit (or any of their allies) see a given target from a projected position?
    /// This takes into account the sightline of all allies, takes out the attacker's current sight info, and recalculates from the new position.
    /// </summary>
    static public bool UnitHasVisibilityToTargetFromPosition(AbstractActor attacker, ICombatant target, Vector3 position, List<AbstractActor> allies)
    {
        // if any ally has LOS, we don't need to recalculate ours, which is fairly expensive
        for (int i = 0; i < allies.Count; ++i)
        {
            if (allies[i].VisibilityCache.VisibilityToTarget(target).VisibilityLevel == VisibilityLevel.LOSFull)
            {
                return true;
            }
        }

        // no allies have visibility; now check for the new position and see if we have visibility or not.
        VisibilityLevel visLevel = attacker.Combat.LOS.GetVisibilityToTargetWithPositionsAndRotations(attacker, position, target);

        return visLevel == VisibilityLevel.LOSFull || visLevel == VisibilityLevel.BlipGhost;
    }

    static bool isTargetThreatHighest(AbstractActor movingUnit, AbstractActor target, List<AbstractActor> allTargets)
    {
        float targetThreat = AIThreatUtil.GetThreatRatio(movingUnit, target);

        for (int targetIndex = 0; targetIndex < allTargets.Count; ++targetIndex)
        {
            AbstractActor otherTarget = allTargets[targetIndex];
            if ((!otherTarget.team.IsEnemy(movingUnit.team)) ||
                (otherTarget.IsDead))
            {
                continue;
            }
            float otherThreatRatio = AIThreatUtil.GetThreatRatio(movingUnit, otherTarget);
            if (otherThreatRatio > targetThreat)
            {
                return false;
            }
        }
        return true;
    }

    static public bool IsEveryEnemyGhosted(AbstractActor unit, List<ICombatant> enemies)
    {
        bool anEnemyIsGhosted = false;
        bool thereAreBlips = false;

        for (int i = 0; i < enemies.Count; ++i)
        {
            AbstractActor enemy = enemies[i] as AbstractActor;

            var visLevel = unit.team.VisibilityToTarget(enemy);

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
            unit.team.enemyTeamMayHaveECM = true;
        }

        return anEnemyIsGhosted || (unit.team.enemyTeamMayHaveECM && thereAreBlips);
    }

    static public bool HasAbilityAvailable(AbstractActor unit, ActiveAbilityID abilityID)
    {
        Pilot p = unit.GetPilot();

        if (p != null)
        {
            Ability sensorLockAbility = p.GetActiveAbility(abilityID);
            return (sensorLockAbility != null) && (sensorLockAbility.IsAvailable);
        }

        return false;
    }

    static public bool EvaluateSensorLockQuality(AbstractActor movingUnit, ICombatant target, out float quality, bool ignoreActivation = false)
    {
        AbstractActor targetUnit = target as AbstractActor;

        if ((targetUnit == null) || (movingUnit.DynamicUnitRole == UnitRole.LastManStanding) || (!targetUnit.HasActivatedThisRound && !ignoreActivation) || (targetUnit.IsDead))
        {
            quality = float.MinValue;
            return false;
        }

        float range = (targetUnit.CurrentPosition - movingUnit.CurrentPosition).magnitude;
        float sensorRange = movingUnit.Combat.LOS.GetAdjustedSensorRange(movingUnit, targetUnit);

        // If the target is too far away, then don't sensor lock
        if (range > sensorRange)
        {
            quality = float.MinValue;
            return false;
        }

        // If the target is not our enemy, then don't sensor lock
        if (!movingUnit.team.IsEnemy(target.team))
        {
            quality = float.MinValue;
            return false;
        }

        // If the target cannot productively be sensor locked, then don't.
        if (targetUnit.HasSensorLockEvasiveImmunity)
        {
            quality = float.MinValue;
            return false;
        }

        /* Make a list of my friends such that:
         *    have LOF to this target AND
         *    haven't gone yet this round AND
         *    consider this target their highest threat
         */

        List<AbstractActor> friendsWhoWillShootAtThisTarget = new List<AbstractActor>();
        List<AbstractActor> hostileActors = new List<AbstractActor>();
        List<AbstractActor> friendlyActors = new List<AbstractActor>();

        for (int actorIndex = 0; actorIndex < movingUnit.Combat.AllActors.Count; ++actorIndex)
        {
            AbstractActor maybeHostileActor = movingUnit.Combat.AllActors[actorIndex];
            if ((!maybeHostileActor.IsDead) && (maybeHostileActor.team.IsEnemy(movingUnit.team)))
            {
                hostileActors.Add(maybeHostileActor);
            }
            if ((!maybeHostileActor.IsDead) && (maybeHostileActor.team.IsFriendly(movingUnit.team)))
            {
                friendlyActors.Add(maybeHostileActor);
            }
        }

        for (int friendIndex = 0; friendIndex < friendlyActors.Count; ++friendIndex)
        {
            AbstractActor friend = friendlyActors[friendIndex];
            if ((friend == movingUnit) ||
                (friend.HasActivatedThisRound))
            {
                continue;
            }

            // check LOF to the target
            bool hasLOF = false;
            for (int weaponIndex = 0; weaponIndex < friend.Weapons.Count; ++weaponIndex)
            {
                Weapon w = friend.Weapons[weaponIndex];
                if (w.CanFire && friend.HasLOFToTargetUnit(target, w))
                {
                    hasLOF = true;
                    break;
                }
            }
            if (!hasLOF)
            {
                continue;
            }

            // check to see if their evaluation of the target's threat is highest
            if (!isTargetThreatHighest(movingUnit, targetUnit, hostileActors))
            {
                continue;
            }

            // success, add to our list
            friendsWhoWillShootAtThisTarget.Add(friend);
        }

        if (friendsWhoWillShootAtThisTarget.Count == 0)
        {
            quality = float.MinValue;
            return false;
        }

        /* Determine the value of sensor locking this target:
         * SLExpDmg = exp dmg, evaluated with SL modifier for my friends list (above)
         * NSLExpDmg = exp dmg for me and those same friends, evaluated with no (new) SL modifier.
         * SLValue = SLExpDmg - NSLExpDmg
         */

        float sensorLockExpectedDamage = 0.0f;
        AbstractActor targetActor = target as AbstractActor;
        bool canSeeTarget = movingUnit.Combat.LOS.GetVisibilityToTarget(movingUnit, target) == VisibilityLevel.LOSFull || ((targetActor != null) && (targetActor.IsSensorLocked));
        float nonSensorLockExpectedDamage = canSeeTarget ? AIUtil.CalcMaxExpectedDamageToHostile(movingUnit, target, movingUnit.IsFuryInspired, false) : 0.0f; // no SL modifier

        // Make a disposable version of the target's stat collection

        StatCollection originalTargetStats = targetUnit.StatCollection;
        StatCollection scratchTargetStats = targetUnit.StatCollection.MakeClone();

        int originalPips = targetUnit.EvasivePipsCurrent;

        targetUnit.StatCollection = scratchTargetStats;

        // fake the sensor lock effects
        CombatGameState combat = movingUnit.Combat;
        List<Effect> effects = combat.EffectManager.CreateEffect(combat.Constants.Visibility.SensorLockSingleStepEffect, "hypothetical_SensorLock", -1, movingUnit, targetUnit, new WeaponHitInfo(), -1, true);
        if (combat.Constants.ToHit.SensorLockStripsEvasivePips)
        {
            for (int i = 0; i < combat.Constants.ToHit.SensorLockPipsStripped; ++i)
            {
                targetUnit.ConsumeEvasivePip(false);
            }
        }

        for (int friendIndex = 0; friendIndex < friendsWhoWillShootAtThisTarget.Count; ++friendIndex)
        {
            AbstractActor friend = friendsWhoWillShootAtThisTarget[friendIndex];
            sensorLockExpectedDamage += AIUtil.CalcExpectedDamageToHostileAtPoints(friend, friend.CurrentPosition, target, target.CurrentPosition, friend.IsFuryInspired); // with SL modifier
        }

        /* TODO(somebody after Dave) - So, this function is evaluating the
         * "quality" of a hypothetical sensor lock on a target, and as of
         * August 14th, 2018, there's some discussion about a modification to
         * Sensor Lock that also gives two levels of Sensors Impaired to the
         * target, which would make it useful to sensor lock targets with lots
         * of damage dealing capacity.
         *
         * So, what one could do to make that work is first (before entering
         * into the hypothetical world of sensor lock/sensors impaired) figure
         * out the target hostile to targetUnit for which targetUnit can do the
         * most expected damage. Then, inside the hypothetical sensors impaired
         * world, calculate the expected damage to that same unit. Subtract
         * hypothetical damage from outside damage, and you should get an
         * amount of damage saved by the application of Sensor Locked. This can
         * be added directly to the Sensor Lock quality, or maybe scaled.
         */


        // reset things to the original state
        for (int effectIndex = 0; effectIndex < effects.Count; ++effectIndex)
        {
            combat.EffectManager.CancelEffect(effects[effectIndex], true);
        }
        targetUnit.StatCollection = originalTargetStats;
        targetUnit.EvasivePipsCurrent = originalPips;

        for (int friendIndex = 0; friendIndex < friendsWhoWillShootAtThisTarget.Count; ++friendIndex)
        {
            AbstractActor friend = friendsWhoWillShootAtThisTarget[friendIndex];
            canSeeTarget = friend.Combat.LOS.GetVisibilityToTarget(friend, target) == VisibilityLevel.LOSFull || ((targetActor != null) && (targetActor.IsSensorLocked));
            nonSensorLockExpectedDamage += canSeeTarget ? AIUtil.CalcExpectedDamageToHostileAtPoints(friend, friend.CurrentPosition, target, target.CurrentPosition, friend.IsFuryInspired) : 0.0f;  // without a SL modifier
        }

        quality = sensorLockExpectedDamage - nonSensorLockExpectedDamage;
        return true;
    }

    public static List<AbstractActor> HostilesToUnit(AbstractActor unit)
    {
        List<AbstractActor> hostileActors = new List<AbstractActor>();

        for (int actorIndex = 0; actorIndex < unit.Combat.AllActors.Count; ++actorIndex)
        {
            AbstractActor maybeHostileActor = unit.Combat.AllActors[actorIndex];
            if ((!maybeHostileActor.IsDead) && (maybeHostileActor.team.IsEnemy(unit.team)))
            {
                hostileActors.Add(maybeHostileActor);
            }
        }
        return hostileActors;
    }

    public static bool IsPositionWithinLanceSpread(AbstractActor unit, List<AbstractActor> lancemates, Vector3 destination)
    {
        if (lancemates == null)
        {
            return true;
        }

        float spread = 0;
        if (unit.Combat.TurnDirector.IsInterleaved)
        {
            spread = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_InterleavedLanceSpreadDistance).FloatVal;
            float minMoveDist = float.MaxValue;
            for (int lmi = 0; lmi < lancemates.Count; ++lmi)
            {
                AbstractActor lanceMate = lancemates[lmi];
                minMoveDist = Mathf.Min(minMoveDist, lanceMate.MaxWalkDistance);
            }
            spread = Mathf.Min(spread, minMoveDist);
        }
        else
        {
            spread = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_NonInterleavedLanceSpreadDistance).FloatVal;
            float minMoveDist = float.MaxValue;
            for (int lmi = 0; lmi < lancemates.Count; ++lmi)
            {
                AbstractActor lanceMate = lancemates[lmi];
                float moveDist = Mathf.Max(lanceMate.MaxWalkDistance, lanceMate.MaxSprintDistance);
                minMoveDist = Mathf.Min(minMoveDist, moveDist);
            }
            spread = Mathf.Min(spread, minMoveDist);
        }

        Vector3 lanceCenter = Vector3.zero;
        int contributions = 0;

        for (int lancemateIndex = 0; lancemateIndex < lancemates.Count; ++lancemateIndex)
        {
            AbstractActor teammate = lancemates[lancemateIndex];
            if (teammate == unit)
            {
                continue;
            }
            lanceCenter += teammate.CurrentPosition;
            contributions += 1;
        }

        if (contributions == 0)
        {
            // if there's nobody but me, then sure, it's good enough.
            return true;
        }

        lanceCenter *= (1.0f / contributions);
        return (destination - lanceCenter).magnitude <= spread;
    }

    /// <summary>
    /// Gets all the units of this lance that are not dead.
    /// </summary>
    /// <param name="combat"></param>
    /// <param name="lanceGUID"></param>
    /// <returns></returns>
    public static List<AbstractActor> GetLanceUnits(CombatGameState combat, string lanceGUID)
    {
        List<AbstractActor> lanceUnits = new List<AbstractActor>();

        List<ITaggedItem> units = combat.ItemRegistry.GetObjectsOfType(TaggedObjectType.Unit);
        for (int unitIndex = 0; unitIndex < units.Count; ++unitIndex)
        {
            AbstractActor unit = units[unitIndex] as AbstractActor;
            if (unit == null)
            {
                continue;
            }
            if ((unit.LanceId == lanceGUID) && (!unit.IsDead))
            {
                lanceUnits.Add(unit);
            }
        }
        return lanceUnits;
    }

    public static Vector3 MaybeClipMovementDestinationToStayWithinLanceSpread(AbstractActor unit, Vector3 destination)
    {
        Vector3 displacementVector = destination - unit.CurrentPosition;

        AbstractActor mostDistantUnit = null;
        float mostDistantDistance = float.MinValue;

        string unitLanceGUID = unit.LanceId;

        for (int teamUnitIndex = 0; teamUnitIndex < unit.team.units.Count; ++teamUnitIndex)
        {
            AbstractActor teamUnit = unit.team.units[teamUnitIndex];
            if ((teamUnit == unit) || (teamUnit.IsDead) || (!teamUnit.LanceId.Equals(unitLanceGUID)))
            {
                continue;
            }

            float dist = (teamUnit.CurrentPosition - destination).magnitude;
            if (dist > mostDistantDistance)
            {
                mostDistantUnit = teamUnit;
                mostDistantDistance = dist;
            }
        }

        // if we don't care about any other units, just pass through.
        if (mostDistantUnit == null)
        {
            return destination;
        }

        float spread = unit.BehaviorTree.GetBehaviorVariableValue(
            unit.Combat.TurnDirector.IsInterleaved ?
              BehaviorVariableName.Float_InterleavedLanceSpreadDistance :
              BehaviorVariableName.Float_NonInterleavedLanceSpreadDistance).FloatVal;
        if (mostDistantDistance <= spread)
        {
            // destination is within acceptable limits
            return destination;
        }

        // else, we have to do some vector math

        //  ||M+d*t - A|| = s
        // where M is my location,
        //       A is my ally's location
        //       d is the candidate displacement vector
        //       t is an interpolation value between [0,1]
        //       s is the spread distance
        // solve that as a quadratic formula
        // (Mx + dx*t - Ax) ^ 2 + (My + dy*t - Ay) ^ 2 = s ^ 2

        // rewrite to clean up
        // define Jx = Mx - Ax, Jy = My - Ay
        // (Jx + dx*t) ^ 2 + (Jy + dy*t) ^2 = s ^ 2
        // Jx ^ 2 + 2 * Jx * dx * t + t ^ 2 * dx ^ 2 + Jy ^ 2 + 2 * Jy * dy * t + t ^ 2 * dy ^ 2 - s ^ 2 = 0
        // t ^ 2 * dx ^ 2 + t ^ 2 * dy ^ 2 + 2 * Jx * dx * t +  2 * Jy * dy * t + Jx ^ 2 + Jy ^ 2 - s ^ 2 = 0
        // t ^ 2 [dx + dy] + 2 * t [Jx * dx + Jy * dy] + Jx ^ 2 + Jy ^ 2 - s ^ 2

        // and now we solve for t
        // rewrite again, using new variables
        // a = dx + dy
        // b = 2 [Jx * dx + Jy * dy]
        // c = Jx ^ 2 + Jy ^ 2 - s ^ 2

        // t =  (-b +- sqrt(b^2 - 4ac)) / (2a)

        Vector3 J3 = unit.CurrentPosition - mostDistantUnit.CurrentPosition;
        Vector3 J = new Vector3(J3.x, 0, J3.z);
        float a = displacementVector.x * displacementVector.x + displacementVector.z * displacementVector.z;
        float b = 2 * (J.x * displacementVector.x + J.z * displacementVector.z);
        float c = J.x * J.x + J.z + J.z - spread * spread;

        float disc = b * b - 4 * a * c;
        float sqrtDisc = Mathf.Sqrt(disc);

        float t1 = (-b + sqrtDisc) / (2 * a);
        float t2 = (-b - sqrtDisc) / (2 * a);

        float t = 0.0f;

        if (t1 >= 0.0f)
        {
            t = t1;
        }
        if (t2 >= 0.0f)
        {
            t = Mathf.Max(t, t2);
        }

        t = Mathf.Min(1, t);

        if (t == 0.0f)
        {
            Debug.Log("failing!");
        }

        float tAbove = t;
        float tBelow = 0;
        float distAbove = 0.0f;
        float distBelow = (mostDistantUnit.CurrentPosition - unit.CurrentPosition).magnitude;
        const float SMALLEST_DELTA_DIST = 0.5f;
        const float SMALLEST_DELTA_T = 0.001f;

        const int steps = 10;
        while (true)
        {
            for (int i = steps; i >= 0; i--)
            {
                float testT = tBelow + (tAbove - tBelow) * i / steps;

                Vector3 testDest = unit.CurrentPosition + displacementVector * testT;
                float testDist = (testDest - mostDistantUnit.CurrentPosition).magnitude;

                if (testDist > spread)
                {
                    tAbove = testT;
                    distAbove = testDist;
                }
                else if (testDist < spread)
                {
                    tBelow = testT;
                    distBelow = testDist;
                    break;
                }
                else if (testDist == spread)
                {
                    // probably will never happen
                    return testDest;
                }
            }

            float deltaDist = distAbove - distBelow;
            float deltaT = tAbove - tBelow;
            if ((deltaDist < SMALLEST_DELTA_DIST) ||
                (deltaT < SMALLEST_DELTA_T))
            {
                // go with the "below" value and call it good
                return unit.CurrentPosition + displacementVector * tBelow;
            }
        }
    }

    /// <summary>
    /// Consider a list of possible ranges (range of ranges) that this unit could reach for a given hostile target. Return the greatest value.
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public static float CalcMaxExpectedDamageToHostile(AbstractActor unit, ICombatant target, bool isInspired, bool useHighFidelity, bool ignoreHitChance = false, bool accurateWeaponDmg = false)
    {
        if (useHighFidelity)
        {
            return CalcHighFidelityMaxExpectedDamageToHostile(unit, target, isInspired, ignoreHitChance, accurateWeaponDmg);
        }
        else
        {
            return CalcLowFidelityMaxExpectedDamageToHostile(unit, target, isInspired, ignoreHitChance, accurateWeaponDmg);
        }
    }

    /// <summary>
    /// Gets the weapons that can fire at the target
    /// </summary>
    /// <returns></returns>
    public static List<Weapon> WeaponsInRange(AbstractActor attacker, ICombatant target)
    {
        List<Weapon> weaponsInRange = new List<Weapon>();

        for (int weaponIndex = 0; weaponIndex < attacker.Weapons.Count; ++weaponIndex)
        {
            Weapon w = attacker.Weapons[weaponIndex];
            if (!w.CanFire || !attacker.HasLOFToTargetUnit(target, w))
            {
                continue;
            }

            weaponsInRange.Add(w);
        }

        return weaponsInRange;
    }

    /// <summary>
    /// Finds a better (see below) approximation of the maximum expected damage that this unit might do to this target. This
    /// is done by checking all reachable locations for this unit. This means that we have to wait for pathfinding to be
    /// complete before calling this.
    /// </summary>
    /// <param name="unit"></param>
    /// <param name="target"></param>
    /// <param name="isInspired"></param>
    /// <returns></returns>
    static float CalcHighFidelityMaxExpectedDamageToHostile(AbstractActor unit, ICombatant target, bool isInspired, bool ignoreHitChance = false, bool accurateWeaponDmg = false)
    {
        if (unit.Pathing == null)
        {
            return CalcLowFidelityMaxExpectedDamageToHostile(unit, target, isInspired);
        }

        PathNodeGrid[] grids = new PathNodeGrid[]
        {
            unit.Pathing.getGrid(MoveType.Backward),
            unit.Pathing.getGrid(MoveType.Walking)
        };

        float bestDamage = 0.0f;

        for (int gridIndex = 0; gridIndex < grids.Length; ++gridIndex)
        {
            PathNodeGrid grid = grids[gridIndex];

            List<PathNode> pathNodes = grid.GetSampledPathNodes();
            for (int pathNodeIndex = 0; pathNodeIndex < pathNodes.Count; ++pathNodeIndex)
            {
                PathNode pathNode = pathNodes[pathNodeIndex];
                float damageAtThisPosition = CalcExpectedDamageToHostileAtPoints(unit, pathNode.Position, target, target.CurrentPosition, isInspired, ignoreHitChance, accurateWeaponDmg);
                bestDamage = Mathf.Max(damageAtThisPosition, bestDamage);
            }
        }

        return bestDamage;
    }

    /// <summary>
    /// Finds a coarse approximation of the maximum expected damage that this unit might do to this target. This is done by
    /// finding a point close to the target location, and a point far away from the target, both within the unit's movement
    /// range. It then interpolates between those points to find a variety of locations in between, and evaluating the
    /// expected damage to the target, assuming the unit was standing at each of those locations. It returns the highest
    /// expected damage of these sampled locations.
    /// </summary>
    /// <param name="unit"></param>
    /// <param name="target"></param>
    /// <param name="isInspired"></param>
    /// <returns></returns>
    static float CalcLowFidelityMaxExpectedDamageToHostile(AbstractActor unit, ICombatant target, bool isInspired, bool ignoreHitChance = false, bool accurateWeaponDmg = false)
    {
        float rangeToTarget = (unit.CurrentPosition - target.CurrentPosition).magnitude;
        float movementDistance = unit.MaxWalkDistance;
        int hexSteps = Mathf.CeilToInt(movementDistance / unit.Combat.HexGrid.HexWidth);

        if (hexSteps == 0)
        {
            return CalcExpectedDamageToHostileAtPoints(unit, unit.CurrentPosition, target, target.CurrentPosition, isInspired, ignoreHitChance, accurateWeaponDmg);
        }

        List<Vector3> reachablePoints = unit.Combat.HexGrid.GetGridPointsAroundPointWithinRadius(unit.CurrentPosition, hexSteps);
        if (reachablePoints.Count == 0)
        {
            return 0.0f;
        }

        Vector3 farthestPoint = Vector3.zero;
        float farthestDist = float.MinValue;

        for (int i = 0; i < reachablePoints.Count; ++i)
        {
            Vector3 testPoint = reachablePoints[i];
            float testDist = (testPoint - target.CurrentPosition).magnitude;
            if (testDist > farthestDist)
            {
                farthestPoint = testPoint;
                farthestDist = testDist;
            }
        }

        Vector3 nearestPoint = Vector3.zero;
        float nearestDistToTarget = float.MaxValue;
        float nearestDistToFarPoint = float.MaxValue;

        for (int i = 0; i < reachablePoints.Count; ++i)
        {
            Vector3 testPoint = reachablePoints[i];
            float distToTarget = (testPoint - target.CurrentPosition).magnitude;
            float distToFarPoint = (testPoint - farthestPoint).magnitude;
            if ((distToTarget < nearestDistToTarget) ||
                ((distToTarget == nearestDistToTarget) && (distToFarPoint < nearestDistToFarPoint)))
            {
                nearestPoint = testPoint;
                nearestDistToTarget = distToTarget;
                nearestDistToFarPoint = distToFarPoint;
            }
        }

        // ok, we've got two points that we can reach, maybe. Now, sample points in between those two points

        int numSteps = Mathf.FloorToInt((farthestPoint - nearestPoint).magnitude / unit.Combat.HexGrid.HexWidth);
        Vector3 step = (farthestPoint - nearestPoint) / numSteps;

        float bestDamage = 0.0f;

        for (int i = 0; i <= numSteps; ++i)
        {
            Vector3 unsnappedPoint = nearestPoint + step * i;
            Vector3 snappedPoint = unit.Combat.HexGrid.GetClosestPointOnGrid(unsnappedPoint);

            float damageAtThisRange = CalcExpectedDamageToHostileAtPoints(unit, snappedPoint, target, target.CurrentPosition, isInspired, ignoreHitChance, accurateWeaponDmg);
            bestDamage = Mathf.Max(damageAtThisRange, bestDamage);
        }
        return bestDamage;
    }

    static float CalcExpectedDamageToHostileAtPoints(AbstractActor attacker, Vector3 attackerPosition, ICombatant target, Vector3 targetPosition, bool attackerIsInspired, bool ignoreHitChance = false, bool accurateWeaponDmg = false)
    {
        float damage = 0.0f;

        accurateWeaponDmg = attacker.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_ExpectedDamageAccuracyIncrease).BoolVal ? true : accurateWeaponDmg;

        for (int weaponIndex = 0; weaponIndex < attacker.Weapons.Count; ++weaponIndex)
        {
            Weapon w = attacker.Weapons[weaponIndex];

            if (accurateWeaponDmg)
            {
                if (!w.CanFire || !attacker.HasLOFToTargetUnit(target, w))
                {
                    continue;
                }
            }
            else
            {
                if (!w.CanFire)
                {
                    continue;
                }
            }

            float toHitChance;
            if (attackerIsInspired)
            {
                float testRange = (attackerPosition - targetPosition).magnitude;
                if ((testRange <= w.MaxRange) && (testRange >= w.MinRange))
                {
                    toHitChance = 0.95f;
                }
                else
                {
                    toHitChance = 0.0f;
                }
            }
            else
            {
                toHitChance = attacker.Combat.ToHit.GetToHitChance(attacker, w, target, attackerPosition, targetPosition, 1, MeleeAttackType.NotSet, false);
            }

            toHitChance = ignoreHitChance ? .95f : toHitChance;

            var weaponDamage = accurateWeaponDmg
                ? w.DamagePerShotFromPosition(MeleeAttackType.NotSet, attackerPosition, target) * w.ShotsWhenFired
                : w.DamagePerShot * w.ShotsWhenFired;

            damage += toHitChance * weaponDamage;
        }

        return damage;
    }

    /// <summary>
    /// Calculate how much more damage this unit will do to its target if it is inspired.
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public static float CalcInspirationDeltaVersusHostile(AbstractActor unit, ICombatant target, bool useHighFidelity)
    {
        return CalcMaxExpectedDamageToHostile(unit, target, true, useHighFidelity) - CalcMaxExpectedDamageToHostile(unit, target, false, useHighFidelity);
    }

    public static float CalcMaxInspirationDelta(AbstractActor unit, bool useHighFidelity)
    {
        float inspirationDelta = 0.0f;

        for (int unitIndex = 0; unitIndex < unit.Combat.AllActors.Count; ++unitIndex)
        {
            AbstractActor target = unit.Combat.AllActors[unitIndex];
            if (unit.Combat.HostilityMatrix.IsEnemy(unit.TeamId, target.TeamId) && (!target.IsDead))
            {
                inspirationDelta = Mathf.Max(inspirationDelta, CalcInspirationDeltaVersusHostile(unit, target, useHighFidelity));
            }
        }

        return inspirationDelta;
    }

    public static float CalcMaxInspirationDelta(List<AbstractActor> actorList, bool useHighFidelity)
    {
        float maxInspirationDelta = float.MinValue;
        for (int actorIndex = 0; actorIndex < actorList.Count; ++actorIndex)
        {
            AbstractActor actor = actorList[actorIndex];
            if (actor.IsDead)
            {
                continue;
            }
            float thisInspirationDelta = CalcMaxInspirationDelta(actor, useHighFidelity);
            maxInspirationDelta = Mathf.Max(thisInspirationDelta, maxInspirationDelta);
        }
        return maxInspirationDelta;
    }

    /// <summary>
    /// Gets the maximum steepness value that is within ALL of this unit's lance's unit's maximum steepnesses.
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public static float GetMaxSteepnessForAllLance(AbstractActor unit)
    {
        float steepness = float.MaxValue;

        for (int lanceUnitIndex = 0; lanceUnitIndex < unit.lance.unitGuids.Count; ++lanceUnitIndex)
        {
            string lanceUnitGUID = unit.lance.unitGuids[lanceUnitIndex];
            AbstractActor lanceMate = unit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(lanceUnitGUID);
            if ((lanceMate == null) || (lanceMate.IsDead) || (lanceMate.PathingCaps == null))
            {
                continue;
            }
            steepness = Mathf.Min(steepness, lanceMate.PathingCaps.MaxSteepness);
        }
        return steepness;
    }

    /// <summary>
    /// Gets the maximum range of all of the unit's weapons that can fire.
    /// </summary>
    /// <param name="unit"></param>
    /// <returns></returns>
    public static float GetMaxWeaponRange(AbstractActor unit)
    {
        float bestWeaponRange = float.MinValue;
        for (int wi = 0; wi < unit.Weapons.Count; ++wi)
        {
            Weapon w = unit.Weapons[wi];
            if ((w.CanFire) &&
                (w.MaxRange > bestWeaponRange))
            {
                bestWeaponRange = w.MaxRange;
            }
        }
        return bestWeaponRange;
    }

    public static bool UnitHasLOFToUnit(AbstractActor attacker, AbstractActor target, CombatGameState combat)
    {
        float attackRange = (attacker.CurrentPosition - target.CurrentPosition).magnitude;

        for (int weaponIndex = 0; weaponIndex < attacker.Weapons.Count; ++weaponIndex)
        {
            Weapon w = attacker.Weapons[weaponIndex];
            if (!w.CanFire)
            {
                continue;
            }
            if (w.MaxRange < attackRange)
            {
                continue;
            }
            if ((w.IndirectFireCapable) && (attacker.HasLOSToTargetUnit(target)))
            {
                return true;
            }
            if (combat.LOFCache.UnitHasLOFToTarget(attacker, target, w.MaxRange, attacker.CurrentPosition, Quaternion.LookRotation(target.CurrentPosition - attacker.CurrentPosition), false))
            {
                return true;
            }
        }
        return false;
    }

    public static bool UnitHasDirectLOFToUnit(AbstractActor attacker, ICombatant target, CombatGameState combat)
    {
        return UnitHasDirectLOFToTargetFromPosition(attacker, target, combat, attacker.CurrentPosition);
    }

    public static bool UnitHasDirectLOFToTargetFromPosition(AbstractActor attacker, ICombatant target, CombatGameState combat, Vector3 attackerPosition)
    {
        Weapon w;

        float attackRange = (attackerPosition - target.CurrentPosition).magnitude;

        Mech attackerMech = attacker as Mech;
        if (attackerMech != null)
        {
            w = attackerMech.MeleeWeapon;
            if (w.MaxRange <= attackRange)
            {
                return true;
            }
        }

        for (int weaponIndex = 0; weaponIndex < attacker.Weapons.Count; ++weaponIndex)
        {
            w = attacker.Weapons[weaponIndex];
            if (!w.CanFire)
            {
                continue;
            }
            if (w.MaxRange < attackRange)
            {
                continue;
            }
            if (!w.WillFireAtTargetFromPosition(target, attackerPosition))
            {
                continue;
            }
            if (combat.LOFCache.UnitHasLOFToTarget(attacker, target, w.MaxRange, attackerPosition, Quaternion.LookRotation(target.CurrentPosition - attackerPosition), false))
            {
                return true;
            }
        }
        return false;
    }

    public static bool UnitHasLOFToTargetFromPosition(AbstractActor attacker, ICombatant target, CombatGameState combat, Vector3 attackerPosition)
    {
        if (UnitHasDirectLOFToTargetFromPosition(attacker, target, combat, attackerPosition))
        {
            return true;
        }

        float attackRange = (attackerPosition - target.CurrentPosition).magnitude;

        for (int weaponIndex = 0; weaponIndex < attacker.Weapons.Count; ++weaponIndex)
        {
            Weapon w = attacker.Weapons[weaponIndex];
            if ((!w.IndirectFireCapable) || (!w.CanFire) || (w.MaxRange < attackRange) || (!w.WillFireAtTargetFromPosition(target, attackerPosition)))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    public static bool IsExposedToHostileFire(AbstractActor unit, CombatGameState combat)
    {
        if (unit.IsDead)
        {
            return false;
        }

        List<ITaggedItem> unitItems = combat.ItemRegistry.GetObjectsOfType(TaggedObjectType.Unit);
        for (int unitIndex = 0; unitIndex < unitItems.Count; ++unitIndex)
        {
            AbstractActor otherUnit = unitItems[unitIndex] as AbstractActor;
            if (otherUnit == null)
            {
                continue;
            }
            if (otherUnit.team.IsEnemy(unit.team))
            {
                if (UnitHasLOFToUnit(otherUnit, unit, combat))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool IsExposedToHostileFireAndAlone(AbstractActor unit, CombatGameState combat)
    {
        if (!IsExposedToHostileFire(unit, combat))
        {
            return false;
        }

        for (int lanceUnitIndex = 0; lanceUnitIndex < unit.lance.unitGuids.Count; ++lanceUnitIndex)
        {
            AbstractActor lanceUnit = combat.ItemRegistry.GetItemByGUID<AbstractActor>(unit.lance.unitGuids[lanceUnitIndex]);
            if (lanceUnit == unit)
            {
                continue;
            }
            if (IsExposedToHostileFire(lanceUnit, combat))
            {
                return false;
            }
        }
        return true;
    }

    public static AbstractActor GetExposedAloneLancemate(AbstractActor unit, CombatGameState combat)
    {
        AbstractActor foundActor = null;
        if (IsExposedToHostileFire(unit, combat))
        {
            return null;
        }

        // If a lance is 3 units or more (this unit plus 2 others), it is
        // possible to trigger "alone" calculations.

        const int MIN_OTHER_UNITS_FOR_ALONE = 2;

        int countAliveUnits = 0;

        for (int lanceUnitIndex = 0; lanceUnitIndex < unit.lance.unitGuids.Count; ++lanceUnitIndex)
        {
            AbstractActor lanceUnit = combat.ItemRegistry.GetItemByGUID<AbstractActor>(unit.lance.unitGuids[lanceUnitIndex]);
            if (lanceUnit == unit)
            {
                continue;
            }
            if (lanceUnit.IsDead)
            {
                continue;
            }

            countAliveUnits++;

            if (IsExposedToHostileFire(lanceUnit, combat))
            {
                if (foundActor == null)
                {
                    foundActor = lanceUnit;
                }
                else
                {
                    return null;
                }
            }
        }

        if (countAliveUnits >= MIN_OTHER_UNITS_FOR_ALONE)
        {
            return foundActor;
        }

        return null;
    }

    static PathNode PruneDangerousPath(CombatGameState combat, PathNode node)
    {
        while (node != null)
        {
            if (DynamicLongRangePathfinder.IsLocationSafe(combat, node.Position))
            {
                return node;
            }
            node = node.Parent;
        }
        return null;
    }

    public static PathNode GetPrunedClosestValidPathNode(AbstractActor unit, PathNodeGrid grid, CombatGameState combat, float movementBudget, List<Vector3> lrPath)
    {
        for (int pathIndex = lrPath.Count - 1; pathIndex > 0; --pathIndex)
        {
            PathNode node = grid.GetValidPathNodeAt(lrPath[pathIndex], movementBudget);
            if (node != null)
            {
                if (AIUtil.Get2DDistanceBetweenVector3s(unit.CurrentPosition, node.Position) < 1.0f)
                {
                    continue;
                }

                return PruneDangerousPath(combat, node);
            }
        }

        // fail
        return null;
    }

    public static float Get2DDistanceBetweenVector3s(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;

        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static float Get2DSquaredDistanceBetweenVector3s(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;

        return (dx * dx + dz * dz);
    }

    public static bool IsDFAAcceptable(AbstractActor attackingUnit, ICombatant targetCombatant)
    {
        AbstractActor targetActor = targetCombatant as AbstractActor;

        if (targetActor == null)
        {
            return false;
        }

        float myLegDamageLevel = 0.0f;
        Mech attackingMech = attackingUnit as Mech;
        if (attackingMech != null)
        {
            myLegDamageLevel = AttackEvaluator.LegDamageLevel(attackingMech);
        }

        float dfaDamageLevel = attackingUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
        float dfaOwnMaxDamageLevel = attackingUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;

        float damageLevel = AttackEvaluator.MaxDamageLevel(attackingUnit, targetActor);

        return ((attackingUnit.CanDFATargetFromPosition(targetActor, attackingUnit.CurrentPosition)) &&
                (damageLevel >= dfaDamageLevel) &&
                (myLegDamageLevel >= dfaOwnMaxDamageLevel));
    }
}

public static class AttackEvaluator
{
    /// <summary>
    /// calculates the damage level (0: no damage, 1:location no armor) for the most damaged location
    /// </summary>
    /// <returns>The damage level</returns>
    /// <param name="targetUnit">Target unit.</param>
    public static float MaxDamageLevel(AbstractActor attackingUnit, ICombatant targetUnit)
    {
        float highestDamageLevel = 0.0f;

        List<int> hitLocations = targetUnit.GetPossibleHitLocations(attackingUnit);

        for (int locIndex = 0; locIndex < hitLocations.Count; ++locIndex)
        {
            float currentDamageLevel = 0.0f;
            float maxArmor = targetUnit.MaxArmorForLocation(hitLocations[locIndex]);
            if (maxArmor == 0.0f)
            {
                currentDamageLevel = 1.0f;
            }
            else
            {
                float currentArmor = targetUnit.ArmorForLocation(hitLocations[locIndex]);
                float armorRemaining = (float)currentArmor / maxArmor;
                currentDamageLevel = 1.0f - armorRemaining;
            }
            highestDamageLevel = Mathf.Max(highestDamageLevel, currentDamageLevel);
        }

        return highestDamageLevel;
    }

    /// <summary>
    /// Gets the chance to cause structural damage to the target based on damage, num of weapons
    /// Current damage to structures and potential damage to structures, and remaining armor
    /// </summary>
    /// <param name="attackingUnit"></param>
    /// <param name="targetUnit"></param>
    /// <param name="damage"></param>
    /// <param name="weaponSpreadWeight">0-1 lerp from damge divided by weapons and </param>
    /// <returns></returns>
    public static float ChanceToCauseStructuralDamage(AbstractActor attackingUnit, ICombatant targetUnit, float damage, float weaponSpreadWeight)
    {
        float minChance = 0.05f;

        List<int> hitLocations = targetUnit.GetPossibleHitLocations(attackingUnit);

        var weaponsInRange = AIUtil.WeaponsInRange(attackingUnit, targetUnit);
        int numOfShots = 0;

        for (int i = 0; i < weaponsInRange.Count; i++)
        {
            numOfShots += Mathf.RoundToInt(Mathf.Lerp(weaponsInRange[i].ShotsWhenFired, 1, weaponSpreadWeight));
        }

        if ( !(damage > 0) || hitLocations.Count == 0 || !(numOfShots > 0))
            return minChance;

        var structuralDamageWeight = attackingUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_StructuralDamagePercentageMultiplier).FloatVal;

        var damageSpread = damage / Mathf.Min(numOfShots, hitLocations.Count - 1);
        var weightedDamageSpread = Mathf.Lerp(damageSpread, damage, weaponSpreadWeight);

        float structureDamagePotential = 0;
        int validLocations = 0;

        for (int locIndex = 0; locIndex < hitLocations.Count; ++locIndex)
        {
            if (hitLocations[locIndex] == (int)ArmorLocation.Head)
                continue;

            float currentArmor = targetUnit.ArmorForLocation(hitLocations[locIndex]);
            float currentStructure = targetUnit.StructureForLocation(hitLocations[locIndex]);
            float maxStructure = targetUnit.MaxStructureForLocation(hitLocations[locIndex]);

            if (currentStructure <= 0 || maxStructure == 0)
                continue;

            if (weightedDamageSpread > currentArmor)
            {
                var percentageDmgHitsStructure = (currentStructure - (weightedDamageSpread - currentArmor)) / maxStructure;
                structureDamagePotential += 1 + percentageDmgHitsStructure * structuralDamageWeight;
            }

            validLocations++;
        }

        return Mathf.Max(structureDamagePotential / validLocations, minChance);
    }

    /// <summary>
    /// calculates the hit points (0: no armor... ) for the least armored location, also counting critical structure
    /// </summary>
    /// <returns>The armor value</returns>
    /// <param name="targetUnit">Target unit.</param>
    public static float MinHitPoints(ICombatant targetUnit)
    {
        Mech mechUnit = targetUnit as Mech;

        if (mechUnit != null)
        {
            float minArmorPoints = float.MaxValue;

            float criticalStructure = Mathf.Min(mechUnit.GetCurrentStructure(ChassisLocations.Head), mechUnit.GetCurrentStructure(ChassisLocations.CenterTorso));

            //minArmorPoints = Mathf.Min(minArmorPoints, targetUnit.ArmorForLocation((int)ArmorLocation.Head) * mechUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_HeadArmorMultiplier).FloatVal);
            minArmorPoints = Mathf.Min(minArmorPoints, targetUnit.ArmorForLocation((int)ArmorLocation.CenterTorso) * mechUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CenterTorsoArmorMultiplier).FloatVal);
            minArmorPoints = Mathf.Min(minArmorPoints, targetUnit.ArmorForLocation((int)ArmorLocation.CenterTorsoRear) * mechUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CenterTorsoRearArmorMultiplier).FloatVal);
            float legMult = mechUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_RemainingLegArmorMultiplier).FloatVal;

            if (mechUnit.IsLegged)
            {
                if (mechUnit.IsLocationDestroyed(ChassisLocations.LeftLeg))
                {
                    criticalStructure = Mathf.Min(criticalStructure, mechUnit.GetCurrentStructure(ChassisLocations.RightLeg));
                    minArmorPoints = Mathf.Min(minArmorPoints, targetUnit.ArmorForLocation((int)ArmorLocation.RightLeg) * legMult);
                }
                else
                {
                    criticalStructure = Mathf.Min(criticalStructure, mechUnit.GetCurrentStructure(ChassisLocations.LeftLeg));
                    minArmorPoints = Mathf.Min(minArmorPoints, targetUnit.ArmorForLocation((int)ArmorLocation.LeftLeg) * legMult);
                }
            }
            return minArmorPoints + criticalStructure;
        }

        else if ((targetUnit as Vehicle) != null)
        {
            float minHitPoints = float.MaxValue;

            foreach (VehicleChassisLocations chassisLoc in System.Enum.GetValues(typeof(VehicleChassisLocations)))
            {
                if (chassisLoc == VehicleChassisLocations.Invalid ||
                    chassisLoc == VehicleChassisLocations.All ||
                    chassisLoc == VehicleChassisLocations.None ||
                    chassisLoc == VehicleChassisLocations.MainBody)
                {
                    continue;
                }
                float armor = targetUnit.ArmorForLocation((int)chassisLoc);
                float structure = targetUnit.StructureForLocation((int)chassisLoc);
                minHitPoints = Mathf.Min(minHitPoints, (armor + structure));
            }
            return minHitPoints;
        }

        else if ((targetUnit as BattleTech.Building) != null)
        {
            BattleTech.Building targetBuilding = targetUnit as BattleTech.Building;
            return targetBuilding.CurrentStructure;
        }

        else if ((targetUnit as Turret) != null)
        {
            Turret targetTurret = targetUnit as Turret;
            return targetTurret.CurrentArmor + targetTurret.TurretStructure;
        }

        return -1;
    }


    /// <summary>
    /// calculates the hit points (0: no armor... ) for the least armored location, also counting critical structure
    /// </summary>
    /// <returns>The armor value</returns>
    /// <param name="targetUnit">Target unit.</param>
    /// <param name="attackerPosition">Attacker position.</param>
    public static float GetHitPointsFromAttackerPosition(ICombatant targetUnit, Vector3 attackerPosition)
    {
        Mech mechUnit = targetUnit as Mech;

        if (mechUnit != null)
        {
            float minArmorPoints = float.MaxValue;
            foreach (ArmorLocation armorLoc in System.Enum.GetValues(typeof(ArmorLocation)))
            {
                if (armorLoc == ArmorLocation.Invalid || armorLoc == ArmorLocation.None)
                {
                    continue;
                }

                float armor = targetUnit.ArmorForLocation((int)armorLoc);
                minArmorPoints = Mathf.Min(minArmorPoints, armor);
            }
            return minArmorPoints + mechUnit.GetCurrentStructure(ChassisLocations.Head) + mechUnit.GetCurrentStructure(ChassisLocations.CenterTorso);
        }

        else if ((targetUnit as Vehicle) != null)
        {
            float minHitPoints = float.MaxValue;

            foreach (VehicleChassisLocations chassisLoc in System.Enum.GetValues(typeof(VehicleChassisLocations)))
            {
                if (chassisLoc == VehicleChassisLocations.Invalid ||
                    chassisLoc == VehicleChassisLocations.All ||
                    chassisLoc == VehicleChassisLocations.None ||
                    chassisLoc == VehicleChassisLocations.MainBody)
                {
                    continue;
                }
                float armor = targetUnit.ArmorForLocation((int)chassisLoc);
                float structure = targetUnit.StructureForLocation((int)chassisLoc);
                minHitPoints = Mathf.Min(minHitPoints, (armor + structure));
            }
            return minHitPoints;
        }

        else if ((targetUnit as BattleTech.Building) != null)
        {
            BattleTech.Building targetBuilding = targetUnit as BattleTech.Building;
            return targetBuilding.CurrentStructure;
        }

        return -1;
    }


    /// <summary>
    /// calculates the hit points (0: no armor... ) for the least armored location in the undamaged version of this unit, also counting critical structure
    /// </summary>
    /// <returns>The armor value</returns>
    /// <param name="targetUnit">Target unit.</param>
    public static float MinOriginalHitPoints(AbstractActor attackingUnit, ICombatant targetUnit)
    {
        Mech mechUnit = targetUnit as Mech;

        if (mechUnit != null)
        {
            float minArmorPoints = float.MaxValue;
            foreach (ArmorLocation armorLoc in System.Enum.GetValues(typeof(ArmorLocation)))
            {
                if (armorLoc == ArmorLocation.Invalid || armorLoc == ArmorLocation.None)
                {
                    continue;
                }

                float armor = targetUnit.MaxArmorForLocation((int)armorLoc);
                minArmorPoints = Mathf.Min(minArmorPoints, armor);
            }
            return minArmorPoints + mechUnit.GetMaxStructure(ChassisLocations.Head) + mechUnit.GetMaxStructure(ChassisLocations.CenterTorso);
        }

        else if ((targetUnit as Vehicle) != null)
        {
            float minHitPoints = float.MaxValue;

            foreach (VehicleChassisLocations chassisLoc in System.Enum.GetValues(typeof(VehicleChassisLocations)))
            {
                if (chassisLoc == VehicleChassisLocations.Invalid ||
                    chassisLoc == VehicleChassisLocations.All ||
                    chassisLoc == VehicleChassisLocations.None ||
                    chassisLoc == VehicleChassisLocations.MainBody)
                {
                    continue;
                }
                float armor = targetUnit.MaxArmorForLocation((int)chassisLoc);
                float structure = targetUnit.MaxStructureForLocation((int)chassisLoc);
                minHitPoints = Mathf.Min(minHitPoints, (armor + structure));
            }
            return minHitPoints;
        }

        else if ((targetUnit as BattleTech.Building) != null)
        {
            BattleTech.Building targetBuilding = targetUnit as BattleTech.Building;
            return targetBuilding.CurrentStructure;
        }

        return -1;
    }

    /// <summary>
    /// calculates the damage level (0: no damage, 1:location destroyed) for the most damaged leg location
    /// </summary>
    /// <returns>The damage level</returns>
    /// <param name="mech">Attacking mech.</param>
    public static float LegDamageLevel(Mech mech)
    {
        List<ArmorLocation> legLocations = new List<ArmorLocation>();
        legLocations.Add(ArmorLocation.LeftLeg);
        legLocations.Add(ArmorLocation.RightLeg);

        float maxDamage = 0;
        for (int i = 0; i < legLocations.Count; ++i)
        {
            float maxArmor = mech.GetMaxArmor(legLocations[i]);
            float currentArmor = mech.GetCurrentArmor(legLocations[i]);
            float damageFrac = 0.0f;
            if (maxArmor > 0)
            {
                damageFrac = 1.0f - (currentArmor / maxArmor);
            }
            maxDamage = Mathf.Max(maxDamage, damageFrac);
        }
        return maxDamage;
    }

    static List<List<Weapon>> MakeWeaponSets(List<Weapon> potentialWeapons)
    {
        List<List<Weapon>> weaponSets = new List<List<Weapon>>();

        if (potentialWeapons.Count > 0)
        {
            Weapon firstWeapon = potentialWeapons[0];
            List<Weapon> butFirst = potentialWeapons.GetRange(1, potentialWeapons.Count - 1);
            List<List<Weapon>> butFirstWeaponSets = MakeWeaponSets(butFirst);

            for (int weaponSetIndex = 0; weaponSetIndex < butFirstWeaponSets.Count; ++weaponSetIndex)
            {
                List<Weapon> weaponList = butFirstWeaponSets[weaponSetIndex];
                weaponSets.Add(weaponList);
                List<Weapon> newWeaponList = new List<Weapon>(weaponList);
                newWeaponList.Add(firstWeapon);
                weaponSets.Add(newWeaponList);
            }
        }
        else
        {
            List<Weapon> emptyWeaponSet = new List<Weapon>();
            weaponSets.Add(emptyWeaponSet);
        }
        return weaponSets;
    }

    static List<List<Weapon>> MakeWeaponSetsForEvasive(List<Weapon> potentialWeapons, float toHitFrac, ICombatant target, Vector3 shooterPosition)
    {
        List<Weapon> exceedsToHitWeapons = new List<Weapon>();
        List<Weapon> noAmmoWeapons = new List<Weapon>();
        List<Weapon> otherWeapons = new List<Weapon>();

        for (int weaponIndex = 0; weaponIndex < potentialWeapons.Count; ++weaponIndex)
        {
            Weapon w = potentialWeapons[weaponIndex];
            if (!w.CanFire)
            {
                continue;
            }
            float weaponToHit = w.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true);
            if (weaponToHit < toHitFrac)
            {
                if (w.AmmoCategoryValue.Is_NotSet)
                {
                    noAmmoWeapons.Add(w);
                }
                else
                {
                    otherWeapons.Add(w);
                }
            }
            else
            {
                exceedsToHitWeapons.Add(w);
            }
        }

        float bestDamage = float.MinValue;
        Weapon bestWeapon = null;

        for (int weaponIndex = 0; weaponIndex < noAmmoWeapons.Count; ++weaponIndex)
        {
            Weapon w = noAmmoWeapons[weaponIndex];
            float toHit = w.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true);
            float expDmg = toHit * w.ShotsWhenFired * w.DamagePerShot;
            if (expDmg > bestDamage)
            {
                bestDamage = expDmg;
                bestWeapon = w;
            }
        }

        if (bestWeapon == null)
        {
            for (int weaponIndex = 0; weaponIndex < otherWeapons.Count; ++weaponIndex)
            {
                Weapon w = otherWeapons[weaponIndex];
                float toHit = w.GetToHitFromPosition(target, 1, shooterPosition, target.CurrentPosition, true, true);
                float expDmg = toHit * w.ShotsWhenFired * w.DamagePerShot;
                if (expDmg > bestDamage)
                {
                    bestDamage = expDmg;
                    bestWeapon = w;
                }
            }
        }

        if (bestWeapon != null)
        {
            exceedsToHitWeapons.Add(bestWeapon);
        }

        return MakeWeaponSets(exceedsToHitWeapons);
    }

    /// <summary>
    /// An object that carries data about an attack, including the expected damage and heat that will result from it.
    /// </summary>
    public class AttackEvaluation : System.IComparable
    {
        public List<Weapon> WeaponList;
        public AIUtil.AttackType AttackType;
        public float HeatGenerated;
        public float ExpectedDamage;
        public float lowestHitChance;

        public int CompareTo(object otherObj)
        {
            AttackEvaluation otherAttack = otherObj as AttackEvaluation;
            if (otherAttack == null)
            {
                return -1;
            }

            int dmgSort = ExpectedDamage.CompareTo(otherAttack.ExpectedDamage);
            if (dmgSort != 0)
            {
                // invert, so higher damage sorts earlier
                return -dmgSort;
            }

            int hitChance = lowestHitChance.CompareTo(otherAttack.lowestHitChance);
            if (hitChance != 0)
            {
                // invert, so higher hit chance sorts earlier
                return -hitChance;
            }

            int heatSort = HeatGenerated.CompareTo(otherAttack.HeatGenerated);
            if (heatSort != 0)
            {
                // lower heat sorts earlier
                return heatSort;
            }

            return WeaponList.Count.CompareTo(otherAttack.WeaponList.Count);
        }

        static public List<AttackEvaluation> EvaluateAttacks(AbstractActor unit, ICombatant target, List<List<Weapon>>[] weaponSetListByAttack, Vector3 attackPosition, Vector3 targetPosition, bool targetIsEvasive)
        {
            List<AttackEvaluation> evaluations = new List<AttackEvaluation>();

            for (int attackType = 0; attackType < (int)AIUtil.AttackType.Count; ++attackType)
            {
                List<List<Weapon>> weaponSetList = weaponSetListByAttack[attackType];
                if (weaponSetList == null)
                {
                    continue;
                }
                for (int weaponSetIndex = 0; weaponSetIndex < weaponSetList.Count; ++weaponSetIndex)
                {
                    List<Weapon> weaponSet = weaponSetList[weaponSetIndex];
                    AttackEvaluation evaluation = new AttackEvaluation();
                    evaluation.WeaponList = weaponSet;
                    evaluation.AttackType = (AIUtil.AttackType)attackType;

                    evaluation.HeatGenerated = AIUtil.HeatForAttack(weaponSet);
                    Mech mech = unit as Mech;
                    if (mech != null)
                    {
                        evaluation.HeatGenerated += mech.TempHeat;
                        evaluation.HeatGenerated -= mech.AdjustedHeatsinkCapacity;
                    }

                    evaluation.ExpectedDamage = AIUtil.ExpectedDamageForAttack(unit, evaluation.AttackType, weaponSet, target, attackPosition, targetPosition, true, unit);
                    evaluation.lowestHitChance = AIUtil.LowestHitChance(weaponSet, target, attackPosition, targetPosition, targetIsEvasive);

                    evaluations.Add(evaluation);
                }
            }

            evaluations.Sort((a, b) => (a.ExpectedDamage.CompareTo(b.ExpectedDamage)));
            evaluations.Reverse();
            return evaluations;
        }
    }

    static void FinalizeShootContextLog(AbstractActor unit)
    {
        string msg = unit.BehaviorTree.debugLogShootContextStringBuilder.ToString();
        unit.BehaviorTree.debugLogShootContextStringBuilder = null;
        string filename = unit.Combat.AILogCache.MakeFilename("shoot");
        unit.Combat.AILogCache.AddLogData(filename, msg);
    }

    public static BehaviorTreeResults MakeAttackOrder(AbstractActor unit, bool isStationary)
    {
        Debug.Assert(unit != null, "unit is null in AttackEvaluator.MakeAttackOrder");
        if (unit == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        bool cleanUpBuilder = false;

        if (unit.BehaviorTree.debugLogShootContextStringBuilder == null)
        {
            cleanUpBuilder = true;
            unit.BehaviorTree.debugLogShootContextStringBuilder = new System.Text.StringBuilder();
        }
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "making attack order");
        if (unit.BehaviorTree.enemyUnits.Count == 0)
        {
            AIUtil.LogAI("no enemy units", unit);
            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "no enemy units. not shooting. Failure.");
            if (cleanUpBuilder)
            {
                FinalizeShootContextLog(unit);
            }
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        AIUtil.LogAI("enemy unit count: " + unit.BehaviorTree.enemyUnits.Count, unit);
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "Enemy Unit Count: " + unit.BehaviorTree.enemyUnits.Count);

        AIUtil.LogAI("Evaluating attack from pos " + unit.CurrentPosition, unit);

        float designatedAttackDamage = 0.0f;
        BehaviorTreeResults designatedTargetOrder = null;
        AbstractActor designatedTarget = null;
        float designatedFirepowerTakeaway = 0.0f;

        // if the unit's team has a designated target, prefer that.
        AITeam team = unit.team as AITeam;
        if (team != null)
        {
            if (team.DesignatedTargetForLance.ContainsKey(unit.lance))
            {
                designatedTarget = team.DesignatedTargetForLance[unit.lance];
                if ((designatedTarget != null) && (!designatedTarget.IsDead))
                {
                    for (int designatedTargetIndex = 0; designatedTargetIndex < unit.BehaviorTree.enemyUnits.Count; ++designatedTargetIndex)
                    {
                        if (unit.BehaviorTree.enemyUnits[designatedTargetIndex] == designatedTarget)
                        {
                            designatedAttackDamage = MakeAttackOrderForTarget(unit, designatedTarget, designatedTargetIndex, isStationary, out designatedTargetOrder);
                            designatedFirepowerTakeaway = AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(unit, unit.CurrentPosition, designatedTarget, designatedTarget.CurrentPosition, designatedTarget.CurrentRotation, unit.Weapons, MeleeAttackType.NotSet);
                            break;
                        }
                    }
                }
            }
        }

        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "Designated Target Attack Damage: " + designatedAttackDamage);
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "Designated Target Firepower Takeaway: " + designatedFirepowerTakeaway);

        float exceedPercentage = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OpportunityFireExceedsDesignatedTargetByPercentage).FloatVal;
        float exceedFrac = exceedPercentage / 100.0f;

        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "Opportunity Fire Damage Threshold (%): " + exceedPercentage);

        float takeawayExceedPercentage = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OpportunityFireExceedsDesignatedTargetFirepowerTakeawayByPercentage).FloatVal;
        float takeawayExceedFrac = takeawayExceedPercentage / 100.0f;

        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "Opportunity Fire Takeaway Threshold (%): " + takeawayExceedPercentage);

        for (int enemyUnitIndex = 0; enemyUnitIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyUnitIndex)
        {
            ICombatant target = unit.BehaviorTree.enemyUnits[enemyUnitIndex];
            if ((target == designatedTarget) || (target.IsDead))
            {
                continue;
            }

            AbstractActor targetActor = target as AbstractActor;
            if (targetActor != null)
            {
                Pilot p = targetActor.GetPilot();
                string pilotName = (p == null) ? "???" : p.Description.Callsign;
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("Target: {0} {1} {2}", targetActor.DisplayName, targetActor.VariantName, pilotName));
            }
            else
            {
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("Target: {0}", target.DisplayName));
            }

            BehaviorTreeResults targetOrder;
            float targetDamage = MakeAttackOrderForTarget(unit, target, enemyUnitIndex, isStationary, out targetOrder);
            float targetFirepowerTakeaway = AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(unit, unit.CurrentPosition, target, target.CurrentPosition, target.CurrentRotation, unit.Weapons, MeleeAttackType.NotSet);

            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("Damage to Target: {0}", targetDamage));
            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("Damage Takeaway from Target: {0}", targetFirepowerTakeaway));

            // boost targetDamage for unsteadiness/evasive effects
            // While selecting targets, if:
            // - a target is evasive
            // - I have the ability to knock him unsteady, getting rid of evasive
            // - my friends have the ability to do more than < X > damage more as a result of my attack,
            // then, provide a bonus to selecting that target.

            if ((targetActor != null) &&
                (targetActor.IsEvasive))
            {
                int pipsRemoved = CountPipsRemovedByAttack(unit, unit.CurrentPosition, targetActor, target.CurrentPosition, target.CurrentRotation, unit.Weapons, MeleeAttackType.NotSet);

                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("Pips Removed from Target: {0}", pipsRemoved));

                if (pipsRemoved > 0)
                {
                    float additionalDamage = AdditionalDamageFromFriendsGainedByAttackingEvasiveTarget(unit, targetActor, target.CurrentPosition, pipsRemoved);

                    unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("Additional Damage Gained as a result of my attack: {0}", additionalDamage));

                    targetDamage += additionalDamage;
                }
            }

            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("Comparing damage to: {0} and takeaway to: {1}", designatedAttackDamage, designatedFirepowerTakeaway));

            if ((targetOrder != null) &&
                (targetOrder.orderInfo != null) &&
                ((targetDamage > designatedAttackDamage * (1 + exceedFrac)) || (targetFirepowerTakeaway > designatedFirepowerTakeaway * (1 + takeawayExceedFrac))))
            {
                AttackOrderInfo aoi = targetOrder.orderInfo as AttackOrderInfo;
                MultiTargetAttackOrderInfo mtAoi = targetOrder.orderInfo as MultiTargetAttackOrderInfo;

                if (aoi != null)
                {
                    for (int wi = 0; wi < aoi.Weapons.Count; ++wi)
                    {
                        Weapon w = aoi.Weapons[wi];
                        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "weapon: " + w.Name);
                    }
                }
                else if (mtAoi != null)
                {
                    unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "multi-target order");
                    for (int subTargetOrderIndex = 0; subTargetOrderIndex < mtAoi.SubTargetOrders.Count; ++subTargetOrderIndex)
                    {
                        AttackOrderInfo subAttackOrder = mtAoi.SubTargetOrders[subTargetOrderIndex];
                        AbstractActor subTargetActor = subAttackOrder.TargetUnit as AbstractActor;
                        if (subTargetActor == null)
                        {
                            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "target: " + subAttackOrder.TargetUnit.DisplayName);
                        }
                        else
                        {
                            Pilot p = subTargetActor.GetPilot();
                            string pilotName = (p == null) ? "???" : p.Description.Callsign;
                            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "target: " + subAttackOrder.TargetUnit.DisplayName + " " + pilotName);
                        }

                        for (int weaponIndex = 0; weaponIndex < subAttackOrder.Weapons.Count; ++weaponIndex)
                        {
                            Weapon w = subAttackOrder.Weapons[weaponIndex];
                            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "weapon: " + w.Name);
                        }
                    }
                }

                // opportunity fire
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "taking opportunity fire shot. Success");

                if (cleanUpBuilder)
                {
                    FinalizeShootContextLog(unit);
                }
                return targetOrder;
            }
        }

        if ((designatedTargetOrder != null) &&
            (designatedTargetOrder.orderInfo != null))
        {
            // attack designated target
            unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "attacking designated target. Success");
            if (cleanUpBuilder)
            {
                FinalizeShootContextLog(unit);
            }
            return designatedTargetOrder;
        }

        // otherwise, no good attacks
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "no good attacks. not shooting. Failure.");
        if (cleanUpBuilder)
        {
            FinalizeShootContextLog(unit);
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }

    static float AdditionalDamageFromFriendsGainedByAttackingEvasiveTarget(AbstractActor unit, AbstractActor target, Vector3 targetPosition, int pipsRemoved)
    {
        //List<AbstractActor> lanceMates = new List<AbstractActor>();

        List<AbstractActor> guysWhoWillGoBeforeTarget = new List<AbstractActor>();

        Dictionary<int, List<AbstractActor>> lanceMatesByPhase = new Dictionary<int, List<AbstractActor>>();
        int phase;
        for (phase = 1; phase <= 5; ++phase)
        {
            lanceMatesByPhase[phase] = new List<AbstractActor>();
        }

        for (int lanceMateIndex = 0; lanceMateIndex < unit.lance.unitGuids.Count; ++lanceMateIndex)
        {
            string lanceMateGUID = unit.lance.unitGuids[lanceMateIndex];
            if (lanceMateGUID == unit.GUID)
            {
                continue;
            }

            AbstractActor lanceMate = unit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(lanceMateGUID);
            int initiative = lanceMate.Initiative;
            lanceMatesByPhase[initiative].Add(lanceMate);
        }

        // for everybody that hasn't gone yet this phase
        int currentPhase = unit.Combat.TurnDirector.CurrentPhase;
        for (int lanceMateIndex = 0; lanceMateIndex < lanceMatesByPhase[currentPhase].Count; ++lanceMateIndex)
        {
            AbstractActor lanceMate = lanceMatesByPhase[currentPhase][lanceMateIndex];
            if (!lanceMate.HasActivatedThisRound)
            {
                guysWhoWillGoBeforeTarget.Add(lanceMate);
            }
        }

        // for every phase up to and including the phase the target is going on
        phase = currentPhase;
        while (true)
        {
            if (target.Initiative == phase)
            {
                break;
            }

            phase++;
            if (phase > 5)
            {
                phase = 1;
            }

            for (int lanceMateIndex = 0; lanceMateIndex < lanceMatesByPhase[phase].Count; ++lanceMateIndex)
            {
                AbstractActor lanceMate = lanceMatesByPhase[phase][lanceMateIndex];
                guysWhoWillGoBeforeTarget.Add(lanceMate);
            }
        }

        // ok, we've got a list of our lancemates that will go before (or at worst on the same phase as) the target.

        float damageWithPipReduction = 0.0f;
        float damageWithoutPipReduction = 0.0f;

        // original pips
        int originalPips = target.EvasivePipsCurrent;
        int reducedPips = Mathf.Max(0, originalPips - pipsRemoved);

        for (int lanceMateIndex = 0; lanceMateIndex < guysWhoWillGoBeforeTarget.Count; ++lanceMateIndex)
        {
            AbstractActor lm = guysWhoWillGoBeforeTarget[lanceMateIndex];

            for (int weaponIndex = 0; weaponIndex < lm.Weapons.Count; ++weaponIndex)
            {
                Weapon w = lm.Weapons[weaponIndex];
                if ((!w.CanFire) || (!lm.HasLOFToTargetUnit(target, w)))
                {
                    continue;
                }

                target.EvasivePipsCurrent = originalPips;
                float hitProb = w.GetToHitFromPosition(target, 1, lm.CurrentPosition, target.CurrentPosition, true, true);
                damageWithoutPipReduction += hitProb * w.DamagePerShot * w.ShotsWhenFired;

                target.EvasivePipsCurrent = reducedPips;
                hitProb = w.GetToHitFromPosition(target, 1, lm.CurrentPosition, target.CurrentPosition, true, true);
                damageWithPipReduction += hitProb * w.DamagePerShot * w.ShotsWhenFired;
            }
        }

        // remember to set it back to the original value!!
        target.EvasivePipsCurrent = originalPips;

        return damageWithPipReduction - damageWithoutPipReduction;
    }

    static int CountPipsRemovedByAttack(AbstractActor unit, Vector3 unitPosition, AbstractActor targetUnit, Vector3 targetPosition, Quaternion targetRotation, List<Weapon> weapons, MeleeAttackType meleeAttackType)
    {
        // any attack that hits strips one pip
        // compute probability of *any* attack landing - that is, the inverse of the chance that all weapons miss

        // unsteady removes all pips - if the stability damage plus the target's current stability is higher than the unsteady threshold

        // return the bigger of the two

        float chanceOfAllMissing = 1.0f;

        float stabilityDamage = 0.0f;

        for (int weaponIndex = 0; weaponIndex < weapons.Count; ++weaponIndex)
        {
            Weapon w = weapons[weaponIndex];
            float weaponChance = w.GetToHitFromPosition(targetUnit, 1, unit.CurrentPosition, targetPosition, true, true);
            chanceOfAllMissing *= (1.0f - weaponChance);
            stabilityDamage += weaponChance * w.Instability();
        }

        Mech targetMech = targetUnit as Mech;
        if (targetMech != null)
        {
            if ((stabilityDamage + targetMech.CurrentStability) > targetMech.MaxStability)
            {
                return targetMech.EvasivePipsCurrent;
            }
        }

        return ((1.0f - chanceOfAllMissing) >= unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PipStripAttackProbabilityThreshold).FloatVal) ? 1 : 0;
    }

    static void LogShoot(AbstractActor unit, string msg)
    {
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, msg);
        AIUtil.LogAI(msg, unit);
    }

    static float MakeAttackOrderForTarget(AbstractActor unit, ICombatant target, int enemyUnitIndex, bool isStationary, out BehaviorTreeResults order)
    {
        float myHeatLevel = 0.0f;
        float acceptableHeat = float.MaxValue;
        float myLegDamageLevel = 0.0f;

        Mech myMech = unit as Mech;
        if (myMech != null)
        {
            myHeatLevel = myMech.CurrentHeat;
            acceptableHeat = AIUtil.GetAcceptableHeatLevelForMech(myMech);
            myLegDamageLevel = LegDamageLevel(myMech);
        }
        LogShoot(unit, "heat level: " + myHeatLevel);
        LogShoot(unit, "acceptable heat: " + acceptableHeat);

        float dfaDamageLevel = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ExistingTargetDamageForDFAAttack).FloatVal;
        float dfaOwnMaxDamageLevel = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OwnMaxLegDamageForDFAAttack).FloatVal;
        float overheatDamageLevel = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ExistingTargetDamageForOverheatAttack).FloatVal;
        float overheatAccuracyLevel = unit.BehaviorTree.weaponToHitThreshold;

        float damageLevel = MaxDamageLevel(unit, target);

        LogShoot(unit, "Evaluating attack target " + target.DisplayName + " at " + target.CurrentPosition);

        if (!AIUtil.UnitHasVisibilityToTargetFromCurrentPosition(unit, target))
        {
            // Our team can't see this hostile.
            order = BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
            return 0.0f;
        }

        /*
            * I should not do any attack that results in a shutdown unless my target has (1) [(red)] damage
            * somewhere that a potential hit could land and that all weapons involved in the attack have at least a
            * (2) [70%] chance to hit.
            *
            * If all weapons do not have a [70%] chance to hit and there and the target does not have (red) damage
            * that can be hit we select the weapon with worst accuracy and remove it from consideration to fire.
            * Next we evaluate if the attack will cause a shutdown if will not, fire the remaining weapons. If the
            * attack will cause a shutdown once again remove the weapon with the least accuracy from consideration
            * and reevaluate. Continue this loop until an attack can occur without an overheat or all weapons are
            * removed from consideration.
            */

        List<Weapon>[] potentialWeaponsForAttackType = {
            new List<Weapon>(),
            new List<Weapon>(),
            new List<Weapon>(),
        };

        for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
        {
            Weapon weapon = unit.Weapons[weaponIndex];

            LogShoot(unit, "Considering Weapon " + weapon.Name);

            if (!weapon.CanFire)
            {
                LogShoot(unit, "disregarding because not can fire");
                continue;
            }

            bool willFireAtTarget = weapon.WillFireAtTargetFromPosition(target, unit.CurrentPosition, unit.CurrentRotation);
            bool hasLOF = unit.Combat.LOFCache.UnitHasLOFToTarget(unit, target, weapon);
            bool inRange = ((target.CurrentPosition - unit.CurrentPosition).magnitude <= weapon.MaxRange);

            LogShoot(unit, "will fire at target? " + willFireAtTarget);
            LogShoot(unit, "hasLOF? " + hasLOF);
            LogShoot(unit, "inRange? " + inRange);

            if (willFireAtTarget && hasLOF && inRange)
            {
                LogShoot(unit, "willFireAtTarget and LOF");
                potentialWeaponsForAttackType[(int)AIUtil.AttackType.Shooting].Add(weapon);
            }
            else
            {
                LogShoot(unit, "not WFAT or hasLOF or inRange");
            }

            // TODO - Verify Melee attacks don't double-dip.
            if (weapon.WeaponCategoryValue.CanUseInMelee)
            {
                LogShoot(unit, "adding to melee and DFA");

                potentialWeaponsForAttackType[(int)AIUtil.AttackType.Melee].Add(weapon);
                potentialWeaponsForAttackType[(int)AIUtil.AttackType.DeathFromAbove].Add(weapon);
            }
        }

        Mech targetMech = target as Mech;
        bool targetIsEvasive = (targetMech != null) && targetMech.IsEvasive;

        // create weapon sets based on attack type

        List<List<Weapon>>[] weaponSetsForAttackType = new List<List<Weapon>>[3];
        for (int attackType = 0; attackType < (int)AIUtil.AttackType.Count; ++attackType)
        {
            LogShoot(unit, "considering attack type " + attackType);
            if ((myMech == null) &&
                ((attackType == (int)AIUtil.AttackType.Melee) ||
                    (attackType == (int)AIUtil.AttackType.DeathFromAbove)))
            {
                // non-mechs can't melee, can't DFA
                LogShoot(unit, "this unit can't melee or dfa");
                continue;
            }

            string engageDbgMsg;

            if ((attackType == (int)AIUtil.AttackType.Melee) &&
                (!myMech.CanEngageTarget(target, out engageDbgMsg)))
            {
                // can't melee
                LogShoot(unit, "unit.CanEngageTarget returned FALSE because: " + engageDbgMsg);
                continue;
            }

            if ((attackType == (int)AIUtil.AttackType.Melee) &&
                (targetMech != null))
            {
                float targetExpDmg = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(targetMech, unit, targetMech.CurrentPosition, myMech.CurrentPosition, false, unit);
                float myExpDmg = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(myMech, target, myMech.CurrentPosition, target.CurrentPosition, false, unit);
                if (myExpDmg <= 0)
                {
                    // don't expect to do any damage, bail
                    LogShoot(unit, "expected damage: " + myExpDmg);
                    continue;
                }

                float ratio = targetExpDmg / myExpDmg;
                float bvRatio = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MeleeDamageRatioCap).FloatVal;
                if (ratio > bvRatio)
                {
                    LogShoot(unit, "melee ratio too high: " + ratio + " vs " + bvRatio);
                    continue;
                }
            }

            if ((attackType == (int)AIUtil.AttackType.DeathFromAbove) &&
                (!AIUtil.IsDFAAcceptable(unit, target)))
            {
                // can't DFA
                LogShoot(unit, "unit cannot DFA");
                continue;
            }

            if (targetIsEvasive && (unit.UnitType == UnitType.Mech))
            {
                float toHitFrac = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_EvasiveToHitFloor).FloatVal / 100.0f;
                weaponSetsForAttackType[attackType] = MakeWeaponSetsForEvasive(potentialWeaponsForAttackType[attackType], toHitFrac, target, unit.CurrentPosition);
            }
            else
            {
                weaponSetsForAttackType[attackType] = MakeWeaponSets(potentialWeaponsForAttackType[attackType]);
            }
            if ((myMech != null) &&
                ((attackType == (int)AIUtil.AttackType.Melee) ||
                    (attackType == (int)AIUtil.AttackType.DeathFromAbove)))
            {
                for (int weaponSetIndex = 0; weaponSetIndex < weaponSetsForAttackType[attackType].Count; ++weaponSetIndex)
                {
                    if (attackType == (int)AIUtil.AttackType.Melee)
                    {
                        weaponSetsForAttackType[attackType][weaponSetIndex].Add(myMech.MeleeWeapon);
                    }
                    if (attackType == (int)AIUtil.AttackType.DeathFromAbove)
                    {
                        weaponSetsForAttackType[attackType][weaponSetIndex].Add(myMech.DFAWeapon);
                    }

                    // TODO - Verify melee attacks don't double-dip
                    for (int wi = 0; wi < myMech.Weapons.Count; ++wi)
                    {
                        Weapon w = myMech.Weapons[wi];
                        if (w.CanFire &&
                            (w.WeaponCategoryValue.CanUseInMelee) &&
                            (!weaponSetsForAttackType[attackType][weaponSetIndex].Contains(w)))
                        {
                            weaponSetsForAttackType[attackType][weaponSetIndex].Add(w);
                        }
                    }
                }
            }
        }

        List<AttackEvaluation> evaluations = AttackEvaluation.EvaluateAttacks(unit, target, weaponSetsForAttackType, unit.CurrentPosition, target.CurrentPosition, targetIsEvasive);

        LogShoot(unit, string.Format("found {0} different attack solutions", evaluations.Count));

        float bestShootDmg = 0;
        float bestMeleeDmg = 0;
        float bestDFADmg = 0;

        for (int evaluationIndex = 0; evaluationIndex < evaluations.Count; ++evaluationIndex)
        {
            AttackEvaluation evaluatedAttack = evaluations[evaluationIndex];
            LogShoot(unit, string.Format("evaluated attack of type {0} with {1} weapons and a result of {2}",
                evaluatedAttack.AttackType, evaluatedAttack.WeaponList.Count, evaluatedAttack.ExpectedDamage));

            switch (evaluatedAttack.AttackType)
            {
                case AIUtil.AttackType.Shooting:
                    bestShootDmg = Mathf.Max(bestShootDmg, evaluatedAttack.ExpectedDamage);
                    break;
                case AIUtil.AttackType.Melee:
                    bestMeleeDmg = Mathf.Max(bestMeleeDmg, evaluatedAttack.ExpectedDamage);
                    break;
                case AIUtil.AttackType.DeathFromAbove:
                    bestDFADmg = Mathf.Max(bestDFADmg, evaluatedAttack.ExpectedDamage);
                    break;
                default:
                    Debug.Log("unknown attack type: " + evaluatedAttack.AttackType);
                    break;
            }
        }
        LogShoot(unit, "best shooting: " + bestShootDmg);
        LogShoot(unit, "best melee: " + bestMeleeDmg);
        LogShoot(unit, "best dfa: " + bestDFADmg);

        for (int evaluationIndex = 0; evaluationIndex < evaluations.Count; ++evaluationIndex)
        {
            AttackEvaluation evaluatedAttack = evaluations[evaluationIndex];

            LogShoot(unit, "evaluating attack solution #" + evaluationIndex);
            LogShoot(unit, "------");
            LogShoot(unit, "Weapons:");
            foreach (Weapon w in evaluatedAttack.WeaponList)
            {
                LogShoot(unit, "Weapon: " + w.Name);
            }

            LogShoot(unit, "heat generated for attack solution: " + evaluatedAttack.HeatGenerated);
            LogShoot(unit, "current heat: " + myHeatLevel);
            LogShoot(unit, "acceptable heat: " + acceptableHeat);
            bool willOverheat = evaluatedAttack.HeatGenerated + myHeatLevel > acceptableHeat;
            LogShoot(unit, "will overheat? " + willOverheat);

            // don't accept an overheat attack if it would cause this unit's death!
            if (willOverheat && myMech.OverheatWillCauseDeath())
            {
                LogShoot(unit, "rejecting attack because overheat would cause own death");
                continue;
            }

            bool enoughDamageForOverheatAttack = damageLevel >= overheatDamageLevel;
            LogShoot(unit, "but enough damage for overheat attack? " + enoughDamageForOverheatAttack);
            bool enoughAccuracyForOverheatAttack = evaluatedAttack.lowestHitChance >= overheatAccuracyLevel;
            LogShoot(unit, "but enough accuracy for overheat attack? " + enoughAccuracyForOverheatAttack);

            AbstractActor targetActor = target as AbstractActor;

            if ((evaluatedAttack.AttackType == AIUtil.AttackType.Melee) &&
                ((!unit.CanEngageTarget(target)) ||
                    (targetActor == null) ||
                    (!isStationary)))
            {
                // can't melee
                LogShoot(unit, "Can't Melee");
                continue;
            }

            if ((evaluatedAttack.AttackType == AIUtil.AttackType.DeathFromAbove) &&
                ((!unit.CanDFATargetFromPosition(target, unit.CurrentPosition)) ||
                    (damageLevel < dfaDamageLevel) ||
                    (myLegDamageLevel > dfaOwnMaxDamageLevel)))
            {
                // can't DFA
                LogShoot(unit, "Can't DFA");
                continue;
            }

            if (willOverheat && ((!enoughDamageForOverheatAttack) || (!enoughAccuracyForOverheatAttack)))
            {
                // reject this attack
                LogShoot(unit, "rejecting attack for not enough damage or accuracy on an attack that will overheat");
                continue;
            }
            else if (evaluatedAttack.WeaponList.Count == 0)
            {
                LogShoot(unit, "rejecting attack for not having any weapons");
                continue;
            }
            else if (evaluatedAttack.ExpectedDamage <= 0)
            {
                LogShoot(unit, "rejecting attack for not having any expected damage");
                continue;
            }
            else // valid attack
            {
                BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);

                MultiTargetAttackOrderInfo multiAttackOrder;
                CalledShotAttackOrderInfo calledShotOrder;

                // try to make an "offensive push" morale-based called shot order
                CalledShotAttackOrderInfo offensivePushOrder = MakeOffensivePushOrder(unit, evaluatedAttack, enemyUnitIndex);
                if (offensivePushOrder != null)
                {
                    results.orderInfo = offensivePushOrder;
                    results.debugOrderString = unit.DisplayName + " using offensive push";
                }
                // try to make a called shot order
                else if ((calledShotOrder = MakeCalledShotOrder(unit, evaluatedAttack, enemyUnitIndex, false)) != null)
                {
                    results.orderInfo = calledShotOrder;
                    results.debugOrderString = unit.DisplayName + " using called shot";
                }
                // try to make a multi-attack order
                else if ((!willOverheat) &&
                         ((multiAttackOrder = MultiAttack.MakeMultiAttackOrder(unit, evaluatedAttack, enemyUnitIndex)) != null))
                {
                    results.orderInfo = multiAttackOrder;
                    results.debugOrderString = unit.DisplayName + " using multi attack";
                }
                else
                {
                    AttackOrderInfo attackOrderInfo = new AttackOrderInfo(target);
                    attackOrderInfo.Weapons = evaluatedAttack.WeaponList;
                    attackOrderInfo.TargetUnit = target;
                    attackOrderInfo.VentFirst = (willOverheat && unit.HasVentCoolantAbility && unit.CanVentCoolant);

                    // For melee attacks, remove the melee weapon from the list. It's automatically used in melee attacks so the attackOrderInfo weapons needs to just be the AnitPersonnel ones.
                    switch (evaluatedAttack.AttackType)
                    {
                        case AIUtil.AttackType.Melee:
                            {
                                attackOrderInfo.IsMelee = true;
                                attackOrderInfo.Weapons.Remove(myMech.MeleeWeapon);
                                attackOrderInfo.Weapons.Remove(myMech.DFAWeapon);
                                Debug.Assert(myMech.CanEngageTarget(target));
                                List<PathNode> meleePathNodes = myMech.Pathing.GetMeleeDestsForTarget(targetActor);
                                if (meleePathNodes.Count == 0)
                                {
                                    LogShoot(unit, "Failing for lack of melee destinations");
                                    continue;
                                }
                                attackOrderInfo.AttackFromLocation = myMech.FindBestPositionToMeleeFrom(targetActor, meleePathNodes);
                                break;
                            }
                        case AIUtil.AttackType.DeathFromAbove:
                            {
                                attackOrderInfo.IsDeathFromAbove = true;
                                attackOrderInfo.Weapons.Remove(myMech.MeleeWeapon);
                                attackOrderInfo.Weapons.Remove(myMech.DFAWeapon);
                                Debug.Assert(myMech.CanDFA);
                                List<PathNode> dfaDestNodes = myMech.JumpPathing.GetDFADestsForTarget(targetActor);
                                if (dfaDestNodes.Count == 0)
                                {
                                    LogShoot(unit, "Failing for lack of DFA destinations");
                                    continue;
                                }
                                attackOrderInfo.AttackFromLocation = myMech.FindBestPositionToMeleeFrom(targetActor, dfaDestNodes);
                                break;
                            }
                    }
                    results.orderInfo = attackOrderInfo;
                    results.debugOrderString = unit.DisplayName + " using attack type: " + evaluatedAttack.AttackType + " against: " + target.DisplayName;
                }

                LogShoot(unit, "attack order: " + results.debugOrderString);

                order = results;
                return evaluatedAttack.ExpectedDamage;
            }
        }

        LogShoot(unit, "There are no targets I can shoot at without overheating.");
        order = null;
        return 0.0f;
    }

    static CalledShotAttackOrderInfo MakeOffensivePushOrder(AbstractActor attackingUnit, AttackEvaluation evaluatedAttack, int enemyUnitIndex)
    {
        if ((!attackingUnit.CanUseOffensivePush()) || (!EvaluateInspirationValueNode.ShouldUnitUseInspire(attackingUnit)))
        {
            return null;
        }
        else
        {
            return MakeCalledShotOrder(attackingUnit, evaluatedAttack, enemyUnitIndex, true);
        }
    }

    static CalledShotAttackOrderInfo MakeCalledShotOrder(AbstractActor attackingUnit, AttackEvaluation evaluatedAttack, int enemyUnitIndex, bool isMoraleAttack)
    {
        ICombatant targetUnit = attackingUnit.BehaviorTree.enemyUnits[enemyUnitIndex];
        Mech targetMech = targetUnit as Mech;

        if ((targetMech == null) || (!targetMech.IsVulnerableToCalledShots()) || (evaluatedAttack.AttackType == AIUtil.AttackType.Melee) || (evaluatedAttack.AttackType == AIUtil.AttackType.DeathFromAbove))
        {
            return null;
        }

        Mech attackingMech = attackingUnit as Mech;

        for (int weaponIndex = 0; weaponIndex < evaluatedAttack.WeaponList.Count; ++weaponIndex)
        {
            Weapon w = evaluatedAttack.WeaponList[weaponIndex];
            if ((w.WeaponCategoryValue.IsMelee) ||
                (w.Type == WeaponType.Melee) ||
                ((attackingMech != null) &&
                    ((w == attackingMech.DFAWeapon) ||
                        (w == attackingMech.MeleeWeapon))))
            {
                return null;
            }
        }

        List<ArmorLocation> armorLocs = new List<ArmorLocation>
        {
            ArmorLocation.Head,
            ArmorLocation.CenterTorso,
            ArmorLocation.LeftTorso,
            ArmorLocation.LeftArm,
            ArmorLocation.LeftLeg,
            ArmorLocation.RightTorso,
            ArmorLocation.RightArm,
            ArmorLocation.RightLeg,
        };

        List<ChassisLocations> hitLocations = new List<ChassisLocations>
        {
            ChassisLocations.Head,
            ChassisLocations.CenterTorso,
            ChassisLocations.LeftTorso,
            ChassisLocations.LeftArm,
            ChassisLocations.LeftLeg,
            ChassisLocations.RightTorso,
            ChassisLocations.RightArm,
            ChassisLocations.RightLeg,
        };

        List<float> targetChances = new List<float>(armorLocs.Count);
        float totalChance = 0.0f;
        for (int locIndex = 0; locIndex < armorLocs.Count; ++locIndex)
        {
            float c = CalcCalledShotLocationTargetChance(targetMech, armorLocs[locIndex], hitLocations[locIndex]);
            targetChances.Add(c);
            totalChance += c;
        }

        float roll = Random.Range(0.0f, totalChance);

        CalledShotAttackOrderInfo order = null;

        for (int locIndex = 0; locIndex < armorLocs.Count; ++locIndex)
        {
            float targ = targetChances[locIndex];
            if (roll < targ)
            {
                order = new CalledShotAttackOrderInfo(targetMech, armorLocs[locIndex], isMoraleAttack);
                break;
            }
            roll -= targ;
        }
        if (order == null)
        {
            Debug.LogError("Failed to calculate called shot. Targeting head as fallback.");
            order = new CalledShotAttackOrderInfo(targetMech, ArmorLocation.Head, isMoraleAttack);
        }

        for (int weaponIndex = 0; weaponIndex < evaluatedAttack.WeaponList.Count; ++weaponIndex)
        {
            Weapon w = evaluatedAttack.WeaponList[weaponIndex];
            AIUtil.LogAI("Called Shot: Adding weapon " + w.Name);
            order.AddWeapon(w);
        }
        return order;
    }

    static float CalcCalledShotLocationTargetChance(Mech targetMech, ArmorLocation armorLoc, ChassisLocations chassisLoc)
    {
        float chanceAccumulator = 0.0f;
        LocationDamageLevel damageLevel = targetMech.GetLocationDamageLevel(chassisLoc);
        if (damageLevel == LocationDamageLevel.Destroyed)
        {
            return 0.0f;
        }
        switch (armorLoc)
        {
            case ArmorLocation.Head: chanceAccumulator = targetMech.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CalledShotHeadBaseChance).FloatVal; break;
            case ArmorLocation.CenterTorso: chanceAccumulator = targetMech.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CalledShotCenterTorsoBaseChance).FloatVal; break;
            default: chanceAccumulator = targetMech.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CalledShotOtherBaseChance).FloatVal; break;
        }
        if (damageLevel == LocationDamageLevel.Penalized || damageLevel == LocationDamageLevel.NonFunctional)
        {
            chanceAccumulator *= targetMech.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CalledShotDamagedChanceMultiplier).FloatVal;
        }

        List<MechComponent> components = targetMech.GetComponentsForLocation(chassisLoc, ComponentType.Weapon);

        float weaponDamage = 0.0f;
        for (int componentIndex = 0; componentIndex < components.Count; ++componentIndex)
        {
            Weapon w = components[componentIndex] as Weapon;
            if ((w != null) && (w.CanFire))
            {
                float dmg = w.ShotsWhenFired * w.DamagePerShot;
                weaponDamage += dmg;
            }
        }

        chanceAccumulator += targetMech.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CalledShotWeaponDamageChance).FloatVal * weaponDamage;

        return chanceAccumulator;
    }

    static ActiveAbilityOrderInfo MakeActiveAbilityOrderInfo(AbstractActor attackingUnit, AbstractActor targetUnit, ActiveAbilityID ability)
    {
        switch (ability)
        {
            case ActiveAbilityID.SensorLock: return new SensorLockOrderInfo(attackingUnit, targetUnit);
            default: Debug.LogError("unknown ability: " + ability); return null;
        }
    }
}


public class OrderInfo
{
    public float priority;

    OrderType orderTypeBacking;
    public OrderInfo(OrderType orderType)
    {
        orderTypeBacking = orderType;
    }

    public virtual OrderType OrderType
    {
        get
        {
            return orderTypeBacking;
        }
    }

    public override string ToString()
    {
        return string.Format("Order: {0} Type: {1}", this.GetType(), this.OrderType);
    }
}

public class MovementOrderInfo : OrderInfo
{
    public MovementOrderInfo(Vector3 destination, Vector3 lookAt) : base(OrderType.Undefined)
    {
        this.Destination = destination;
        this.LookAt = lookAt;
    }

    public override OrderType OrderType
    {
        get
        {
            if (IsSprinting)
            {
                return OrderType.SprintMove;
            }
            if (IsJumping)
            {
                return OrderType.JumpMove;
            }
            return OrderType.Move;
        }
    }
    public Vector3 Destination;
    public Vector3 LookAt;
    public bool IsSprinting;
    public bool IsJumping;
    public bool IsReverse;
    public bool IsMelee;
}


public class AttackOrderInfo : OrderInfo
{
    public bool IsMelee;
    public bool IsDeathFromAbove;
    public List<Weapon> Weapons;
    public Vector3 AttackFromLocation;
    public ICombatant TargetUnit;
    public bool VentFirst;

    public AttackOrderInfo(ICombatant targetUnit, OrderType orderType) : base(orderType)
    {
        this.TargetUnit = targetUnit;
        Weapons = new List<Weapon>();
        IsMelee = false;
        IsDeathFromAbove = false;
        VentFirst = false;
    }

    public AttackOrderInfo(ICombatant targetUnit) : this(targetUnit, OrderType.Attack)
    {
    }

    public AttackOrderInfo(AbstractActor targetUnit, bool isMelee, bool isDeathFromAbove) : this(targetUnit)
    {
        IsMelee = isMelee;
        IsDeathFromAbove = isDeathFromAbove;
    }

    public void AddWeapon(Weapon weapon)
    {
        if (!Weapons.Contains(weapon))
        {
            Weapons.Add(weapon);
        }
    }
}

public class MultiTargetAttackOrderInfo : OrderInfo
{
    public List<AttackOrderInfo> SubTargetOrders;

    public MultiTargetAttackOrderInfo() : base(OrderType.MultiTargetAttack)
    {
        SubTargetOrders = new List<AttackOrderInfo>();
    }

    public void AddAttack(AttackOrderInfo subTargetOrder)
    {
        SubTargetOrders.Add(subTargetOrder);
    }
}

public class CalledShotAttackOrderInfo : AttackOrderInfo
{
    public readonly int TargetLocation;
    public readonly bool IsMoraleAttack;
    public CalledShotAttackOrderInfo(ICombatant targetUnit, ArmorLocation loc, bool isMoraleAttack) : base(targetUnit, OrderType.CalledShotAttack)
    {
        TargetLocation = (int)loc;
        this.IsMoraleAttack = isMoraleAttack;
    }
}

public abstract class ActiveAbilityOrderInfo : OrderInfo
{
    public AbstractActor movingUnit;
    public ICombatant targetUnit;

    public ActiveAbilityOrderInfo(AbstractActor movingUnit, ICombatant targetUnit) : base(OrderType.ActiveAbility)
    {
        this.movingUnit = movingUnit;
        this.targetUnit = targetUnit;
    }

    public abstract ActiveAbilityID GetActiveAbilityID();
}

public class SensorLockOrderInfo : ActiveAbilityOrderInfo
{
    public SensorLockOrderInfo(AbstractActor movingUnit, ICombatant targetUnit) : base(movingUnit, targetUnit)
    {
    }

    public override ActiveAbilityID GetActiveAbilityID()
    {
        return ActiveAbilityID.SensorLock;
    }
}

public class ActiveProbeOrderInfo : OrderInfo
{
    public AbstractActor MovingUnit;
    public List<ICombatant> Targets = null;

    public ActiveProbeOrderInfo(AbstractActor movingUnit, List<ICombatant> targets) : base(OrderType.ActiveProbe)
    {
        MovingUnit = movingUnit;
        Targets = targets;
    }
}

public class ClaimInspirationOrderInfo : OrderInfo
{
    public AbstractActor movingUnit;

    public ClaimInspirationOrderInfo(AbstractActor movingUnit) : base(OrderType.ClaimInspiration)
    {
        this.movingUnit = movingUnit;
    }
}

public class BehaviorTreeResults
{
    public BehaviorNodeState nodeState;
    public OrderInfo orderInfo;
    public string debugOrderString;
    public string behaviorTrace;

    public BehaviorTreeResults(BehaviorNodeState nodeState)
    {
        this.nodeState = nodeState;
        this.behaviorTrace = "";
    }

    public static BehaviorTreeResults BehaviorTreeResultsFromBoolean(bool success)
    {
        return new BehaviorTreeResults(success ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}

public abstract class BehaviorNode
{
    protected string name;
    protected BehaviorTree tree;
    protected AbstractActor unit;

    // Only used by PrioritySelector or PrioritySequence nodes.
    public int Priority;

    public BehaviorNode(string name, BehaviorTree tree, AbstractActor unit)
    {
        this.name = name;
        this.tree = tree;
        this.unit = unit;
    }

    public void LogAI(string info, string loggerName = HBS.Logging.LoggerNames.AI_BEHAVIORNODES)
    {
        LogAI(unit, info, loggerName);
    }

    public static void LogAI(AbstractActor unit, string info, string loggerName = HBS.Logging.LoggerNames.AI_BEHAVIORNODES)
    {
        AIUtil.LogAI(info, loggerName);
        AITeam team = unit.team as AITeam;
        string outString = string.Format("Log[{0}] : {1}\n", loggerName, info);
        team.behaviorTreeLogString += outString;
        unit.BehaviorTree.behaviorTraceStringBuilder.AppendLine(outString);
    }

    /// <summary>
    /// This is a convenience wrapper around Tick() which calls into OnStart and OnComplete.
    /// </summary>
    public BehaviorTreeResults Update()
    {
        AITeam team = unit.team as AITeam;
        if (currentState != BehaviorNodeState.Running)
        {
            //LogAI("Behavior Node " + this.name + " starting", HBS.Logging.LoggerNames.AI_BEHAVIORNODES);
            currentState = BehaviorNodeState.Running;
            team.behaviorTreeLogString += "starting " + this.name + ".\n";
            try
            {
                OnStart();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning( string.Format("AI Behavior Tree Error in OnStart for {0} : {1}", this.name, e));
                team.behaviorTreeLogString += "OnStart failed " + this.name + "\n" + e;

                // BT-24198: Changed these AI Behavior Tree Error messages to warnings, and hiding the AI ASSERT floatie, so that they can't be player-facing
                /*if (DebugBridge.TestToolsEnabled)
                    unit.Combat.MessageCenter.PublishMessage(new FloatieMessage(unit.GUID, unit.GUID, new Localize.Text("AI ASSERT"), FloatieMessage.MessageNature.AIDebug));*/

                return null;
            }
        }
        //LogAI("Behavior Node " + this.name + " running", HBS.Logging.LoggerNames.AI_BEHAVIORNODES);
        BehaviorTreeResults results;
        try
        {
            results = Tick();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning( string.Format("AI Behavior Tree Error in Tick for {0} : {1}", this.name, e));
            team.behaviorTreeLogString += "Tick failed " + this.name + "\n" + e;

            /*if (DebugBridge.TestToolsEnabled)
                unit.Combat.MessageCenter.PublishMessage(new FloatieMessage(unit.GUID, unit.GUID, new Localize.Text("AI ASSERT"), FloatieMessage.MessageNature.AIDebug));*/

            return null;
        }
        if (results == null)
        {
            Debug.LogWarning("null results from " + this.name);
            team.behaviorTreeLogString += "Null results from " + this.name;

            /*if (DebugBridge.TestToolsEnabled)
                unit.Combat.MessageCenter.PublishMessage(new FloatieMessage(unit.GUID, unit.GUID, new Localize.Text("AI ASSERT"), FloatieMessage.MessageNature.AIDebug));*/

            return null;
        }
        currentState = results.nodeState;
        //LogAI("Behavior Node " + this.name + " now in state: " + currentState, HBS.Logging.LoggerNames.AI_BEHAVIORNODES);
        if (currentState != BehaviorNodeState.Running)
        {
            //LogAI("Behavior Node " + this.name + " completing", HBS.Logging.LoggerNames.AI_BEHAVIORNODES);
            try
            {
                OnComplete();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(string.Format("AI Behavior Tree Error in OnComplete for {0} : {1}", this.name, e));
                team.behaviorTreeLogString += "OnComplete error in " + this.name + "\n" + e;
                return null;
            }
            team.behaviorTreeLogString += "completed " + this.name + " with " + currentState + " ";
            tree.behaviorTraceStringBuilder.AppendLine(string.Format("in update completed for {0} with result {1}.", this.name, currentState.ToString()));

            if (results.orderInfo != null)
            {
                team.behaviorTreeLogString += "and order info: " + results.orderInfo + "\n";
                tree.behaviorTraceStringBuilder.AppendLine(string.Format("order info {0}. \r\n", results.orderInfo));
            }
            else
            {
                team.behaviorTreeLogString += "and NULL order info.\n";
                tree.behaviorTraceStringBuilder.AppendLine("NULL order info. \r\n");
            }
        }
        return results;
    }

    public virtual void Reset()
    {
        currentState = BehaviorNodeState.Ready;
    }

    protected virtual void OnStart() { }
    abstract protected BehaviorTreeResults Tick();
    protected virtual void OnComplete() { }

    private BehaviorNodeState currentState;
}

public abstract class LeafBehaviorNode : BehaviorNode
{
    public LeafBehaviorNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }
}

public abstract class DecoratorBehaviorNode : BehaviorNode
{
    public BehaviorNode ChildNode;

    public DecoratorBehaviorNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    public void AddChild(BehaviorNode child)
    {
        Debug.Assert(ChildNode == null);
        ChildNode = child;
    }

    public override void Reset()
    {
        base.Reset();
        ChildNode.Reset();
    }
}

public abstract class CompositeBehaviorNode : BehaviorNode
{
    public List<BehaviorNode> Children;

    public CompositeBehaviorNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
        Children = new List<BehaviorNode>();
    }

    public void AddChild(BehaviorNode child)
    {
        Children.Add(child);
    }

    public override void Reset()
    {
        base.Reset();
        for (int childIndex = 0; childIndex < Children.Count; ++childIndex)
        {
            Children[childIndex].Reset();
        }
    }
}

public class SequenceNode : CompositeBehaviorNode
{
    private int currentChildIndex;

    public SequenceNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected void OnStart()
    {
        currentChildIndex = 0;
    }

    override protected BehaviorTreeResults Tick()
    {
        OrderInfo orders = null;
        while (true)
        {
            BehaviorTreeResults childResults = Children[currentChildIndex].Update();
            if ((childResults != null) && (childResults.orderInfo != null))
            {
                orders = childResults.orderInfo;
            }

            if (childResults.nodeState != BehaviorNodeState.Success)
            {
                return childResults;
            }
            currentChildIndex += 1;
            if (currentChildIndex == Children.Count)
            {
                childResults.nodeState = BehaviorNodeState.Success;
                childResults.orderInfo = orders;
                childResults.debugOrderString = this.name + " > " + childResults.debugOrderString;
                return childResults;
            }
        }
    }
}

public class SelectorNode : CompositeBehaviorNode
{
    private int currentChildIndex;

    public SelectorNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected void OnStart()
    {
        currentChildIndex = 0;
    }

    override protected BehaviorTreeResults Tick()
    {
        OrderInfo orders = null;
        while (true)
        {
            BehaviorTreeResults childResults = Children[currentChildIndex].Update();
            if (childResults.orderInfo != null)
            {
                orders = childResults.orderInfo;
            }

            if (childResults.nodeState != BehaviorNodeState.Failure)
            {
                BehaviorTreeResults results = new BehaviorTreeResults(childResults.nodeState);
                results.orderInfo = orders;
                results.debugOrderString = this.name + " > " + childResults.debugOrderString;
                return results;
            }

            currentChildIndex += 1;
            if (currentChildIndex == Children.Count)
            {
                childResults.nodeState = BehaviorNodeState.Failure;
                return childResults;
            }
        }
    }
}

public class InverterNode : DecoratorBehaviorNode
{
    public InverterNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorTreeResults childResults = ChildNode.Update();
        switch (childResults.nodeState)
        {
            case BehaviorNodeState.Failure:
                childResults.nodeState = BehaviorNodeState.Success;
                break;
            case BehaviorNodeState.Success:
                childResults.nodeState = BehaviorNodeState.Failure;
                break;
        }
        return childResults;
    }
}


/// <summary>
/// Success Decorator Behavior Node.
/// A decorator that calls its child, passing "success" back for success or failure. On "running", it will pass that
/// through. Good for eliminating failures from parts of the tree not really in conditionals, or for guarding a sequence
/// from unexpected failures.
/// </summary>
public class SuccessDecoratorNode : DecoratorBehaviorNode
{
    public SuccessDecoratorNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorTreeResults childResults = ChildNode.Update();

        if (childResults.nodeState == BehaviorNodeState.Failure)
        {
            childResults.nodeState = BehaviorNodeState.Success;
        }
        return childResults;
    }
}


/// <summary>
/// Timer Behavior Node.
/// A decorator that calls its child, passing through success/failure. On "running", it will re-call its child up to a
/// fixed maximum number of times, then return a failure.
/// </summary>
public class TimerNode : DecoratorBehaviorNode
{
    public int MaxCalls;
    private int currentCalls;

    public TimerNode(string name, BehaviorTree tree, AbstractActor unit, int MaxCalls) : base(name, tree, unit)
    {
        this.MaxCalls = MaxCalls;
        currentCalls = 0;
    }

    protected override void OnStart()
    {
        base.OnStart();
        currentCalls = 0;
    }

    protected override BehaviorTreeResults Tick()
    {
        BehaviorTreeResults childResults = ChildNode.Update();
        switch (childResults.nodeState)
        {
            case BehaviorNodeState.Running:
                currentCalls++;
                if (currentCalls == MaxCalls)
                {
                    childResults.nodeState = BehaviorNodeState.Failure;
                    return childResults;
                }

                BehaviorTreeResults res = new BehaviorTreeResults(BehaviorNodeState.Running);
                res.debugOrderString = "Timer > " + childResults.debugOrderString;
                return res;
            default:
                currentCalls = 0;
                return childResults;
        }
    }
}

class LanceHasLOSNode : LeafBehaviorNode
{
    public LanceHasLOSNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        List<Team> teams = tree.battleTechGame.Combat.Teams;
        Team myTeam = teams.Find(x => x.GUID == unit.TeamId);
        // foreach member of my lance
        for (int unitIndex = 0; unitIndex < myTeam.units.Count; ++unitIndex)
        {
            AbstractActor testUnit = myTeam.units[unitIndex];
            List<string> magicallyVisibleEnemyGUIDs = unit.GetMagicallyVisibleUnitGUIDs();

            foreach (Team otherTeam in teams)
            {
                if (otherTeam == myTeam)
                {
                    continue;
                }

                // foreach enemy
                // if has LOS, return success
                foreach (AbstractActor enemyUnit in otherTeam.units)
                {
                    if ((magicallyVisibleEnemyGUIDs.Contains(enemyUnit.GUID)) ||
                        testUnit.HasLOSToTargetUnit(enemyUnit))
                    {
                        BehaviorTreeResults res = new BehaviorTreeResults(BehaviorNodeState.Success);
                        res.debugOrderString = this.name;
                        return res;
                    }
                }
            }
        }
        // if no LOS, return failure
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class LanceDetectsEnemiesNode : LeafBehaviorNode
{
    public LanceDetectsEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        List<Team> teams = tree.battleTechGame.Combat.Teams;
        Team myTeam = teams.Find(x => x.GUID == unit.TeamId);
        // foreach member of my lance
        foreach (AbstractActor testUnit in myTeam.units)
        {
            List<string> magicallyVisibleEnemyGUIDs = unit.GetMagicallyVisibleUnitGUIDs();

            foreach (Team otherTeam in teams)
            {
                if (otherTeam == myTeam)
                {
                    continue;
                }

                // foreach enemy
                // if has detection, return success
                foreach (AbstractActor enemyUnit in otherTeam.units)
                {
                    if ((magicallyVisibleEnemyGUIDs.Contains(enemyUnit.GUID)) ||
                        testUnit.HasDetectionToTargetUnit(enemyUnit))
                    {
                        BehaviorTreeResults res = new BehaviorTreeResults(BehaviorNodeState.Success);
                        res.debugOrderString = this.name;
                        return res;
                    }
                }
            }
        }
        // if no detection, return failure
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}


class LanceDetectsNonHackedEnemiesNode : LeafBehaviorNode
{
    public LanceDetectsNonHackedEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        List<Team> teams = tree.battleTechGame.Combat.Teams;
        Team myTeam = teams.Find(x => x.GUID == unit.TeamId);
        // foreach member of my lance
        foreach (AbstractActor testUnit in myTeam.units)
        {
            List<string> magicallyVisibleEnemyGUIDs = unit.GetMagicallyVisibleUnitGUIDs();

            foreach (Team otherTeam in teams)
            {
                if (otherTeam == myTeam)
                {
                    continue;
                }

                // foreach enemy
                // if has detection, return success
                foreach (AbstractActor enemyUnit in otherTeam.units)
                {
                    if (tree.IsTargetIgnored(enemyUnit))
                    {
                        continue;
                    }

                    if ((magicallyVisibleEnemyGUIDs.Contains(enemyUnit.GUID)) ||
                        testUnit.HasDetectionToTargetUnit(enemyUnit))
                    {
                        BehaviorTreeResults res = new BehaviorTreeResults(BehaviorNodeState.Success);
                        res.debugOrderString = this.name;
                        return res;
                    }
                }
            }
        }
        // if no detection, return failure
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}


class IsInterleavedNode : LeafBehaviorNode
{
    public IsInterleavedNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.Combat.TurnDirector.IsInterleaved);
    }
}

class IsInMeleeNode : LeafBehaviorNode
{
    public IsInMeleeNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return new BehaviorTreeResults(/*unit.IsInMelee ? BehaviorNodeState.Success : */BehaviorNodeState.Failure);
    }
}

class SortEnemiesByThreatNode : LeafBehaviorNode
{
    public SortEnemiesByThreatNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        AIThreatUtil.SortHostileUnitsByThreat(unit, tree.enemyUnits);

        AIUtil.LogAI("Enemy Units Sorted By Threat", unit);
        for (int i = 0; i < tree.enemyUnits.Count; ++i)
        {
            string msg = string.Format("{0} - {1}", i, tree.enemyUnits[i].LogDisplayName);
            AIUtil.LogAI(msg, unit);
        }

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

// Not heavily used.
class SortEnemiesByEffectivenessNode : LeafBehaviorNode
{
    public SortEnemiesByEffectivenessNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    private float ComputeExpectedDamage(ICombatant targetUnit)
    {
        Mech targetMech = targetUnit as Mech;
        bool targetIsEvasive = (targetMech != null) && targetMech.IsEvasive;

        if (!AIUtil.UnitHasVisibilityToTargetFromCurrentPosition(unit, targetUnit))
        {
            // Our team can't see this hostile.
            return 0.0f;
        }

        float expectedDamage = 0;
        for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
        {
            Weapon weapon = unit.Weapons[weaponIndex];

            if (!weapon.CanFire)
            {
                continue;
            }

            if (unit.Combat.LOFCache.UnitHasLOFToTarget(unit, targetUnit, weapon) &&
                weapon.WillFireAtTargetFromPosition(targetUnit, unit.CurrentPosition, unit.CurrentRotation))
            {
                int numShots = weapon.ShotsWhenFired;
                float toHit = weapon.GetToHitFromPosition(targetUnit, 1, unit.CurrentPosition, targetUnit.CurrentPosition, true, targetIsEvasive);
                float damagePerShot = weapon.DamagePerShotFromPosition(MeleeAttackType.NotSet, unit.CurrentPosition, targetUnit);
                float heatDamagePerShot = 1 + (weapon.HeatDamagePerShot); // Factoring in heatDamagePerShot. +1, since most weapons deal 0 heat Dmg
                expectedDamage += numShots * toHit * damagePerShot + heatDamagePerShot;
            }
        }
        return expectedDamage;
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Sort((x, y) => ComputeExpectedDamage(x).CompareTo(ComputeExpectedDamage(y)));
        tree.enemyUnits.Reverse();

        // simply do the threat / effectiveness calculations, store the values in the tree
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class SortEnemiesByProximityNode : LeafBehaviorNode
{
    public SortEnemiesByProximityNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    private float ComputeProximity(ICombatant enemyUnit)
    {
        Vector3 deltaPosition = enemyUnit.CurrentPosition - unit.CurrentPosition;
        return deltaPosition.sqrMagnitude;
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Sort((x, y) => ComputeProximity(x).CompareTo(ComputeProximity(y)));
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

/// <summary>
/// Sorts enemies by records in the Target Priority Record List. Needs to be a stable sort, so we can run this after
/// another sort and preserve the previous sort within priorities.
/// </summary>
class SortEnemiesByPriorityListNode : LeafBehaviorNode
{
    public SortEnemiesByPriorityListNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Dictionary<int, List<ICombatant>> priorityTable = new Dictionary<int, List<ICombatant>>();

        for (int unitIndex = 0; unitIndex < tree.enemyUnits.Count; ++unitIndex)
        {
            ICombatant targetUnit = tree.enemyUnits[unitIndex];

            if (targetUnit.IsDead)
            {
                continue;
            }

            int priority = tree.TargetPriorityForUnit(targetUnit);
            if (!priorityTable.ContainsKey(priority))
            {
                priorityTable[priority] = new List<ICombatant>();
            }
            priorityTable[priority].Add(targetUnit);
        }

        List<ICombatant> combatants = unit.Combat.GetAllMiscCombatants();
        for (int combatantIndex = 0; combatantIndex < combatants.Count; ++combatantIndex)
        {
            ICombatant targetCombatant = combatants[combatantIndex];
            int priority = tree.TargetPriorityForUnit(targetCombatant);
            if ((priority <= 0) || (targetCombatant.IsDead))
            {
                continue;
            }
            if (!priorityTable.ContainsKey(priority))
            {
                priorityTable[priority] = new List<ICombatant>();
            }
            priorityTable[priority].Add(targetCombatant);
        }


        List<int> priorityLevels = new List<int>(priorityTable.Keys);
        priorityLevels.Sort();
        priorityLevels.Reverse();

        bool anyNonZeroPriority = ((priorityLevels.Count > 0) && (priorityLevels[0] > 0));

        if (!anyNonZeroPriority)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        tree.enemyUnits.Clear();

        for (int priorityLevelIndex = 0; priorityLevelIndex < priorityLevels.Count; ++priorityLevelIndex)
        {
            int priorityLevel = priorityLevels[priorityLevelIndex];
            if ((priorityLevel == 0) && (anyNonZeroPriority))
            {
                // don't care about non-priority targets.
                break;
            }
            List<ICombatant> unitList = priorityTable[priorityLevels[priorityLevelIndex]];
            for (int unitIndex = 0; unitIndex < unitList.Count; ++unitIndex)
            {
                ICombatant target = unitList[unitIndex];
                if (!tree.enemyUnits.Contains(target))
                {
                    tree.enemyUnits.Add(target);
                }
            }
        }

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class SortEnemiesByDistanceToLastSeenLocationNode : LeafBehaviorNode
{
    public SortEnemiesByDistanceToLastSeenLocationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    private float ComputeDistance(ICombatant enemyUnit)
    {
        AbstractActor enemyActor = enemyUnit as AbstractActor;

        if (enemyActor != null)
        {
            if (unit.team.VisibilityCache.previouslyDetectedEnemyLocations.ContainsKey(enemyActor))
            {
                Vector3 seenLoc = unit.team.VisibilityCache.previouslyDetectedEnemyLocations[enemyActor];

                if (unit.Combat.MapMetaData.IsWithinBounds(seenLoc))
                {
                    return (unit.team.VisibilityCache.previouslyDetectedEnemyLocations[enemyActor] - unit.CurrentPosition).magnitude;
                }
            }
            // this unit doesn't have a last seen location - how is that possible?
            return float.MaxValue;
        }
        else
        {
            // non-actor targets (e.g. buildings), we'll assume we have full information.
            return (enemyUnit.CurrentPosition - unit.CurrentPosition).magnitude;
        }
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Sort((x, y) => ComputeDistance(x).CompareTo(ComputeDistance(y)));

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class FindVisibleEnemiesNode : LeafBehaviorNode
{
    public FindVisibleEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Clear();

        List<ICombatant> visibleEnemyUnits = AIUtil.GetVisibleUnitsForUnit(unit);

        // Make sure that all enemy units are really enemies, and that they aren't dead.
        List<ICombatant> filteredEnemyUnitList = new List<ICombatant>();
        for (int veuIndex = 0; veuIndex < visibleEnemyUnits.Count; ++veuIndex)
        {
            ICombatant targetUnit = visibleEnemyUnits[veuIndex];
            if ((!targetUnit.IsDead) &&
                (tree.unit.IsEnemy(targetUnit)))
            {
                filteredEnemyUnitList.Add(targetUnit);
            }
        }

        tree.enemyUnits = filteredEnemyUnitList;
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class FindDetectedEnemiesNode : LeafBehaviorNode
{
    public FindDetectedEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Clear();

        List<ICombatant> detectedEnemyUnits = AIUtil.GetDetectedUnitsForUnit(unit);

        // Make sure that all enemy units are really enemies, and that they aren't dead.
        List<ICombatant> filteredEnemyUnitList = new List<ICombatant>();
        for (int deuIndex = 0; deuIndex < detectedEnemyUnits.Count; ++deuIndex)
        {
            ICombatant targetUnit = detectedEnemyUnits[deuIndex];
            if ((!targetUnit.IsDead) &&
                (tree.unit.IsEnemy(targetUnit)) &&
                (!tree.IsTargetIgnored(targetUnit)))
            {
                filteredEnemyUnitList.Add(targetUnit);
            }
        }

        tree.enemyUnits = filteredEnemyUnitList;
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

/// <summary>
/// As FindDetectedEnemiesNode, above, but filters out enemies whose priority is 0 or less, as the mission has told us not to care about them.
/// </summary>

class FindDetectedNonHackedEnemiesNode : LeafBehaviorNode
{
    public FindDetectedNonHackedEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Clear();

        List<ICombatant> detectedEnemyUnits = AIUtil.GetDetectedUnitsForUnit(unit);

        // Make sure that all enemy units are really enemies, and that they aren't dead.
        List<ICombatant> filteredEnemyUnitList = new List<ICombatant>();
        for (int deuIndex = 0; deuIndex < detectedEnemyUnits.Count; ++deuIndex)
        {
            ICombatant targetUnit = detectedEnemyUnits[deuIndex];
            if ((!targetUnit.IsDead) &&
                (tree.unit.IsEnemy(targetUnit)) &&
                (!tree.IsTargetIgnored(targetUnit)))
            {
                filteredEnemyUnitList.Add(targetUnit);
            }
        }

        tree.enemyUnits = filteredEnemyUnitList;
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}



class FindPreferredEnemiesNode : LeafBehaviorNode
{
    public FindPreferredEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Clear();

        List<string> tags = new List<string>(tree.preferredTargetPriorities.Keys);

        for (int tagIndex = 0; tagIndex < tags.Count; ++tagIndex)
        {
            string tag = tags[tagIndex];

            List<ITaggedItem> items = tree.battleTechGame.Combat.ItemRegistry.WithType(TaggedObjectType.Unit).WithTag(tag).Search();
            items.AddRange(tree.battleTechGame.Combat.ItemRegistry.WithType(TaggedObjectType.Building).WithTag(tag).Search());
            items.AddRange(tree.battleTechGame.Combat.ItemRegistry.WithType(TaggedObjectType.Objective).WithTag(tag).Search());

            for (int itemIndex = 0; itemIndex < items.Count; ++itemIndex)
            {
                ITaggedItem item = items[itemIndex];
                ICombatant actor = item as ICombatant;
                if ((actor != null) && (!actor.IsDead) && (!tree.enemyUnits.Contains(actor)))
                {
                    tree.enemyUnits.Add(actor);
                }
            }
        }

        return new BehaviorTreeResults((tree.enemyUnits.Count > 0) ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}

class WasTargetedRecentlyNode : LeafBehaviorNode
{
    public WasTargetedRecentlyNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(tree.WasUnitTargetedRecently(unit));
    }
}

class HasPriorityTargetNode : LeafBehaviorNode
{
    public HasPriorityTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(tree.HasPriorityTargets());
    }
}

class SortEnemiesByPreferredTargetPriority : LeafBehaviorNode
{
    public SortEnemiesByPreferredTargetPriority(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    int GetPreferredTargetPriority(ICombatant target)
    {
        HBS.Collections.TagSet tags = target.EncounterTags;

        int highestPriority = 0;
        bool foundMatch = false;

        for (int tagIndex = 0; tagIndex < tags.Count; ++tagIndex)
        {
            string tag = tags[tagIndex];

            if (tree.preferredTargetPriorities.ContainsKey(tag))
            {
                int priorityForKey = tree.preferredTargetPriorities[tag];
                if (!foundMatch || priorityForKey > highestPriority)
                {
                    foundMatch = true;
                    highestPriority = priorityForKey;
                }
            }
        }
        return highestPriority;
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits.Sort((x, y) => GetPreferredTargetPriority(x).CompareTo(GetPreferredTargetPriority(y)));
        tree.enemyUnits.Reverse();

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class UsePreferredTargetToHitThreshold : LeafBehaviorNode
{
    public UsePreferredTargetToHitThreshold(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.weaponToHitThreshold = tree.GetBehaviorVariableValue(BehaviorVariableName.Float_PreferredTargetToHitThreshold).FloatVal;
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class UseNormalToHitThreshold : LeafBehaviorNode
{
    public UseNormalToHitThreshold(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.weaponToHitThreshold = tree.GetBehaviorVariableValue(BehaviorVariableName.Float_AccuracyNeededForOverheatAttack).FloatVal;
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class MaybeFilterOutPriorityTargetsNode : LeafBehaviorNode
{
    public MaybeFilterOutPriorityTargetsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorVariableValue shouldFilter = tree.GetBehaviorVariableValue(BehaviorVariableName.Bool_ProhibitTargetingPriorityTargetsAfterBeingTargeted);

        Debug.Assert(shouldFilter != null);

        if (shouldFilter.BoolVal)
        {
            tree.enemyUnits.RemoveAll(x => tree.TargetPriorityForUnit(x) > 0);
        }

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class FilterKeepingRecentAttackersNode : LeafBehaviorNode
{
    public FilterKeepingRecentAttackersNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    bool hasRecentAttacker()
    {
        for (int enemyUnitIndex = 0; enemyUnitIndex < tree.enemyUnits.Count; ++enemyUnitIndex)
        {
            ICombatant enemyUnit = tree.enemyUnits[enemyUnitIndex];

            if (didUnitAttackRecently(enemyUnit))
            {
                return true;
            }
        }
        return false;
    }

    bool didUnitAttackRecently(ICombatant attacker)
    {
        AbstractActor attackerUnit = attacker as AbstractActor;
        if (attackerUnit == null)
        {
            return false;
        }

        int recentTargetThreshold = tree.GetBehaviorVariableValue(BehaviorVariableName.Int_RecentTargetThresholdPhases).IntVal;

        if (unit.LastTargetedPhaseNumberByAttackerGUID.ContainsKey(attackerUnit.GUID))
        {
            int phasesSinceTarget = unit.Combat.TurnDirector.TotalElapsedPhases - unit.LastTargetedPhaseNumberByAttackerGUID[attackerUnit.GUID];
            return (phasesSinceTarget <= recentTargetThreshold);
        }

        return false;
    }

    override protected BehaviorTreeResults Tick()
    {
        if (hasRecentAttacker())
        {
            tree.enemyUnits = tree.enemyUnits.FindAll(x => didUnitAttackRecently(x));
        }

        return new BehaviorTreeResults(tree.enemyUnits.Count > 0 ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}


class FindPreviouslySeenEnemiesNode : LeafBehaviorNode
{
    public FindPreviouslySeenEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits = new List<ICombatant>();
        foreach (AbstractActor actor in unit.team.VisibilityCache.previouslyDetectedEnemyLocations.Keys)
        {
            if (!actor.IsDead)
            {
                Vector3 seenLoc = unit.team.VisibilityCache.previouslyDetectedEnemyLocations[actor];
                if ((unit.Combat.MapMetaData.IsWithinBounds(seenLoc)) && (!tree.enemyUnits.Contains(actor)))
                {
                    tree.enemyUnits.Add(actor);
                }
            }
        }
        return new BehaviorTreeResults(tree.enemyUnits.Count > 0 ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}


class FindPreviouslySeenNonHackedEnemiesNode : LeafBehaviorNode
{
    public FindPreviouslySeenNonHackedEnemiesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.enemyUnits = new List<ICombatant>();
        foreach (AbstractActor actor in unit.team.VisibilityCache.previouslyDetectedEnemyLocations.Keys)
        {
            if ((!actor.IsDead) && (!tree.IsTargetIgnored(actor)))
            {
                Vector3 seenLoc = unit.team.VisibilityCache.previouslyDetectedEnemyLocations[actor];
                if (unit.Combat.MapMetaData.IsWithinBounds(seenLoc) && (!tree.enemyUnits.Contains(actor)))
                {
                    tree.enemyUnits.Add(actor);
                }
            }
        }
        return new BehaviorTreeResults(tree.enemyUnits.Count > 0 ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}



class ShootAtHighestPriorityEnemyNode : LeafBehaviorNode
{
    public ShootAtHighestPriorityEnemyNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return AttackEvaluator.MakeAttackOrder(unit, !unit.HasMovedThisRound);
    }
}

class ExecuteStationaryAttackNode : LeafBehaviorNode
{
    public ExecuteStationaryAttackNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorTreeResults r = AttackEvaluator.MakeAttackOrder(unit, true);

        if (r.nodeState == BehaviorNodeState.Failure)
        {
            LogAI(unit, "Failed to make stationary attack!!");
        }
        return r;
    }
}

class CloseToIdealRangeNode : LeafBehaviorNode
{
    public CloseToIdealRangeNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        const float RANGE_WINDOW = 0.2f; // fraction of ideal range +/- that is acceptable (e.g. 1.0 plus or minus 0.2)
        float idealRange = AppetitivePreferInIdealWeaponRangeHostileFactor.CalcIdealDistance(unit);
        if (idealRange < 1.0f)
        {
            // can never get this close
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant enemy = unit.BehaviorTree.enemyUnits[enemyIndex];
            if (enemy.IsDead)
            {
                continue;
            }
            float distanceToEnemy = (enemy.CurrentPosition - unit.CurrentPosition).magnitude;

            float fracOfIdealDistance = distanceToEnemy / idealRange;

            if ((fracOfIdealDistance < 1.0f + RANGE_WINDOW) &&
                (fracOfIdealDistance > 1.0f - RANGE_WINDOW))
            {
                return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
            }
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
    }
}


class CanMoveAndShootWithoutOverheatingNode : LeafBehaviorNode
{
    public CanMoveAndShootWithoutOverheatingNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        LogAI("Current heat: " + mechUnit.CurrentHeat);
        LogAI("Walk heat: " + mechUnit.WalkHeat);
        LogAI("Acceptable heat: " + AIUtil.GetAcceptableHeatLevelForMech(mechUnit));

        for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
        {
            Weapon weapon = mechUnit.Weapons[weaponIndex];
            float weaponHeat = weapon.HeatGenerated;

            LogAI(string.Format("weapon {0} firing heat: {1}", weapon.Name, weaponHeat));
            if (mechUnit.CurrentHeat + weaponHeat + mechUnit.WalkHeat < AIUtil.GetAcceptableHeatLevelForMech(mechUnit))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class CanMeleeHostileTargetsNode : LeafBehaviorNode
{
    public CanMeleeHostileTargetsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        bool hasWeaponsThatCanShoot = false;
        for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
        {
            Weapon w = unit.Weapons[weaponIndex];
            if (w.CanFire)
            {
                hasWeaponsThatCanShoot = true;
                break;
            }
        }

        // If I have no weapons that I can shoot, we'll melee, disregarding the weight ratio.
        bool useWeightRatio = hasWeaponsThatCanShoot;

        // loop over all hostiles, and see if there's any that I can melee
        for (int hostileIndex = 0; hostileIndex < unit.BehaviorTree.enemyUnits.Count; ++hostileIndex)
        {
            ICombatant hostile = unit.BehaviorTree.enemyUnits[hostileIndex];

            Mech hostileMech = hostile as Mech;
            if (hostileMech != null)
            {
                float targetExpDmg = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(hostileMech, unit, hostileMech.CurrentPosition, mechUnit.CurrentPosition, false, unit);
                float myExpDmg = AIUtil.ExpectedDamageForMeleeAttackUsingUnitsBVs(mechUnit, hostileMech, mechUnit.CurrentPosition, hostileMech.CurrentPosition, false, unit);
                if (myExpDmg <= 0)
                {
                    // don't expect to do any damage, bail
                    continue;
                }

                float ratio = targetExpDmg / myExpDmg;
                if ((useWeightRatio) && (ratio > unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MeleeDamageRatioCap).FloatVal))
                {
                    continue;
                }
            }

            if (unit.CanEngageTarget(hostile))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }

        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class CanDFAHostileTargetsNode : LeafBehaviorNode
{
    public CanDFAHostileTargetsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if ((mechUnit == null) || (mechUnit.WorkingJumpjets == 0))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        // loop over all hostiles, and see if there's any that I can DFA
        for (int hostileIndex = 0; hostileIndex < unit.BehaviorTree.enemyUnits.Count; ++hostileIndex)
        {
            ICombatant hostile = unit.BehaviorTree.enemyUnits[hostileIndex];

            if (unit.CanDFATargetFromPosition(hostile, unit.CurrentPosition))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class MoveTowardsHighestPriorityEnemyNode : LeafBehaviorNode
{
    public MoveTowardsHighestPriorityEnemyNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    float getMinWeaponRange(out bool foundWeapon)
    {
        float minWeaponRange = float.MaxValue;
        foundWeapon = false;

        for (int wi = 0; wi < unit.Weapons.Count; ++wi)
        {
            Weapon w = unit.Weapons[wi];
            if (!w.CanFire)
            {
                continue;
            }
            minWeaponRange = Mathf.Min(minWeaponRange, w.LongRange);
            foundWeapon = true;
        }
        return minWeaponRange;
    }

    override protected BehaviorTreeResults Tick()
    {
        // foreach enemy in the tree's list, try to move toward it

        Vector3 activeUnitPosition = unit.CurrentPosition;

        float minDistance = 20.0f; // never any closer than 20m
        foreach (Weapon weapon in unit.Weapons)
        {
            float minWeaponDist = weapon.MinRange;
            minDistance = Mathf.Max(minDistance, minWeaponDist);
        }

        // Step 1: find a location that I can walk to and then can shoot at an enemy, preferring ones with higher threat
        // levels.
        foreach (AbstractActor targetActor in tree.enemyUnits)
        {
            // TODO may need to consider magic knowledge.
            if (!AIUtil.UnitHasDetectionToTargetFromCurrentPosition(unit, targetActor))
            {
                // Our team can't detect this hostile.
                continue;
            }

            LogAI("Considering moving towards " + targetActor.Description.Name);
            Vector3 targetUnitPosition = targetActor.CurrentPosition;
            Vector3 displacementTowardsTarget = targetUnitPosition - activeUnitPosition;
            float originalLength = displacementTowardsTarget.magnitude;
            if (originalLength < minDistance)
            {
                LogAI("trying to attack, but too close");
                continue;
            }

            float newLength = originalLength - minDistance;
            displacementTowardsTarget = Vector3.ClampMagnitude(displacementTowardsTarget, newLength);

            Vector3 destination = activeUnitPosition + displacementTowardsTarget;
            destination = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, destination);

            destination = AIUtil.MaybeClipMovementDestinationToStayWithinLanceSpread(unit, destination);
            if ((destination - unit.CurrentPosition).magnitude < 1.0f)
            {
                continue;
            }

            if (true) // TODO(dlecompte): currently assuming we can move to any location - may need strategic pathfinding
            {
                LogAI("Found a path to my target");

                List<Weapon> weaponsThatCanShoot = new List<Weapon>();

                // now check to see if I can shoot from the end of that path to my target.
                for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
                {
                    Weapon weapon = unit.Weapons[weaponIndex];

                    if (!weapon.CanFire)
                    {
                        continue;
                    }

                    if (weapon.WillFireAtTargetFromPosition(targetActor, destination, Quaternion.LookRotation(targetActor.CurrentPosition - destination)))
                    {
                        // found at least one weapon that will shoot from here.
                        LogAI("Found a weapon that will shoot at my target");
                        weaponsThatCanShoot.Add(weapon);
                    }
                }

                if (weaponsThatCanShoot.Count > 0)
                {
                    bool foundWeapon = false;
                    float minIdealRange = getMinWeaponRange(out foundWeapon);
                    Debug.Assert(foundWeapon);

                    var isTargetGhosted = targetActor?.IsGhosted ?? false;

                    if (isTargetGhosted)
                    {
                        minIdealRange = Mathf.Min(minIdealRange, unit.MaxWalkDistance);
                    }


                    float movementBudget = unit.Pathing.MaxCost * 0.8f;
                    Vector3 lookAt = destination;
                    List<Vector3> pathToDestination = DynamicLongRangePathfinder.GetPathToDestination(destination, movementBudget, unit, false, minIdealRange);
                    if ((pathToDestination == null) || (pathToDestination.Count == 0))
                    {
                        continue;
                    }
                    Vector3 lastPointOnPath = pathToDestination[pathToDestination.Count - 1];
                    PathNodeGrid grid = unit.Pathing.CurrentGrid;

                    PathNode pathNode = unit.Pathing.UpdateAIPath(destination, lookAt, MoveType.Walking);
                    pathNode = AIUtil.GetPrunedClosestValidPathNode(unit, grid, unit.Combat, movementBudget, pathToDestination);

                    if (pathNode == null)
                    {
                        continue;
                    }
                    Vector3 dest = pathNode.Position;

                    Vector3 cur = unit.CurrentPosition;
                    AIUtil.LogAI(string.Format("issuing order from [{0} {1} {2}] to [{3} {4} {5}] looking at [{6} {7} {8}]",
                        cur.x, cur.y, cur.z,
                        dest.x, dest.y, dest.z,
                        lookAt.x, lookAt.y, lookAt.z
                    ));

                    BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
                    results.orderInfo = new MovementOrderInfo(dest, lookAt);
                    results.debugOrderString = string.Format("{0} moving toward target: {1} dest: {2}", this.name, targetActor.DisplayName, destination);
                    return results;
                }
                LogAI("But cannot find firing solution to my target");
            }
        }

        // Step 2: as above, without trying to find a firing solution.
        foreach (AbstractActor targetActor in tree.enemyUnits)
        {
            bool foundWeapon = false;
            float minIdealRange = getMinWeaponRange(out foundWeapon);
            if (!foundWeapon)
            {
                Mech mechUnit = unit as Mech;

                if ((mechUnit != null) && (unit.WorkingJumpjets > 0))
                {
                    minIdealRange = mechUnit.JumpDistance;
                }
                else
                {
                    minIdealRange = unit.Combat.HexGrid.HexWidth;
                }
            }

            var isTargetGhosted = targetActor?.IsGhosted ?? false;

            if (isTargetGhosted)
            {
                minIdealRange = Mathf.Min(minIdealRange, unit.MaxWalkDistance);
            }

            LogAI("Considering moving towards " + targetActor.Description.Name);
            Vector3 targetActorLocation = targetActor.CurrentPosition;
            Vector3 displacementTowardsTarget = targetActorLocation - activeUnitPosition;
            float originalLength = displacementTowardsTarget.magnitude;
            if (originalLength < minDistance)
            {
                LogAI("trying to attack, but too close");
                continue;
            }

            float newLength = originalLength - minDistance;
            displacementTowardsTarget = Vector3.ClampMagnitude(displacementTowardsTarget, newLength);

            Vector3 destination = activeUnitPosition + displacementTowardsTarget;
            destination = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, destination);
            destination = AIUtil.MaybeClipMovementDestinationToStayWithinLanceSpread(unit, destination);

            float movementBudget = unit.Pathing.MaxCost;
            List<Vector3> pathToDestination = DynamicLongRangePathfinder.GetPathToDestination(destination, movementBudget, unit, false, minIdealRange);
            if ((pathToDestination == null) || (pathToDestination.Count == 0))
            {
                continue;
            }

            unit.Pathing.UpdateAIPath(destination, targetActorLocation, MoveType.Walking);
            PathNodeGrid grid = unit.Pathing.CurrentGrid;

            PathNode pathNode = AIUtil.GetPrunedClosestValidPathNode(unit, grid, unit.Combat, movementBudget, pathToDestination);

            if (pathNode == null)
            {
                continue;
            }

            Vector3 dest = pathNode.Position;

            Vector3 cur = unit.CurrentPosition;
            AIUtil.LogAI(string.Format("issuing order from [{0} {1} {2}] to [{3} {4} {5}]",
                cur.x, cur.y, cur.z,
                dest.x, dest.y, dest.z
            ));

            LogAI("Found a path to my target");
            BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
            MovementOrderInfo mvtOrderInfo = new MovementOrderInfo(dest, targetActorLocation);
            mvtOrderInfo.IsSprinting = true;
            results.orderInfo = mvtOrderInfo;
            results.debugOrderString = string.Format("{0} (no firing solution) moving toward target: {1} dest: {2}", this.name, targetActor.DisplayName, dest);
            return results;
        }
        LogAI("Can not find anywhere to move.");

        // else, return failure
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class MoveTowardsHighestPriorityEnemyLastSeenLocationNode : LeafBehaviorNode
{
    public MoveTowardsHighestPriorityEnemyLastSeenLocationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        // foreach enemy in the tree's list, try to move toward its last seen location

        BehaviorTreeResults results;
        MovementOrderInfo mvtOrderInfo;

        for (int combatantIndex = 0; combatantIndex < tree.enemyUnits.Count; ++combatantIndex)
        {
            ICombatant targetCombatant = tree.enemyUnits[combatantIndex];
            LogAI("Considering moving towards " + targetCombatant.Description.Name);
            if (targetCombatant.IsDead)
            {
                continue;
            }

            Vector3 targetActorLocation;
            if (targetCombatant.UnitType == UnitType.Building)
            {
                targetActorLocation = targetCombatant.CurrentPosition;
            }
            else
            {
                AbstractActor targetActor = targetCombatant as AbstractActor;
                if ((targetActor == null) ||
                    (!unit.team.VisibilityCache.previouslyDetectedEnemyLocations.ContainsKey(targetActor)) ||
                    (!unit.Combat.MapMetaData.IsWithinBounds(unit.team.VisibilityCache.previouslyDetectedEnemyLocations[targetActor])))
                {
                    continue;
                }

                targetActorLocation = unit.team.VisibilityCache.previouslyDetectedEnemyLocations[targetActor];
            }

            targetActorLocation = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, targetActorLocation);

            if ((targetActorLocation - unit.CurrentPosition).magnitude < unit.Combat.Constants.MoveConstants.RotateInPlaceThreshold)
            {
                results = new BehaviorTreeResults(BehaviorNodeState.Success);
                mvtOrderInfo = new MovementOrderInfo(unit.CurrentPosition, targetActorLocation);
                mvtOrderInfo.IsSprinting = false;
                results.orderInfo = mvtOrderInfo;
                results.debugOrderString = string.Format("{0} turning toward target: {1} dest: {2}", this.name, targetCombatant.DisplayName, targetActorLocation);
                return results;
            }

            unit.Pathing.UpdateAIPath(targetActorLocation, targetActorLocation, MoveType.Walking);

            Vector3 successorPoint = targetActorLocation;
            float movementBudget = unit.Pathing.MaxCost;

            List<Vector3> pathToDestination = DynamicLongRangePathfinder.GetPathToDestination(targetActorLocation, movementBudget, unit, false, unit.SpotterDistanceAbsolute);
            if ((pathToDestination == null) || (pathToDestination.Count == 0))
            {
                continue;
            }

            PathNodeGrid grid = unit.Pathing.CurrentGrid;
            PathNode pathNode = AIUtil.GetPrunedClosestValidPathNode(unit, grid, unit.Combat, movementBudget, pathToDestination);
            if (pathNode == null)
            {
                continue;
            }

            Vector3 dest = pathNode.Position;
            Vector3 cur = unit.CurrentPosition;
            LogAI(string.Format("issuing order from [{0} {1} {2}] to [{3} {4} {5}] looking at [{6} {7} {8}]",
                cur.x, cur.y, cur.z,
                dest.x, dest.y, dest.z,
                successorPoint.x, successorPoint.y, successorPoint.z
            ));

            LogAI("Found a path to my target");
            results = new BehaviorTreeResults(BehaviorNodeState.Success);
            mvtOrderInfo = new MovementOrderInfo(dest, successorPoint);
            mvtOrderInfo.IsSprinting = true;
            results.orderInfo = mvtOrderInfo;
            results.debugOrderString = string.Format("{0} (no firing solution) moving toward target: {1} dest: {2}", this.name, targetCombatant.DisplayName, dest);
            return results;
        }
        LogAI("Can not find anywhere to move.");

        // else, return failure
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}


class ClearMoveCandidatesNode : LeafBehaviorNode
{
    public ClearMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        tree.movementCandidateLocations = new List<MoveDestination>();
        tree.influenceMapEvaluator.ResetWorkspace();
        tree.influenceMapEvaluator.InitializeEvaluationForUnit(unit);
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class GenerateForwardMoveCandidatesNode : LeafBehaviorNode
{
    public GenerateForwardMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Debug.Assert(tree.movementCandidateLocations != null);

        List<PathNode> nodes = unit.Pathing.getGrid(MoveType.Walking).GetSampledPathNodes();

        string stayInsideRegionGUID = RegionUtil.GetStayInsideRegionGUID(unit);

        for (int locationIndex = 0; locationIndex < nodes.Count; ++locationIndex)
        {
            if (stayInsideRegionGUID != null)
            {
                MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(nodes[locationIndex].Position);

                if (cell != null)
                {
                    MapEncounterLayerDataCell encounterDataCell = cell.MapEncounterLayerDataCell;
                    if ((encounterDataCell != null) &&
                        (encounterDataCell.regionGuidList != null) &&
                        (!encounterDataCell.regionGuidList.Contains(stayInsideRegionGUID)))
                    {
                        // this location is outside our region, disregard.
                        continue;
                    }
                }
            }
            tree.movementCandidateLocations.Add(new MoveDestination(nodes[locationIndex], MoveType.Walking));
        }
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class GenerateReverseMoveCandidatesNode : LeafBehaviorNode
{
    public GenerateReverseMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Debug.Assert(tree.movementCandidateLocations != null);
        List<PathNode> nodes = unit.Pathing.getGrid(MoveType.Backward).GetSampledPathNodes();

        string stayInsideRegionGUID = RegionUtil.GetStayInsideRegionGUID(unit);

        for (int locationIndex = 0; locationIndex < nodes.Count; ++locationIndex)
        {
            if (AIUtil.Get2DDistanceBetweenVector3s(nodes[locationIndex].Position, unit.CurrentPosition) < 1.0f)
            {
                continue;
            }

            if (stayInsideRegionGUID != null)
            {
                MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(nodes[locationIndex].Position);

                if (cell != null)
                {
                    MapEncounterLayerDataCell encounterDataCell = cell.MapEncounterLayerDataCell;
                    if ((encounterDataCell != null) &&
                        (encounterDataCell.regionGuidList != null) &&
                        (!encounterDataCell.regionGuidList.Contains(stayInsideRegionGUID)))
                    {
                        // this location is outside our region, disregard.
                        continue;
                    }
                }
            }
            tree.movementCandidateLocations.Add(new MoveDestination(nodes[locationIndex], MoveType.Backward));
        }
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class GenerateMeleeMoveCandidatesNode : LeafBehaviorNode
{
    public GenerateMeleeMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Debug.Assert(tree.movementCandidateLocations != null);

        Mech mech = unit as Mech;
        if (mech == null)
        {
            // no moves to generate, so trivially successful
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        string stayInsideRegionGUID = RegionUtil.GetStayInsideRegionGUID(unit);

        for (int targetIndex = 0; targetIndex < unit.BehaviorTree.enemyUnits.Count; ++targetIndex)
        {
            ICombatant target = unit.BehaviorTree.enemyUnits[targetIndex];

            if (!mech.CanEngageTarget(target))
            {
                continue;
            }

            AbstractActor targetActor = target as AbstractActor;

            if (targetActor == null)
            {
                continue;
            }

            List<PathNode> meleeDestNodes = mech.Pathing.GetMeleeDestsForTarget(targetActor);

            for (int i = 0; i < meleeDestNodes.Count; ++i)
            {
                PathNode node = meleeDestNodes[i];
                if (stayInsideRegionGUID != null)
                {
                    MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(node.Position);

                    if (cell != null)
                    {
                        MapEncounterLayerDataCell encounterDataCell = cell.MapEncounterLayerDataCell;
                        if ((encounterDataCell != null) &&
                            (encounterDataCell.regionGuidList != null) &&
                            (!encounterDataCell.regionGuidList.Contains(stayInsideRegionGUID)))
                        {
                            // this location is outside our region, disregard.
                            continue;
                        }
                    }
                }
                tree.movementCandidateLocations.Add(new MeleeMoveDestination(node, targetActor));
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class GenerateJumpMoveCandidatesNode : LeafBehaviorNode
{
    public GenerateJumpMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Debug.Assert(tree.movementCandidateLocations != null);
        Mech mechUnit = unit as Mech;
        if ((mechUnit != null) && (mechUnit.WorkingJumpjets > 0))
        {
            string stayInsideRegionGUID = RegionUtil.GetStayInsideRegionGUID(unit);

            List<PathNode> nodes = unit.JumpPathing.GetSampledPathNodes();
            for (int locationIndex = 0; locationIndex < nodes.Count; ++locationIndex)
            {
                if (AIUtil.Get2DDistanceBetweenVector3s(nodes[locationIndex].Position, unit.CurrentPosition) < 1.0f)
                {
                    continue;
                }

                float moveHeat = mechUnit.CalcJumpHeat((nodes[locationIndex].Position - unit.CurrentPosition).magnitude);
                if (moveHeat + mechUnit.CurrentHeat > AIUtil.GetAcceptableHeatLevelForMech(mechUnit))
                {
                    continue;
                }

                if (stayInsideRegionGUID != null)
                {
                    MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(nodes[locationIndex].Position);

                    if (cell != null)
                    {
                        MapEncounterLayerDataCell encounterDataCell = cell.MapEncounterLayerDataCell;
                        if ((encounterDataCell != null) &&
                            (encounterDataCell.regionGuidList != null) &&
                            (!encounterDataCell.regionGuidList.Contains(stayInsideRegionGUID)))
                        {
                            // this location is outside our region, disregard.
                            continue;
                        }
                    }
                }
                tree.movementCandidateLocations.Add(new MoveDestination(nodes[locationIndex], MoveType.Jumping));
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class GenerateStationaryMoveCandidatesNode : LeafBehaviorNode
{
    public GenerateStationaryMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Debug.Assert(tree.movementCandidateLocations != null);
        PathNodeGrid grid = unit.Pathing.getGrid(MoveType.Walking);
        Point point = grid.Start;
        float floatAngle = unit.CurrentRotation.eulerAngles.y;
        PathNode node = new PathNode(null, point.X, point.Z, unit.CurrentPosition, PathingUtil.FloatAngleTo8Angle(floatAngle), null, null, unit);
        MoveDestination dest = new MoveDestination(node, MoveType.None);
        tree.movementCandidateLocations.Add(dest);
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class GenerateSprintMoveCandidatesNode : LeafBehaviorNode
{
    public GenerateSprintMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Debug.Assert(tree.movementCandidateLocations != null);

        MoveType mt = unit.CanSprint ? MoveType.Sprinting : MoveType.Walking;

        List<PathNode> nodes = unit.Pathing.getGrid(mt).GetSampledPathNodes();
        string stayInsideRegionGUID = RegionUtil.GetStayInsideRegionGUID(unit);
        bool allowSprintToRegularLocations = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_SprintToRegularLocations).BoolVal;

        for (int locationIndex = 0; locationIndex < nodes.Count; ++locationIndex)
        {
            PathNode node = nodes[locationIndex];

            if ((!allowSprintToRegularLocations) && nodeHasRegularMove(node))
            {
                continue;
            }
            if (stayInsideRegionGUID != null)
            {
                MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(nodes[locationIndex].Position);

                if (cell != null)
                {
                    MapEncounterLayerDataCell encounterDataCell = cell.MapEncounterLayerDataCell;
                    if ((encounterDataCell != null) &&
                        (encounterDataCell.regionGuidList != null) &&
                        (!encounterDataCell.regionGuidList.Contains(stayInsideRegionGUID)))
                    {
                        // this location is outside our region, disregard.
                        continue;
                    }
                }
            }

            tree.movementCandidateLocations.Add(new MoveDestination(nodes[locationIndex], mt));
        }
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }

    bool nodeHasRegularMove(PathNode node)
    {
        Vector2 nodeAxial = unit.Combat.HexGrid.HexAxialRound(unit.Combat.HexGrid.CartesianToHexAxial(node.Position));

        for (int i = 0; i < tree.movementCandidateLocations.Count; ++i)
        {
            MoveDestination dest = tree.movementCandidateLocations[i];
            Vector2 destAxial = unit.Combat.HexGrid.HexAxialRound(unit.Combat.HexGrid.CartesianToHexAxial(dest.PathNode.Position));
            if (destAxial == nodeAxial)
            {
                // go through destination and see if it has normal moves
                if ((dest.MoveType == MoveType.Walking) ||
                    (dest.MoveType == MoveType.Backward) ||
                    (dest.MoveType == MoveType.Jumping))
                {
                    return true;
                }
            }
        }
        return false;
    }
}

class GenerateMoveCandidatesNode : LeafBehaviorNode
{
    public GenerateMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        MoveType[] moveTypes = { MoveType.Walking, MoveType.Backward, MoveType.Jumping, MoveType.Sprinting };

        for (int moveTypeIndex = 0; moveTypeIndex < moveTypes.Length; ++moveTypeIndex)
        {
            MoveType moveType = moveTypes[moveTypeIndex];

            List<PathNode> nodes;
            if (moveType == MoveType.Jumping)
            {
                Mech mechUnit = unit as Mech;
                if ((mechUnit == null) ||
                    (mechUnit.WorkingJumpjets == 0))
                {
                    // Don't jump if you can't jump
                    continue;
                }

                nodes = new List<PathNode>();
                List<PathNode> jumpNodes = mechUnit.JumpPathing.GetSampledPathNodes();
                for (int pathNodeIndex = 0; pathNodeIndex < jumpNodes.Count; ++pathNodeIndex)
                {
                    PathNode node = jumpNodes[pathNodeIndex];

                    float moveHeat = mechUnit.CalcJumpHeat((node.Position - unit.CurrentPosition).magnitude);
                    if (moveHeat + mechUnit.CurrentHeat > AIUtil.GetAcceptableHeatLevelForMech(mechUnit))
                    {
                        continue;
                    }
                    nodes.Add(node);
                }
            }
            else
            {
                nodes = unit.Pathing.getGrid(moveType).GetSampledPathNodes();
            }
            for (int nodeIndex = 0; nodeIndex < nodes.Count; ++nodeIndex)
            {
                PathNode node = nodes[nodeIndex];
                if (node != null)
                {
                    tree.movementCandidateLocations.Add(new MoveDestination(node, moveType));
                }
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

abstract class GenerateMoveTowardDistantHostileCandidatesNode : LeafBehaviorNode
{
    abstract protected bool IsSprinting();

    public GenerateMoveTowardDistantHostileCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    protected virtual float getMinWeaponRange(out bool foundWeapon)
    {
        float minWeaponRange = float.MaxValue;
        foundWeapon = false;

        for (int wi = 0; wi < unit.Weapons.Count; ++wi)
        {
            Weapon w = unit.Weapons[wi];
            if (!w.CanFire)
            {
                continue;
            }
            minWeaponRange = Mathf.Min(minWeaponRange, w.LongRange);
            foundWeapon = true;
        }
        return minWeaponRange;
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.influenceMapEvaluator.InitializeEvaluationForUnit(unit);

        LogAI(string.Format("in {0}, enemyUnitsCount: {1}", this.name, tree.enemyUnits.Count));

        float bestDeviation = float.MaxValue;
        PathNode bestDestinationNode = null;

        for (int hostileIndex = 0; hostileIndex < tree.enemyUnits.Count; ++hostileIndex)
        {
            ICombatant hostile = tree.enemyUnits[hostileIndex];
            Vector3 targetLocation = hostile.CurrentPosition;
            AbstractActor targetActor = hostile as AbstractActor;

            targetLocation = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, targetLocation);

            if ((targetLocation - unit.CurrentPosition).magnitude < unit.Combat.HexGrid.HexWidth)
            {
                continue;
            }

            if (IsSprinting())
            {
                unit.Pathing.SetSprinting();
            }
            else
            {
                unit.Pathing.SetWalking();
            }

            float maxMoveDistance = IsSprinting() ? unit.MaxSprintDistance : unit.MaxWalkDistance;
            bool foundWeapon = false;
            float weaponRange = getMinWeaponRange(out foundWeapon);
            if (!foundWeapon)
            {
                weaponRange = 24.0f;
            }

            var isTargetGhosted = targetActor?.IsGhosted ?? false;

            if (isTargetGhosted)
            {
                weaponRange = Mathf.Min(weaponRange, unit.MaxWalkDistance);
            }

            List<Vector3> pathToDestination = DynamicLongRangePathfinder.GetPathToDestination(targetLocation, maxMoveDistance, unit, IsSprinting(), weaponRange);
            if ((pathToDestination == null) || (pathToDestination.Count == 0))
            {
                continue;
            }

            PathNode destinationNode = AIUtil.GetPrunedClosestValidPathNode(unit, unit.Pathing.CurrentGrid, unit.Combat, maxMoveDistance, pathToDestination);

            if (destinationNode != null)
            {
                LogAI(string.Format("dest non-null {0}", destinationNode.Position));
                LogAI(string.Format("generating order to {3} to target location: {0} destNode: {1} dist {2}", targetLocation, destinationNode, (targetLocation - unit.CurrentPosition).magnitude, IsSprinting() ? "sprint" : "walk"));

                float deviation = (destinationNode.Position - targetLocation).magnitude;
                if (deviation < bestDeviation)
                {
                    LogAI(string.Format("found destination with deviation {0}", deviation));
                    bestDestinationNode = destinationNode;
                    bestDeviation = deviation;

                    PathNode nodeWalker = destinationNode;
                    while (true)
                    {
                        if (nodeWalker == null)
                        {
                            break;
                        }
                        LogAI(string.Format("node: pos {0} cost {1} depth {2}", nodeWalker.Position, nodeWalker.CostToThisNode, nodeWalker.DepthInPath));
                        nodeWalker = nodeWalker.Parent;
                    }
                }
            }
        }

        if (bestDestinationNode != null)
        {
            MoveType mt = IsSprinting() ? MoveType.Sprinting : MoveType.Walking;
            tree.movementCandidateLocations.Add(new MoveDestination(bestDestinationNode, mt));
            tree.influenceMapEvaluator.WorkspacePushPathNodeAngle(bestDestinationNode, PathingUtil.GetAngle(bestDestinationNode.Position - unit.CurrentPosition), mt, null);
        }

        return new BehaviorTreeResults(tree.movementCandidateLocations.Count > 0 ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}


class GenerateSprintMoveTowardDistantHostileCandidatesNode : GenerateMoveTowardDistantHostileCandidatesNode
{
    public GenerateSprintMoveTowardDistantHostileCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected bool IsSprinting()
    {
        return unit.CanSprint;
    }
}

class GenerateNormalMoveTowardDistantHostileCandidatesNode : GenerateMoveTowardDistantHostileCandidatesNode
{
    public GenerateNormalMoveTowardDistantHostileCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    protected override bool IsSprinting()
    {
        return false;
    }
}

class GenerateForcedNormalMoveTowardDistantHostileCandidatesNode : GenerateMoveTowardDistantHostileCandidatesNode
{
    public GenerateForcedNormalMoveTowardDistantHostileCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    protected override bool IsSprinting()
    {
        return false;
    }

    protected override float getMinWeaponRange(out bool foundWeapon)
    {
        foundWeapon = false;

        // TODO (Allie): Maybe we should not do this loop at all and just return 0f and foundWeapon true?
        // Then again, we may actually want the parent class's behavior if no weapons are fireable..
        for (int wi = 0; wi < unit.Weapons.Count; ++wi)
        {
            Weapon w = unit.Weapons[wi];
            if (w.CanFire)
            {
                foundWeapon = true;
                break;
            }
        }
        return 0f;
    }
}

class GenerateSprintMoveTowardTeammatesCandidatesNode : LeafBehaviorNode
{
    public GenerateSprintMoveTowardTeammatesCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.influenceMapEvaluator.InitializeEvaluationForUnit(unit);

        Vector3 accum = new Vector3();
        int teamCount = 0;

        for (int i = 0; i < unit.team.units.Count; ++i)
        {
            AbstractActor otherUnit = unit.team.units[i];
            if (otherUnit.IsDead)
            {
                continue;
            }
            accum += otherUnit.CurrentPosition;
            teamCount++;
        }

        Vector3 centerPos = accum * 1.0f / teamCount;

        Vector3 targetLocation = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, centerPos);

        MoveType mt;
        if (unit.CanSprint)
        {
            unit.Pathing.SetSprinting();
            mt = MoveType.Sprinting;
        }
        else
        {
            unit.Pathing.SetWalking();
            mt = MoveType.Sprinting;
        }

        float movementCost = Mathf.Max(unit.MaxSprintDistance, unit.MaxWalkDistance);

        float teamMateProximity = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PersonalSpaceRadius).FloatVal;

        List<Vector3> pathToDestination = DynamicLongRangePathfinder.GetPathToDestination(targetLocation, movementCost, unit, unit.CanSprint, teamMateProximity);
        if ((pathToDestination == null) || (pathToDestination.Count == 0))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        PathNode destinationNode = AIUtil.GetPrunedClosestValidPathNode(unit, unit.Pathing.CurrentGrid, unit.Combat, movementCost, pathToDestination);
        if (destinationNode != null)
        {
            float angle = PathingUtil.GetAngle(targetLocation - destinationNode.Position);
            tree.movementCandidateLocations.Add(new MoveDestination(destinationNode, mt));
            tree.influenceMapEvaluator.WorkspacePushPathNodeAngle(destinationNode, angle, mt, null);
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class GenerateNormalMoveTowardTeammatesCandidatesNode : LeafBehaviorNode
{
    public GenerateNormalMoveTowardTeammatesCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.influenceMapEvaluator.InitializeEvaluationForUnit(unit);

        Vector3 accum = new Vector3();
        int teamCount = 0;

        for (int i = 0; i < unit.team.units.Count; ++i)
        {
            AbstractActor otherUnit = unit.team.units[i];
            if (otherUnit.IsDead)
            {
                continue;
            }
            accum += otherUnit.CurrentPosition;
            teamCount++;
        }

        Vector3 centerPos = accum * 1.0f / teamCount;

        Vector3 targetLocation = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, centerPos);

        unit.Pathing.SetWalking();
        float movementCost = unit.MaxWalkDistance;

        float teamMateProximity = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PersonalSpaceRadius).FloatVal;

        List<Vector3> pathToDestination = DynamicLongRangePathfinder.GetPathToDestination(targetLocation, movementCost, unit, false, teamMateProximity);
        if ((pathToDestination == null) || (pathToDestination.Count == 0))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        PathNode destinationNode = AIUtil.GetPrunedClosestValidPathNode(unit, unit.Pathing.CurrentGrid, unit.Combat, movementCost, pathToDestination);
        if (destinationNode != null)
        {
            float angle = PathingUtil.GetAngle(targetLocation - destinationNode.Position);
            tree.movementCandidateLocations.Add(new MoveDestination(destinationNode, MoveType.Walking));
            tree.influenceMapEvaluator.WorkspacePushPathNodeAngle(destinationNode, angle, MoveType.Walking, null);
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class AreAnyHostilesInWeaponRangeNode : LeafBehaviorNode
{
    public AreAnyHostilesInWeaponRangeNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        List<AbstractActor> allies = unit.Combat.GetAllAlliesOf(unit);

        for (int hostileIndex = 0; hostileIndex < tree.enemyUnits.Count; ++hostileIndex)
        {
            ICombatant hostile = tree.enemyUnits[hostileIndex];
            AbstractActor hostileActor = hostile as AbstractActor;

            float distanceToHostile = (hostile.CurrentPosition - unit.CurrentPosition).magnitude;

            if (!AIUtil.UnitHasVisibilityToTargetFromPosition(unit, hostile, unit.CurrentPosition, allies))
            {
                // Our team can't see this hostile
                continue;
            }

            if ((unit.CanEngageTarget(hostile)) || (unit.CanDFATargetFromPosition(hostile, unit.CurrentPosition)))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }

            if (distanceToHostile <= unit.MaxWalkDistance)
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }

            if ((hostileActor?.IsGhosted ?? false))
            {
                var parentECM = hostileActor.ParentECMCarrier;

                var lerpValue = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SignalInWeapRngWhenEnemyGhostedWithinMoveDistance).FloatVal;
                var movementDistance = Mathf.Lerp(unit.MaxWalkDistance, unit.MaxSprintDistance, lerpValue);

                var range = parentECM.AuraComponents[0].componentDef.statusEffects[0].targetingData.range;
                var distance = Vector3.Distance(unit.CurrentPosition, parentECM.CurrentPosition) - range;

                if (distance >= movementDistance)
                {
                    continue;
                }
            }
            

            for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
            {
                Weapon weapon = unit.Weapons[weaponIndex];

                if (!weapon.CanFire)
                {
                    continue;
                }

                if (weapon.MaxRange >= distanceToHostile)
                {
                    return new BehaviorTreeResults(BehaviorNodeState.Success);
                }
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class AreAnyDetectedHostilesInWeaponRangePlusSprintDistanceNode : LeafBehaviorNode
{
    public AreAnyDetectedHostilesInWeaponRangePlusSprintDistanceNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        float sprintDistance = Mathf.Max(unit.MaxSprintDistance, unit.MaxWalkDistance);

        // initialize with the distance we could walk and melee
        float weaponDistance = unit.MaxWalkDistance;

        for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
        {
            Weapon weapon = unit.Weapons[weaponIndex];

            if (!weapon.CanFire)
            {
                continue;
            }

            weaponDistance = Mathf.Max(weapon.MaxRange, weaponDistance);
        }

        for (int hostileIndex = 0; hostileIndex < tree.enemyUnits.Count; ++hostileIndex)
        {
            ICombatant hostile = tree.enemyUnits[hostileIndex];
            if (hostile.IsDead)
            {
                continue;
            }

            if (unit.team.VisibilityToTarget(hostile) == VisibilityLevel.None)
            {
                continue;
            }

            float distanceToHostile = (hostile.CurrentPosition - unit.CurrentPosition).magnitude;

            if (distanceToHostile < (weaponDistance + sprintDistance))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class AreAnyHostilesInDetectionRangeOfThisUnitNode : LeafBehaviorNode
{
    public AreAnyHostilesInDetectionRangeOfThisUnitNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        for (int hostileIndex = 0; hostileIndex < tree.enemyUnits.Count; ++hostileIndex)
        {
            ICombatant hostile = tree.enemyUnits[hostileIndex];

            if (hostile.IsDead)
            {
                continue;
            }

            float distanceToHostile = (hostile.CurrentPosition - unit.CurrentPosition).magnitude;
            float detectionRangeForHostile = unit.Combat.LOS.GetAdjustedSensorRange(unit, hostile as AbstractActor);
            if (distanceToHostile < detectionRangeForHostile)
            {
                return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
            }
        }
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
    }
}

/// <summary>
/// are there any (non-melee, non-DFA) weapons that the unit can fire?
/// </summary>
class HasRangedWeaponsNode : LeafBehaviorNode
{
    public HasRangedWeaponsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
        {
            Weapon w = unit.Weapons[weaponIndex];
            if (w.CanFire)
            {
                return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
            }
        }
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
    }
}

class DoAnyMovesYieldLOFToAnyHostileNode : LeafBehaviorNode
{
    public DoAnyMovesYieldLOFToAnyHostileNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        float walkDistance = unit.MovementCaps.MaxWalkDistance;
        float weaponRange = AIUtil.GetMaxWeaponRange(unit);
        float weaponPlusMovementDistance = walkDistance + weaponRange;

        int hexSteps = Mathf.CeilToInt(weaponPlusMovementDistance / unit.Combat.HexGrid.HexWidth);

        if (hexSteps == 0)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        List<Vector3> reachablePoints = unit.Combat.HexGrid.GetGridPointsAroundPointWithinRadius(unit.CurrentPosition, hexSteps);

        List<bool> hasIndirectFire = new List<bool>();
        List<float> maxDirectRange = new List<float>();
        List<float> maxIndirectRange = new List<float>();

        for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
        {
            hasIndirectFire.Add(false);
            maxDirectRange.Add(0);
            maxIndirectRange.Add(0);

            ICombatant enemyCombatant = unit.BehaviorTree.enemyUnits[i];
            AbstractActor enemyUnit = enemyCombatant as AbstractActor;
            if ((enemyUnit == null) || (enemyUnit.IsDead))
            {
                continue;
            }

            for (int weaponIndex = 0; weaponIndex < enemyUnit.Weapons.Count; ++weaponIndex)
            {
                Weapon w = enemyUnit.Weapons[weaponIndex];
                if (w.CanFire)
                {
                    if (w.IndirectFireCapable)
                    {
                        hasIndirectFire[i] = true;
                        maxIndirectRange[i] = Mathf.Max(maxIndirectRange[i], w.MaxRange);
                    }
                    else
                    {
                        maxDirectRange[i] = Mathf.Max(maxDirectRange[i], w.MaxRange);
                    }
                }
            }
        }

        for (int vi = 0; vi < reachablePoints.Count; ++vi)
        {
            Vector3 reachablePos = reachablePoints[vi];
            for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
            {
                ICombatant enemyUnit = unit.BehaviorTree.enemyUnits[enemyIndex];
                Vector3 collisionWorldPos = Vector3.zero;
                LineOfFireLevel lofLevel = unit.Combat.LOFCache.GetLineOfFire(unit, reachablePos, enemyUnit, enemyUnit.CurrentPosition, Quaternion.LookRotation(reachablePos - enemyUnit.CurrentPosition), out collisionWorldPos);
                float range = (reachablePos - enemyUnit.CurrentPosition).magnitude;
                if (((lofLevel != LineOfFireLevel.LOFBlocked) && (range < maxDirectRange[enemyIndex])) ||
                    ((hasIndirectFire[enemyIndex]) && (range < maxIndirectRange[enemyIndex])))
                {
                    return new BehaviorTreeResults(BehaviorNodeState.Success);
                }
            }
        }

        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}


class SortMoveCandidatesByDecreasingDistanceToHostilesNode : LeafBehaviorNode
{
    Vector3 center;

    public SortMoveCandidatesByDecreasingDistanceToHostilesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    private int sortByEnemyProximity(MoveDestination a, MoveDestination b)
    {
        Vector3 aLoc = a.PathNode.Position;
        Vector3 bLoc = b.PathNode.Position;
        float aDist = (aLoc - center).sqrMagnitude;
        float bDist = (bLoc - center).sqrMagnitude;

        float delta = bDist - aDist;
        if (delta < 0)
        {
            return -1;
        }
        else if (delta > 0)
        {
            return 1;
        }
        return 0;
    }

    override protected BehaviorTreeResults Tick()
    {
        // at the end of this sort, the 0th position is the furthest away from the center of the enemies.

        center = Vector3.zero;
        for (int enemyIndex = 0; enemyIndex < tree.enemyUnits.Count; ++enemyIndex)
        {
            center += tree.enemyUnits[enemyIndex].CurrentPosition;
        }
        center /= (float)tree.enemyUnits.Count;

        tree.movementCandidateLocations.Sort(sortByEnemyProximity);
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class FilterMoveCandidatesByLowestLOSToHostilesNode : LeafBehaviorNode
{
    public FilterMoveCandidatesByLowestLOSToHostilesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    int countLOSToLoc(Vector3 loc)
    {
        int count = 0;

        for (int enemyIndex = 0; enemyIndex < tree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant target = tree.enemyUnits[enemyIndex];
            AbstractActor targetActor = target as AbstractActor;
            BattleTech.Building targetBuilding = target as BattleTech.Building;

            float distance = (target.CurrentPosition - loc).magnitude * 1.5f;
            Vector3 offset = Vector3.zero;

            if (targetActor != null)
            {
                offset = targetActor.HighestLOSPosition;
            }
            else if (targetBuilding != null)
            {
                // TODO - need to figure out LOS to buildings
            }

            if (target.Combat.LOS.HasLineOfSight(target.CurrentPosition + offset, loc + offset, distance, targetGuid: string.Empty))
            {
                ++count;
            }
        }
        return count;
    }

    override protected BehaviorTreeResults Tick()
    {
        if (tree.movementCandidateLocations.Count == 0)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        int[] losCounts = new int[tree.movementCandidateLocations.Count];

        for (int locIndex = 0; locIndex < tree.movementCandidateLocations.Count; ++locIndex)
        {
            Vector3 loc = tree.movementCandidateLocations[locIndex].PathNode.Position;
            losCounts[locIndex] = countLOSToLoc(loc);
        }

        // find the best one
        int bestLOSCount = losCounts[0];
        for (int locIndex = 1; locIndex < tree.movementCandidateLocations.Count; ++locIndex)
        {
            if (losCounts[locIndex] < bestLOSCount)
            {
                bestLOSCount = losCounts[locIndex];
            }
        }

        // remove any that aren't best
        for (int locIndex = tree.movementCandidateLocations.Count - 1; locIndex >= 0; --locIndex)
        {
            if (losCounts[locIndex] > bestLOSCount)
            {
                tree.movementCandidateLocations.RemoveAt(locIndex);
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class MoveTowardsHighestPriorityMoveCandidateNode : LeafBehaviorNode
{
    bool useSprintJuice;

    public MoveTowardsHighestPriorityMoveCandidateNode(string name, BehaviorTree tree, AbstractActor unit, bool useSprintJuice) : base(name, tree, unit)
    {
        this.useSprintJuice = useSprintJuice;
    }

    override protected BehaviorTreeResults Tick()
    {
        if (tree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count == 0)
        {
            LogAI("No evaluated move candidates");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        WorkspaceEvaluationEntry bestEvaluatedEntry = tree.influenceMapEvaluator.WorkspaceEvaluationEntries[0];
        MoveType bestMoveType = bestEvaluatedEntry.GetBestMoveType();
        Vector3 destinationPosition = bestEvaluatedEntry.Position;

        BehaviorVariableValue stayInsideRegionVariableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_StayInsideRegionGUID);
        if ((stayInsideRegionVariableValue != null) || (stayInsideRegionVariableValue.StringVal.Length == 0))
        {
            destinationPosition = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, destinationPosition);
        }

        float rotationDegrees = bestEvaluatedEntry.Angle;
        Quaternion rotationQuaternion = Quaternion.Euler(0, rotationDegrees, 0);
        Vector3 rotatedForward = rotationQuaternion * Vector3.forward;

        BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);

        Vector3 lookAt = destinationPosition + rotatedForward;

        OrderInfo orderInfo;
        if (bestMoveType == MoveType.None)
        {
            orderInfo = new OrderInfo(OrderType.Brace);
        }
        else
        {
            MovementOrderInfo moveOrderInfo = new MovementOrderInfo(destinationPosition, lookAt);
            orderInfo = moveOrderInfo;
            moveOrderInfo.IsReverse = bestMoveType == MoveType.Backward;
            moveOrderInfo.IsJumping = bestMoveType == MoveType.Jumping;
            moveOrderInfo.IsSprinting = bestMoveType == MoveType.Sprinting;
            moveOrderInfo.IsMelee = bestMoveType == MoveType.Melee;

            if (bestMoveType == MoveType.Melee)
            {
                Mech mech = unit as Mech;

                // TODO - Verify melee attacks don't double-dip
                AttackOrderInfo attackOrder = new AttackOrderInfo(bestEvaluatedEntry.Target, true, false);
                orderInfo = attackOrder;
                attackOrder.AddWeapon(mech.MeleeWeapon);
                for (int wi = 0; wi < mech.Weapons.Count; ++wi)
                {
                    Weapon w = mech.Weapons[wi];
                    if (w.CanFire && (w.WeaponCategoryValue.CanUseInMelee))
                    {
                        attackOrder.AddWeapon(w);
                    }
                }
                attackOrder.AttackFromLocation = bestEvaluatedEntry.Position;
            }
        }

        results.orderInfo = orderInfo;
        string moveVerb = "undefined movement";

        bool sprintJuiceIncreasing = false;
        switch (bestMoveType)
        {
            case MoveType.None:
                moveVerb = "bracing";
                sprintJuiceIncreasing = true;
                break;
            case MoveType.Walking:
                moveVerb = "walking";
                sprintJuiceIncreasing = true;
                break;
            case MoveType.Backward:
                moveVerb = "reversing";
                sprintJuiceIncreasing = true;
                break;
            case MoveType.Sprinting:
                moveVerb = "sprinting";
                sprintJuiceIncreasing = false;
                break;
            case MoveType.Jumping:
                moveVerb = "jumping";
                sprintJuiceIncreasing = true;
                break;
            case MoveType.Melee:
                moveVerb = "engaging";
                sprintJuiceIncreasing = true;
                break;
            default:
                moveVerb = "moving in an unknown way";
                break;
        }

        if (tree.HasProximityTaggedTargets() && tree.IsOutsideProximityTargetDistance())
        {
            useSprintJuice = false;
        }

        if (useSprintJuice)
        {
            if (sprintJuiceIncreasing)
            {
                unit.BehaviorTree.IncreaseSprintHysteresisLevel();
            }
            else
            {
                unit.BehaviorTree.DecreaseSprintHysteresisLevel();
            }
        }

        float moveDist = (bestEvaluatedEntry.Position - unit.CurrentPosition).magnitude;
        results.debugOrderString = string.Format("{0} {1} toward dest: {2} from {3} dist {4}", this.name, moveVerb, bestEvaluatedEntry.Position.ToString(), unit.CurrentPosition.ToString(), moveDist);
        LogAI("movement order: " + results.debugOrderString);
        LogAI("move verb " + moveVerb);
        LogAI("distance: " + (this.unit.CurrentPosition - bestEvaluatedEntry.Position).magnitude);
        return results;
    }
}


class MeleeWithHighestPriorityEnemyNode : LeafBehaviorNode
{
    public MeleeWithHighestPriorityEnemyNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    List<ICombatant> getAllMeleeTargetsForMech(Mech mech)
    {
        List<ICombatant> allTargets = mech.Combat.GetAllMiscCombatants();
        List<AbstractActor> allActors = mech.Combat.AllActors;
        for (int actorIndex = 0; actorIndex < allActors.Count; ++actorIndex)
        {
            AbstractActor actor = allActors[actorIndex];
            if ((!actor.IsDead) && (mech.CanEngageTarget(actor)))
            {
                allTargets.Add(actor);
            }
        }
        return allTargets;
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mech = unit as Mech;

        if ((mech == null) || (tree.enemyUnits.Count == 0))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        List<ICombatant> meleeTargets = getAllMeleeTargetsForMech(mech);

        for (int enemyUnitIndex = 0; enemyUnitIndex < tree.enemyUnits.Count; ++enemyUnitIndex)
        {
            ICombatant target = tree.enemyUnits[enemyUnitIndex];

            if (!meleeTargets.Contains(target))
            {
                continue;
            }

            // TODO - Make sure melee attacks don't double-dip
            BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
            AttackOrderInfo attackOrderInfo = new AttackOrderInfo(target);
            results.orderInfo = attackOrderInfo;
            foreach (Weapon weapon in mech.Weapons)
            {
                if (weapon.WeaponCategoryValue.CanUseInMelee)
                {
                    attackOrderInfo.AddWeapon(weapon);
                }
            }
            attackOrderInfo.AddWeapon(mech.MeleeWeapon);
            results.debugOrderString = this.name + " melee attack at: " + target.DisplayName;
            return results;
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class BraceNode : LeafBehaviorNode
{
    public BraceNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
        /*
        Debug.LogError("Executing brace command from a BraceNode: " + this.name);

        bool canShoot = (unit.IsOperational) && (!unit.HasFiredThisRound);
        Debug.LogError(string.Format("canMove: {0} canShoot: {1}", unit.CanMove, canShoot));

        if (canShoot)
        {
            for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
            {
                ICombatant enemyUnit = unit.BehaviorTree.enemyUnits[i];

                bool hasLOF = false;

                foreach (Weapon w in unit.Weapons)
                {
                    if (unit.Combat.LOFCache.UnitHasLOFToTarget(unit, enemyUnit, w))
                    {
                        hasLOF = true;
                        break;
                    }
                }
                Debug.LogError(string.Format("target: {0} hasLOF: {1}", enemyUnit.DisplayName, hasLOF));
            }
        }
        */
        results.orderInfo = new OrderInfo(OrderType.Brace);
        return results;
    }
}

class FailNode : LeafBehaviorNode
{
    public FailNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class SucceedNode : LeafBehaviorNode
{
    public SucceedNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class BlockUntilPathfindingReadyNode : LeafBehaviorNode
{
    float startTime;
    int tickCount;

    const float TIME_TO_WAIT_FOR_PATHFINDING = 60.0f;
    const int TICKS_TO_WAIT_FOR_PATHFINDING = 20;

    public BlockUntilPathfindingReadyNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected void OnStart()
    {
        startTime = Time.realtimeSinceStartup;
        tickCount = 0;
        LogAI("Block until pathfinding ready started at " + startTime);

        if ((unit.Pathing != null) && (!unit.Pathing.ArePathGridsComplete))
        {
            // pathing isn't ready yet, make sure it's highest priority.
            unit.Combat.PathingManager.AddNewBlockingPath(unit.Pathing);
        }
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.Pathing == null)
        {
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        tickCount += 1;

        // if the pathfinding information is ready
        if (unit.Pathing.ArePathGridsComplete)
        {
            LogAI("Block until pathfinding completing with grids complete");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        float timeNow = Time.realtimeSinceStartup;

        float deltaTime = timeNow - startTime;
        if ((deltaTime > TIME_TO_WAIT_FOR_PATHFINDING) && (tickCount > TICKS_TO_WAIT_FOR_PATHFINDING))
        {
            LogAI(string.Format("Block until pathfinding failing, having timed out with too long a time {0} {1}", deltaTime, tickCount));
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
        else
        {
            LogAI("Block until pathfinding waiting for pathing");
            return new BehaviorTreeResults(BehaviorNodeState.Running);
        }
    }
}

class IsMovementAvailableForUnitNode : LeafBehaviorNode
{
    public IsMovementAvailableForUnitNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        // For units that cannot move, fail fast
        Turret turretUnit = unit as Turret;
        if (turretUnit != null)
        {
            // Turrets cannot move, so no movement is available, return failure, which is like a false for predicates like this.
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        // if can move
        if ((unit.IsOperational) && (!unit.HasMovedThisRound))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}

class IsAttackAvailableForUnitNode : LeafBehaviorNode
{
    public IsAttackAvailableForUnitNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((unit.IsOperational) && (!unit.HasFiredThisRound) && (unit.Combat.TurnDirector.IsInterleaved))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}

class IsMeleeAvailableForUnitNode : LeafBehaviorNode
{
    public IsMeleeAvailableForUnitNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.IsOperational)// && (!unit.IsInMelee))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}

class IsSprintAvailableForUnitNode : LeafBehaviorNode
{
    public IsSprintAvailableForUnitNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        bool isUnsteady = false;
        Mech mech = unit as Mech;
        if (mech != null)
        {
            isUnsteady = mech.IsUnsteady;
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.CanSprint && !isUnsteady);
    }
}

class RandomPercentageLessThanBVNode : LeafBehaviorNode
{
    BehaviorVariableName bvName;
    public RandomPercentageLessThanBVNode(string name, BehaviorTree tree, AbstractActor unit, BehaviorVariableName bvName) : base(name, tree, unit)
    {
        this.bvName = bvName;
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorVariableValue toHitPercentage = tree.GetBehaviorVariableValue(bvName);
        if (toHitPercentage == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        // Need a random number from 0 (inclusive) to 100 (exclusive). Chances of rolling a 100 exactly is basically
        // zero, but still, be paranoid and reroll 100s if they do come up.

        float roll = -1.0f;
        bool rollIsGood = false;
        while (!rollIsGood)
        {
            roll = Random.Range(0.0f, 100.0f);
            rollIsGood = (roll != 100.0f);
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(roll < toHitPercentage.FloatVal);
    }
}

class IsBVTrueNode : LeafBehaviorNode
{
    BehaviorVariableName bvName;
    public IsBVTrueNode(string name, BehaviorTree tree, AbstractActor unit, BehaviorVariableName bvName) : base(name, tree, unit)
    {
        this.bvName = bvName;
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorVariableValue value = tree.GetBehaviorVariableValue(bvName);
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(value.BoolVal);
    }
}

class IsInMinWeaponRange : LeafBehaviorNode
{
    public IsInMinWeaponRange(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    protected override BehaviorTreeResults Tick()
    {
        float weaponRange = AIUtil.GetMaxWeaponRange(unit);
        float distanceToHostiles = AIUtil.DistanceToClosestEnemy(unit, unit.CurrentPosition);

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(weaponRange > distanceToHostiles);
    }
}

class IsUrbanBiomeNode : LeafBehaviorNode
{
    private TerrainGenerator terrainGen = null;

    public IsUrbanBiomeNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    protected override BehaviorTreeResults Tick()
    {
        bool isAlwaysEnabled = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_EnableUrbanBiomeNavigationEverywhere).BoolVal;
        bool isEnabledForUrbanBiomes = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_EnableUrbanBiomeNavigation).BoolVal;

        if (isAlwaysEnabled)
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);

        if (isEnabledForUrbanBiomes)
        {
            if (terrainGen == null)
                terrainGen = Terrain.activeTerrain.GetComponent<TerrainGenerator>();

            bool isUrban = terrainGen.biome.biomeSkin == Biome.BIOMESKIN.urbanHighTech;
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(isUrban);
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
    }
}

class SetNavigatingNookNode : LeafBehaviorNode
{
    private bool isNavigatingNook;
    public SetNavigatingNookNode(string name, BehaviorTree tree, AbstractActor unit, bool isNavigatingNook) : base(name, tree, unit)
    {
        this.isNavigatingNook = isNavigatingNook;
    }

    protected override BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Bool_IsNavigatingNook, new BehaviorVariableValue(isNavigatingNook));
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }
}

class SetNoLOFToHostilesCacheNode : LeafBehaviorNode
{
    private bool noLOFToHostiles;
    public SetNoLOFToHostilesCacheNode(string name, BehaviorTree tree, AbstractActor unit, bool noLOFToHostiles) : base(name, tree, unit)
    {
        this.noLOFToHostiles = noLOFToHostiles;
    }

    protected override BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Bool_NoLOFToHostiles, new BehaviorVariableValue(noLOFToHostiles));
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }
}

class IsProneNode : LeafBehaviorNode
{
    public IsProneNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        LogAI("IsProne running");
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            LogAI("unit is not a mech, cannot be prone. Failing.");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(mechUnit.IsProne);
    }
}

class StandNode : LeafBehaviorNode
{
    public StandNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        if (mechUnit.IsProne)
        {
            BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
            results.orderInfo = new OrderInfo(OrderType.Stand);
            LogAI("Issuing stand order: " + results);
            return results;
        }
        else
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}

class IsUnsteadyNode : LeafBehaviorNode
{
    public IsUnsteadyNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.IsUnsteady)
        {
            LogAI("unit is labelled as being unsteady. Success.");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            LogAI("unit is not labelled as being unsteady. Failing.");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}

class IsBracedNode : LeafBehaviorNode
{
    public IsBracedNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.BracedLastRound)
        {
            LogAI("unit is labelled as being braced. Success.");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            LogAI("unit is not labelled as being braced. Failing.");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}

class IsEvasiveNode : LeafBehaviorNode
{
    public IsEvasiveNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.IsEvasive)
        {
            LogAI("unit is labelled as being evasive. Success.");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            LogAI("unit is not labelled as being evasive. Failing.");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}

class IsInspiredNode : LeafBehaviorNode
{
    public IsInspiredNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;

        if (mechUnit == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(mechUnit.IsFuryInspired);
    }
}

class IsAlertedNode : LeafBehaviorNode
{
    public IsAlertedNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        bool isAlerted = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_Alerted).BoolVal;
        return new BehaviorTreeResults(isAlerted ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}

class HasMoveCandidatesNode : LeafBehaviorNode
{
    public HasMoveCandidatesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((tree.movementCandidateLocations == null) || (tree.movementCandidateLocations.Count == 0))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
        else
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
    }
}

class DebugLogNode : LeafBehaviorNode
{
    string logMsg;

    public DebugLogNode(string name, BehaviorTree tree, AbstractActor unit, string msg) : base(name, tree, unit)
    {
        logMsg = msg;
    }

    override protected BehaviorTreeResults Tick()
    {
        LogAI(logMsg);
        AITeam aiTeam = unit.team as AITeam;
        aiTeam.behaviorTreeLogString += logMsg;
        aiTeam.behaviorTreeLogString += "\n";
        //Debug.LogError(string.Format("AI DebugLog for {0} {1} : {2}",unit.DisplayName, unit.Nickname, logMsg));
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class DebugLogToContextNode : LeafBehaviorNode
{
    string logMsg;
    AIDebugContext context;

    public DebugLogToContextNode(string name, BehaviorTree tree, AbstractActor unit, string msg, AIDebugContext context) : base(name, tree, unit)
    {
        logMsg = msg;
        this.context = context;
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.AddMessageToDebugContext(context, logMsg);
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class DebugLogEnemiesByThreatNode : LeafBehaviorNode
{
    public DebugLogEnemiesByThreatNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "enemies by threat");
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "----");

        for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
        {
            ICombatant enemyUnit = unit.BehaviorTree.enemyUnits[i];
            if (enemyUnit.IsDead)
            {
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("[{0}] --DEAD-- {1}", i, enemyUnit.DisplayName));
                continue;
            }
            AbstractActor enemyActor = enemyUnit as AbstractActor;
            if (enemyActor != null)
            {
                Pilot pilot = enemyActor.GetPilot();
                string pilotName = (pilot != null) ? pilot.Description.Name : "???";
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("[{0}] {1} {2} {3} {4} {5}", i, enemyUnit.DisplayName, enemyActor.UnitName, enemyActor.VariantName, enemyActor.Nickname, pilotName));
            }
            else
            {
                unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, string.Format("[{0}] {1}", i, enemyUnit.DisplayName));
            }
        }
        unit.BehaviorTree.AddMessageToDebugContext(AIDebugContext.Shoot, "----");
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class WithLogContextNode : DecoratorBehaviorNode
{
    AIDebugContext context;

    public WithLogContextNode(string name, BehaviorTree tree, AbstractActor unit, AIDebugContext context) : base(name, tree, unit)
    {
        this.context = context;
    }

    override protected BehaviorTreeResults Tick()
    {
        return ChildNode.Update();
    }

    override protected void OnStart()
    {
        // create our stringbuilder
        switch (context)
        {
            case AIDebugContext.Shoot:
                tree.debugLogShootContextStringBuilder = new System.Text.StringBuilder();
                Pilot p = unit.GetPilot();
                string pilotName = (p != null) ? p.Description.Name : "???";
                tree.debugLogShootContextStringBuilder.AppendLine(string.Format("Shooting Log for {0} {1} {2} {3} {4}", unit.DisplayName, unit.UnitName, unit.VariantName, unit.Nickname, pilotName));
                break;
            default:
                Debug.LogError("unrecognized debug log context: " + context);
                break;
        }
    }

    override protected void OnComplete()
    {
        // write it out
        string msg = null;
        string logPrefix = null;
        switch (context)
        {
            case AIDebugContext.Shoot:
                msg = tree.debugLogShootContextStringBuilder.ToString();
                tree.debugLogShootContextStringBuilder = null;
                logPrefix = "shoot";
                break;
            default:
                Debug.LogError("unrecognized debug log context: " + context);
                break;
        }
        string filename = unit.Combat.AILogCache.MakeFilename(logPrefix);
        unit.Combat.AILogCache.AddLogData(filename, msg);
    }
}

class SetMoodNode : LeafBehaviorNode
{
    AIMood mood;

    public SetMoodNode(string name, BehaviorTree tree, AbstractActor unit, AIMood mood) : base(name, tree, unit)
    {
        this.mood = mood;
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.mood = mood;
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class IsMoodNode : LeafBehaviorNode
{
    AIMood testMood;

    public IsMoodNode(string name, BehaviorTree tree, AbstractActor unit, AIMood mood) : base(name, tree, unit)
    {
        this.testMood = mood;
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.BehaviorTree.mood == testMood);
    }
}

class HasBulwarkSkillNode : LeafBehaviorNode
{
    public HasBulwarkSkillNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.HasBulwarkAbility);
    }
}

class HasRecklessSkillNode : LeafBehaviorNode
{
    public HasRecklessSkillNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.CanMoveAfterShooting);
    }
}

class IsStationaryMoveInBulwarkThresholdNode : LeafBehaviorNode
{
    public IsStationaryMoveInBulwarkThresholdNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count == 0)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        float curFloatAngle = unit.CurrentRotation.eulerAngles.y;
        int cur8Angle = PathingUtil.FloatAngleTo8Angle(curFloatAngle);

        float minAccum = float.MaxValue;
        float maxAccum = float.MinValue;

        for (int evalIndex = 0; evalIndex < unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count; ++evalIndex)
        {
            minAccum = Mathf.Min(minAccum, unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[evalIndex].RegularMoveAccumulator);
            maxAccum = Mathf.Max(maxAccum, unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[evalIndex].RegularMoveAccumulator);
        }

        for (int evalIndex = 0; evalIndex < unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count; ++evalIndex)
        {
            WorkspaceEvaluationEntry entry = unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[evalIndex];
            if ((entry.Position == unit.CurrentPosition) &&
                (PathingUtil.FloatAngleTo8Angle(entry.Angle) == cur8Angle))
            {
                // this is a stationary entry

                float thisValue = entry.RegularMoveAccumulator;
                float thisNormalized = (thisValue - minAccum) / (maxAccum - minAccum);

                bool inWindow = (thisNormalized >= 1.0 - unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_BulwarkThresholdPercentage).FloatVal / 100.0f);
                if (inWindow)
                {
                    return new BehaviorTreeResults(BehaviorNodeState.Success);
                }
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class SortMoveCandidatesByInfMapNode : LeafBehaviorNode
{
    public SortMoveCandidatesByInfMapNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    Color getColorForValue(float v)
    {
        List<KeyValuePair<float, Color>> colorList = new List<KeyValuePair<float, Color>>
        {
            new KeyValuePair<float, Color>(0.0f, new Color(1.0f, 0.0f, 0.5f)), // purple
            new KeyValuePair<float, Color>(0.2f, new Color(1.0f, 0.0f, 0.0f)), // red
            new KeyValuePair<float, Color>(0.5f, new Color(1.0f, 1.0f, 0.0f)), // yellow
            new KeyValuePair<float, Color>(0.9f, new Color(0.0f, 1.0f, 0.0f)), // green
            new KeyValuePair<float, Color>(1.0f, new Color(1.0f, 1.0f, 1.0f)), // white
        };

        if (v < 0.0f)
        {
            return colorList[0].Value;
        }

        if (v > 1.0f)
        {
            return colorList[colorList.Count - 1].Value;
        }

        for (int i = 1; i < colorList.Count; ++i)
        {
            int prev = i - 1;

            float prevVal = colorList[prev].Key;
            float thisVal = colorList[i].Key;

            if ((prevVal <= v) &&
                (v <= thisVal))
            {
                Color prevColor = colorList[prev].Value;
                Color thisColor = colorList[i].Value;

                float frac = (v - prevVal) / (thisVal - prevVal);

                float interpR = Mathf.Lerp(prevColor.r, thisColor.r, frac);
                float interpG = Mathf.Lerp(prevColor.g, thisColor.g, frac);
                float interpB = Mathf.Lerp(prevColor.b, thisColor.b, frac);

                return new Color(interpR, interpG, interpB);
            }
        }

        return Color.magenta;
    }

    void drawDebugLines()
    {
        int wsIndex;
        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        // find min and max
        for (wsIndex = 0; wsIndex < unit.BehaviorTree.influenceMapEvaluator.firstFreeWorkspaceEvaluationEntryIndex; ++wsIndex)
        {
            WorkspaceEvaluationEntry weEntry = unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[wsIndex];

            if (weEntry.HasRegularMove)
            {
                float val = weEntry.RegularMoveAccumulator;
                minVal = Mathf.Min(minVal, val);
                maxVal = Mathf.Max(maxVal, val);
            }

            if (weEntry.HasSprintMove)
            {
                float val = unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[wsIndex].SprintMoveAccumulator;
                minVal = Mathf.Min(minVal, val);
                maxVal = Mathf.Max(maxVal, val);
            }
        }

        for (int i = 0; i < unit.BehaviorTree.influenceMapEvaluator.firstFreeWorkspaceEvaluationEntryIndex; ++i)
        {
            WorkspaceEvaluationEntry wsEvalEntry = unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[i];
            if (wsEvalEntry.HasRegularMove)
            {
                DrawTick(wsEvalEntry, wsEvalEntry.RegularMoveAccumulator, minVal, maxVal);
            }
            if (wsEvalEntry.HasSprintMove)
            {
                DrawTick(wsEvalEntry, wsEvalEntry.SprintMoveAccumulator, minVal, maxVal);
            }
        }
    }

    private void DrawTick(WorkspaceEvaluationEntry entry, float accum, float minVal, float maxVal)
    {
        Vector3 destPos = entry.Position;
        float degrees = entry.Angle;
        Quaternion quat = Quaternion.Euler(0, degrees, 0);
        Vector3 rotatedForward = quat * Vector3.forward;

        float normalized = 0.5f;
        if (maxVal > minVal)
        {
            normalized = (accum - minVal) / (maxVal - minVal);
        }

        Color color = getColorForValue(normalized);
        float durationSeconds = 20.0f;
        Vector3 rayDirection = (rotatedForward + Vector3.up) * 0.4f * unit.Combat.HexGrid.HexWidth;
        Debug.DrawRay(destPos, rayDirection, color, durationSeconds);
    }

    override protected void OnStart()
    {
        unit.BehaviorTree.influenceMapEvaluator.InitializeEvaluationForUnit(unit);
        LogAI(string.Format("Sorting {0} candidates by influence map", unit.BehaviorTree.movementCandidateLocations.Count));

        //sprintWeightBiasMult = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SprintWeightBiasMultiplicative).FloatVal;
        //sprintWeightBiasAdd = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SprintWeightBiasAdditive).FloatVal;
    }

    private class AccumulatorComparer : IComparer<WorkspaceEvaluationEntry>
    {
        public int Compare(WorkspaceEvaluationEntry wee1, WorkspaceEvaluationEntry wee2)
        {
            // deliberately sorting decreasing
            return wee2.GetHighestAccumulator().CompareTo(wee1.GetHighestAccumulator());
        }
    }

    override protected BehaviorTreeResults Tick()
    {
        const float EVALUATION_SECONDS_PER_TICK = 0.02f;

        if (unit.BehaviorTree.movementCandidateLocations.Count == 0)
        {
            unit.BehaviorTree.influenceMapEvaluator.Reset();
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        bool evaluationComplete = unit.BehaviorTree.influenceMapEvaluator.RunEvaluationForSeconds(EVALUATION_SECONDS_PER_TICK);

        if (!evaluationComplete)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Running);
        }
        unit.BehaviorTree.influenceMapEvaluator.Reset();

        AccumulatorComparer accumComp = new AccumulatorComparer();

        unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries.Sort(
            0,
            unit.BehaviorTree.influenceMapEvaluator.firstFreeWorkspaceEvaluationEntryIndex,
            accumComp);

        unit.BehaviorTree.influenceMapEvaluator.ExportInfluenceMapToCSV();

        HBS.Logging.LogLevel aiLogLevel;
        HBS.Logging.Logger.GetLoggerLevel(HBS.Logging.LoggerNames.AI, out aiLogLevel);

        drawDebugLines();
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class FilterMoveCandidatesByReachabilityNode : LeafBehaviorNode
{
    public FilterMoveCandidatesByReachabilityNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        //Debug.LogError("Do Nothing");
        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class HighestMoveCandidateIsStationaryNode : LeafBehaviorNode
{
    public HighestMoveCandidateIsStationaryNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries == null) ||
            (unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count == 0))
        {
            LogAI("no move candidates, failing");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        Vector2 currentAxialPosition = unit.Combat.HexGrid.CartesianToHexAxial(unit.CurrentPosition);
        Vector2 destinationAxialPosition = unit.Combat.HexGrid.CartesianToHexAxial(unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[0].Position);

        int current8DirectionFacing = PathingUtil.FloatAngleTo8Angle(unit.CurrentRotation.eulerAngles.y);
        int dest8DirectionFacing = PathingUtil.FloatAngleTo8Angle(unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[0].Angle);

        if ((unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[0].GetBestMoveType() == MoveType.None) ||
            ((currentAxialPosition == destinationAxialPosition) &&
             (current8DirectionFacing == dest8DirectionFacing)))
        {
            LogAI("best movement is stationary");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        LogAI("best movement is not stationary");
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class HighestPriorityMoveCandidateIsAttackNode : LeafBehaviorNode
{
    public HighestPriorityMoveCandidateIsAttackNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((unit.BehaviorTree.movementCandidateLocations == null) ||
            (unit.BehaviorTree.movementCandidateLocations.Count == 0))
        {
            LogAI("no move candidates, failing");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        MoveDestination dest = unit.BehaviorTree.movementCandidateLocations[0];

        MeleeMoveDestination meleeDest = dest as MeleeMoveDestination;
        if (meleeDest != null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        Vector3 destPos = dest.PathNode.Position;
        Quaternion destRot = Quaternion.Euler(0, PathingUtil.FloatAngleFrom8Angle(dest.PathNode.Angle), 0);

        float maxRange = 0;
        bool isIndirectFireCapable = false;

        for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
        {
            Weapon weapon = unit.Weapons[weaponIndex];
            maxRange = Mathf.Max(maxRange, weapon.MaxRange);
            isIndirectFireCapable |= weapon.IndirectFireCapable;
        }

        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant target = unit.BehaviorTree.enemyUnits[enemyIndex];
            AbstractActor targetActor = target as AbstractActor;
            if ((targetActor != null) && (unit.VisibilityCache.VisibilityToTarget(targetActor).VisibilityLevel == VisibilityLevel.LOSFull) &&
                unit.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(unit, targetActor, maxRange, destPos, destRot, targetActor.CurrentPosition, targetActor.CurrentRotation, isIndirectFireCapable))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class HighestPriorityMoveCandidateIsWalkingMoveNode : LeafBehaviorNode
{
    public HighestPriorityMoveCandidateIsWalkingMoveNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries == null) ||
            (unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count == 0))
        {
            LogAI("no move candidates, failing");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        WorkspaceEvaluationEntry dest = unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[0];

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(dest.GetBestMoveType() == MoveType.Walking);
    }
}

class RewriteHighestPriorityMoveCandidateAsSprintNode : LeafBehaviorNode
{
    public RewriteHighestPriorityMoveCandidateAsSprintNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries == null) ||
            (unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries.Count == 0))
        {
            LogAI("no move candidates, failing");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        WorkspaceEvaluationEntry dest = unit.BehaviorTree.influenceMapEvaluator.WorkspaceEvaluationEntries[0];

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class DelaySecondsNode : LeafBehaviorNode
{
    float delayTimeSeconds;
    float startTime;

    public DelaySecondsNode(string name, BehaviorTree tree, AbstractActor unit, float seconds) : base(name, tree, unit)
    {
        this.delayTimeSeconds = seconds;
    }

    override protected void OnStart()
    {
        startTime = Time.realtimeSinceStartup;
        LogAI("Started at " + startTime);
    }

    override protected BehaviorTreeResults Tick()
    {
        float timeNow = Time.realtimeSinceStartup;
        LogAI("Ticked at " + startTime);

        float deltaTime = timeNow - startTime;
        LogAI("deltaTime: " + deltaTime);

        if (deltaTime >= delayTimeSeconds)
        {
            LogAI("complete");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            LogAI("pending");
            return new BehaviorTreeResults(BehaviorNodeState.Running);
        }
    }
}

class DelayCountNode : LeafBehaviorNode
{
    float delayCountMax;
    float counter;

    public DelayCountNode(string name, BehaviorTree tree, AbstractActor unit, int countMax) : base(name, tree, unit)
    {
        this.delayCountMax = countMax;
    }

    override protected void OnStart()
    {
        counter = 0;
        LogAI("Started counter");
    }

    override protected BehaviorTreeResults Tick()
    {
        counter++;
        LogAI("counter now: " + counter);

        if (counter >= delayCountMax)
        {
            LogAI("complete");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            LogAI("pending");
            return new BehaviorTreeResults(BehaviorNodeState.Running);
        }
    }
}

class IsOverheatedNode : LeafBehaviorNode
{
    public IsOverheatedNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            // non-mechs can never overheat, return false
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        float acceptableHeat = AIUtil.GetAcceptableHeatLevelForMech(mechUnit);
        float curHeat = mechUnit.CurrentHeat;

        return new BehaviorTreeResults(curHeat > acceptableHeat ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}

class FilterMovesForHeatNode : LeafBehaviorNode
{
    public FilterMovesForHeatNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            // non-mechs can never overheat, trivially successful
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }

        float shutdownHeat = mechUnit.MaxHeat;
        float curHeat = mechUnit.CurrentHeat;

        List<MoveDestination> newDestinations = new List<MoveDestination>();

        for (int moveIndex = 0; moveIndex < mechUnit.BehaviorTree.movementCandidateLocations.Count; ++moveIndex)
        {
            MoveDestination moveDest = mechUnit.BehaviorTree.movementCandidateLocations[moveIndex];
            MoveType moveType = moveDest.MoveType;
            Vector3 destination = moveDest.PathNode.Position;
            DesignMaskDef destDesignMaskDef = mechUnit.Combat.MapMetaData.GetPriorityDesignMaskAtPos(destination);
            float destinationHeatAddedPerTurn = (destDesignMaskDef == null) ? 0.0f : destDesignMaskDef.heatPerTurn;
            float moveHeat = 0.0f;
            bool hasAnyAttacks = false;

            switch (moveType)
            {
                case MoveType.Backward: // fall through
                case MoveType.Walking:
                    moveHeat = mechUnit.WalkHeat;
                    break;
                case MoveType.Sprinting:
                    moveHeat = mechUnit.SprintHeat;
                    break;
                case MoveType.Jumping:
                    moveHeat = mechUnit.CalcJumpHeat((destination - mechUnit.CurrentPosition).magnitude);
                    break;
                default:
                    moveHeat = 0.0f;
                    break;
            }

            float newHeat = curHeat + moveHeat + destinationHeatAddedPerTurn;

            if (moveType == MoveType.Sprinting)
            {
                if (newHeat < shutdownHeat)
                {
                    newDestinations.Add(moveDest);
                }
            }
            else
            {
                bool hasNonShutdownAttack = false;
                bool hasShutdownKillAttack = false;

                // is this a kill?
                for (int hostileIndex = 0; hostileIndex < mechUnit.BehaviorTree.enemyUnits.Count; ++hostileIndex)
                {
                    ICombatant hostile = mechUnit.BehaviorTree.enemyUnits[hostileIndex];

                    float attackDamage = 0.0f;
                    float attackHeat = 0.0f;

                    for (int weaponIndex = 0; weaponIndex < mechUnit.Weapons.Count; ++weaponIndex)
                    {
                        Weapon w = mechUnit.Weapons[weaponIndex];
                        if ((!w.CanFire) || (!w.WillFireAtTargetFromPosition(hostile, destination)))
                        {
                            continue;
                        }

                        AbstractActor hostileActor = hostile as AbstractActor;
                        bool hostileIsEvasive = (hostileActor != null) && (hostileActor.IsEvasive);
                        float toHit = w.GetToHitFromPosition(hostile, 1, destination, hostile.CurrentPosition, true, hostileIsEvasive);
                        attackDamage += toHit * w.ShotsWhenFired * w.DamagePerShotFromPosition(MeleeAttackType.NotSet, destination, hostile);
                        if (attackDamage > 0)
                        {
                            hasAnyAttacks = true;
                            attackHeat += w.HeatGenerated;
                        }
                    }

                    attackHeat *= mechUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_HeatFracForHeatFilter).FloatVal;
                    attackDamage *= mechUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_DamageFracForHeatFilter).FloatVal;

                    if (newHeat + attackHeat < shutdownHeat)
                    {
                        hasNonShutdownAttack = true;
                        break;
                    }
                    if (attackDamage > AttackEvaluator.MinHitPoints(hostile))
                    {
                        hasShutdownKillAttack = true;
                        break;
                    }
                }

                if (hasNonShutdownAttack || hasShutdownKillAttack || (!hasAnyAttacks))
                {
                    newDestinations.Add(moveDest);
                }
            }
        }

        mechUnit.BehaviorTree.movementCandidateLocations = newDestinations;

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class IsOutsideCoolDownRangeNode : LeafBehaviorNode
{
    public IsOutsideCoolDownRangeNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        float distanceToHostiles = AIUtil.DistanceToClosestEnemy(unit, unit.CurrentPosition);
        float coolDownRange = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CoolDownRange).FloatVal;

        return new BehaviorTreeResults(distanceToHostiles > coolDownRange ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}

class HasLOSToAnyHostileNode : LeafBehaviorNode
{
    public HasLOSToAnyHostileNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant target = unit.BehaviorTree.enemyUnits[enemyIndex];
            AbstractActor targetActor = target as AbstractActor;
            if ((targetActor != null) && (unit.VisibilityCache.VisibilityToTarget(targetActor).VisibilityLevel == VisibilityLevel.LOSFull))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class HasDirectLOFToAnyHostileNode : LeafBehaviorNode
{
    public HasDirectLOFToAnyHostileNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant target = unit.BehaviorTree.enemyUnits[enemyIndex];
            AbstractActor targetActor = target as AbstractActor;

            if (targetActor == null)
            {
                continue;
            }

            if ((AIUtil.UnitHasDirectLOFToUnit(unit, targetActor, unit.Combat)))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }

            // also check if we might be able to melee
            float distanceToTarget = (targetActor.CurrentPosition - unit.CurrentPosition).magnitude;

            if (distanceToTarget <= unit.MaxWalkDistance)
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class HasDirectLOFToAnyHostileFromReachableLocationsNode : LeafBehaviorNode
{
    public HasDirectLOFToAnyHostileFromReachableLocationsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.Pathing.SetWalking();

        // if the pathfinding information is ready
        if (!unit.Pathing.ArePathGridsComplete)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Running);
        }

        List<PathNode> pathNodes = unit.Pathing.CurrentGrid.GetSampledPathNodes();

        foreach (PathNode pn in pathNodes)
        {
            for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
            {
                ICombatant target = unit.BehaviorTree.enemyUnits[enemyIndex];
                AbstractActor targetActor = target as AbstractActor;

                if ((targetActor == null) || targetActor.IsDead)
                {
                    continue;
                }

                if ((AIUtil.UnitHasDirectLOFToTargetFromPosition(unit, targetActor, unit.Combat, pn.Position)))
                {
                    return new BehaviorTreeResults(BehaviorNodeState.Success);
                }

                // also check if we might be able to melee
                float distanceToTarget = AIUtil.Get2DDistanceBetweenVector3s(targetActor.CurrentPosition, pn.Position);
                if (distanceToTarget <= unit.Combat.Constants.MoveConstants.MeleeDistance)
                {
                    return new BehaviorTreeResults(BehaviorNodeState.Success);
                }
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class HasLOFToAnyHostileFromReachableLocationsNode : LeafBehaviorNode
{
    public HasLOFToAnyHostileFromReachableLocationsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        // first, consider melee, as that's perhaps the fastest way to return success.
        unit.Pathing.SetMelee();
        // if the pathfinding information is not yet ready
        if (!unit.Pathing.ArePathGridsComplete)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Running);
        }

        for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
        {
            ICombatant target = unit.BehaviorTree.enemyUnits[i];
            AbstractActor targetActor = target as AbstractActor;
            if (targetActor == null)
            {
                continue;
            }

            if (unit.Pathing.CanMeleeMoveTo(targetActor))
            {
                return new BehaviorTreeResults(BehaviorNodeState.Success);
            }
        }

        unit.Pathing.SetWalking();

        // if the pathfinding information is not yet ready
        if (!unit.Pathing.ArePathGridsComplete)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Running);
        }

        List<PathNode> pathNodes = unit.Pathing.CurrentGrid.GetSampledPathNodes();

        foreach (PathNode pn in pathNodes)
        {
            for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
            {
                ICombatant target = unit.BehaviorTree.enemyUnits[enemyIndex];

                if (target.IsDead)
                    continue;

                if ((AIUtil.UnitHasLOFToTargetFromPosition(unit, target, unit.Combat, pn.Position)))
                {
                    return new BehaviorTreeResults(BehaviorNodeState.Success);
                }

                // also check if we might be able to melee
                float distanceToTarget = AIUtil.Get2DDistanceBetweenVector3s(target.CurrentPosition, pn.Position);
                if (distanceToTarget <= unit.Combat.Constants.MoveConstants.MeleeDistance)
                {
                    return new BehaviorTreeResults(BehaviorNodeState.Success);
                }
            }
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class AnyHostileBehindMeNode : LeafBehaviorNode
{
    public AnyHostileBehindMeNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        List<AbstractActor> hostileUnits = new List<AbstractActor>();
        for (int enemyIndex = 0; enemyIndex < unit.Combat.AllActors.Count; ++enemyIndex)
        {
            AbstractActor hostileUnit = unit.Combat.AllActors[enemyIndex];
            if ((!hostileUnit.IsDead) && (hostileUnit.team.IsEnemy(unit.team)))
            {
                hostileUnits.Add(hostileUnit);
            }
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(AITeam.CanAnyUnitsFromListGetBehindUnit(unit, hostileUnits));
    }

}

class IsShutDownNode : LeafBehaviorNode
{
    public IsShutDownNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            // non-mechs can never overheat, return false
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        return new BehaviorTreeResults(mechUnit.IsShutDown ? BehaviorNodeState.Success : BehaviorNodeState.Failure);
    }
}

class MechStartUpNode : LeafBehaviorNode
{
    public MechStartUpNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        Mech mechUnit = unit as Mech;
        if (mechUnit == null)
        {
            // non-mechs can never overheat, return false
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
        results.orderInfo = new OrderInfo(OrderType.StartUp);
        LogAI("Issuing startup order: " + results);
        return results;
    }
}

class HasTakenMajorDamageSinceLastTurnNode : LeafBehaviorNode
{
    public HasTakenMajorDamageSinceLastTurnNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        float lastArmor = tree.previousAverageArmor;

        if (lastArmor <= 0)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        unit.EvaluateExpectedArmor();
        float currentArmor = unit.averageArmor;

        float delta = (lastArmor - currentArmor) / lastArmor;

        float ratio = tree.GetBehaviorVariableValue(BehaviorVariableName.Float_MajorDamageRatio).FloatVal;

        if (delta >= ratio)
        {
            //AIUtil.ShowAIDebugUnitFloatie(unit, "OUCH");
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        return new BehaviorTreeResults(BehaviorNodeState.Failure);
    }
}

class ExpectedDamageToMeLessThanNode : LeafBehaviorNode
{
    BehaviorVariableName bvName;
    public ExpectedDamageToMeLessThanNode(string name, BehaviorTree tree, AbstractActor unit, BehaviorVariableName bvName) : base(name, tree, unit)
    {
        this.bvName = bvName;
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorVariableValue overkillPercentage = tree.GetBehaviorVariableValue(bvName);
        if (overkillPercentage == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
        float overkillFrac = overkillPercentage.FloatVal / 100.0f;

        float criticalHP = AttackEvaluator.MinHitPoints(unit);
        float expDmg = 0.0f;

        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            AbstractActor hostile = unit.BehaviorTree.enemyUnits[enemyIndex] as AbstractActor;
            if ((hostile == null) || (hostile.IsDead))
            {
                continue;
            }

            expDmg += AIUtil.ExpectedDamageForAttack(hostile, AIUtil.AttackType.Shooting, hostile.Weapons, unit, hostile.CurrentPosition, unit.CurrentPosition, false, hostile);
        }

        float overkillValue = expDmg / criticalHP;
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(overkillValue > overkillFrac);
    }
}

class InspireAvailableNode : LeafBehaviorNode
{
    public InspireAvailableNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        // Note: "inspire" now just happens in single player campaign, and the "inspire" this node refers
        // to was changed to "Focus", which was then removed... at some point, this whole node
        // can be taken out! see BT-9362
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        /*
        if ((!unit.IsOperational) || (unit.HasFiredThisRound))
        {
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.team.CanInspire);
        */
    }
}

class EvaluateInspirationValueNode : LeafBehaviorNode
{
    public EvaluateInspirationValueNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    static public bool ShouldUnitUseInspire(AbstractActor unit)
    {
        float inspirationDelta = AIUtil.CalcMaxInspirationDelta(unit, true);
        AITeam aiTeam = unit.team as AITeam;

        if ((aiTeam == null) || (!unit.CanBeInspired))
        {
            return false;
        }

        if (inspirationDelta < unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MinimumInspirationDamage).FloatVal)
        {
            return false;
        }

        float inspirationFrac = 1.0f - aiTeam.GetInspirationWindow();

        return (inspirationDelta > aiTeam.GetInspirationTargetDamage() * inspirationFrac);
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(ShouldUnitUseInspire(unit));
    }
}

class ClaimInspirationNode : LeafBehaviorNode
{
    public ClaimInspirationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorTreeResults result = new BehaviorTreeResults(BehaviorNodeState.Success);
        result.orderInfo = new ClaimInspirationOrderInfo(unit);
        return result;
    }
}

class HasECMGhostedStateNode : LeafBehaviorNode
{
    public HasECMGhostedStateNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        bool isGhosted = false;

        Dictionary<string,List<EffectData>> effectDict = unit.AuraCache.PreviewAurasAffectingMe(unit, unit.CurrentPosition);
        foreach (List<EffectData> effects in effectDict.Values)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                EffectData effect = effects[i];
                if (effect.effectType == EffectType.StatisticEffect && effect.targetingData.auraEffectType == AuraEffectType.ECM_GHOST)
                    isGhosted = true;
            }
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(isGhosted);
    }
}

class HasMinimumStealthPipsNode : LeafBehaviorNode
{
    public HasMinimumStealthPipsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        bool hasMinStealthPips = unit.StealthPipsCurrent >= unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_MinimumECMGhostedPipsToFire).IntVal;
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(hasMinStealthPips);
    }
}

class CanDoSignificantDamageWhileGhostedNode : LeafBehaviorNode
{
    public CanDoSignificantDamageWhileGhostedNode(string name, BehaviorTree tree, AbstractActor unit) : base(name,tree,unit)
    {
    }

    protected override BehaviorTreeResults Tick()
    {
        bool canDoStructureDamage = false;

        for (int i = 0; i < tree.enemyUnits.Count; ++i)
        {
            var enemyUnity = tree.enemyUnits[i];

            var maxDamage = AIUtil.CalcMaxExpectedDamageToHostile(unit, enemyUnity, unit.IsFuryInspired, false, ignoreHitChance: true, accurateWeaponDmg: true);
            var expectedDamage = AIUtil.CalcMaxExpectedDamageToHostile(unit, enemyUnity, unit.IsFuryInspired, false, ignoreHitChance: false, accurateWeaponDmg: true);

            var weightedDamageLerpValue = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ExpectedAndMaxDamageShootingInGhostStateLerp).FloatVal;

            // Unclamped so that it can iteract properly with the hysteresis multiplier value
            var weightedDamage = Mathf.LerpUnclamped(expectedDamage, maxDamage, weightedDamageLerpValue + tree.ghostStateHysteresisMultiplier - 1);

            // Used for dividing dmg against number of weapons lerped by a value
            var weaponSpreadWeight = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_WeaponDamageSpreadLerpValue).FloatVal;

            // Increase confidence in landing more shots in same locations as turns without shooting increases
            weaponSpreadWeight *= tree.ghostStateHysteresisMultiplier;

            var chance = AttackEvaluator.ChanceToCauseStructuralDamage(unit, enemyUnity, weightedDamage, weaponSpreadWeight);
            var confidenceInChance = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ConfidenceInSignificantDamageWhileGhostedLevel).FloatVal;
            confidenceInChance -= tree.ghostStateHysteresisMultiplier - 1;

            var enemyActor = enemyUnity as AbstractActor;

            bool isInECM = unit.ParentECMCarrier == null ? true : unit.ParentECMCarrier.ghostSpotCounts.Contains(enemyUnity.GUID);

            if (isInECM || chance > confidenceInChance)
            {
                canDoStructureDamage = true;
                break;
            }
        }

        if (canDoStructureDamage)
        {
            tree.ghostStateHysteresisMultiplier = 1;
        }
        else
        {
            tree.ghostStateHysteresisMultiplier += unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_GhostStateHysteresisMultiplierTurnIncrease).FloatVal;
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(canDoStructureDamage);
    }
}

class CacheHasActiveProbeTargetsNode : LeafBehaviorNode
{
    public CacheHasActiveProbeTargetsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        float apRadius = 0f;
        int numClusteredEnemies = 0;

        // Get active probe radius
        for (int i = 0; i < unit.ComponentAbilities.Count; ++i)
        {
            Ability ability = unit.ComponentAbilities[i];
            if (ability.Def.Targeting == AbilityDef.TargetingType.ActiveProbe)
            {
                apRadius = ability.Def.FloatParam1;
                break;
            }
        }

        // BT-21716: This check results in skipping enemy units that either are ECM carriers, or are unGhosted. But we DO want to be able to active probe / sensor lock all units.
        /*bool isEnemeyECMfielded = false;
        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            AbstractActor enemy = unit.BehaviorTree.enemyUnits[enemyIndex] as AbstractActor;

            if (DoesActorHaveECM(enemy))
            {
                isEnemeyECMfielded = true;
                break;
            }
        }*/

        if( apRadius > 0f)
        {
            unit.BehaviorTree.activeProbeTargets.Clear();
            // Look for enemies within active probe range
            for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
            {
                AbstractActor enemy = unit.BehaviorTree.enemyUnits[enemyIndex] as AbstractActor;

                // BT-21716: This check results in skipping enemy units that either are ECM carriers, or are unGhosted. But we DO want to be able to active probe / sensor lock all units.
                /*if (enemy.StealthPipsCurrent == 0 && isEnemeyECMfielded)
                    continue;*/

                float sqrDistToEnemy = (unit.CurrentPosition - enemy.CurrentPosition).sqrMagnitude;
                if (sqrDistToEnemy < apRadius * apRadius)
                {
                    unit.BehaviorTree.activeProbeTargets.Add(enemy);
                    numClusteredEnemies++;
                }
            }
        }

        bool hasActiveProbeTargets = numClusteredEnemies >= unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_MinimumActiveProbeCount).IntVal;
        if (!hasActiveProbeTargets)
            unit.BehaviorTree.activeProbeTargets.Clear();

        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Bool_HasActiveProbeTargets, new BehaviorVariableValue(hasActiveProbeTargets));
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }

    private bool DoesActorHaveECM(AbstractActor actor)
    {
        if (actor == null || actor.IsDead)
            return false;

        if (actor.AuraComponents == null || actor.AuraComponents.Count <= 0)
            return false;

        for (int auraIndex = 0; auraIndex < actor.AuraComponents.Count; ++auraIndex)
        {
            MechComponent component = actor.AuraComponents[auraIndex];

            for (int i = 0; i < component.componentDef.statusEffects.Length; ++i)
            {
                EffectData effectData = component.componentDef.statusEffects[i];
                if(effectData.targetingData.auraEffectType == AuraEffectType.ECM_GHOST ||
                    effectData.targetingData.auraEffectType == AuraEffectType.ECM_GENERAL)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

class FireActiveProbeNode : LeafBehaviorNode
{
    public FireActiveProbeNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorTreeResults res = new BehaviorTreeResults(BehaviorNodeState.Success);
        res.orderInfo = new ActiveProbeOrderInfo(unit, unit.BehaviorTree.activeProbeTargets);
        return res;
    }
}

class DoesActiveProbeHaveTargetsNode : LeafBehaviorNode
{
    public DoesActiveProbeHaveTargetsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.BehaviorTree.unitBehaviorVariables.GetVariable(BehaviorVariableName.Bool_HasActiveProbeTargets).BoolVal);
    }
}

class ClearActiveProbeHasTargetsNode : LeafBehaviorNode
{
    public ClearActiveProbeHasTargetsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Bool_HasActiveProbeTargets, new BehaviorVariableValue(false));
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }
}

class HasActiveProbeAbilityNode : LeafBehaviorNode
{
    public HasActiveProbeAbilityNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((!unit.IsOperational) || (unit.HasFiredThisRound))
        {
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        bool hasAbility = unit.HasActiveProbeAbility;

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(hasAbility);
    }
}

class HasSensorLockAbilityNode : LeafBehaviorNode
{
    public HasSensorLockAbilityNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if ((!unit.IsOperational) || (unit.HasFiredThisRound))
        {
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(AIUtil.HasAbilityAvailable(unit, ActiveAbilityID.SensorLock));
    }
}

class ClearSensorLockNode : LeafBehaviorNode
{
    public ClearSensorLockNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        //clear behavior variable(s) for sensor lock targets
        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.String_SensorLockedTargetGUID, new BehaviorVariableValue(""));

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }
}

class HasSensorLockTargetNode : LeafBehaviorNode
{
    public HasSensorLockTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        var everyEnemyGhosted = AIUtil.IsEveryEnemyGhosted(unit, tree.enemyUnits);

        for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant enemy = unit.BehaviorTree.enemyUnits[enemyIndex];

            float enemySensorQuality;
            bool willSensorLock = AIUtil.EvaluateSensorLockQuality(unit, enemy, out enemySensorQuality, ignoreActivation: everyEnemyGhosted);

            if (willSensorLock && (enemySensorQuality > unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MinimumSensorLockQuality).FloatVal))
            {
                return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
            }
        }

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
    }
}

class HasRecordedSensorLockTargetNode : LeafBehaviorNode
{
    public HasRecordedSensorLockTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        string targetGUID = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_SensorLockedTargetGUID).StringVal;

        if (unit.team.attackedByEcmProtectedUnits.Count > 0 && AIUtil.HasAbilityAvailable(unit, ActiveAbilityID.SensorLock))
        {
            float currentThreat = float.MinValue;
            string currentTarget = targetGUID;

            for (int i = 0; i < unit.team.attackedByEcmProtectedUnits.Count; i++)
            {
                var attacker = unit.team.attackedByEcmProtectedUnits[i];
                if (attacker.IsGhosted)
                {
                    var threat = AIThreatUtil.GetThreatRatio(unit, attacker);

                    if (currentThreat < threat)
                    {
                        currentThreat = threat;
                        currentTarget = attacker.GUID;
                    }
                }
            }

            if (currentTarget != targetGUID)
            {
                unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.String_SensorLockedTargetGUID, new BehaviorVariableValue(currentTarget));
            }

            targetGUID = currentTarget;
        }

        bool hasTarget = !((targetGUID == null) || (targetGUID.Length == 0));
        if (!hasTarget)
        {
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }
        ICombatant target = unit.Combat.ItemRegistry.GetItemByGUID<ICombatant>(targetGUID);

        return (BehaviorTreeResults.BehaviorTreeResultsFromBoolean((target != null) && (!target.IsDead)));
    }
}

class SortEnemiesBySensorLockQualityNode : LeafBehaviorNode
{
    public SortEnemiesBySensorLockQualityNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    // Nested class to sort first by preferred damage ("overkill ratio"), then by distance
    private class SortByLockQualityHelper : IComparer<ICombatant>
    {
        AbstractActor attacker;
        bool ignoreActivation;

        public SortByLockQualityHelper(AbstractActor attacker, bool ignoreActivation)
        {
            this.attacker = attacker;
            this.ignoreActivation = ignoreActivation;
        }

        public int Compare(ICombatant a, ICombatant b)
        {
            AbstractActor t1 = a as AbstractActor;
            AbstractActor t2 = b as AbstractActor;

            if ((t1 == null) && (t2 == null))
            {
                return 0;
            }
            if (t1 == null)
            {
                return 1;
            }
            if (t2 == null)
            {
                return -1;
            }

            float slq1, slq2;
            bool b1 = AIUtil.EvaluateSensorLockQuality(attacker, t1, out slq1, ignoreActivation);
            bool b2 = AIUtil.EvaluateSensorLockQuality(attacker, t2, out slq2, ignoreActivation);

            if (b1 && b2)
            {
                // deliberately swapped, to sort decreasing
                return Comparer<float>.Default.Compare(slq2, slq1);
            }
            else if (b1)
            {
                return -1;
            }
            else if (b2)
            {
                return 1;
            }
            return 0;
        }
    }

    override protected BehaviorTreeResults Tick()
    {
        SortByLockQualityHelper slqh = new SortByLockQualityHelper(unit, AIUtil.IsEveryEnemyGhosted(unit, tree.enemyUnits));

        tree.enemyUnits.Sort(slqh);

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}

class RecordHighestPriorityEnemyAsSensorLockTargetNode : LeafBehaviorNode
{
    public RecordHighestPriorityEnemyAsSensorLockTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        var everyEnemyGhosted = AIUtil.IsEveryEnemyGhosted(unit, tree.enemyUnits);

        // find the index of the target
        int enemyIndex;
        for (enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
        {
            ICombatant enemy = unit.BehaviorTree.enemyUnits[enemyIndex];
            float enemySensorQuality;
            bool canSensorLock = AIUtil.EvaluateSensorLockQuality(unit, enemy, out enemySensorQuality, ignoreActivation: everyEnemyGhosted);

            if (canSensorLock && (enemySensorQuality > unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MinimumSensorLockQuality).FloatVal))
            {
                break;
            }
        }
        if (enemyIndex >= unit.BehaviorTree.enemyUnits.Count)
        {
            Debug.LogError("RecordHighestPriorityEnemyAsSensorLockTargetNode: failing because no targets are acceptable");
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        ICombatant target = unit.BehaviorTree.enemyUnits[enemyIndex];

        // store the sensor lock target in a behavior variable
        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.String_SensorLockedTargetGUID, new BehaviorVariableValue(target.GUID));

        //Debug.LogError("RecordHighestPriorityEnemyAsSensorLockTargetNode: succeeded");
        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }
}

class SensorLockRecordedSensorLockTargetNode : LeafBehaviorNode
{
    public SensorLockRecordedSensorLockTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        string targetGUID = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_SensorLockedTargetGUID).StringVal;

        ICombatant target = unit.Combat.ItemRegistry.GetItemByGUID<ICombatant>(targetGUID);

        if (!unit.team.IsEnemy(target.team))
        {
            Debug.LogError("target isn't an enemy for sensor lock: " + targetGUID);
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        if (target == null)
        {
            Debug.LogError("missing target for sensor lock: " + targetGUID);
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
        }

        if (unit.team.attackedByEcmProtectedUnits.Count > 0)
        {
            unit.team.attackedByEcmProtectedUnits.Clear();
        }

        BehaviorTreeResults res = new BehaviorTreeResults(BehaviorNodeState.Success);
        res.orderInfo = new SensorLockOrderInfo(unit, target);
        return res;
    }
}

class FilterNonSensorLockMovesNode : LeafBehaviorNode
{
    public FilterNonSensorLockMovesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        // only keep moves that are within sensor distance of our sensorLockTarget
        string targetGUID = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_SensorLockedTargetGUID).StringVal;
        bool hasTarget = !((targetGUID == null) || (targetGUID.Length == 0));
        if (!hasTarget)
        {
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
        }

        AbstractActor targetActor = unit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(targetGUID);
        if (targetActor == null)
        {
            Debug.LogError("Can't find sensor locked target with GUID " + targetGUID);
            return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
        }

        float detectionRange = unit.Combat.LOS.GetAdjustedSensorRange(unit, targetActor);

        List<MoveDestination> newDestinations = new List<MoveDestination>();

        float highestDistance = float.MinValue;

        for (int candidateLocationIndex = 0; candidateLocationIndex < tree.movementCandidateLocations.Count; ++candidateLocationIndex)
        {
            MoveDestination dest = tree.movementCandidateLocations[candidateLocationIndex];
            float distance = (dest.PathNode.Position - unit.CurrentPosition).magnitude;
            if (distance <= detectionRange)
            {
                newDestinations.Add(dest);
            }
            highestDistance = Mathf.Max(distance, highestDistance);
        }

        //Debug.LogErrorFormat("loc Count: {0} post-filter loc count: {1} highest distance: {2} range: {3}", tree.movementCandidateLocations.Count, newDestinations.Count, highestDistance, detectionRange);
        tree.movementCandidateLocations = newDestinations;

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }
}

class FilterOutNonLOFMovesNode : LeafBehaviorNode
{
    public FilterOutNonLOFMovesNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        List<MoveDestination> newDestinations = new List<MoveDestination>();

        for (int candidateLocationIndex = 0; candidateLocationIndex < tree.movementCandidateLocations.Count; ++candidateLocationIndex)
        {
            MoveDestination dest = tree.movementCandidateLocations[candidateLocationIndex];
            bool foundTarget = false;
            for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
            {
                Weapon w = unit.Weapons[weaponIndex];
                for (int targetIndex = 0; targetIndex < unit.BehaviorTree.enemyUnits.Count; ++targetIndex)
                {
                    ICombatant target = unit.BehaviorTree.enemyUnits[targetIndex];
                    Quaternion destRot = Quaternion.Euler(0, PathingUtil.FloatAngleFrom8Angle(dest.PathNode.Angle), 0);
                    if (w.WillFireAtTargetFromPosition(target, dest.PathNode.Position, destRot))
                    {
                        foundTarget = true;
                        break;
                    }
                }
                if (foundTarget)
                {
                    break;
                }
            }

            if (foundTarget)
            {
                newDestinations.Add(dest);
            }
        }

        //Debug.LogErrorFormat("loc Count: {0} post-filter loc count: {1} highest distance: {2} range: {3}", tree.movementCandidateLocations.Count, newDestinations.Count, highestDistance, detectionRange);
        tree.movementCandidateLocations = newDestinations;

        return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(true);
    }
}

public class MoveDestination
{
    public PathNode PathNode;
    public MoveType MoveType;

    public MoveDestination(PathNode pathNode, MoveType moveType)
    {
        this.PathNode = pathNode;
        this.MoveType = moveType;
    }
}

public class MeleeMoveDestination : MoveDestination
{
    public AbstractActor Target;

    public MeleeMoveDestination(PathNode pathNode, AbstractActor target) : base(pathNode, MoveType.Melee)
    {
        this.Target = target;
    }
}

[SerializableContract("BehaviorTree")]
public class BehaviorTree : IGuid
{
    [fastJSON.JsonIgnore]
    public GameInstance battleTechGame { get; private set; }

    [SerializableMember(SerializationTarget.SaveGame)]
    private BehaviorTreeIDEnum behaviorTreeIDEnum;


    public BehaviorNode RootNode { get; private set; }

    [fastJSON.JsonIgnore]
    public AbstractActor unit; // hydrate / dehydrate

    // List of known enemy units
    // serialization: initialize empty, will be rebuilt each activation
    public List<ICombatant> enemyUnits;

    // List of enemies that can be targeted by Active Probe
    //
    public List<ICombatant> activeProbeTargets;

    // List of candidate movement locations
    // serialization: initialize empty, will be rebuilt each activation
    public List<MoveDestination> movementCandidateLocations;

    // maps a tag to a priority
    [SerializableMember(SerializationTarget.SaveGame)]
    public Dictionary<string, int> preferredTargetPriorities;

    // a lower bound on accuracy for shots
    // serialization: _should_ be rebuilt each activation, probably skip?
    public float weaponToHitThreshold;

    [SerializableMember(SerializationTarget.SaveGame)]
    public BehaviorVariableScope unitBehaviorVariables;

    [SerializableMember(SerializationTarget.SaveGame)]
    public List<string> ExcludedRegionGUIDs;

    // a human readable version of the last order, for debug
    // serialization: rebuilt each activation for debug - ignore.
    public string lastOrderDebugString;

    [SerializableMember(SerializationTarget.SaveGame)]
    public AIMood mood;

    // logic for evaluating influence maps
    // serialization: hook up new empty object
    [fastJSON.JsonIgnore]
    public InfluenceMapEvaluator influenceMapEvaluator;

    // what my average armor was previously
    // serialization: no (recomputed each activation)
    public float previousAverageArmor;

    [SerializableMember(SerializationTarget.SaveGame)]
    public float sprintHysteresisLevel;

    [SerializableMember(SerializationTarget.SaveGame)]
    public float ghostStateHysteresisMultiplier;

    // debug stringbuilder
    // serialization: no
    public System.Text.StringBuilder debugLogShootContextStringBuilder;

    // debug stringbuilder
    // serialization: no
    public System.Text.StringBuilder behaviorTraceStringBuilder;

    [SerializableMember(SerializationTarget.SaveGame)]
    public int issuedOrdersOnRound;

    [SerializableMember(SerializationTarget.SaveGame)]
    public int issuedOrdersOnPhase;

    [SerializableMember(SerializationTarget.SaveGame)]
    public string GUID { get; private set; }

    public BehaviorTree(AbstractActor unit, GameInstance game, BehaviorTreeIDEnum behaviorTreeIDEnum)
    {
        battleTechGame = game;
        this.behaviorTreeIDEnum = behaviorTreeIDEnum;

        enemyUnits = new List<ICombatant>();
        activeProbeTargets = new List<ICombatant>();
        preferredTargetPriorities = new Dictionary<string, int>();

        this.unit = unit;

        unitBehaviorVariables = new BehaviorVariableScope();
        mood = AIMood.Undefined;
        influenceMapEvaluator = new InfluenceMapEvaluator();

        unit.EvaluateExpectedArmor();
        previousAverageArmor = unit.averageArmor;

        sprintHysteresisLevel = 1.0f;
        ghostStateHysteresisMultiplier = 1.0f;
        InitBehaviorTraceStringBuilder();

        issuedOrdersOnRound = -1;
        issuedOrdersOnPhase = -1;

        InitRootNode();
    }

    private BehaviorTree()
    {
    }

    // TO BE CALLED POST HYDRATION
    public void InitFromSave()
    {
        enemyUnits = new List<ICombatant>();

        influenceMapEvaluator = new InfluenceMapEvaluator();

        unit.EvaluateExpectedArmor();
        previousAverageArmor = unit.averageArmor;
        activeProbeTargets = new List<ICombatant>();

        InitBehaviorTraceStringBuilder();

        InitRootNode();
    }

    private void InitRootNode()
    {
        switch (behaviorTreeIDEnum)
        {
            case BehaviorTreeIDEnum.CoreAITree:
                RootNode = CoreAI_BT.InitRootNode(this, unit, battleTechGame);
                return;
            case BehaviorTreeIDEnum.DoNothingTree:
                RootNode = AlwaysPass_BT.InitRootNode(this, unit, battleTechGame);
                return;
            case BehaviorTreeIDEnum.FollowRouteAITree:
                RootNode = PatrolAI_BT.InitRootNode(this, unit, battleTechGame);
                return;
            case BehaviorTreeIDEnum.FleeAITree:
                RootNode = FleeAI_BT.InitRootNode(this, unit, battleTechGame);
                return;
            case BehaviorTreeIDEnum.PatrolAndShootAITree:
                RootNode = PatrolAndShoot_BT.InitRootNode(this, unit, battleTechGame);
                return;
            case BehaviorTreeIDEnum.TutorialSprintAITree:
                RootNode = TutorialSprint_BT.InitRootNode(this, unit, battleTechGame);
                return;
            case BehaviorTreeIDEnum.FollowRouteOppFireAITree:
                RootNode = PatrolOppAI_BT.InitRootNode(this, unit, battleTechGame);
                return;
            case BehaviorTreeIDEnum.PanzyrAITree:
                RootNode = PanzyrAI_BT.InitRootNode(this, unit, battleTechGame);
                return;
            default:
                Debug.Assert(false, "unrecognized Behavior Tree: " + behaviorTreeIDEnum.ToString());
                return;
        }
    }

    public void SetGuid(string guid)
    {
        GUID = guid;
    }

    public void Reset()
    {
        RootNode.Reset();
        InitBehaviorTraceStringBuilder();
    }

    public void InitBehaviorTraceStringBuilder()
    {
        behaviorTraceStringBuilder = new System.Text.StringBuilder();
        behaviorTraceStringBuilder.AppendLine("Round: " + unit.Combat.TurnDirector.CurrentRound);
        behaviorTraceStringBuilder.AppendLine("Phase: " + unit.Combat.TurnDirector.CurrentPhase);
        behaviorTraceStringBuilder.AppendLine("Unit: " + unit.DisplayName);
        behaviorTraceStringBuilder.AppendLine("Unit GUID: " + unit.GUID);
        Pilot p = unit.GetPilot();
        if (p == null)
        {
            behaviorTraceStringBuilder.AppendLine("NULL Pilot");
        }
        else
        {
            behaviorTraceStringBuilder.AppendLine("Pilot Callsign: " + p.Callsign);
        }
        if (unit.lance != null)
        {
            behaviorTraceStringBuilder.AppendLine("Lance: " + unit.lance.DisplayName);
        }
        else
        {
            behaviorTraceStringBuilder.AppendLine("Lance: NULL");
        }
        if (unit.team != null)
        {
            behaviorTraceStringBuilder.AppendLine("Team: " + unit.team.DisplayName);
        }
        else
        {
            behaviorTraceStringBuilder.AppendLine("Team: NULL");
        }

        behaviorTraceStringBuilder.AppendLine("Role: " + unit.DynamicUnitRole);
    }

    public BehaviorTreeResults Update()
    {
        BehaviorTreeResults results = RootNode.Update();

        unit.EvaluateExpectedArmor();
        previousAverageArmor = unit.averageArmor;
        return results;
    }

    internal BehaviorVariableValue GetBehaviorVariableValue(BehaviorVariableName name)
    {
        BehaviorVariableValue value = unitBehaviorVariables.GetVariable(name);
        if (value != null)
        {
            return value;
        }

        Pilot pilot = unit.GetPilot();
        if (pilot != null)
        {
            BehaviorVariableScope personalityScope = unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForAIPersonality(pilot.pilotDef.AIPersonality);
            if (personalityScope != null)
            {
                value = personalityScope.GetVariableWithMood(name, unit.BehaviorTree.mood);
                if (value != null)
                {
                    return value;
                }
            }
        }

        if (unit.lance != null)
        {
            value = unit.lance.BehaviorVariables.GetVariable(name);
            if (value != null)
            {
                return value;
            }
        }
        if (unit.team != null)
        {
            value = unit.team.BehaviorVariables.GetVariable(name);
            if (value != null)
            {
                return value;
            }
        }

        UnitRole role = unit.DynamicUnitRole;
        if (role == UnitRole.Undefined)
        {
            role = unit.StaticUnitRole;
        }

        BehaviorVariableScope roleScope = unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForRole(role);
        if (roleScope != null)
        {
            value = roleScope.GetVariableWithMood(name, unit.BehaviorTree.mood);
            if (value != null)
            {
                return value;
            }
        }

        // TODO if there are more skill-based tests, add them here. Probably also refactor this into some better support functions.
        if (unit.CanMoveAfterShooting)
        {
            BehaviorVariableScope skillScope = unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetScopeForAISkill(AISkillID.Reckless);
            if (skillScope != null)
            {
                value = skillScope.GetVariableWithMood(name, unit.BehaviorTree.mood);
                if (value != null)
                {
                    return value;
                }
            }
        }

        value = unit.Combat.BattleTechGame.BehaviorVariableScopeManager.GetGlobalScope().GetVariableWithMood(name, unit.BehaviorTree.mood);
        if (value != null)
        {
            return value;
        }

        return DefaultBehaviorVariableValue.GetSingleton();
    }

    internal void RemoveBehaviorVariableValue(BehaviorVariableName name)
    {
        BehaviorVariableValue value = unitBehaviorVariables.GetVariable(name);
        if (value != null)
        {
            unitBehaviorVariables.RemoveVariable(name);
            return;
        }
        if (unit.lance != null)
        {
            value = unit.lance.BehaviorVariables.GetVariable(name);
            if (value != null)
            {
                unit.lance.BehaviorVariables.RemoveVariable(name);
                return;
            }
        }
        if (unit.team != null)
        {
            value = unit.team.BehaviorVariables.GetVariable(name);
            if (value != null)
            {
                unit.team.BehaviorVariables.RemoveVariable(name);
                return;
            }
        }
    }

    public List<AbstractActor> GetAllyUnits()
    {
        List<AbstractActor> allies = new List<AbstractActor>();

        for (int teamIndex = 0; teamIndex < unit.Combat.Teams.Count; ++teamIndex)
        {
            Team team = unit.Combat.Teams[teamIndex];
            if (team.IsFriendly(unit.team))
            {
                for (int unitIndex = 0; unitIndex < team.units.Count; ++unitIndex)
                {
                    AbstractActor otherUnit = team.units[unitIndex];
                    if ((otherUnit != unit) && (!otherUnit.IsDead))
                    {
                        allies.Add(otherUnit);
                    }
                }
            }
        }
        return allies;
    }

    public void IssueAIOrder(AIOrder order)
    {
        unitBehaviorVariables.IssueAIOrder(order);
    }

    public bool IsTargetIgnored(ICombatant targetUnit)
    {
        if (unit.team != null)
        {
            List<ITargetPriorityRecord> teamIgnoreTargetRecords = unit.team.BehaviorVariables.IgnoreTargetRecords;
            for (int tprIndex = 0; tprIndex < teamIgnoreTargetRecords.Count; ++tprIndex)
            {
                ITargetPriorityRecord record = teamIgnoreTargetRecords[tprIndex];
                int targetPriority;
                bool priorityApplies = record.GetTargetPriorityForUnit(targetUnit, out targetPriority);
                if (priorityApplies)
                {
                    return true;
                }
            }
        }

        if (unit.lance != null)
        {
            List<ITargetPriorityRecord> lanceIgnoreTargetRecords = unit.lance.BehaviorVariables.IgnoreTargetRecords;
            for (int lprIndex = 0; lprIndex < lanceIgnoreTargetRecords.Count; ++lprIndex)
            {
                ITargetPriorityRecord record = lanceIgnoreTargetRecords[lprIndex];
                int targetPriority;
                bool priorityApplies = record.GetTargetPriorityForUnit(targetUnit, out targetPriority);
                if (priorityApplies)
                {
                    return true;
                }
            }
        }

        for (int uprIndex = 0; uprIndex < unitBehaviorVariables.IgnoreTargetRecords.Count; ++uprIndex)
        {
            ITargetPriorityRecord record = unitBehaviorVariables.IgnoreTargetRecords[uprIndex];
            int targetPriority;
            bool priorityApplies = record.GetTargetPriorityForUnit(targetUnit, out targetPriority);
            if (priorityApplies)
            {
                return true;
            }
        }

        return false;

    }

    public int TargetPriorityForUnit(ICombatant targetUnit)
    {
        if (IsTargetIgnored(targetUnit))
        {
            return 0;
        }

        int priority = 0;

        if (unit.team != null)
        {
            List<ITargetPriorityRecord> teamTargetRecords = unit.team.BehaviorVariables.TargetPriorityRecords;

            for (int tprIndex = 0; tprIndex < teamTargetRecords.Count; ++tprIndex)
            {
                ITargetPriorityRecord record = teamTargetRecords[tprIndex];
                int targetPriority;
                bool priorityApplies = record.GetTargetPriorityForUnit(targetUnit, out targetPriority);
                if (priorityApplies)
                {
                    priority = targetPriority;
                }
            }
        }

        if (unit.lance != null)
        {
            List<ITargetPriorityRecord> lanceTargetRecords = unit.lance.BehaviorVariables.TargetPriorityRecords;

            for (int lprIndex = 0; lprIndex < lanceTargetRecords.Count; ++lprIndex)
            {
                ITargetPriorityRecord record = lanceTargetRecords[lprIndex];
                int targetPriority;
                bool priorityApplies = record.GetTargetPriorityForUnit(targetUnit, out targetPriority);
                if (priorityApplies)
                {
                    priority = targetPriority;
                }
            }
        }

        for (int uprIndex = 0; uprIndex < unitBehaviorVariables.TargetPriorityRecords.Count; ++uprIndex)
        {
            ITargetPriorityRecord record = unitBehaviorVariables.TargetPriorityRecords[uprIndex];
            int targetPriority;
            bool priorityApplies = record.GetTargetPriorityForUnit(targetUnit, out targetPriority);
            if (priorityApplies)
            {
                priority = targetPriority;
            }
        }

        return priority;
    }

    bool AnyRecordsHaveKnownTargets(List<ITargetPriorityRecord> targetRecords)
    {
        if (targetRecords == null)
        {
            return false;
        }

        for (int recordIndex = 0; recordIndex < targetRecords.Count; ++recordIndex)
        {
            ITargetPriorityRecord record = targetRecords[recordIndex];

            RegionTargetPriorityRecord regionRecord = record as RegionTargetPriorityRecord;

            if (regionRecord != null)
            {
                return true;
            }

            for (int unitIndex = 0; unitIndex < unit.Combat.AllActors.Count; ++unitIndex)
            {
                AbstractActor otherActor = unit.Combat.AllActors[unitIndex];
                if (otherActor.IsDead)
                {
                    continue;
                }
                if (unit.Combat.HostilityMatrix.GetHostility(unit.team.GUID, otherActor.team.GUID) != Hostility.FRIENDLY)
                {
                    int targetPriority;
                    bool priorityApplies = record.GetTargetPriorityForUnit(otherActor, out targetPriority);
                    if (priorityApplies)
                    {
                        if (unit.team.VisibilityToTarget(otherActor) == VisibilityLevel.LOSFull)
                        {
                            return true;
                        }
                    }
                }
            }
            List<ICombatant> miscCombatants = unit.Combat.GetAllMiscCombatants();
            for (int miscIndex = 0; miscIndex < miscCombatants.Count; ++miscIndex)
            {
                ICombatant miscCombatant = miscCombatants[miscIndex];
                if (miscCombatant.IsDead)
                {
                    continue;
                }
                int targetPriority;
                bool priorityApplies = record.GetTargetPriorityForUnit(miscCombatant, out targetPriority);
                if (priorityApplies && (targetPriority > 0))
                {
                    return true;
                }
            }

        }
        return false;
    }

    public bool HasPriorityTargets()
    {
        if (WasUnitTargetedRecently(unit))
        {
            return false;
        }

        if (unit.team != null)
        {
            List<ITargetPriorityRecord> teamTargetRecords = unit.team.BehaviorVariables.TargetPriorityRecords;

            if (AnyRecordsHaveKnownTargets(teamTargetRecords))
            {
                return true;
            }
        }

        if (unit.lance != null)
        {
            List<ITargetPriorityRecord> lanceTargetRecords = unit.lance.BehaviorVariables.TargetPriorityRecords;

            if (AnyRecordsHaveKnownTargets(lanceTargetRecords))
            {
                return true;
            }
        }

        return (AnyRecordsHaveKnownTargets(unitBehaviorVariables.TargetPriorityRecords));
    }

    List<ICombatant> GetPriorityTargetsForRecords(List<ITargetPriorityRecord> targetRecords)
    {
        List<ICombatant> targets = new List<ICombatant>();
        if ((targetRecords == null) || (targetRecords.Count == 0))
        {
            return targets;
        }

        for (int recordIndex = 0; recordIndex < targetRecords.Count; ++recordIndex)
        {
            ITargetPriorityRecord record = targetRecords[recordIndex];

            for (int unitIndex = 0; unitIndex < unit.Combat.AllActors.Count; ++unitIndex)
            {
                AbstractActor otherActor = unit.Combat.AllActors[unitIndex];
                if (otherActor.IsDead)
                {
                    continue;
                }
                if (unit.Combat.HostilityMatrix.GetHostility(unit.team.GUID, otherActor.team.GUID) != Hostility.FRIENDLY)
                {
                    int targetPriority;
                    bool priorityApplies = record.GetTargetPriorityForUnit(otherActor, out targetPriority);
                    if (priorityApplies)
                    {
                        if (unit.team.VisibilityToTarget(otherActor) == VisibilityLevel.LOSFull)
                        {
                            targets.Add(otherActor);
                        }
                    }
                }
            }
            List<ICombatant> miscCombatants = unit.Combat.GetAllMiscCombatants();
            for (int miscIndex = 0; miscIndex < miscCombatants.Count; ++miscIndex)
            {
                ICombatant miscCombatant = miscCombatants[miscIndex];
                if (miscCombatant.IsDead)
                {
                    continue;
                }
                int targetPriority;
                bool priorityApplies = record.GetTargetPriorityForUnit(miscCombatant, out targetPriority);
                if ((priorityApplies) && (targetPriority > 0))
                {
                    targets.Add(miscCombatant);
                }
            }
        }
        return targets;
    }

    public List<ICombatant> GetPriorityTargets()
    {
        List<ICombatant> targets = new List<ICombatant>();
        if (!WasUnitTargetedRecently(unit))
        {
            if (unit.team != null)
            {
                List<ITargetPriorityRecord> teamTargetRecords = unit.team.BehaviorVariables.TargetPriorityRecords;
                targets.AddRange(GetPriorityTargetsForRecords(teamTargetRecords));
            }

            if (unit.lance != null)
            {
                List<ITargetPriorityRecord> lanceTargetRecords = unit.lance.BehaviorVariables.TargetPriorityRecords;
                targets.AddRange(GetPriorityTargetsForRecords(lanceTargetRecords));
            }

            targets.AddRange(GetPriorityTargetsForRecords(unitBehaviorVariables.TargetPriorityRecords));
        }
        return targets;
    }

    public bool WasUnitTargetedRecently(AbstractActor unit)
    {
        if (unit.LastTargetedPhaseNumber < 0)
        {
            return false;
        }

        int recentTargetThreshold = GetBehaviorVariableValue(BehaviorVariableName.Int_RecentTargetThresholdPhases).IntVal;
        int phasesSinceTarget = unit.Combat.TurnDirector.TotalElapsedPhases - unit.LastTargetedPhaseNumber;
        return (phasesSinceTarget <= recentTargetThreshold);
    }

    public float GetSprintHysteresisLevel()
    {
        return sprintHysteresisLevel;
    }

    public void DecreaseSprintHysteresisLevel()
    {
        float sprintHysteresisMultiplier = GetBehaviorVariableValue(BehaviorVariableName.Float_SprintHysteresisMultiplier).FloatVal;
        Debug.Assert(sprintHysteresisMultiplier < 1.0f);
        sprintHysteresisLevel *= sprintHysteresisMultiplier;
    }

    public void IncreaseSprintHysteresisLevel()
    {
        float sprintHysteresisRecoveryTurns = GetBehaviorVariableValue(BehaviorVariableName.Float_SprintHysteresisRecoveryTurns).FloatVal;

        if (sprintHysteresisRecoveryTurns <= 1.0f)
        {
            sprintHysteresisLevel = 1.0f;
        }
        else
        {
            sprintHysteresisLevel = Mathf.Min(sprintHysteresisLevel + 1.0f / sprintHysteresisRecoveryTurns, 1.0f);
        }
    }

    public void AddMessageToDebugContext(AIDebugContext context, string msg)
    {
        switch (context)
        {
            case AIDebugContext.Shoot:
                if (debugLogShootContextStringBuilder == null)
                {
                    // I guess we can shoot outside of the shoot subtree?

                    debugLogShootContextStringBuilder = new System.Text.StringBuilder();
                    Pilot p = unit.GetPilot();
                    string pilotName = (p != null) ? p.Description.Name : "???";
                    debugLogShootContextStringBuilder.AppendLine(string.Format("Shooting Log for {0} {1} {2} {3} {4}", unit.DisplayName, unit.UnitName, unit.VariantName, unit.Nickname, pilotName));
                    debugLogShootContextStringBuilder.AppendLine(msg);
                    string logMsg = debugLogShootContextStringBuilder.ToString();
                    debugLogShootContextStringBuilder = null;
                    string logPrefix = "shoot_sp";
                    string filename = unit.Combat.AILogCache.MakeFilename(logPrefix);
                    unit.Combat.AILogCache.AddLogData(filename, logMsg);
                }
                else
                {
                    debugLogShootContextStringBuilder.AppendLine(msg);
                }
                break;
            default:
                Debug.LogError("unknown debug log context: " + context);
                break;
        }
    }

    #region priority targets

    List<ICombatant> GetProximityTargets()
    {
        List<ICombatant> targets = new List<ICombatant>();

        string[] tags = new string[1] { unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_PreferProximityToTaggedTargetFactorTag).StringVal };

        List<ITaggedItem> taggedItems = unit.Combat.ItemRegistry.GetObjectsWithTagSet(new HBS.Collections.TagSet(tags));

        for (int targetIndex = 0; targetIndex < taggedItems.Count; ++targetIndex)
        {
            ICombatant target = taggedItems[targetIndex] as ICombatant;
            if ((target == null) || (target.IsDead))
            {
                continue;
            }
            targets.Add(target);
        }

        return targets;
    }

    public bool HasProximityTaggedTargets()
    {
        return (GetProximityTargets().Count > 0);
    }

    public bool IsOutsideProximityTargetDistance()
    {
        float distance = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PreferProximityToTaggedTargetFactorDistance).FloatVal;

        List<ICombatant> targets = GetProximityTargets();
        for (int targetIndex = 0; targetIndex < targets.Count; ++targetIndex)
        {
            ICombatant target = targets[targetIndex];
            if ((target.CurrentPosition - unit.CurrentPosition).magnitude > distance)
            {
                return true;
            }
        }
        return false;
    }

    #endregion // priority targets

    public void Hydrate(CombatGameState combat, BattleTech.Save.Test.SerializableReferenceContainer references)
    {
        battleTechGame = combat.BattleTechGame;
        unit = references.GetItem<AbstractActor>(this, "unit");
        if (unitBehaviorVariables != null)
        {
            unitBehaviorVariables.Hydrate(references);
        }
    }

    public void Dehydrate(BattleTech.Save.Test.SerializableReferenceContainer references)
    {
        references.AddItem(this, "unit", unit);
        if (unitBehaviorVariables != null)
        {
            unitBehaviorVariables.Dehydrate(references);
        }
    }

}
