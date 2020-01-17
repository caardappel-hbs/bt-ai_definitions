using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleTech
{
	public class InclineMeshHelper
	{
		InclineMeshData meshData;

		public InclineMeshHelper(InclineMeshData meshData)
		{
			this.meshData = meshData;
		}

		class AStarPathEndpoint : IComparable
		{
			public Point point;
			public float elapsedDistance;
			public float estimatedDistanceRemaining;

			public AStarPathEndpoint(Point point, float elapsedDistance, float estimatedDistanceRemaining)
			{
				this.point = point;
				this.elapsedDistance = elapsedDistance;
				this.estimatedDistanceRemaining = estimatedDistanceRemaining;
			}

			public int CompareTo(object other)
			{
				AStarPathEndpoint otherEndpoint = other as AStarPathEndpoint;

				float myTotal = elapsedDistance + estimatedDistanceRemaining;
				float otherTotal = otherEndpoint.elapsedDistance + otherEndpoint.estimatedDistanceRemaining;

				return myTotal.CompareTo(otherTotal);
			}
		}

		void getDirectionDeltas(InclineMeshNode.Direction dir, out int dx, out int dz)
		{
			switch (dir)
			{
			case InclineMeshNode.Direction.East:    dx =  1;  dz =  0; return;
			case InclineMeshNode.Direction.North:   dx =  0;  dz =  1; return;
			case InclineMeshNode.Direction.West:    dx = -1;  dz =  0; return;
			case InclineMeshNode.Direction.South:   dx =  0;  dz = -1; return;
			default:
				Debug.LogError("bad direction: " + dir);

				dx = int.MinValue;
				dz = int.MinValue;
				break;
			}
		}

		/// <summary>
		/// Finds the indices for the indexed mesh node closest to the given point.
		/// </summary>
		/// <returns><c>true</c>, if closest sampled indices was found, <c>false</c> otherwise.</returns>
		/// <param name="point">Point.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="z">The z coordinate.</param>
		public bool FindClosestSampledIndices(Vector3 point, out int x, out int z)
		{
			// TODO this math isn't right
			x = Mathf.RoundToInt(point.x / meshData.downsampleFactor);
			z = Mathf.RoundToInt(point.z / meshData.downsampleFactor);

			return true;
		}

		public Vector3 IndicesToVector(int x, int z)
		{
			// TODO this math isn't right
			return new Vector3(x * meshData.downsampleFactor, 0.0f, z * meshData.downsampleFactor);
		}
	}
}

