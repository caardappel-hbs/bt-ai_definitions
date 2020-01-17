using GraphCoroutines;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BattleTech
{
	public class EvaluationDebugLogRecord
	{
		public float RawValue;
		public float NormValue;
		public float RegularValue;
		public float RegularWeight;
		public float SprintValue;
		public float SprintWeight;
		public float ScaledSprintValue;

		public EvaluationDebugLogRecord(float rawValue, float normValue, float regularValue, float regularWeight, float sprintValue, float sprintWeight)
		{
			this.RawValue = rawValue;
			this.NormValue = normValue;
			this.RegularValue = regularValue;
			this.RegularWeight = regularWeight;
			this.SprintValue = sprintValue;
			this.SprintWeight = sprintWeight;
		}
	}

	public enum ProfileSection
	{
		AllInfluenceMaps = 1,
		Locations = 2,
		PositionalFactors = 3,
		HostileFactors = 4,
		AllyFactors = 5,
	}

	public class WorkspaceKey : IEquatable<WorkspaceKey>, IComparable<WorkspaceKey>
	{
		public readonly int HexX;
		public readonly int HexY;
		public readonly int FacingAngleDegrees;
        public readonly string targetGUID;

		public WorkspaceKey(int hexX, int hexY, int degrees, AbstractActor target)
		{
			this.HexX = hexX;
			this.HexY = hexY;
			this.FacingAngleDegrees = degrees;
            this.targetGUID = (target != null) ? target.GUID : "";
		}

		public override int GetHashCode()
		{
			// picked a few primes to avoid too much clashing
			return FacingAngleDegrees + 401 * HexY + 7919 * HexX + targetGUID.GetHashCode();
		}

		public override bool Equals(object other)
		{
			return this.Equals(other as WorkspaceKey);
		}

		public bool Equals(WorkspaceKey other)
		{
			return ((other != null) &&
				(this.HexX == other.HexX) &&
				(this.HexY == other.HexY) &&
				(this.FacingAngleDegrees == other.FacingAngleDegrees));
		}

		public int CompareTo(object other)
		{
			return this.CompareTo(other as WorkspaceKey);
		}

		public int CompareTo(WorkspaceKey other)
		{
			if (other == null)
			{
				return 1;
			}

			if (this.HexX != other.HexX)
			{
				return this.HexX.CompareTo(other.HexX);
			}
			if (this.HexY != other.HexY)
			{
				return this.HexY.CompareTo(other.HexY);
			}
			else
			{
				return this.FacingAngleDegrees.CompareTo(other.FacingAngleDegrees);
			}
		}
	}

	public class WorkspaceEvaluationEntry
	{
		public Dictionary<MoveType, PathNode> PathNodes;
		public Vector3 Position;
		public float Angle;
		public float FactorValue;
		public float RegularMoveAccumulator;
		public float SprintMoveAccumulator;
		public float ScaledSprintMoveAccumulator;
		public Dictionary<string, EvaluationDebugLogRecord> ValuesByFactorName;
		public bool HasRegularMove { get; private set; }
		public bool HasSprintMove { get; private set; }
        public bool HasMeleeMove { get; private set; }
        public AbstractActor Target;

        public WorkspaceEvaluationEntry()
		{
			this.Reset(Vector3.zero, 0.0f, null);
		}

		public WorkspaceEvaluationEntry(PathNode pathNode, Vector3 position, float angle, MoveType moveType, AbstractActor target)
		{
			this.Reset(position, angle, target);
			this.AddMoveTypeToPathNode(moveType, pathNode);
            this.Target = target;
		}

        public void Reset(Vector3 position, float angleDegrees, AbstractActor target)
		{
			this.PathNodes = new Dictionary<MoveType, PathNode>();
			this.Position = position;
			this.Angle = angleDegrees;
			this.FactorValue = 0.0f; // used to store the current factor value pre normalization
			this.RegularMoveAccumulator = 0.0f; // used to collect all normalized values
			this.SprintMoveAccumulator = 0.0f; // used to collect all normalized values
			this.ScaledSprintMoveAccumulator = 0.0f; // used to collect all normalized values
            this.ValuesByFactorName = new Dictionary<string, EvaluationDebugLogRecord>(); // used to store values for debug output
			this.HasRegularMove = false;
			this.HasSprintMove = false;
            this.HasMeleeMove = false;
            this.Target = target;
		}

		public void AddMoveTypeToPathNode(MoveType moveType, PathNode pathNode)
		{
			this.PathNodes[moveType] = pathNode;
			if (moveType == MoveType.Sprinting)
			{
				this.HasSprintMove = true;
			} else if (moveType == MoveType.Melee)
            {
                this.HasMeleeMove = true;
            }
			else
			{
				this.HasRegularMove = true;
			}
		}

		public float GetHighestAccumulator()
		{
            List<float> values = new List<float>();
            if ((this.HasRegularMove) || (this.HasMeleeMove))
            {
                values.Add(RegularMoveAccumulator);
            }
            if (this.HasSprintMove)
            {
                values.Add(SprintMoveAccumulator);
            }

            if (values.Count == 0)
            {
                return float.MinValue;
            }

            float best = values[0];
            for (int test = 1; test < values.Count; ++test)
            {
                best = Mathf.Max(best, values[test]);
            }
            return best;
		}

		public MoveType GetBestMoveType()
		{
            List<KeyValuePair<MoveType, float>> moveValues = new List<KeyValuePair<MoveType, float>>();

            if ((this.HasRegularMove) || (this.HasMeleeMove))
            {
                moveValues.Add(new KeyValuePair<MoveType, float>(MoveType.Walking, RegularMoveAccumulator));
            }
            if (this.HasSprintMove)
            {
                moveValues.Add(new KeyValuePair<MoveType, float>(MoveType.Sprinting, SprintMoveAccumulator));
            }

            if (moveValues.Count == 0)
            {
                return MoveType.None;
            }

            // sort (ascending) by accumulator value
            moveValues.Sort((x,y) => x.Value.CompareTo(y.Value));
            // flip for descending values
            moveValues.Reverse();

            MoveType bestMove = moveValues[0].Key;

            if (bestMove != MoveType.Walking)
            {
                return bestMove;
            }
			return GetBestRegularMoveType();
		}

		public MoveType GetBestRegularMoveType()
		{
			MoveType[] moveTypesInOrder = new MoveType[]
			{
                MoveType.Melee,
				MoveType.None,
				MoveType.Walking,
				MoveType.Backward,
				MoveType.Jumping,
			};

			for (int mti = 0; mti < moveTypesInOrder.Length; ++mti)
			{
				MoveType mt = moveTypesInOrder[mti];
				if (PathNodes.ContainsKey(mt))
				{
					return mt;
				}
			}
			UnityEngine.Debug.LogError("don't know what move type to return");
			return MoveType.None;
		}
	}

	public class InfluenceMapEvaluator
	{
		InfluenceMapPositionFactor[] positionalFactors;
		InfluenceMapHostileFactor[] hostileFactors;
		InfluenceMapAllyFactor[] allyFactors;

		public PreferHigherExpectedDamageToHostileFactor expectedDamageFactor;

		GraphCoroutine evaluationCoroutine;
		bool evaluationComplete = false;
		AbstractActor unit;

		public Dictionary<WorkspaceKey, int> WorkspaceKeys;
		public List<WorkspaceEvaluationEntry> WorkspaceEvaluationEntries;
		public int firstFreeWorkspaceEvaluationEntryIndex;

		const int WORKSPACE_STEP_SLICE = 16;

		Dictionary<string, long> ProfileSectionStarts;
		Dictionary<string, long> ProfileSectionDurations;

		bool isProfiling;

		public InfluenceMapEvaluator()
		{
			evaluationCoroutine = null;
			positionalFactors = new InfluenceMapPositionFactor[]{
				new PreferLowerMovementFactor(),
				new PreferHigherPositionFactor(),
				new PreferStationaryWhenHostilesInMeleeRangeFactor(),
				new PreferFarthestAwayFromClosestHostilePositionFactor(), // special one that considers all hostiles at once
				new PreferLessSteepPositionFactor(),
				new PreferOutsideCoolDownRangePositionFactor(),
				new PreferLessTargetablePositionFactor(),
				new PreferHigherHeatSinkPositionFactor(),
				new PreferHigherHeatPerTurnPositionFactor(),
				new PreferProximityToTaggedTargetFactor(),
				new PreferLocationsThatGrantGuardPositionFactor(),
				new PreferNotLethalPositionFactor(),
				new PreferHigherDamageReductionPositionFactor(),
				new PreferHigherHigherMeleeToHitPenaltyPositionFactor(),
				new PreferHigherMovementBonusPositionFactor(),
				new PreferLowerStabilityDamageMultiplierPositionFactor(),
				new PreferHigherVisibilityCostPositionFactor(),
				new PreferHigherSensorRangeMultiplierPositionFactor(),
				new PreferLowerSignatureMultiplierPositionFactor(),
				new PreferSurroundingHostileUnitsFactor(),
				new PreferNotSurroundedByHostileUnitsFactor(),
				new PreferEqualizeExposureCountPositionalFactor(),
				new PreferInsideFenceNegativeLogicPositionalFactor(),
				new PreferLowerRangedToHitPenaltyPositionFactor(),
				new PreferHigherRangedDefenseBonusPositionFactor(),
				new PreferInsideExcludedRegionPositionalFactor(),
				new PreferExposedAlonePositionalFactor(),
				new PreferNearExposedAllyPositionalFactor(),
				new PreferFiringSolutionWhenExposedAllyPositionalFactor(),
                new PreferFriendlyECMPositionFactor(),
                new PreferHostileECMPositionFactor(),
                new PreferActiveProbePositionFactor(),
            };

			expectedDamageFactor = new PreferHigherExpectedDamageToHostileFactor();

			hostileFactors = new InfluenceMapHostileFactor[] {
				expectedDamageFactor,
				new PreferAttackFromBehindHostileFactor(),
				new PreferAttackFrom90DegreesToHostileFactor(),
				new PreferNoCloserThanMinDistToHostileFactor(),
				new PreferLowerLOSCountToHostileFactor(),
				new PreferFacingHostileFactor(),
				new PreferWeakerArmorOfHostileFactor(),
				new PreferPresentingStrongerArmorToHostileFactor(),
				new PreferInsideMeleeDistanceToHostileFactor(),
				new PreferBeingBehindBracedHostileFactor(),
				new PreferLowerExpectedDamageFromHostileFactor(),
				new PreferHigherLOSCountToHostileFactor(),
				new PreferLethalDamageToRearArcFromHostileFactor(),
				new AppetitivePreferBehindHostileFactor(),
				new PreferInsideSprintExclusionRadiusHostileFactor(),
				new AppetitivePreferInIdealWeaponRangeHostileFactor(),
				new PreferHigherFirepowerTakenFromHostileFactor(),
                new PreferOptimalDistanceToHostileFactor(),
			};

			allyFactors = new InfluenceMapAllyFactor[] {
				new PreferNoCloserThanPersonalSpaceToAllyFactor(),
				new PreferOptimalDistanceToAllyFactor(),
			};

			WorkspaceKeys = new Dictionary<WorkspaceKey, int>();
			WorkspaceEvaluationEntries = new List<WorkspaceEvaluationEntry>();
			firstFreeWorkspaceEvaluationEntryIndex = 0;

			ProfileSectionStarts = new Dictionary<string, long>();
			ProfileSectionDurations = new Dictionary<string, long>();
		}

		public void InitializeEvaluationForUnit(AbstractActor unit)
		{
			this.unit = unit;
			evaluationComplete = false;

			isProfiling = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_ProfileInfluenceMapCalculations).BoolVal;
			ValidateCoverage();
		}

		public void Reset()
		{
			evaluationCoroutine = null;
		}

		/// <summary>
		/// A debugging function that goes through all of the behavior
		/// variables, then matches them against the influence weights in use.
		/// If any are not being used, report them.
		/// </summary>
		private void ValidateCoverage()
		{
			HashSet<BehaviorVariableName> bvs = new HashSet<BehaviorVariableName>();

			List<WeightedFactor> factors = new List<WeightedFactor>();
			factors.AddRange(positionalFactors);
			factors.AddRange(hostileFactors);
			factors.AddRange(allyFactors);

			for (int i = 0; i < factors.Count; ++i)
			{
				WeightedFactor factor = factors[i];
				UnityEngine.Debug.Assert(!bvs.Contains(factor.GetRegularMoveWeightBVName()));
				bvs.Add(factor.GetRegularMoveWeightBVName());
				UnityEngine.Debug.Assert(!bvs.Contains(factor.GetSprintMoveWeightBVName()));
				bvs.Add(factor.GetSprintMoveWeightBVName());
			}

			foreach (BehaviorVariableName bvName in Enum.GetValues(typeof(BehaviorVariableName)))
			{
				if (!bvs.Contains(bvName))
				{
					string bvNameString = bvName.ToString().ToLower();
					if (bvNameString.EndsWith("weight") && bvNameString.StartsWith("float_"))
					{
						UnityEngine.Debug.LogError("unconnected weight BV: " + bvName);
					}
				}
			}
		}

		/// <summary>
		/// Evaluate at a SINGLE position - contrast with the similar function that does a normalized calculation over all positions
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="rotationIndex"></param>
		/// <returns></returns>
		public float GetEvaluationAtPositionOrientation(Vector3 pos, int rotationIndex, MoveType moveType, PathNode pathNode)
		{
            if (pathNode == null)
            {
                throw new ArgumentException(string.Format("missing pathNode in GetEvaluationAtPositionOrientation"));
            }

            // like the evaluation of multiple locations, minus the normalization.
            // only useful for regular (i.e. non-sprint) moves.

            // for each factor,
            // if the weight for that factor is 0, skip it
            // for each position, evaluate the factor at that position
            // then multiply the value by the weight for that factor
            // add it in to the destination's influence variable value

            float moveValue = 0.0f;

            if ((moveType == MoveType.Walking) && (unit.GainsEntrenchedFromNormalMoves))
            {
                moveValue += unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SureFootingAbilityWalkBoost).FloatVal;
            }

            // positional
            for (int posFactorIndex = 0; posFactorIndex < positionalFactors.Length; ++posFactorIndex)
			{
				InfluenceMapPositionFactor factor = positionalFactors[posFactorIndex];
				if (factor == null)
				{
					throw new IndexOutOfRangeException(string.Format("null factor for pos index {0}", posFactorIndex));
				}

				BehaviorVariableName factorRegularName = factor.GetRegularMoveWeightBVName();
				BehaviorVariableValue factorRegularValue = unit.BehaviorTree.GetBehaviorVariableValue(factorRegularName);
				if (factorRegularValue == null)
				{
					throw new ArgumentException(string.Format("missing behavior variable value for {0}", factorRegularName));
				}

				float factorWeight = factorRegularValue.FloatVal;
				if (factorWeight == 0.0f)
				{
					continue;
				}

				moveValue += factorWeight * factor.EvaluateInfluenceMapFactorAtPosition(unit, pos, rotationIndex, moveType, pathNode);
			}

			// precalc hostile armor data
			for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
			{
				ICombatant enemyUnit = unit.BehaviorTree.enemyUnits[enemyIndex];
				AbstractActor enemyActor = enemyUnit as AbstractActor;
				if (enemyActor != null)
				{
					enemyActor.EvaluateExpectedArmor();
				}
			}
			// precalc my own armor data
			unit.EvaluateExpectedArmor();

			// hostile factors
			for (int hostileFactorIndex = 0; hostileFactorIndex < hostileFactors.Length; ++hostileFactorIndex)
			{
				InfluenceMapHostileFactor factor = hostileFactors[hostileFactorIndex];
				float weight = unit.BehaviorTree.GetBehaviorVariableValue(factor.GetRegularMoveWeightBVName()).FloatVal;
				if (weight == 0.0f)
				{
					continue;
				}

				for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
				{
					ICombatant enemyUnit = unit.BehaviorTree.enemyUnits[enemyIndex];
                    if (unit.BehaviorTree.IsTargetIgnored(enemyUnit))
                    {
                        continue;
                    }

                    float hostileMultiplier = FactorUtil.HostileFactor(unit, enemyUnit);

					moveValue += weight * hostileMultiplier * factor.EvaluateInfluenceMapFactorAtPositionWithHostile(unit, pos, rotationIndex, moveType, enemyUnit);
				}
			}

			// ally
			for (int allyFactorIndex = 0; allyFactorIndex < allyFactors.Length; ++allyFactorIndex)
			{
				InfluenceMapAllyFactor factor = allyFactors[allyFactorIndex];
				float weight = unit.BehaviorTree.GetBehaviorVariableValue(factor.GetRegularMoveWeightBVName()).FloatVal;
				if (weight == 0.0f)
				{
					continue;
				}

				for (int allyIndex = 0; allyIndex < unit.BehaviorTree.GetAllyUnits().Count; ++allyIndex)
				{
					AbstractActor allyUnit = unit.BehaviorTree.GetAllyUnits()[allyIndex];

					moveValue += weight * factor.EvaluateInfluenceMapFactorAtPositionWithAlly(unit, pos, rotationIndex, allyUnit);
				}
			}

			return moveValue;
		}

		public bool RunEvaluationForSeconds(float seconds)
		{
			float startTime = Time.realtimeSinceStartup;
			if (evaluationCoroutine == null)
			{
				evaluationCoroutine = new GraphCoroutine(this.IncrementalEvaluate());
			}

			while (Time.realtimeSinceStartup - startTime <= seconds)
			{
				evaluationCoroutine.Update();
				if (evaluationComplete)
				{
					evaluationCoroutine = null;
					break;
				}
			}

			return evaluationComplete;
		}

		public void ExportInfluenceMapToCSV()
		{
			string dirName = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_InfluenceMapCalculationLogDirectory).StringVal;
			dirName = System.IO.Path.Combine(Application.persistentDataPath, dirName);
			try
			{
				System.IO.Directory.CreateDirectory(dirName);
			}
			catch (System.IO.IOException ioException)
			{
				UnityEngine.Debug.LogError(string.Format("Failed to create directory {0} : exception {1}", dirName, ioException));
				return;
			}

			DateTime timeNow = DateTime.Now;

			string filename = string.Format("influence_map_calculations_{0:D4}_{1:D2}_{2:D2}_{3:D2}.{4:D2}.{5:D2}_r{6:D2}_p{7:D2}.csv",
				timeNow.Year, timeNow.Month, timeNow.Day,
				timeNow.Hour, timeNow.Minute, timeNow.Second,
                unit.Combat.TurnDirector.CurrentRound,
                unit.Combat.TurnDirector.CurrentPhase);

			string fullFN = System.IO.Path.Combine(dirName, filename);

			Mech mech = unit as Mech;

			string output = "";
			using (System.IO.StringWriter stringWriter = new System.IO.StringWriter())
			{
				stringWriter.WriteLine("unit name, " + unit.DisplayName);
                stringWriter.WriteLine("unit GUID, " + unit.GUID);
                Pilot pilot = unit.GetPilot();
				stringWriter.WriteLine("pilot name, " + ((pilot == null) ? "[none]" : pilot.Name));
                stringWriter.WriteLine("pilot callsign, " + ((pilot == null) ? "[none]" : pilot.Callsign));
                stringWriter.WriteLine("unit mood, " + unit.BehaviorTree.mood.ToString());
				stringWriter.WriteLine("unit dynamic lance role, " + unit.DynamicUnitRole.ToString());
				float heat = (mech == null) ? 0.0f : mech.CurrentHeat;
				stringWriter.WriteLine("unit current heat, " + heat);
				stringWriter.WriteLine(string.Format("starting cartesian pos, {0}, {1}, {2}", unit.CurrentPosition.x, unit.CurrentPosition.y, unit.CurrentPosition.z));
				Vector2 startAxialPos = unit.Combat.HexGrid.CartesianToHexAxial(unit.CurrentPosition);
				stringWriter.WriteLine(string.Format("starting axial pos, {0}, {1}", Mathf.RoundToInt(startAxialPos.x), Mathf.RoundToInt(startAxialPos.y)));
				stringWriter.WriteLine("starting heading, " + unit.CurrentRotation.eulerAngles.y);
				int start8Angle = PathingUtil.FloatAngleTo8Angle(unit.CurrentRotation.eulerAngles.y);
				stringWriter.WriteLine("starting 8angle heading, " + start8Angle);
				stringWriter.WriteLine("sprint hysteresis value, " + unit.BehaviorTree.GetSprintHysteresisLevel());
				stringWriter.WriteLine("");

				stringWriter.WriteLine("hostile units sorted by threat");
				List<ICombatant> hostileUnitList = new List<ICombatant>();
				for (int otherUnitIndex = 0; otherUnitIndex < unit.Combat.AllActors.Count; ++otherUnitIndex)
				{
					AbstractActor otherUnit = unit.Combat.AllActors[otherUnitIndex];

					if ((!otherUnit.IsDead) && (otherUnit.team.IsEnemy(unit.team)) && (!unit.BehaviorTree.IsTargetIgnored(otherUnit)))
					{
						hostileUnitList.Add(otherUnit);
					}
				}

                if ((unit.BehaviorTree != null) && (unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_FilterHostileInfluenceMapsToPriorityTargets).BoolVal))
                {
                    List<ICombatant> priorityTargets = unit.BehaviorTree.GetPriorityTargets();
                    if ((priorityTargets != null) && (priorityTargets.Count > 0))
                    {
                        hostileUnitList = priorityTargets;
                    }
                }

                AIThreatUtil.SortHostileUnitsByThreat(unit, hostileUnitList);
				for (int otherUnitIndex = 0; otherUnitIndex < hostileUnitList.Count; ++otherUnitIndex)
				{
					ICombatant otherUnit = hostileUnitList[otherUnitIndex];
					AbstractActor otherActor = otherUnit as AbstractActor;
					Pilot otherPilot = otherUnit.GetPilot();
					string otherPilotName = (otherPilot == null) ? " " : otherPilot.pilotDef.Description.DisplayName; // TODO clean this up, so gross!
					string nickName = (otherActor == null) ? " " : otherActor.Nickname;
					string variantName = (otherActor == null) ? " " : otherActor.VariantName;
					stringWriter.WriteLine(string.Format("{0}, {1}, {2}, {3}, {4},", otherUnit.DisplayName, nickName, variantName, otherPilotName, AIThreatUtil.GetThreatRatio(unit, otherActor)));
				}

				stringWriter.WriteLine("");

				stringWriter.Write("Best Move Value, Best Move Type, Regular Move Value, Raw Sprint Value, Biased Sprint Value, available move type(s), cartesian X, cartesian Z, axial X, axial Y, float angle, 8-angle, stationary?, ");

				List<string> factorNames = new List<string>();

				bool includeZeroWeights = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_InfluenceMapCalculationLogIncludeZeroWeightedFactors).BoolVal;

				List<WeightedFactor> allFactors = new List<WeightedFactor>();
				allFactors.AddRange(positionalFactors);
				allFactors.AddRange(hostileFactors);
				allFactors.AddRange(allyFactors);

				for (int factorIndex = 0; factorIndex < allFactors.Count; ++factorIndex)
				{
					WeightedFactor factor = allFactors[factorIndex];
					BehaviorVariableName weightBVName = factor.GetRegularMoveWeightBVName();
					float weight = unit.BehaviorTree.GetBehaviorVariableValue(weightBVName).FloatVal;
					float sprintWeight = unit.BehaviorTree.GetBehaviorVariableValue(factor.GetSprintMoveWeightBVName()).FloatVal;

					if ((weight != 0) || (sprintWeight != 0) || (includeZeroWeights))
					{
						factorNames.Add(weightBVName.ToString());
					}
				}

				foreach (string factorName in factorNames)
				{
					stringWriter.Write(string.Format("Raw Unnormalized evaluation({0}), Base evaluation({0}), Regular move weight({0}), Weighted regular evaluation({0}), Sprint move weight({0}), Weighted sprint evaluation({0}),  ,", factorName));
				}
				stringWriter.WriteLine("");

				for (int i = 0; i < firstFreeWorkspaceEvaluationEntryIndex; ++i)
				{
					WorkspaceEvaluationEntry e = WorkspaceEvaluationEntries[i];

					stringWriter.Write(string.Format("{0}, ", e.GetHighestAccumulator()));
					stringWriter.Write(string.Format("{0}, ", e.GetBestMoveType()));
					if (e.HasRegularMove)
						stringWriter.Write(string.Format("{0}, ", e.RegularMoveAccumulator));
					else
						stringWriter.Write(", ");

					if (e.HasSprintMove)
					{
						stringWriter.Write(string.Format("{0}, ", e.SprintMoveAccumulator));
						stringWriter.Write(string.Format("{0}, ", e.ScaledSprintMoveAccumulator));
					}
					else
						stringWriter.Write(", , ");

					string moves = "";
					if (e.PathNodes.ContainsKey(MoveType.None))
					{
						moves += "None ";
					}
					if (e.PathNodes.ContainsKey(MoveType.Walking))
					{
						moves += "Walk ";
					}
					if (e.PathNodes.ContainsKey(MoveType.Backward))
					{
						moves += "Back ";
					}
					if (e.PathNodes.ContainsKey(MoveType.Jumping))
					{
						moves += "Jump ";
					}
					if (e.PathNodes.ContainsKey(MoveType.Sprinting))
					{
						moves += "Sprint ";
					}
                    if (e.PathNodes.ContainsKey(MoveType.Melee))
                    {
                        moves += "Melee ";
                    }
					stringWriter.Write(string.Format("{0}, ", moves));

					stringWriter.Write(string.Format("{0}, {1}, ", e.Position.x, e.Position.z));

					Vector2 axialPos = unit.Combat.HexGrid.CartesianToHexAxial(e.Position);
					stringWriter.Write(string.Format("{0}, {1}, ", Mathf.RoundToInt(axialPos.x), Mathf.RoundToInt(axialPos.y)));

					int this8Angle = PathingUtil.FloatAngleTo8Angle(e.Angle);
					stringWriter.Write(string.Format("{0}, {1}, ", e.Angle, this8Angle));

					bool isStationary = (axialPos == startAxialPos) && (this8Angle == start8Angle);
					stringWriter.Write(string.Format("{0}, ", (isStationary ? "yes" : "no")));

					foreach (string factorName in factorNames)
					{
						if (e.ValuesByFactorName.ContainsKey(factorName))
						{
							EvaluationDebugLogRecord record = e.ValuesByFactorName[factorName];

							stringWriter.Write(string.Format("{0}, {1}, {2}, {3}, {4}, {5}, , ", record.RawValue, record.NormValue, record.RegularWeight, record.RegularValue, record.SprintWeight, record.SprintValue));
						}
						else
						{
							stringWriter.Write("n/a, n/a, n/a, n/a, n/a, n/a, , ");
						}
					}

					stringWriter.WriteLine("");
				}
				output = stringWriter.ToString();
			}

			if (unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_LogInfluenceMapCalculations).BoolVal)
			{
				// write output to the file
				using (System.IO.StreamWriter file = new System.IO.StreamWriter(fullFN))
				{
					file.Write(output);
				}
			}
			else
			{
				// store output in our InfLogCache
				unit.Combat.AILogCache.AddLogData(fullFN, output);
			}
		}

		#region Evaluation Coroutines

		IEnumerable<Instruction> IncrementalEvaluate()
		{
			ProfileFrameBegin();
			ProfileBegin(ProfileSection.AllInfluenceMaps);
			// for each factor,
			// if the weight for that factor is 0, skip it
			// for each position, evaluate the factor at that position, store in our array
			// scale that array to lie between -1 and 1
			// then multiply the array by the weight for that factor
			// add it in to the destination's influence variable value

			yield return ControlFlow.Call(Eval_Initialize());

			yield return ControlFlow.Call(Eval_PositionalFactors());

			yield return ControlFlow.Call(Eval_HostileFactors());

			yield return ControlFlow.Call(Eval_AllyFactors());

			yield return ControlFlow.Call(Apply_SprintScaling());

			expectedDamageFactor.LogEvaluation();

			evaluationComplete = true;
			ProfileEnd(ProfileSection.AllInfluenceMaps);
			ProfileFrameEnd();
			yield return null;
		}

		public void ResetWorkspace()
		{
			firstFreeWorkspaceEvaluationEntryIndex = 0;
			WorkspaceKeys.Clear();
		}

		public void WorkspacePushPathNodeAngle(PathNode pathNode, float angleDegrees, MoveType moveType, AbstractActor target)
		{
			Vector2 hexAxial = unit.Combat.HexGrid.HexAxialRound(unit.Combat.HexGrid.CartesianToHexAxial(pathNode.Position));

			WorkspaceKey key = new WorkspaceKey(Mathf.RoundToInt(hexAxial.x), Mathf.RoundToInt(hexAxial.y), Mathf.RoundToInt(angleDegrees), target);

			int entryIndex;
			if (!WorkspaceKeys.ContainsKey(key))
			{
				// grab a new WorkspaceEntry and insert it at this location
				entryIndex = GetUnusedWorkspaceEvaluationEntryIndex();
				WorkspaceKeys[key] = entryIndex;
				WorkspaceEvaluationEntry entry = WorkspaceEvaluationEntries[entryIndex];
				entry.Reset(pathNode.Position, angleDegrees, target);
				entry.AddMoveTypeToPathNode(moveType, pathNode);
			}
			else
			{
				entryIndex = WorkspaceKeys[key];
				UnityEngine.Debug.Assert(entryIndex < firstFreeWorkspaceEvaluationEntryIndex);
				WorkspaceEvaluationEntry entry = WorkspaceEvaluationEntries[entryIndex];
				entry.AddMoveTypeToPathNode(moveType, pathNode);
			}
		}

		int GetUnusedWorkspaceEvaluationEntryIndex()
		{
			int index = firstFreeWorkspaceEvaluationEntryIndex;
			if (WorkspaceEvaluationEntries.Count == firstFreeWorkspaceEvaluationEntryIndex)
			{
				WorkspaceEvaluationEntries.Add(new WorkspaceEvaluationEntry());
			}

			firstFreeWorkspaceEvaluationEntryIndex++;
			return index;
		}

		IEnumerable<Instruction> Eval_Initialize()
		{
			ResetWorkspace();
			ProfileBegin(ProfileSection.Locations);
			for (int locationIndex = 0; locationIndex < unit.BehaviorTree.movementCandidateLocations.Count; ++locationIndex)
			{
				if (!IsMovementCandidateLocationReachable(unit, unit.BehaviorTree.movementCandidateLocations[locationIndex]))
				{
					continue;
				}
                MoveDestination dest = unit.BehaviorTree.movementCandidateLocations[locationIndex];
                PathNode basePathNode = dest.PathNode;
				float baseAngle = PathingUtil.FloatAngleFrom8Angle(basePathNode.Angle);
				float moveDistance = 0.0f;
                AbstractActor target = null;
                MeleeMoveDestination meleeDest = dest as MeleeMoveDestination;
                if (meleeDest != null)
                {
                    target = meleeDest.Target;
                }

				switch (unit.BehaviorTree.movementCandidateLocations[locationIndex].MoveType)
				{
					case MoveType.Walking:
						moveDistance = unit.MaxWalkDistance;
						break;
					case MoveType.Sprinting:
						moveDistance = unit.MaxSprintDistance;
						break;
					case MoveType.Backward:
						moveDistance = unit.MaxBackwardDistance;
						break;
					case MoveType.Jumping:
                    {
                        Mech mech = unit as Mech;
                        if (mech != null)
                        {
                            moveDistance = mech.JumpDistance;
                        }
                        break;
                    }
                    case MoveType.Melee:
                    {
                        Mech mech = unit as Mech;
                        if (mech != null)
                        {
                            moveDistance = mech.MaxMeleeEngageRangeDistance;
                        }
                        break;
                    }
                }

				// we could look left or right, so clamp to 180 degrees
				float postMoveRotationAvailable = Mathf.Min(unit.Pathing.GetAngleAvailable(moveDistance - basePathNode.CostToThisNode), 180.0f);
				float angularIncrement = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_AngularSelectionResolution).FloatVal;

                if (target == null)
                {
                    for (float deltaAngle = 0; deltaAngle < postMoveRotationAvailable; deltaAngle += angularIncrement)
                    {
                        // look right
                        WorkspacePushPathNodeAngle(basePathNode, baseAngle + deltaAngle, unit.BehaviorTree.movementCandidateLocations[locationIndex].MoveType, null);
                        if (deltaAngle > 0)
                        {
                            // look left
                            WorkspacePushPathNodeAngle(basePathNode, baseAngle - deltaAngle, unit.BehaviorTree.movementCandidateLocations[locationIndex].MoveType, null);
                        }
                    }
                }
                else
                {
                    WorkspacePushPathNodeAngle(basePathNode, baseAngle, unit.BehaviorTree.movementCandidateLocations[locationIndex].MoveType, target);
                }
            }
			ProfileEnd(ProfileSection.Locations);
			yield return null;
		}

		// evaluate factors using positional factors
		IEnumerable<Instruction> Eval_PositionalFactors()
		{
			ProfileBegin(ProfileSection.PositionalFactors);
			int workspaceIndex;

			for (workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
			{
				WorkspaceEvaluationEntries[workspaceIndex].RegularMoveAccumulator = 0.0f;
				WorkspaceEvaluationEntries[workspaceIndex].SprintMoveAccumulator = 0.0f;
            }

            for (int posFactorIndex = 0; posFactorIndex < positionalFactors.Length; ++posFactorIndex)
			{
				InfluenceMapPositionFactor factor = positionalFactors[posFactorIndex];
				ProfileBegin(ProfileSection.PositionalFactors, factor.Name);

				float regularWeight = unit.BehaviorTree.GetBehaviorVariableValue(factor.GetRegularMoveWeightBVName()).FloatVal;
				float sprintWeight = unit.BehaviorTree.GetBehaviorVariableValue(factor.GetSprintMoveWeightBVName()).FloatVal;

                if ((regularWeight == 0.0f) && (sprintWeight == 0.0f))
				{
					ProfileEnd(ProfileSection.PositionalFactors, factor.Name);
					continue;
				}

				float minValue = float.MaxValue;
				float maxValue = float.MinValue;

				factor.InitEvaluationForPhaseForUnit(unit);

				for (workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
				{
					WorkspaceEvaluationEntry evalEntry = WorkspaceEvaluationEntries[workspaceIndex];
					MoveType moveType = evalEntry.HasSprintMove ? MoveType.Sprinting : MoveType.Walking;

					PathNode node = null;
					if (evalEntry.PathNodes.ContainsKey(moveType))
					{
						node = evalEntry.PathNodes[moveType];
					}
					else
					{
						if ((moveType == MoveType.Walking) && (evalEntry.PathNodes.ContainsKey(MoveType.Backward)))
						{
							node = evalEntry.PathNodes[MoveType.Backward];
						}
						else
						{
							// pick something from the available movetypes
							foreach (MoveType mt in evalEntry.PathNodes.Keys)
							{
								node = evalEntry.PathNodes[mt];
								break;
							}
						}
					}

					float v = factor.EvaluateInfluenceMapFactorAtPosition(unit, evalEntry.Position, evalEntry.Angle, moveType, node);
					evalEntry.FactorValue = v;
					minValue = Mathf.Min(v, minValue);
					maxValue = Mathf.Max(v, maxValue);

					if (workspaceIndex % WORKSPACE_STEP_SLICE == 0)
					{
						yield return null;
					}
				}

				// if this entire factor is uniform, don't bother doing any more with it, it won't help us choose.
				if (minValue >= maxValue)
				{
					ProfileEnd(ProfileSection.PositionalFactors, factor.Name);
					yield return null;
					continue;
				}

				// normalize the values and add them into the movetype-specific accumulators
				for (workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
				{
					WorkspaceEvaluationEntry entry = WorkspaceEvaluationEntries[workspaceIndex];
					float calcValue = entry.FactorValue;
					float normValue = (calcValue - minValue) / (maxValue - minValue);

					UnityEngine.Debug.Assert((normValue <= 1.0f) && (normValue >= 0.0f), "normalized influence values should be between 0 and 1");

					float regularValue = normValue * regularWeight;
					float sprintValue = normValue * sprintWeight;

					entry.ValuesByFactorName[factor.GetRegularMoveWeightBVName().ToString()] = new EvaluationDebugLogRecord(calcValue, normValue, regularValue, regularWeight, sprintValue, sprintWeight);

					WorkspaceEvaluationEntries[workspaceIndex].RegularMoveAccumulator += regularValue;
					WorkspaceEvaluationEntries[workspaceIndex].SprintMoveAccumulator += sprintValue;
                }
                yield return null;
				ProfileEnd(ProfileSection.PositionalFactors, factor.Name);
			}
			ProfileEnd(ProfileSection.PositionalFactors);
		}

		class CombatantDistanceComparer : IComparer<ICombatant>
		{
			public AbstractActor actingUnit;

			public CombatantDistanceComparer(AbstractActor unit)
			{
				this.actingUnit = unit;
			}

			public int Compare(ICombatant x, ICombatant y)
			{
				float xDistSqr = (x.CurrentPosition - actingUnit.CurrentPosition).sqrMagnitude;
				float yDistSqr = (y.CurrentPosition - actingUnit.CurrentPosition).sqrMagnitude;
				return xDistSqr.CompareTo(yDistSqr);
			}
		}

		List<ICombatant> getNClosestCombatants(List<ICombatant> combatants, int count)
		{
			if (combatants.Count > count)
			{
				CombatantDistanceComparer comparer = new CombatantDistanceComparer(unit);
				combatants.Sort(comparer);
				combatants.RemoveRange(count, combatants.Count - count);
			}
			return combatants;
		}

		/// <summary>
		/// evaluate hostile factors
		/// ForAll hostile factors
		///   ForAll locations
		///     ForAll hostiles
		///       evaluate factor vs hostile unit at this location
		///
		/// </summary>
		/// <returns></returns>
		IEnumerable<Instruction> Eval_HostileFactors()
		{
			ProfileBegin(ProfileSection.HostileFactors);
			// precalc hostile armor data
			for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
			{
				ICombatant enemyUnit = unit.BehaviorTree.enemyUnits[enemyIndex];
				AbstractActor enemyActor = enemyUnit as AbstractActor;
				if (enemyActor != null)
				{
					enemyActor.EvaluateExpectedArmor();
				}
			}
			// precalc my own armor data
			unit.EvaluateExpectedArmor();

			yield return null;

			int hostileCount = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_HostileInfluenceCount).IntVal;
			List<ICombatant> hostiles = getNClosestCombatants(unit.BehaviorTree.enemyUnits, hostileCount);

			AIUtil.LogAI(string.Format("evaluating vs {0} hostiles", hostiles.Count));

			int workspaceIndex;

			// hostile factors
			for (int hostileFactorIndex = 0; hostileFactorIndex < hostileFactors.Length; ++hostileFactorIndex)
			{
				InfluenceMapHostileFactor factor = hostileFactors[hostileFactorIndex];
				ProfileBegin(ProfileSection.HostileFactors, factor.Name);
				UnityEngine.Debug.Log("evaluating " + factor.Name);
				bool specialLogging = false;
				if (factor.Name == "prefer lower damage from hostiles")
				{
					specialLogging = true;
				}
				BehaviorVariableName regularMoveWeightName = factor.GetRegularMoveWeightBVName();
				BehaviorVariableName sprintMoveWeightName = factor.GetSprintMoveWeightBVName();

                float regularMoveWeight = unit.BehaviorTree.GetBehaviorVariableValue(regularMoveWeightName).FloatVal;
				float sprintMoveWeight = unit.BehaviorTree.GetBehaviorVariableValue(sprintMoveWeightName).FloatVal;

				if ((regularMoveWeight == 0.0f) && (sprintMoveWeight == 0.0f))
				{
					ProfileEnd(ProfileSection.HostileFactors, factor.Name);
					continue;
				}

				float minValue = float.MaxValue;
				float maxValue = float.MinValue;

				factor.InitEvaluationForPhaseForUnit(unit);

				for (workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
				{
					WorkspaceEvaluationEntry evalEntry = WorkspaceEvaluationEntries[workspaceIndex];

					evalEntry.FactorValue = 0.0f;

					for (int enemyIndex = 0; enemyIndex < hostiles.Count; ++enemyIndex)
					{
						ICombatant enemyUnit = hostiles[enemyIndex];

						MoveType moveType = evalEntry.HasSprintMove ? MoveType.Sprinting : MoveType.Walking;
						float value = factor.EvaluateInfluenceMapFactorAtPositionWithHostile(unit, evalEntry.Position, evalEntry.Angle, moveType, enemyUnit);

						evalEntry.FactorValue += value;
						minValue = Mathf.Min(minValue, evalEntry.FactorValue);
						maxValue = Mathf.Max(maxValue, evalEntry.FactorValue);
					}

					if (workspaceIndex % WORKSPACE_STEP_SLICE == 0)
					{
						yield return null;
					}
				}

				if (specialLogging)
				{
					UnityEngine.Debug.Log("minVal: " + minValue);
					UnityEngine.Debug.Log("maxVal: " + maxValue);
				}

				// if this factor is uniform across all locations, it doesn't contribute, so skip further processing.
				if (minValue >= maxValue)
				{
					yield return null;
					ProfileEnd(ProfileSection.HostileFactors, factor.Name);
					continue;
				}

				for (workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
				{
					float calcValue = WorkspaceEvaluationEntries[workspaceIndex].FactorValue;
					float normValue = (calcValue - minValue) / (maxValue - minValue);
					float regularMoveValue = normValue * regularMoveWeight;
					float sprintMoveValue = normValue * sprintMoveWeight;
					WorkspaceEvaluationEntries[workspaceIndex].RegularMoveAccumulator += regularMoveValue;
					WorkspaceEvaluationEntries[workspaceIndex].SprintMoveAccumulator += sprintMoveValue;
					WorkspaceEvaluationEntries[workspaceIndex].ValuesByFactorName[factor.GetRegularMoveWeightBVName().ToString()] = new EvaluationDebugLogRecord(calcValue, normValue, regularMoveValue, regularMoveWeight, sprintMoveValue, sprintMoveWeight);
				}
				yield return null;
				ProfileEnd(ProfileSection.HostileFactors, factor.Name);
			}
			ProfileEnd(ProfileSection.HostileFactors);
		}

		//
		/// <summary>
		/// evaluate ally factors
		///
		/// ForAll ally factors
		///   Forall locations
		///     ForAll ally units
		///       evaluate factor with ally unit at this location
		///
		/// </summary>
		/// <returns></returns>
		IEnumerable<Instruction> Eval_AllyFactors()
		{
			ProfileBegin(ProfileSection.AllyFactors);
			int allyCount = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_AllyInfluenceCount).IntVal;
			List<ICombatant> allies = getNClosestCombatants(unit.BehaviorTree.GetAllyUnits().ConvertAll<ICombatant>(X => X as ICombatant), allyCount);

			AIUtil.LogAI(string.Format("evaluating vs {0} allies", allies.Count));

			for (int allyFactorIndex = 0; allyFactorIndex < allyFactors.Length; ++allyFactorIndex)
			{
				InfluenceMapAllyFactor factor = allyFactors[allyFactorIndex];
				AIUtil.LogAI("evaluating " + factor.Name);
				BehaviorVariableName regularWeightName = factor.GetRegularMoveWeightBVName();
				BehaviorVariableName sprintWeightName = factor.GetSprintMoveWeightBVName();

				float regularMoveWeight = unit.BehaviorTree.GetBehaviorVariableValue(regularWeightName).FloatVal;
				float sprintMoveWeight = unit.BehaviorTree.GetBehaviorVariableValue(sprintWeightName).FloatVal;

				if ((regularMoveWeight == 0.0f) && (sprintMoveWeight == 0.0f))
				{
					continue;
				}

				float minValue = float.MaxValue;
				float maxValue = float.MinValue;

				factor.InitEvaluationForPhaseForUnit(unit);

				for (int workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
				{
					WorkspaceEvaluationEntry evalEntry = WorkspaceEvaluationEntries[workspaceIndex];

					WorkspaceEvaluationEntries[workspaceIndex].FactorValue = 0.0f;

					for (int allyIndex = 0; allyIndex < allies.Count; ++allyIndex)
					{
						ICombatant allyUnit = allies[allyIndex];

						float value = factor.EvaluateInfluenceMapFactorAtPositionWithAlly(unit, evalEntry.Position, evalEntry.Angle, allyUnit);
						WorkspaceEvaluationEntries[workspaceIndex].FactorValue += value;
						minValue = Mathf.Min(minValue, WorkspaceEvaluationEntries[workspaceIndex].FactorValue);
						maxValue = Mathf.Max(maxValue, WorkspaceEvaluationEntries[workspaceIndex].FactorValue);
					}

					if (workspaceIndex % WORKSPACE_STEP_SLICE == 0)
					{
						yield return null;
					}
				}

				// if this factor is uniform across all locations, it doesn't contribute, so skip further processing.
				if (minValue >= maxValue)
				{
					yield return null;
					continue;
				}

				for (int workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
				{
					float calcValue = WorkspaceEvaluationEntries[workspaceIndex].FactorValue;
					float normValue = (calcValue - minValue) / (maxValue - minValue);
					float regularMoveValue = normValue * regularMoveWeight;
					float sprintMoveValue = normValue * sprintMoveWeight;
					WorkspaceEvaluationEntries[workspaceIndex].RegularMoveAccumulator += regularMoveValue;
					WorkspaceEvaluationEntries[workspaceIndex].SprintMoveAccumulator += sprintMoveValue;
					WorkspaceEvaluationEntries[workspaceIndex].ValuesByFactorName[factor.GetRegularMoveWeightBVName().ToString()] = new EvaluationDebugLogRecord(calcValue, normValue, regularMoveValue, regularMoveWeight, sprintMoveValue, sprintMoveWeight);
				}
				yield return null;
			}
			ProfileEnd(ProfileSection.AllyFactors);
		}

		//
		/// <summary>
		/// scale sprint accumulator
		///
		/// Forall locations
		///   multiply sprint accumulator and shift, store in scaledSprintAccumulator
		///
		/// </summary>
		/// <returns></returns>
		///
		IEnumerable<Instruction> Apply_SprintScaling()
		{
			float sprintBiasMult = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SprintWeightBiasMultiplicative).FloatVal;
			float sprintBiasAdd = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SprintWeightBiasAdditive).FloatVal;
			float sprintHysteresisLevel = unit.BehaviorTree.GetSprintHysteresisLevel();

			if (unit.BehaviorTree.HasPriorityTargets() && unit.BehaviorTree.IsOutsideProximityTargetDistance())
			{
				sprintHysteresisLevel = 1.0f;
			}

			for (int workspaceIndex = 0; workspaceIndex < firstFreeWorkspaceEvaluationEntryIndex; ++workspaceIndex)
			{
				WorkspaceEvaluationEntry evalEntry = WorkspaceEvaluationEntries[workspaceIndex];
				evalEntry.ScaledSprintMoveAccumulator = (evalEntry.SprintMoveAccumulator * sprintBiasMult + sprintBiasAdd) * sprintHysteresisLevel;

				if (workspaceIndex % WORKSPACE_STEP_SLICE == 0)
				{
					yield return null;
				}
			}
		}
		#endregion // Evaluation Coroutines


		/// <summary>
		/// checks to see if a movement candidate is reachable
		/// </summary>
		/// <returns>The unit that is moving.</returns>
		/// <param name="unit">Unit.</param>
		/// <param name="moveDestination">location to consider</param>
		public bool IsMovementCandidateLocationReachable(AbstractActor unit, MoveDestination moveDestination)
		{
			if (!((moveDestination.MoveType == MoveType.Walking) ||
				(moveDestination.MoveType == MoveType.Sprinting) ||
				(moveDestination.MoveType == MoveType.Backward)))
			{
				// for other movetypes, it's probably fine.
				return true;
			}

			Vector3 candidateLocation = moveDestination.PathNode.Position;
			int candidate8angle = moveDestination.PathNode.Angle;
			float candidateFloatAngle = PathingUtil.FloatAngleFrom8Angle(candidate8angle);
			Quaternion candidateRotation = Quaternion.Euler(0, candidateFloatAngle, 0);
			Vector3 rotatedForward = candidateRotation * Vector3.forward;
			unit.Pathing.UpdateAIPath(candidateLocation, candidateLocation + rotatedForward, moveDestination.MoveType);

			// now jam the result destination's altitude back into our initial requested position
			candidateLocation.y = unit.Pathing.ResultDestination.y;

			// Did we get a path to the right place?
			Vector2 requestedAxialCoords = unit.Combat.HexGrid.CartesianToHexAxial(candidateLocation);
			Vector2 resultAxialCoords = unit.Combat.HexGrid.CartesianToHexAxial(unit.Pathing.ResultDestination);

			int result8angle = PathingUtil.FloatAngleTo8Angle(unit.Pathing.ResultAngle);

			return ((resultAxialCoords == requestedAxialCoords) && (result8angle == candidate8angle));
		}

#region Profiling

		void ProfileFrameBegin()
		{
			if (!isProfiling)
			{
				return;
			}
			ProfileSectionStarts.Clear();
		}

		void ProfileFrameEnd()
		{
			if (!isProfiling)
			{
				return;
			}
			// TODO(dave) write this instead to a file
			UnityEngine.Debug.LogError("---");
			foreach (string sectionName in ProfileSectionDurations.Keys)
			{
				long duration = ProfileSectionDurations[sectionName];
				UnityEngine.Debug.LogError(string.Format("section: {0} dur: {1}", sectionName, duration / (float)Stopwatch.Frequency));
			}
		}

		void ProfileBegin(ProfileSection section)
		{
			if (!isProfiling)
			{
				return;
			}
			ProfileSectionStarts[section.ToString()] = Stopwatch.GetTimestamp();
		}

		void ProfileEnd(ProfileSection section)
		{
			if (!isProfiling)
			{
				return;
			}
			ProfileSectionDurations[section.ToString()] = Stopwatch.GetTimestamp() - ProfileSectionStarts[section.ToString()];
		}

		void ProfileBegin(ProfileSection section, string subsection)
		{
			if (!isProfiling)
			{
				return;
			}
			string label = string.Format("{0}:{1}", section, subsection);
			ProfileSectionStarts[label] = Stopwatch.GetTimestamp();
		}

		void ProfileEnd(ProfileSection section, string subsection)
		{
			if (!isProfiling)
			{
				return;
			}
			string label = string.Format("{0}:{1}", section, subsection);
			ProfileSectionDurations[label] = Stopwatch.GetTimestamp() - ProfileSectionStarts[label];
		}
		#endregion // Profiling
	}
}

