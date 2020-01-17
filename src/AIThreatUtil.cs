using System.Collections.Generic;
using UnityEngine;
using BattleTech;

public class AIThreatUtil
{

	// Targets are divided into three categories:
	// Vulnerable Threats - sorted by threat
	// Non-Vulnerable Threats - sorted by threat
	// Non-Threats - sorted by distance
	private class SortMakeThreatHelper : IComparer<ICombatant>
	{
		AbstractActor thisUnit;
		float threatThreshold;
		float vulnerabilityThreshold;

		public SortMakeThreatHelper(AbstractActor thisUnit)
		{
			this.thisUnit = thisUnit;
			this.threatThreshold = thisUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_ThreatDamageRatioThreshold).FloatVal;
			this.vulnerabilityThreshold = thisUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_VulnerableDamageRatioThreshold).FloatVal;
		}

		public void DebugDumpState()
		{
			foreach (ICombatant target in thisUnit.BehaviorTree.enemyUnits)
			{
				Debug.Log("unit: " + target.DisplayName);
				Debug.Log("distance: " + (target.CurrentPosition - thisUnit.CurrentPosition).magnitude);
				Debug.Log("okr: " + ComputeVulnerabilityRatio(target, thisUnit.CanMove));
			}
		}

		private float GetExpectedDamageForAllWeaponsVsTarget(AbstractActor attackingUnit, ICombatant targetUnit, bool targetIsEvasive)
		{
			if (!AIUtil.UnitHasVisibilityToTargetFromCurrentPosition(attackingUnit, targetUnit))
			{
				// Our team can't see this hostile.
				return 0.0f;
			}

			float damage = 0;

			for (int weaponIndex = 0; weaponIndex < attackingUnit.Weapons.Count; ++weaponIndex)
			{
				Weapon weapon = attackingUnit.Weapons[weaponIndex];
				if (weapon.CanFire && weapon.WillFireAtTarget(targetUnit))
				{
					int numShots = weapon.ShotsWhenFired;
					float toHit = weapon.GetToHitFromPosition(targetUnit, 1, attackingUnit.CurrentPosition, targetUnit.CurrentPosition, true, targetIsEvasive); // TODO (DAVE) : 1 = attacking a single target. Once AI can multi-target, this should reflect the number of targets

					float damagePerShot = weapon.DamagePerShotFromPosition(MeleeAttackType.NotSet, attackingUnit.CurrentPosition, targetUnit);
					float heatDamagePerShot = 1 + (weapon.HeatDamagePerShot); // Factoring in heatDamagePerShot. +1, since most weapons deal 0 heat Dmg
					damage += numShots * toHit * damagePerShot * heatDamagePerShot;
				}
			}

			return damage;
		}

		public float ComputeVulnerabilityRatio(ICombatant targetUnit, bool iCanMove)
		{
			float hp;
			Mech targetMech = targetUnit as Mech;
			AbstractActor targetActor = targetUnit as AbstractActor;

			if ((!iCanMove) && (targetMech != null))
			{
				// only use armor exposed to us at our current position
				hp = targetMech.ExpectedRelativeArmorFromAttackerWorldPosition(thisUnit.CurrentPosition, targetMech.CurrentPosition, targetMech.CurrentRotation)
					+ targetMech.GetCurrentStructure(ChassisLocations.Head) + targetMech.GetCurrentStructure(ChassisLocations.CenterTorso);
			}
			else
			{
				// find the target's weakest armor
				hp = AttackEvaluator.MinHitPoints(targetUnit);
			}

			bool targetIsEvasive = (targetMech != null) && targetMech.IsEvasive;
			float expectedDamage = GetExpectedDamageForAllWeaponsVsTarget(thisUnit, targetUnit, targetIsEvasive);

			if ((targetActor != null) && (targetActor.IsVulnerableToCalledShots()))
			{
				float multiplier = targetActor.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_CalledShotVulnerabilityMultiplier).FloatVal;
				expectedDamage *= multiplier;
			}

			if (hp <= 0)
			{
				return float.MaxValue;
			}
			return expectedDamage / hp;
		}

		public float ComputeMaxThreatRatioOverLance(ICombatant shooterUnit, Lance lance)
		{
			float ratio = 0.0f;

            if (shooterUnit == null)
            {
                return 0.0f;
            }

			for (int unitIndex = 0; unitIndex < lance.unitGuids.Count; ++unitIndex)
			{
				string guid = lance.unitGuids[unitIndex];
				AbstractActor teamUnit = shooterUnit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(guid);
				if (teamUnit == null)
				{
					continue;
				}
				float teamUnitRatio = ComputeThreatRatio(shooterUnit, teamUnit);
				ratio = Mathf.Max(ratio, teamUnitRatio);
			}
			return ratio;
		}

        /// <summary>
        /// Calculate an "overkill" ratio; if the shooter did an alpha strike on the target's weakest location, what's the ratio of damage compared to the amount to blow through.
        /// </summary>
        /// <param name="shooterUnit"></param>
        /// <param name="targetUnit"></param>
        /// <returns></returns>
		public float ComputeThreatRatio(ICombatant shooterUnit, AbstractActor targetUnit)
		{
			AbstractActor shooterActor = shooterUnit as AbstractActor;
			if (shooterActor == null)
			{
				return 0.0f;
			}

			float targetHP = AttackEvaluator.MinHitPoints(targetUnit);
			float expectedDamageToTarget = GetExpectedDamageForAllWeaponsVsTarget(shooterActor, targetUnit, targetUnit.IsEvasive);

			if (targetHP < 1.0f)
			{
                targetHP = 1.0f;
			}
			return expectedDamageToTarget / targetHP;
		}

		public int Compare(ICombatant t1, ICombatant t2)
		{
			float threatRatio1 = ComputeMaxThreatRatioOverLance(t1, thisUnit.lance);
			float threatRatio2 = ComputeMaxThreatRatioOverLance(t2, thisUnit.lance);

			bool t1IsThreat = threatRatio1 > threatThreshold;
			bool t2IsThreat = threatRatio2 > threatThreshold;

			float vulnerabilityRatio1 = ComputeVulnerabilityRatio(t1, thisUnit.CanMove);
			float vulnerabilityRatio2 = ComputeVulnerabilityRatio(t2, thisUnit.CanMove);

			bool t1IsVulnerable = vulnerabilityRatio1 > vulnerabilityThreshold;
			bool t2IsVulnerable = vulnerabilityRatio2 > vulnerabilityThreshold;

			float dist1 = (t1.CurrentPosition - thisUnit.CurrentPosition).magnitude;
			float dist2 = (t2.CurrentPosition - thisUnit.CurrentPosition).magnitude;

			if (t1IsVulnerable && t2IsVulnerable)
			{
				if (t1IsThreat && t2IsThreat)
				{
					// deliberately reversed
					return Comparer<float>.Default.Compare(vulnerabilityRatio2, vulnerabilityRatio1);
				}
				else if (t1IsThreat)
				{
					return -1;
				}
				else if (t2IsThreat)
				{
					return 1;
				}
				// deliberately reversed
				return Comparer<float>.Default.Compare(vulnerabilityRatio2, vulnerabilityRatio1);
			}

			if (t1IsVulnerable)
			{
				return -1;
			}
			if (t2IsVulnerable)
			{
				return 1;
			}

			// sorted correctly, higher distance is less desireable
			return Comparer<float>.Default.Compare(dist1, dist2);
		}
	}

	public static void SortHostileUnitsByThreat(AbstractActor thisUnit, List<ICombatant> units)
	{
		SortMakeThreatHelper smth = new SortMakeThreatHelper(thisUnit);

		units.Sort(smth);

		string logFilename = thisUnit.Combat.AILogCache.MakeFilename("targ_sort");
		System.Text.StringBuilder targSortSB = new System.Text.StringBuilder();
		for (int i = 0; i < units.Count; ++i)
		{
			ICombatant unit = units[i];
			AbstractActor targetActor = unit as AbstractActor;
			if (targetActor == null)
			{
				targSortSB.AppendLine(string.Format("{0}  {1} (not AbstractActor)", i, unit.DisplayName));
				continue;
			}

			targSortSB.AppendLine(string.Format("{0}  {1}  threatRatio: {2} vulnerabilityRatio: {3}", i, unit.DisplayName, GetThreatRatio(thisUnit, targetActor), smth.ComputeVulnerabilityRatio(targetActor, thisUnit.CanMove)));
		}

		thisUnit.Combat.AILogCache.AddLogData(logFilename, targSortSB.ToString());
	}

	public static float GetThreatRatio(AbstractActor thisUnit, AbstractActor targetUnit)
	{
		SortMakeThreatHelper smth = new SortMakeThreatHelper(thisUnit);

		return smth.ComputeMaxThreatRatioOverLance(targetUnit, thisUnit.lance);
	}
}
