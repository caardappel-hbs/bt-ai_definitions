using System.Collections;
using UnityEngine;

using BattleTech;

public static class BehaviorTreeFactory
{
	public static BehaviorTree MakeBehaviorTree(GameInstance game, AbstractActor unit, BehaviorTreeIDEnum treeID)
	{
		return new BehaviorTree(unit, game, treeID);
	}
}

