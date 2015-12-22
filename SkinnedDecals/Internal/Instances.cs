using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SkinnedDecals.Internal
{
	public abstract class BaseInstance : DecalInstance
	{
		protected readonly Camera camera;
		protected readonly DecalTextureSet decal;
		protected readonly Renderer renderer;
		protected Material material;
		protected Material[] sharedMaterials;
		protected readonly CommandBuffer cmd;
		protected Material[] materials;
		protected readonly int submeshMask;

		protected abstract CameraEvent CameraEvent { get; }

		protected abstract string CommandName { get; }

		protected virtual string ShaderName => "Decal/Normal";

		public override Color Color
		{
			get { return material.color; }
			set
			{
				material.color = value;
				if (materials == null) return;
				foreach (var m in materials)
					m.color = value;
			}
		}

		protected BaseInstance(Camera camera, DecalTextureSet decal, Renderer renderer, int submeshMask = 0)
		{
			this.camera = camera;
			this.decal = decal;
			this.renderer = renderer;
			this.submeshMask = submeshMask;
			// ReSharper disable once VirtualMemberCallInContructor
			cmd = new CommandBuffer {name = $"{CommandName} ({renderer.name}, {decal.name})"};
			// ReSharper disable once VirtualMemberCallInContructor
			material = new Material(Shader.Find(ShaderName));
			Texture2D albedo, normal, roughness;
			decal.GetTextures(out albedo, out normal, out roughness);
			material.mainTexture = albedo;
			Debug.Log(material.GetTexture("_MainTex"));
			material.SetTexture("_BumpMap", normal);
			material.SetTexture("_MetallicGlossMap", roughness);
			sharedMaterials = renderer.sharedMaterials;
		}

		protected override void Enable()
		{
			camera.AddCommandBuffer(CameraEvent, cmd);
		}

		protected override void Disable()
		{
			camera.RemoveCommandBuffer(CameraEvent, cmd);
		}

		public override void Dispose()
		{
			cmd.Dispose();
			Object.Destroy(material);
			if (materials == null) return;
			foreach(var m in materials)
				Object.Destroy(m);
		}
	}

	public class ScreenSpaceInstance : BaseInstance
	{
		private static readonly RenderTargetIdentifier[] colorTargets;
		private static readonly RenderTargetIdentifier none =
			new RenderTargetIdentifier(BuiltinRenderTextureType.GBuffer3);
		private static readonly RenderTargetIdentifier depth =
			new RenderTargetIdentifier(BuiltinRenderTextureType.Depth);

		static ScreenSpaceInstance()
		{
			colorTargets =
				new[]
				{
					BuiltinRenderTextureType.GBuffer0,
					BuiltinRenderTextureType.GBuffer1,
					BuiltinRenderTextureType.GBuffer2,
					BuiltinRenderTextureType.GBuffer3
				}.Select(type => new RenderTargetIdentifier(type))
				.ToArray();
		}

		protected override CameraEvent CameraEvent => CameraEvent.AfterForwardOpaque;

		protected override string CommandName => "Screen space decal";

		protected override string ShaderName => "Decal/ScreenSpace";

		public ScreenSpaceInstance(Camera camera, DecalProjector projector, Renderer renderer, Mesh cubeMesh)
			: base(camera, projector.Decal, renderer)
		{
			//cmd.SetRenderTarget(colorTargets, none);
			//cmd.SetGlobalTexture("_Depth", depth);
			material.SetVector("_LocalCameraPos", projector.transform.worldToLocalMatrix * (Vector4)camera.transform.position);
			camera.depthTextureMode = DepthTextureMode.Depth;
			cmd.DrawMesh(cubeMesh, projector.transform.localToWorldMatrix, material);
		}
	}

	public abstract class StaticInstance : BaseInstance
	{
		protected override string CommandName => "Static decal";

		protected StaticInstance(Camera camera, Renderer renderer, DecalProjector projector,
			int submeshMask = 0) : base(camera, projector.Decal, renderer, submeshMask)
		{
			var projectorMatrix = projector.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
			material.SetMatrix("_ProjectorMatrix", projectorMatrix);
		}
	}

	public class DeferredStaticInstance : StaticInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterGBuffer;

		public DeferredStaticInstance(Camera camera, Renderer renderer, DecalProjector projector,
			int submeshMask = 0) : base(camera, renderer, projector, submeshMask)
		{
			for (int i = 0; i < sharedMaterials.Length; i++)
			{
				if ((submeshMask & 1 << i) != 0)
					continue;
				cmd.DrawRenderer(renderer, material, i);
			}
		}
	}

	public class ForwardStaticInstance : StaticInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterForwardOpaque;

		public ForwardStaticInstance(Camera camera, Renderer renderer, DecalProjector projector,
			int submeshMask = 0) : base(camera, renderer, projector, submeshMask)
		{
			var matCache = new Dictionary<Texture2D, Material>();
			for (int i = 0; i < sharedMaterials.Length; i++)
			{
				if ((submeshMask & 1 << i) != 0)
					continue;
				Material mat;
				var tex = sharedMaterials[i].mainTexture as Texture2D;
				if (tex == null)
					mat = material;
				else if (!matCache.TryGetValue(tex, out mat))
				{
					matCache.Add(tex, mat = Object.Instantiate(material));
					mat.SetTexture("_BodyAlbedo", tex);
				}
				cmd.DrawRenderer(renderer, mat, i);
			}
			if (matCache.Count == 1)
				material = matCache.First().Value;
			else
				materials = matCache.Values.ToArray();
		}
	}

	public abstract class SkinnedInstance : BaseInstance
	{
		protected override string CommandName => "Skinned decal";

		private struct SkinnedData
		{
			public ComputeBuffer buffer1, buffer2;
		}

		private static readonly Dictionary<Material, SkinnedData> skinnedData
			= new Dictionary<Material, SkinnedData>(); 

		private static Vector3 UvOffset(Vector3 u)
		{
			return new Vector3(u.x + 0.5f, u.y + 0.5f, 1);
		}

		protected virtual Material ProjectSubmesh(Mesh mesh, Transform projector,
			Vector3[] verts, Vector3[] uvData, Vector4[] planes, int submesh)
		{
			Profiler.BeginSample("Calculate UVs");
			for (int i = 0; i < uvData.Length; i++)
				// 10 is an arbitrary value such that x>1 or x<-1
				// -1 is an arbitrary negative value
				uvData[i] = new Vector3(10, 10, -1);

			var tris = mesh.GetTriangles(submesh);

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
			}
			Profiler.EndSample();

			Profiler.BeginSample("Optimize buffers");
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
			Profiler.EndSample();

			Profiler.BeginSample("Create buffers");
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

			var mat = Object.Instantiate(material);
			mat.SetBuffer("_UvBuffer1", buffer1);
			mat.SetBuffer("_UvBuffer2", buffer2);
			mat.SetInt("_Buffer1Offset", minUv);
			mat.SetInt("_Buffer2Offset", data2Offset);

			skinnedData.Add(mat, new SkinnedData {buffer1 = buffer1, buffer2 = buffer2});

			Profiler.EndSample();
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

		protected void ProjectSkinned(SkinnedMeshRenderer renderer, Transform projector)
		{
			Profiler.BeginSample("Initialization");

			var mesh = new Mesh();
			renderer.BakeMesh(mesh);

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
			var planes = new[]
			{
				GetPlane(e[1], e[6], e[4]),
				GetPlane(e[1], e[4], e[0]),
				GetPlane(e[1], e[0], e[6]),
				GetPlane(e[5], e[3], e[2]),
				GetPlane(e[5], e[7], e[3]),
				GetPlane(e[5], e[2], e[7]),
			};

			Profiler.EndSample();
			Profiler.BeginSample("Getting data");

			var verts = mesh.vertices;

			for (int i = 0; i < verts.Length; i++)
				verts[i] = renderer.transform.TransformPoint(verts[i]);

			var uvData = new Vector3[verts.Length];

			Profiler.EndSample();

			var matList = new List<Material>();
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				if((submeshMask & 1 << i) != 0)
					continue;
				Profiler.BeginSample("Submesh " + i);
				var mat = ProjectSubmesh(mesh, projector, verts, uvData, planes, i);
				matList.Add(mat);
				cmd.DrawRenderer(renderer, mat, i);
				Profiler.EndSample();
			}
			if (matList.Count == 1)
				material = matList[0];
			else
				materials = matList.ToArray();
		}

		protected SkinnedInstance(Camera camera, DecalTextureSet decal, SkinnedMeshRenderer renderer,
			Transform projector, int submeshMask = 0) : base(camera, decal, renderer, submeshMask)
		{
			ProjectSkinned(renderer, projector);
		}

		public override void Dispose()
		{
			SkinnedData data;
			if (materials != null)
				foreach (var m in materials)
				{
					if (!skinnedData.TryGetValue(m, out data)) continue;
					data.buffer2?.Dispose();
					data.buffer1?.Dispose();
				}
			if (skinnedData.TryGetValue(material, out data))
			{
				data.buffer2?.Dispose();
				data.buffer1?.Dispose();
			}
			base.Dispose();
		}
	}

	public class DeferredSkinnedInstance : SkinnedInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterGBuffer;

		public DeferredSkinnedInstance(Camera camera, DecalTextureSet decal, SkinnedMeshRenderer renderer,
			Transform projector, int submeshMask = 0) : base(camera, decal, renderer, projector, submeshMask)
		{
		}
	}

	public class ForwardSkinnedInstance : SkinnedInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterForwardOpaque;

		protected override Material ProjectSubmesh(Mesh mesh, Transform projector,
			Vector3[] verts, Vector3[] uvData, Vector4[] planes, int submesh)
		{
			var mat = base.ProjectSubmesh(mesh, projector, verts, uvData, planes, submesh);
			mat.SetTexture("_BodyAlbedo", sharedMaterials[submesh].mainTexture);
			return mat;
		}

		public ForwardSkinnedInstance(Camera camera, DecalTextureSet decal, SkinnedMeshRenderer renderer,
			Transform projector, int submeshMask = 0) : base(camera, decal, renderer, projector, submeshMask)
		{
		}
	}
}
