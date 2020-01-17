using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BattleTech;


public class RegionUtil
{
	public static bool PointInRegion(CombatGameState combat, Vector3 point, string regionGUID)
	{
		MapEncounterLayerDataCell cell = combat.EncounterLayerData.GetCellAt(point);

		List<string> cellRegionGUIDList = cell.GetRegionGuids();

		return (cellRegionGUIDList == null) ? false : cellRegionGUIDList.Contains(regionGUID);
	}

	public static Vector3 LastPointAlongSegmentInsideRegion(CombatGameState combat, Vector3 start, Vector3 end, string regionGUID, out bool pointIsValid)
	{
		if (!PointInRegion(combat, start, regionGUID))
		{
			pointIsValid = false;
			return Vector3.zero;
		}

		pointIsValid = true;
		if (PointInRegion(combat, end, regionGUID))
		{
			return end;
		}

		Vector3 bestYet = start;
		float segmentLength = (end - start).magnitude;
		for (int step = 1; step < segmentLength; ++step)
		{
			float frac = step / segmentLength;
			Vector3 interpolated = start + (end - start) * frac;
			if (!PointInRegion(combat, interpolated, regionGUID))
			{
				break;
			}
			bestYet = interpolated;
		}
		return bestYet;
	}

	public static string StayInsideRegionGUID(AbstractActor unit)
	{
		BehaviorVariableValue variableValue = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_StayInsideRegionGUID);
		if ((variableValue == null) || (variableValue.StringVal.Length == 0))
		{
			return string.Empty;
		}
		return variableValue.StringVal;
	}

	public static Vector3 MaybeClipMovementDestinationToStayInsideRegion(AbstractActor unit, Vector3 destination)
	{
		string regionGUID = StayInsideRegionGUID(unit);
		if (regionGUID == string.Empty)
		{
			return destination;
		}
		bool pointIsValid;
		Vector3 point = LastPointAlongSegmentInsideRegion(unit.Combat, unit.CurrentPosition, destination, regionGUID, out pointIsValid);
		return pointIsValid ? point : destination;
	}

    public static void MaybeClipPathToStayInsideRegion(AbstractActor unit, List<PathNode> path)
    {
        string regionGUID = StayInsideRegionGUID(unit);
        if (regionGUID == string.Empty)
        {
            return;
        }

        int lastPointInside = -1;

        for (int i = 0; i < path.Count; ++i)
        {
            PathNode pathNode = path[i];

            if (PointInRegion(unit.Combat, pathNode.Position, regionGUID))
            {
                lastPointInside = i;
            }
        }
        if ((lastPointInside >= 0) && (lastPointInside < path.Count))
        {
            path.RemoveRange(lastPointInside + 1, path.Count - lastPointInside - 1);
        }
    }


    public static string GetStayInsideRegionGUID(AbstractActor unit)
	{
		if (unit == null)
		{
			return null;
		}

		BehaviorVariableValue variableValue = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_StayInsideRegionGUID);
		if ((variableValue == null) || variableValue.StringVal.Length == 0)
		{
			return null;
		}

		return variableValue.StringVal;
	}
}

class HasStayInsideRegionNode : LeafBehaviorNode
{
	public HasStayInsideRegionNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		string regionGUID = RegionUtil.GetStayInsideRegionGUID(unit);

		if (regionGUID == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}
		return new BehaviorTreeResults(BehaviorNodeState.Success);
	}
}

class IsInsideStayInsideRegionNode : LeafBehaviorNode
{
	public IsInsideStayInsideRegionNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		string regionGUID = RegionUtil.GetStayInsideRegionGUID(unit);
		if (regionGUID == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		if (RegionUtil.PointInRegion(unit.Combat, unit.CurrentPosition, regionGUID))
		{
			return new BehaviorTreeResults(BehaviorNodeState.Success);
		}
		else
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}
	}
}

class MoveToStayInsideRegionNode : LeafBehaviorNode
{
	public MoveToStayInsideRegionNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		string regionGUID = RegionUtil.GetStayInsideRegionGUID(unit);
		if (regionGUID == null)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		if (unit.IsInRegion(regionGUID))
		{
			return new BehaviorTreeResults(BehaviorNodeState.Success);
		}

		ITaggedItem item = unit.Combat.ItemRegistry.GetItemByGUID(regionGUID);
		if (item == null)
		{
			Debug.Log("no item with GUID: " + regionGUID);
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}
		RegionGameLogic region = item as RegionGameLogic;
		if (region == null)
		{
			Debug.Log("item is not region: " + regionGUID);
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		// TODO: find a point inside the region, for now using the average of all vertices.
		int numPoints = region.regionPointList.Length;

		Vector3 destination = new Vector3();

		for (int pointIndex = 0; pointIndex < numPoints; ++pointIndex)
		{
			destination += region.regionPointList[pointIndex].Position;
		}
		if (numPoints == 0)
		{
			Debug.Log("no points in region: " + regionGUID);
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		destination = RoutingUtil.Decrowd(destination * 1.0f / numPoints, unit);
		destination = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, destination);

        var cell = unit.Combat.MapMetaData.GetCellAt(destination);
        destination.y = cell.cachedHeight;

		if ((destination - unit.CurrentPosition).magnitude < 1)
		{
			// already close (should probably have been caught, above)
			return new BehaviorTreeResults(BehaviorNodeState.Success);
		}

        bool shouldSprint = unit.CanSprint;

        //float sprintRange = Mathf.Max(unit.MaxSprintDistance, unit.MaxWalkDistance);
        float moveRange = unit.MaxWalkDistance;

        if ((destination - unit.CurrentPosition).magnitude < moveRange)
        {
            shouldSprint = false;
        }

        if (shouldSprint)
        {
            unit.Pathing.SetSprinting();
        }
        else
        {
            unit.Pathing.SetWalking();
        }

		unit.Pathing.UpdateAIPath(destination, destination, shouldSprint ? MoveType.Sprinting : MoveType.Walking);
		Vector3 destinationThisTurn = unit.Pathing.ResultDestination;

		float movementBudget = unit.Pathing.MaxCost;
		PathNodeGrid grid = unit.Pathing.CurrentGrid;
		Vector3 successorPoint = destination;

        var longRangeToShorRangeDistanceThreshold = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_LongRangeToShortRangeDistanceThreshold).FloatVal;

        if (grid.GetValidPathNodeAt(destinationThisTurn, movementBudget) == null || (destinationThisTurn - destination).magnitude > longRangeToShorRangeDistanceThreshold)
        {
            List<AbstractActor> lanceUnits = AIUtil.GetLanceUnits(unit.Combat, unit.LanceId);
            List<Vector3> path = DynamicLongRangePathfinder.GetDynamicPathToDestination(destination, movementBudget, unit, shouldSprint, lanceUnits, grid, 0);
            if (path == null || path.Count == 0)
            {
                return new BehaviorTreeResults(BehaviorNodeState.Failure);
            }

            destinationThisTurn = path[path.Count - 1];

            Vector2 flatDestination = new Vector2(destination.x, destination.z);

            float currentClosestPointInRegionDistance = float.MaxValue;
            Vector3? closestPoint = null;

            for (int i = 0; i < path.Count; ++i)
            {
                Vector3 pointOnPath = path[i];

                if (RegionUtil.PointInRegion(unit.Combat, pointOnPath, regionGUID))
                {
                    var distance = (flatDestination - new Vector2(pointOnPath.x, pointOnPath.z)).sqrMagnitude;

                    if (distance < currentClosestPointInRegionDistance)
                    {
                        currentClosestPointInRegionDistance = distance;
                        closestPoint = pointOnPath;
                    }
                }
            }

            if (closestPoint != null)
            {
                destinationThisTurn = closestPoint.Value;
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
		mvtOrderInfo.IsSprinting = shouldSprint;
		results.orderInfo = mvtOrderInfo;
		results.debugOrderString = string.Format("{0}: dest:{1} sprint:{2}", this.name, destination, mvtOrderInfo.IsSprinting);
		return results;
	}
}

class IsInsideEncounterBoundsNode : LeafBehaviorNode
{
	public IsInsideEncounterBoundsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		return BehaviorTreeResults.BehaviorTreeResultsFromBoolean(unit.Combat.EncounterLayerData.IsInEncounterBounds(unit.CurrentPosition));
	}
}

class MoveInsideEncounterBoundsNode : LeafBehaviorNode
{
	public MoveInsideEncounterBoundsNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
	{
	}

	override protected BehaviorTreeResults Tick()
	{
		BattleTech.Designed.EncounterBoundaryChunkGameLogic boundaryChunk = unit.Combat.EncounterLayerData.encounterBoundaryChunk;

		if (boundaryChunk.IsInEncounterBounds(unit.CurrentPosition))
		{
			return new BehaviorTreeResults(BehaviorNodeState.Success);
		}

		// find closest center
		float bestDist = float.MaxValue;
		Vector3 destination = Vector3.zero;

		if (boundaryChunk.encounterBoundaryRectList.Count == 0)
		{
			return new BehaviorTreeResults(BehaviorNodeState.Failure);
		}

		for (int i = 0; i < boundaryChunk.encounterBoundaryRectList.Count; ++i)
		{
			RectHolder rh = boundaryChunk.encounterBoundaryRectList[i];
			Vector3 c = rh.rect.center;
			float dist = (unit.CurrentPosition - c).magnitude;

			if (dist < bestDist)
			{
				bestDist = dist;
				destination = c;
			}
		}

		if ((destination - unit.CurrentPosition).magnitude < 1)
		{
			// already close (should probably have been caught, above)
			return new BehaviorTreeResults(BehaviorNodeState.Success);
		}

		unit.Pathing.UpdateAIPath(destination, destination, MoveType.Sprinting);
		Vector3 destinationThisTurn = unit.Pathing.ResultDestination;

		float movementBudget = unit.Pathing.MaxCost;
		PathNodeGrid grid = unit.Pathing.CurrentGrid;
		Vector3 successorPoint = destination;
		if ((grid.GetValidPathNodeAt(destinationThisTurn, movementBudget) == null) ||
			((destinationThisTurn - destination).magnitude > 1.0f))
		{
			// can't get all the way to the destination.
			if (unit.Combat.EncounterLayerData.inclineMeshData != null)
			{
                List<AbstractActor> lanceUnits = AIUtil.GetLanceUnits(unit.Combat, unit.LanceId);
                List<Vector3> path = DynamicLongRangePathfinder.GetDynamicPathToDestination(destinationThisTurn, movementBudget, unit, true, lanceUnits, unit.Pathing.CurrentGrid, 100.0f);

                if ((path != null) && (path.Count > 0))
                {
                    destinationThisTurn = path[path.Count - 1];
                }
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
		mvtOrderInfo.IsSprinting = true;
		results.orderInfo = mvtOrderInfo;
		results.debugOrderString = string.Format("{0}: dest:{1} sprint:{2}", this.name, destination, mvtOrderInfo.IsSprinting);
		return results;
	}
}

