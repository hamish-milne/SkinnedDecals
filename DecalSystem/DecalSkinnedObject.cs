using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using static DecalSystem.ShaderKeywords;

namespace DecalSystem
{
	[RequireComponent(typeof(SkinnedMeshRenderer))]
	[RendererType(typeof(SkinnedMeshRenderer))]
	public class DecalSkinnedObject : DecalObjectBase
	{

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

		[NonSerialized] private SkinnedMeshRenderer skinnedRenderer;
		[NonSerialized] private int[][] triangleCache;
		[NonSerialized] private Vector2[] uvBuffer;

		public override Renderer Renderer => SkinnedRenderer;

		public SkinnedMeshRenderer SkinnedRenderer =>
			skinnedRenderer != null ? skinnedRenderer : (skinnedRenderer = GetComponent<SkinnedMeshRenderer>());

		public override bool ScreenSpace => false;

		private Mesh mergedMesh;

		private bool doClearChannels;

		[SerializeField]
		protected List<SkinnedInstance> instances = new List<SkinnedInstance>();

		protected readonly List<SkinnedChannel> channels = new List<SkinnedChannel>();

		[Serializable]
		protected class SkinnedInstance : DecalInstance
		{
			[NonSerialized] public DecalSkinnedObject obj;

			public Matrix4x4 initialMatrix = Matrix4x4.identity;
			[HideInInspector] public int uvDataStart;
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
					RemoveFromChannel();
					obj.AddInstance(this);
				}
			}

			public void RemoveFromChannel()
			{
				channel?.Remove(this);
				channel = null;
			}

			public override bool Enabled
			{
				get { return base.Enabled; }
				set
				{
					if (base.Enabled == value) return;
					base.Enabled = value;
					if (value)
						obj.AddInstance(this);
					else
						RemoveFromChannel();
				}
			}

			public SkinnedInstance() { }

			public SkinnedInstance(DecalSkinnedObject obj, DecalMaterial decalMaterial, Vector2[] uvData, int uvDataStart, Matrix4x4 initialMatrix)
			{
				this.obj = obj;
				this.decalMaterial = decalMaterial;
				this.uvData = uvData;
				this.uvDataStart = uvDataStart;
				this.initialMatrix = initialMatrix;
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
					if (float.IsInfinity(instance.uvData[j].x))
						continue;
					uvData[i] = instance.uvData[j];
				}
				DecalMaterial = instance.DecalMaterial;
				Instances.Add(instance);
				return true;
			}

			public void Remove(SkinnedInstance instance)
			{
				if (Instances.Remove(instance))
				{
					// If this is the only instance, we can simply kill it
					if (Instances.Count == 0)
					{
						obj.channels.Remove(this);
						Dispose();
						obj.ClearData();
					}
					else
					{
						// Otherwise clear the parts of the buffer it occupies
						for (int i = instance.uvDataStart, j = 0; j < instance.uvData.Length && i < uvData.Length; i++, j++)
						{
							if (float.IsInfinity(uvData[i].x) || float.IsInfinity(instance.uvData[j].x))
								continue;
							uvData[i] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
						}
						Update();
					}
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
				for(int i = 0; i < uvData.Length; i++)
					uvData[i] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			}

			public abstract void Dispose();
		}

		protected class SkinnedBufferChannel : SkinnedChannel, IDecalDraw
		{
			private ComputeBuffer buffer;
			private Matrix4x4 singleMatrix;

			public bool Enabled => Instances.Count > 0;

			public override void Update()
			{
				if (buffer == null)
					buffer = new ComputeBuffer(uvData.Length, sizeof(float) * 2);
				buffer.SetData(uvData);
				if(Instances.Count == 1)
					singleMatrix = Instances[0].initialMatrix.inverse;
			}

			public virtual void GetDrawCommand(DecalCamera dcam, ref Mesh mesh, ref Renderer renderer,
				ref int submesh, ref Material material, ref Matrix4x4 matrix)
			{
				if (DecalMaterial == null) return;
				if (dcam.CanDrawRenderers(DecalMaterial))
				{
					renderer = obj.SkinnedRenderer;
					material = DecalMaterial.GetMaterial(SkinnedBuffer);
				}
				else
				{
					// Need to manually bake mesh - not efficient. Revert to UV method
					// This happens when a camera starts using the forward path
					mesh = obj.GetCurrentMesh();
					matrix = obj.transform.localToWorldMatrix;
					material = DecalMaterial.GetMaterial(SkinnedBuffer);
					obj.doClearChannels = true;
				}
			}

			public virtual void AddShaderProperties(IShaderProperties properties)
			{
				properties.Add(ShaderKeywords.Buffer, buffer);
				properties.Add(BDecalMatrix, singleMatrix); // Use a different property name when drawing a renderer
			}

			public SkinnedBufferChannel(DecalSkinnedObject obj) : base(obj) { }

			public override void Dispose()
			{
				Instances.Clear();
				buffer?.Dispose();
				buffer = null;
			}
		}

		protected class SkinnedMeshChannel : SkinnedChannel
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

			private SkinnedMeshRenderer decalRenderer;
			private readonly UvChannel uvChannel;
			private readonly Mesh decalMesh;
			private static readonly List<Vector4> uvList = new List<Vector4>();
			private Material material;
			private readonly MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();

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
				if (material == null)
					material = DecalMaterial.GetMaterial(SkinnedUv, ShaderKeywords.UvChannel, id);
				if (!mats.Contains(material))
					decalRenderer.sharedMaterials = mats.Where(m => m != null).Concat(new[] {material}).ToArray();
				if (Instances.Count == 1)
				{
					materialPropertyBlock.SetMatrix(ProjectorSingle, Instances[0].initialMatrix.inverse);
					materialPropertyBlock.SetMatrix(RealObjectToWorld, decalRenderer.localToWorldMatrix);
					materialPropertyBlock.SetMatrix(RealWorldToObject, decalRenderer.worldToLocalMatrix);
					decalRenderer.SetPropertyBlock(materialPropertyBlock);
				}
			}

			private static readonly HashSet<Pair<SkinnedMeshRenderer, UvChannel>> usedChannels
				= new HashSet<Pair<SkinnedMeshRenderer, UvChannel>>();

			public static SkinnedMeshRenderer[] GetDecalRenderers(Transform parent) => usedChannels.Select(p => p.First).Where(p => p != null && p.transform.parent == parent).ToArray();

			private static SkinnedMeshChannel TryCreate(DecalSkinnedObject obj, SkinnedMeshRenderer decalRenderer,
				UvChannel uvChannel)
			{
				if(obj == null) throw new ArgumentNullException(nameof(obj));
				if(decalRenderer == null) throw new ArgumentNullException(nameof(decalRenderer));
				if(usedChannels.Add(new Pair<SkinnedMeshRenderer, UvChannel>(decalRenderer, uvChannel)))
					return new SkinnedMeshChannel(obj, decalRenderer, decalRenderer.sharedMesh, uvChannel);
				return null;
			}

			public override void Dispose()
			{
				if (decalRenderer != null)
					decalRenderer.sharedMaterials = decalRenderer.sharedMaterials.Where(m => m != material).ToArray();
				usedChannels.Remove(new Pair<SkinnedMeshRenderer, UvChannel>(decalRenderer, uvChannel));
				decalRenderer = null;
			}

			public SkinnedMeshChannel(DecalSkinnedObject obj, SkinnedMeshRenderer decalRenderer, Mesh decalMesh, UvChannel uvChannel) : base(obj)
			{
				this.decalMesh = decalMesh;
				this.decalRenderer = decalRenderer;
				this.uvChannel = uvChannel;
			}

			public static SkinnedMeshChannel Create(DecalSkinnedObject obj, DecalMaterial material)
			{
				var skinnedRenderer = obj.SkinnedRenderer;
				var transform = obj.transform;

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
						var ret = TryCreate(obj, smr, c);
						if (ret != null) return ret;
					}
				}
				// Failing that, create a new renderer
				// Don't save these; they are re-created when needed and just clutter up the scene file
				var go = new GameObject(decalRendererName) { hideFlags = HideFlags.HideAndDontSave };
				go.transform.parent = transform;
				var dr = go.AddComponent<SkinnedMeshRenderer>();
				var mergedMesh = obj.GetMergedMesh();
				dr.sharedMesh = Instantiate(mergedMesh);
				// In theory, the following can change at any time, so TODO: make sure these stay updated
				dr.bones = skinnedRenderer.bones;
				dr.rootBone = skinnedRenderer.rootBone;
				dr.localBounds = skinnedRenderer.localBounds;
				dr.quality = skinnedRenderer.quality;
				dr.updateWhenOffscreen = skinnedRenderer.updateWhenOffscreen;
				var newC = TryCreate(obj, dr, UvChannel.Uv1A);
				if (newC == null) throw new Exception("SkinnedMeshChannel.TryCreate is null for a new renderer");
				return newC;
			}
		}

		protected Mesh GetMergedMesh()
		{
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
			return mergedMesh;
		}

		protected virtual SkinnedChannel CreateChannel(DecalMaterial material)
		{
			SkinnedChannel ret;
			if (material.IsModeSupported(SkinnedBuffer) && DecalManager.Current.CanDrawRenderers(material))
			{
				ret = new SkinnedBufferChannel(this);
			}
			else if (material.IsModeSupported(SkinnedUv))
			{
				ret = SkinnedMeshChannel.Create(this, material);
			}
			else
			{
				throw new Exception("No skinned modes supported for " + material);
			}
			channels.Add(ret);
			ClearData();
			return ret;
		}

		public void ClearChannels()
		{
			foreach (var o in instances)
				o.channel = null;
			foreach (var r in SkinnedMeshChannel.GetDecalRenderers(transform))
				DestroyImmediate(r.gameObject);
			foreach (var c in channels)
				c.Dispose();
			channels.Clear();
			ClearData();
		}

		protected virtual void AddInstance(SkinnedInstance inst)
		{
			if (!inst.Enabled || inst.DecalMaterial == null || inst.channel != null) return;
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

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh, float maxNormal)
		{
			base.AddDecal(projector, decal, submesh, maxNormal);
			Profiler.BeginSample("Add skinned decal");
			var mesh = GetCurrentMesh();
			var verts = mesh.vertices;
			var normals = mesh.normals; // TODO: Allow this to not be done
			if (uvBuffer == null)
				uvBuffer = new Vector2[verts.Length];
			Profiler.BeginSample("Projecting");
			ProjectionUtility.TransformVerts(verts, transform, projector);
			ProjectionUtility.TransformNormals(normals, transform, projector);
			var tris = GetTriangles(submesh);
			var pResult = ProjectionUtility.Project(tris, verts, normals, uvBuffer);
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

			
			var inst = new SkinnedInstance(this, decal, newUvBuffer, start, transform.worldToLocalMatrix * projector.localToWorldMatrix);
			AddInstance(inst);
			instances.Add(inst);
			Profiler.EndSample();
			Profiler.EndSample();
			return inst;
		}

		public override Bounds Bounds => SkinnedRenderer.bounds;

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
		}

		public override IDecalDraw[] GetDecalDraws()
		{
			if (doClearChannels)
			{
				ClearChannels();
				doClearChannels = false;
			}
			return base.GetDecalDraws();
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
			if (instance is SkinnedInstance inst && instances.Remove(inst))
			{
				inst.RemoveFromChannel();
				return true;
			}
			return false;
		}

		public override void UpdateBackRefs()
		{
			foreach (var o in instances)
				o.obj = this;
		}
	}
}
