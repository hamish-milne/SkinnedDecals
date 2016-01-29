﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

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
		[SerializeField]
		protected UvChannel uvChannels = ~UvChannel.Uv1A;

		[SerializeField]
		protected List<DecalChannel> channels = new List<DecalChannel>();

		private int[][] triangleCache;
		private Vector2[] uvBuffer;

		public SkinnedMeshRenderer Renderer { get; private set; }

		public override bool UseManualCulling => true;

		public override bool ScreenSpace => false;

		[NonSerialized]
		private SkinnedMeshRenderer decalRenderer;

		[SerializeField] protected Mesh mergedMesh;

		protected const string decalRendererName = "__DECAL__";

		public static bool Sm4Supported => DecalMaterialStandard.Sm4Supported;

		protected bool TryUseCommandBuffer =>
			Renderer.sharedMesh.subMeshCount > 1 &&
			!DecalManager.Current.RequiresRenderingPath(RenderingPath.Forward) &&
			Sm4Supported;

		public bool UseCommandBuffer =>
			DecalRenderer == Renderer &&
			TryUseCommandBuffer;

		public SkinnedMeshRenderer DecalRenderer
		{
			get
			{
				if (decalRenderer != null)
					return decalRenderer;
				// If this renderer has only one submesh, we can add decal commands to the material list
				if (Renderer.sharedMesh.subMeshCount <= 1 ||
					// Otherwise, we'll need to make a new renderer if the forward path is being used,
					// OR SM4 isn't supported
					(!DecalManager.Current.RequiresRenderingPath(RenderingPath.Forward) && Sm4Supported))
				{
					decalRenderer = Renderer;
				}
				else
				{
					// First try to find an existing decal renderer...
					var rTransform = Renderer.transform.Find(decalRendererName);
					if (rTransform == null)
					{
						var newObj = new GameObject(decalRendererName) {hideFlags = HideFlags.HideAndDontSave};
						rTransform = newObj.transform;
						rTransform.parent = Renderer.transform;
					}
					decalRenderer = rTransform.GetComponent<SkinnedMeshRenderer>();
					// Failing that, make a new one
					if (decalRenderer == null)
					{
						decalRenderer = rTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
						// Merge submeshes into one, allowing materials to just be put into one big list
						if (mergedMesh == null)
						{
							var oldMesh = Renderer.sharedMesh;
							mergedMesh = Instantiate(oldMesh);
							mergedMesh.subMeshCount = 1;
							mergedMesh.SetTriangles(oldMesh.triangles, 0);
						}
						decalRenderer.sharedMesh = mergedMesh;
						decalRenderer.sharedMaterials = new Material[0];
						// In theory, the following can change at any time, so TODO: make sure these stay updated
						decalRenderer.bones = Renderer.bones;
						decalRenderer.rootBone = Renderer.rootBone;
						decalRenderer.localBounds = Renderer.localBounds;
						decalRenderer.quality = Renderer.quality;
						decalRenderer.updateWhenOffscreen = Renderer.updateWhenOffscreen;
					}
				}
				return decalRenderer;
			}
		}

		public override Bounds Bounds => Renderer.bounds;

		public override Mesh Mesh => (decalRenderer ?? Renderer).sharedMesh;

		public override Renderer CreateRenderer(GameObject target, Mesh mesh)
		{
			var ret = target.AddComponent<SkinnedMeshRenderer>();
			ret.sharedMesh = mesh;
			ret.bones = Renderer.bones;
			ret.rootBone = Renderer.rootBone;
			ret.updateWhenOffscreen = Renderer.updateWhenOffscreen;
			ret.localBounds = Renderer.localBounds;
			ret.quality = Renderer.quality;
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
				if (Sm4Supported)
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
					material.SetInt("_UvChannel", channelId);
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
					material.SetBuffer("_Buffer", buffer);
			}

			public void Reload(int count)
			{
				if (array == null || array.Length != count)
				{
					array = new Vector2[count];
					for (int i = 0; i < count; i++)
						array[i] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
				}
				if (Sm4Supported)
				{
					if(material == null)
						material = Instantiate(DecalMaterial.GetMaterial("_SKINNEDBUFFER"));
					if(buffer == null || buffer.count != count)
						buffer = new ComputeBuffer(count, Marshal.SizeOf(typeof(Vector2)));
					BindBuffer();
				}
				else
				{
					if(material == null)
						material = Instantiate(DecalMaterial.GetMaterial("_SKINNEDUV"));
					list = new List<Vector4>(count);
				}
				DecalMaterial.CopyTo(material);
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

		protected virtual void Awake()
		{
			Renderer = GetComponent<SkinnedMeshRenderer>();
			triangleCache = new int[Mesh.subMeshCount][];
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
				if (TryUseCommandBuffer && decalRenderer != Renderer)
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
			DecalChannel ret = null;
			if (Sm4Supported)
			{
				ret = new DecalChannel(this, decal, Mesh.vertexCount);
			}
			else
			{
				const int numUvChannels = 8;
				for (int i = 0; i < numUvChannels; i++)
				{
					var c = (UvChannel)(1 << i);
					if ((uvChannels & c) != 0 || DecalRenderer != Renderer)
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
			return ret;
		}

		public override DecalInstance AddDecal(Transform projector, DecalMaterial decal, int submesh)
		{
			base.AddDecal(projector, decal, submesh);
			var mesh = new Mesh();
			Renderer.BakeMesh(mesh);
			var verts = mesh.vertices;
			if (uvBuffer == null)
				uvBuffer = new Vector2[verts.Length];
			ProjectionUtility.TransformVerts(verts, transform, projector);
			var tris = triangleCache[submesh];
			if (tris == null)
				triangleCache[submesh] = tris = mesh.GetTriangles(submesh);
			if (!ProjectionUtility.Project(tris, verts, uvBuffer))
				return null;
			foreach (var c in channels)
			{
				if (c.DecalMaterial != decal)
					continue;
				if (c.AttemptCopy(uvBuffer, false))
				{
					c.Apply(submesh);
					return c;
				}
			}
			var newC = CreateChannel(decal, submesh);
			newC.AttemptCopy(uvBuffer, true);
			newC.Apply(submesh);
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
								renderer = Renderer,
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
			if(decalRenderer != Renderer && decalRenderer != null)
				Destroy(decalRenderer.gameObject);
			foreach (var c in channels)
			{
				c.Dispose();
			}
		}

		protected virtual void Update()
		{
			// Rebind buffers every frame - workaround for a b_u_g where they sometimes become unbound
			foreach(var c in channels)
				c.BindBuffer();
		}

		protected virtual void OnWillRenderObject()
		{
			DecalManager.Current.RenderObject(this, Camera.current);
		}
	}
}