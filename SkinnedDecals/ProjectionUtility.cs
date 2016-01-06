using UnityEngine;

namespace SkinnedDecals
{
	public static class ProjectionUtility
	{
		private static readonly Vector4[] planes;
		
		private static Vector4 GetPlane(Vector3 va, Vector3 vb, Vector3 vc)
		{
			var ab = vb - va;
			var ac = vc - va;
			var cross = Vector3.Cross(ab, ac);
			var d = -(cross.x * va.x + cross.y * va.y + cross.z * va.z);
			return new Vector4(cross.x, cross.y, cross.z, d);
		}
		
		static ProjectionUtility()
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
				e[i] = e[i] / 2;
			planes = new[]
			{
				GetPlane(e[1], e[6], e[4]),
				GetPlane(e[1], e[4], e[0]),
				GetPlane(e[1], e[0], e[6]),
				GetPlane(e[5], e[3], e[2]),
				GetPlane(e[5], e[7], e[3]),
				GetPlane(e[5], e[2], e[7]),
			};
		}
		
		private static Vector3 UvOffset(Vector3 u)
		{
			return new Vector3(u.x + 0.5f, u.y + 0.5f, 1);
		}

		public static void BakeUvData(int[] tris, Vector3[] verts, Matrix4x4 projector, Vector3[] uvData,
			bool dontClear = false)
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
				var v1 = verts[t1];
				var v2 = verts[t2];
				var v3 = verts[t3];

				// Check that this triangle intersects the projection box
				//     by checking that for each of the box's 8 planes, at least
				//     one vertex is on the side facing the centre of the box
				var check = true;
				// Keep this as a loop for speed
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var plane in planes)
				{
					if (plane.x * v1.x + plane.y * v1.y + plane.z * v1.z + plane.w > 0 ||
						plane.x * v2.x + plane.y * v2.y + plane.z * v2.z + plane.w > 0 ||
						plane.x * v3.x + plane.y * v3.y + plane.z * v3.z + plane.w > 0)
						continue;
					check = false;
					break;
				}
				if (!check)
					continue;

				// Compute the UV data at each vertex
				uvData[t1] = UvOffset(projector * v1);
				uvData[t2] = UvOffset(projector * v2);
				uvData[t3] = UvOffset(projector * v3);
			}
		}		
	}
}
