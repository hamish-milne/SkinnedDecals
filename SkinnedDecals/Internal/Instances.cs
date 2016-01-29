using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
// ReSharper disable DoNotCallOverridableMethodsInConstructor

namespace SkinnedDecals.Internal
{
	public abstract class BaseInstance : DecalCameraInstance
	{
		protected readonly Renderer renderer;
		protected Material material;
		protected Material[] sharedMaterials;
		protected Material[] materials;
		protected readonly int submeshMask;

		protected abstract string ShaderName { get; }

		protected BaseInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera)
		{
			renderer = rendererIndex < 0 ? null : parent.Object.Renderers[rendererIndex];
			this.submeshMask = submeshMask;
			material = new Material(Shader.Find(ShaderName)) {mainTexture = Decal.Albedo};
			material.SetTextureKeyword("_BumpMap", "_NORMALMAP", Decal.Normal);
			material.SetTextureKeyword("_MetallicGlossMap", "_METALLICGLOSSMAP", Decal.Roughness);
			material.SetTextureKeyword("_ParallaxMap", "_PARALLAXMAP", Decal.Height);
			material.SetTexture("_EmissionMap", Decal.Emission);
			material.SetColor("_EmissionColor", Decal.EmissionColor);
			if(Decal.EmissionColor != Color.black)
				material.EnableKeyword("_EMISSION");
			else
				material.DisableKeyword("_EMISSION");
			material.color = Decal.MainColor;
			sharedMaterials = renderer?.sharedMaterials;
		}

		public override void Dispose()
		{
			UnityEngine.Object.Destroy(material);
			if (materials == null) return;
			foreach(var m in materials)
				UnityEngine.Object.Destroy(m);
		}
	}

	public abstract class CommandInstance : BaseInstance
	{
		protected readonly CommandBuffer cmd;

		protected abstract RenderingPath RenderingPath { get; }

		protected abstract CameraEvent CameraEvent { get; }

		protected abstract string CommandName { get; }

		protected CommandInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
			cmd = new CommandBuffer {name = $"{CommandName} ({renderer?.name}, {Decal.name})"};
			//cmd.SetRenderTarget(BuiltinRenderTextureType., BuiltinRenderTextureType.CameraTarget);
			switch (RenderingPath)
			{
				case RenderingPath.DeferredShading:
					material.DisableKeyword("_FORWARD");
					material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
					material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
					break;
				case RenderingPath.Forward:
					material.EnableKeyword("_FORWARD");
					material.SetInt("_SrcBlend", (int)BlendMode.DstColor);
					material.SetInt("_DstBlend", (int)BlendMode.Zero);
					break;
			}
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
			base.Dispose();
		}
	}

	public class RenderObjectInstance : CommandInstance
	{
		protected override string ShaderName => "Decal/Standard";

		private readonly Renderer decalRenderer;

		public RenderObjectInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
			var smr = renderer as SkinnedMeshRenderer;
			var mesh = smr != null ? smr.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
			var newMesh = MeshUtility.GetMesh(mesh, parent, rendererIndex, submeshMask);
			var newGo = new GameObject("Decal");
			newGo.transform.parent = renderer.transform;
			newGo.transform.localPosition = Vector3.zero;
			newGo.transform.localRotation = Quaternion.identity;
			if (smr != null)
			{
				var newSmr = newGo.AddComponent<SkinnedMeshRenderer>();
				newSmr.sharedMesh = newMesh;
				newSmr.bones = smr.bones;
				newSmr.rootBone = smr.rootBone;
				decalRenderer = newSmr;
			}
			else
			{
				newGo.AddComponent<MeshFilter>().sharedMesh = newMesh;
				decalRenderer = newGo.AddComponent<MeshRenderer>();
			}
			decalRenderer.enabled = false;
			/*material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", 5);
			material.SetInt("_DstBlend", 10);
			material.SetInt("_ZWrite", 0);
			material.DisableKeyword("_ALPHATEST_ON");
			material.EnableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = 3000;*/
			decalRenderer.material = material;

			cmd.DrawRenderer(decalRenderer, material, 0, 1);
		}

		protected override RenderingPath RenderingPath => RenderingPath.DeferredShading;
		protected override CameraEvent CameraEvent => CameraEvent.AfterGBuffer;
		protected override string CommandName => "Decal test";

		/*protected override void Enable()
		{
			decalRenderer.enabled = true;
		}*/

		/*protected override void Disable()
		{
			decalRenderer.enabled = false;
		}*/

		public override void Update()
		{
			//Graphics.DrawMesh(((SkinnedMeshRenderer)decalRenderer).sharedMesh, renderer.transform.localToWorldMatrix, material, 0, this.Camera.Camera);
		}
	}

	public class ScreenSpaceInstance : CommandInstance
	{
		protected override RenderingPath RenderingPath => RenderingPath.UsePlayerSettings;

		protected override CameraEvent CameraEvent => CameraEvent.BeforeLighting;

		protected override string CommandName => "Screen space decal";

		protected override string ShaderName => "Decal/ScreenSpace";

		private readonly Matrix4x4 matrix;

		public ScreenSpaceInstance(DecalInstance parent, DecalCamera camera) : base(parent, camera, -1)
		{
			camera.Camera.depthTextureMode = DepthTextureMode.Depth;
			matrix = parent.Object.transform.localToWorldMatrix * parent.ObjectToProjector.inverse;
			cmd.DrawMesh(DecalManager.Current.CubeMesh, matrix, material);
		}

		public override void OnPreRender()
		{
			var w = Camera.transform.position;
			var localCameraPos = matrix.inverse*new Vector4(w.x, w.y, w.z, 1);
			material.SetVector("_LocalCameraPos", localCameraPos);
			if(materials != null)
				foreach(var m in materials)
					m.SetVector("_LocalCameraPos", localCameraPos);
		}
	}

	public abstract class StaticInstance : CommandInstance
	{
		protected override string ShaderName => "Decal/Static";

		protected override string CommandName => "Static decal";

		protected StaticInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
			material.SetMatrix("_Object2Projector",
				parent.GetProjectionMatrix(parent.Object.Renderers.IndexOf(renderer)));
		}
	}

	public class DeferredStaticInstance : StaticInstance
	{
		protected override RenderingPath RenderingPath => RenderingPath.DeferredShading;

		protected override CameraEvent CameraEvent => CameraEvent.BeforeLighting;

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
		protected override RenderingPath RenderingPath => RenderingPath.Forward;

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

	public class UvSkinnedInstance : CommandInstance
	{
		protected override string ShaderName => "Decal/Skinned";

		protected override RenderingPath RenderingPath => RenderingPath.UsePlayerSettings;
		protected override CameraEvent CameraEvent => CameraEvent.AfterGBuffer;
		protected override string CommandName => "Skinned decal UV";

		public UvSkinnedInstance(DecalInstance parent, DecalCamera camera, int rendererIndex,
			int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
			var smr = (SkinnedMeshRenderer) renderer;
			var mesh = smr.sharedMesh;
			mesh.uv2 = parent.GetUvData(rendererIndex);
			mesh.UploadMeshData(false);
			cmd.DrawRenderer(renderer, material, 0);
		}
	}

	public abstract class SkinnedInstance : CommandInstance
	{
		private struct SkinnedData
		{
			public ComputeBuffer buffer1, buffer2;
		}
		
		private static Material CreateSkinnedData(Vector2[] uvData,
			Material baseMaterial, out SkinnedData skinnedData)
		{
			skinnedData = default(SkinnedData);

			// Cut off space at beginning and end
			var minUv = 0;
			try
			{
				while (float.IsInfinity(uvData[minUv].x))
					minUv++;
			}
			catch (IndexOutOfRangeException)
			{
				Debug.LogWarning("No triangles intersect projection box");
				return null;
			}
			var maxUv = uvData.Length;
			while ((float.IsInfinity(uvData[maxUv - 1].x)))
				maxUv--;

			// Find longest string of empty space in the remainder
			int maxSpaceStart = -1, maxSpaceLength = -1;
			int spaceStart = -1, spaceLength = -1;
			for (int i = minUv; i < maxUv; i++)
			{
				if (float.IsInfinity(uvData[i].x))
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

			// Offset the data by an arbitrary about to make out of range values
			// not between 1 and 0
			var uvOffset = new Vector2(4, 4);

			// Create the two buffer arrays
			var data1 = new Vector2[maxSpaceStart - minUv];
			for (int i = 0; i < data1.Length; i++)
				data1[i] = float.IsInfinity(uvData[i + minUv].x) ? Vector2.zero : (uvData[i + minUv] + uvOffset);
			var data2Offset = (maxSpaceStart + maxSpaceLength);
			var data2 = new Vector2[maxUv - data2Offset];
			for (int i = 0; i < data2.Length; i++)
				data2[i] = float.IsInfinity(uvData[i + data2Offset].x) ? Vector2.zero : (uvData[i + data2Offset] + uvOffset);

			const int vector3Size = sizeof(float) * 2;
			var buffer1 = new ComputeBuffer(data1.Length, vector3Size);
			var buffer2 = new ComputeBuffer(data2.Length, vector3Size);
			buffer1.SetData(data1);
			buffer2.SetData(data2);

			var mat = UnityEngine.Object.Instantiate(baseMaterial);
			mat.SetBuffer("_Buffer1", buffer1);
			mat.SetBuffer("_Buffer2", buffer2);
			mat.SetInt("_Offset1", minUv);
			mat.SetInt("_Offset2", data2Offset);

			skinnedData = new SkinnedData { buffer1 = buffer1, buffer2 = buffer2 };
			return mat;
		}

		protected override string ShaderName => "Decal/Skinned";

		protected override string CommandName => "Skinned decal";

		private static readonly Dictionary<Material, SkinnedData> skinnedData
			= new Dictionary<Material, SkinnedData>();
		
		protected virtual Material ModifySubmeshMaterial(Material baseMaterial, int submesh)
		{
			return baseMaterial;
		}

		protected void ProjectSkinned(SkinnedMeshRenderer renderer, int index)
		{
			var matList = new List<Material>();
			var count = renderer.sharedMesh.subMeshCount;
			for (int i = 0; i < count; i++)
			{
				if((submeshMask & 1 << i) != 0)
					continue;
				Profiler.BeginSample("Submesh " + i);
				SkinnedData data;
				var mat = CreateSkinnedData(Parent.GetUvData(index), material, out data);
				if (mat != null)
				{
					mat = ModifySubmeshMaterial(mat, i);
					skinnedData.Add(mat, data);
					matList.Add(mat);
					cmd.DrawRenderer(renderer, mat, i);
				}
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
		protected override RenderingPath RenderingPath => RenderingPath.DeferredShading;

		protected override CameraEvent CameraEvent => CameraEvent.AfterGBuffer;

		//protected Mesh mesh = new Mesh();

		/*protected override void Enable()
		{
			
		}

		public override void Update()
		{
			((SkinnedMeshRenderer) renderer).BakeMesh(mesh);
			Graphics.DrawMesh(mesh, renderer.transform.localToWorldMatrix, material, 0, this.Camera.Camera);
		}*/

		public DeferredSkinnedInstance(DecalInstance parent, DecalCamera camera,
			int rendererIndex, int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
		}
	}

	public class ForwardSkinnedInstance : SkinnedInstance
	{
		protected override RenderingPath RenderingPath => RenderingPath.Forward;

		protected override CameraEvent CameraEvent => CameraEvent.AfterForwardOpaque;
		
		/*protected override Material ModifySubmeshMaterial(Material baseMaterial, int submesh)
		{
			
			baseMaterial.SetTexture("_BodyAlbedo", sharedMaterials[submesh].mainTexture);
			return baseMaterial;
		}*/

		public ForwardSkinnedInstance(DecalInstance parent, DecalCamera camera,
			int rendererIndex, int submeshMask = 0) : base(parent, camera, rendererIndex, submeshMask)
		{
		}
	}
}
