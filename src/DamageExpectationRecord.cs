using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The idea here is to pass one of these in to the combat attack resolution code, which will record all the potential outcomes as a recursive tree of DamageExpectationRecords, which we will then collapse into a single record.
/// a record should have the ability to 
/// - add a dependent record (with a probability)
/// - add damage to a component
/// - add damage to structure
/// - add damage to armor?
/// </summary>

namespace BattleTech
{
	public struct ComponentLocator : System.IEquatable<ComponentLocator>
	{
		public AbstractActor Unit;
		public ChassisLocations Loc;
		public int SlotIndex;

		public ComponentLocator(Mech mech, ChassisLocations loc, int slotIndex)
		{
			Unit = mech;
			Loc = loc;
			SlotIndex = slotIndex;
		}

		public ComponentLocator(Vehicle vehicle, int slotIndex)
		{
			Unit = vehicle;
			Loc = ChassisLocations.None;
			SlotIndex = slotIndex;
		}

		public ComponentLocator(Turret turret, int slotIndex)
		{
			Unit = turret;
			Loc = ChassisLocations.None;
			SlotIndex = slotIndex;
		}

		public MechComponent GetComponent()
		{
			Mech mech = Unit as Mech;
			if (mech != null)
			{
				return mech.GetComponentInSlot(Loc, SlotIndex);
			}
			Vehicle vehicle = Unit as Vehicle;
			Turret turret = Unit as Turret;
			if ((vehicle != null) || (turret != null))
			{
				return Unit.Weapons[SlotIndex];
			}
			return null;
		}

		override public int GetHashCode()
		{
			int hash = 17;
			hash = hash * 19 + Unit.GUID.GetHashCode();
			hash = hash * 19 + (int)Loc;
			hash = hash * 19 + SlotIndex;
			return hash;
		}

		public bool Equals(ComponentLocator other)
		{
			return ((Unit.GUID == other.Unit.GUID) &&
				(Loc == other.Loc) &&
				(SlotIndex == other.SlotIndex));
		}
	}

	public class DamageExpectationRecord
	{
		class ChildWithProbability
		{
			public float Probability;
			public DamageExpectationRecord DamageExpectationRecord;

			public ChildWithProbability(float probability, DamageExpectationRecord damageExpectationRecord)
			{
				this.Probability = probability;
				this.DamageExpectationRecord = damageExpectationRecord;
			}
		}

		Dictionary<ComponentLocator, float> componentDamageDictionary;
		Dictionary<ChassisLocations, float> chassisLocationDictionary;
		Dictionary<VehicleChassisLocations, float> vehicleChassisLocationDictionary;
		Dictionary<ArmorLocation, float> armorLocationDictionary;
		Dictionary<VehicleChassisLocations, float> vehicleArmorLocationDictionary;
		float pilotDamage;
		public float lethalProbability;

		const float MAX_PILOT_DAMAGE = 5.0f;
		const float MAX_COMPONENT_DAMAGE = 2.0f;

		List<ChildWithProbability> children;

		public DamageExpectationRecord()
		{
			componentDamageDictionary = new Dictionary<ComponentLocator, float>();
			chassisLocationDictionary = new Dictionary<ChassisLocations, float>();
			armorLocationDictionary = new Dictionary<ArmorLocation, float>();
			vehicleChassisLocationDictionary = new Dictionary<VehicleChassisLocations, float>();
			vehicleArmorLocationDictionary = new Dictionary<VehicleChassisLocations, float>();
			pilotDamage = 0.0f;
			lethalProbability = 0.0f;
			children = new List<ChildWithProbability>();
		}

		public void AddChildRecord(float probability, DamageExpectationRecord childRecord)
		{
			children.Add(new ChildWithProbability(probability, childRecord));
		}

		public void AddComponentDamage(float damage, ComponentLocator componentLoc)
		{
			float existingDamage = 0.0f;

			if (componentDamageDictionary.ContainsKey(componentLoc))
			{
				existingDamage = componentDamageDictionary[componentLoc];
			}
			componentDamageDictionary[componentLoc] = existingDamage + damage;
		}

		public void AddStructureDamage(float damage, ChassisLocations loc)
		{
			float existingDamage = 0.0f;
			if (chassisLocationDictionary.ContainsKey(loc))
			{
				existingDamage = chassisLocationDictionary[loc];
			}
			chassisLocationDictionary[loc] = existingDamage + damage;
		}

		public void AddVehicleStructureDamage(float damage, VehicleChassisLocations loc)
		{
			float existingDamage = 0.0f;
			if (vehicleChassisLocationDictionary.ContainsKey(loc))
			{
				existingDamage = vehicleChassisLocationDictionary[loc];
			}
			vehicleChassisLocationDictionary[loc] = existingDamage + damage;
		}

		public void AddArmorDamage(float damage, ArmorLocation loc)
		{
			float existingDamage = 0.0f;
			if (armorLocationDictionary.ContainsKey(loc))
			{
				existingDamage = armorLocationDictionary[loc];
			}
			armorLocationDictionary[loc] = existingDamage + damage;
		}

		public void AddVehicleArmorDamage(float damage, VehicleChassisLocations loc)
		{
			float existingDamage = 0.0f;
			if (vehicleArmorLocationDictionary.ContainsKey(loc))
			{
				existingDamage = vehicleArmorLocationDictionary[loc];
			}
			vehicleArmorLocationDictionary[loc] = existingDamage + damage;
		}

		public void AddPilotDamage(float damage)
		{
			pilotDamage += damage;
		}

		public void KillPilot()
		{
			pilotDamage = MAX_PILOT_DAMAGE;
		}

		public void DestroyComponent(ComponentLocator compLoc)
		{
			componentDamageDictionary[compLoc] = MAX_COMPONENT_DAMAGE;
		}

		public float GetStructureDamageForLocation(ChassisLocations loc)
		{
			float dmg = 0.0f;
			if (chassisLocationDictionary.ContainsKey(loc))
			{
				dmg = chassisLocationDictionary[loc];
			}

			for (int childIndex = 0; childIndex < children.Count; ++childIndex)
			{
				ChildWithProbability c = children[childIndex];
				dmg += c.DamageExpectationRecord.GetStructureDamageForLocation(loc) * c.Probability;
			}
			return dmg;
		}

		public float GetVehicleStructureDamageForLocation(VehicleChassisLocations loc)
		{
			float dmg = 0.0f;
			if (vehicleChassisLocationDictionary.ContainsKey(loc))
			{
				dmg = vehicleChassisLocationDictionary[loc];
			}

			for (int childIndex = 0; childIndex < children.Count; ++childIndex)
			{
				ChildWithProbability c = children[childIndex];
				dmg += c.DamageExpectationRecord.GetVehicleStructureDamageForLocation(loc) * c.Probability;
			}
			return dmg;
		}

		public float GetTurretStructureDamage()
		{
			float dmg = 0.0f;
			/* TODO
			if (chassisLocationDictionary.ContainsKey())
			{
				dmg = vehicleChassisLocationDictionary[loc];
			}

			for (int childIndex = 0; childIndex < children.Count; ++childIndex)
			{
				ChildWithProbability c = children[childIndex];
				dmg += c.DamageExpectationRecord.GetVehicleStructureDamageForLocation(loc) * c.Probability;
			}*/
			return dmg;
		}

		public float GetBuildingStructureDamage()
		{
			float dmg = 0.0f;
			/* TODO
			if (chassisLocationDictionary.ContainsKey())
			{
				dmg = vehicleChassisLocationDictionary[loc];
			}

			for (int childIndex = 0; childIndex < children.Count; ++childIndex)
			{
				ChildWithProbability c = children[childIndex];
				dmg += c.DamageExpectationRecord.GetVehicleStructureDamageForLocation(loc) * c.Probability;
			}*/
			return dmg;
		}


		public float GetComponentDamageForLocation(ComponentLocator compLoc)
		{
			float dmg = 0.0f;
			if (componentDamageDictionary.ContainsKey(compLoc))
			{
				dmg = componentDamageDictionary[compLoc];
			}

			for (int childIndex = 0; childIndex < children.Count; ++childIndex)
			{
				ChildWithProbability c = children[childIndex];
				dmg += c.DamageExpectationRecord.GetComponentDamageForLocation(compLoc) * c.Probability;
			}
			return dmg;
		}

		public float GetArmorDamageForLocation(ArmorLocation loc)
		{
			float dmg = 0.0f;
			if (armorLocationDictionary.ContainsKey(loc))
			{
				dmg = armorLocationDictionary[loc];
			}

			for (int childIndex = 0; childIndex < children.Count; ++childIndex)
			{
				ChildWithProbability c = children[childIndex];
				dmg += c.DamageExpectationRecord.GetArmorDamageForLocation(loc) * c.Probability;
			}
			return dmg;
		}

		public float GetPilotDamage()
		{
			float dmg = pilotDamage;
			for (int childIndex = 0; childIndex < children.Count; ++childIndex)
			{
				ChildWithProbability c = children[childIndex];
				dmg += c.DamageExpectationRecord.GetPilotDamage() * c.Probability;
			}
			return dmg;
		}

		/// <summary>
		/// Take a tree of DamageExpectationRecords and add them up into a single record with no children.
		/// </summary>
		/// <returns></returns>
		public DamageExpectationRecord Flatten()
		{
			DamageExpectationRecord newRecord = new DamageExpectationRecord();
			flattenInto(newRecord, this, 1.0f);
			return newRecord;
		}

		public void flattenInto(DamageExpectationRecord target, DamageExpectationRecord source, float probability)
		{
			foreach (KeyValuePair<ComponentLocator, float> kvp in source.componentDamageDictionary)
			{
				ComponentLocator key = kvp.Key;
				float dmg = kvp.Value;
				target.AddComponentDamage(dmg * probability, key);
			}

			foreach (ChassisLocations loc in source.chassisLocationDictionary.Keys)
			{
				float dmg = source.chassisLocationDictionary[loc];
				target.AddStructureDamage(dmg * probability, loc);
			}

			foreach (ArmorLocation loc in source.armorLocationDictionary.Keys)
			{
				float dmg = source.armorLocationDictionary[loc];
				target.AddArmorDamage(dmg * probability, loc);
			}

			foreach (VehicleChassisLocations loc in source.vehicleChassisLocationDictionary.Keys)
			{
				float dmg = source.vehicleChassisLocationDictionary[loc];
				target.AddVehicleStructureDamage(dmg * probability, loc);
			}

			foreach (VehicleChassisLocations loc in source.vehicleArmorLocationDictionary.Keys)
			{
				float dmg = source.vehicleArmorLocationDictionary[loc];
				target.AddVehicleArmorDamage(dmg * probability, loc);
			}

			target.AddPilotDamage(pilotDamage * probability);
			target.lethalProbability += lethalProbability * probability;

			for (int childIndex = 0; childIndex < source.children.Count; ++childIndex)
			{
				ChildWithProbability c = source.children[childIndex];
				flattenInto(target, c.DamageExpectationRecord, c.Probability);
			}
		}
	}
}
