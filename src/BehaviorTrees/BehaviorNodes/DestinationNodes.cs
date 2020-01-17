using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BattleTech;

class DestinationUtil
{
	static public RoutePointGameLogic FindDestinationByGUID(BehaviorTree tree, string waypointGUID)
	{
		ITaggedItem item = tree.battleTechGame.Combat.ItemRegistry.GetItemByGUID(waypointGUID);
		return item as RoutePointGameLogic;
	}
}

class LanceHasPreAttackDestinationNode : LeafBehaviorNode
{
	public LanceHasPreAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_LancePreAttackDestinationGUID);
		if (variableValue == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		string destinationGUID = variableValue.StringVal;

		RoutePointGameLogic destination = DestinationUtil.FindDestinationByGUID(tree, destinationGUID);

		if (destination == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		return new BehaviorTreeResults(BehaviorNodeState.Success);
	}
}

class UnitHasPreAttackDestinationNode : LeafBehaviorNode
{
	public UnitHasPreAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_UnitPreAttackDestinationGUID);
		if (variableValue == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		string destinationGUID = variableValue.StringVal;

		RoutePointGameLogic destination = DestinationUtil.FindDestinationByGUID(tree, destinationGUID);

		if (destination == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		return new BehaviorTreeResults(BehaviorNodeState.Success);
	}
}

class LanceHasPostAttackDestinationNode : LeafBehaviorNode
{
	public LanceHasPostAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_LancePostAttackDestinationGUID);
		if (variableValue == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		string destinationGUID = variableValue.StringVal;

		RoutePointGameLogic destination = DestinationUtil.FindDestinationByGUID(tree, destinationGUID);

		if (destination == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		return new BehaviorTreeResults(BehaviorNodeState.Success);
	}
}

class UnitHasPostAttackDestinationNode : LeafBehaviorNode
{
	public UnitHasPostAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_UnitPostAttackDestinationGUID);
		if (variableValue == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		string destinationGUID = variableValue.StringVal;

		RoutePointGameLogic destination = DestinationUtil.FindDestinationByGUID(tree, destinationGUID);

		if (destination == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		return new BehaviorTreeResults(BehaviorNodeState.Success);
	}
}


class MoveToDestinationNode : LeafBehaviorNode
{
	protected bool waitForLance;
	protected BehaviorVariableName destinationBVarName;

	public MoveToDestinationNode(string name, BehaviorTree tree, AbstractActor unit, bool waitForLance, BehaviorVariableName destinationBVarName) : base(name, tree, unit)
	{
		this.waitForLance = waitForLance;
		this.destinationBVarName = destinationBVarName;
	}

	override protected BehaviorTreeResults Tick()
	{
		BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(destinationBVarName);
		if (variableValue == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		string destinationGUID = variableValue.StringVal;

		RoutePointGameLogic destination = DestinationUtil.FindDestinationByGUID(tree, destinationGUID);

		if (destination == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		float sprintDistance = Mathf.Max(unit.MaxSprintDistance, unit.MaxWalkDistance);

		if (waitForLance)
		{
			for (int lanceMemberIndex = 0; lanceMemberIndex < unit.lance.unitGuids.Count; ++lanceMemberIndex)
			{
				ITaggedItem item = unit.Combat.ItemRegistry.GetItemByGUID(unit.lance.unitGuids[lanceMemberIndex]);
				if (item == null)
				{
					continue;
				}
				AbstractActor lanceUnit = item as AbstractActor;
				if (lanceUnit == null)
				{
					continue;
				}
                float unitMoveDistance = Mathf.Max(lanceUnit.MaxWalkDistance, lanceUnit.MaxSprintDistance);
				sprintDistance = Mathf.Min(sprintDistance, unitMoveDistance);
			}
		}

		MoveType moveType = tree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteShouldSprint).BoolVal ?
			MoveType.Sprinting : MoveType.Walking;
		
		unit.Pathing.UpdateAIPath(destination.Position, destination.Position, moveType);

		Vector3 offset = unit.Pathing.ResultDestination - unit.CurrentPosition;
		if (offset.magnitude > sprintDistance)
		{
			offset = offset.normalized * sprintDistance;
		}

		Vector3 destinationThisTurn = RoutingUtil.Decrowd(unit.CurrentPosition + offset, unit);
		destinationThisTurn = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, destinationThisTurn);

		float destinationRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_RouteWaypointRadius).FloatVal;

		List<AbstractActor> unitsToWaitFor = new List<AbstractActor>();

		if (waitForLance)
		{
			if (unit.lance != null)
			{
				for (int lanceGUIDIndex = 0; lanceGUIDIndex < unit.lance.unitGuids.Count; ++lanceGUIDIndex)
				{
					string guid = unit.lance.unitGuids[lanceGUIDIndex];
					ITaggedItem item = unit.Combat.ItemRegistry.GetItemByGUID(guid);
					if (item != null)
					{
						AbstractActor lanceUnit = item as AbstractActor;
						if (lanceUnit != null)
						{
							unitsToWaitFor.Add(lanceUnit);
						}
					}
				}
			}
			else
			{
				unitsToWaitFor.Add(unit);
			}
		}
		if (RoutingUtil.AllUnitsInsideRadiusOfPoint(unitsToWaitFor, destination.Position, destinationRadius))
		{
			tree.RemoveBehaviorVariableValue(destinationBVarName);
		}

		bool isSprinting = tree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteShouldSprint).BoolVal;
		unit.Pathing.UpdateAIPath(destinationThisTurn, destination.Position, isSprinting ? MoveType.Sprinting : MoveType.Walking);
		destinationThisTurn = unit.Pathing.ResultDestination;

		float movementBudget = unit.Pathing.MaxCost;
		PathNodeGrid grid = unit.Pathing.CurrentGrid;
		Vector3 successorPoint = destination.Position;
		if ((grid.GetValidPathNodeAt(destinationThisTurn, movementBudget) == null) ||
			((destinationThisTurn - destination.Position).magnitude > 1.0f))
		{
			// can't get all the way to the destination.
			if (unit.Combat.EncounterLayerData.inclineMeshData != null)
			{
				float maxSteepnessRatio = Mathf.Tan(Mathf.Deg2Rad * AIUtil.GetMaxSteepnessForAllLance(unit));
				List<AbstractActor> lanceUnits = AIUtil.GetLanceUnits(unit.Combat, unit.LanceId);
				destinationThisTurn = unit.Combat.EncounterLayerData.inclineMeshData.GetDestination(
					unit.CurrentPosition, 
					destinationThisTurn, 
					movementBudget, 
					maxSteepnessRatio, 
					unit, 
					isSprinting, 
					lanceUnits,
					unit.Pathing.CurrentGrid,
					out successorPoint);
			}
		}

		Vector3 cur = unit.CurrentPosition;
		AIUtil.LogAI(string.Format("issuing order from [{0} {1} {2}] to [{3} {4} {5}] looking at [{6} {7} {8}]", 
			cur.x, cur.y, cur.z, 
			destinationThisTurn.x, destinationThisTurn.y, destinationThisTurn.z,
			successorPoint.x, successorPoint.y, successorPoint.z
		));

		BehaviorTreeResults results = new BehaviorTreeResults(BehaviorNodeState.Success);
		MovementOrderInfo mvtOrderInfo = new MovementOrderInfo(destinationThisTurn, successorPoint);
		mvtOrderInfo.IsSprinting = isSprinting;
		results.orderInfo = mvtOrderInfo;
		results.debugOrderString = string.Format("{0} moving toward destination: {1} dest: {2}", this.name, destinationThisTurn, destination.Position);
		return results;
	}
}

class MoveLanceToPreAttackDestinationNode : MoveToDestinationNode
{
	public MoveLanceToPreAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit, true, BehaviorVariableName.String_LancePreAttackDestinationGUID)
	{
	}
}

class MoveUnitToPreAttackDestinationNode : MoveToDestinationNode
{
	public MoveUnitToPreAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit, false, BehaviorVariableName.String_UnitPreAttackDestinationGUID)
	{
	}
}

class MoveLanceToPostAttackDestinationNode : MoveToDestinationNode
{
	public MoveLanceToPostAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit, true, BehaviorVariableName.String_LancePostAttackDestinationGUID)
	{
	}
}

class MoveUnitToPostAttackDestinationNode : MoveToDestinationNode
{
	public MoveUnitToPostAttackDestinationNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit, false, BehaviorVariableName.String_UnitPostAttackDestinationGUID)
	{
	}
}

