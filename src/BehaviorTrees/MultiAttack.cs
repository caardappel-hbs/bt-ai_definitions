using System.Collections.Generic;
using UnityEngine;

using BattleTech;

/// <summary>
/// This file contains the static (utility) class MultiAttack, which provides
/// methods for determining how to distribute weapons to targetable enemies.
/// </summary>

namespace BattleTech
{
	public static class MultiAttack
	{
		/// <summary>
		/// Attempt to kill the primary target.
		/// If there are not any weapons left over, we're done.
		/// Use the leftover weapons to try to kill secondary targets.
		/// If there are weapons left over and no kills to be had, assign one to each evasive target.
		/// If there are still weapons remaining, distribute randomly.
		/// </summary>
		/// <param name="unit"></param>
		/// <param name="evaluatedAttack"></param>
		/// <param name="primaryTargetIndex"></param>
		/// <returns>multi attack order, if possible, or null if a multi-attack doesn't make sense or is not possible</returns>
		public static MultiTargetAttackOrderInfo MakeMultiAttackOrder(AbstractActor unit, AttackEvaluator.AttackEvaluation evaluatedAttack, int primaryTargetIndex)
		{
            if ((unit.MaxTargets <= 1) || (evaluatedAttack.AttackType != AIUtil.AttackType.Shooting))
            {
                // cannot multi-attack
                return null;
            }

            ICombatant primaryTarget = unit.BehaviorTree.enemyUnits[primaryTargetIndex];

            /// indices into unit.BehaviorTree.enemyUnits for secondary targets.
            List<int> potentialSecondaryTargetIndices = new List<int>();

            Dictionary<string, bool> attackGeneratedForTargetGUID = new Dictionary<string, bool>();

			for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
			{
				ICombatant target = unit.BehaviorTree.enemyUnits[i];
                bool isPrimary = (target.GUID == primaryTarget.GUID);
                attackGeneratedForTargetGUID[target.GUID] = isPrimary;

				if (isPrimary || (target.IsDead) || unit.VisibilityToTargetUnit(target) != VisibilityLevel.LOSFull)
				{
					continue;
				}
                // make sure not to permit duplicate targets
                for (int dupIndex = 0; dupIndex < i; ++dupIndex)
                {
                    if (unit.BehaviorTree.enemyUnits[dupIndex].GUID == target.GUID)
                    {
                        continue;
                    }
                }
				potentialSecondaryTargetIndices.Add(i);
			}

			if (potentialSecondaryTargetIndices.Count == 0)
			{
				// no other targets available, fall back to doing a single attack
				return null;
			}

			float overkillThresholdPercentage = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_MultiTargetOverkillThreshold).FloatVal;
			float overkillThresholdFrac = overkillThresholdPercentage / 100.0f;

			List<Weapon> weaponsToKillPrimaryTarget = PartitionWeaponListToKillTarget(unit, evaluatedAttack.WeaponList, primaryTarget, overkillThresholdFrac);

			if ((weaponsToKillPrimaryTarget == null) || (weaponsToKillPrimaryTarget.Count == 0))
			{
				// can't kill primary target, so fall back to single attack.
				return null;
			}

			List<Weapon> weaponsSetAside = ListRemainder(evaluatedAttack.WeaponList, weaponsToKillPrimaryTarget);

			if ((weaponsSetAside == null) || (weaponsSetAside.Count == 0))
			{
				// can exactly kill the primary target with a single attack, don't bother multiattacking.
				return null;
			}

			Dictionary<string, List<Weapon>> weaponListsByTargetGUID = new Dictionary<string, List<Weapon>>();

			for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
			{
                ICombatant target = unit.BehaviorTree.enemyUnits[i];
				weaponListsByTargetGUID[target.GUID] = new List<Weapon>();
			}

			// Do the initial allocation to the first enemy unit
			weaponListsByTargetGUID[primaryTarget.GUID] = weaponsToKillPrimaryTarget;

			// Now, walk through the potential secondary targets and see if the set aside weapons could kill any of them.
			// If we find a kill, we want to set aside the weapons necessary for that kill.
			// if we find multiple kills, pick one, set aside those weapons, and solve again
			// with the remaining weapons and repeat until we can't find any more kills, or
			// we use up our number of attacks.
			// HACK optimization - only go through the list once, which biases our attacks
			// to prefer index #0.

			int usedAttacks = 1;

			for (int secondaryUnitDoubleIndex = 0; secondaryUnitDoubleIndex < potentialSecondaryTargetIndices.Count; ++secondaryUnitDoubleIndex)
			{
				if ((usedAttacks == unit.MaxTargets) || (weaponsSetAside.Count == 0))
				{
					break;
				}

                int secondaryUnitActualIndex = potentialSecondaryTargetIndices[secondaryUnitDoubleIndex];
                ICombatant secondaryUnit = unit.BehaviorTree.enemyUnits[secondaryUnitActualIndex];
                if (attackGeneratedForTargetGUID[secondaryUnit.GUID])
                {
                    continue;
                }
				List<Weapon> weaponsToKillSecondaryTarget = PartitionWeaponListToKillTarget(unit, weaponsSetAside, secondaryUnit, overkillThresholdFrac);
				if ((weaponsToKillSecondaryTarget == null) || (weaponsToKillSecondaryTarget.Count == 0))
				{
					continue;
				}
				// we've found a potential kill
				weaponsSetAside = ListRemainder(weaponsSetAside, weaponsToKillSecondaryTarget);
				weaponListsByTargetGUID[secondaryUnit.GUID] = weaponsToKillSecondaryTarget;
				++usedAttacks;
				attackGeneratedForTargetGUID[secondaryUnit.GUID] = true;
			}

			// Now, look for targets that have evasive pips that we could strip off.
			// Only use a single weapon per target

			for (int secondaryUnitDoubleIndex = 0; secondaryUnitDoubleIndex < potentialSecondaryTargetIndices.Count; ++secondaryUnitDoubleIndex)
			{
				int secondaryUnitActualIndex = potentialSecondaryTargetIndices[secondaryUnitDoubleIndex];
                ICombatant secondaryUnit = unit.BehaviorTree.enemyUnits[secondaryUnitActualIndex];
				if ((usedAttacks == unit.MaxTargets) || (weaponsSetAside.Count == 0))
				{
					break;
				}
				if (attackGeneratedForTargetGUID[secondaryUnit.GUID])
				{
                    // we already generated an attack for this target
					continue;
				}

				Mech secondaryMech = secondaryUnit as Mech;
				if ((secondaryMech != null) && (secondaryMech.IsEvasive))
				{
					Weapon stripWeapon = FindWeaponToHitTarget(unit, weaponsSetAside, secondaryMech);
					if (stripWeapon != null)
					{
						List<Weapon> stripList = new List<Weapon>();
						stripList.Add(stripWeapon);
						weaponListsByTargetGUID[secondaryUnit.GUID] = stripList;
						weaponsSetAside = ListRemainder(weaponsSetAside, stripList);
						++usedAttacks;
						attackGeneratedForTargetGUID[secondaryUnit.GUID] = true;
					}
				}
			}

			// Now, if we've got extra weapons, let's send them to target 0.
			if (weaponsSetAside.Count > 0)
			{
                weaponListsByTargetGUID[primaryTarget.GUID].AddRange(weaponsSetAside);
                weaponsSetAside.Clear();
			}

			int targetCount = 0;
            foreach (string guid in weaponListsByTargetGUID.Keys)
			{
				if (attackGeneratedForTargetGUID[guid])
				{
					++targetCount;
					Debug.Assert(weaponListsByTargetGUID[guid].Count > 0);
				}
			}

			// if, after all of that, we didn't end up having more than one target, go home and just do a single attack.
			if (targetCount <= 1)
			{
				return null;
			}

			MultiTargetAttackOrderInfo multiTargetOrder = new MultiTargetAttackOrderInfo();

            foreach(string guid in weaponListsByTargetGUID.Keys)
            {
				if (!attackGeneratedForTargetGUID[guid])
				{
					continue;
				}
                ICombatant target = null;
                for (int i = 0; i < unit.BehaviorTree.enemyUnits.Count; ++i)
                {
                    ICombatant testTarget = unit.BehaviorTree.enemyUnits[i];
                    if (testTarget.GUID == guid)
                    {
                        target = testTarget;
                        break;
                    }
                }
                Debug.Assert(target != null);
                if (target == null)
                {
                    continue;
                }
                AttackOrderInfo auxAttackOrder = new AttackOrderInfo(target);
				for (int wi = 0; wi < weaponListsByTargetGUID[guid].Count; ++wi)
				{
					auxAttackOrder.AddWeapon(weaponListsByTargetGUID[guid][wi]);
				}
				multiTargetOrder.AddAttack(auxAttackOrder);
			}

            if (!ValidateMultiAttackOrder(multiTargetOrder, unit))
            {
                return null;
            }
			return multiTargetOrder;
		}

		static List<Weapon> ListRemainder(List<Weapon> fullList, List<Weapon> takeAwayList)
		{
			List<Weapon> remainder = new List<Weapon>();
			for (int wi = 0; wi < fullList.Count; ++ wi)
			{
				Weapon w = fullList[wi];
				if (!takeAwayList.Contains(w))
				{
					remainder.Add(w);
				}
			}
			return remainder;
		}

		static Weapon FindWeaponToHitTarget(AbstractActor attacker, List<Weapon> weaponList, ICombatant target)
		{
			Mech targetMech = target as Mech;
			bool targetIsEvasive = ((targetMech != null) && (targetMech.IsEvasive));

			for (int wi = 0; wi < weaponList.Count; ++wi)
			{
				Weapon w = weaponList[wi];

                if (!attacker.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(
                    attacker, target, w.MaxRange, attacker.CurrentPosition, attacker.CurrentRotation,
                    target.CurrentPosition, target.CurrentRotation, w.IndirectFireCapable))
                {
                    continue;
                }

                float dmg = GetExpectedDamageForMultiTargetWeapon(attacker, attacker.CurrentPosition, target, target.CurrentPosition, targetIsEvasive, w, attacker.MaxTargets);
				if (dmg > 0)
				{
					return w;
				}
			}
			return null;
		}

		static List<Weapon> PartitionWeaponListToKillTarget(AbstractActor attacker, List<Weapon> weapons, ICombatant target, float overkillThresholdFrac)
		{
			// evaluate the weapons' damages to the target

			List<KeyValuePair<Weapon, float>> weaponsWithDamage = new List<KeyValuePair<Weapon, float>>();
			for (int wi = 0; wi < weapons.Count; ++wi)
			{
				Weapon w = weapons[wi];
				Mech targetMech = target as Mech;

				if (!attacker.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(
					attacker, target, w.MaxRange, attacker.CurrentPosition, attacker.CurrentRotation,
					target.CurrentPosition, target.CurrentRotation, w.IndirectFireCapable))
				{
					continue;
				}

				bool targetIsEvasive = ((targetMech != null) && (targetMech.IsEvasive));
				float dmg = GetExpectedDamageForMultiTargetWeapon(attacker, attacker.CurrentPosition, target, target.CurrentPosition, targetIsEvasive, w, attacker.MaxTargets);
				weaponsWithDamage.Add(new KeyValuePair<Weapon, float>(w, dmg));
			}

			// sort by damage
			weaponsWithDamage.Sort((x, y) => x.Value.CompareTo(y.Value));

			// reverse it to start with big damage
			weaponsWithDamage.Reverse();

			float totalDamage = 0.0f;
			float targetDamage = AttackEvaluator.MinHitPoints(target) * overkillThresholdFrac;

			List<Weapon> killList = new List<Weapon>();
			for (int wi = 0; wi < weaponsWithDamage.Count; ++wi)
			{
				Weapon w = weaponsWithDamage[wi].Key;
				float dmg = weaponsWithDamage[wi].Value;
				totalDamage += dmg;
				killList.Add(w);
				if (totalDamage >= targetDamage)
				{
					return killList;
				}
			}

			return null;
		}

		static float GetExpectedDamageForMultiTargetWeapon(AbstractActor attackerUnit, Vector3 attackPosition, ICombatant targetUnit, Vector3 targetPosition, bool targetIsEvasive, Weapon weapon, int numTargets)
		{
			if ((targetPosition - attackPosition).magnitude > weapon.MaxRange)
			{
				return 0.0f;
			}

            if (!attackerUnit.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(
                attackerUnit, targetUnit, weapon.MaxRange, attackerUnit.CurrentPosition, attackerUnit.CurrentRotation,
                targetUnit.CurrentPosition, targetUnit.CurrentRotation, weapon.IndirectFireCapable))
            {
                return 0.0f;
            }

            int numShots = weapon.ShotsWhenFired;
			float toHit = weapon.GetToHitFromPosition(targetUnit, numTargets, attackPosition, targetPosition, true, targetIsEvasive);

			float damagePerShot = weapon.DamagePerShotFromPosition(MeleeAttackType.NotSet, attackPosition, targetUnit);
			return numShots * toHit * damagePerShot;
		}

        static bool ValidateMultiAttackOrder(MultiTargetAttackOrderInfo order, AbstractActor unit)
        {
            AIUtil.LogAI("Multiattack validation", unit);
            for (int subAttackIndex = 0; subAttackIndex < order.SubTargetOrders.Count; ++subAttackIndex)
            {
                AttackOrderInfo subOrder = order.SubTargetOrders[subAttackIndex];

                AIUtil.LogAI(string.Format("SubAttack #{0}: target {1} {2}", subAttackIndex, subOrder.TargetUnit.GUID, subOrder.TargetUnit.DisplayName));

                foreach (Weapon w in subOrder.Weapons)
                {
                    AIUtil.LogAI(string.Format("  Weapon {0}", w.Name));
                }
            }

            List<string> targetGUIDs = new List<string>();
            foreach (AttackOrderInfo subOrder in order.SubTargetOrders)
            {
                string thisGUID = subOrder.TargetUnit.GUID;
                if (targetGUIDs.IndexOf(thisGUID) != -1)
                {
                    // found duplicated target GUIDs
                    AIUtil.LogAI("Multiattack error: Duplicated target GUIDs", unit);
                    return false;
                }

                foreach(Weapon w in subOrder.Weapons)
                {
                    if (!w.CanFire)
                    {
                        AIUtil.LogAI("Multiattack error: weapon that cannot fire", unit);
                        return false;
                    }

                    ICombatant target = subOrder.TargetUnit;

                    if (!unit.Combat.LOFCache.UnitHasLOFToTargetAtTargetPosition(
                        unit, target, w.MaxRange, unit.CurrentPosition, unit.CurrentRotation,
                        target.CurrentPosition, target.CurrentRotation, w.IndirectFireCapable))
                    {
                        AIUtil.LogAI("Multiattack error: weapon that cannot fire", unit);
                        return false;
                    }
                }
            }

            AIUtil.LogAI("Multiattack validates OK", unit);
            return true;
        }
	}
}
