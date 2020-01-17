using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using HBS.Collections;


namespace BattleTech
{
	class FindSprintTutorialTargetNode : LeafBehaviorNode
	{
		public FindSprintTutorialTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
		{
		}

		override protected BehaviorTreeResults Tick()
		{
			unit.BehaviorTree.enemyUnits = new List<ICombatant>();

			string[] targetTags = { "tutorial_sprint_target" };
			TagSet targetTagSet = new TagSet(targetTags);
			List<ITaggedItem> items = unit.Combat.ItemRegistry.GetObjectsOfTypeWithTagSet(TaggedObjectType.Unit, targetTagSet);

			for (int i = 0; i < items.Count; ++i)
			{
				ICombatant targetUnit = items[i] as ICombatant;
				if ((targetUnit != null) && (targetUnit.IsOperational))
				{
					unit.BehaviorTree.enemyUnits.Add(targetUnit);
				}
			}

			return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.BehaviorTree.enemyUnits.Count > 0);
		}
	}

	class FindPlayerTutorialTargetNode : LeafBehaviorNode
	{
		public FindPlayerTutorialTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
		{
		}

		override protected BehaviorTreeResults Tick()
		{
			unit.BehaviorTree.enemyUnits = new List<ICombatant>();

			List<ITaggedItem> items = unit.Combat.ItemRegistry.GetObjectsOfType(TaggedObjectType.Unit);

			for (int i = 0; i < items.Count; ++i)
			{
				ICombatant targetUnit = items[i] as ICombatant;
				if ((targetUnit != null) &&
					(targetUnit.team.PlayerControlsTeam) &&
					(targetUnit.IsOperational))
				{
					unit.BehaviorTree.enemyUnits.Add(targetUnit);
				}
			}

			return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.BehaviorTree.enemyUnits.Count > 0);
		}
	}


	class ShootTrainingWeaponsAtTargetNode : LeafBehaviorNode
	{
		public ShootTrainingWeaponsAtTargetNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
		{
		}

		override protected BehaviorTreeResults Tick()
		{
			const int laserCount = 2;

			List<Weapon> lasers = new List<Weapon>();

			for (int wi = 0; wi < unit.Weapons.Count; ++wi)
			{
				if (lasers.Count >= laserCount)
				{
					break;
				}

				Weapon w = unit.Weapons[wi];
				// TODO? (dlecompte) make this more specific
				if (w.WeaponCategoryValue.IsEnergy)
				{
					lasers.Add(w);
				}
			}

			if ((lasers.Count == 0) || (unit.BehaviorTree.enemyUnits.Count == 0))
			{
				return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(false);
			}

			AttackOrderInfo attackOrder = new AttackOrderInfo(unit.BehaviorTree.enemyUnits[0]);

			attackOrder.Weapons = lasers;
			attackOrder.TargetUnit = unit.BehaviorTree.enemyUnits[0];

			BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
			results.orderInfo = attackOrder;
			return results;
		}
	}
}
