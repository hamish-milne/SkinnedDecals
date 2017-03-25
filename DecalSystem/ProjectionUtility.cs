using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace DecalSystem
{
	// ReSharper disable once InconsistentNaming
	/// <summary>
	/// A general pair structure that will be faster than <c>KeyValuePair</c>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="U"></typeparam>
	public struct Pair<T, U> : IEquatable<Pair<T, U>>
	{
		public T First { get; }
		public U Second { get; }

		public Pair(T first, U second)
		{
			First = first;
			Second = second;
		}

		public override int GetHashCode()
		{
			return (First?.GetHashCode() ?? 0) * 17 ^ (Second?.GetHashCode() ?? 0) * 23;
		}

		public bool Equals(Pair<T, U> other)
		{
			return other.First.Equals(First) && other.Second.Equals(Second);
		}
	}

	/// <summary>
	/// Provides mesh projection functionality
	/// </summary>
	public static class ProjectionUtility
	{

		/// <summary>
		/// Transforms the given vertex array into decal space
		/// </summary>
		/// <param name="verts">The array of verticies, modified in-place</param>
		/// <param name="obj">The mesh-providing object in the scene</param>
		/// <param name="projector">The projector transform</param>
		public static void TransformVerts(Vector3[] verts, Transform obj, Transform projector)
		{
			Profiler.BeginSample("Transform vertices");
			for (int i = 0; i < verts.Length; i++)
				verts[i] = projector.InverseTransformPoint(obj.TransformPoint(verts[i]));
			Profiler.EndSample();
		}

		public static void TransformNormals(Vector3[] normals, Transform obj, Transform projector)
		{
			if (normals == null) return;
			Profiler.BeginSample("Transform normals");
			for (int i = 0; i < normals.Length; i++)
				normals[i] = projector.InverseTransformDirection(obj.TransformDirection(normals[i]));
			Profiler.EndSample();
		}
		
		public static bool Project(int[] tris, Vector3[] verts, Vector3[] normals, Vector2[] uvData, float maxNormal = 0f, bool dontClear = false)
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
				if(normals != null)
					if (normals[t1].z > maxNormal &&
						normals[t2].z > maxNormal &&
						normals[t3].z > maxNormal)
						continue;

				// Compute the UV data at each vertex
				uvData[t1] = new Vector2(v1.x + 0.5f, v1.y + 0.5f);
				uvData[t2] = new Vector2(v2.x + 0.5f, v2.y + 0.5f);
				uvData[t3] = new Vector2(v3.x + 0.5f, v3.y + 0.5f);
				ret = true;
			}
			Profiler.EndSample();
			return ret;
		}

		public static Mesh GetMesh(Mesh mesh, Transform obj, Transform projector, Mesh skinnedMesh, int submeshMask, float? interpolateNormals, float maxNormal = 0f)
		{
			Profiler.BeginSample("Building static decal mesh");

			var verts = mesh.vertices;
			var normals = maxNormal < 1f ? mesh.normals : null;
			var newVerts = new List<Vector3>(mesh.vertexCount);
			var vertMap = new int[verts.Length];
			var newTris = new List<int>(mesh.vertexCount);

			TransformVerts(verts, obj, projector);
			TransformNormals(normals, obj, projector);

			for (int i = 0; i < vertMap.Length; i++)
				vertMap[i] = -1;

			// Map valid vertices to a new list
			if (submeshMask == 0)
			{
				Profiler.BeginSample("Processing triangles");
				ProcessTriangles(mesh.triangles, verts, newTris, newVerts, vertMap, normals, maxNormal);
				Profiler.EndSample();
			}
			else
			{
				for (int i = 0; i < mesh.subMeshCount; i++)
				{
					if ((submeshMask & 1 << i) != 0)
						continue;
					Profiler.BeginSample("Processing triangles for submesh " + i);
					ProcessTriangles(mesh.GetTriangles(i), verts, newTris, newVerts, vertMap, normals, maxNormal);
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
			if (interpolateNormals.HasValue)
			{
				var decalToObject = obj.worldToLocalMatrix * projector.localToWorldMatrix;
				var bitangent = decalToObject.GetColumn(1);
				var normal = -(Vector3) decalToObject.GetColumn(2);
				var t = interpolateNormals.Value;
				var newNormals = reverseMap
					.Select(id => Vector3.Lerp(oldNormals[id], normal, t))
					.ToArray();
				ret.normals = newNormals; // TODO: Replace with SetNormals etc.
				//ret.normals = reverseMap.Select(id => oldNormals[id]).ToArray();
				ret.tangents = newNormals
					.Select(n => Vector3.Cross(n, bitangent))
					.Select(v => new Vector4(v.x, v.y, v.z, -1))
					.ToArray();
			}
			else
			{
				ret.normals = reverseMap.Select(id => oldNormals[id]).ToArray();
				ret.tangents = reverseMap.Select(id => oldTangents[id]).ToArray();
			}
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

		private static bool CheckPlanes(Vector3 v1, Vector3 v2, Vector3 v3)
		{
			// Check that this triangle intersects the projection box
			//     by checking that for each of the box's 6 planes, at least
			//     one vertex is on the side facing the centre of the box
			if ( v1.x > 0.5f &&  // 1, 0, 0
			     v2.x > 0.5f &&
			     v3.x > 0.5f)
				return false;
			if (-v1.x > 0.5f &&  // -1, 0, 0
			    -v2.x > 0.5f &&
			    -v3.x > 0.5f)
				return false;
			if ( v1.y > 0.5f &&  // 0, 1, 0
				 v2.y > 0.5f &&
				 v3.y > 0.5f)
				return false;
			if (-v1.y > 0.5f &&  // 0, -1, 0
				-v2.y > 0.5f &&
				-v3.y > 0.5f)
				return false;
			if ( v1.z > 0.5f &&  // 0, 0, 1
				 v2.z > 0.5f &&
				 v3.z > 0.5f)
				return false;
			if (-v1.z > 0.5f &&  // 0, 0, -1
				-v2.z > 0.5f &&
				-v3.z > 0.5f)
				return false;
			return true;
		}

		private static void ProcessTriangles(int[] tris, Vector3[] verts, List<int> newTris,
			List<Vector3> newVerts, int[] vertMap, Vector3[] normals = null, float maxNormal = 0f)
		{
			for (int i = 0; i < tris.Length; i += 3)
			{
				var t1 = tris[i];
				var t2 = tris[i + 1];
				var t3 = tris[i + 2];
				if (!CheckPlanes(verts[t1], verts[t2], verts[t3]))
					continue;
				if (normals != null)
					if (normals[t1].z > maxNormal &&
						normals[t2].z > maxNormal &&
						normals[t3].z > maxNormal)
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
