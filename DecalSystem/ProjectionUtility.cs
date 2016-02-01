using System;
using System.Collections.Generic;
using System.Linq;
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
			Profiler.BeginSample("Transform vertices");
			for (int i = 0; i < verts.Length; i++)
				verts[i] = projector.InverseTransformPoint(obj.TransformPoint(verts[i]));
			Profiler.EndSample();
		}

		// TODO: (maybe) Limit normals
		public static bool Project(int[] tris, Vector3[] verts, Vector2[] uvData, bool dontClear = false)
		{
			if (!dontClear)
			{
				for (int i = 0; i < uvData.Length; i++)
					uvData[i] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			}
			bool ret = false;
			Profiler.BeginSample("Main projection loop");
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

				if (!CheckPlanes(v1, v2, v3))
					continue;

				// Compute the UV data at each vertex
				uvData[t1] = ((Vector2) v1) + new Vector2(0.5f, 0.5f);
				uvData[t2] = ((Vector2) v2) + new Vector2(0.5f, 0.5f);
				uvData[t3] = ((Vector2) v3) + new Vector2(0.5f, 0.5f);
				ret = true;
			}
			Profiler.EndSample();
			return ret;
		}

		public static Mesh GetMesh(Mesh mesh, Transform obj, Transform projector, Mesh skinnedMesh, int submeshMask)
		{
			Profiler.BeginSample("Building static decal mesh");

			var verts = mesh.vertices;
			var newVerts = new List<Vector3>(mesh.vertexCount);
			var vertMap = new int[verts.Length];
			var newTris = new List<int>(mesh.vertexCount);

			TransformVerts(verts, obj, projector);

			for (int i = 0; i < vertMap.Length; i++)
				vertMap[i] = -1;

			// Map valid vertices to a new list
			if (submeshMask == 0)
			{
				Profiler.BeginSample("Processing triangles");
				ProcessTriangles(mesh.triangles, verts, newTris, newVerts, vertMap);
				Profiler.EndSample();
			}
			else
			{
				for (int i = 0; i < mesh.subMeshCount; i++)
				{
					if ((submeshMask & 1 << i) != 0)
						continue;
					Profiler.BeginSample("Processing triangles for submesh " + i);
					ProcessTriangles(mesh.GetTriangles(i), verts, newTris, newVerts, vertMap);
					Profiler.EndSample();
				}
			}

			if(newVerts.Count == 0 || newTris.Count == 0)
				throw new Exception("New mesh is empty");

			// Get the vertex IDs of the old mesh..
			var reverseMap = new int[newVerts.Count];
			for (int i = 0; i < vertMap.Length; i++)
				if (vertMap[i] >= 0)
					reverseMap[vertMap[i]] = i;

			Profiler.BeginSample("Set geometry");
			var ret = new Mesh { subMeshCount = 1 };
			var newUvs = newVerts.Select(v => (Vector2)v + new Vector2(0.5f, 0.5f)).ToArray();
			// Inverse transform verts
			for(int i = 0; i < newVerts.Count; i++)
				newVerts[i] = obj.InverseTransformPoint(projector.TransformPoint(newVerts[i]));

			// Set vertices before anything else
			ret.SetVertices(newVerts);
			ret.uv = newUvs;
			ret.SetTriangles(newTris, 0);
			var oldNormals = mesh.normals;
			var oldTangents = mesh.tangents;
			ret.normals = reverseMap.Select(id => oldNormals[id]).ToArray();
			ret.tangents = reverseMap.Select(id => oldTangents[id]).ToArray();
			ret.RecalculateBounds();
			Profiler.EndSample();

			// Find the start and end points of the new mesh within the old one
			int startVertex, endVertex;
			for (startVertex = 0; startVertex < vertMap.Length; startVertex++)
			{
				if (vertMap[startVertex] >= 0)
					break;
			}
			for (endVertex = vertMap.Length - 1; endVertex >= 0; endVertex--)
			{
				if (vertMap[endVertex] >= 0)
					break;
			}

			if (skinnedMesh != null)
			{
				Profiler.BeginSample("Setting skin data");
				var boneWeights = skinnedMesh.boneWeights;
				if (boneWeights.Length != mesh.vertexCount)
				{
					Debug.LogError(skinnedMesh + " has no skin data");
				}
				else
				{
					var newBoneWeights = new BoneWeight[newVerts.Count];
					for (int k = startVertex; k <= endVertex; k++)
					{
						var vIndex = vertMap[k];
						if (vIndex < 0) continue;
						newBoneWeights[vIndex] = boneWeights[k];
					}
					ret.boneWeights = newBoneWeights;
					ret.bindposes = skinnedMesh.bindposes;
				}
				Profiler.EndSample();
			}

			mesh = skinnedMesh;
			if (mesh != null && mesh.blendShapeCount > 0)
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
							if (vIndex < 0) continue;
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

			Profiler.EndSample();
			return ret;
		}

		static bool CheckPlanes(Vector3 v1, Vector3 v2, Vector3 v3)
		{
			// Check that this triangle intersects the projection box
			//     by checking that for each of the box's 6 planes, at least
			//     one vertex is on the side facing the centre of the box
			var check = true;
			// Keep this as a loop for speed
			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var p in planeMask)
			{
				if ((v1.x * p.x) + (v1.y * p.y) + (v1.z * p.z) < 0.5f ||
					(v2.x * p.x) + (v2.y * p.y) + (v2.z * p.z) < 0.5f ||
					(v3.x * p.x) + (v3.y * p.y) + (v3.z * p.z) < 0.5f)
					continue;
				check = false;
				break;
			}
			return check;
		}

		static void ProcessTriangles(int[] tris, Vector3[] verts, List<int> newTris,
			List<Vector3> newVerts, int[] vertMap)
		{
			for (int i = 0; i < tris.Length; i += 3)
			{
				if (!CheckPlanes(verts[tris[i]], verts[tris[i + 1]], verts[tris[i + 2]]))
					continue;
				for (int j = 0; j < 3; j++)
				{
					var t = tris[i + j];
					int vIndex = vertMap[t];
					if (vIndex < 0)
					{
						vertMap[t] = vIndex = newVerts.Count;
						newVerts.Add(verts[t]);
					}
					newTris.Add(vIndex);
				}
			}
		}
	}
}
