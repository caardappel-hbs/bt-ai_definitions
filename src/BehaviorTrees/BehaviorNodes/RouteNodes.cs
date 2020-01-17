using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BattleTech;
using System;

public class RoutingUtil
{
    public static RouteGameLogic FindRouteByGUID(BehaviorTree tree, string patrolRouteGUID)
    {
        ITaggedItem item = tree.battleTechGame.Combat.ItemRegistry.GetItemByGUID(patrolRouteGUID);
        return item as RouteGameLogic;
    }

    public static Vector3 Decrowd(Vector3 target, AbstractActor unit)
    {
        float myRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PersonalSpaceRadius).FloatVal;
        List<ITaggedItem> units = unit.BehaviorTree.battleTechGame.Combat.ItemRegistry.WithType(TaggedObjectType.Unit).Search();

        float testOffsetRadius = 0.0f;
        float testOffsetAngle = 0.0f;

        while (testOffsetRadius < 10.0f * myRadius)
        {
            Vector3 candidatePoint = new Vector3(target.x + Mathf.Cos(testOffsetAngle) * testOffsetRadius,
                                   target.y,
                                target.z + Mathf.Sin(testOffsetAngle) * testOffsetRadius);

            bool anyViolatedConstraints = false;

            for (int unitIndex = 0; unitIndex < units.Count; ++unitIndex)
            {
                AbstractActor otherUnit = units[unitIndex] as AbstractActor;
                if (otherUnit == unit)
                {
                    continue;
                }

                float otherRadius = 0.0f;
                if (otherUnit.BehaviorTree != null)
                {
                    BehaviorVariableValue otherRadiusValue = otherUnit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PersonalSpaceRadius);
                    if (otherRadiusValue != null)
                    {
                        otherRadius = otherRadiusValue.FloatVal;
                    }
                }
                float desiredRadius = myRadius + otherRadius;

                Vector3 constraintVector = candidatePoint - otherUnit.CurrentPosition;
                float constraintVectorMag = constraintVector.magnitude;
                if (constraintVectorMag < desiredRadius)
                {
                    anyViolatedConstraints = true;
                    break;
                }
            }
            if (!anyViolatedConstraints)
            {
                return candidatePoint;
            }

            float newWrap = testOffsetAngle * testOffsetRadius + myRadius;
            if (newWrap > Mathf.PI * 2 * testOffsetRadius)
            {
                testOffsetRadius += myRadius;
            }

            if (testOffsetRadius > 0.0f)
            {
                testOffsetAngle = newWrap / testOffsetRadius;
                testOffsetAngle = testOffsetAngle % (Mathf.PI * 2);
            }
        }

        // TODO: do something fancier here.
        return target;
    }

    public static bool AllUnitsInsideRadiusOfPoint(List<AbstractActor> units, Vector3 target, float radius)
    {
        for (int i = 0; i < units.Count; ++i)
        {
            float dist = (units[i].CurrentPosition - target).magnitude;
            if (dist > radius)
            {
                return false;
            }
        }
        return true;
    }
}


class UnitHasRouteNode : LeafBehaviorNode
{
    public UnitHasRouteNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_RouteGUID);
        if (variableValue == null)
        {
            // No route, this is normal.
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        string patrolRouteGUID = variableValue.StringVal;

        RouteGameLogic route = RoutingUtil.FindRouteByGUID(tree, patrolRouteGUID);

        if (route == null)
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


class MoveAlongRouteNode : LeafBehaviorNode
{
    abstract class PatrolRouteWaypoints
    {
        protected int currentWaypoint;
        public bool goingForward;
        protected int routeSize;

        public PatrolRouteWaypoints(int currentWaypoint, bool goingForward, int routeSize)
        {
            this.currentWaypoint = currentWaypoint;
            this.goingForward = goingForward;
            this.routeSize = routeSize;
            Debug.Assert(routeSize > 0);
        }

        protected int step()
        {
            return goingForward ? 1 : -1;
        }

        abstract public IEnumerable<int> GetWaypoints();
    }

    class CircuitRouteWaypoints : PatrolRouteWaypoints
    {
        public CircuitRouteWaypoints(int currentWaypoint, bool goingForward, int routeSize) : base(currentWaypoint, goingForward, routeSize)
        {
        }

        override public IEnumerable<int> GetWaypoints()
        {
            while (true)
            {
                yield return currentWaypoint;
                currentWaypoint += this.step();
                if (currentWaypoint >= routeSize)
                {
                    currentWaypoint = 0;
                }
                else if (currentWaypoint < 0)
                {
                    currentWaypoint = routeSize - 1;
                }
            }
        }
    }

    class PingPongRouteWaypoints : PatrolRouteWaypoints
    {
        public PingPongRouteWaypoints(int currentWaypoint, bool goingForward, int routeSize) : base(currentWaypoint, goingForward, routeSize)
        {
        }

        override public IEnumerable<int> GetWaypoints()
        {
            while (true)
            {
                yield return currentWaypoint;
                currentWaypoint += this.step();
                if (currentWaypoint >= routeSize)
                {
                    currentWaypoint = routeSize - 2;
                    goingForward = false;
                }
                else if (currentWaypoint < 0)
                {
                    currentWaypoint = 1;
                    goingForward = true;
                }
            }
        }
    }

    class OneWayRouteWaypoints : PatrolRouteWaypoints
    {
        public OneWayRouteWaypoints(int currentWaypoint, bool goingForward, int routeSize) : base(currentWaypoint, goingForward, routeSize)
        {
        }

        override public IEnumerable<int> GetWaypoints()
        {
            while(true)
            {
                yield return currentWaypoint;
                currentWaypoint += this.step();
                if ((currentWaypoint >= routeSize) || (currentWaypoint < 0))
                {
                    break;
                }
            }
        }
    }


    public MoveAlongRouteNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    RouteGameLogic getRoute()
    {
        BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_RouteGUID);
        if (variableValue == null)
        {
            // no route
            AIUtil.LogAI("No behavior variable for route GUID found");
            return null;
        }

        string routeGUID = variableValue.StringVal;

        return RoutingUtil.FindRouteByGUID(tree, routeGUID);
    }

    void findLastReachablePatrolWaypoint(Vector3 startPosition, RouteGameLogic patrolRoute, List<PathNode> pathNodes, PatrolRouteWaypoints waypointIterator, float destinationRadius, out Vector3 lastReachablePoint, out Vector3 firstUnreachableWayPoint, out int nextWaypointIndex, out bool completedRoute)
    {
        List<int> indicesConsideredThisTurn = new List<int>();  // to keep us from visiting the same node twice in a turn, either in a loop or in a ping-pong

        // We can definitely reach the start point
        lastReachablePoint = startPosition;

        // The following is just to get past the compiler error of assigning an out param. 
        firstUnreachableWayPoint = startPosition;
        PathNode tempPathNode = null;
        PathNode realPathNode = null;

        foreach (int waypointIndex in waypointIterator.GetWaypoints())
        {
            if (indicesConsideredThisTurn.IndexOf(waypointIndex) >= 0)
            {
                // we've already been here
                nextWaypointIndex = waypointIndex;
                completedRoute = false;
                return;
            }

            Vector3 wayPoint = patrolRoute.routePointList[waypointIndex].Position;
            firstUnreachableWayPoint = wayPoint;

            Vector3 foundPoint;

            // Try and find 
            if (findClosestWalkablePointToDestination(unit.Combat.HexGrid, wayPoint, startPosition, pathNodes, destinationRadius, out foundPoint, out tempPathNode))
            {
                // If a pathnode was found, update the realPathNode.
                if (tempPathNode != null)
                    realPathNode = tempPathNode;

                lastReachablePoint = foundPoint;
                indicesConsideredThisTurn.Add(waypointIndex);
            }
            else
            {
                // At this point we have a start point we can reach and the next waypoint we can't reach. Try to find
                // the closest point in the path node to the next waypoint. We're going to find the closest point to the 
                // firstUnreachableWayPoint in our pathNodes that pass through the lastPathNode that we can reach.
                float closestDistance = AIUtil.Get2DSquaredDistanceBetweenVector3s(lastReachablePoint, firstUnreachableWayPoint);
                PathNode closestPathNode = realPathNode;

                // Loop through all our path nodes.
                for (int i = 0; i < pathNodes.Count; ++i)
                {
                    // Find the distance from this node to the first unreachable waypoint.
                    PathNode currentPathNode = pathNodes[i];
                    float currentDistance = AIUtil.Get2DSquaredDistanceBetweenVector3s(currentPathNode.Position, firstUnreachableWayPoint);

                    // If this node is closer to the destination than our current best node
                    if (currentDistance <= closestDistance)
                    {
                        // If we couldn't reach the first node, just move towards the first node as best we can.
                        if (realPathNode == null)
                        {
                            closestPathNode = pathNodes[i];
                            closestDistance = currentDistance;
                            continue;
                        }

                        // See if this path walks through the previous waypoint.
                        while (currentPathNode != null)
                        {
                            // If so, change our closest node.
                            if (currentPathNode == realPathNode)
                            {
                                closestPathNode = pathNodes[i];
                                closestDistance = currentDistance;
                                break;
                            }
                            currentPathNode = currentPathNode.Parent;
                        }
                    }
                }

                lastReachablePoint = closestPathNode.Position;
                nextWaypointIndex = waypointIndex;
                completedRoute = false;
                return;
            }
        }

        // used up all waypoints
        nextWaypointIndex = -1;
        completedRoute = true;
    }

    bool findClosestWalkablePointToDestination(HexGrid grid, Vector3 destination, Vector3 startLoc, List<PathNode> pathNodes, float destinationRadius, out Vector3 foundPoint, out PathNode foundPathNode)
    {
        bool foundAny = false;
        foundPoint = Vector3.zero;
        foundPathNode = null;

        float closestDistanceToDestination = float.MaxValue;
        float closestDistanceToStart = float.MaxValue; // used for tie-breaking between nodes of equal distance to destination

        for (int nodeIndex = 0; nodeIndex < pathNodes.Count; ++nodeIndex)
        {
            PathNode node = pathNodes[nodeIndex];
            float distToDest = AIUtil.Get2DDistanceBetweenVector3s(node.Position, destination);
            if (distToDest <= destinationRadius)
            {
                float distToStart = AIUtil.Get2DDistanceBetweenVector3s(node.Position, startLoc);
                if ((distToDest < closestDistanceToDestination) ||
                    ((distToDest == closestDistanceToDestination) &&
                     (distToStart < closestDistanceToStart)))
                {
                    foundAny = true;
                    foundPathNode = node;
                    foundPoint = node.Position;
                    closestDistanceToDestination = distToDest;
                    closestDistanceToStart = distToStart;
                }
            }
        }

        return foundAny;
    }

    Vector3 getReachablePointOnRoute(Vector3 startPosition, RouteGameLogic patrolRoute, PatrolRouteWaypoints waypointIterator, List<PathNode> validDestinations, out bool isComplete, out int nextWaypointIndex, out bool nextPointGoesForward, out Vector3 successorPoint)
    {
        nextWaypointIndex = -1;
        nextPointGoesForward = false;

        float destinationRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_RouteWaypointRadius).FloatVal;
        //Vector3 foundDest;
        Vector3 lastReachablePoint;
        Vector3 firstUnreachableWaypoint;

        List<AbstractActor> lanceUnits = AIUtil.GetLanceUnits(unit.Combat, unit.LanceId);

        bool completedRoute;
        findLastReachablePatrolWaypoint(startPosition, patrolRoute, validDestinations, waypointIterator, destinationRadius, out lastReachablePoint, out firstUnreachableWaypoint, out nextWaypointIndex, out completedRoute);
        successorPoint = startPosition;

        if (completedRoute)
        {
            isComplete = true;
            nextWaypointIndex = patrolRoute.routePointList.Length - 1;
            nextPointGoesForward = true;
            // look away from the foundDest in the direction that we moved
            // TODO(dlecompte) try to look away from the previous waypoint? Or maybe the pathnode's previous parent?
            successorPoint = lastReachablePoint + (lastReachablePoint - startPosition);
            // TODO(dlecompte) this ignores maxDist, probably could prune smarter.
            return lastReachablePoint;
        }

        Vector3 nextWaypoint = patrolRoute.routePointList[nextWaypointIndex].Position;

        nextPointGoesForward = waypointIterator.goingForward;
        successorPoint = nextWaypoint;
        isComplete = false;

        return lastReachablePoint;
    }

    Vector3 FindLastPointOnPathInNodeList(HexGrid grid, List<Vector3> path, List<PathNode> pathNodes, out bool foundAny)
    {
        Debug.Assert(path != null);
        Debug.Assert(path.Count > 0);
        Vector3 lastPoint = Vector3.zero; // bogus point
        foundAny = false;
        for (int pathIndex = 0; pathIndex < path.Count; ++pathIndex)
        {
            Vector3 pathPoint = path[pathIndex];

            int pathNodeIndex = FindIndexOfPointInNodeList(grid, pathPoint, pathNodes, findClosest:true);
            if (pathNodeIndex == -1)
            {
                // we're done
                break;
            }
            lastPoint = pathPoint;
            foundAny = true;
        }
        return lastPoint;
    }

    int FindIndexOfPointInNodeList(HexGrid grid, Vector3 point, List<PathNode> pathNodes, bool findClosest = false)
    {
        HBS.Math.HexPoint3 hexPoint = unit.Combat.HexGrid.GetClosestHexPoint3OnGrid(point);

        float closestDistance = float.MaxValue;
        int closestIndex = -1;

        for (int i = 0; i < pathNodes.Count; ++i)
        {
            PathNode pn = pathNodes[i];

            HBS.Math.HexPoint3 testHexPoint = unit.Combat.HexGrid.GetClosestHexPoint3OnGrid(pn.Position);

            if (hexPoint == testHexPoint)
            {
                return i;
            }

            if (findClosest)
            {
                float distance = AIUtil.Get2DDistanceBetweenVector3s(point, pn.Position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }
        }
        if (findClosest)
            return closestIndex;
        else
            return -1;
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorTreeResults results;

        if (unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteCompleted).BoolVal)
        {
            results = new BehaviorTreeResults(BehaviorNodeState.Success);
            results.orderInfo = new OrderInfo(OrderType.Brace);
            results.debugOrderString = string.Format("{0}: bracing for end of patrol route", this.name);
            return results;
        }

        bool isSprinting = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteShouldSprint).BoolVal;

        if (isSprinting && unit.CanSprint)
        {
            unit.Pathing.SetSprinting();
        }
        else
        {
            unit.Pathing.SetWalking();
        }

        PathNodeGrid grid = unit.Pathing.CurrentGrid;
        if (grid.UpdateBuild(25) > 0)
        {
            // have to wait for the grid to build.
            results = new BehaviorTreeResults(BehaviorNodeState.Running);
            return results;
        }

        if (!unit.Pathing.ArePathGridsComplete)
        {
            // have to wait for the grid to build.
            results = new BehaviorTreeResults(BehaviorNodeState.Running);
            return results;
        }

        float destinationRadius = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_RouteWaypointRadius).FloatVal;

        RouteGameLogic myPatrolRoute = getRoute();
        if (myPatrolRoute == null)
        {
            AIUtil.LogAI("Move Along Route failing because no route found", unit);
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        BehaviorVariableValue nrpiVal = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Int_RouteTargetPoint);
        int nextRoutePointIndex = (nrpiVal != null) ? nrpiVal.IntVal : 0;
        BehaviorVariableValue pfVal = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteFollowingForward);
        bool patrollingForward = (pfVal != null) ? pfVal.BoolVal : true;

        PatrolRouteWaypoints routeWaypointIterator = null;

        switch (myPatrolRoute.routeTransitType)
        {
            case RouteTransitType.Circuit:
                routeWaypointIterator = new CircuitRouteWaypoints(nextRoutePointIndex, patrollingForward, myPatrolRoute.routePointList.Length);
                break;
            case RouteTransitType.OneWay:
                routeWaypointIterator = new OneWayRouteWaypoints(nextRoutePointIndex, patrollingForward, myPatrolRoute.routePointList.Length);
                break;
            case RouteTransitType.PingPong:
                routeWaypointIterator = new PingPongRouteWaypoints(nextRoutePointIndex, patrollingForward, myPatrolRoute.routePointList.Length);
                break;
            default:
                Debug.LogError("Invalid route transit type: " + myPatrolRoute.routeTransitType);
                AIUtil.LogAI("Move Along Route failing because patrol route was set to an invalid transit type: " + myPatrolRoute.routeTransitType, unit);
                return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        float movementAvailable = unit.Pathing.MaxCost * unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_PatrolRouteThrottlePercentage).FloatVal / 100.0f;

        bool isComplete = false;
        int nextWaypoint = -1;
        bool nextPointGoesForward = false;
        Vector3 successorPoint;
        List<PathNode> availablePathNodes = unit.Pathing.CurrentGrid.GetSampledPathNodes();

        // prune for region
        string regionGUID = RegionUtil.StayInsideRegionGUID(unit);
        if (!string.IsNullOrEmpty(regionGUID))
        {
            availablePathNodes = availablePathNodes.FindAll(node => RegionUtil.PointInRegion(unit.Combat, node.Position, regionGUID));
        }

        string guardGUID = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.String_GuardLanceGUID).StringVal;
        Lance guardLance = guardGUID != null ? unit.Combat.ItemRegistry.GetItemByGUID<Lance>(guardGUID) : null;

        // if guarding units, adjust movement available to account for their speed
        if (guardLance != null)
        {
            movementAvailable = adjustMovementAvailableForGuardLance(unit, movementAvailable, guardLance);
        }

        // prune for distance from start point
        availablePathNodes = availablePathNodes.FindAll(node => node.CostToThisNode <= movementAvailable);

        // if there is a guarding lance, make sure that we're not moving out of the lance tether
        if (guardLance != null)
        {
            availablePathNodes = filterAvailablePathNodesForGuardTether(unit, availablePathNodes, guardLance);
        }


        Vector3 patrolPoint = getReachablePointOnRoute(unit.CurrentPosition, myPatrolRoute, routeWaypointIterator, availablePathNodes, out isComplete, out nextWaypoint, out nextPointGoesForward, out successorPoint);

        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Bool_RouteFollowingForward, new BehaviorVariableValue(nextPointGoesForward));
        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Int_RouteTargetPoint, new BehaviorVariableValue(nextWaypoint));
        unit.BehaviorTree.unitBehaviorVariables.SetVariable(BehaviorVariableName.Bool_RouteCompleted, new BehaviorVariableValue(isComplete));

        //Vector3 destination = RegionUtil.MaybeClipMovementDestinationToStayInsideRegion(unit, patrolPoint);
        Vector3 destination = patrolPoint;

        if (!isComplete)
        {
            List<PathNode> path = constructPath(unit.Combat.HexGrid, destination, availablePathNodes);

            if ((path.Count == 0) || ((path.Count == 1) && (AIUtil.Get2DDistanceBetweenVector3s(path[0].Position, unit.CurrentPosition) < 1)))
            {
                // can't actually make progress - fail here, and presumably pass later on.
                AIUtil.LogAI("Move Along Route failing because no nodes in path.", unit);

                DialogueGameLogic proximityDialogue = unit.Combat.ItemRegistry.GetItemByGUID<DialogueGameLogic>(unit.Combat.Constants.CaptureEscortProximityDialogID);

                if (proximityDialogue != null)
                {
                    TriggerDialog triggerDialogueMessage = new TriggerDialog(unit.GUID, unit.Combat.Constants.CaptureEscortProximityDialogID, async: false);
                    unit.Combat.MessageCenter.PublishMessage(triggerDialogueMessage);
                }
                else
                {
                    Debug.LogError("Could not find CaptureEscortProximityDialog. This is only a real error message if this is a Capture Escort (Normal Escort) mission. For other missions (Story, Ambush Convoy, etc) you can safely ignore this error message.");
                }

                return new BehaviorTreeResults(BehaviorNodeState.Failure);
            }

            destination = path[path.Count - 1].Position;
        }

        Vector3 cur = unit.CurrentPosition;

        if ((destination - cur).magnitude < 1)
        {
            // can't actually make progress - fail here, and presumably pass later on.
            AIUtil.LogAI("Move Along Route failing because destination too close to unit start.", unit);
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        AIUtil.LogAI(string.Format("issuing order from [{0} {1} {2}] to [{3} {4} {5}] looking at [{6} {7} {8}]",
            cur.x, cur.y, cur.z,
            destination.x, destination.y, destination.z,
            successorPoint.x, successorPoint.y, successorPoint.z
        ), unit);

        results = new BehaviorTreeResults(BehaviorNodeState.Success);
        MovementOrderInfo mvtOrderInfo = new MovementOrderInfo(destination, successorPoint);
        mvtOrderInfo.IsSprinting = isSprinting;
        results.orderInfo = mvtOrderInfo;
        results.debugOrderString = string.Format("{0}: dest:{1} sprint:{2}", this.name, destination, mvtOrderInfo.IsSprinting);
        return results;
    }

    float adjustMovementAvailableForGuardLance(AbstractActor unit, float movementAvailable, Lance guardLance)
    {
        float minUnitMoveDistance = float.MaxValue;

        // make sure I don't go faster than the slowest unit in the guard lance
        for (int i = 0; i < guardLance.unitGuids.Count; ++i)
        {
            string unitGUID = guardLance.unitGuids[i];
            AbstractActor guardUnitActor = unit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(unitGUID);
            if ((guardUnitActor != null) && (!guardUnitActor.IsDead))
            {
                float maxMoveDistance = Mathf.Max(guardUnitActor.MaxSprintDistance, guardUnitActor.MaxWalkDistance);
                minUnitMoveDistance = Mathf.Min(minUnitMoveDistance, maxMoveDistance);
            }
        }

        // also make sure I don't go faster than the slowest unit in my lance
        for (int i = 0; i < unit.lance.unitGuids.Count; ++i)
        {
            string unitGUID = unit.lance.unitGuids[i];
            AbstractActor lanceUnitActor = unit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(unitGUID);
            if ((lanceUnitActor != null) && (!lanceUnitActor.IsDead))
            {
                float maxMoveDistance = Mathf.Max(lanceUnitActor.MaxSprintDistance, lanceUnitActor.MaxWalkDistance);
                minUnitMoveDistance = Mathf.Min(minUnitMoveDistance, maxMoveDistance);
            }
        }

        float guardSpeedPct = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_GuardLanceSpeedPercent).FloatVal;
        float guardSpeedFrac = guardSpeedPct / 100.0f;

        float availableMoveDistance = minUnitMoveDistance * guardSpeedFrac;
        return Mathf.Min(movementAvailable, availableMoveDistance);
    }

    List<PathNode> filterAvailablePathNodesForGuardTether(AbstractActor unit, List<PathNode> availablePathNodes, Lance guardLance)
    {
        float guardTetherDistance = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Float_GuardLanceTetherDistance).FloatVal;

        if (guardTetherDistance <= 0.0f)
        {
            // ignore the tether
            return availablePathNodes;
        }

        List<PathNode> goodNodes = new List<PathNode>();

        // accept nodes inside the tether distance

        for (int pnIndex = 0; pnIndex < availablePathNodes.Count; ++pnIndex)
        {
            PathNode node = availablePathNodes[pnIndex];

            if (isPathNodeInsideTetherDistanceFromGuardLance(node, guardTetherDistance, guardLance))
            {
                goodNodes.Add(node);
            }
        }

        if (goodNodes.Count > 0)
        {
            return goodNodes;
        }

        // otherwise, accept nodes that are closing the distance to the guard lance

        float startDist = findDistanceFromGuardLance(unit.CurrentPosition, guardLance);

        for (int pnIndex = 0; pnIndex < availablePathNodes.Count; ++pnIndex)
        {
            PathNode node = availablePathNodes[pnIndex];

            float newDist = findDistanceFromGuardLance(node.Position, guardLance);

            if (newDist < startDist)
            {
                goodNodes.Add(node);
            }
        }

        return goodNodes;
    }

    List<PathNode> constructPath(HexGrid grid, Vector3 destinationPosition, List<PathNode> availablePathNodes)
    {
        int index = FindIndexOfPointInNodeList(grid, destinationPosition, availablePathNodes, findClosest:true);
        List<PathNode> pathNodes = new List<PathNode>();
        if (index == -1)
        {
            return pathNodes;
        }
        PathNode currentPathNode = availablePathNodes[index];
        while (true)
        {
            pathNodes.Insert(0, currentPathNode);
            if (currentPathNode.Parent == null)
            {
                return pathNodes;
            }
            currentPathNode = currentPathNode.Parent;
        }
    }

    bool isPathNodeInsideTetherDistanceFromGuardLance(PathNode pathNode, float guardTetherDistance, Lance guardLance)
    {
        return (findDistanceFromGuardLance(pathNode.Position, guardLance) <= guardTetherDistance);
    }

    float findDistanceFromGuardLance(Vector3 position, Lance guardLance)
    {
        float bestDistance = float.MaxValue;

        for (int i = 0; i < guardLance.unitGuids.Count; ++i)
        {
            string unitGUID = guardLance.unitGuids[i];
            AbstractActor guardUnitActor = unit.Combat.ItemRegistry.GetItemByGUID<AbstractActor>(unitGUID);
            if ((guardUnitActor != null) && (!guardUnitActor.IsDead))
            {
                bestDistance = Mathf.Min(bestDistance, AIUtil.Get2DDistanceBetweenVector3s(guardUnitActor.CurrentPosition, position));
            }
        }
        return bestDistance;
    }
}


class LanceHasCompletedRouteNode : LeafBehaviorNode
{
    public LanceHasCompletedRouteNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.lance == null)
        {
            Debug.Log("No lance for this unit found");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        BehaviorVariableValue startedBV = unit.lance.BehaviorVariables.GetVariable(BehaviorVariableName.Bool_RouteCompleted);

        if ((startedBV != null) && (startedBV.BoolVal))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}


class LanceHasStartedRouteNode : LeafBehaviorNode
{
    public LanceHasStartedRouteNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    override protected BehaviorTreeResults Tick()
    {
        if (unit.lance == null)
        {
            Debug.Log("No lance for this unit found");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        BehaviorVariableValue startedBV = unit.lance.BehaviorVariables.GetVariable(BehaviorVariableName.Bool_RouteStarted);

        if ((startedBV != null) && (startedBV.BoolVal))
        {
            return new BehaviorTreeResults(BehaviorNodeState.Success);
        }
        else
        {
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }
    }
}


class LanceStartRouteNode : LeafBehaviorNode
{
    public LanceStartRouteNode(string name, BehaviorTree tree, AbstractActor unit) : base(name, tree, unit)
    {
    }

    int closestPointOnRouteToLance(RouteGameLogic route, Lance lance)
    {
        Vector3 lanceCenter = Vector3.zero;
        int count = 0;
        for (int unitIndex = 0; unitIndex < lance.unitGuids.Count; ++unitIndex)
        {
            string unitGUID = lance.unitGuids[unitIndex];
            ITaggedItem item = tree.battleTechGame.Combat.ItemRegistry.GetItemByGUID(unitGUID);
            if (item == null)
            {
                continue;
            }
            AbstractActor unit = item as AbstractActor;
            if (unit == null)
            {
                continue;
            }
            if (unit.IsDead)
            {
                continue;
            }
            ++count;
            lanceCenter += unit.CurrentPosition;
        }

        if (count == 0)
        {
            return 0;
        }

        lanceCenter *= 1.0f / count;

        float bestDist = float.MaxValue;
        int bestIndex = -1;

        // Now iterate over all points, to find closest point to this center point.

        for (int waypointIndex = 0; waypointIndex < route.routePointList.Length; ++waypointIndex)
        {
            RoutePointGameLogic point = route.routePointList[waypointIndex];
            float dist = (point.Position - lanceCenter).magnitude;
            if (dist < bestDist)
            {
                bestIndex = waypointIndex;
                bestDist = dist;
            }
        }
        return bestIndex;
    }

    override protected BehaviorTreeResults Tick()
    {
        BehaviorVariableValue variableValue = tree.GetBehaviorVariableValue(BehaviorVariableName.String_RouteGUID);
        if (variableValue == null)
        {
            Debug.Log("No behavior variable for patrol route GUID found");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        string patrolRouteGUID = variableValue.StringVal;
        RouteGameLogic route = RoutingUtil.FindRouteByGUID(tree, patrolRouteGUID);

        if (route == null)
        {
            Debug.Log("No route matching GUID found");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        if (unit.lance == null)
        {
            Debug.Log("No lance for this unit found");
            return new BehaviorTreeResults(BehaviorNodeState.Failure);
        }

        BehaviorVariableValue closestPointBV = tree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteStartAtClosestPoint);
        BehaviorVariableValue forwardBV = tree.GetBehaviorVariableValue(BehaviorVariableName.Bool_RouteFollowingForward);

        int routeTargetPoint = 0;

        if (closestPointBV.BoolVal)
        {
            routeTargetPoint = closestPointOnRouteToLance(route, unit.lance);
        }
        else
        {
            if (forwardBV.BoolVal)
            {
                routeTargetPoint = 0;
            }
            else
            {
                routeTargetPoint = route.routePointList.Length - 1;
            }
        }
        unit.lance.BehaviorVariables.SetVariable(BehaviorVariableName.Int_RouteTargetPoint, new BehaviorVariableValue(routeTargetPoint));
        unit.lance.BehaviorVariables.SetVariable(BehaviorVariableName.Bool_RouteStarted, new BehaviorVariableValue(true));
        unit.lance.BehaviorVariables.SetVariable(BehaviorVariableName.Bool_RouteCompleted, new BehaviorVariableValue(false));
        unit.lance.BehaviorVariables.SetVariable(BehaviorVariableName.Bool_RouteFollowingForward, new BehaviorVariableValue(forwardBV.BoolVal));

        return new BehaviorTreeResults(BehaviorNodeState.Success);
    }
}


