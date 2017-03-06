using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using static DecalSystem.ShaderKeywords;

namespace DecalSystem
{
	public enum UvChannel
	{
		Uv1A,
		Uv1B,
		Uv2A,
		Uv2B,
		Uv3A,
		Uv3B,
		Uv4A,
		Uv4B,
	}

	[RequireComponent(typeof(SkinnedMeshRenderer))]
	[RendererType(typeof(SkinnedMeshRenderer))]
	public class DecalSkinnedObject : DecalObjectBase
	{
		private static readonly string[] modes = { SkinnedBuffer, SkinnedUv };

		public override string[] RequiredModes => modes;

		[SerializeField, UseProperty] protected bool allowMerge;

		public bool AllowMerge
		{
			get { return allowMerge; }
			set
			{
				if (allowMerge == value) return;
				if(!value)
					ClearChannels();
				allowMerge = value;
			}
		}

		private SkinnedMeshRenderer skinnedRenderer;
		[NonSerialized] private int[][] triangleCache;
		[NonSerialized] private Vector2[] uvBuffer;

		public override Renderer Renderer => SkinnedRenderer;

		public SkinnedMeshRenderer SkinnedRenderer =>
			skinnedRenderer != null ? skinnedRenderer : (skinnedRenderer = GetComponent<SkinnedMeshRenderer>());

		public override bool UseManualCulling => true;

		public override bool ScreenSpace => false;

		private Mesh mergedMesh;

		[SerializeField]
		protected List<SkinnedInstance> instances = new List<SkinnedInstance>();

		protected readonly List<SkinnedChannel> channels = new List<SkinnedChannel>();

		[Serializable]
		protected class SkinnedInstance : DecalInstance
		{
			[NonSerialized] public DecalSkinnedObject obj;

			public int uvDataStart;
			[HideInInspector] public Vector2[] uvData;
			public SkinnedChannel channel;

			public override DecalObject DecalObject => obj;

			public override DecalMaterial DecalMaterial
			{
				get { return base.DecalMaterial; }
				set
				{
					if (base.DecalMaterial == value) return;
					base.DecalMaterial = value;
					channel?.Remove(this);
				}
			}

			public override bool Enabled
			{
				get { return base.Enabled; }
				set
				{
					if (base.Enabled == value) return;
					if(value)
						obj.AddInstance(this);
					else
						channel?.Remove(this);
				}
			}

			public SkinnedInstance(DecalSkinnedObject obj, DecalMaterial decalMaterial, Vector2[] uvData, int uvDataStart)
			{
				this.obj = obj;
				this.decalMaterial = decalMaterial;
				this.uvData = uvData;
				this.uvDataStart = uvDataStart;
			}
		}

		private Mesh bakedMesh;
		private bool bakeRequired = true;

		public override Mesh GetCurrentMesh()
		{
			if (bakedMesh == null)
				bakedMesh = new Mesh();
			if (bakeRequired)
			{
				SkinnedRenderer.BakeMesh(bakedMesh);
				bakeRequired = false;
			}
			return bakedMesh;
		}

		protected int[] GetTriangles(int submesh)
		{
			if(triangleCache == null)
				triangleCache = new int[Mesh.subMeshCount][];
			var tris = triangleCache[submesh];
			if (tris == null)
				triangleCache[submesh] = tris = Mesh.GetTriangles(submesh);
			return tris;
		}

		protected abstract class SkinnedChannel : IDisposable
		{
			protected readonly Vector2[] uvData;

			public List<SkinnedInstance> Instances { get; } = new List<SkinnedInstance>();

			public bool Add(SkinnedInstance instance, bool force = false)
			{
				if (!force)
				{
					if (DecalMaterial != null && DecalMaterial != instance.DecalMaterial) return false;
					for (int i = instance.uvDataStart, j = 0; j < instance.uvData.Length && i < uvData.Length; i++, j++)
					{
						if (float.IsInfinity(uvData[i].x) || float.IsInfinity(instance.uvData[j].x))
							continue;
						return false;
					}
				}
				for (int i = instance.uvDataStart, j = 0; j < instance.uvData.Length && i < uvData.Length; i++, j++)
				{
					uvData[i] = instance.uvData[j];
				}
				DecalMaterial = instance.DecalMaterial;
				Instances.Add(instance);
				return true;
			}

			public void Remove(SkinnedInstance instance)
			{
				if(Instances.Remove(instance))
					for (int i = instance.uvDataStart, j = 0; j < instance.uvData.Length && i < uvData.Length; i++, j++)
					{
						uvData[i] = instance.uvData[j];
					}
			}

			public DecalObject DecalObject => obj;
			public DecalMaterial DecalMaterial { get; set; }

			protected DecalSkinnedObject obj;

			public abstract void Update();

			protected SkinnedChannel(DecalSkinnedObject obj)
			{
				this.obj = obj;
				uvData = new Vector2[obj.Mesh.vertexCount];
			}

			public abstract void Dispose();

			public abstract void BindBuffers();
		}

		protected class SkinnedBufferChannel : SkinnedChannel, IDecalDraw
		{
			private ComputeBuffer buffer;
			private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

			public bool Enabled => true;

			public override void Update()
			{
				if (buffer == null)
					buffer = new ComputeBuffer(uvData.Length, sizeof(float) * 2);
				//block.SetBuffer(ShaderKeywords.Buffer, buffer);
				buffer.SetData(uvData);
			}

			public virtual void GetDrawCommand(RenderingPath renderPath, ref Mesh mesh, ref Renderer renderer, ref int submesh, ref Material material, ref MaterialPropertyBlock propertyBlock, ref Matrix4x4 matrix, List<KeyValuePair<string, ComputeBuffer>> buffers)
			{
				propertyBlock = block;
				if (renderPath == RenderingPath.DeferredShading)
				{
					renderer = obj.SkinnedRenderer;
					material = DecalMaterial?.GetMaterial(SkinnedBuffer);
					buffers.Add(new KeyValuePair<string, ComputeBuffer>(ShaderKeywords.Buffer, buffer));
				}
				else
				{
					mesh = obj.GetCurrentMesh();
					material = DecalMaterial?.GetMaterial(SkinnedBuffer);
					block.SetBuffer(ShaderKeywords.Buffer, buffer);
				}
			}

			public override void BindBuffers()
			{
				// TODO: Test if this is needed now
				// block?.SetBuffer(ShaderKeywords.Buffer, buffer);
			}

			public SkinnedBufferChannel(DecalSkinnedObject obj) : base(obj) { }

			public override void Dispose()
			{
				buffer?.Dispose();
				buffer = null;
			}
		}

		protected class SkinnedMeshChannel : SkinnedChannel
		{

			private SkinnedMeshRenderer decalRenderer;
			private readonly UvChannel uvChannel;
			private readonly Mesh decalMesh;
			private static readonly List<Vector4> uvList = new List<Vector4>();

			public override void Update()
			{
				var useZw = ((int)uvChannel % 2) == 1;
				var id = (int) uvChannel;
				var set = id / 2;
				if(id < 0 || id >= 8) throw new Exception("Invalid UV channel");
				decalMesh.GetUVs(set, uvList);
				if (useZw)
				{
					for (int i = 0; i < uvData.Length; i++)
					{
						var d = uvList[i];
						var r = uvData[i];
						uvList[i] = new Vector4(d.x, d.y, r.x, r.y);
					}
				}
				else
				{
					for (int i = 0; i < uvData.Length; i++)
					{
						var d = uvList[i];
						var r = uvData[i];
						uvList[i] = new Vector4(r.x, r.y, d.z, d.w);
					}
				}
				decalMesh.SetUVs(set, uvList);
				decalMesh.UploadMeshData(false);
				var mats = decalRenderer.sharedMaterials;
				if (mats.Length < 8 || mats[id] == null)
				{
					Array.Resize(ref mats, 8);
					mats[id] = DecalMaterial?.GetMaterial(SkinnedUv, ShaderKeywords.UvChannel, id);
					decalRenderer.sharedMaterials = mats;
				}
			}

			public override void BindBuffers()
			{
				// None
			}

			private static readonly HashSet<KeyValuePair<SkinnedMeshRenderer, UvChannel>> usedChannels
				= new HashSet<KeyValuePair<SkinnedMeshRenderer, UvChannel>>();

			public static SkinnedMeshRenderer[] GetDecalRenderers() => usedChannels.Select(p => p.Key).ToArray();

			public static SkinnedMeshChannel TryCreate(DecalSkinnedObject obj, SkinnedMeshRenderer decalRenderer,
				UvChannel uvChannel)
			{
				if(obj == null) throw new ArgumentNullException(nameof(obj));
				if(decalRenderer == null) throw new ArgumentNullException(nameof(decalRenderer));
				if(usedChannels.Add(new KeyValuePair<SkinnedMeshRenderer, UvChannel>(decalRenderer, uvChannel)))
					return new SkinnedMeshChannel(obj, decalRenderer, decalRenderer.sharedMesh, uvChannel);
				return null;
			}

			public override void Dispose()
			{
				usedChannels.Remove(new KeyValuePair<SkinnedMeshRenderer, UvChannel>(decalRenderer, uvChannel));
				decalRenderer = null;
			}

			public SkinnedMeshChannel(DecalSkinnedObject obj, SkinnedMeshRenderer decalRenderer, Mesh decalMesh, UvChannel uvChannel) : base(obj)
			{
				this.decalMesh = decalMesh;
				this.decalRenderer = decalRenderer;
				this.uvChannel = uvChannel;
			}
		}

		protected virtual SkinnedChannel CreateChannel(DecalMaterial material)
		{
			if (material.IsModeSupported(SkinnedBuffer))
			{
				var ret = new SkinnedBufferChannel(this);
				channels.Add(ret);
				ClearData();
				return ret;
			}
			else if(material.IsModeSupported(SkinnedUv))
			{
				const string decalRendererName = "__DECAL__";
				// Try to find an existing renderer-channel pair that isn't used
				for (int i = 0; i < transform.childCount; i++)
				{
					var child = transform.GetChild(i);
					var smr = child.GetComponent<SkinnedMeshRenderer>();
					if (smr == null) continue;
					if (child.name != decalRendererName) continue;
					for (var c = UvChannel.Uv1A; c <= UvChannel.Uv4B; c++)
					{
						var ret = SkinnedMeshChannel.TryCreate(this, smr, c);
						if (ret != null) return ret;
					}
				}
				// Failing that, create a new renderer
				// Don't save these; they are re-created when needed and just clutter up the scene file
				var go = new GameObject(decalRendererName) {hideFlags = HideFlags.DontSave};
				go.transform.parent = transform;
				var dr = go.AddComponent<SkinnedMeshRenderer>();
				// Merge submeshes into one, allowing materials to just be put into one big list
				if (mergedMesh == null)
				{
					var oldMesh = SkinnedRenderer.sharedMesh;
					if (oldMesh.subMeshCount <= 1)
						mergedMesh = SkinnedRenderer.sharedMesh;
					else
					{
						mergedMesh = Instantiate(oldMesh);
						mergedMesh.subMeshCount = 1;
						mergedMesh.SetTriangles(oldMesh.triangles, 0);
					}
				}
				dr.sharedMesh = Instantiate(mergedMesh);
				// In theory, the following can change at any time, so TODO: make sure these stay updated
				dr.bones = SkinnedRenderer.bones;
				dr.rootBone = SkinnedRenderer.rootBone;
				dr.localBounds = SkinnedRenderer.localBounds;
				dr.quality = SkinnedRenderer.quality;
				dr.updateWhenOffscreen = SkinnedRenderer.updateWhenOffscreen;
				var newC = SkinnedMeshChannel.TryCreate(this, dr, UvChannel.Uv1A);
				if(newC == null) throw new Exception("TryCreate is null for a new renderer");
				channels.Add(newC);
				ClearData();
				return newC;
			}
			throw new Exception("No skinned modes supported for " + material);
		}

		public void ClearChannels()
		{
			foreach (var o in instances)
				o.channel = null;
			foreach (var r in SkinnedMeshChannel.GetDecalRenderers())
				DestroyImmediate(r.gameObject);
			foreach (var c in channels)
				c.Dispose();
			channels.Clear();
		}

		protected virtual void AddInstance(SkinnedInstance inst)
		{
			if (inst.DecalMaterial == null || inst.channel != null) return;
			var added = false;
			if(AllowMerge)
				foreach (var c in channels)
					if (c.Add(inst))
					{
						c.Update();
						inst.channel = c;
						added = true;
						break;
					}
			if (!added)
			{
				var newC = CreateChannel(inst.DecalMaterial);
				newC.Add(inst, true);
				newC.Update();
				inst.channel = newC;
			}
		}

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			Profiler.BeginSample("Add skinned decal");
			var mesh = GetCurrentMesh();
			var verts = mesh.vertices;
			if (uvBuffer == null)
				uvBuffer = new Vector2[verts.Length];
			Profiler.BeginSample("Projecting");
			ProjectionUtility.TransformVerts(verts, transform, projector);
			var tris = GetTriangles(submesh);
			var pResult = ProjectionUtility.Project(tris, verts, uvBuffer);
			Profiler.EndSample();
			if (!pResult)
				return null;
			Profiler.BeginSample("Copying");

			// Compress by cutting unused start and end portions
			int start = -1, end = uvBuffer.Length;
			while (float.IsInfinity(uvBuffer[++start].x)) { }
			while (float.IsInfinity(uvBuffer[--end].x)) { }
			var newUvBuffer = new Vector2[(end - start) + 1];
			for (int i = start; i <= end; i++)
				newUvBuffer[i - start] = uvBuffer[i];

			
			var inst = new SkinnedInstance(this, decal, newUvBuffer, start);
			AddInstance(inst);
			instances.Add(inst);
			Profiler.EndSample();
			Profiler.EndSample();
			return inst;
		}

		public override Bounds? Bounds => SkinnedRenderer.bounds;

		public override Mesh Mesh => SkinnedRenderer.sharedMesh;

		public override Renderer CreateRenderer(GameObject target, Mesh mesh)
		{
			var ret = target.AddComponent<SkinnedMeshRenderer>();
			ret.sharedMesh = mesh;
			ret.bones = SkinnedRenderer.bones;
			ret.rootBone = SkinnedRenderer.rootBone;
			ret.updateWhenOffscreen = SkinnedRenderer.updateWhenOffscreen;
			ret.localBounds = SkinnedRenderer.localBounds;
			ret.quality = SkinnedRenderer.quality;
			return ret;
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			ClearChannels();
		}

		protected virtual void LateUpdate()
		{
			bakeRequired = true;
			// Rebind buffers every frame - workaround for a 'feature' in unity where they sometimes become unbound
			foreach (var c in channels)
				c.BindBuffers();
		}

		protected virtual void OnWillRenderObject()
		{
			DecalManager.Current?.RenderObject(this, Camera.current);
		}

		public override IEnumerable<IDecalDraw> GetDrawsUncached()
		{
			foreach(var i in instances)
				AddInstance(i);
			return channels.OfType<IDecalDraw>();
		}

		public override int Count => instances.Count;
		public override DecalInstance GetDecal(int index)
		{
			return instances[index];
		}

		public override bool RemoveDecal(DecalInstance instance)
		{
			var inst = instance as SkinnedInstance;
			if (instances.Remove(inst))
			{
				inst?.channel?.Remove(inst);
				return true;
			}
			return false;
		}
	}
}
