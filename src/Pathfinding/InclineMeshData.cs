using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BattleTech
{
	public struct InclineLinkData : IEquatable<InclineLinkData>
	{
		const float INCLINEDOWNSAMPLE = 10.0f;

		public byte incline;
		public byte decline;
		public byte distance;

		public InclineLinkData(byte incline, byte decline, byte distance)
		{
			this.incline = incline;
			this.decline = decline;
			this.distance = distance;
		}

		public InclineLinkData(float inclineRatio, float declineRatio, float distance)
		{
			if (inclineRatio > 1.0f)
			{
				inclineRatio = 1.0f;
			}
			this.incline = (byte)(Mathf.RoundToInt((inclineRatio * 255) / INCLINEDOWNSAMPLE));

			if (declineRatio > 1.0f)
			{
				declineRatio = 1.0f;
			}
			this.decline = (byte)(Mathf.RoundToInt((declineRatio * 255) / INCLINEDOWNSAMPLE));

			if (distance > 255)
			{
				distance = 255.0f;
			}
			this.distance = (byte)(Mathf.CeilToInt(distance));
		}

		public void Read(BinaryReader br)
		{
			this.incline = br.ReadByte ();
			this.decline = br.ReadByte ();
			this.distance = br.ReadByte ();
		}

		public void Write(BinaryWriter bw)
		{
			bw.Write (incline);
			bw.Write (decline);
			bw.Write (distance);
		}

		/// <summary>
		/// Returns the slope up ("incline") as a floating point number between 0 and 1.
		/// </summary>
		public float inclineAsFloat()
		{
			return incline * INCLINEDOWNSAMPLE / 255.0f;
		}

		/// <summary>
		/// Returns the slope down ("decline") as a floating point number between 0 and (positive!) 1.
		/// </summary>
		public float declineAsFloat()
		{
			return decline * INCLINEDOWNSAMPLE/ 255.0f;
		}

		public float distanceAsFloat()
		{
			return (float)distance;
		}

		/// <summary>
		/// Determines if this path dominates another path. In this sense, we mean that A dominates B if A is always 
		/// better than B. If any of B's components are less than A, then A doesn't dominate B. If A==B, A doesn't 
		/// dominate B. Note that if A dominates B, we know that B can't dominate A. So, domination, I guess, creates a
		/// partial ordering.
		/// </summary>
		/// <param name="other">Other.</param>
		public bool Dominates(InclineLinkData other)
		{
			if ((other.incline < this.incline) ||
				(other.decline < this.decline) ||
				(other.distance < this.distance))
			{
				return false;
			}

			if ((other.incline == this.incline) &&
				(other.decline == this.decline) &&
				(other.distance == this.distance))
			{
				return false;
			}

			return true;
		}

		public bool Equals(InclineLinkData other)
		{
			return ((this.incline == other.incline) &&
				(this.decline == other.decline) &&
				(this.distance == other.distance));
		}
	};

	public class InclineMeshNode
	{
		public enum Direction
		{
			East,
			North,
			West,
			South
		};

		public List<InclineLinkData>[] NeighborLinks;

		public InclineMeshNode()
		{
			NeighborLinks = new List<InclineLinkData>[4];
		}

		public void Write(BinaryWriter bw)
		{
			for (int direction = 0; direction < 4; ++direction)
			{
				if (NeighborLinks [direction] == null) 
				{
					bw.Write ((Int32) 0);
				} 
				else 
				{
					bw.Write ((Int32)NeighborLinks [direction].Count);
					for (int linkIndex = 0; linkIndex < NeighborLinks[direction].Count; ++linkIndex)
					{
						NeighborLinks[direction][linkIndex].Write(bw);
					}
				}
			}
		}

		public void Read(BinaryReader br)
		{
			NeighborLinks = new List<InclineLinkData>[4];

			for (int direction = 0; direction < 4; ++direction)
			{
				int linkCount = br.ReadInt32 ();

				if (linkCount == 0)
				{
					NeighborLinks [direction] = null;
				} 
				else 
				{
					NeighborLinks [direction] = new List<InclineLinkData> (linkCount);

					for (int linkIndex = 0; linkIndex < linkCount; ++linkIndex) 
					{
						InclineLinkData linkData = new InclineLinkData();
						linkData.Read(br);
						NeighborLinks[direction].Add(linkData);
					}
				}
			}
		}
	}

	public class InclineIndexPoint : System.IEquatable<InclineIndexPoint>
	{
		public int X;
		public int Z;

		public InclineIndexPoint(int x, int z)
		{
			X = x;
			Z = z;
		}

		public override bool Equals(object obj)
		{
			InclineIndexPoint objPoint = obj as InclineIndexPoint;
			return X == (objPoint).X && Z == (objPoint).Z;
		}

		public bool Equals(InclineIndexPoint other)
		{
			return X == other.X && Z == other.Z;
		}

		public override int GetHashCode()
		{
			return X.GetHashCode() * 19 + Z.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("[I{0}, {1}]", X, Z);
		}
	}

	public class InclineMeshData
	{
		public int downsampleFactor;
		public int sizeX;
		public int sizeZ;

		public InclineMeshNode[,] nodes;
		public MapMetaData mapMetaData;

		const bool TAKE_FIRST_PATH = false;

		public InclineMeshData(int sizeX, int sizeZ)
		{
			this.sizeX = sizeX;
			this.sizeZ = sizeZ;
			if ((sizeX > 0) && (sizeZ > 0)) 
			{
				this.nodes = new InclineMeshNode[sizeX, sizeZ];
			} 
			else 
			{
				this.nodes = null;
			}
		}

		public InclineMeshData() : this(-1, -1)
		{
		}

		public bool LoadFromPath(string path)
		{
			FileStream fileStream = null;

			try
			{
				fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

				BinaryReader br = new BinaryReader(fileStream);
				// read here

				int sizeX = br.ReadInt32 ();
				if (sizeX <= 0)
				{
					Debug.LogErrorFormat("Error loading incline mesh {0}, sizeX == {1}", path, sizeX);
					return false;
				}
				int sizeZ = br.ReadInt32 ();
				if (sizeZ <= 0)
				{
					Debug.LogErrorFormat("Error loading incline mesh {0}, sizeX == {1}", path, sizeZ);
					return false;
				}
				this.downsampleFactor = br.ReadInt32();
				if (downsampleFactor <= 0)
				{
					Debug.LogErrorFormat("Error loading incline mesh {0}, downsampleFactor == {1}", path, downsampleFactor);
					return false;
				}
				this.sizeX = sizeX;
				this.sizeZ = sizeZ;
				this.nodes = new InclineMeshNode[sizeX, sizeZ];

				for (int x = 0; x < sizeX; ++x)
					for (int z = 0; z < sizeZ; ++z) 
					{
						bool hasNode = br.ReadBoolean();
						if (!hasNode)
						{
							nodes[x,z] = null;
						}
						else
						{
							InclineMeshNode node = new InclineMeshNode();
							nodes[x,z] = node;
							node.Read(br);
						}
					}

			}
			catch(System.IO.IsolatedStorage.IsolatedStorageException ise)
			{
				Debug.LogWarningFormat("IsolatedStorageException {0}, could not find {1}. This will cripple the AI.", ise.Message, path);
				return false;
			}
			catch (FileNotFoundException e)
			{
				Debug.LogWarningFormat("Could not find {0}. This will cripple the AI.", e.FileName);
				return false;
			}
			finally
			{
				if (fileStream != null)
				{
					fileStream.Dispose();
				}
			}
			return true;
		}

		public void SaveToPath(string path)
		{
			Debug.LogFormat("writing data to {0}", path);
			using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
			{
				BinaryWriter bw = new BinaryWriter(fileStream);			
				bw.Write(sizeX);
				bw.Write(sizeZ);
				bw.Write(downsampleFactor);

				for (int x = 0; x < sizeX; ++x)
					for (int z = 0; z < sizeZ; ++z) 
					{
						if (nodes[x,z] == null)
						{
							bw.Write(false);
						}
						else
						{
							bw.Write(true);
							// now write the node
							nodes[x,z].Write(bw);
						}
					}
			}
		}

		public Point InclineIndicesToMapIndices(InclineIndexPoint inclineIndices)
		{
			return new Point(inclineIndices.X * downsampleFactor, inclineIndices.Z * downsampleFactor);
		}

		public InclineIndexPoint MapIndicesToInclineIndices(Point mapIndices)
		{
			int incX = Mathf.RoundToInt(mapIndices.X / (float)downsampleFactor);
			int incZ = Mathf.RoundToInt(mapIndices.Z / (float)downsampleFactor);
			return new InclineIndexPoint(incX, incZ);
		}

		public Vector3 InclineIndicesToWorldPoint(InclineIndexPoint inclineIndices)
		{
			Point mapIndices = InclineIndicesToMapIndices(inclineIndices);

			return mapMetaData.getWorldPos(mapIndices);
		}

		public InclineIndexPoint WorldPointToInclineIndices(Vector3 worldPoint)
		{
			Point mapIndices = mapMetaData.GetIndex(worldPoint);
			return MapIndicesToInclineIndices(mapIndices);
		}

		public class PointWithDistance : IComparable<PointWithDistance>, IComparable
		{
			public InclineIndexPoint point;
			public float distance;
			public float estimatedTotalDistance;
			public PathNode pathNode;

			public PointWithDistance(InclineIndexPoint point, float distance, float estimatedTotalDistance)
			{
				this.point = point;
				this.distance = distance;
				this.estimatedTotalDistance = estimatedTotalDistance;
			}

			public int CompareTo(PointWithDistance other)
			{
				return this.estimatedTotalDistance.CompareTo(other.estimatedTotalDistance);
			}

			public int CompareTo(object other)
			{
				PointWithDistance otherPt = other as PointWithDistance;
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
		/// Finds a path from any of start to goal, such that the path has no links that are steeper up than maxIncline
		/// nor steeper down than maxDecline.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="startPoint">PointWithDistance of point to start from</param>
		/// <param name="goalPoint">map indices of point to go to</param>
		List<PointWithDistance> FindPath(PointWithDistance startPoint, InclineIndexPoint goalPoint, float maxIncline, float maxDecline)
		{
			List<PointWithDistance> startList = new List<PointWithDistance>();
			startList.Add(startPoint);
			return FindPath(startList, goalPoint, maxIncline, maxDecline);
		}

		/// <summary>
		/// Finds a path from any of the points in startPointList to goal, such that the path has no links that are steeper up than maxIncline
		/// nor steeper down than maxDecline.
		/// </summary>
		/// <returns>The path.</returns>
		/// <param name="startPointList">PointWithDistance structs for starting area</param>
		/// <param name="goalPoint">map indices of point to go to</param>
		List<PointWithDistance> FindPath(List<PointWithDistance> startPointList, InclineIndexPoint goalPoint, float maxIncline, float maxDecline)
		{
			List<PointWithDistance> path = new List<PointWithDistance>();

			HeapQueue<PointWithDistance> openHeap = new HeapQueue<PointWithDistance>();

			Dictionary<InclineIndexPoint, float> bestDistanceDict = new Dictionary<InclineIndexPoint, float>();

			Dictionary<InclineIndexPoint, PointWithDistance> bestPrevPoint = new Dictionary<InclineIndexPoint, PointWithDistance>();

			Point goalMapPoint = InclineIndicesToMapIndices(goalPoint);

			Vector3 worldGoalPoint = mapMetaData.getWorldPos(goalMapPoint);

			for (int startIndex = 0; startIndex < startPointList.Count; ++startIndex)
			{
				PointWithDistance pwd = startPointList[startIndex];
				openHeap.Push(pwd);
				bestDistanceDict[pwd.point] = pwd.distance;
                bestPrevPoint[pwd.point] = null;
			}

			float bestPathLength = -1;
			PointWithDistance bestGoalPoint = new PointWithDistance(new InclineIndexPoint(-1024, -1024),
				float.MaxValue,
				float.MaxValue);

			while (!openHeap.IsEmpty())
			{
				PointWithDistance ptWithDist = openHeap.PopMinimum();

				if ((bestPathLength > 0) && ((ptWithDist.estimatedTotalDistance > bestPathLength) || TAKE_FIRST_PATH))
				{
					break;
				}

				int[] xOffsets = {1, 0, -1, 0};
				int[] zOffsets = { 0, 1, 0, -1 };

				InclineMeshNode node = nodes[ptWithDist.point.X, ptWithDist.point.Z];

				Vector3 worldNodePoint = InclineIndicesToWorldPoint(ptWithDist.point);

				for (int direction = 0; direction < 4; ++direction)
				{
					int dx = xOffsets[direction];
					int dz = zOffsets[direction];

					int nx = ptWithDist.point.X + dx;
					int nz = ptWithDist.point.Z + dz;

					InclineIndexPoint neighborPoint = new InclineIndexPoint(nx, nz);
					Point mapNeighborPoint = InclineIndicesToMapIndices(neighborPoint);

					if ((!mapMetaData.IsWithinBounds(mapNeighborPoint)) || (node.NeighborLinks[direction] == null))
					{
						continue;
					}

					Vector3 worldNeighborPoint = InclineIndicesToWorldPoint(neighborPoint);

					for (int linkIndex = 0; linkIndex < node.NeighborLinks[direction].Count; ++linkIndex)
					{
						Debug.DrawLine(worldNodePoint, worldNeighborPoint, Color.yellow, 15.0f);

						InclineLinkData link = node.NeighborLinks[direction][linkIndex];

						if ((link.declineAsFloat() > maxDecline) || (link.inclineAsFloat() > maxIncline))
						{
							continue;
						}

						float linkDistance = (worldNeighborPoint - worldNodePoint).magnitude;

						float totalDistance = ptWithDist.distance + linkDistance;

						if ((bestPathLength >= 0) &&
						    (totalDistance >= bestPathLength))
						{
							continue;
						}

						if ((!bestDistanceDict.ContainsKey(neighborPoint)) ||
						    (totalDistance < bestDistanceDict[neighborPoint]))
						{
							bestDistanceDict[neighborPoint] = totalDistance;
							bestPrevPoint[neighborPoint] = ptWithDist;

							float distanceToGoal = (worldNeighborPoint - worldGoalPoint).magnitude;

							if (neighborPoint.Equals(goalPoint))
							{
								if ((bestPathLength < 0) ||
								    (totalDistance < bestPathLength))
								{
									bestPathLength = totalDistance;
									bestGoalPoint = new PointWithDistance(neighborPoint, totalDistance, 0.0f);
								}
							}
							else
							{
								openHeap.Push(new PointWithDistance(neighborPoint, totalDistance, totalDistance + distanceToGoal));
							}
						}
						break;
					}
				}
			}

			if (bestPathLength >= 0)
			{
				PointWithDistance p = bestGoalPoint;
				path.Add(p);
				while (bestPrevPoint.ContainsKey(p.point))
				{
					PointWithDistance prevPoint = bestPrevPoint[p.point];
                    if ((prevPoint == null) || (path.Contains(prevPoint)))
                    {
                        break;
                    }
					path.Insert(0, prevPoint);
					p = prevPoint;
				}
			}

			return path;
		}

		void drawCircle(Vector3 center, float radius, Color color, float persistTime)
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
		/// </summary>
		/// <returns>The destination.</returns>
		/// <param name="start">Start.</param>
		/// <param name="goal">Goal.</param>
		/// <param name="movementBudget">Movement budget.</param>
		/// <param name="maxSlope">Max slope.</param>
		/// <param name="unit">unit that is moving</param>
		/// <param name="shouldSprint">whether to sprint or not</param>
		/// <param name="lanceUnits">observe lance spread from these units when choosing destinations</param>
		/// <param name="pathGrid">the pathing grid that indicates where a unit can get to on this turn</param>
		/// <param name="lookAtPoint">point to look at</param>
		public Vector3 GetDestination(Vector3 start, Vector3 goal, float movementBudget, float maxSlope, AbstractActor unit, bool shouldSprint, List<AbstractActor> lanceUnits, PathNodeGrid pathGrid, out Vector3 lookAtPoint)
		{
			List<PointWithDistance> startPointList = new List<PointWithDistance>();
			startPointList.Add(new PointWithDistance(WorldPointToInclineIndices(start), 0, (goal - start).magnitude));
			return GetDestination(startPointList, goal, movementBudget, maxSlope, unit, shouldSprint, lanceUnits, pathGrid, out lookAtPoint);
		}

		public Vector3 GetDestination(Vector3 goal, float movementBudget, float maxSlope, AbstractActor unit, bool shouldSprint, List<AbstractActor> lanceUnits, PathNodeGrid pathGrid, out Vector3 lookAtPoint)
		{
			List<PointWithDistance> startPointList = new List<PointWithDistance>();

			List<PathNode> pathNodes = pathGrid.GetSampledPathNodes();
			for (int pni = 0; pni < pathNodes.Count; ++pni)
			{
				PathNode pn = pathNodes[pni];
				PointWithDistance pwd = new PointWithDistance(WorldPointToInclineIndices(pn.Position), pn.DepthInPath * 24, (goal - pn.Position).magnitude);
				pwd.pathNode = pn;
				startPointList.Add(pwd);
			}

			return GetDestination(startPointList, goal, movementBudget, maxSlope, unit, shouldSprint, lanceUnits, pathGrid, out lookAtPoint);
		}

		bool isPointInList(Vector3 point, List<Vector3> pointList, float tolerance)
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

		public Vector3 GetDestination(List<PointWithDistance> startPointList, Vector3 goal, float movementBudget, float maxSlope, AbstractActor unit, bool shouldSprint, List<AbstractActor> lanceUnits, PathNodeGrid pathGrid, out Vector3 lookAtPoint)
		{
			if (shouldSprint && unit.CanSprint)
			{
				unit.Pathing.SetSprinting();
			}
			else
			{
				unit.Pathing.SetWalking();
			}

			InclineIndexPoint goalPoint = WorldPointToInclineIndices(goal);

			List<PointWithDistance> pathLatticePoints = FindPath(startPointList, goalPoint, maxSlope, maxSlope);

			if ((pathLatticePoints == null) || (pathLatticePoints.Count == 0))
			{
				// can't find a path(!)

				// set the lookAtPoint (out) variable to a point further out in the direction of start->goal
				lookAtPoint = goal * 2.0f - unit.CurrentPosition;
				return goal;
			}


			List<PathNode> pathNodes = new List<PathNode>();
			PathNode walkNode = pathLatticePoints[0].pathNode;
			while (walkNode != null)
			{
				pathNodes.Insert(0, walkNode);
				walkNode = walkNode.Parent;
			}

			List<Vector3> longRangePathWorldPoints = pathLatticePoints.ConvertAll(x => InclineIndicesToWorldPoint(x.point));
			List<Vector3> pathWorldPoints = pathNodes.ConvertAll(x => x.Position);
			pathWorldPoints.AddRange(longRangePathWorldPoints);

			Vector3 destination;
			if (longRangePathWorldPoints.Count == 1)
			{
				destination = longRangePathWorldPoints[0];
				if ((goal- destination).magnitude < 1.0f)
				{
					lookAtPoint = destination * 2.0f - unit.CurrentPosition;
				}
				else
				{
					lookAtPoint = goal;
				}

				return destination;
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

			// if we can get to the goal, look at a point further away in the direction of start -> goal
			lookAtPoint = 2 * goal - unit.CurrentPosition;
			Vector3 clipPoint = goal;

			// Now we walk along the pathWorldPoints, snapping them to grid points.
			// We want to take the point furthest along the path that doesn't alias to an earlier point.
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
						lookAtPoint = nextPointsInOrder[i];
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
						lookAtPoint = nextPointsInOrder[i];
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

			//float lookAngle = PathingUtil.GetAngle(lookAtPoint - clipPoint);
			//Vector3 resultPos = clipPoint;
			drawCircle(clipPoint, scale * 0.4f, new Color(1.0f, 0.5f, 0.0f), 30.0f);
			return clipPoint;
		}
	}
}

