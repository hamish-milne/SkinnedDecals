using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace SkinnedDecals
{
	public static class MeshUtility
	{
		private static readonly Dictionary<DecalInstance, Mesh> cache = new Dictionary<DecalInstance, Mesh>();
		
		public static Mesh GetMesh(Mesh mesh, DecalInstance data, int index, int submeshMask)
        {
			Mesh ret;
			if(cache.TryGetValue(data, out ret))
				return ret;
			
			Profiler.BeginSample($"Building mesh for ({mesh.name}, {data.Decal.name})");

			var uvData = data.GetUvData(index);
			var verts = mesh.vertices;
			var newVerts = new List<Vector3>(uvData.Count(uv => !float.IsInfinity(uv.x)));
			var vertMap = new int[verts.Length];
			var newTris = new List<int>(newVerts.Count * 2);

			for (int i = 0; i < vertMap.Length; i++)
				vertMap[i] = -1;

			// Map valid vertices to a new list
			if(submeshMask == 0)
			{
				Profiler.BeginSample("Processing triangles");
				ProcessTriangles(uvData, mesh.triangles, verts, newTris, newVerts, vertMap);
				Profiler.EndSample();
			} else
			{
				for(int i = 0; i < mesh.subMeshCount; i++)
				{
					if((submeshMask & 1 << i) != 0)
						continue;
					Profiler.BeginSample("Processing triangles for submesh " + i);
					ProcessTriangles(uvData, mesh.GetTriangles(i), verts, newTris, newVerts, vertMap);
					Profiler.EndSample();
				}
			}
			
			Profiler.BeginSample("Set geometry");
			ret = new Mesh {subMeshCount = 1};
			ret.SetVertices(newVerts);
			ret.SetTriangles(newTris, 0);
			ret.RecalculateBounds();
			ret.RecalculateNormals();
			Profiler.EndSample();

			// Find the start and end points of the new mesh within the old one
			int startVertex, endVertex;
			for (startVertex = 0; startVertex < vertMap.Length; startVertex++)
			{
				if(vertMap[startVertex] >= 0)
					break;
			}
			for (endVertex = vertMap.Length - 1; endVertex >= 0; endVertex--)
			{
				if(vertMap[endVertex] >= 0)
					break;
			}
			
			Profiler.BeginSample("Set UV, bone weight and bind poses");
			var boneWeights = mesh.boneWeights;
			var newBoneWeights = new BoneWeight[newVerts.Count];
			var newUv = new Vector2[newVerts.Count];
			for (int k = startVertex; k <= endVertex; k++)
			{
				var vIndex = vertMap[k];
				if (vIndex < 0) continue;
				newBoneWeights[vIndex] = boneWeights[k];
				newUv[vIndex] = uvData[k];
			}
			ret.boneWeights = newBoneWeights;
			ret.uv = newUv;
			ret.bindposes = mesh.bindposes;
			Profiler.EndSample();
			
			if (mesh.blendShapeCount > 0)
			{
				Profiler.BeginSample("Set blend shapes");
				var deltaVertices = verts; // We don't need the 'verts' array anymore so we can use it here
				var deltaNormals = new Vector3[mesh.vertexCount];
				var deltaTangents = new Vector3[mesh.vertexCount];

				var newDeltaVertices = new Vector3[ret.vertexCount];
				var newDeltaNormals = new Vector3[ret.vertexCount];
				var newDeltaTangents = new Vector3[ret.vertexCount];

				for (int i = 0; i < mesh.blendShapeCount; i++)
				{
					var frameCount = mesh.GetBlendShapeFrameCount(i);
					var shapeName = mesh.GetBlendShapeName(i);
					for (int j = 0; j < frameCount; j++)
					{
						var weight = mesh.GetBlendShapeFrameWeight(i, j);
						mesh.GetBlendShapeFrameVertices(i, j,
							deltaVertices, deltaNormals, deltaTangents);
						for (int k = startVertex; k <= endVertex; k++)
						{
							var vIndex = vertMap[k];
							if(vIndex < 0) continue;
							newDeltaVertices[vIndex] = deltaVertices[k];
							newDeltaNormals[vIndex] = deltaNormals[k];
							newDeltaTangents[vIndex] = deltaTangents[k];
						}
						ret.AddBlendShapeFrame(shapeName, weight,
							newDeltaVertices, newDeltaNormals, newDeltaTangents);
					}
				}
				Profiler.EndSample();
			}

			ret.UploadMeshData(true);
			cache[data] = ret;

			Profiler.EndSample();
			return ret;
		}
		
		static void ProcessTriangles(Vector2[] uvData, int[] tris, Vector3[] verts, List<int> newTris,
			List<Vector3> newVerts, int[] vertMap)
		{
			for(int i = 0; i < tris.Length; i += 3)
			{
				if( float.IsInfinity(uvData[tris[i]].x) ||
					float.IsInfinity(uvData[tris[i + 1]].x) ||
					float.IsInfinity(uvData[tris[i + 2]].x))
					continue;
				for(int j = 0; j < 3; j++)
				{
					var t = tris[i + j];
					int vIndex = vertMap[t];
					if(vIndex < 0)
					{
						vertMap[t] = vIndex = newVerts.Count;
						newVerts.Add(verts[t]);
					}
					newTris.Add(vIndex);
				}
			}
		}
	}

	public class MeshTest : MonoBehaviour
	{
		public Mesh oldMesh;
		public Mesh newMesh;
		public DecalProjector projector;
		public DecalObject obj;
		public SkinnedMeshRenderer smr;
		public MeshFilter mf;

		public void Update()
		{
			if (Input.GetKeyDown(KeyCode.B))
			{
				var instance = projector.Project(obj);
				newMesh = MeshUtility.GetMesh(oldMesh, instance, 0, 0);
				smr.sharedMesh = newMesh;
				mf.sharedMesh = newMesh;
			}
		}
	}
}