using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkinnedDecals.Internal
{
	public abstract class BaseInstance : DecalCameraInstance
	{
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

		protected BaseInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera)
		{
			var decal = Decal.Decal;
			this.renderer = rendererIndex < 0 ? null : parent.Object.Renderers[rendererIndex];
			this.submeshMask = submeshMask;
			// ReSharper disable once VirtualMemberCallInContructor
			cmd = new CommandBuffer {name = $"{CommandName} ({renderer?.name}, {decal.name})"};
			// ReSharper disable once VirtualMemberCallInContructor
			material = new Material(Shader.Find(ShaderName));
			material.mainTexture = decal.Albedo;
			material.SetTextureKeyword("_BumpMap", "_NORMALMAP", decal.Normal);
			material.SetTextureKeyword("_MetallicGlossMap", "_METALLICGLOSSMAP", decal.Roughness);
			sharedMaterials = renderer?.sharedMaterials;
		}

		protected override void Enable()
		{
			Camera.Camera.AddCommandBuffer(CameraEvent, cmd);
		}

		protected override void Disable()
		{
			Camera.Camera.RemoveCommandBuffer(CameraEvent, cmd);
		}

		public override void Dispose()
		{
			cmd.Dispose();
			UnityEngine.Object.Destroy(material);
			if (materials == null) return;
			foreach(var m in materials)
				UnityEngine.Object.Destroy(m);
		}
	}

	public class RenderObjectInstance : BaseInstance
	{
		protected override string ShaderName => "Standard";

		protected override CameraEvent CameraEvent => CameraEvent.AfterEverything;
		protected override string CommandName => "";

		public RenderObjectInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
			// TODO
		}
	}

	public class ScreenSpaceInstance : BaseInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterForwardOpaque;

		protected override string CommandName => "Screen space decal";

		protected override string ShaderName => "Decal/ScreenSpace";

		public ScreenSpaceInstance(DecalInstance parent, DecalCamera camera,
			Mesh cubeMesh) : base(parent, camera, -1)
		{
			camera.Camera.depthTextureMode = DepthTextureMode.Depth;
			var matrix = Decal.ObjectToProjector.inverse * parent.Object.transform.localToWorldMatrix;
			cmd.DrawMesh(cubeMesh, matrix, material);
		}
	}

	public abstract class StaticInstance : BaseInstance
	{
		protected override string CommandName => "Static decal";

		protected StaticInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
			material.SetMatrix("_ObjectToProjector",
				parent.Decal.GetProjectionMatrix(parent.Object.Renderers.IndexOf(renderer)));
		}
	}

	public class DeferredStaticInstance : StaticInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterGBuffer;

		public DeferredStaticInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
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

		public ForwardStaticInstance(DecalInstance parent, DecalCamera camera, int rendererIndex, 
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
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
					matCache.Add(tex, mat = UnityEngine.Object.Instantiate(material));
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
		private struct SkinnedData
		{
			public ComputeBuffer buffer1, buffer2;
		}
		
		private static Material CreateSkinnedData(Vector3[] uvData,
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

			var mat = UnityEngine.Object.Instantiate(baseMaterial);
			mat.SetBuffer("_UvBuffer1", buffer1);
			mat.SetBuffer("_UvBuffer2", buffer2);
			mat.SetInt("_Buffer1Offset", minUv);
			mat.SetInt("_Buffer2Offset", data2Offset);

			skinnedData = new SkinnedData { buffer1 = buffer1, buffer2 = buffer2 };
			return mat;
		}
		
		protected override string CommandName => "Skinned decal";

		private static readonly Dictionary<Material, SkinnedData> skinnedData
			= new Dictionary<Material, SkinnedData>();
		
		protected virtual Material ModifySubmeshMaterial(Material baseMaterial, int submesh)
		{
			return baseMaterial;
		}

		protected void ProjectSkinned(SkinnedMeshRenderer renderer, int index)
		{
			var uvData = Decal.GetUvData(index);

			var matList = new List<Material>();
			var count = renderer.sharedMesh.subMeshCount;
			for (int i = 0; i < count; i++)
			{
				if((submeshMask & 1 << i) != 0)
					continue;
				Profiler.BeginSample("Submesh " + i);
				SkinnedData data;
				var mat = CreateSkinnedData(Decal.GetUvData(index), material, out data);
				mat = ModifySubmeshMaterial(mat, i);
				matList.Add(mat);
				cmd.DrawRenderer(renderer, mat, i);
				Profiler.EndSample();
			}
			if (matList.Count == 1)
				material = matList[0];
			else
				materials = matList.ToArray();
		}

		protected SkinnedInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
			ProjectSkinned((SkinnedMeshRenderer)renderer, rendererIndex);
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

		public DeferredSkinnedInstance(DecalInstance parent, DecalCamera camera,
			int rendererIndex, int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
		}
	}

	public class ForwardSkinnedInstance : SkinnedInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterForwardOpaque;
		
		protected override Material ModifySubmeshMaterial(Material baseMaterial, int submesh)
		{
			baseMaterial.SetTexture("_BodyAlbedo", sharedMaterials[submesh].mainTexture);
			return baseMaterial;
		}

		public ForwardSkinnedInstance(DecalInstance parent, DecalCamera camera,
			int rendererIndex, int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
		}
	}
}
