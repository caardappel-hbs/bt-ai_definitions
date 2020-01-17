using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTech
{
	[ExecuteInEditMode]
	public class InclineMeshBuilder : MonoBehaviour
	{
		public int downsampleFactor = 8;
		public float overshootMultiplier = 1.5f;

		public InclineMeshBuilder()
		{
		}

		public InclineMeshData BuildMeshData(MapMetaData mapMetaData)
		{
			DateTime startTime = System.DateTime.Now;
			Debug.Log ("started making incline mesh at " + startTime);

			int numInclineNodesX = Mathf.CeilToInt(mapMetaData.mapTerrainDataCells.GetLength(0) / (float)downsampleFactor);
			int numInclineNodesZ = Mathf.CeilToInt(mapMetaData.mapTerrainDataCells.GetLength(1) / (float)downsampleFactor);

			InclineMeshData meshData = new InclineMeshData(numInclineNodesX, numInclineNodesZ);
			meshData.downsampleFactor = downsampleFactor;
			meshData.mapMetaData = mapMetaData;

			for (int x = 0; x < numInclineNodesX; ++x)
			{
				for (int z = 0; z < numInclineNodesZ; ++z)
				{
					InclineIndexPoint inclinePoint = new InclineIndexPoint(x, z);
					processNode(meshData, mapMetaData, inclinePoint);
				}
			}
			DateTime endTime = System.DateTime.Now;
			TimeSpan elapsed = endTime - startTime;

			Debug.Log ("finished making incline mesh at " + endTime);
			Debug.Log ("Elapsed Time: " + elapsed);

			return meshData;
		}

		float getDistanceBetweenTwoInclinePoints(InclineMeshData meshData, MapMetaData mapMetaData)
		{
			InclineIndexPoint inclinePoint1 = new InclineIndexPoint(1, 1);
			InclineIndexPoint inclinePoint2 = new InclineIndexPoint(1, 2);

			Point mapPoint1 = meshData.InclineIndicesToMapIndices(inclinePoint1);
			Point mapPoint2 = meshData.InclineIndicesToMapIndices(inclinePoint2);

			Vector3 worldVec1 = mapMetaData.getWorldPos(mapPoint1);
			Vector3 worldVec2 = mapMetaData.getWorldPos(mapPoint2);

			return (worldVec2 - worldVec1).magnitude;
		}

		void processNode(InclineMeshData meshData, MapMetaData mapMetaData, InclineIndexPoint inclinePoint)
		{
			Point startPointMapIndices = meshData.InclineIndicesToMapIndices(inclinePoint);

			if (!mapMetaData.IsWithinBounds(startPointMapIndices))
			{
				return;
			}

			Dictionary<Point, List<InclineLinkData>> bestPaths = new Dictionary<Point, List<InclineLinkData>>();
			List<InclineLinkData> starterList = new List<InclineLinkData>();
			starterList.Add(new InclineLinkData());

			bestPaths[startPointMapIndices] = starterList;

			List<Point> openPoints = new List<Point>();
			openPoints.Add(startPointMapIndices);

			float maximumPathDistance = overshootMultiplier * getDistanceBetweenTwoInclinePoints(meshData, mapMetaData);

			while (openPoints.Count > 0)
			{
				Point p = openPoints[0];
				openPoints.RemoveAt(0);

				//Debug.LogFormat("dequeueing {0} {1}", p.X, p.Z);

				for (int d = 0; d < 4; ++d)
				{
					int dx = 0;
					int dz = 0;

					switch (d)
					{
					case 0: dx =  1; dz =  0; break;
					case 1: dx =  0; dz =  1; break;
					case 2: dx = -1; dz =  0; break;
					case 3: dx =  0; dz = -1; break;
					default: Debug.LogError("invalid direction: " + d); continue;
					}

					Point newPoint = new Point(p.X + dx, p.Z + dz);

					if ((!mapMetaData.IsWithinBounds(newPoint))||
						(!mapMetaData.IsWithinBounds(p)))
					{
						continue;
					}

					float incline, decline, distance;
					getIncrementalInclines(mapMetaData, p, newPoint, out incline, out decline, out distance);

					bool pointIsDirty = false;

					for (int pathIndex = 0 ; pathIndex < bestPaths[p].Count; pathIndex++)
					{
						InclineLinkData pathSoFar = bestPaths[p][pathIndex];

						float oldFloatIncline = pathSoFar.inclineAsFloat();
						float oldFloatDecline = pathSoFar.declineAsFloat();
						float oldFloatDistance = pathSoFar.distanceAsFloat();

						float newFloatIncline = Mathf.Max(incline, oldFloatIncline);
						float newFloatDecline = Mathf.Max(decline, oldFloatDecline);
						float newFloatDistance = oldFloatDistance + distance;

						// if the distance is too far, also stop recursing.
						if (newFloatDistance > maximumPathDistance)
						{
							continue;
						}

						InclineLinkData newPathLinkData = new InclineLinkData(newFloatIncline, newFloatDecline, newFloatDistance);

						bool foundAnyBetter = false;
						int iAmBetterThanThisIndex = -1;

						if (bestPaths.ContainsKey(newPoint))
						{
							for (int existingPathIndex = 0; existingPathIndex < bestPaths[newPoint].Count; ++existingPathIndex)
							{
								InclineLinkData existingPath = bestPaths[newPoint][existingPathIndex];
								// If an existing path is as good or better than us, then stop recursing.

								if (existingPath.Equals(newPathLinkData) || 
									existingPath.Dominates(newPathLinkData))
								{
									foundAnyBetter = true;
									break;
								}

								if (newPathLinkData.Dominates(existingPath))
								{
									iAmBetterThanThisIndex = existingPathIndex;
									break;
								}
							}
						}
						else
						{
							bestPaths[newPoint] = new List<InclineLinkData>();
						}

						if (iAmBetterThanThisIndex >= 0)
						{
							// found a better path than an existing one, remove the old one and recompute.
							bestPaths[newPoint][iAmBetterThanThisIndex] = newPathLinkData;
							pointIsDirty = true;
						}
						else if (!foundAnyBetter)
						{
							// Otherwise, add this path to the list of paths, and recurse.

							bestPaths[newPoint].Add(newPathLinkData);
							pointIsDirty = true;
						}
					}
					if (pointIsDirty)
					{
						openPoints.Add(newPoint);
					}
				}
			}

			meshData.nodes[inclinePoint.X, inclinePoint.Z] = new InclineMeshNode();

			// now, grab the links from our neighbors
			for (int d = 0; d < 4; ++d)
			{
				int dx = 0;
				int dz = 0;

				switch (d)
				{
				case 0: dx =  1; dz =  0; break;
				case 1: dx =  0; dz =  1; break;
				case 2: dx = -1; dz =  0; break;
				case 3: dx =  0; dz = -1; break;
				default: Debug.LogError("invalid direction: " + d); continue;
				}

				InclineIndexPoint neighborInclinePoint = new InclineIndexPoint(inclinePoint.X + dx, inclinePoint.Z + dz);
				Point neighborMapIndices = meshData.InclineIndicesToMapIndices(neighborInclinePoint);

				if (!mapMetaData.IsWithinBounds(neighborMapIndices))
				{
					continue;
				}

				if (bestPaths.ContainsKey(neighborMapIndices))
				{
					/*
					foreach (InclineLinkData ild in bestPaths[neighborWorldPoint])
					{
						//Debug.LogFormat("from {0} {1} in dir {2}: {3}/{4}/{5}", inclinePoint.X, inclinePoint.Z, d, ild.incline, ild.decline, ild.distance);
					}
					*/
					meshData.nodes[inclinePoint.X, inclinePoint.Z].NeighborLinks[d] = bestPaths[neighborMapIndices];

					// draw debug
					//Debug.DrawLine(meshData.InclineIndicesToWorldPoint(inclinePoint), meshData.mapMetaData.getWorldPos(neighborMapIndices), Color.white, 25.0f);
				}
			}
		}


		/// <summary>
		/// Gets incremental incline data for a point and a neighboring point.
		/// </summary>
		/// <param name="mapMetaData">Map meta data.</param>
		/// <param name="startPoint">Start point, using mapmetadata indices.</param>
		/// <param name="endPoint">End point, using mapmetadata indices.</param>
		/// <param name="incline">Incline (out).</param>
		/// <param name="decline">Decline (out).</param>
		/// <param name="distance">Distance (out).</param>
		void getIncrementalInclines(MapMetaData mapMetaData, Point startPoint, Point endPoint, out float incline, out float decline, out float distance)
		{
			Vector3 startVector = mapMetaData.getWorldPos(startPoint);
			Vector3 endVector = mapMetaData.getWorldPos(endPoint);

			MapTerrainDataCell startCell = mapMetaData.GetCellAt(startPoint);
			MapTerrainDataCell endCell = mapMetaData.GetCellAt(endPoint);

			float horzDistance = Mathf.Abs(startVector.x - endVector.x) + Mathf.Abs(startVector.z - endVector.z);
			distance = horzDistance;

			float vertDistance = endCell.cachedHeight - startCell.cachedHeight;

			if (vertDistance > 0)
			{
				decline = 0.0f;
				incline = vertDistance / horzDistance;
			}
			else
			{
				incline = 0.0f;
				decline = -(vertDistance / horzDistance);
			}
		}


		/// <summary>
		/// Returns true if the existing path is as good or better than the new path - that is, if the existing path's 
		/// upwards steepness, downward steepness, and distance are each equal or less than the new path's steepnesses 
		/// and distance.
		/// </summary>
		/// <param name="existingPath">existing path</param>
		/// <param name="newPath">new path</param>
		bool isExistingPathAsGoodOrBetterThan(InclineLinkData existingPath, InclineLinkData newPath)
		{
			if ((existingPath.decline > newPath.decline) ||
				(existingPath.incline > newPath.incline) ||
				(existingPath.distance > newPath.distance))
			{
				return false;
			}

			return true;
		}
	}
}

