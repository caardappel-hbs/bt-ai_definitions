using System.Collections.Generic;
using UnityEngine;

namespace BattleTech
{
	public class AIAttackEvaluator
	{
		static public DamageExpectationRecord EvaluateAttack(AbstractActor attacker, Vector3 attackerPosition, ICombatant target, Vector3 targetPosition, Quaternion targetRotation, List<Weapon> weapons, MeleeAttackType attackType)
		{
			// for all weapons in an attack

			// figure out the locations that are likely to be hit
			// use HitTable to figure this out

			// for each location, figure out the chance to 
			// - do criticals (without breaching armor?)
			// - breach armor
			// - do structural damage
			// - do component damage (weapons get damaged, then destroyed)
			// - trigger ammo explosion
			// - lose the location
			// - lose sub-locations
			// - kill the mech

			// types.cs ConsolidateCriticalHitInfo
			// Mech.cs CheckForCrit
			// CombatCritChance GetCritChance

			DamageExpectationRecord root = new DamageExpectationRecord();

			for (int weaponIndex = 0; weaponIndex < weapons.Count; ++weaponIndex)
			{
				Weapon w = weapons[weaponIndex];

				// figure out chance to hit the target
				AbstractActor targetActor = target as AbstractActor;
				bool targetIsEvasive = (targetActor != null) && (targetActor.IsEvasive);
				float toHitProbability = w.GetToHitFromPosition(target, 1, attackerPosition, targetPosition, true, targetIsEvasive);

				DamageExpectationRecord weaponDamageExpectationRecord = new DamageExpectationRecord();
				root.AddChildRecord(toHitProbability, weaponDamageExpectationRecord);

				float expectedDamage = w.ShotsWhenFired * w.DamagePerShotFromPosition(attackType, attackerPosition, target);

				Mech targetMech = target as Mech;
				Vehicle targetVehicle = target as Vehicle;
				Turret targetTurret = target as Turret;
				Building targetBuilding = target as Building;
				if (targetMech != null)
				{
					evaluateWeaponAttackOnMech(expectedDamage, w, ref weaponDamageExpectationRecord, attackerPosition, targetMech, targetPosition, targetRotation);
				}
				else if (targetVehicle != null)
				{
					evaluateWeaponAttackOnVehicle(expectedDamage, w, ref weaponDamageExpectationRecord, attackerPosition, targetVehicle, targetPosition, targetRotation);
				}
				else if (targetTurret != null)
				{
					evaluateWeaponAttackOnTurret(expectedDamage, w, ref weaponDamageExpectationRecord, attackerPosition, targetTurret, targetPosition, targetRotation);
				}
				else if (targetBuilding != null)
				{
					evaluateWeaponAttackOnBuilding(expectedDamage, w, ref weaponDamageExpectationRecord, attackerPosition, targetBuilding, targetPosition, targetRotation);
				}
			}
			consolidateDamageExpectationRecord(ref root, target);

			return root;
		}

		static void evaluateWeaponAttackOnMech(float expectedDamage, Weapon w, ref DamageExpectationRecord damageExpectationRecord, Vector3 attackerPosition, Mech targetMech, Vector3 targetPosition, Quaternion targetRotation)
		{
			// use hit table to figure out where this will go
			Dictionary<ArmorLocation, float> locations = GetLocationDictionary(attackerPosition, targetMech, targetPosition, targetRotation);

			foreach (KeyValuePair<ArmorLocation, float> locKVP in locations)
			{
				ArmorLocation loc = locKVP.Key;
				float probability = locKVP.Value;

				DamageExpectationRecord locRecord = new DamageExpectationRecord();
				damageExpectationRecord.AddChildRecord(probability, locRecord);

				float existingArmor = targetMech.ArmorForLocation((int)loc);
				float armorThatWillBeRemoved = Mathf.Min(existingArmor, expectedDamage);
				float damageRemaining = expectedDamage - existingArmor;
				locRecord.AddArmorDamage(armorThatWillBeRemoved, loc);

				ChassisLocations sLoc = MechStructureRules.GetChassisLocationFromArmorLocation((ArmorLocation)loc);

				// there's a chance this hit will be a critical hit
				if (!targetMech.IsLocationDestroyed(sLoc))
				{
					float critChance = targetMech.Combat.CritChance.GetCritChance(targetMech, sLoc, w);

					if (critChance > 0)
					{
						DamageExpectationRecord critRecord = new DamageExpectationRecord();
						locRecord.AddChildRecord(critChance, critRecord);

						// iterate over components, apply one point of damage to each location.

						Dictionary<ComponentLocator, float> componentDict = getComponentDictionary(targetMech, sLoc);

						float probOfHittingAmmo = 0.0f;
						foreach (KeyValuePair<ComponentLocator, float> componentKVP in componentDict)
						{
							ComponentLocator compLoc = componentKVP.Key;
							MechComponent component = compLoc.GetComponent();
							float componentProbability = componentKVP.Value;

							DamageExpectationRecord componentRecord = new DamageExpectationRecord();
							critRecord.AddChildRecord(componentProbability, componentRecord);

							componentRecord.AddComponentDamage(1.0f, compLoc);

							// if this component is ammo, there's a chance we could lose this location and all child locations
							if (component.componentType == ComponentType.AmmunitionBox)
							{
								AmmunitionBox abComponent = component as AmmunitionBox;
								int remainingAmmo = abComponent.CurrentAmmo;
								int capacity = abComponent.ammunitionBoxDef.Capacity;
								float percentage = ((float)remainingAmmo) / ((float)capacity);

								if (percentage > 0.5f)
								{
									probOfHittingAmmo += componentProbability;
								}
							}
						}
						if (probOfHittingAmmo > 0.0f)
						{
							DamageExpectationRecord ammoBlownRecord = new DamageExpectationRecord();
							locRecord.AddChildRecord(probOfHittingAmmo, ammoBlownRecord);

							foreach (KeyValuePair<ComponentLocator, float> componentKVP in componentDict)
							{
								ComponentLocator compLoc = componentKVP.Key;
								ammoBlownRecord.AddComponentDamage(2.0f, compLoc);
							}
						}
					}
				}

				if (damageRemaining > 0)
				{
					// some goes in to the structure
					float currentStructure = targetMech.GetCurrentStructure(sLoc);

					float structureDamage = Mathf.Min(damageRemaining, currentStructure);
					float damageAfterStructure = damageRemaining - structureDamage;

					locRecord.AddStructureDamage(structureDamage, sLoc);

					if (damageAfterStructure > 0)
					{
						// some hits a component
						Dictionary<ComponentLocator, float> componentDict = getComponentDictionary(targetMech, sLoc);

						float probOfHittingAmmo = 0.0f;

						foreach (KeyValuePair<ComponentLocator, float> componentKVP in componentDict)
						{
							ComponentLocator compLoc = componentKVP.Key;
							MechComponent component = compLoc.GetComponent();
							float componentProbability = componentKVP.Value;

							DamageExpectationRecord componentRecord = new DamageExpectationRecord();
							locRecord.AddChildRecord(componentProbability, componentRecord);

							componentRecord.AddComponentDamage(1.0f, compLoc);

							// if this component is ammo, there's a chance we could lose this location and all child locations
							if (component.componentType == ComponentType.AmmunitionBox)
							{
								AmmunitionBox abComponent = component as AmmunitionBox;
								int remainingAmmo = abComponent.CurrentAmmo;
								int capacity = abComponent.ammunitionBoxDef.Capacity;
								float percentage = ((float)remainingAmmo) / ((float)capacity);

								if (percentage > 0.5f)
								{
									probOfHittingAmmo += componentProbability;
								}
							}
						}

						if (probOfHittingAmmo > 0)
						{
							DamageExpectationRecord ammoBlownRecord = new DamageExpectationRecord();
							locRecord.AddChildRecord(probOfHittingAmmo, ammoBlownRecord);

							foreach (KeyValuePair<ComponentLocator, float> componentKVP in componentDict)
							{
								ComponentLocator compLoc = componentKVP.Key;
								ammoBlownRecord.AddComponentDamage(2.0f, compLoc);
							}
						}
					}
				}
			}
		}

		static void evaluateWeaponAttackOnVehicle(float expectedDamage, Weapon w, ref DamageExpectationRecord damageExpectationRecord, Vector3 attackerPosition, Vehicle targetVehicle, Vector3 targetPosition, Quaternion targetRotation)
		{
			// use hit table to figure out where this will go
			Dictionary<VehicleChassisLocations, float> locations = GetLocationDictionary(attackerPosition, targetVehicle, targetPosition, targetRotation);

			foreach (KeyValuePair<VehicleChassisLocations, float> locKVP in locations)
			{
				VehicleChassisLocations loc = locKVP.Key;
				float probability = locKVP.Value;

				DamageExpectationRecord locRecord = new DamageExpectationRecord();
				damageExpectationRecord.AddChildRecord(probability, locRecord);

				float existingArmor = targetVehicle.ArmorForLocation((int)loc);
				float armorThatWillBeRemoved = Mathf.Min(existingArmor, expectedDamage);
				float damageRemaining = expectedDamage - existingArmor;
				locRecord.AddVehicleArmorDamage(armorThatWillBeRemoved, loc);

				if (damageRemaining > 0)
				{
					// some goes in to the structure
					float currentStructure = targetVehicle.GetCurrentStructure(loc);

					float structureDamage = Mathf.Min(damageRemaining, currentStructure);
					//float damageAfterStructure = damageRemaining - structureDamage;

					locRecord.AddVehicleStructureDamage(structureDamage, loc);
				}
			}
		}

		static void evaluateWeaponAttackOnTurret(float expectedDamage, Weapon w, ref DamageExpectationRecord damageExpectationRecord, Vector3 attackerPosition, Turret targetTurret, Vector3 targetPosition, Quaternion targetRotation)
		{
			float existingArmor = targetTurret.CurrentArmor;
			float armorThatWillBeRemoved = Mathf.Min(existingArmor, expectedDamage);
			float damageRemaining = expectedDamage - existingArmor;
			damageExpectationRecord.AddArmorDamage(armorThatWillBeRemoved, ArmorLocation.None);

			if (damageRemaining > 0)
			{
				// some goes in to the structure
				float currentStructure = targetTurret.GetCurrentStructure(BuildingLocation.Structure);

				float structureDamage = Mathf.Min(damageRemaining, currentStructure);

				damageExpectationRecord.AddStructureDamage(structureDamage, ChassisLocations.None);
			}
		}

		static void evaluateWeaponAttackOnBuilding(float expectedDamage, Weapon w, ref DamageExpectationRecord damageExpectationRecord, Vector3 attackerPosition, Building targetBuilding, Vector3 targetPosition, Quaternion targetRotation)
		{
			float existingArmor = targetBuilding.CurrentArmor;
			float armorThatWillBeRemoved = Mathf.Min(existingArmor, expectedDamage);
			float damageRemaining = expectedDamage - existingArmor;
			damageExpectationRecord.AddArmorDamage(armorThatWillBeRemoved, ArmorLocation.None);

			if (damageRemaining > 0)
			{
				// some goes in to the structure
				float currentStructure = targetBuilding.CurrentStructure;

				float structureDamage = Mathf.Min(damageRemaining, currentStructure);

				damageExpectationRecord.AddStructureDamage(structureDamage, ChassisLocations.None);
			}
		}

		static void consolidateDamageExpectationRecord(ref DamageExpectationRecord damageExpectationRecord, ICombatant target)
		{
			damageExpectationRecord = damageExpectationRecord.Flatten();

			Mech targetMech = target as Mech;
			if (targetMech != null)
			{
				foreach (ChassisLocations loc in System.Enum.GetValues(typeof(ChassisLocations)))
				{
					if ((loc == ChassisLocations.All) ||
						(loc == ChassisLocations.Arms) ||
						(loc == ChassisLocations.Torso) ||
						(loc == ChassisLocations.MainBody) ||
						(loc == ChassisLocations.Legs) ||
						(loc == ChassisLocations.None))
					{
						continue;
					}
					if (damageExpectationRecord.GetStructureDamageForLocation(loc) >= targetMech.GetCurrentStructure(loc))
					{
						NukeMechLocation(ref damageExpectationRecord, targetMech, loc);
					}
				}

				// if left or right torso are destroyed, mark left or right arm as destroyed
				if (damageExpectationRecord.GetStructureDamageForLocation(ChassisLocations.LeftTorso) >= targetMech.LeftTorsoStructure)
				{
					NukeMechLocation(ref damageExpectationRecord, targetMech, ChassisLocations.LeftArm);
				}
				if (damageExpectationRecord.GetStructureDamageForLocation(ChassisLocations.RightTorso) >= targetMech.RightTorsoStructure)
				{
					NukeMechLocation(ref damageExpectationRecord, targetMech, ChassisLocations.RightArm);
				}

				// if both legs are destroyed, mark whole unit as destroyed
				// if either center torso structure or head structure are destroyed, mark whole unit as destroyed

				if (((damageExpectationRecord.GetStructureDamageForLocation(ChassisLocations.LeftLeg) >= targetMech.StructureForLocation((int)ChassisLocations.LeftLeg)) &&
					(damageExpectationRecord.GetStructureDamageForLocation(ChassisLocations.RightLeg) >= targetMech.StructureForLocation((int)ChassisLocations.RightLeg))) ||
					(damageExpectationRecord.GetStructureDamageForLocation(ChassisLocations.CenterTorso) >= targetMech.StructureForLocation((int)ChassisLocations.CenterTorso)) ||
					(damageExpectationRecord.GetStructureDamageForLocation(ChassisLocations.Head) >= targetMech.StructureForLocation((int)ChassisLocations.Head)))
				{
					NukeUnit(ref damageExpectationRecord, targetMech);
				}

				// TODO if the pilot is destroyed, mark whole unit as destroyed
			}

			Vehicle targetVehicle = target as Vehicle;
			if (targetVehicle != null)
			{
				foreach (VehicleChassisLocations loc in System.Enum.GetValues(typeof(VehicleChassisLocations)))
				{
					if ((loc == VehicleChassisLocations.All) ||
						(loc == VehicleChassisLocations.None) ||
						(loc == VehicleChassisLocations.MainBody) ||
						(loc == VehicleChassisLocations.Invalid))
					{
						continue;
					}
					if (damageExpectationRecord.GetVehicleStructureDamageForLocation(loc) >= targetVehicle.StructureForLocation((int)loc))
					{
						NukeUnit(ref damageExpectationRecord, targetVehicle);
					}
				}
			}

			// if unit is a turret and center structure is destroyed, mark whole unit as destroyed
			Turret targetTurret = target as Turret;
			if (targetTurret != null)
			{
				if (damageExpectationRecord.GetTurretStructureDamage() >= targetTurret.StructureForLocation((int)BuildingLocation.Structure))
				{
					NukeUnit(ref damageExpectationRecord, targetTurret);
				}
			}

			// if unit is a building and center structure is destroyed, mark whole unit as destroyed
			Building targetBuilding = target as Building;
			if (targetBuilding != null)
			{
				if (damageExpectationRecord.GetBuildingStructureDamage() >= targetBuilding.StructureForLocation((int)BuildingLocation.Structure))
				{
					NukeUnit(ref damageExpectationRecord, targetBuilding);
				}
			}
		}

		static void NukeMechLocation(ref DamageExpectationRecord damageExpectationRecord, Mech targetMech, ChassisLocations loc)
		{
			// foreach component, destroy it
			Dictionary<ComponentLocator, float> componentDict = getComponentDictionary(targetMech, loc);

			foreach (KeyValuePair<ComponentLocator, float> componentKVP in componentDict)
			{
				ComponentLocator compLoc = componentKVP.Key;
				damageExpectationRecord.AddComponentDamage(2.0f, compLoc);
			}
		}

		static void NukeUnit(ref DamageExpectationRecord damageExpectationRecord, Mech targetMech)
		{
			damageExpectationRecord.lethalProbability = 1.0f;

			// foreach location
			foreach (ChassisLocations loc in System.Enum.GetValues(typeof(ChassisLocations)))
			{
				if ((loc == ChassisLocations.All) ||
					(loc == ChassisLocations.Arms) ||
					(loc == ChassisLocations.Legs) ||
					(loc == ChassisLocations.Torso) ||
					(loc == ChassisLocations.None))
				{
					continue;
				}
				NukeMechLocation(ref damageExpectationRecord, targetMech, loc);
			}
		}

		static void NukeUnit(ref DamageExpectationRecord damageExpectationRecord, Vehicle targetVehicle)
		{
			damageExpectationRecord.lethalProbability = 1.0f;

			List<Weapon> weaponList = targetVehicle.Weapons;
			for (int slotIndex = 0; slotIndex < weaponList.Count; ++slotIndex)
			{
				ComponentLocator compLoc = new ComponentLocator(targetVehicle, slotIndex);
				damageExpectationRecord.AddComponentDamage(2.0f, compLoc);
			}
		}

		static void NukeUnit(ref DamageExpectationRecord damageExpectationRecord, Turret targetTurret)
		{
			damageExpectationRecord.lethalProbability = 1.0f;

			List<Weapon> weaponList = targetTurret.Weapons;
			for (int slotIndex = 0; slotIndex < weaponList.Count; ++slotIndex)
			{
				ComponentLocator compLoc = new ComponentLocator(targetTurret, slotIndex);
				damageExpectationRecord.AddComponentDamage(2.0f, compLoc);
			}
		}

		static void NukeUnit(ref DamageExpectationRecord damageExpectationRecord, Building targetBuilding)
		{
			damageExpectationRecord.lethalProbability = 1.0f;
		}

		static Dictionary<ComponentLocator, float> getComponentDictionary(Mech targetMech, ChassisLocations chassisLoc)
		{
			Dictionary<ComponentLocator, float> componentDict = new Dictionary<ComponentLocator, float>();

			int inventorySlotsCount = targetMech.MechDef.GetChassisLocationDef(chassisLoc).InventorySlots;
			if (inventorySlotsCount == 0)
			{
				return componentDict;
			}

			float slotProbability = 1.0f / inventorySlotsCount;

			for (int slotIndex = 0; slotIndex < inventorySlotsCount; ++slotIndex)
			{
				MechComponent component = targetMech.GetComponentInSlot(chassisLoc, slotIndex);
				if (component != null)
				{
					ComponentLocator compLoc = new ComponentLocator(targetMech, chassisLoc, slotIndex);
					componentDict[compLoc] = slotProbability;
				}
			}

			return componentDict;
		}

		static AttackDirection GetAttackDirection(Vector3 attackerPosition, AbstractActor targetActor, Vector3 targetPosition, Quaternion targetRotation)
		{
			if (targetActor.IsProne)
			{
				return AttackDirection.ToProne;
			}

			HitLocation hitLocationHelper = targetActor.Combat.HitLocation;
			return hitLocationHelper.GetAttackDirection(attackerPosition, targetPosition, targetRotation);
		}

		static Dictionary<ArmorLocation, float> GetLocationDictionary(Vector3 attackerPosition, Mech m, Vector3 targetPosition, Quaternion targetRotation)
		{
			//Dictionary<ArmorLocation, float> locDir = new Dictionary<ArmorLocation, float>();

			// TODO handle called shots - see HitLocationRules.cs GetAdjacentHitLocation

			AttackDirection attackDirection = GetAttackDirection(attackerPosition, m, targetPosition, targetRotation);
			HitLocation hitLocationHelper = m.Combat.HitLocation;
			Dictionary<ArmorLocation, int> hitTable = hitLocationHelper.GetMechHitTable(attackDirection, false);

			return HitTableToLocationDirectory(hitTable);
		}

		static Dictionary<VehicleChassisLocations, float> GetLocationDictionary(Vector3 attackerPosition, Vehicle targetVehicle, Vector3 targetPosition, Quaternion targetRotation)
		{
			//Dictionary<ArmorLocation, float> locDir = new Dictionary<ArmorLocation, float>();

			AttackDirection attackDirection = GetAttackDirection(attackerPosition, targetVehicle, targetPosition, targetRotation);
			HitLocation hitLocationHelper = targetVehicle.Combat.HitLocation;
			Dictionary<VehicleChassisLocations, int> hitTable = hitLocationHelper.GetVehicleHitTable(attackDirection, false);

			return HitTableToLocationDirectory(hitTable);
		}

		static Dictionary<ArmorLocation, float> GetLocationDictionary(Turret t)
		{
			// TODO
			Debug.LogError("TODO need to implement turret location dictionary");
			return null;
		}

		static Dictionary<T, float> HitTableToLocationDirectory<T>(Dictionary<T, int> hitTable)
		{
			// find our max roll value from the specified table
			float totalWeights = 0.0f;
			foreach (KeyValuePair<T, int> kvp in hitTable)
			{
				totalWeights += kvp.Value;
			}

			Dictionary<T, float> locDir = new Dictionary<T, float>();

			// go through the dictionary entries and find the result KeyValuePair
			foreach (KeyValuePair<T, int> kvp in hitTable)
			{
				locDir[kvp.Key] = kvp.Value / totalWeights;
			}
			return locDir;
		}

		static float FirepowerFromUnit(ICombatant unit)
		{
			AbstractActor actor = unit as AbstractActor;
			if (actor == null)
			{
				return 0.0f;
			}
			float dmg = 0.0f;
			List<Weapon> weaponList = actor.Weapons;
			for (int weaponIndex = 0; weaponIndex < weaponList.Count; ++weaponIndex)
			{
				Weapon w = weaponList[weaponIndex];
				if (w.CanFire)
				{
					dmg += w.ShotsWhenFired * w.DamagePerShot;
				}
			}
			return dmg;
		}

		static public List<ComponentLocator> GetWeaponComponentLocatorList(Mech m)
		{
			List<ComponentLocator> compLocList = new List<ComponentLocator>();

			foreach (ChassisLocations loc in System.Enum.GetValues(typeof(ChassisLocations)))
			{
				if ((loc == ChassisLocations.All) ||
					(loc == ChassisLocations.Arms) ||
					(loc == ChassisLocations.Legs) ||
					(loc == ChassisLocations.Torso) ||
					(loc == ChassisLocations.None))
				{
					continue;
				}

				int inventorySlotsCount = m.MechDef.GetChassisLocationDef(loc).InventorySlots;
				if (inventorySlotsCount == 0)
				{
					continue;
				}

				for (int slotIndex = 0; slotIndex < inventorySlotsCount; ++slotIndex)
				{
					MechComponent component = m.GetComponentInSlot(loc, slotIndex);
					if (component is Weapon)
					{
						ComponentLocator compLoc = new ComponentLocator(m, loc, slotIndex);
						compLocList.Add(compLoc);
					}
				}
			}
			return compLocList;
		}

		static public List<ComponentLocator> GetWeaponComponentLocatorList(Vehicle v)
		{
			List<ComponentLocator> compLocList = new List<ComponentLocator>();

			List<Weapon> weaponList = v.Weapons;
			for (int i = 0; i < weaponList.Count; ++i)
			{
				ComponentLocator loc = new ComponentLocator(v, i);
				compLocList.Add(loc);
			}
			return compLocList;
		}

		static public List<ComponentLocator> GetWeaponComponentLocatorList(Turret t)
		{
			List<ComponentLocator> compLocList = new List<ComponentLocator>();

			List<Weapon> weaponList = t.Weapons;
			for (int i = 0; i < weaponList.Count; ++i)
			{
				ComponentLocator loc = new ComponentLocator(t, i);
				compLocList.Add(loc);
			}
			return compLocList;
		}


		static public List<ComponentLocator> GetWeaponComponentLocatorList(AbstractActor target)
		{
			Mech targetMech = target as Mech;
			if (targetMech != null)
			{
				return GetWeaponComponentLocatorList(targetMech);
			}

			Vehicle targetVehicle = target as Vehicle;
			if (targetVehicle != null)
			{
				return GetWeaponComponentLocatorList(targetVehicle);
			}

			Turret targetTurret = target as Turret;
			if (targetTurret != null)
			{
				return GetWeaponComponentLocatorList(targetTurret);
			}

			Debug.LogError("unrecognized target type: " + target);
			return null;
		}

		static public float EvaluateFirepowerReductionFromAttack(AbstractActor attacker, Vector3 attackerPosition, ICombatant target, Vector3 targetPosition, Quaternion targetRotation, List<Weapon> weapons, MeleeAttackType attackType)
		{
			AbstractActor actor = target as AbstractActor;
			if (actor == null)
			{
				return 0.0f;
			}

			DamageExpectationRecord damageExpectationRecord = EvaluateAttack(attacker, attackerPosition, target, targetPosition, targetRotation, weapons, attackType);

			float dmg = 0.0f;
			List<ComponentLocator> weaponList = GetWeaponComponentLocatorList(actor);
			for (int weaponIndex = 0; weaponIndex < weaponList.Count; ++weaponIndex)
			{
				ComponentLocator compLoc = weaponList[weaponIndex];
				MechComponent mechComp = compLoc.GetComponent();
				Weapon w = mechComp as Weapon;

				if (w.CanFire)
				{
					float weaponBaseDamage = w.ShotsWhenFired * w.DamagePerShot;
					if (w.DamageLevel == ComponentDamageLevel.Functional)
					{
						int expDmg = Mathf.RoundToInt(damageExpectationRecord.GetComponentDamageForLocation(compLoc));
						if (expDmg == 1)
						{
							// that's like half damage
							dmg += weaponBaseDamage * 0.5f;
						}
						else if (expDmg > 1)
						{
							dmg += weaponBaseDamage;
						}
					}
					else if (w.DamageLevel == ComponentDamageLevel.Penalized)
					{
						int expDmg = Mathf.RoundToInt(damageExpectationRecord.GetComponentDamageForLocation(compLoc));
						if (expDmg >= 1)
						{
							dmg += weaponBaseDamage;
						}
					}
				}
			}
			return dmg;
		}
	}
}
