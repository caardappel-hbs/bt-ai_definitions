using System.Collections.Generic;
using System.IO;
using UnityEngine;

using HBS.Math;

namespace BattleTech
{
	public class DynamicLongRangePathfinder
	{
        public class PointWithCost : System.IComparable<PointWithCost>, System.IComparable
		{
			public HexPoint3 point;
			public float cost;
			public float estimatedTotalCost;
			public PathNode pathNode;

			public PointWithCost(HexPoint3 point, float cost, float estimatedTotalCost)
			{
				this.point = point;
				this.cost = cost;
				this.estimatedTotalCost = estimatedTotalCost;
			}

			public int CompareTo(PointWithCost other)
			{
				return this.estimatedTotalCost.CompareTo(other.estimatedTotalCost);
			}

			public int CompareTo(object other)
			{
				PointWithCost otherPt = other as PointWithCost;
				if (otherPt == null)
				{
					return 1;
				}
				else
				{
					return this.CompareTo(otherPt);
				}
			}
		}

        /// <summary>
        /// Finds a path from any of start to goal, such that the path has no links that are steeper than the unit's maxGrade.
        /// </summary>
        /// <returns>The path.</returns>
        /// <param name="startPointList">List of PointWithDistances for points to start from</param>
        /// <param name="snappedGoalPoint">HexPoint3 to go to</param>
        /// <param name="unit">moving unit</param>
        /// <param name="moveType">move type - walk, sprint</param>
        /// <param name="targetRadius">how close to get to the target</param>
        /// <param name="actorAware">Discard nodes where other actors reside</param>
        public static List<PointWithCost> FindPath(List<PointWithCost> startPointList, HexPoint3 snappedGoalPoint, AbstractActor unit, MoveType moveType, float targetRadius, bool actorAware)
        {
            MapMetaData mapMetaData = unit.Combat.MapMetaData;
            HexGrid hexGrid = unit.Combat.HexGrid;
            unit.Pathing.MoveType = moveType;
            List<AbstractActor> actors = null;

            bool startedInEncounterBounds = false;
            BattleTech.Designed.EncounterBoundaryChunkGameLogic boundaryChunk = unit.Combat.EncounterLayerData.encounterBoundaryChunk;

            for (int spi = 0; spi < startPointList.Count; ++spi)
            {
                PointWithCost sp = startPointList[spi];
                //Vector3 wp = HexPoint3ToWorldPoint(sp.point, hexGrid);

                if (boundaryChunk.IsInEncounterBounds(unit.CurrentPosition))
                {
                    startedInEncounterBounds = true;
                    break;
                }
            }

            actorAware = unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_EnableLongRangePathfindingBeActorAware).BoolVal ? true : actorAware;

            if (actorAware)
            {
                actors = unit.Combat.AllActors;
                actors.Remove(unit);
            }

            List<PointWithCost> path = new List<PointWithCost>();

            HeapQueue<PointWithCost> openHeap = new HeapQueue<PointWithCost>();

            Dictionary<HexPoint3, float> bestCostDict = new Dictionary<HexPoint3, float>();

            Dictionary<HexPoint3, PointWithCost> bestPrevPoint = new Dictionary<HexPoint3, PointWithCost>();

            Vector3 worldGoalPoint = HexPoint3ToWorldPoint(snappedGoalPoint, hexGrid);

            float bestPathCost = float.MaxValue;
            bool anyPathFound = false;
            PointWithCost bestGoalPoint = new PointWithCost(new HexPoint3(-4000, -4000),
                float.MaxValue,
                float.MaxValue);

            for (int startIndex = 0; startIndex < startPointList.Count; ++startIndex)
            {
                PointWithCost pwd = startPointList[startIndex];
                openHeap.Push(pwd);
                bestCostDict[pwd.point] = pwd.cost;
                bestPrevPoint[pwd.point] = null;

                Vector3 wp = HexPoint3ToWorldPoint(pwd.point, hexGrid);

                if ((pwd.point.Equals(snappedGoalPoint)) ||
                    (AIUtil.Get2DDistanceBetweenVector3s(wp, worldGoalPoint) < targetRadius))
                {
                    if (pwd.cost < bestPathCost)
                    {
                        anyPathFound = true;
                        bestPathCost = pwd.cost;
                        bestGoalPoint = pwd;
                    }
                }
            }

            while (!openHeap.IsEmpty())
            {
                PointWithCost ptWithCost = openHeap.PopMinimum();

                if (ptWithCost.estimatedTotalCost > bestPathCost)
                {
                    continue;
                }

                Vector3 worldPoint = HexPoint3ToWorldPoint(ptWithCost.point, hexGrid);

                if (actorAware && CheckForOccupiedPoint(actors, worldPoint))
                {
                    continue;
                }

                if (startedInEncounterBounds && (!boundaryChunk.IsInEncounterBounds(worldPoint)))
                {
                    continue;
                }

                for (int direction = 0; direction < 6; ++direction)
                {
                    HexPoint3 neighborHexPoint = ptWithCost.point.Step(direction, 1);
                    Vector3 neighborWorldPoint = HexPoint3ToWorldPoint(neighborHexPoint, hexGrid);

                    if ((!mapMetaData.IsWithinBounds(neighborWorldPoint)) ||
                        (unit.Pathing.CurrentGrid.FindBlockerReciprocal(worldPoint, neighborWorldPoint)))
                    {
                        continue;
                    }

                    Debug.DrawLine(worldPoint, neighborWorldPoint, Color.yellow, 15.0f);

                    float linkCost = unit.Pathing.CurrentGrid.GetTerrainModifiedCost(worldPoint, neighborWorldPoint);
                    float newCost = ptWithCost.cost + linkCost;

                    if (newCost >= bestPathCost)
                    {
                        continue;
                    }

                    if ((!bestCostDict.ContainsKey(neighborHexPoint)) ||
                        (newCost < bestCostDict[neighborHexPoint]))
                    {
                        bestCostDict[neighborHexPoint] = newCost;
                        bestPrevPoint[neighborHexPoint] = ptWithCost;

                        if ((neighborHexPoint.Equals(snappedGoalPoint)) ||
                            ((neighborWorldPoint - worldGoalPoint).magnitude < targetRadius))
                        {
                            if (newCost < bestPathCost)
                            {
                                anyPathFound = true;
                                bestPathCost = newCost;
                                bestGoalPoint = new PointWithCost(neighborHexPoint, newCost, 0.0f);
                            }
                        }
                        else
                        {
                            Vector3 remainingDistance = (worldGoalPoint - neighborWorldPoint);
                            float estRemainingCost = remainingDistance.magnitude;

                            openHeap.Push(new PointWithCost(neighborHexPoint, newCost, newCost + estRemainingCost));
                        }
                    }
                }
            }

            if (anyPathFound)
            {
                PointWithCost p = bestGoalPoint;
                path.Add(p);
                while (bestPrevPoint.ContainsKey(p.point))
                {
                    PointWithCost prevPoint = bestPrevPoint[p.point];
                    if ((prevPoint == null) || (path.Contains(prevPoint)))
                    {
                        break;
                    }
                    path.Insert(0, prevPoint);
                    p = prevPoint;
                }
            }
            else
            {
                // draw the failed path data
                const int SIDES = 3;
                const float RADIUS = 12;

                foreach (PointWithCost startPoint in startPointList)
                {
                    Vector3 worldStartPoint = HexPoint3ToWorldPoint(startPoint.point, hexGrid);
                    for (int i = 0; i < SIDES; ++i)
                    {
                        float dx0 = RADIUS * Mathf.Cos(i * Mathf.PI * 2 / SIDES);
                        float dz0 = RADIUS * Mathf.Sin(i * Mathf.PI * 2 / SIDES);
                        float dx1 = RADIUS * Mathf.Cos((i + 1) * Mathf.PI * 2 / SIDES);
                        float dz1 = RADIUS * Mathf.Sin((i + 1) * Mathf.PI * 2 / SIDES);

                        Vector3 wp0 = new Vector3(worldStartPoint.x + dx0, 0, worldStartPoint.z + dz0);
                        Vector3 wp1 = new Vector3(worldStartPoint.x + dx1, 0, worldStartPoint.z + dz1);
                        Debug.DrawLine(wp0, wp1, Color.magenta, 15.0f);
                    }
                }
                
                Vector3 worldEndPoint = HexPoint3ToWorldPoint(snappedGoalPoint, hexGrid);
                Color orangeColor = new Color(1.0f, 0.5f, 0.0f);
                for (int i = 0; i < SIDES; ++i)
                {
                    float dx0 = RADIUS * Mathf.Cos(i * Mathf.PI * 2 / SIDES);
                    float dz0 = RADIUS * Mathf.Sin(i * Mathf.PI * 2 / SIDES);
                    float dx1 = RADIUS * Mathf.Cos((i + 1) * Mathf.PI * 2 / SIDES);
                    float dz1 = RADIUS * Mathf.Sin((i + 1) * Mathf.PI * 2 / SIDES);

                    Vector3 wp0 = new Vector3(worldEndPoint.x + dx0, 0, worldEndPoint.z + dz0);
                    Vector3 wp1 = new Vector3(worldEndPoint.x + dx1, 0, worldEndPoint.z + dz1);
                    Debug.DrawLine(wp0, wp1, orangeColor, 15.0f);
                }
            }

            int removedCount = 0;
            // Now, check to see if the end of the path is in "danger". If it is, prune until it's not, which might lead to an empty path.
            while (path.Count > 0)
            {
                PointWithCost lastHexPoint = path[path.Count - 1];
                Vector3 lastWorldPoint = HexPoint3ToWorldPoint(lastHexPoint.point, hexGrid);
                MapTerrainDataCell dataCell = unit.Combat.MapMetaData.GetCellAt(lastWorldPoint);

                if (SplatMapInfo.IsDropshipLandingZone(dataCell.terrainMask) || SplatMapInfo.IsDangerousLocation(dataCell.terrainMask) || SplatMapInfo.IsDropPodLandingZone(dataCell.terrainMask))
                {
                    path.RemoveAt(path.Count - 1);
                    ++removedCount;
                }
                else
                {
                    break;
                }
            }

            if (removedCount > 0)
            {
                if (path.Count == 0)
                {
                    BehaviorNode.LogAI(unit, string.Format("DANGER TRIM: removed all {0} points, bracing", removedCount));
                }
                else
                {
                    BehaviorNode.LogAI(unit, string.Format("DANGER TRIM: removed {0} points, moving to {1}", removedCount, path[path.Count - 1]));
                }
            }

            return path;
        }

        private static bool CheckForOccupiedPoint(List<AbstractActor> actors, Vector3 worldPoint)
        {
            if (actors != null)
            {
                for (int i = 0; i < actors.Count; i++)
                {
                    var actorPosition = actors[i].CurrentPosition;
                    actorPosition.y = 0;

                    if ((actorPosition - worldPoint).sqrMagnitude < 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static void drawCircle(Vector3 center, float radius, Color color, float persistTime)
		{
			float thetaStep = 0.3f;
			for (float theta = 0; theta < Mathf.PI * 2; theta += thetaStep)
			{
				float nextTheta = theta + thetaStep;

				float xOff = Mathf.Cos(theta) * radius;
				float zOff = Mathf.Sin(theta) * radius;

				float xNextOff = Mathf.Cos(nextTheta) * radius;
				float zNextOff = Mathf.Sin(nextTheta) * radius;

				Vector3 offset1 = new Vector3(xOff, 0, zOff);
				Vector3 offset2 = new Vector3(xNextOff, 0, zNextOff);

				Debug.DrawLine(center + offset1, center + offset2, color, persistTime);
			}
		}

        /// <summary>
        /// Finds a destination point along a path from start to goal, where the distance from the destination should be
        /// approximately movementBudget. The path from start to goal will not have inclines nor declines exceeding maxSlope.
        /// <returns>The destination.</returns>
        /// <param name="start">Start.</param>
        /// <param name="goal">Goal.</param>
        /// <param name="movementBudget">Movement budget.</param>
        /// <param name="maxSlope">Max slope.</param>
        /// <param name="unit">unit that is moving</param>
        /// <param name="shouldSprint">whether to sprint or not</param>
        /// <param name="lanceUnits">observe lance spread from these units when choosing destinations</param>
        /// <param name="pathGrid">the pathing grid that indicates where a unit can get to on this turn</param>
        /// <param name="targetRadius">how close to get to the target to consider it a success</param>
        /// </summary>

        public static List<Vector3> GetDynamicPathToDestination(Vector3 goal, float movementBudget, AbstractActor unit, bool shouldSprint, List<AbstractActor> lanceUnits, PathNodeGrid pathGrid, float targetRadius)
		{
			List<PointWithCost> startPointList = new List<PointWithCost>();

            HexGrid hexGrid = unit.Combat.HexGrid;

			List<PathNode> pathNodes = pathGrid.GetSampledPathNodes();
			for (int pni = 0; pni < pathNodes.Count; ++pni)
			{
				PathNode pn = pathNodes[pni];
				PointWithCost pwd = new PointWithCost(hexGrid.GetClosestHexPoint3OnGrid(pn.Position), pn.DepthInPath, (goal - pn.Position).magnitude);
				pwd.pathNode = pn;
				startPointList.Add(pwd);
			}

			return GetDynamicPathToDestination(startPointList, goal, movementBudget, unit, shouldSprint, lanceUnits, pathGrid, targetRadius, actorAware: true);
		}

		static bool isPointInList(Vector3 point, List<Vector3> pointList, float tolerance)
		{
			for (int i = 0; i < pointList.Count; ++i)
			{
				if ((pointList[i]-point).magnitude < tolerance)
				{
					return true;
				}
			}
			return false;
		}

		public static List<Vector3> GetDynamicPathToDestination(List<PointWithCost> startPointList, Vector3 goal, float movementBudget, AbstractActor unit, bool shouldSprint, List<AbstractActor> lanceUnits, PathNodeGrid pathGrid, float targetRadius, bool actorAware = false)
		{
            HexGrid hexGrid = unit.Combat.HexGrid;

			if (shouldSprint && unit.CanSprint)
			{
				unit.Pathing.SetSprinting();
			}
			else
			{
				unit.Pathing.SetWalking();
			}

            HexPoint3 goalPoint = hexGrid.GetClosestHexPoint3OnGrid(goal);

			List<PointWithCost> pathLatticePoints = FindPath(startPointList, goalPoint, unit, shouldSprint ? MoveType.Sprinting : MoveType.Walking, targetRadius, actorAware);

			if ((pathLatticePoints == null) || (pathLatticePoints.Count == 0))
			{
                // can't find a path(!)
                return null;
            }

            // Dig, if you will:
            // A - Current unit position
            // B - a point on the edge between short range and long range pathfinding
            // C - the goal point
            // FindPath returned a path from B to C
            // pathLatticePoints[0] is B
            // walking backward through parents gets us the path from A to B
			List<PathNode> pathNodes = new List<PathNode>();
			PathNode walkNode = pathLatticePoints[0].pathNode;
			while (walkNode != null)
			{
				pathNodes.Insert(0, walkNode);
				walkNode = walkNode.Parent;
			}

			List<Vector3> longRangePathWorldPoints = pathLatticePoints.ConvertAll(x => hexGrid.HexPoint3ToCartesianWorld(x.point));
			List<Vector3> pathWorldPoints = pathNodes.ConvertAll(x => x.Position);
			pathWorldPoints.AddRange(longRangePathWorldPoints);

            // we have now spliced the A->B path together with the B->C path to have a A->C path

			if (longRangePathWorldPoints.Count == 1)
			{
                // if the B->C path is just one node, it's just B, so just return the A->C path,
                // which is just the A->B path, which we can do all in one go.

				return pathWorldPoints;
			}

			// Debug Draw Path
			float scale = 1.0f;
			for (int pathIndex = 0; pathIndex < pathWorldPoints.Count - 1; ++pathIndex)
			{
				int nextIndex = pathIndex + 1;
				Vector3 p0 = pathWorldPoints[pathIndex];
				Vector3 p1 = pathWorldPoints[nextIndex];

				scale = (p1 - p0).magnitude;

				for (int dx = -1; dx <= 1; ++dx)
				{
					for (int dz = -1; dz <= 1; ++dz)
					{
						Vector3 offset = new Vector3(dx * scale * 0.1f, 0, dz * scale * 0.1f);
						Debug.DrawLine(p0 + offset, p1 + offset, Color.red, 30.0f);
					}
				}
			}

			drawCircle(unit.CurrentPosition, scale * 0.4f, Color.cyan, 30.0f);
			drawCircle(goal, scale * 0.4f, Color.magenta, 30.0f);
			drawCircle(pathWorldPoints[0], scale * 0.3f, Color.red, 30.0f);
			drawCircle(pathWorldPoints[0], scale * 0.35f, Color.white, 30.0f);
			drawCircle(pathWorldPoints[0], scale * 0.4f, Color.blue, 30.0f);

			// TODO/dlecompte push this up to filter our selection of path nodes, above.
			float spread = unit.BehaviorTree.GetBehaviorVariableValue(
				unit.Combat.TurnDirector.IsInterleaved ?
				  BehaviorVariableName.Float_InterleavedLanceSpreadDistance :
				  BehaviorVariableName.Float_NonInterleavedLanceSpreadDistance).FloatVal;

			drawCircle(unit.CurrentPosition, spread, Color.green, 30.0f);

			Debug.Assert(pathWorldPoints.Count >= 2); // already tested this, above.
			float accumDistance = 0.0f;

			Vector3 clipPoint = goal;

			// Now we walk along the pathWorldPoints, snapping them to grid points.
			// We want to take the point furthest along the path that doesn't alias to an earlier point.
            // Also, nodes must be "safe" from artillery
			// Also, we want to make sure that it's the furthest point within our movement budget and within our lance spread.
			// MUST BE : within movement budget, not an alias to an earlier point
			// IF any points exist inside lance spread, pick last point inside lance spread, else last point.

			List<Vector3> dedupedSnappedPointsList = new List<Vector3>();
			List<Vector3> snappedPointsInOrder = new List<Vector3>();
			List<Vector3> nextPointsInOrder = new List<Vector3>();
			List<bool> pointsInSpreadRangeList = new List<bool>();
			List<bool> isNewGroundList = new List<bool>();

			float ROUNDING_RADIUS = 1.0f;

			bool wasEverInside = false;

			for (int pointIndex = 0; (pointIndex < pathWorldPoints.Count) && (accumDistance <= movementBudget); ++pointIndex)
			{
				Vector3 thisPoint = pathWorldPoints[pointIndex];
				Vector3 nextPoint = goal;

                if (pointIndex + 1 < pathWorldPoints.Count)
				{
					nextPoint = pathWorldPoints[pointIndex + 1];
				}

				Vector3 thisSnappedPoint = unit.Combat.HexGrid.GetClosestPointOnGrid(thisPoint);
                if (!IsLocationSafe(unit.Combat, thisSnappedPoint))
                {
                    continue;
                }
				snappedPointsInOrder.Add(thisSnappedPoint);
				nextPointsInOrder.Add(nextPoint);

				bool pointIsInsideSpread = AIUtil.IsPositionWithinLanceSpread(unit, lanceUnits, thisSnappedPoint);
				wasEverInside |= pointIsInsideSpread;
				pointsInSpreadRangeList.Add(pointIsInsideSpread);

				bool alreadyVisited = isPointInList(thisSnappedPoint, dedupedSnappedPointsList, ROUNDING_RADIUS);

				isNewGroundList.Add(!alreadyVisited);

				if (!alreadyVisited)
				{
					dedupedSnappedPointsList.Add(thisSnappedPoint);
				}

				if (pointIndex + 1 < pathWorldPoints.Count)
				{
					accumDistance += (nextPoint - thisPoint).magnitude;
				}
			}

			if (wasEverInside)
			{
				// find the last point of our list that is "new ground" and inside
				for (int i = snappedPointsInOrder.Count - 1; i >= 0; --i)
				{
					if (isNewGroundList[i] && pointsInSpreadRangeList[i])
					{
						clipPoint = snappedPointsInOrder[i];
						break;
					}
				}
			}

			if ((!wasEverInside) || ((clipPoint - unit.CurrentPosition).magnitude < 1.0f))
			{
				// find the last point of our list that is "new ground"
				for (int i = snappedPointsInOrder.Count - 1; i >= 0; --i)
				{
					if (isNewGroundList[i])
					{
						clipPoint = snappedPointsInOrder[i];
						break;
					}
				}
			}

			for (int i = snappedPointsInOrder.Count - 1; i >= 0; --i)
			{
				drawCircle(snappedPointsInOrder[i], scale * 0.2f, new Color(0.5f, 0.5f, 0.0f), 30.0f);
				if (pointsInSpreadRangeList[i])
					drawCircle(snappedPointsInOrder[i], scale * 0.25f, new Color(0.0f, 1.0f, 0.0f), 30.0f);
			}

			//Vector3 resultPos = clipPoint;
			drawCircle(clipPoint, scale * 0.4f, new Color(1.0f, 0.5f, 0.0f), 30.0f);
			return snappedPointsInOrder;
		}

        public static bool IsLocationSafe(CombatGameState combat, Vector3 point)
        {
            MapTerrainDataCell dataCell = combat.MapMetaData.GetCellAt(point);

            return (!(SplatMapInfo.IsDropshipLandingZone(dataCell.terrainMask) || SplatMapInfo.IsDropPodLandingZone(dataCell.terrainMask) || SplatMapInfo.IsDangerousLocation(dataCell.terrainMask)));
        }

        static public Vector3 HexPoint3ToWorldPoint(HexPoint3 hexPoint, HexGrid hexGrid)
        {
            return hexGrid.HexAxialToCartesian(new Vector2(hexPoint.q, hexPoint.r));
        }


        /// <summary>
        /// Finds a destination this turn along a long range path towards the goal.
        /// </summary>
        /// <param name="goal"></param>
        /// <param name="movementThisTurn"></param>
        /// <param name="unit"></param>
        /// <param name="shouldSprint"></param>
        /// <param name="lookAtPoint"></param>
        /// <param name="pathIsSafe">(out) flag whether the path ends in a place out of (e.g. artillery) danger</param>
        /// <returns></returns>

        static public List<Vector3> GetPathToDestination(Vector3 goal, float movementThisTurn, AbstractActor unit, bool shouldSprint, float destinationRadius)
        {
            List<AbstractActor> lanceUnits = AIUtil.GetLanceUnits(unit.Combat, unit.LanceId);

            // can't get all the way to the destination.
            if ((unit.Combat.EncounterLayerData.inclineMeshData != null) &&
                (unit.BehaviorTree.GetBehaviorVariableValue(BehaviorVariableName.Bool_UseDynamicLongRangePathfinding).BoolVal == false))
            {
                float maxSteepnessRatio = Mathf.Tan(Mathf.Deg2Rad * AIUtil.GetMaxSteepnessForAllLance(unit));
                // TODO - make this work, if we want to maintain it
                Vector3 lookAtPoint;
                Vector3 dest = unit.Combat.EncounterLayerData.inclineMeshData.GetDestination(
                    goal,
                    movementThisTurn,
                    maxSteepnessRatio,
                    unit,
                    shouldSprint,
                    lanceUnits,
                    unit.Pathing.CurrentGrid,
                    out lookAtPoint);

                List<Vector3> path = new List<Vector3>();
                path.Add(dest);
                return path;
            }
            else
            {
                return GetDynamicPathToDestination(
                    goal,
                    movementThisTurn,
                    unit,
                    shouldSprint,
                    lanceUnits,
                    unit.Pathing.CurrentGrid,
                    destinationRadius);
            }
        }
    }
}

