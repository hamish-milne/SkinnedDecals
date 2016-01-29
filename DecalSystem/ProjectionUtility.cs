using UnityEngine;

namespace DecalSystem
{
	public static class ProjectionUtility
	{
		private static readonly Vector3[] planeMask =
		{
			new Vector3( 1,  0,  0),
			new Vector3(-1,  0,  0),
			new Vector3( 0,  1,  0),
			new Vector3( 0, -1,  0),
			new Vector3( 0,  0,  1),
			new Vector3( 0,  0, -1),
		};

		public static void TransformVerts(Vector3[] verts,
			Transform obj, Transform projector)
		{
			for (int i = 0; i < verts.Length; i++)
				verts[i] = projector.InverseTransformPoint(obj.TransformPoint(verts[i]));
		}

		public static bool Project(int[] tris, Vector3[] verts, Vector2[] uvData, bool dontClear = false)
		{
			if (!dontClear)
			{
				for (int i = 0; i < uvData.Length; i++)
					uvData[i] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			}
			bool ret = false;
			for (int i = 0; i < tris.Length; i += 3)
			{
				// Vertex IDs for this triangle
				var t1 = tris[i];
				var t2 = tris[i + 1];
				var t3 = tris[i + 2];

				// Projected vertex positions
				var v1 = verts[t1];
				var v2 = verts[t2];
				var v3 = verts[t3];

				// Check that this triangle intersects the projection box
				//     by checking that for each of the box's 6 planes, at least
				//     one vertex is on the side facing the centre of the box
				var check = true;
				// Keep this as a loop for speed
				// ReSharper disable once LoopCanBeConvertedToQuery
				foreach (var p in planeMask)
				{
					if ((v1.x*p.x) + (v1.y*p.y) + (v1.z*p.z) < 0.5f ||
						(v2.x*p.x) + (v2.y*p.y) + (v2.z*p.z) < 0.5f ||
						(v3.x*p.x) + (v3.y*p.y) + (v3.z*p.z) < 0.5f)
						continue;
					check = false;
					break;
				}
				if (!check)
					continue;

				// Compute the UV data at each vertex
				uvData[t1] = ((Vector2) v1) + new Vector2(0.5f, 0.5f);
				uvData[t2] = ((Vector2) v2) + new Vector2(0.5f, 0.5f);
				uvData[t3] = ((Vector2) v3) + new Vector2(0.5f, 0.5f);
				ret = true;
			}
			return ret;
		}
	}
}
