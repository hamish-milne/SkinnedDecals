using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;
using static DecalSystem.ShaderKeywords;

namespace DecalSystem
{
	[Flags]
	public enum UvChannel
	{
		Uv1A = 0x1,
		Uv1B = 0x2,
		Uv2A = 0x4,
		Uv2B = 0x8,
		Uv3A = 0x10,
		Uv3B = 0x20,
		Uv4A = 0x40,
		Uv4B = 0x80,
	}

	[RequireComponent(typeof(SkinnedMeshRenderer))]
	public class DecalSkinnedObject : DecalObjectBase
	{
		private static readonly string[] modes = { SkinnedBuffer, SkinnedUv };

		public override string[] RequiredModes => modes;

		[SerializeField]
		protected UvChannel uvChannels = ~UvChannel.Uv1A;

		[SerializeField]
		protected List<DecalChannel> channels = new List<DecalChannel>();

		private SkinnedMeshRenderer skinnedRenderer;
		private int[][] triangleCache;
		private Vector2[] uvBuffer;

		public override Renderer Renderer => SkinnedRenderer;

		public SkinnedMeshRenderer SkinnedRenderer =>
			skinnedRenderer != null ? skinnedRenderer : (skinnedRenderer = GetComponent<SkinnedMeshRenderer>());

		public override bool UseManualCulling => true;

		public override bool ScreenSpace => false;

		[NonSerialized]
		private SkinnedMeshRenderer decalRenderer;

		[SerializeField] protected Mesh mergedMesh;

		[SerializeField] protected bool useBuffer;

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

		protected const string DecalRendererName = "__DECAL__";

		protected int[] GetTriangles(int submesh)
		{
			if(triangleCache == null)
				triangleCache = new int[Mesh.subMeshCount][];
			var tris = triangleCache[submesh];
			if (tris == null)
				triangleCache[submesh] = tris = Mesh.GetTriangles(submesh);
			return tris;
		}

		protected bool TryUseCommandBuffer =>
			SkinnedRenderer.sharedMesh.subMeshCount > 1 &&
			!DecalManager.Current.RequiresRenderingPath(RenderingPath.Forward) &&
			useBuffer;

		public bool UseCommandBuffer =>
			DecalRenderer == SkinnedRenderer &&
			TryUseCommandBuffer;

		public SkinnedMeshRenderer DecalRenderer
		{
			get
			{
				if (decalRenderer != null)
					return decalRenderer;
				// If this renderer has only one submesh, we can add decal commands to the material list
				if (SkinnedRenderer.sharedMesh.subMeshCount <= 1 ||
					// Otherwise, we'll need to make a new renderer if the forward path is being used,
					// OR buffers aren't supported
					(!DecalManager.Current.RequiresRenderingPath(RenderingPath.Forward) && useBuffer))
				{
					decalRenderer = SkinnedRenderer;
				}
				else
				{
					// First try to find an existing decal renderer...
					var rTransform = SkinnedRenderer.transform.Find(DecalRendererName);
					if (rTransform == null)
					{
						var newObj = new GameObject(DecalRendererName) {hideFlags = HideFlags.HideAndDontSave};
						rTransform = newObj.transform;
						rTransform.parent = SkinnedRenderer.transform;
					}
					decalRenderer = rTransform.GetComponent<SkinnedMeshRenderer>();
					// Failing that, make a new one
					if (decalRenderer == null)
					{
						decalRenderer = rTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
						// Merge submeshes into one, allowing materials to just be put into one big list
						if (mergedMesh == null)
						{
							var oldMesh = SkinnedRenderer.sharedMesh;
							mergedMesh = Instantiate(oldMesh);
							mergedMesh.subMeshCount = 1;
							mergedMesh.SetTriangles(oldMesh.triangles, 0);
						}
						decalRenderer.sharedMesh = mergedMesh;
						decalRenderer.sharedMaterials = new Material[0];
						// In theory, the following can change at any time, so TODO: make sure these stay updated
						decalRenderer.bones = SkinnedRenderer.bones;
						decalRenderer.rootBone = SkinnedRenderer.rootBone;
						decalRenderer.localBounds = SkinnedRenderer.localBounds;
						decalRenderer.quality = SkinnedRenderer.quality;
						decalRenderer.updateWhenOffscreen = SkinnedRenderer.updateWhenOffscreen;
					}
				}
				return decalRenderer;
			}
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

		[Serializable]
		protected class DecalChannel : DecalInstance, IDisposable
		{
			public int submeshMask;
			public Material material;
			[SerializeField] protected DecalSkinnedObject obj;
			[SerializeField] protected Vector2[] array;
			[SerializeField] protected UvChannel channel;

			private List<Vector4> list;
			private ComputeBuffer buffer;

			public override DecalObject DecalObject => obj;

			public override bool HasMultipleDecals => true;

			public unsafe bool AttemptCopy(Vector2[] input, bool force)
			{
				fixed (Vector2* inPtr = input)
				fixed (Vector2* outPtr = array)
				{
					Vector2* inValue, outValue;
					if (!force)
					{
						inValue = inPtr + input.Length;
						outValue = outPtr + input.Length;
						do
						{
							inValue--;
							outValue--;
							if (float.IsInfinity((*inValue).x) || float.IsInfinity((*outValue).x)) continue;
							return false;
						} while (inValue > inPtr);
					}
					inValue = inPtr + input.Length;
					outValue = outPtr + input.Length;
					do
					{
						inValue--;
						outValue--;
						if (float.IsInfinity((*inValue).x)) continue;
						*outValue = *inValue;
					} while (inValue > inPtr);
				}
				return true;
			}

			void ApplyToMesh(Mesh mesh, int uv, bool useZw)
			{
				mesh.GetUVs(uv, list);
				if (useZw)
				{
					for (int i = 0; i < array.Length; i++)
					{
						var d = list[i];
						var r = array[i];
						list[i] = new Vector4(d.x, d.y, r.x, r.y);
					}
				}
				else
				{
					for (int i = 0; i < array.Length; i++)
					{
						var d = list[i];
						var r = array[i];
						list[i] = new Vector4(r.x, r.y, d.z, d.w);
					}
				}
				mesh.SetUVs(uv, list);
				mesh.UploadMeshData(false);
			}

			public void Apply(int submesh)
			{
				if (obj.useBuffer)
				{
					buffer.SetData(array);
				}
				else
				{
					int uvChannel, channelId;
					bool useZw;
					switch (channel)
					{
						case UvChannel.Uv1A:
							uvChannel = 0;
							channelId = 0;
							useZw = false;
							break;
						case UvChannel.Uv1B:
							uvChannel = 0;
							channelId = 1;
							useZw = true;
							break;
						case UvChannel.Uv2A:
							uvChannel = 1;
							channelId = 2;
							useZw = false;
							break;
						case UvChannel.Uv2B:
							uvChannel = 1;
							channelId = 3;
							useZw = true;
							break;
						case UvChannel.Uv3A:
							uvChannel = 2;
							channelId = 4;
							useZw = false;
							break;
						case UvChannel.Uv3B:
							uvChannel = 2;
							channelId = 5;
							useZw = true;
							break;
						case UvChannel.Uv4A:
							uvChannel = 3;
							channelId = 6;
							useZw = false;
							break;
						case UvChannel.Uv4B:
							uvChannel = 3;
							channelId = 7;
							useZw = true;
							break;
						default:
							throw new Exception("Invalid UV channel");
					}
					ApplyToMesh(obj.Mesh, uvChannel, useZw);
					material.SetInt(ShaderKeywords.UvChannel, channelId);
				}
				submeshMask |= (1 << submesh);
			}

			public void AddMaterial(Renderer renderer)
			{
				var mats = renderer.sharedMaterials;
				// Our material is unique, so make sure we only add it once
				if (mats.Contains(material))
					return;
				var oldLen = mats.Length;
				Array.Resize(ref mats, oldLen + 1);
				mats[oldLen] = material;
				renderer.sharedMaterials = mats;
			}

			public void BindBuffer()
			{
				if(buffer != null)
					material.SetBuffer(ShaderKeywords.Buffer, buffer);
			}

			public void Reload(int count)
			{
				if (array == null || array.Length != count)
				{
					array = new Vector2[count];
					for (int i = 0; i < count; i++)
						array[i] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
				}
				if (obj.useBuffer)
				{
					if(material == null)
						material = DecalMaterial.CreateMaterial(SkinnedBuffer);
					if (buffer == null || buffer.count != count)
					{
						buffer = new ComputeBuffer(count, Marshal.SizeOf(typeof (Vector2)));
						buffer.SetData(array);
					}
					BindBuffer();
				}
				else
				{
					if(material == null)
						material = DecalMaterial.CreateMaterial(SkinnedUv);
					list = new List<Vector4>(count);
				}
			}

			public DecalChannel(DecalSkinnedObject obj, DecalMaterial decal, int count, UvChannel channel = 0)
			{
				this.obj = obj;
				this.decal = decal;
				this.channel = channel;
				Reload(count);
			}

			// Default constructor to allow serialization
			public DecalChannel()
			{
			}

			public void Refresh()
			{
				DecalMaterial.CopyTo(material);
				DecalMaterial.SetKeywords(material);
			}

			public void Dispose()
			{
				buffer?.Dispose();
			}
		}

		public override void Refresh(RefreshAction action)
		{
			base.Refresh(action);
			if((action & (RefreshAction.MaterialPropertiesChanged
				| RefreshAction.ChangeInstanceMaterial)) != 0)
				foreach(var obj in channels)
					obj.Refresh();
			// If the camera rendering path changed, check again if we need a separate decal object
			if ((action & RefreshAction.CamerasChanged) != 0)
			{
				if (TryUseCommandBuffer && decalRenderer != SkinnedRenderer)
					Destroy(decalRenderer.gameObject);
				decalRenderer = null;
				if (!UseCommandBuffer)
				{
					foreach (var obj in channels)
						obj.AddMaterial(DecalRenderer);
					ClearData();
				}
			}
		}

		protected DecalChannel CreateChannel(DecalMaterial decal, int submesh)
		{
			bool decalUseBuffers = decal.IsModeSupported(SkinnedBuffer);
			if (channels.Count == 0)
				useBuffer = decalUseBuffers;
			else if (useBuffer != decalUseBuffers)
			{
				Debug.LogError("DecalSkinnedObject: Mismatch in buffer support! " + decal, this);
				return null;
			}
			Profiler.BeginSample("Create channel");
			DecalChannel ret = null;
			if (useBuffer)
			{
				ret = new DecalChannel(this, decal, Mesh.vertexCount);
			}
			else
			{
				const int numUvChannels = 8;
				for (int i = 0; i < numUvChannels; i++)
				{
					var c = (UvChannel)(1 << i);
					if ((uvChannels & c) != 0 || DecalRenderer != SkinnedRenderer)
					{
						ret = new DecalChannel(this, decal, Mesh.vertexCount, c);
						break;
					}
				}
			}
			if (ret != null)
			{
				channels.Add(ret);
				ret.Refresh();
				if(!UseCommandBuffer)
					ret.AddMaterial(DecalRenderer);
			}
			Profiler.EndSample();
			return ret;
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
			foreach (var c in channels)
			{
				if (c.DecalMaterial != decal)
					continue;
				if (c.AttemptCopy(uvBuffer, false))
				{
					c.Apply(submesh);
					Profiler.EndSample();
					return c;
				}
			}
			var newC = CreateChannel(decal, submesh);
			if (newC != null)
			{
				newC.AttemptCopy(uvBuffer, true);
				newC.Apply(submesh);
			}
			Profiler.EndSample();
			Profiler.EndSample();
			ClearData();
			return newC;
		}

		private const int deferredBasePass = 2;
		private const int deferredSmoothnessPass = 3;

		protected override void GetDeferredData(out MeshData[] meshData, out RendererData[] rendererData)
		{
			foreach (var c in channels)
				c.Reload(Mesh.vertexCount);
			meshData = null;
			rendererData = null;
			if (UseCommandBuffer)
				rendererData = channels
					.Where(c => c.Enabled)
					.SelectMany(c => Enumerable.Range(0, Mesh.subMeshCount)
						.Where(i => (c.submeshMask & (1 << i)) != 0)
						.SelectMany(i =>
						{
							var rd1 = new RendererData
							{
								renderer = SkinnedRenderer,
								material = c.material,
								submesh = i,
								pass = deferredBasePass
							};
							var rd2 = rd1;
							rd2.pass = deferredSmoothnessPass;
							return new[] {rd1, rd2};
						})).ToArray();
		}


		protected override void GetForwardData(out MeshData[] meshData, out RendererData[] rendererData)
		{
			meshData = null;
			rendererData = null;
		}

		protected virtual void OnDestroy()
		{
			if(decalRenderer != SkinnedRenderer && decalRenderer != null)
				Destroy(decalRenderer.gameObject);
			foreach (var c in channels)
			{
				c.Dispose();
			}
		}

		protected virtual void LateUpdate()
		{
			bakeRequired = true;
			// Rebind buffers every frame - workaround for a 'feature' in unity where they sometimes become unbound
			foreach (var c in channels)
				c.BindBuffer();
		}

		protected virtual void OnWillRenderObject()
		{
			DecalManager.Current.RenderObject(this, Camera.current);
		}
	}
}
