using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SkinnedDecals.Internal
{

	public struct SkinnedData
	{
		public ComputeBuffer buffer1, buffer2;
	}

	public static class ProjectionUtility
	{
		private static Vector3 UvOffset(Vector3 u)
		{
			return new Vector3(u.x + 0.5f, u.y + 0.5f, 1);
		}

		public static void BakeUvData(int[] tris, Vector3[] verts, Vector4[] planes,
			Transform projector, Vector3[] uvData, List<int> intersectTris = null, bool dontClear = false)
		{
			if (!dontClear)
			{
				for (int i = 0; i < uvData.Length; i++)
					// 10 is an arbitrary value such that x>1 or x<-1
					// -1 is an arbitrary negative value
					uvData[i] = new Vector3(10, 10, -1);
			}

			for (int i = 0; i < tris.Length; i += 3)
			{
				// Vertex IDs for this triangle
				var t1 = tris[i];
				var t2 = tris[i + 1];
				var t3 = tris[i + 2];

				// World vertices
				var w1 = verts[t1];
				var w2 = verts[t2];
				var w3 = verts[t3];

				// Check that this triangle intersects the projection box
				//     by checking that for each of the box's 8 planes, at least
				//     one vertex is on the side facing the centre of the box
				var check = true;
				// Keep this as a loop for speed
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var plane in planes)
				{
					if (plane.x * w1.x + plane.y * w1.y + plane.z * w1.z + plane.w > 0 ||
						plane.x * w2.x + plane.y * w2.y + plane.z * w2.z + plane.w > 0 ||
						plane.x * w3.x + plane.y * w3.y + plane.z * w3.z + plane.w > 0)
						continue;
					check = false;
					break;
				}
				if (!check)
					continue;

				// Compute the UV data at each vertex
				uvData[t1] = UvOffset(projector.InverseTransformPoint(w1));
				uvData[t2] = UvOffset(projector.InverseTransformPoint(w2));
				uvData[t3] = UvOffset(projector.InverseTransformPoint(w3));

				if (intersectTris != null)
				{
					intersectTris.Add(t1);
					intersectTris.Add(t2);
					intersectTris.Add(t3);
				}
			}
		}

		public static Material CreateSkinnedData(Vector3[] uvData,
			Material baseMaterial, out SkinnedData skinnedData)
		{
			skinnedData = default(SkinnedData);

			// Cut off space at beginning and end
			var minUv = 0;
			try
			{
				while (uvData[minUv].z <= 0)
					minUv++;
			}
			catch (IndexOutOfRangeException)
			{
				// No triangles intersect projection box
				return null;
			}
			var maxUv = uvData.Length;
			while (uvData[maxUv - 1].z <= 0)
				maxUv--;

			// Find longest string of empty space in the remainder
			int maxSpaceStart = -1, maxSpaceLength = -1;
			int spaceStart = -1, spaceLength = -1;
			for (int i = minUv; i < maxUv; i++)
			{
				if (uvData[i].z < 0)
				{
					spaceStart = spaceStart < 0 ? i : spaceStart;
					spaceLength++;
				}
				else
				{
					if (spaceLength > maxSpaceLength)
					{
						maxSpaceStart = spaceStart;
						maxSpaceLength = spaceLength;
					}
					spaceStart = -1;
					spaceLength = -1;
				}
			}
			if (maxSpaceStart < 0)
			{
				maxSpaceStart = maxUv;
				maxSpaceLength = 0;
			}

			// Create the two buffer arrays
			var data1 = new Vector3[maxSpaceStart - minUv];
			for (int i = 0; i < data1.Length; i++)
				data1[i] = uvData[i + minUv];
			var data2Offset = (maxSpaceStart + maxSpaceLength);
			var data2 = new Vector3[maxUv - data2Offset];
			for (int i = 0; i < data2.Length; i++)
				data2[i] = uvData[i + data2Offset];

			const int vector3Size = sizeof(float) * 3;
			var buffer1 = new ComputeBuffer(data1.Length, vector3Size);
			var buffer2 = new ComputeBuffer(data2.Length, vector3Size);
			buffer1.SetData(data1);
			buffer2.SetData(data2);

			var mat = Object.Instantiate(baseMaterial);
			mat.SetBuffer("_UvBuffer1", buffer1);
			mat.SetBuffer("_UvBuffer2", buffer2);
			mat.SetInt("_Buffer1Offset", minUv);
			mat.SetInt("_Buffer2Offset", data2Offset);

			skinnedData = new SkinnedData { buffer1 = buffer1, buffer2 = buffer2 };
			return mat;
		}

		static Vector4 GetPlane(Vector3 va, Vector3 vb, Vector3 vc)
		{
			var ab = vb - va;
			var ac = vc - va;
			var cross = Vector3.Cross(ab, ac);
			var d = -(cross.x * va.x + cross.y * va.y + cross.z * va.z);
			return new Vector4(cross.x, cross.y, cross.z, d);
		}

		public static void StartProjection(Renderer renderer, Mesh mesh,
			Transform projector, out Vector4[] planes, out Vector3[] verts)
		{
			var e = new[]
			{
				new Vector3(+1, +1, +1),
				new Vector3(+1, +1, -1),
				new Vector3(+1, -1, +1),
				new Vector3(-1, +1, +1),
				new Vector3(+1, -1, -1),
				new Vector3(-1, -1, +1),
				new Vector3(-1, +1, -1),
				new Vector3(-1, -1, -1),
			};
			for (int i = 0; i < e.Length; i++)
				e[i] = projector.TransformPoint(e[i] / 2);
			planes = new[]
			{
				GetPlane(e[1], e[6], e[4]),
				GetPlane(e[1], e[4], e[0]),
				GetPlane(e[1], e[0], e[6]),
				GetPlane(e[5], e[3], e[2]),
				GetPlane(e[5], e[7], e[3]),
				GetPlane(e[5], e[2], e[7]),
			};

			verts = mesh.vertices;
			for (int i = 0; i < verts.Length; i++)
				verts[i] = renderer.transform.TransformPoint(verts[i]);
		}
	}
}
