using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkinnedDecals.Internal
{
	public abstract class BaseInstance : DecalCameraInstance
	{
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

		protected BaseInstance(DecalInstance parent, DecalCamera camera, Renderer renderer, int submeshMask = 0)
			: base(parent, camera)
		{
			decal = Parent.Projector.Decal;
			this.renderer = renderer;
			this.submeshMask = submeshMask;
			// ReSharper disable once VirtualMemberCallInContructor
			cmd = new CommandBuffer {name = $"{CommandName} ({renderer.name}, {decal.name})"};
			// ReSharper disable once VirtualMemberCallInContructor
			material = new Material(Shader.Find(ShaderName));
			material.mainTexture = decal.Albedo;
			material.SetTextureKeyword("_BumpMap", "_NORMALMAP", decal.Normal);
			material.SetTextureKeyword("_MetallicGlossMap", "_METALLICGLOSSMAP", decal.Roughness);
			sharedMaterials = renderer.sharedMaterials;
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

		public RenderObjectInstance(DecalInstance parent, DecalCamera camera, Renderer renderer, int submeshMask = 0)
			: base(parent, camera, renderer, submeshMask)
		{
			var projector = parent.Projector.transform;
			Vector4[] planes;
			Vector3[] verts;
			var mesh = renderer.GetMesh();
			ProjectionUtility.StartProjection(renderer, mesh, projector, out planes, out verts);
			var uvData = new Vector3[verts.Length];
			var intersectTris = new List<int>();
			if (submeshMask == 0)
			{
				ProjectionUtility.BakeUvData(mesh.triangles, verts, planes, projector, uvData, intersectTris);
			}
			else
			{
				bool cleared = false;
				for (int i = 0; i < mesh.subMeshCount; i++)
				{
					if((submeshMask & 1 << i) != 0)
						continue;
					ProjectionUtility.BakeUvData(mesh.GetTriangles(i), verts, planes, projector, uvData, intersectTris, cleared);
					cleared = true;
				}
			}
			var vertMap = new Dictionary<int, int>();
			var newUvs = new List<Vector2>();
			var newVerts = new List<Vector3>();
			var newTris = new int[intersectTris.Count];
			for (int i = 0; i < intersectTris.Count; i += 3)
			{
				for (int j = 0; j < 3; j++)
				{
					var t = intersectTris[i + j];
					int vertId;
					if (!vertMap.TryGetValue(t, out vertId))
					{
						vertId = newUvs.Count;
						newUvs.Add(uvData[i + j]);
						newVerts.Add(verts[i + j]);
					}
					newTris[i + j] = vertId;
				}
			}
			var skinned = renderer as SkinnedMeshRenderer;
			// TODO: Blendshapes
			if(skinned)
				mesh.Clear();
			else
				mesh = new Mesh();
			mesh.subMeshCount = 1;
			mesh.SetVertices(newVerts);
			mesh.SetTriangles(newTris, 0);
			mesh.SetUVs(0, newUvs);
			mesh.UploadMeshData(true);
		}
	}

	public class ScreenSpaceInstance : BaseInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterForwardOpaque;

		protected override string CommandName => "Screen space decal";

		protected override string ShaderName => "Decal/ScreenSpace";

		public ScreenSpaceInstance(DecalInstance parent, DecalCamera camera, Renderer renderer, Mesh cubeMesh)
			: base(parent, camera, renderer)
		{
			camera.Camera.depthTextureMode = DepthTextureMode.Depth;
			cmd.DrawMesh(cubeMesh, Parent.Projector.transform.localToWorldMatrix, material);
		}
	}

	public abstract class StaticInstance : BaseInstance
	{
		protected override string CommandName => "Static decal";

		protected StaticInstance(DecalInstance parent, DecalCamera camera, Renderer renderer,
			int submeshMask = 0) : base(parent, camera, renderer, submeshMask)
		{
			var projectorMatrix = Parent.Projector.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
			material.SetMatrix("_ProjectorMatrix", projectorMatrix);
		}
	}

	public class DeferredStaticInstance : StaticInstance
	{
		protected override CameraEvent CameraEvent => CameraEvent.AfterGBuffer;

		public DeferredStaticInstance(DecalInstance parent, DecalCamera camera, Renderer renderer,
			int submeshMask = 0) : base(parent, camera, renderer, submeshMask)
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

		public ForwardStaticInstance(DecalInstance parent, DecalCamera camera, Renderer renderer, 
			int submeshMask = 0) : base(parent, camera, renderer, submeshMask)
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
		protected override string CommandName => "Skinned decal";

		private static readonly Dictionary<Material, SkinnedData> skinnedData
			= new Dictionary<Material, SkinnedData>();

		protected virtual Material ProjectSubmesh(Mesh mesh, Transform projector,
			Vector3[] verts, Vector3[] uvData, Vector4[] planes, int submesh)
		{
			var tris = mesh.GetTriangles(submesh);
			ProjectionUtility.BakeUvData(tris, verts, planes, projector, uvData);
			SkinnedData data;
			var mat = ProjectionUtility.CreateSkinnedData(uvData, material, out data);
			skinnedData.Add(mat, data);
			return mat;
		}

		protected void ProjectSkinned(SkinnedMeshRenderer renderer, Transform projector)
		{
			var mesh = new Mesh();
			renderer.BakeMesh(mesh);

			Vector4[] planes;
			Vector3[] verts;
			ProjectionUtility.StartProjection(renderer, mesh, projector, out planes, out verts);
			var uvData = new Vector3[verts.Length];

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

		protected SkinnedInstance(DecalInstance parent, DecalCamera camera, SkinnedMeshRenderer renderer,
			int submeshMask = 0) : base(parent, camera, renderer, submeshMask)
		{
			ProjectSkinned(renderer, Parent.Projector.transform);
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

		public DeferredSkinnedInstance(DecalInstance parent, DecalCamera camera, SkinnedMeshRenderer renderer,
			int submeshMask = 0) : base(parent, camera, renderer, submeshMask)
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

		public ForwardSkinnedInstance(DecalInstance parent, DecalCamera camera, SkinnedMeshRenderer renderer,
			int submeshMask = 0) : base(parent, camera, renderer, submeshMask)
		{
		}
	}
}
