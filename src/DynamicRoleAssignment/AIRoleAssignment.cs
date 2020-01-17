using System;
using System.Collections.Generic;
using UnityEngine;


namespace BattleTech
{
	public class AIRoleAssignment
	{
		const string NONCOMBATANT_ROLE_TAG = "unit_noncombatant";
		const string SCOUT_ROLE_TAG = "unit_role_scout";
		const string ROLE_TAG_PREFIX = "unit_role_";

		class UnitRoleAssignmentRecord : IEquatable<UnitRoleAssignmentRecord>
		{
			public AbstractActor unit;
			public UnitRole role;

			public UnitRoleAssignmentRecord(AbstractActor unit, UnitRole role)
			{
				this.unit = unit;
				this.role = role;
			}

			public bool Equals(UnitRoleAssignmentRecord other)
			{
				return ((other.unit == this.unit) && (other.role == this.role));
			}

			override public bool Equals(object other)
			{
				UnitRoleAssignmentRecord otherRecord = other as UnitRoleAssignmentRecord;
				return ((otherRecord != null) && (this.Equals(otherRecord)));
			}

			override public int GetHashCode()
			{
				return unit.GetHashCode() + role.GetHashCode();
			}
		}

		static float getAbstractRoleTagMultiplier(CombatGameState combat, UnitRole role)
		{
			switch (role)
			{
				case UnitRole.Brawler:
					return combat.Constants.DynamicAIRoleConstants.brawlerTagMultiplier;
				case UnitRole.Sniper:
					return combat.Constants.DynamicAIRoleConstants.sniperTagMultiplier;
				case UnitRole.Spotter:
					Debug.LogError("spotter is deprecated");
					return 0.0f;
				case UnitRole.Flanker:
					Debug.LogError("flanker is deprecated");
					return 0.0f;
				case UnitRole.Scout:
					Debug.LogError("scouts do not use tag multipliers");
					return 0.0f;
				case UnitRole.LastManStanding:
					Debug.LogError("lastManStanding does not use tag multipliers");
					return 0.0f;
				default:
					Debug.LogError("unexpected role: " + role);
					return 1.0f;
			}
		}

		static float getRoleTagMultiplierForUnit(AbstractActor unit, UnitRole role)
		{
			bool match = false;
			string roleName = ROLE_TAG_PREFIX + role.ToString().ToLower();
			if (unit.GetTags().Contains(roleName))
			{
				match = true;
			}
			Mech mech = unit as Mech;
			if ((mech != null) && (mech.MechDef.MechTags.Contains(roleName)))
			{
				match = true;
			}

			return (match ? getAbstractRoleTagMultiplier(unit.Combat, role) : 1.0f);
		}

		public static void AssignRoleToUnit(AbstractActor unit, List<AbstractActor> otherUnits)
		{
			if (unit.BehaviorTree.HasPriorityTargets() ||
				(!unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_UseDynamicLanceRoles).BoolVal))
			{
				// don't assign dynamic roles when there's a priority target
				unit.DynamicUnitRole = UnitRole.Undefined;
				return;
			}

			if ((unit.StaticUnitRole == UnitRole.Turret) || (unit.StaticUnitRole == UnitRole.Vehicle && (!UnitIsECMRole(unit) && !UnitIsActiveProbe(unit))))
			{
				// don't assign roles to turrets or vehicles, except if they are ECM vehicles - Allie
				return;
			}

			//Debug.Log("trying to assign role to " + unit.DisplayName);
			otherUnits = otherUnits.FindAll(x => (x != unit) &&
				(x.StaticUnitRole != UnitRole.Turret) &&
				(x.StaticUnitRole != UnitRole.Vehicle) &&
				(!x.IsDead));

			//Debug.Log("other unit count: " + otherUnits.Count);

			// and now a list of the other interesing units plus our selected unit
			List<AbstractActor> allUnits = new List<AbstractActor>();
			allUnits.Add(unit);
			for (int unitIndex = 0; unitIndex < otherUnits.Count; ++unitIndex)
			{
				allUnits.Add(otherUnits[unitIndex]);
			}

            // OK So we do want to give an ECM Carrier role to ECM carrying vehicles.. - Allie
            if (unit.StaticUnitRole == UnitRole.Vehicle)
            {
                Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
                if (UnitIsEWE(unit))
                    assignmentDict[unit] = UnitRole.Ewe;
                else if (UnitIsECMRole(unit))
                    assignmentDict[unit] = UnitRole.EcmCarrier;
                else if (UnitIsActiveProbe(unit))
                    assignmentDict[unit] = UnitRole.ActiveProbe;
                else
                    return;

                applyAssignments(assignmentDict, allUnits, true);
                return;
            }

            // lastManStanding is its own thing (sigh)
            if (UnitIsLastManStandingRole(unit))
			{
				Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
				assignmentDict[unit] = UnitRole.LastManStanding;
				applyAssignments(assignmentDict, allUnits);
				return;
			}

			// noncombatant units are their own thing (sigh)
			if (UnitIsNonCombatantRole(unit))
			{
				Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
				assignmentDict[unit] = UnitRole.NonCombatant;
				applyAssignments(assignmentDict, allUnits);
				return;
			}

			// melee only units are their own thing (sigh)
			if (UnitIsMeleeOnlyRole(unit))
			{
				Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
				assignmentDict[unit] = UnitRole.MeleeOnly;
				applyAssignments(assignmentDict, allUnits);
				return;
			}

            if (UnitIsEWE(unit))
            {
                Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
                assignmentDict[unit] = UnitRole.Ewe;
                applyAssignments(assignmentDict, allUnits);
                return;
            }

            if (UnitIsActiveProbe(unit))
            {
                Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
                assignmentDict[unit] = UnitRole.ActiveProbe;
                applyAssignments(assignmentDict, allUnits);
                return;
            }

            if (UnitIsECMRole(unit))
            {
                Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
                assignmentDict[unit] = UnitRole.EcmCarrier;
                applyAssignments(assignmentDict, allUnits);
                return;
            }


			// scouts are their own thing (sigh)
			if (UnitMustBeScout(unit))
			{
				Dictionary<AbstractActor, UnitRole> assignmentDict = new Dictionary<AbstractActor, UnitRole>();
				assignmentDict[unit] = UnitRole.Scout;
				applyAssignments(assignmentDict, allUnits);
				return;
			}

			// first check to see if we're meeting our minimums. e.g. at startup, we won't.
			if (!unitsMeetMinimums(allUnits))
			{
				fillOutMinimums(allUnits);
			}

			List<Dictionary<AbstractActor, UnitRole>> possibleAssignments = new List<Dictionary<AbstractActor, UnitRole>>();

			UnitRole[] dynamicRoles = {
				UnitRole.Brawler,
				UnitRole.Sniper,
				// UnitRole.Scout,   // scouts don't use the normal evaluation routines
			};

			// do the raw evaluation, without considering role tags
			Dictionary<UnitRoleAssignmentRecord, float> unNormalizedRoleEvaluations = new Dictionary<UnitRoleAssignmentRecord, float>();

			for (int unitIndex = 0; unitIndex < allUnits.Count; ++unitIndex)
			{
				AbstractActor assignUnit = allUnits[unitIndex];
				for (int roleIndex = 0; roleIndex < dynamicRoles.Length; ++roleIndex)
				{
					UnitRole assignRole = dynamicRoles[roleIndex];

					UnitRoleAssignmentRecord assignmentRecord = new UnitRoleAssignmentRecord(assignUnit, assignRole);
					float evaluation = EvaluateAssignmentForUnit(assignUnit, assignRole);
					unNormalizedRoleEvaluations[assignmentRecord] = evaluation;
				}
			}

			// normalize the evaluations across roles, so that 0.0 is the worst brawler and 1.0 is the best
			// do the raw evaluation, without considering role tags
			Dictionary<UnitRoleAssignmentRecord, float> normalizedRoleEvaluations = new Dictionary<UnitRoleAssignmentRecord, float>();
			for (int roleIndex = 0; roleIndex < dynamicRoles.Length; ++roleIndex)
			{
				UnitRole assignRole = dynamicRoles[roleIndex];

				float maxValue = float.MinValue;
				float minValue = float.MaxValue;

				for (int unitIndex = 0; unitIndex < allUnits.Count; ++unitIndex)
				{
					AbstractActor assignUnit = allUnits[unitIndex];
					float val = unNormalizedRoleEvaluations[new UnitRoleAssignmentRecord(assignUnit, assignRole)];
					maxValue = Mathf.Max(maxValue, val);
					minValue = Mathf.Min(minValue, val);
				}
				float valueRange = maxValue - minValue;
				for (int unitIndex = 0; unitIndex < allUnits.Count; ++unitIndex)
				{
					AbstractActor assignUnit = allUnits[unitIndex];
					float val = unNormalizedRoleEvaluations[new UnitRoleAssignmentRecord(assignUnit, assignRole)];

					float normalized = valueRange == 0.0f ? 1.0f : (val - minValue) / valueRange;

					normalizedRoleEvaluations[new UnitRoleAssignmentRecord(assignUnit, assignRole)] = normalized;
				}
			}

			// now add in the role tag multipliers
			Dictionary<UnitRoleAssignmentRecord, float> normalizedRoleEvaluationsWithTagMultipliers = new Dictionary<UnitRoleAssignmentRecord, float>();
			for (int roleIndex = 0; roleIndex < dynamicRoles.Length; ++roleIndex)
			{
				UnitRole assignRole = dynamicRoles[roleIndex];

				for (int unitIndex = 0; unitIndex < allUnits.Count; ++unitIndex)
				{
					AbstractActor assignUnit = allUnits[unitIndex];
					UnitRoleAssignmentRecord assignmentRecord = new UnitRoleAssignmentRecord(assignUnit, assignRole);
					float val = normalizedRoleEvaluations[assignmentRecord];
					float scaled = val * getRoleTagMultiplierForUnit(assignUnit, assignRole);
					normalizedRoleEvaluationsWithTagMultipliers[assignmentRecord] = scaled;
				}
			}


			// first, consider just assigning this unit to a new role (abandoning the old role)
			for (int roleIndex = 0; roleIndex < dynamicRoles.Length; ++roleIndex)
			{
				UnitRole newRole = dynamicRoles[roleIndex];
				if (newRole == unit.DynamicUnitRole)
				{
					continue;
				}

				Dictionary<AbstractActor, UnitRole> newAssignment = new Dictionary<AbstractActor, UnitRole>();
				newAssignment[unit] = newRole;
				//Debug.LogFormat("Assignment {0} abandonOld", possibleAssignments.Count);
				//Debug.LogFormat("Assigning {0} to role {1})", unit.DisplayName, newRole);
				possibleAssignments.Add(newAssignment);
			}

			// now, try swapping with each of the other units
			if (unit.DynamicUnitRole != UnitRole.Undefined)
			{
				for (int otherUnitIndex = 0; otherUnitIndex < otherUnits.Count; ++otherUnitIndex)
				{
					AbstractActor otherUnit = otherUnits[otherUnitIndex];
					if ((otherUnit.DynamicUnitRole == unit.DynamicUnitRole) || (otherUnit.DynamicUnitRole == UnitRole.Undefined))
					{
						// can't swap with someone who's the same as me, and don't want to swap with an undefined role
						continue;
					}

					Dictionary<AbstractActor, UnitRole> newAssignment = new Dictionary<AbstractActor, UnitRole>();
					newAssignment[unit] = otherUnit.DynamicUnitRole;
					newAssignment[otherUnit] = unit.DynamicUnitRole;
					//Debug.LogFormat("Assignment {0} swap", possibleAssignments.Count);
					//Debug.LogFormat("Assigning {0} to role {1}", unit.DisplayName, otherUnit.DynamicUnitRole);
					//Debug.LogFormat("Assigning {0} to role {1}", otherUnit.DisplayName, unit.DynamicUnitRole);
					possibleAssignments.Add(newAssignment);
				}
			}

			// now, iterate over all the possible assignements and find the one with the highest value

			float bestNonPenaltyEvaluationScore = float.MinValue;
			int bestNonPenaltyIndex = -1;

			float bestPenaltyEvaluationScore = float.MinValue;
			int bestPenaltyIndex = -1;

			for (int assignmentIndex = 0; assignmentIndex < possibleAssignments.Count; ++assignmentIndex)
			{
				float assignmentEvaluationScore = evaluateAssignments(possibleAssignments[assignmentIndex], allUnits, normalizedRoleEvaluationsWithTagMultipliers);

				if (assignmentEvaluationScore > 0.0f)
				{
					if (assignmentEvaluationScore > bestNonPenaltyEvaluationScore)
					{
						bestNonPenaltyIndex = assignmentIndex;
						bestNonPenaltyEvaluationScore = assignmentEvaluationScore;
					}
				}
				else
				{
					if (assignmentEvaluationScore > bestPenaltyEvaluationScore)
					{
						bestPenaltyIndex = assignmentIndex;
						bestPenaltyEvaluationScore = assignmentEvaluationScore;
					}
				}
			}

			float statusQuoEvaluationScore = evaluateAssignments(null, allUnits, normalizedRoleEvaluationsWithTagMultipliers);

			bool isUnassigned = unit.DynamicUnitRole == UnitRole.Undefined;

			if (bestNonPenaltyIndex >= 0)
			{
				float ratio = statusQuoEvaluationScore <= 0 ? float.MaxValue : (bestNonPenaltyEvaluationScore - statusQuoEvaluationScore) / statusQuoEvaluationScore;

				if (isUnassigned || (ratio > unit.Combat.Constants.DynamicAIRoleConstants.hysteresis))
				{
					//Debug.LogFormat("applying {0} nonpenalty", bestNonPenaltyIndex);
					applyAssignments(possibleAssignments[bestNonPenaltyIndex], allUnits);
				}
			}
			else
			{
				if (isUnassigned || (bestPenaltyEvaluationScore > statusQuoEvaluationScore))
				{
					//Debug.LogFormat("applying {0} penalty", bestPenaltyIndex);
					applyAssignments(possibleAssignments[bestPenaltyIndex], allUnits);
				}
			}
			if (unit.DynamicUnitRole == UnitRole.Undefined)
			{
				Debug.LogError("Dynamic Role Assignment: chose to leave unit undefined");
				Debug.LogError("bestNonPenaltyIndex: " + bestNonPenaltyIndex);
				Debug.LogError("bestNonPenaltyEvaluationScore: " + bestNonPenaltyEvaluationScore);
				Debug.LogError("bestPenaltyIndex: " + bestPenaltyIndex);
				Debug.LogError("bestPenaltyEvaluationScore: " + bestPenaltyEvaluationScore);
			}
		}

		static float evaluateAssignments(Dictionary<AbstractActor, UnitRole> unitAssignments, List<AbstractActor> allUnits, Dictionary<UnitRoleAssignmentRecord, float> roleAssignmentDictionary)
		{
			float penalty = getLegalTeamCountPenalty(countRolesAfterAssignment(unitAssignments, allUnits), allUnits);
			if (penalty > 0)
			{
				return -penalty;
			}

			float score = 0.0f;

			for (int unitIndex = 0; unitIndex < allUnits.Count; ++unitIndex)
			{
				AbstractActor unit = allUnits[unitIndex];
				UnitRole role = unit.DynamicUnitRole;
				if ((unitAssignments != null) && (unitAssignments.ContainsKey(unit)))
				{
					role = unitAssignments[unit];
				}

				UnitRoleAssignmentRecord assignmentRecord = new UnitRoleAssignmentRecord(unit, role);
				// might not actually be there, e.g. if the unit is currently unassigned.
				if (roleAssignmentDictionary.ContainsKey(assignmentRecord))
				{
					score += roleAssignmentDictionary[assignmentRecord];
				}
			}
			return score;
		}

		static float getLegalTeamCountPenalty(Dictionary<UnitRole, int> countsDict, List<AbstractActor> units)
		{
			if (units.Count == 0)
			{
				return 0.0f;
			}

			CombatGameState combat = units[0].Combat;
			float unitCount = (float)units.Count;

			int brawlerCount = countsDict.ContainsKey(UnitRole.Brawler) ? countsDict[UnitRole.Brawler] : 0;
			int sniperCount = countsDict.ContainsKey(UnitRole.Sniper) ? countsDict[UnitRole.Sniper] : 0;

			float brawlerFrac = brawlerCount / unitCount;
			float sniperFrac = sniperCount / unitCount;

			float penalty = 0.0f;

			if (brawlerCount < combat.Constants.DynamicAIRoleConstants.brawlerMinAbs)
			{
				penalty += 1.0f;
			}
			if (sniperCount < combat.Constants.DynamicAIRoleConstants.sniperMinAbs)
			{
				penalty += 1.0f;
			}

			if (brawlerFrac > combat.Constants.DynamicAIRoleConstants.brawlerMaxFrac)
			{
				penalty += 1.0f;
			}
			if (sniperFrac > combat.Constants.DynamicAIRoleConstants.sniperMaxFrac)
			{
				penalty += 1.0f;
			}

			return penalty;
		}

		static void applyAssignments(Dictionary<AbstractActor, UnitRole> unitAssignments, List<AbstractActor> allUnits, bool forceVehicle = false)
		{
			foreach (AbstractActor unit in unitAssignments.Keys)
			{
				if ((unit.StaticUnitRole == UnitRole.Vehicle && !forceVehicle) ||
					(unit.StaticUnitRole == UnitRole.Turret))
				{
					Debug.LogError("should not be assigning roles to vehicles or turrets");
				}
				//Debug.Log(string.Format("Assigning {0} to have role {1}", unit.DisplayName, unitAssignments[unit]));
				unit.DynamicUnitRole = unitAssignments[unit];
			}
		}

		static Dictionary<UnitRole, int> countRolesAfterAssignment(Dictionary<AbstractActor, UnitRole> unitAssignments, List<AbstractActor> allUnits)
		{
			Dictionary<UnitRole, int> counts = new Dictionary<UnitRole, int>();
			counts[UnitRole.Brawler] = 0;
			counts[UnitRole.Sniper] = 0;

			for (int unitIndex = 0; unitIndex < allUnits.Count; ++unitIndex)
			{
				AbstractActor unit = allUnits[unitIndex];
				UnitRole role = unit.DynamicUnitRole;
				if ((unitAssignments != null) && (unitAssignments.ContainsKey(unit)))
				{
					role = unitAssignments[unit];
				}
				if (counts.ContainsKey(role))
				{
					counts[role] += 1;
				}
				else
				{
					counts[role] = 1;
				}
			}
			return counts;
		}

		static bool unitsMeetMinimums(List<AbstractActor> units)
		{
			if (units.Count == 0)
			{
				return true;
			}

			Dictionary<UnitRole, int> counts = countRolesAfterAssignment(null, units);
			CombatGameConstants constants = units[0].Combat.Constants;

			return ((counts[UnitRole.Brawler] >= constants.DynamicAIRoleConstants.brawlerMinAbs) &&
				(counts[UnitRole.Sniper] >= constants.DynamicAIRoleConstants.sniperMinAbs));
		}

		static void fillOutMinimums(List<AbstractActor> units)
		{
			if (units.Count == 0)
			{
				return;
			}

			CombatGameConstants constants = units[0].Combat.Constants;

			// TODO be smarter about doing an assignment based on priority and by fit
			int constrainedMinimumCount = constants.DynamicAIRoleConstants.brawlerMinAbs +
										  constants.DynamicAIRoleConstants.sniperMinAbs;

			if (units.Count < constrainedMinimumCount)
			{
				// this is where we'd fill out by priority

				for (int i = 0; i < units.Count; ++i)
				{
					UnitRole role = getRoleByPriorityIndex(constants, i);
					units[i].DynamicUnitRole = role;
				}
			}
			else
			{
				for (int i = 0; i < constrainedMinimumCount; ++i)
				{
					UnitRole role = getRoleByPriorityIndex(constants, i);
					units[i].DynamicUnitRole = role;
				}
			}
		}

		static UnitRole getRoleByPriorityIndex(CombatGameConstants constants, int index)
		{
			// TODO - I'm ignoring the priority value for now and just hardcoding it to be brawler, sniper, scout

			int[] counts = new int[3];
			counts[0] = constants.DynamicAIRoleConstants.brawlerMinAbs;
			counts[1] = constants.DynamicAIRoleConstants.sniperMinAbs;

			UnitRole[] dynamicRoles = {
				UnitRole.Brawler,
				UnitRole.Sniper
			};

			for (int roleIndex = 0; roleIndex < 3; ++roleIndex)
			{
				if (index < counts[roleIndex])
				{
					return dynamicRoles[roleIndex];
				}
				index -= counts[roleIndex];
			}

			return UnitRole.Undefined;
		}

		static float EvaluateAssignmentForUnit(AbstractActor unit, UnitRole role)
		{
			if (unit.IsDead)
			{
				return 0.0f;
			}

			switch (role)
			{
				case UnitRole.Brawler: return 1.0f;
				case UnitRole.Sniper: return EvaluateSniper(unit);
				case UnitRole.Scout: return 0.0f; // handled separately
				case UnitRole.LastManStanding: return 0.0f; // handled separately
				default:
					// don't know what to do with this
					return 0.0f;
			}
		}

		static float EvaluateWeaponDamageAtRange(CombatGameState combat, AbstractActor attacker, Weapon weapon, float range)
		{
			if (!weapon.CanFire)
			{
				return 0.0f;
			}

			float baseChance = combat.ToHit.GetBaseToHitChance(attacker);
			float modifier = combat.ToHit.GetRangeModifierForDist(weapon, range);

			// convert modifier into to hit chance through MAGIC

			float toHitChance = baseChance - (modifier * 0.05f);

			float damage = weapon.DamagePerShot * weapon.ShotsWhenFired;
			return toHitChance * damage;
		}

		static float getDamageCentroid(AbstractActor unit)
		{
			float minRange = float.MaxValue;
			float maxRange = float.MinValue;

			for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
			{
				Weapon w = unit.Weapons[weaponIndex];
				if (w.CanFire)
				{
					minRange = Mathf.Min(minRange, w.MinRange);
					maxRange = Mathf.Max(maxRange, w.MaxRange);
				}
			}
			if (minRange > maxRange)
			{
				return 0.0f;
			}
			if (minRange == maxRange)
			{
				return minRange;
			}

			const float MIN_SLICE_SIZE = 1.0f;
			const float MAX_SLICE_COUNT = 20.0f;
			float sliceWidth = Mathf.Max((maxRange - minRange) / MAX_SLICE_COUNT, MIN_SLICE_SIZE);

			float totalDamage = 0.0f;
			float accumulator = 0.0f;

			for (float range = minRange; range <= maxRange; range += sliceWidth)
			{
				float dmg = 0.0f;
				for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
				{
					Weapon w = unit.Weapons[weaponIndex];
					dmg += EvaluateWeaponDamageAtRange(unit.Combat, unit, w, range);
				}
				totalDamage += dmg;
				accumulator += (dmg * range);
			}
			if (totalDamage == 0.0f)
			{
				return 0.0f;
			}
			return (accumulator / totalDamage);
		}

		/// <summary>
		/// How well suited is this unit to being a sniper? Snipers need to do damage at long range.
		/// </summary>
		/// <returns>A value of how sniper-y this unit is</returns>
		/// <param name="unit">Unit.</param>
		static float EvaluateSniper(AbstractActor unit)
		{
			bool foundAnyIndirectWeapons = false;
			for (int weaponIndex = 0; weaponIndex < unit.Weapons.Count; ++weaponIndex)
			{
				Weapon w = unit.Weapons[weaponIndex];
				if ((!w.CanFire) && (w.IndirectFireCapable))
				{
					foundAnyIndirectWeapons = true;
					break;
				}
			}

			return getDamageCentroid(unit) * (foundAnyIndirectWeapons ? 2.0f : 1.0f);
		}

		/// <summary>
		/// Must this unit / may this unit be a scout? This is (currently?) only controlled by the designers.
		/// - scouts *must* have the scout tag
		/// - at least one living unit in the lance *must not* have the scout tag
		/// - if at least one living unit in the lance does not have the scout tag, you must take the scout role.
		/// </summary>
		/// <returns>whether the unit should be a scout</returns>
		/// <param name="unit">Unit.</param>
		static bool UnitMustBeScout(AbstractActor unit)
		{
			string roleName = SCOUT_ROLE_TAG;

			if (!unit.GetTags().Contains(roleName))
			{
				return false;
			}

			for (int unitIndex = 0; unitIndex < unit.Combat.AllActors.Count; ++unitIndex)
			{
				AbstractActor otherUnit = unit.Combat.AllActors[unitIndex];
				if ((otherUnit == unit) ||
					(otherUnit.lance != unit.lance) ||
					(otherUnit.IsDead))
				{
					continue;
				}
				if (!otherUnit.GetTags().Contains(roleName))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Must this unit / may this unit be "lastManStanding"? This is based strictly on being the only mech remaining on a lance.
		/// </summary>
		/// <returns>whether the unit should be "lastManStanding"</returns>
		/// <param name="unit">Unit.</param>
		static bool UnitIsLastManStandingRole(AbstractActor unit)
		{
			if (unit as Mech == null)
			{
				return false;
			}

			for (int unitIndex = 0; unitIndex < unit.Combat.AllActors.Count; ++unitIndex)
			{
				AbstractActor otherUnit = unit.Combat.AllActors[unitIndex];
				if ((otherUnit != unit) &&
					(otherUnit.lance == unit.lance) &&
					(!otherUnit.IsDead))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Must this unit / may this unit be "noncombatant"? This is based strictly on unit tags.
		/// </summary>
		/// <returns>whether the unit should be "noncombatant"</returns>
		/// <param name="unit">Unit.</param>
		static bool UnitIsNonCombatantRole(AbstractActor unit)
		{
			return unit.GetTags().Contains(NONCOMBATANT_ROLE_TAG);
		}

		/// <summary>
		/// Must this unit / may this unit be "melee only"? This is based strictly on unit weapons remaining.
		/// </summary>
		/// <returns>whether the unit should be "melee only"</returns>
		/// <param name="unit">Unit.</param>
		static bool UnitIsMeleeOnlyRole(AbstractActor unit)
		{
			Mech mech = unit as Mech;
			if (mech == null)
			{
				return false;
			}

			if (UnitIsNonCombatantRole(unit))
			{
				return false;
			}

			for (int wi = 0; wi < unit.Weapons.Count; ++wi)
			{
				Weapon w = unit.Weapons[wi];

				if ((w == mech.MeleeWeapon) ||
					(w == mech.DFAWeapon))
				{
					continue;
				}

				if (w.CanFire)
				{
					return false;
				}
			}
			return true;
		}

        static bool UnitIsECMRole(AbstractActor unit)
        {
            for (int auraIndex = 0; auraIndex < unit.AuraComponents.Count; ++auraIndex)
            {
                MechComponent component = unit.AuraComponents[auraIndex];

                for (int i = 0; i < component.componentDef.statusEffects.Length; ++i)
                {
                    EffectData effectData = component.componentDef.statusEffects[i];
                    if (effectData.targetingData.auraEffectType == AuraEffectType.ECM_GENERAL || effectData.targetingData.auraEffectType == AuraEffectType.ECM_GHOST)
                        return true;
                }
            }

            return false;
        }

        static bool UnitIsActiveProbe(AbstractActor unit)
        {
            return unit.HasActiveProbeAbility;
        }

        static bool UnitIsEWE(AbstractActor unit)
        {
            return UnitIsActiveProbe(unit) && UnitIsECMRole(unit);
        }
    }
}

