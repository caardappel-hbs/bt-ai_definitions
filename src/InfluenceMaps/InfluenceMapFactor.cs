using System;
using System.Collections.Generic;

using UnityEngine;

namespace BattleTech
{
	public class FactorUtil
	{
		public static float HostileFactor(AbstractActor unit, ICombatant hostileUnit)
		{
			int index = unit.BehaviorTree.enemyUnits.IndexOf(hostileUnit);

			switch (index)
			{
				case 0:
					return 1.0f;
				case 1:
					return 0.5f;
				default:
					return 0.25f;
			}
		}

		public static bool IsStationaryForActor(Vector3 position, float floatAngle, AbstractActor unit)
		{
			Vector2 positionAxial = unit.Combat.HexGrid.CartesianToHexAxial(position);
			Vector2 currentPositionAxial = unit.Combat.HexGrid.CartesianToHexAxial(unit.CurrentPosition);

			int target8Angle = PathingUtil.FloatAngleTo8Angle(floatAngle);
			int current8Angle = PathingUtil.FloatAngleTo8Angle(unit.CurrentRotation.eulerAngles.y);

			return ((target8Angle == current8Angle) && (positionAxial == currentPositionAxial));
		}

        public static bool DoesActorHaveECM(AbstractActor actor)
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

                    if (effectData.targetingData.auraEffectType == AuraEffectType.ECM_GENERAL || effectData.targetingData.auraEffectType == AuraEffectType.ECM_GHOST)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool DoesECMAffectDestination(AbstractActor actor, AbstractActor unit, Vector3 position)
        {
            if (DoesActorHaveECM(actor))
            {
                var effects = unit.AuraCache.PreviewAurasFromActorAffectingMe(actor, unit, position);

                if (ContainAuraEffectTypes(effects, AuraEffectType.ECM_GHOST, AuraEffectType.ECM_GENERAL, AuraEffectType.ECM_COUNTER))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainAuraEffectTypes(List<EffectData> effects, params AuraEffectType[] auras)
        {
            var auraHashSet = new HashSet<AuraEffectType>(auras);

            for (int effectIndex = 0; effectIndex < effects.Count; ++effectIndex)
            {
                EffectData effectData = effects[effectIndex];

                if (auraHashSet.Contains(effectData.targetingData.auraEffectType))
                {
                    return true;
                }
            }

            return false;
        }

        public static int ChangesInECMAuraState(AbstractActor enemy, AbstractActor unit, Vector3 targetPosition, params AuraEffectType[] auras)
        {
            if (DoesActorHaveECM(enemy))
            {
                var enemies = unit.Combat.GetAllEnemiesOf(unit);


                var currentEffects = unit.AuraCache.PreviewAurasFromActorAffectingMe(enemy, unit, unit.CurrentPosition);
                var targetPosEffects = unit.AuraCache.PreviewAurasFromActorAffectingMe(enemy, unit, targetPosition);

                if (ContainAuraEffectTypes(currentEffects, auras) && !ContainAuraEffectTypes(targetPosEffects, auras))
                {
                    bool hasOtherSpotters = false;

                    for (int i = 0; i < enemy.ghostSpotCounts.Count; i++)
                    {
                        if (enemy.ghostSpotCounts[i] != unit.GUID)
                        {
                            hasOtherSpotters = true;
                            break;
                        }
                    }

                    // if leaving would mean there would be no more spotters
                    if (!hasOtherSpotters)
                    {
                        return -1;
                    }
                }
                else if (!ContainAuraEffectTypes(currentEffects, auras) && ContainAuraEffectTypes(targetPosEffects, auras))
                {
                    // If no one else inside ECM and there is not any enemy that is revelead
                    if (enemy.ghostSpotCounts.Count == 0 && !AreAnyUnitsAffectedByAuraRevealed(enemy, unit.Combat.GetAllEnemiesOf(unit)))
                    {
                        return 1;
                    }
                }
            }

            return 0;
        }

        private static bool AreAnyUnitsAffectedByAuraRevealed(AbstractActor ecmCarrier, List<AbstractActor> potentialAllies)
        {
            for (int i = 0; i < potentialAllies.Count; i++)
            {
                if (potentialAllies[i].IsDead)
                    continue;

                var affectedAlly = potentialAllies[i];

                if (affectedAlly.AuraCache.IsAffectedByAuraFrom(ecmCarrier) && !affectedAlly.IsGhosted)
                {
                    return true;
                }
            }

            return false;
        }
    }

	public abstract class WeightedFactor
	{
		abstract public string Name { get; }
		abstract public BehaviorVariableName GetRegularMoveWeightBVName();
		abstract public BehaviorVariableName GetSprintMoveWeightBVName();

		public BehaviorVariableName GetWeightBVName(MoveType moveType)
		{
			if (moveType == MoveType.Sprinting)
			{
				return GetSprintMoveWeightBVName();
			}
			else
			{
				return GetRegularMoveWeightBVName();
			}
		}

		public virtual void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			// do nothing
		}

		public List<DesignMaskDef> CollectMasksForCellAndPathNode(CombatGameState combat, MapTerrainDataCell cell, PathNode pathNode)
		{
			List<DesignMaskDef> masks = new List<DesignMaskDef>();
			DesignMaskDef mask;
			if (cell != null)
			{
				mask = combat.MapMetaData.GetPriorityDesignMask(cell);

				if (mask != null)
				{
					masks.Add(mask);
				}
			}

			while (pathNode != null)
			{
				Point pathPoint = new Point(combat.MapMetaData.GetXIndex(pathNode.Position.x), combat.MapMetaData.GetZIndex(pathNode.Position.z));
				mask = combat.MapMetaData.GetPriorityDesignMask(combat.MapMetaData.mapTerrainDataCells[pathPoint.Z, pathPoint.X]);
				if ((mask != null) && (mask.stickyEffect != null) && (mask.stickyEffect.effectType != EffectType.NotSet) && (!masks.Contains(mask)))
				{
					masks.Add(mask);
				}
				pathNode = pathNode.Parent;
			}
			return masks;
		}
	}

	public abstract class InfluenceMapPositionFactor : WeightedFactor
	{
		abstract public float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType, PathNode pathNode);
	}

	public abstract class InfluenceMapHostileFactor : WeightedFactor
	{
		abstract public float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit);
	}

	public abstract class InfluenceMapAllyFactor : WeightedFactor
	{
		abstract public float EvaluateInfluenceMapFactorAtPositionWithAlly(AbstractActor unit, Vector3 position, float angle, ICombatant allyUnit);
	}

	#region positional factors
	public class PreferLowerMovementFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "preferLowerMovement"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			return -(position - unit.CurrentPosition).magnitude;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLowerMovementFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLowerMovementFactorWeight;
		}
	}

	public class PreferStationaryWhenHostilesInMeleeRangeFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer to stand still to prepare for melee if hostile is within melee range"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			if (!FactorUtil.IsStationaryForActor(position, angle, unit))
			{
				return 0.0f;
			}

			float meleeRange = unit.MaxMeleeEngageRangeDistance;

			for (int unitIndex = 0; unitIndex < unit.BehaviorTree.enemyUnits.Count; ++unitIndex)
			{
				ICombatant hostile = unit.BehaviorTree.enemyUnits[unitIndex];
				AbstractActor hostileUnit = hostile as AbstractActor;
				if ((hostileUnit == null) || (hostileUnit.IsDead))
				{
					continue;
				}

				float distance = (unit.CurrentPosition - hostileUnit.CurrentPosition).magnitude;

				if (distance <= meleeRange)
				{
					// found one!
					return 1.0f;
				}

			}
			return 0.0f;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferStationaryWhenHostilesInMeleeRangeFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferStationaryWhenHostilesInMeleeRangeFactorWeight;
		}
	}

	public class PreferHigherPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher position"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			return position.y - unit.CurrentPosition.y;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherPositionFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherPositionFactorWeight;
		}
	}

	public class PreferLessSteepPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer less steep"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			// Steepness is 0: level
			// 1: 90 degrees from level (straight up cliff)
			float steepness = unit.Combat.MapMetaData.GetCellAt(position).cachedSteepness;
			return -steepness;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLessSteepPositionFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLessSteepPositionFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that evaluates the distance to ALL hostiles (a hostile factor only considers a single
	/// hostile, so this can't be one of those). It returns the distance to the nearest hostile.
	/// </summary>
	public class PreferFarthestAwayFromClosestHostilePositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer farther away from closest hostile"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			return AIUtil.DistanceToClosestEnemy(unit, position);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherDistanceFromClosestHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherDistanceFromClosestHostileFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that evaluates the distance to ALL hostiles (a hostile factor only considers a single
	/// hostile, so this can't be one of those), and then compares that distance to the CoolDownRange distance. Less
	/// than the distance gets 0.0, greater than the distance gets 1.0. This is similar to ClosestHostile, above, but it
	/// provides even stronger signal to get outside the range.
	/// </summary>
	public class PreferOutsideCoolDownRangePositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer far away from combat to cool down"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			float distance = AIUtil.DistanceToClosestEnemy(unit, position);
			float coolDownRange = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CoolDownRange).FloatVal;

			return distance < coolDownRange ? 0.0f : 1.0f;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferOutsideCoolDownRangeFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferOutsideCoolDownRangeFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that evaluates the damage from ALL hostiles (a hostile factor only considers a single
	/// hostile, so this can't be one of those), and then compares that damage to the DefensiveOverkillPercentage factor times our current health. Less
	/// damage than our scaled health gets 1.0, greater damage gets 0.0.
	/// </summary>
	public class PreferNotLethalPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer not to stand where we expect to be killed"; } }

		protected float expectedDamageForShooting(AbstractActor shootingUnit, ICombatant targetUnit, Vector3 targetPosition, Quaternion targetRotation, MoveType moveType)
		{
			float expectedDamage = 0.0f;
			for (int weaponIndex = 0; weaponIndex < shootingUnit.Weapons.Count; ++weaponIndex)
			{
				Weapon weapon = shootingUnit.Weapons[weaponIndex];

				if (!weapon.CanFire)
				{
					continue;
				}

				Quaternion shooterRotation = Quaternion.LookRotation(targetPosition - shootingUnit.CurrentPosition);

				if (shootingUnit.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(shootingUnit, targetUnit, weapon.MaxRange, shootingUnit.CurrentPosition, shooterRotation, targetPosition, targetRotation, weapon.IndirectFireCapable) &&
					weapon.WillFireAtTargetFromPosition(targetUnit, shootingUnit.CurrentPosition, shooterRotation))
				{
					int numShots = weapon.ShotsWhenFired;
					AbstractActor targetActor = targetUnit as AbstractActor;

					float toHit;

					// set up hypothetical evasive pips
					if (targetActor != null)
					{
						int realEvasivePips = targetActor.EvasivePipsCurrent;
						targetActor.EvasivePipsCurrent = targetActor.GetEvasivePipsResult((targetPosition - targetUnit.CurrentPosition).magnitude,
							moveType == MoveType.Jumping, moveType == MoveType.Sprinting, moveType == MoveType.Melee);

						// TODO (DAVE) : 1 = attacking a single target. Once AI can multi-target, this should reflect the number of targets
						toHit = weapon.GetToHitFromPosition(targetUnit, 1, shootingUnit.CurrentPosition, targetPosition, true,
							moveType == MoveType.Sprinting);

						targetActor.EvasivePipsCurrent = realEvasivePips;
					}
					else
					{
						// TODO (DAVE) : 1 = attacking a single target. Once AI can multi-target, this should reflect the number of targets
						toHit = weapon.GetToHitFromPosition(targetUnit, 1, shootingUnit.CurrentPosition, targetPosition, true,
							moveType == MoveType.Sprinting);
					}
					float damagePerShot = weapon.DamagePerShotFromPosition(MeleeAttackType.NotSet, shootingUnit.CurrentPosition, targetUnit);
					float heatDamagePerShot = (1 + (weapon.HeatDamagePerShot)); // Factoring in heatDamagePerShot. +1, since most weapons deal 0 heat Dmg
					expectedDamage += numShots * toHit * damagePerShot * heatDamagePerShot;
				}
			}
			return expectedDamage;
		}

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType, PathNode pathNode_unused)
		{
			float incomingAccumulatedDamage = 0.0f;
			float myHitPoints = AttackEvaluator.MinHitPoints(unit);
			float overkillFactorLow = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OverkillThresholdLowForLethalPositionFactor).FloatVal;
			float overkillFactorHigh = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OverkillThresholdHighForLethalPositionFactor).FloatVal;
			float overkillFractionLow = overkillFactorLow / 100.0f;
			float overkillFractionHigh = overkillFactorLow / 100.0f;
			Quaternion unitRotation = Quaternion.Euler(0, angle, 0);

			for (int hostileIndex = 0; hostileIndex < unit.BehaviorTree.enemyUnits.Count; ++hostileIndex)
			{
				ICombatant hostile = unit.BehaviorTree.enemyUnits[hostileIndex];
				AbstractActor hostileUnit = hostile as AbstractActor;
				if (hostileUnit == null)
				{
					continue;
				}
				incomingAccumulatedDamage += expectedDamageForShooting(hostileUnit, unit, position, unitRotation, moveType);
			}

			float damageFrac = incomingAccumulatedDamage / myHitPoints;

			if (damageFrac >= overkillFractionHigh)
			{
				return 1.0f;
			}
			if (damageFrac <= overkillFractionLow)
			{
				return 0.0f;
			}
			return (damageFrac - overkillFractionLow) / (overkillFractionHigh - overkillFractionLow);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferNotLethalPositionFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferNotLethalPositionFactorWeight;
		}
	}

    /// <summary>
    /// A positional factor that prefers standing near friendly ECM carriers
    /// </summary>
    public class PreferFriendlyECMPositionFactor : InfluenceMapPositionFactor
    {
        public override string Name { get { return "prefer less targetable locations"; } }

        public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
        {
            List<AbstractActor> allies = unit.BehaviorTree.GetAllyUnits();

            for (int i = 0; i < allies.Count; ++i)
            {
                AbstractActor actor = allies[i];
                if (FactorUtil.DoesECMAffectDestination(actor, unit, position))
                    return 1f;
            }

            return 0f;
        }

        public override BehaviorVariableName GetRegularMoveWeightBVName()
        {
            return BehaviorVariableName.Float_PreferFriendlyECMFields;
        }

        public override BehaviorVariableName GetSprintMoveWeightBVName()
        {
            return BehaviorVariableName.Float_SprintPreferFriendlyECMFields;
        }
    }

    /// <summary>
    /// A positional factor that prefers standing near hostile ECM carriers
    /// </summary>
    public class PreferHostileECMPositionFactor : InfluenceMapPositionFactor
    {
        public override string Name { get { return "prefer to gain ghost spotter effect"; } }

        public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
        {
            var enemies = unit.BehaviorTree.enemyUnits;

            for (int i = 0; i < enemies.Count; ++i)
            {
                var enemy = enemies[i] as AbstractActor;

                switch (FactorUtil.ChangesInECMAuraState(enemy, unit, position, AuraEffectType.ECM_COUNTER))
                {
                    case 1:
                        return 1;
                    case -1:
                        return -1 * unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PreferHostileECMFieldsPenaltyMultiplier).FloatVal;
                }
            }

            return 0f;
        }

        public override BehaviorVariableName GetRegularMoveWeightBVName()
        {
            return BehaviorVariableName.Float_PreferHostileECMFields;
        }

        public override BehaviorVariableName GetSprintMoveWeightBVName()
        {
            return BehaviorVariableName.Float_SprintPreferHostileECMFields;
        }
    }

    /// <summary>
    /// A positional factor that prefers standing active probe usable locations
    /// </summary>
    public class PreferActiveProbePositionFactor : InfluenceMapPositionFactor
    {
        public override string Name { get { return "prefer active probe locations"; } }

        public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
        {
            float value = 0.0f;

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

            bool isEnemeyECMfielded = false;
            for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
            {
                AbstractActor enemy = unit.BehaviorTree.enemyUnits[enemyIndex] as AbstractActor;

                if (FactorUtil.DoesActorHaveECM(enemy))
                {
                    isEnemeyECMfielded = true;
                    break;
                }
            }

            if (apRadius > 0f)
            {
                // Look for enemies within active probe range
                for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
                {
                    AbstractActor enemy = unit.BehaviorTree.enemyUnits[enemyIndex] as AbstractActor;

                    if (enemy.StealthPipsCurrent == 0 && isEnemeyECMfielded)
                        continue;

                    float sqrDistToEnemy = (position - enemy.CurrentPosition).sqrMagnitude;
                    if (sqrDistToEnemy < apRadius * apRadius)
                        numClusteredEnemies++;
                }
            }

            value = 0.1f * numClusteredEnemies;

            return value;
        }

        public override BehaviorVariableName GetRegularMoveWeightBVName()
        {
            return BehaviorVariableName.Float_PreferActiveProbePositions;
        }

        public override BehaviorVariableName GetSprintMoveWeightBVName()
        {
            return BehaviorVariableName.Float_SprintPreferActiveProbePositions;
        }
    }

    /// <summary>
    /// A positional factor that prefers standing in less targetable locations
    /// </summary>
    public class PreferLessTargetablePositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer less targetable locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 0.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += mask.targetabilityModifier;
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLessTargetableLocationFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLessTargetableLocationFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing in locations that grant guard
	/// </summary>
	public class PreferLocationsThatGrantGuardPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer locations that grant guard"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 0.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += (mask.grantsGuarded ? 1.0f : 0.0f);
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLocationsThatGrantGuardFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLocationsThatGrantGuardFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a higher heat sink multiplier. Only provides signal for
	/// mechs; vehicles will output uniform 1.0 values (no multiplier) for all positions.
	/// </summary>
	public class PreferHigherHeatSinkPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher heat sink locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			Mech mechUnit = unit as Mech;
			if (mechUnit == null)
			{
				return 1.0f;
			}

			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 1.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value *= mask.heatSinkMultiplier;
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherHeatSinkLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherHeatSinkLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a higher heat per turn. Only provides signal for
	/// mechs; vehicles will output uniform 1.0 values (no multiplier) for all positions. Presumably you would
	/// weight this negatively.
	/// </summary>
	public class PreferHigherHeatPerTurnPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher heat per turn locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			Mech mechUnit = unit as Mech;
			if (mechUnit == null)
			{
				return 1.0f;
			}

			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 0.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += mask.heatPerTurn;
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherHeatPerTurnLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherHeatPerTurnLocationsFactorWeight;
		}
	}



	/// <summary>
	/// A positional factor that prefers standing where there's a higher damage reduction.
	/// </summary>
	public class PreferHigherDamageReductionPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher damage reduction locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			float value = 0.0f;
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += mask.allDamageTakenMultiplier;
			}

			return 1.0f / Mathf.Max(0.01f, value);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherDamageReductionLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherDamageReductionLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a higher melee to hit penalty.
	/// </summary>
	public class PreferHigherHigherMeleeToHitPenaltyPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher melee to hit penalty locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 0.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += mask.meleeTargetabilityModifier;
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherMeleeToHitPenaltyLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherMeleeToHitPenaltyLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a higher movement bonus.
	/// </summary>
	public class PreferHigherMovementBonusPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher movement bonus locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType, PathNode pathNode_unused)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			if (cell == null)
			{
				return 1.0f;
			}
			float terrainCost = PathNodeGrid.GetTerrainCost(cell, unit, moveType);

			if (terrainCost < 0.01f)
			{
				return 100.0f;
			}

			PathNodeGrid grid = unit.Pathing.getGrid(moveType);
			float baseTerrainCost = grid.Capabilities.MoveCostNormal;

			float bonus = baseTerrainCost / terrainCost;

			return bonus;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherMovementBonusLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherMovementBonusLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a lower stability damage multiplier.
	/// </summary>
	public class PreferLowerStabilityDamageMultiplierPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher stability bonus locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			float value = 0.0f;
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += mask.stabilityDamageMultiplier;
			}

			return 1.0f / Mathf.Max(0.01f, value);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLowerStabilityDamageMultiplierLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLowerStabilityDamageMultiplierLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a higher visibility cost.
	/// </summary>
	public class PreferHigherVisibilityCostPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher visibility cost locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 0.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += mask.visibilityMultiplier;
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherVisibilityCostLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherVisibilityCostLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a higher sensor range multiplier
	/// </summary>
	public class PreferHigherSensorRangeMultiplierPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer higher sensor range multiplier locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 1.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value *= mask.sensorRangeMultiplier;
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherSensorRangeMultiplierLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherSensorRangeMultiplierLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a lower signature multiplier
	/// </summary>
	public class PreferLowerSignatureMultiplierPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer lower signature multiplier locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 1.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value *= mask.signatureMultiplier;
			}

			return 1.0f / Mathf.Max(0.01f, value);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLowerSignatureMultiplierLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLowerSignatureMultiplierLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a lower to hit penalty
	/// </summary>
	public class PreferLowerRangedToHitPenaltyPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer lower ranged to hit penalty locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 1.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value *= mask.toHitFromModifier;
			}

			return 1.0f / Mathf.Max(0.01f, value);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLowerRangedToHitPenaltyLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLowerRangedToHitPenaltyLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing where there's a lower to hit penalty
	/// </summary>
	public class PreferHigherRangedDefenseBonusPositionFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer lower ranged to hit penalty locations"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);
			List<DesignMaskDef> masks = CollectMasksForCellAndPathNode(unit.Combat, cell, pathNode);

			float value = 0.0f;

			for (int maskIndex = 0; maskIndex < masks.Count; ++maskIndex)
			{
				DesignMaskDef mask = masks[maskIndex];
				value += mask.targetabilityModifier;
			}

			return value;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherRangedDefenseBonusLocationsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherRangedDefenseBonusLocationsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers standing at a specified distance from things with a specified tag.
	/// </summary>
	public class PreferProximityToTaggedTargetFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer to be at a specified distance to indicated targets"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			float closestDist = float.MaxValue;

			string[] tags = new string[1] { unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_PreferProximityToTaggedTargetFactorTag).StringVal };

			List<ITaggedItem> targets = unit.Combat.ItemRegistry.GetObjectsWithTagSet(new HBS.Collections.TagSet(tags));

			// TODO(dlecompte): are ICombatants all we need to match the tags against?
			for (int targetIndex = 0; targetIndex < targets.Count; ++targetIndex)
			{
				ITaggedItem target = targets[targetIndex];
				ICombatant combatantTarget = target as ICombatant;

				if (combatantTarget != null)
				{
					float distance = (unit.CurrentPosition - combatantTarget.CurrentPosition).magnitude;
					closestDist = Mathf.Min(closestDist, distance);
				}
			}

			float preferredDistance = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PreferProximityToTaggedTargetFactorDistance).FloatVal;

			if (closestDist < preferredDistance)
			{
				return closestDist / preferredDistance;
			}
			else
			{
				return preferredDistance / closestDist;
			}
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferProximityToTaggedTargetFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferProximityToTaggedTargetFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers this lance surrounding hostile units.
	/// </summary>
	public class PreferSurroundingHostileUnitsFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer surrounding hostile units"; } }

		List<AbstractActor> getUnitsFromLance(CombatGameState combat, Lance lance)
		{
			List<AbstractActor> unitList = new List<AbstractActor>();
			for (int otherUnitIndex = 0; otherUnitIndex < combat.AllActors.Count; ++otherUnitIndex)
			{
				AbstractActor otherUnit = combat.AllActors[otherUnitIndex];
				if (otherUnit.IsDead)
				{
					continue;
				}

				if (otherUnit.lance == lance)
				{
					unitList.Add(otherUnit);
				}
			}
			return unitList;
		}

		private static float GetActorAngle(Vector3 centerPos, Vector3 surroundPos)
		{
			Vector3 deltaPos = surroundPos - centerPos;

			return Mathf.Atan2(deltaPos.z, deltaPos.x);
		}

		private class AngleSortHelper : IComparer<Vector3>
		{
			Vector3 centerPos;
			public AngleSortHelper(Vector3 centerPos)
			{
				this.centerPos = centerPos;
			}

			float getActorAngle(Vector3 actorPos)
			{
				return GetActorAngle(centerPos, actorPos);
			}

			public int Compare(Vector3 v1, Vector3 v2)
			{
				return Comparer<float>.Default.Compare(getActorAngle(v1), getActorAngle(v2));
			}
		}

		float calculateSurround(AbstractActor targetUnit, Vector3 candidatePosition, AbstractActor movingActor, List<AbstractActor> unitList)
		{
			// first, sort the unitList by angle
			AngleSortHelper ash = new AngleSortHelper(targetUnit.CurrentPosition);

			List<Vector3> unitPositions = new List<Vector3>();
			for (int unitIndex = 0; unitIndex < unitList.Count; ++unitIndex)
			{
				AbstractActor unit = unitList[unitIndex];
				Vector3 pos = unit.CurrentPosition;
				if (movingActor == unit)
				{
					pos = candidatePosition;
				}
				unitPositions.Add(pos);
			}

			unitPositions.Sort(ash);

			// for each starting unit, measure the angle going clockwise through the list
			List<float> anglesToUnits = new List<float>();
			for (int i = 0; i < unitList.Count; ++i)
			{
				anglesToUnits.Add(GetActorAngle(targetUnit.CurrentPosition, unitPositions[i]) * Mathf.Rad2Deg);
			}

			// Find the smallest angle that covers all the points. This is a (positive) angle from a{n} to a{n-1}.
			float bestAngle = float.MaxValue;
			const float fullCircle = 360.0f;

			for (int i = 0; i < unitList.Count; ++i)
			{
				int nextIndex = (i + unitList.Count - 1) % unitList.Count;
				float angle = anglesToUnits[i];
				float nextAngle = anglesToUnits[nextIndex];
				float deltaAngle = nextAngle - angle;
				if (deltaAngle < 0)
				{
					deltaAngle += fullCircle;
				}

				if (deltaAngle < bestAngle)
				{
					bestAngle = deltaAngle;
				}
			}

			// return the minimal value
			return bestAngle;
		}

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			List<AbstractActor> myLanceUnits = getUnitsFromLance(unit.Combat, unit.lance);
			if (myLanceUnits.Count < 2)
			{
				return 0.0f;
			}

			float surroundTotal = 0.0f;
			for (int otherUnitIndex = 0; otherUnitIndex < unit.Combat.AllActors.Count; ++otherUnitIndex)
			{
				AbstractActor otherUnit = unit.Combat.AllActors[otherUnitIndex];
				if ((!otherUnit.IsDead) &&
					(unit.Combat.HostilityMatrix.IsEnemy(unit.TeamId, otherUnit.TeamId)))
				{
					surroundTotal += calculateSurround(otherUnit, position, unit, myLanceUnits);
				}
			}

			return surroundTotal;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferSurroundingHostileUnitsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferSurroundingHostileUnitsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that prefers not to be surrounded by hostile units.
	/// </summary>
	public class PreferNotSurroundedByHostileUnitsFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer not being surrounded by hostile units"; } }

		private List<AbstractActor> getHostileUnits(CombatGameState combat, AbstractActor unit)
		{
			List<AbstractActor> units = new List<AbstractActor>();

			for (int unitIndex = 0; unitIndex < combat.AllActors.Count; ++unitIndex)
			{
				AbstractActor otherUnit = combat.AllActors[unitIndex];
				if ((!otherUnit.IsDead) &&
					(combat.HostilityMatrix.IsEnemy(unit.TeamId, otherUnit.TeamId)))
				{
					units.Add(otherUnit);
				}
			}

			return units;
		}

		private static float GetSurroundAngle(Vector3 position, AbstractActor actor)
		{
			Vector3 deltaPos = actor.CurrentPosition - position;

			return Mathf.Atan2(deltaPos.z, deltaPos.x);
		}

		private class AngleSortHelper : IComparer<AbstractActor>
		{
			Vector3 centerPosition;
			public AngleSortHelper(Vector3 centerPosition)
			{
				this.centerPosition = centerPosition;
			}

			float getActorAngle(AbstractActor actor)
			{
				return GetSurroundAngle(centerPosition, actor);
			}

			public int Compare(AbstractActor a1, AbstractActor a2)
			{
				return Comparer<float>.Default.Compare(getActorAngle(a1), getActorAngle(a2));
			}
		}

		float calculateSurround(Vector3 surroundPosition, List<AbstractActor> unitList)
		{
			// first, sort the unitList by angle
			AngleSortHelper ash = new AngleSortHelper(surroundPosition);
			unitList.Sort(ash);

			// for each starting unit, measure the angle going clockwise through the list
			List<float> anglesToUnits = new List<float>();
			for (int i = 0; i < unitList.Count; ++i)
			{
				anglesToUnits.Add(GetSurroundAngle(surroundPosition, unitList[i]) * Mathf.Rad2Deg);
			}

			// Find the smallest angle that covers all the points. This is a (positive) angle from a{n} to a{n-1}.
			float bestAngle = float.MaxValue;
			const float fullCircle = 360.0f;

			for (int i = 0; i < unitList.Count; ++i)
			{
				int nextIndex = (i + unitList.Count - 1) % unitList.Count;
				float angle = anglesToUnits[i];
				float nextAngle = anglesToUnits[nextIndex];
				float deltaAngle = nextAngle - angle;
				if (deltaAngle < 0)
				{
					deltaAngle += fullCircle;
				}

				if (deltaAngle < bestAngle)
				{
					bestAngle = deltaAngle;
				}
			}

			// return the minimal value
			return bestAngle;
		}

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			List<AbstractActor> hostileUnits = getHostileUnits(unit.Combat, unit);
			if (hostileUnits.Count < 2)
			{
				return 0.0f;
			}

			float surroundVal = calculateSurround(position, hostileUnits);

			const float MIN_SURROUND_VAL = 180.0f;

			surroundVal = Mathf.Max(surroundVal, MIN_SURROUND_VAL);

			return 1.0f / surroundVal;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferNotSurroundedByHostileUnitsFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferNotSurroundedByHostileUnitsFactorWeight;
		}
	}

	/// <summary>
	/// A positional factor that tries to get the entire team having some equal value, possibly "engagement with enemy".
	/// This will be accomplished conceputally by generating a voronoi graph for
	/// areas around my living team units, and "shading" each region based on
	/// the center unit's LOS count to living hostiles. We then turn that into an
	/// influence factor by finding the (unsigned) distance from that locally evaluated
	/// number to the mean value.
	/// </summary>
	public abstract class PreferEqualizeQuantityOverTeamFactor : InfluenceMapPositionFactor
	{
		protected List<AbstractActor> teamUnits;
		protected List<ICombatant> hostileUnits;
		float meanInterpolatedValue;
		Vector3 meanPosition;

		struct EvaluatedUnit: IComparable<EvaluatedUnit>
		{
			public AbstractActor unit;
			public float evaluatedValue;

			public EvaluatedUnit(AbstractActor unit, float evaluatedValue)
			{
				this.unit = unit;
				this.evaluatedValue = evaluatedValue;
			}

			public int CompareTo(EvaluatedUnit other)
			{
				return this.evaluatedValue.CompareTo(other.evaluatedValue);
			}
		};

		List<EvaluatedUnit> evaluatedUnits;

		private List<ICombatant> getHostileUnits(AbstractActor unit)
		{
			List<ICombatant> units = new List<ICombatant>();

			for (int unitIndex = 0; unitIndex < unit.BehaviorTree.enemyUnits.Count; ++unitIndex)
			{
				ICombatant otherUnit = unit.BehaviorTree.enemyUnits[unitIndex];
				if (!otherUnit.IsDead)
				{
					units.Add(otherUnit);
				}
			}

			return units;
		}

		private List<AbstractActor> getTeamUnits(AbstractActor unit)
		{
			List<AbstractActor> units = new List<AbstractActor>();

			for (int unitIndex = 0; unitIndex < unit.team.units.Count; ++unitIndex)
			{
				AbstractActor otherUnit = unit.team.units[unitIndex];
				if (!otherUnit.IsDead)
				{
					units.Add(otherUnit);
				}
			}

			return units;
		}

		public abstract float CalcInterpolatedValueForUnit(AbstractActor unit);

		void makeEvaluationList()
		{
			evaluatedUnits = new List<EvaluatedUnit>();
			for (int i = 0; i < teamUnits.Count; ++i)
			{
				AbstractActor unit = teamUnits[i];
				float value = CalcInterpolatedValueForUnit(unit);
				EvaluatedUnit evalUnit = new EvaluatedUnit(unit, value);
				evaluatedUnits.Add(evalUnit);
			}

			evaluatedUnits.Sort();
		}

		float MeanInterpolatedValue()
		{
			float sumValue = 0.0f;
			int friendCount = teamUnits.Count;
			if (friendCount == 0)
			{
				return 0.0f;
			}

			for (int i = 0; i < friendCount; ++i)
			{
				sumValue += evaluatedUnits[i].evaluatedValue;
			}
			return sumValue / friendCount;
		}

		List<AbstractActor> findUnitsWithValue(float v)
		{
			List<AbstractActor> retList = new List<AbstractActor>();
			for (int i = 0; i < evaluatedUnits.Count; ++i)
			{
				if (evaluatedUnits[i].evaluatedValue == v)
				{
					retList.Add(evaluatedUnits[i].unit);
				}
			}
			return retList;
		}

		Vector3 getMeanPosition()
		{
			Vector3 sum = Vector3.zero;
			for (int i = 0; i < teamUnits.Count; ++i)
			{
				sum += teamUnits[i].CurrentPosition;
			}
			return sum / teamUnits.Count;
		}

		Vector3 getMeanPositionOld()
		{
			float val = MeanInterpolatedValue();

			List<AbstractActor> equalList = findUnitsWithValue(val);
			// easiest case; there's only one guy, use that.
			if (equalList.Count == 1)
			{
				return equalList[0].CurrentPosition;
			}

			// maybe there's a lot of guys at this value - return their average position.
			if (equalList.Count > 1)
			{
				Vector3 sum = Vector3.zero;
				for (int i = 0; i < equalList.Count; ++i)
				{
					sum += equalList[i].CurrentPosition;
				}
				return sum * (1.0f / equalList.Count);
			}

			// otherwise, we have to find i such that values{i} < v < values{i+1}

			for (int i = 0; i < evaluatedUnits.Count - 1; ++i)
			{
				float valBelow = evaluatedUnits[i].evaluatedValue;
				float valAbove = evaluatedUnits[i + 1].evaluatedValue;
				if ((valBelow < val) &&
					(valAbove > val))
				{
					// interpolate between posns{i} and posns{i+1}
					float frac = (val - valBelow) / (valAbove - valBelow);

					Vector3 posnBelow = evaluatedUnits[i].unit.CurrentPosition;
					Vector3 posnAbove = evaluatedUnits[i+1].unit.CurrentPosition;
					Vector3 delta = posnAbove - posnBelow;
					return posnBelow + frac * delta;
				}
			}

			// should never get here
			Debug.Assert(false, "how did I get to the bottom of the getMeanPosition?");

			int meanIndex = evaluatedUnits.Count / 2;
			return evaluatedUnits[meanIndex].unit.CurrentPosition;
		}

		float getRangeRadius()
		{
			Vector3 bottomPos = evaluatedUnits[0].unit.CurrentPosition;
			Vector3 topPos = evaluatedUnits[evaluatedUnits.Count - 1].unit.CurrentPosition;

			float bottomDist = (meanPosition - bottomPos).magnitude;
			float topDist = (meanPosition - topPos).magnitude;

			return Mathf.Max(bottomDist, topDist);
		}

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit_unused, Vector3 position, float angle_unused, MoveType moveType_unused, PathNode pathNode_unused)
		{
			if (evaluatedUnits[0].evaluatedValue == evaluatedUnits[evaluatedUnits.Count-1].evaluatedValue)
			{
				return 0.0f;
			}

			// TODO(dlecompte) why is this a hardcoded const and not a behavior variable?
			const float DEAD_ZONE_RADIUS = 75.0f;

			float dist = (position - meanPosition).magnitude;

			if (dist <= DEAD_ZONE_RADIUS)
			{
				return 1.0f;
			}

			// dist == 0: 1.0f
			// dist == DEAD_ZONE_RADIUS : 1.0
			// dist == 2 * DEAD_ZONE_RADIUS : 0.0

			float ramp_dist = dist - DEAD_ZONE_RADIUS;
			float ramp_frac = ramp_dist / DEAD_ZONE_RADIUS;
			return 1.0f - ramp_frac;
		}

		public override void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			teamUnits = getTeamUnits(unit);
			hostileUnits = getHostileUnits(unit);
			makeEvaluationList();
			//meanInterpolatedValue = MeanInterpolatedValue();
			meanPosition = getMeanPosition();
		}
	}

	public class PreferEqualizeExposureCountPositionalFactor : PreferEqualizeQuantityOverTeamFactor
	{
		public override string Name { get { return "prefer team to equalize their exposure"; } }

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferEqualizeEngagementOverTeamFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferEqualizeEngagementOverTeamFactorWeight;
		}

		/// <summary>
		/// figure out how many hostiles have LOF on me
		/// </summary>
		/// <param name="unit"></param>
		/// <returns>count of hostiles with LOF</returns>
		public override float CalcInterpolatedValueForUnit(AbstractActor unit)
		{
			int count = 0;

			for (int i = 0; i < hostileUnits.Count; ++i)
			{
				ICombatant hostile = hostileUnits[i];
				AbstractActor hostileUnit = hostile as AbstractActor;
				if (hostileUnit == null)
				{
					continue;
				}
				for (int weaponIndex = 0; weaponIndex < hostileUnit.Weapons.Count; ++weaponIndex)
				{
					Weapon w = hostileUnit.Weapons[weaponIndex];
					if (hostileUnit.Combat.LOFCache.UnitHasLOFToTarget(hostileUnit, unit, w))
					{
						count++;
						break;
					}
				}
			}
			return count;
		}
	}

	public class PreferInsideFenceNegativeLogicPositionalFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer team to stay inside fence - NEGATIVE LOGIC"; } }

		protected List<AbstractActor> lanceUnits;
		Vector3 meanPosition;

		private List<AbstractActor> getLanceUnits(AbstractActor unit)
		{
			List<AbstractActor> units = new List<AbstractActor>();
			string lanceGUID = unit.LanceId;

			for (int unitIndex = 0; unitIndex < unit.team.units.Count; ++unitIndex)
			{
				AbstractActor otherUnit = unit.team.units[unitIndex];
				if ((!otherUnit.IsDead) && (otherUnit.LanceId.Equals(lanceGUID)))
				{
					units.Add(otherUnit);
				}
			}

			return units;
		}

		Vector3 getMeanPosition()
		{
			Vector3 sum = Vector3.zero;
			float count = 0.0f;

			for (int i = 0; i < lanceUnits.Count; ++i)
			{
				AbstractActor unit = lanceUnits[i];
				if (unit.IsDead)
				{
					continue;
				}
				float weight = 1.0f;
				if (unit.IsVulnerableToCalledShots())
				{
					weight = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_DownedMechFenceContributionMultiplier).FloatVal;
				}
				sum += weight * lanceUnits[i].CurrentPosition;
				count += weight;
			}
			return sum / count;
		}

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle_unused, MoveType moveType_unused, PathNode pathNode_unused)
		{
			float fenceRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_FenceRadius).FloatVal;

			float dist = (position - meanPosition).magnitude;

			// N.B. this is using NEGATIVE LOGIC, so good stuff is 0, and bad stuff goes up (notionally to 1, but will be normalized to 1 anyway).
			if (dist <= fenceRadius)
			{
				return 0.0f;
			}

			float ramp_dist = dist - fenceRadius;
			return ramp_dist / fenceRadius;
		}

		public override void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			lanceUnits = getLanceUnits(unit);
			meanPosition = getMeanPosition();
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferStayInsideFenceNegativeLogicFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferStayInsideFenceNegativeLogicFactorWeight;
		}
	}

	public class PreferInsideExcludedRegionPositionalFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer to be inside excluded region - NEGATIVE LOGIC"; } }

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle_unused, MoveType moveType_unused, PathNode pathNode_unused)
		{
			MapTerrainDataCell cell = unit.Combat.MapMetaData.GetCellAt(position);

			return (SplatMapInfo.IsDropshipLandingZone(cell.terrainMask) || SplatMapInfo.IsDropPodLandingZone(cell.terrainMask) || SplatMapInfo.IsDangerousLocation(cell.terrainMask) ? 1.0f : 0.0f);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_ExcludedRegionWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintExcludedRegionWeight;
		}
	}


	public class PreferExposedAlonePositionalFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer to be exposed and alone: NEGATIVE LOGIC"; } }

		bool exposureOK;
		int exposedTeammateCount;
		Dictionary<AbstractActor, float> maxRanges;
		Dictionary<AbstractActor, bool> isIndirectFireCapable;

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			// if within our window
			if (exposureOK)
			{
				return 0.0f;
			}

			if (exposedTeammateCount > 0)
			{
				return 0.0f;
			}

			Quaternion targetRotation = Quaternion.Euler(0, angle, 0);

			float exposure = 0.0f;

			for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
			{
				ICombatant enemyCombatant = unit.BehaviorTree.enemyUnits[enemyIndex];
				AbstractActor enemyActor = enemyCombatant as AbstractActor;
				if ((enemyActor == null) ||
					(enemyActor.IsDead))
				{
					continue;
				}
				if (enemyActor.HasLOFToTargetUnitAtTargetPosition(unit, maxRanges[enemyActor], unit.CurrentPosition,
					Quaternion.LookRotation(position - unit.CurrentPosition), position, targetRotation, isIndirectFireCapable[enemyActor]))
				{
					exposure += 1.0f;
				}
			}

			return exposure;
		}

		public override void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			int lastAloneRound = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_LastAloneRoundNumber).IntVal;
			int lastNotAloneRound = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_LastNotAloneRoundNumber).IntVal;

			if (lastNotAloneRound > lastAloneRound)
			{
				// we're currently not alone
				int roundsNotAlone = unit.Combat.TurnDirector.CurrentRound - lastAloneRound;
				exposureOK = (roundsNotAlone > unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_AloneCoolDownTurnCount).IntVal);
			}
			else
			{
				// we're currently alone
				int roundsAlone = unit.Combat.TurnDirector.CurrentRound - lastNotAloneRound;
				exposureOK = (roundsAlone <= unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_AloneToleranceTurnCount).IntVal);
			}

			exposedTeammateCount = 0;
			maxRanges = new Dictionary<AbstractActor, float>();
			isIndirectFireCapable = new Dictionary<AbstractActor, bool>();

			for (int lancemateIndex = 0; lancemateIndex < unit.lance.unitGuids.Count; ++lancemateIndex)
			{
				AbstractActor lancemate = unit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(unit.lance.unitGuids[lancemateIndex]);
				if ((lancemate == null) ||
					(lancemate.IsDead))
				{
					continue;
				}
				if (AIUtil.IsExposedToHostileFire(lancemate, unit.Combat))
				{
					exposedTeammateCount += 1;
				}
			}
			for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
			{
				AbstractActor enemy = unit.BehaviorTree.enemyUnits[enemyIndex] as AbstractActor;
				if ((enemy == null) ||
					(enemy.IsDead))
				{
					continue;
				}
				float maxRange = float.MinValue;
				bool indirect = false;
				for (int weaponIndex = 0; weaponIndex < enemy.Weapons.Count; ++weaponIndex)
				{
					Weapon w = enemy.Weapons[weaponIndex];
					if (!w.CanFire)
					{
						continue;
					}
					indirect |= w.IndirectFireCapable;
					maxRange = Mathf.Max(maxRange, w.MaxRange);
				}
				maxRanges[enemy] = maxRange;
				isIndirectFireCapable[enemy] = indirect;
			}
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_AlonePreferenceWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintAlonePreferenceWeight;
		}
	}

	public class PreferFiringSolutionWhenExposedAllyPositionalFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer to have a firing solution when I have an exposed ally"; } }
		AbstractActor exposedAlly;

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused)
		{
			// if no exposed ally, do nothing.
			if (exposedAlly == null)
			{
				return 0.0f;
			}

			Quaternion rotQuat = Quaternion.Euler(0, angle, 0);

			float maxRange = float.MinValue;
			bool isIndirectFireCapable = false;

			for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
			{
				Weapon w = unit.Weapons[weaponIndex];
				if (!w.CanFire)
				{
					continue;
				}
				maxRange = Mathf.Max(w.MaxRange, maxRange);
				isIndirectFireCapable |= w.IndirectFireCapable;
			}

			// TEST - what if we don't count indirect fire when we want to help our buddy?
			isIndirectFireCapable = false;

			for (int enemyIndex = 0; enemyIndex < unit.BehaviorTree.enemyUnits.Count; ++enemyIndex)
			{
				ICombatant enemy = unit.BehaviorTree.enemyUnits[enemyIndex];
				if (unit.HasLOFToTargetUnit(enemy, maxRange, position, rotQuat, isIndirectFireCapable))
				{
					return 1.0f;
				}
			}
			return 0.0f;
		}

		public override void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			exposedAlly = AIUtil.GetExposedAloneLancemate(unit, unit.Combat);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_BuddyAloneFiringSolutionPreferenceWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintBuddyAloneFiringSolutionPreferenceWeight;
		}
	}


	public class PreferNearExposedAllyPositionalFactor : InfluenceMapPositionFactor
	{
		public override string Name { get { return "prefer to be far from exposed ally: NEGATIVE LOGIC"; } }
		AbstractActor exposedAlly;
		float distanceFromUnitToAlly;

		public override float EvaluateInfluenceMapFactorAtPosition(AbstractActor unit, Vector3 position, float angle_unused, MoveType moveType_unused, PathNode pathNode_unused)
		{
			// if no exposed ally, do nothing.
			if (exposedAlly == null)
			{
				return 0.0f;
			}
			if (distanceFromUnitToAlly < 0.1f)
			{
				Debug.LogError("distance from moving unit to exposed unit is very small: " + distanceFromUnitToAlly);
				return 0.0f;
			}

			// normalize distance based on existing distance
			float distanceFromPositionToTarget = (position - exposedAlly.CurrentPosition).magnitude;
			return distanceFromPositionToTarget / distanceFromUnitToAlly;
		}

		public override void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			exposedAlly = AIUtil.GetExposedAloneLancemate(unit, unit.Combat);
			if (exposedAlly != null)
			{
				distanceFromUnitToAlly = (unit.CurrentPosition - exposedAlly.CurrentPosition).magnitude;
			}
			else
			{
				distanceFromUnitToAlly = 0.0f;
			}
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_BuddyAloneMoveNearbyPreferenceWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintBuddyAloneMoveNearbyPreferenceWeight;
		}
	}


	#endregion // positional factors

	#region hostile factors

	// base class for higher expected damage to hostiles, or lower expected damage from hostiles

	public abstract class HostileDamageFactor : InfluenceMapHostileFactor
	{
		public float debugMaxDamage;
		public float debugLastDamage;
		public float debugLastScaledDamage;

		public float debugMaxShootDamage;
		public float debugMaxMeleeDamage;
		public float debugMaxDFADamage;

		protected float expectedDamageForShooting(AbstractActor shootingUnit, Vector3 shootingPosition, Quaternion shootingRotation, ICombatant targetUnit, Vector3 targetPosition, Quaternion targetRotation, bool targetIsEvasive, bool targetIsJumping, bool useHypotheticalEvasiveForTarget)
		{
			float expectedDamage = 0.0f;
			for (int weaponIndex = 0; weaponIndex < shootingUnit.Weapons.Count; ++weaponIndex)
			{
				Weapon weapon = shootingUnit.Weapons[weaponIndex];

				if (!weapon.CanFire)
				{
					continue;
				}

				if (shootingUnit.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(shootingUnit, targetUnit, weapon.MaxRange, shootingPosition, shootingRotation, targetPosition, targetRotation, weapon.IndirectFireCapable) &&
					weapon.WillFireAtTargetFromPosition(targetUnit, shootingPosition, shootingRotation))
				{
					int numShots = weapon.ShotsWhenFired;
					// set up hypothetical evasive pips
					float toHit;
					AbstractActor targetActor = targetUnit as AbstractActor;
					if (useHypotheticalEvasiveForTarget && (targetActor != null))
					{
						int realPips = targetActor.EvasivePipsCurrent;
						targetActor.EvasivePipsCurrent = targetActor.GetEvasivePipsResult((targetPosition - targetActor.CurrentPosition).magnitude, targetIsJumping, targetIsEvasive, false);
						toHit = weapon.GetToHitFromPosition(targetUnit, 1, shootingPosition, targetPosition, true, targetIsEvasive); // TODO (DAVE) : 1 = attacking a single target. Once AI can multi-target, this should reflect the number of targets
						targetActor.EvasivePipsCurrent = realPips;
					}
					else
					{
						toHit = weapon.GetToHitFromPosition(targetUnit, 1, shootingPosition, targetPosition, true, targetIsEvasive); // TODO (DAVE) : 1 = attacking a single target. Once AI can multi-target, this should reflect the number of targets
					}
					float damagePerShot = weapon.DamagePerShotFromPosition(MeleeAttackType.NotSet, shootingPosition, targetUnit);
					float heatDamagePerShot = (1 + (weapon.HeatDamagePerShot)); // Factoring in heatDamagePerShot. +1, since most weapons deal 0 heat Dmg
					expectedDamage += numShots * toHit * damagePerShot * heatDamagePerShot;
				}
			}
			// TODO move multipliers out - they should be looked up based on the moving unit, not the shooter or target.
			return expectedDamage * shootingUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ShootingDamageMultiplier).FloatVal;
		}

		protected float expectedDamageForMelee(AbstractActor attackingUnit, Vector3 attackingPosition, Quaternion attackingRotation, ICombatant targetUnit, Vector3 targetPosition, Quaternion targetRotation, bool targetIsEvasive)
		{
			// TODO CanEngageTarget needs to take a target position into account e.g. CanEngageTargetAtLocation

			Mech attackingMech = attackingUnit as Mech;
			if ((attackingMech == null) ||
				(!attackingMech.CanEngageTarget(targetUnit)))
			{
				return 0.0f;
			}

            if (attackingMech.Pathing.GetMeleeDestsForTarget(targetUnit as AbstractActor).Count == 0)
            {
                return 0.0f;
            }

            return AIUtil.ExpectedDamageForMeleeAttack(attackingMech, targetUnit, attackingPosition, targetPosition, true);
		}

		protected float expectedDamageForDFA(AbstractActor attackingUnit, Vector3 attackingPosition, Quaternion attackingRotation, ICombatant targetUnit, Vector3 targetPosition, Quaternion targetRotation, bool targetIsEvasive)
		{
			Mech mech = attackingUnit as Mech;
			if ((mech == null) ||
                (!mech.CanDFATargetFromPosition(targetUnit, attackingPosition)) ||
                (!AIUtil.IsDFAAcceptable(attackingUnit, targetUnit)))
			{
				return 0.0f;
			}

            if (mech.JumpPathing.GetDFADestsForTarget(targetUnit as AbstractActor).Count == 0)
            {
                return 0.0f;
            }

            List<Weapon> dfaWeapons = attackingUnit.Weapons.FindAll(x => x.WeaponCategoryValue.CanUseInMelee);
			dfaWeapons.Add(mech.DFAWeapon);
            return AIUtil.ExpectedDamageForAttack(attackingUnit, AIUtil.AttackType.DeathFromAbove, dfaWeapons, targetUnit, attackingPosition, targetPosition, true, attackingUnit);
		}

		public void ResetEvaluation()
		{
			debugMaxDamage = 0.0f;

			debugMaxShootDamage = 0.0f;
			debugMaxMeleeDamage = 0.0f;
			debugMaxDFADamage = 0.0f;
		}

		public void LogEvaluation()
		{
			AIUtil.LogAI("max damage that I considered: " + debugMaxDamage);

			if (debugMaxDamage < 1.0f)
			{
				AIUtil.LogAI("not very much damage, is it?");
			}

			AIUtil.LogAI("max shooting damage that I considered: " + debugMaxShootDamage);
			AIUtil.LogAI("max melee damage that I considered: " + debugMaxMeleeDamage);
			AIUtil.LogAI("max DFA damage that I considered: " + debugMaxDFADamage);
		}
	}

	public class PreferHigherExpectedDamageToHostileFactor : HostileDamageFactor
	{
		public override string Name { get { return "prefer higher damage"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			Quaternion rotationQuat = Quaternion.Euler(0, angle, 0);

            if (unit.BehaviorTree.IsTargetIgnored(hostileUnit))
            {
                return 0.0f;
            }

            Mech hostileMech = hostileUnit as Mech;

			bool hostileIsEvasive = (hostileMech != null) && hostileMech.IsEvasive;

            bool isStationary = FactorUtil.IsStationaryForActor(position, angle, unit);


            float[] damages = {
				expectedDamageForShooting(unit, position, rotationQuat, hostileUnit, hostileUnit.CurrentPosition, hostileUnit.CurrentRotation, hostileIsEvasive, false, false),
				isStationary ? expectedDamageForMelee(unit, position, rotationQuat, hostileUnit, hostileUnit.CurrentPosition, hostileUnit.CurrentRotation, hostileIsEvasive) : 0.0f,
				isStationary ? expectedDamageForDFA(unit, position, rotationQuat, hostileUnit, hostileUnit.CurrentPosition, hostileUnit.CurrentRotation, hostileIsEvasive) : 0.0f,
			};

            string shortGUID = unit.GUID.Substring(0, 4);
            Pilot p = unit.GetPilot();
            string pilotName = (p != null) ? p.Callsign : "[no pilot]";
            string msg = String.Format("{4} {5} - expected damages against {0} : shoot: {1} melee: {2} dfa: {3}", hostileUnit.LogDisplayName, damages[0], damages[1], damages[2], unit, shortGUID, pilotName);
            AIUtil.LogAI(msg, unit);

            float expectedDamage = Mathf.Max(damages);

			debugMaxDamage = Mathf.Max(debugMaxDamage, expectedDamage);
			debugLastDamage = expectedDamage;
			debugMaxShootDamage = Mathf.Max(debugMaxShootDamage, damages[0]);
			debugMaxMeleeDamage = Mathf.Max(debugMaxMeleeDamage, damages[1]);
			debugMaxDFADamage = Mathf.Max(debugMaxDFADamage, damages[2]);

			float hostileFactor = FactorUtil.HostileFactor(unit, hostileUnit);
			float scaledExpectedDamage = expectedDamage * hostileFactor;
			if (expectedDamage > 0 && hostileFactor > 0)
			{
				AIUtil.LogAI(String.Format("exp dmg: {0} host fact: {1} total: {2}", expectedDamage, hostileFactor, scaledExpectedDamage), unit);
			}

			debugLastScaledDamage = scaledExpectedDamage;

			return scaledExpectedDamage;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherExpectedDamageToHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherExpectedDamageToHostileFactorWeight;
		}
	}

	public class PreferLowerExpectedDamageFromHostileFactor : HostileDamageFactor
	{
		public override string Name { get { return "prefer lower damage from hostiles"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				return 0.0f;
			}

			bool MyMechIsEvasive = moveType == MoveType.Sprinting;

			Quaternion rotationQuat = Quaternion.Euler(0, angle, 0);

			// handle hypothetical pips from evasive
			int realPips = unit.EvasivePipsCurrent;
			unit.EvasivePipsCurrent = unit.GetEvasivePipsResult((position - unit.CurrentPosition).magnitude, moveType == MoveType.Jumping,
				moveType == MoveType.Sprinting, moveType == MoveType.Melee);
			float damage = expectedDamageForShooting(hostileActor, hostileActor.CurrentPosition, hostileActor.CurrentRotation, unit, position, rotationQuat, MyMechIsEvasive, moveType == MoveType.Jumping, true);
			unit.EvasivePipsCurrent = realPips;
			Debug.Assert(damage >= 0.0f);
			return -damage;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLowerExpectedDamageFromHostileFactorWeight;
		}
		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLowerExpectedDamageFromHostileFactorWeight;
		}
	}

	public class PreferAttackFrom90DegreesToHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer 90 degrees"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			Vector3 vectorFromHostileToUs = position - hostileUnit.CurrentPosition;

			Vector3 hostileForward = hostileUnit.CurrentRotation * Vector3.forward;

			float angleInDegreesToTarget = Vector3.Angle(hostileForward, vectorFromHostileToUs);

			float centerAngle = 90.0f;
			float notchHalfWidth = 30.0f;

			// 180 degrees -> -1.0
			// 90 degrees -> 1.0
			// 0 degrees ->  -1.0

			float angleFactor;
			float deltaAngle = Mathf.Abs(angleInDegreesToTarget - centerAngle);

			if (deltaAngle >= notchHalfWidth)
			{
				angleFactor = -1.0f;
			}
			else
			{
				angleFactor = 1.0f - 2.0f * (deltaAngle / notchHalfWidth);
			}

			return angleFactor * FactorUtil.HostileFactor(unit, hostileUnit);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferAttackFrom90DegreesToHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferAttackFrom90DegreesToHostileFactorWeight;
		}
	}

	public class PreferAttackFromBehindHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer attack from behind"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			Vector3 vectorFromHostileToUs = position - hostileUnit.CurrentPosition;

			Vector3 hostileForward = hostileUnit.CurrentRotation * Vector3.forward;

			float angleInDegreesToTarget = Vector3.Angle(hostileForward, vectorFromHostileToUs);

			// 180 degrees -> 1.0
			// 90 degrees -> 0.0
			// 0 degrees -> -1.0

			float angleFactor = angleInDegreesToTarget / 90.0f - 1.0f;

			return angleFactor * FactorUtil.HostileFactor(unit, hostileUnit);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferAttackFromBehindHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferAttackFromBehindHostileFactorWeight;
		}
	}

	public class PreferNoCloserThanMinDistToHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer no closer than weapon min dist to hostile"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			float minWeaponDistance = 0.0f;

			for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
			{
				Weapon weapon = unit.Weapons[weaponIndex];
				if (!weapon.CanFire)
				{
					continue;
				}
				minWeaponDistance = Mathf.Max(weapon.MinRange, minWeaponDistance);
			}

			float distance = (position - hostileUnit.CurrentPosition).magnitude;

			if (distance < minWeaponDistance)
			{
				return -1.0f;
			}
			else
			{
				return 0.0f;
			}
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferNoCloserThanMinDistToHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferNoCloserThanMinDistToHostileFactorWeight;
		}
	}

	public class PreferFacingHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer facing hostile"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			Vector3 vectorFromUsToHostile = hostileUnit.CurrentPosition - position;

			Quaternion rotationQuat = Quaternion.Euler(0, angle, 0);
			Vector3 myForward = rotationQuat * Vector3.forward;

			float angleInDegreesToTarget = Vector3.Angle(myForward, vectorFromUsToHostile);

			// 0 degrees -> 1.0
			// 90 degrees -> 0.0
			// 180 degrees -> -1.0
			float angleFactor = 1.0f - angleInDegreesToTarget / 90.0f;

			return angleFactor * FactorUtil.HostileFactor(unit, hostileUnit);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferFacingHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferFacingHostileFactorWeight;
		}
	}

	public class PreferLowerLOSCountToHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer lower LOS to hostile"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// assume buildings can't see us.
				return 0.0f;
			}

			float maxDistance = unit.Combat.LOS.GetAdjustedSpotterRange(unit, hostileUnit as AbstractActor);
			bool hasLOS = unit.Combat.LOS.HasLineOfSightHeadToHead(unit, position, hostileActor, hostileUnit.CurrentPosition, maxDistance);

			return hasLOS ? 0.0f : 1.0f;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLOSToFewestHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLOSToFewestHostileFactorWeight;
		}
	}

	public class PreferHigherLOSCountToHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer LOS to most hostiles"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// assume buildings can't see us.
				return 0.0f;
			}

			float maxDistance = unit.Combat.LOS.GetAdjustedSpotterRange(unit, hostileUnit as AbstractActor);
			bool hasLOS = unit.Combat.LOS.HasLineOfSightHeadToHead(unit, position, hostileActor, hostileUnit.CurrentPosition, maxDistance);

			return hasLOS ? 1.0f : 0.0f;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLOSToMostHostilesFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLOSToMostHostilesFactorWeight;
		}
	}

	public class PreferWeakerArmorOfHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer lower armor of hostile"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// all facings for buildings are equivalent; arbitrarily return 0.
				return 0.0f;
			}
			float expectedArmor = hostileActor.ExpectedRelativeArmorFromAttackerWorldPosition(position, hostileUnit.CurrentPosition, hostileUnit.CurrentRotation);
			return -expectedArmor; // less armor is good!
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferAttackingLowerArmorHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferAttackingLowerArmorHostileFactorWeight;
		}
	}

	public class PreferPresentingStrongerArmorToHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer presenting higher armor to hostile"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			Quaternion rotQuat = Quaternion.Euler(0, angle, 0);

			if (!unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_AllowTurningRearArmorToEnemy).BoolVal)
			{
				// If we've turned off the ability to turn your rear armor towards the enemy when aggressive, just return a 0 value in that case.

				AttackDirection attackDirection = unit.Combat.HitLocation.GetAttackDirection(hostileUnit.CurrentPosition, position, rotQuat);
				if ((attackDirection == AttackDirection.FromBack) && (unit.BehaviorTree.mood == AIMood.Aggressive))
				{
					return 0.0f;
				}
			}

			float expectedArmor = unit.ExpectedRelativeArmorFromAttackerWorldPosition(hostileUnit.CurrentPosition, position, rotQuat);
			return expectedArmor; // more armor is good!
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferPresentingHigherArmorToHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferPresentingHigherArmorToHostileFactorWeight;
		}
	}

	public class PreferInsideMeleeDistanceToHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer within melee distance to hostile"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			float distance = (position - hostileUnit.CurrentPosition).magnitude;

			float meleeDistance = unit.MaxMeleeEngageRangeDistance;

			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				return 0.0f;
			}

			if (distance < meleeDistance)
			{
				return 1.0f;
			}

			// falloff like 1/x, so we're at 0.5 when we're at 2x melee range.
			return (meleeDistance / distance);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferInsideMeleeRangeFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferInsideMeleeRangeFactorWeight;
		}
	}

	public class PreferBeingBehindBracedHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer to be behind braced hostile targets"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			bool isBraced = (hostileActor != null) && (hostileActor.BracedLastRound);

			if (!isBraced)
			{
				return 0.0f;
			}

			// cut and pasted from attacking behind influence factor

			Vector3 vectorFromHostileToUs = position - hostileUnit.CurrentPosition;

			Vector3 hostileForward = hostileUnit.CurrentRotation * Vector3.forward;

			float angleInDegreesToTarget = Vector3.Angle(hostileForward, vectorFromHostileToUs);

			// 180 degrees -> 1.0
			// 135 degrees -> 0.0
			// 90 degrees -> -1.0
			// 0 degrees -> -1.0

			float angleFactor = Mathf.Max(-1.0f, angleInDegreesToTarget / 45.0f - 3.0f);

			return angleFactor * FactorUtil.HostileFactor(unit, hostileUnit);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferBeingBehindBracedHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferBeingBehindBracedHostileFactorWeight;
		}
	}

	public class PreferLethalDamageToRearArcFromHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "prefer to not to leave my rear armor open for lethal shots from hostiles"; } }

		// this code is duplicated - should be consolidated into AIUtil?
		protected float expectedDamageForShooting(AbstractActor shootingUnit, ICombatant targetUnit, Vector3 targetPosition, Quaternion targetRotation, bool targetIsEvasive, bool targetIsJumping)
		{
			float expectedDamage = 0.0f;
			for (int weaponIndex = 0; weaponIndex < shootingUnit.Weapons.Count; ++weaponIndex)
			{
				Weapon weapon = shootingUnit.Weapons[weaponIndex];

				if (!weapon.CanFire)
				{
					continue;
				}

				Quaternion shooterRotation = Quaternion.LookRotation(targetPosition - shootingUnit.CurrentPosition);

				if (shootingUnit.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(shootingUnit, targetUnit, weapon.MaxRange, shootingUnit.CurrentPosition, shooterRotation, targetPosition, targetRotation, weapon.IndirectFireCapable) &&
					weapon.WillFireAtTargetFromPosition(targetUnit, shootingUnit.CurrentPosition, shooterRotation))
				{
					int numShots = weapon.ShotsWhenFired;
					// handle hypothetical evasive effects
					AbstractActor targetActor = targetUnit as AbstractActor;
					float toHit;
					if (targetActor != null)
					{
						int realPips = targetActor.EvasivePipsCurrent;
						targetActor.EvasivePipsCurrent = targetActor.GetEvasivePipsResult((targetPosition - targetActor.CurrentPosition).magnitude,
							targetIsJumping, targetIsEvasive, false);
						toHit = weapon.GetToHitFromPosition(targetUnit, 1, shootingUnit.CurrentPosition, targetPosition, true, targetIsEvasive); // TODO (DAVE) : 1 = attacking a single target. Once AI can multi-target, this should reflect the number of targets
						targetActor.EvasivePipsCurrent = realPips;
					}
					else
					{
						toHit = weapon.GetToHitFromPosition(targetUnit, 1, shootingUnit.CurrentPosition, targetPosition, true, targetIsEvasive); // TODO (DAVE) : 1 = attacking a single target. Once AI can multi-target, this should reflect the number of targets
					}
					float damagePerShot = weapon.DamagePerShotFromPosition(MeleeAttackType.NotSet, shootingUnit.CurrentPosition, targetUnit);
					float heatDamagePerShot = (1 + (weapon.HeatDamagePerShot)); // Factoring in heatDamagePerShot. +1, since most weapons deal 0 heat Dmg
					expectedDamage += numShots * toHit * damagePerShot * heatDamagePerShot;
				}
			}
			return expectedDamage;
		}

		float getRearArmorPlusCriticalStructure(Mech unit)
		{
			float rearMult = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CenterTorsoRearArmorMultiplierForRearArc).FloatVal;
			float rearArmor = unit.ArmorForLocation((int)ArmorLocation.CenterTorsoRear);
			float criticalStructure = unit.StructureForLocation((int)ChassisLocations.CenterTorso);
			return rearArmor * rearMult + criticalStructure;
		}

		float getRearArmorPlusCriticalStructure(Vehicle unit)
		{
			float rearMult = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CenterTorsoRearArmorMultiplierForRearArc).FloatVal;
			float rearArmor = unit.ArmorForLocation((int)VehicleChassisLocations.Rear);
			float criticalStructure = unit.StructureForLocation((int)VehicleChassisLocations.Rear);

			return rearArmor * rearMult + criticalStructure;
		}

		float getRearArmorPlusCriticalStructure(Turret unit)
		{
			return 0.0f;
		}


		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// doesn't contribute - any value would be fine.
				return 0.0f;
			}

			// can this hostile get behind me at this location?
			Quaternion rotation = Quaternion.Euler(0, angle, 0);
			bool canGetBehind = AIUtil.CanUnitGetBehindUnit(unit, position, rotation, hostileActor);

			if (!canGetBehind)
			{
				return 0.0f;
			}

			//Quaternion hostileRotation = Quaternion.LookRotation(position - hostileUnit.CurrentPosition);
			float expectedDamage = expectedDamageForShooting(hostileActor, unit, position, rotation, moveType == MoveType.Sprinting, moveType == MoveType.Jumping);

			float rearArmorPlusStructure = 0;
			Mech m = unit as Mech;
			Vehicle v = unit as Vehicle;
			if (m != null)
			{
				rearArmorPlusStructure = getRearArmorPlusCriticalStructure(m);
			}
			else if (v != null)
			{
				rearArmorPlusStructure = getRearArmorPlusCriticalStructure(v);
			}
			else
			{
				// don't really care about this for turrets
				return 0.0f;
			}

			float damageRatio = expectedDamage / rearArmorPlusStructure;

			float overkillFactorLow = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OverkillThresholdLowForRearArcPositionFactor).FloatVal / 100.0f;
			float overkillFactorHigh = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OverkillThresholdHighForRearArcPositionFactor).FloatVal / 100.0f;

			//float lethalityRatioLow = damageRatio / overkillFactorLow;
			//float lethalityRatioHigh = damageRatio / overkillFactorHigh;

			if (damageRatio <= overkillFactorLow)
			{
				return 0.0f;
			}
			if (damageRatio >= overkillFactorHigh)
			{
				return 1.0f;
			}

			return (damageRatio - overkillFactorLow) / (overkillFactorHigh - overkillFactorLow);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferLethalDamageToRearArcFromHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferLethalDamageToRearArcFromHostileFactorWeight;
		}
	}

	public class AppetitivePreferBehindHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "Prefer to approach the rear arc of enemies"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// doesn't contribute - any value would be fine.
				return 0.0f;
			}

			float sprintExclusionRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SprintExclusionRadius).FloatVal;
			float maxRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_AppetitiveBehindMaximumRadius).FloatVal;

			float distance = (position - hostileUnit.CurrentPosition).magnitude;
			if ((distance < sprintExclusionRadius) || (distance > maxRadius))
			{
				return 0.0f;
			}

			Quaternion evaluationAngle = Quaternion.Euler(0, angle, 0);

			Vector3 rotatedForward = hostileUnit.CurrentRotation * Vector3.forward;
			Vector3 vectorToEvaluationPosition = position - hostileUnit.CurrentPosition;
			float bearingFromHostileToEvaluationPositionDegrees = Vector3.Angle(rotatedForward, vectorToEvaluationPosition);
			float bearingFromHostileToCurrentPositionDegrees = Vector3.Angle(rotatedForward, unit.CurrentPosition - hostileUnit.CurrentPosition);

			if (bearingFromHostileToCurrentPositionDegrees > 135.0)
			{
				// We don't want to sprint anymore if we're already behind this hostile.
				return 0.0f;
			}

			// fudge values to prefer facing around the enemy
			float resultingBearingToHostileDegrees = Vector3.Angle(evaluationAngle * Vector3.forward, position - hostileUnit.CurrentPosition);
			// 0 degrees -> 0
			// 90 degress -> 1
			// 180 degrees -> 0
			float facingFactor = 1.0f - Mathf.Abs(90.0f - resultingBearingToHostileDegrees) / 90.0f;

			float angleNorm = Mathf.Abs(bearingFromHostileToEvaluationPositionDegrees) / 180.0f;

			return angleNorm * facingFactor;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_AppetitivePreferApproachingRearArcOfHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintAppetitivePreferApproachingRearArcOfHostileFactorWeight;
		}
	}

	public class AppetitivePreferInIdealWeaponRangeHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "Prefer to approach the ideal weapon range to targets"; } }

		float idealDistance;

		public override void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			idealDistance = CalcIdealDistance(unit);
		}

		public static float CalcIdealDistance(AbstractActor unit)
		{
			float minWeaponRange = float.MaxValue;
			float maxWeaponRange = float.MinValue;

			for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
			{
				Weapon w = unit.Weapons[weaponIndex];
				if (!w.CanFire)
				{
					continue;
				}
				minWeaponRange = Mathf.Min(minWeaponRange, w.MinRange);
				maxWeaponRange = Mathf.Max(minWeaponRange, w.MaxRange);
			}

			if (maxWeaponRange < minWeaponRange)
			{
				// no weapons
				return 0.0f;
			}

            // HACK - any minWeaponRange below 24.0 isn't useful to us
            minWeaponRange = Mathf.Max(24.0f, minWeaponRange);
            maxWeaponRange = Mathf.Max(minWeaponRange, maxWeaponRange);

			int minSteps = 10; // base number of steps
			float minStepSize = 6.0f; // but stepsize doesn't get any smaller than this
			float step = Mathf.Max((maxWeaponRange - minWeaponRange) / minSteps, minStepSize);

			float maxDamage = float.MinValue;
			float maxDamageRange = 0.0f;

			for (float testDistance = minWeaponRange; testDistance <= maxWeaponRange; testDistance += step)
			{
				float totalDamageForThisDistance = 0.0f;

				for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
				{
					Weapon weapon = unit.Weapons[weaponIndex];

					if (!weapon.CanFire)
					{
						continue;
					}

					float baseChance = unit.Combat.ToHit.GetBaseToHitChance(unit);
					float modifier = unit.Combat.ToHit.GetRangeModifierForDist(weapon, testDistance);

					// convert modifier into to hit chance through MAGIC

					float toHitChance = baseChance - (modifier * 0.05f);

					float damage = weapon.DamagePerShot * weapon.ShotsWhenFired;
					float weaponDamageAtDistance = toHitChance * damage;

					totalDamageForThisDistance += weaponDamageAtDistance;
				}

				if (totalDamageForThisDistance > maxDamage)
				{
					maxDamage = totalDamageForThisDistance;
					maxDamageRange = testDistance;
				}
			}

			return maxDamageRange;
		}

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// doesn't contribute - any value would be fine.
				return 0.0f;
			}

			float currentDistanceToHostile = (unit.CurrentPosition - hostileActor.CurrentPosition).magnitude;

			float moveDist = ((moveType == MoveType.Sprinting) && unit.CanSprint) ? unit.MaxSprintDistance : unit.MaxWalkDistance;

			// Can we move to maxDamageRange? if so, return 0. This is the "appetitive" pattern.
			if ((currentDistanceToHostile > idealDistance - moveDist) &&
				(currentDistanceToHostile < idealDistance + moveDist))
			{
				return 0;
			}

            // else, return a ramp where
            // -1 is at max sprint distance away from ideal range
            // +1 is max sprint distance towards ideal range

            float maxMoveDistance = Mathf.Max(unit.MaxSprintDistance, unit.MaxWalkDistance);
            if (maxMoveDistance < 24)
            {
                return 0.0f;
            }

			float evaluatedDistanceToHostile = (position - hostileActor.CurrentPosition).magnitude;
			if (currentDistanceToHostile < idealDistance)
			{
				// too close to enemy, want more distance
				return (evaluatedDistanceToHostile - currentDistanceToHostile) / maxMoveDistance;
			}
			else
			{
				// too far away, want less distance
				return (currentDistanceToHostile - evaluatedDistanceToHostile) / maxMoveDistance;
			}
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_AppetitivePreferIdealWeaponRangeToHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintAppetitivePreferIdealWeaponRangeToHostileFactorWeight;
		}
	}

	public class PreferInsideSprintExclusionRadiusHostileFactor : InfluenceMapHostileFactor
	{
		public override string Name { get { return "Prefer to be a safe distance away from enemies when sprinting"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// doesn't contribute - any value would be fine.
				return 0.0f;
			}

			float sprintExclusionRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_SprintExclusionRadius).FloatVal;

			float distance = (position - hostileUnit.CurrentPosition).magnitude;
			return (distance < sprintExclusionRadius) ? 1.0f : 0.0f;
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferInsideSprintExclusionRadiusHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferInsideSprintExclusionRadiusHostileFactorWeight;
		}
	}

	public class PreferHigherFirepowerTakenFromHostileFactor : InfluenceMapHostileFactor
	{
		List<Weapon> weaponList;

		public override string Name { get { return "Prefer to take more firepower away from hostiles"; } }

		public override void InitEvaluationForPhaseForUnit(AbstractActor unit)
		{
			weaponList = new List<Weapon>();

			for (int wi = 0; wi < unit.Weapons.Count; ++wi)
			{
				Weapon w = unit.Weapons[wi];
				if (w.CanFire)
				{
					weaponList.Add(w);
				}
			}
		}

		public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
		{
			AbstractActor hostileActor = hostileUnit as AbstractActor;
			if (hostileActor == null)
			{
				// doesn't contribute.
				return 0.0f;
			}

			return AIAttackEvaluator.EvaluateFirepowerReductionFromAttack(unit, position, hostileUnit, hostileUnit.CurrentPosition, hostileUnit.CurrentRotation, weaponList, MeleeAttackType.NotSet);
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferHigherFirepowerTakenFromHostileFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferHigherFirepowerTakenFromHostileFactorWeight;
		}
	}

    public class PreferOptimalDistanceToHostileFactor : InfluenceMapHostileFactor
    {
        public override string Name { get { return "prefer optimal distance to hostile"; } }

        public override float EvaluateInfluenceMapFactorAtPositionWithHostile(AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit)
        {
            Vector3 hostilePosition = hostileUnit.CurrentPosition;
            float distance = (hostilePosition - position).magnitude;

            float optimalHostileDistance = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OptimalHostileDistance).FloatVal;

            if (distance < optimalHostileDistance)
            {
                return distance / optimalHostileDistance;
            }
            else
            {
                if (distance == 0.0f)
                {
                    return -1.0f;
                }
                return optimalHostileDistance / distance;
            }
        }

        public override BehaviorVariableName GetRegularMoveWeightBVName()
        {
            return BehaviorVariableName.Float_PreferOptimalDistanceToHostileFactorWeight;
        }

        public override BehaviorVariableName GetSprintMoveWeightBVName()
        {
            return BehaviorVariableName.Float_SprintPreferOptimalDistanceToHostileFactorWeight;
        }
    }

    #endregion // hostile factors

    #region ally factors

    public class PreferNoCloserThanPersonalSpaceToAllyFactor : InfluenceMapAllyFactor
	{
		public override string Name { get { return "prefer no closer than personal space to ally"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithAlly(AbstractActor unit, Vector3 position, float angle, ICombatant allyUnit)
		{
			Vector3 allyPosition = allyUnit.CurrentPosition;
			float distance = (allyPosition - position).magnitude;

			float personalSpaceDistance = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PersonalSpaceRadius).FloatVal;

			if (distance < personalSpaceDistance)
			{
				return -1.0f;
			}
			else
			{
				return 0.0f;
			}
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferNoCloserThanPersonalSpaceToAllyFactorWeight;
		}
		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferNoCloserThanPersonalSpaceToAllyFactorWeight;
		}
	}

	public class PreferOptimalDistanceToAllyFactor : InfluenceMapAllyFactor
	{
		public override string Name { get { return "prefer optimal distance to ally"; } }

		public override float EvaluateInfluenceMapFactorAtPositionWithAlly(AbstractActor unit, Vector3 position, float angle, ICombatant allyUnit)
		{
			Vector3 allyPosition = allyUnit.CurrentPosition;
			float distance = (allyPosition - position).magnitude;

			float optimalAllyDistance = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_OptimalAllyDistance).FloatVal;

			if (distance < optimalAllyDistance)
			{
				return distance / optimalAllyDistance;
			}
			else
			{
				if (distance == 0.0f)
				{
					return -1.0f;
				}
				return optimalAllyDistance / distance;
			}
		}

		public override BehaviorVariableName GetRegularMoveWeightBVName()
		{
			return BehaviorVariableName.Float_PreferOptimalDistanceToAllyFactorWeight;
		}

		public override BehaviorVariableName GetSprintMoveWeightBVName()
		{
			return BehaviorVariableName.Float_SprintPreferOptimalDistanceToAllyFactorWeight;
		}
	}
	#endregion // ally factors
}

