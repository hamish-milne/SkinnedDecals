using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace SkinnedDecals
{
	public static class MeshUtility
	{
		private static readonly Dictionary<DecalData, Mesh> cache = new Dictionary<DecalData, Mesh>();
		
		public static Mesh GetMesh(Mesh mesh, DecalData data, int index, int submeshMask)
        {
			Mesh ret;
			if(cache.TryGetValue(data, out ret))
				return ret;
			
			var uvData = data.GetUvData(index);
			var verts = mesh.vertices;
			var newVerts = new List<Vector3>(uvData.Count(uv => uv.z > 0));
			var vertMap = new Dictionary<int, int>(newVerts.Count);
			var newTris = new List<int>(newVerts.Count * 2);
			
			if(submeshMask == 0)
			{
				ProcessTriangles(uvData, mesh.triangles, verts, newTris, newVerts, vertMap);
			} else
			{
				for(int i = 0; i < mesh.subMeshCount; i++)
				{
					if((submeshMask & 1 << i) != 0)
						continue;
					ProcessTriangles(uvData, mesh.GetTriangles(i), verts, newTris, newVerts, vertMap);
				}
			}
			
			// TODO: Blendshapes
			
			ret = new Mesh();
			ret.subMeshCount = 1;
			ret.SetVertices(newVerts);
			ret.SetTriangles(newTris, 1);
			ret.UploadMeshData(true);
			cache[data] = ret;
			return ret;
		}
		
		static void ProcessTriangles(Vector3[] uvData, int[] tris, Vector3[] verts, List<int> newTris,
			List<Vector3> newVerts,Dictionary<int, int> vertMap)
		{
			for(int i = 0; i < tris.Length; i += 3)
			{
				if(verts[tris[i]].z < 0 || verts[tris[i+1]].z < 0 || verts[tris[i+1]].z < 0)
					continue;
				for(int j = 0; j < 3; j++)
				{
					var t = tris[i + j];
					int vIndex;
					if(!vertMap.TryGetValue(t, out vIndex))
					{
						vertMap.Add(t, newVerts.Count);
						newVerts.Add(verts[t]);
					}
				}
			}
		}
	}
}