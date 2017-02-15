using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DecalSystem
{
	/// <summary>
	/// Collects decal commands to render them, and provides other shared functionality
	/// </summary>
	[ExecuteInEditMode]
	public class DecalManager : MonoBehaviour
	{
		/// <summary>
		/// Stores data to render for a given camera
		/// </summary>
		[Serializable]
		protected class CameraData
		{
			/// <summary>
			/// The camera to render to
			/// </summary>
			public Camera camera;

			/// <summary>
			/// The command buffer to bind, if any
			/// </summary>
			public CommandBuffer command;

			/// <summary>
			/// The list of <c>DrawMesh</c> commands
			/// </summary>
			public readonly List<MeshData> meshData = new List<MeshData>();

			/// <summary>
			/// The list of meshes for which <c>DrawMesh</c> cannot be used
			/// </summary>
			public readonly List<MeshData> meshDataForBuffer = new List<MeshData>();
		}

		/// <summary>
		/// Whether to draw decals on the scene camera
		/// </summary>
		[SerializeField] protected bool renderSceneCamera;

		private CameraData[] cameraData;

		/// <summary>
		/// The currently enabled manager instance
		/// </summary>
		public static DecalManager Current { get; private set; }

		private static Camera sceneCamera;

		static DecalManager()
		{
			// Clear data when an object is enabled or disabled
			DecalObject.ObjectChangeState += (o, b) => Current?.ClearData();
			// Clear data when a decal is added
			DecalObject.DataChanged += o => Current?.ClearData();
		}

		/// <summary>
		/// The scene camera, if any
		/// </summary>
		public static Camera SceneCamera
		{
			get
			{
				if (Application.isEditor && sceneCamera == null)
					sceneCamera = Resources.FindObjectsOfTypeAll<Camera>()
						.FirstOrDefault(c => !c.enabled &&
						c.gameObject.activeInHierarchy && c.name == "SceneCamera");
				return sceneCamera;
			}
		}

		public static DecalManager GetOrCreate()
		{
			if(Current == null)
				Current = new GameObject("DecalManager").AddComponent<DecalManager>();
			return Current;
		}

		protected virtual void OnEnable()
		{
			if (Current != null && Current != this)
				Debug.LogWarning($"DecalSystem: Multiple managers active in scene ({Current}, {this})", this);
			Current = this;
		}

		protected virtual void OnDestroy()
		{
			if (Current == this)
				Current = null;
			ClearData();
		}

		// Holds whether a manually culled object is rendered for each camera
		private readonly HashSet<KeyValuePair<DecalObject, Camera>> toRender
			= new HashSet<KeyValuePair<DecalObject, Camera>>();   

		/// <summary>
		/// Notify the manager that the given object is visible to the given camera
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="renderCamera"></param>
		/// <remarks>
		/// This function only takes effect when <c>DecalObject.UseManualCulling</c> is <c>true</c>
		/// </remarks>
		public virtual void RenderObject(DecalObject obj, Camera renderCamera)
		{
			if(obj == null)
				throw new ArgumentNullException(nameof(obj));
			if(renderCamera == null)
				throw new ArgumentNullException(nameof(renderCamera));
			toRender.Add(new KeyValuePair<DecalObject, Camera>(obj, renderCamera));
		}

		/// <summary>
		/// Gets the command buffer event for the given rendering path
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		protected virtual CameraEvent GetCameraEvent(RenderingPath path)
		{
			switch (path)
			{
				case RenderingPath.Forward:
					return CameraEvent.AfterForwardOpaque;
				case RenderingPath.DeferredShading:
					return CameraEvent.BeforeReflections;
					//CameraEvent.AfterGBuffer;
				case RenderingPath.DeferredLighting:
					return CameraEvent.AfterFinalPass;
				case RenderingPath.VertexLit:
					return CameraEvent.BeforeImageEffects;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static readonly RenderTargetIdentifier[] gBuffer = new[]
		{
			BuiltinRenderTextureType.GBuffer0,
			BuiltinRenderTextureType.GBuffer1,
			BuiltinRenderTextureType.GBuffer2,
			BuiltinRenderTextureType.GBuffer3
		}.Select(b => new RenderTargetIdentifier(b)).ToArray();
		private static readonly RenderTargetIdentifier depth =
			new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive);

		protected virtual void SetupCommandBuffer(CameraData cd, CameraEvent evt, CommandBuffer cmd)
		{
			if (evt == CameraEvent.BeforeReflections)
			{
				cmd.SetRenderTarget(gBuffer, depth);
			}
		}

		/// <summary>
		/// Clears the cached camera data, rebuilding command lists the next frame
		/// </summary>
		public void ClearData()
		{
			if(cameraData != null)
				foreach (var cd in cameraData)
					cd.command?.Dispose();
			cameraData = null;
		}

		// Stores the previous camera setup, to detect changes
		private readonly Dictionary<Camera, RenderingPath> prevRenderingPaths
			= new Dictionary<Camera, RenderingPath>(); 
		private Camera[] cameraArray, prevCameraArray;

		private void ResetRenderingPaths()
		{
			prevRenderingPaths.Clear();
			foreach (var c in prevCameraArray)
				prevRenderingPaths[c] = c.actualRenderingPath;
		}

		/// <summary>
		/// Gets whether the given rendering path is being used currently
		/// </summary>
		/// <param name="path"></param>
		/// <returns><c>true</c> if it is being used, otherwise <c>false</c></returns>
		public virtual bool RequiresRenderingPath(RenderingPath path)
		{
			GetAllCameras();
			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach(var c in cameraArray)
				// The scene camera sometimes returns incorrect values for its rendering path
				if (c.actualRenderingPath == path && c != SceneCamera)
					return true;
			return false;
		}

		// Retrieves the camera list
		private void GetAllCameras()
		{
			if (SceneCamera != null)
			{
				if (cameraArray == null || cameraArray.Length != Camera.allCamerasCount+1)
					cameraArray = new Camera[Camera.allCamerasCount+1];
				Camera.GetAllCameras(cameraArray);
				cameraArray[Camera.allCamerasCount] = SceneCamera;
			}
			else
			{
				if (cameraArray == null || cameraArray.Length != Camera.allCamerasCount)
					cameraArray = Camera.allCameras;
				else
					Camera.GetAllCameras(cameraArray);
			}
		}
		
		/// <summary>
		/// Checks whether any cameras have changed, and refreshes objects accordingly
		/// </summary>
		/// <returns><c>true</c> if they have, otherwise <c>false</c></returns>
		protected bool CheckCameras()
		{
			GetAllCameras();
			if (prevCameraArray == null || prevCameraArray.Length != cameraArray.Length)
			{
				prevCameraArray = (Camera[])cameraArray.Clone();
				ResetRenderingPaths();
				DecalObject.RefreshAll(RefreshAction.CamerasChanged);
				return true;
			}
			for (int i = 0; i < cameraArray.Length; i++)
			{
				var prevPath = RenderingPath.UsePlayerSettings;
				if (prevCameraArray[i] != null && !prevRenderingPaths.TryGetValue(prevCameraArray[i], out prevPath))
					prevPath = RenderingPath.UsePlayerSettings;
				if (cameraArray[i] != prevCameraArray[i] ||
					 cameraArray[i].actualRenderingPath != prevPath)
				{
					cameraArray.CopyTo(prevCameraArray, 0);
					ResetRenderingPaths();
					DecalObject.RefreshAll(RefreshAction.CamerasChanged);
					return true;
				}
			}
				
			return (cameraData == null);
		}

		/// <summary>
		/// Gets a cached command buffer for the given <c>CameraData</c>, binding it
		/// according to its rendering path
		/// </summary>
		/// <param name="cd"></param>
		/// <returns></returns>
		protected CommandBuffer GetCommandBuffer(CameraData cd)
		{
			const string commandName = "Draw decals (DecalSystem)";
			if (cd.command == null)
			{
				var evt = GetCameraEvent(cd.camera.actualRenderingPath);
				var cmds = cd.camera.GetCommandBuffers(evt);
				foreach(var cmd in cmds)
					if (cmd.name == commandName)
						cd.command = cmd;
				if (cd.command == null)
				{
					cd.command = new CommandBuffer {name = commandName};
					cd.camera.AddCommandBuffer(evt, cd.command);
				}
				else
				{
					cd.command.Clear();
				}
			}
			return cd.command;
		}

		protected virtual bool CanUseDrawMesh(RenderingPath rp, MeshData md, RenderPathData rpd)
		{
			return !(rp == RenderingPath.DeferredShading && rpd.RequiresDepthTexture);
		}

		protected void RebuildCameraDataIfNeeded()
		{
			if (CheckCameras())
			{
				Debug.Log("Rebuilding camera data");
				var activeObjects = DecalObject.ActiveObjects;
				ClearData();
				var list = new List<CameraData>();
				foreach (var cam in cameraArray)
				{
					var cd = new CameraData { camera = cam };
					var requireDepthTexture = false;
					// ReSharper disable once ForCanBeConvertedToForeach
					for (int i = 0; i < activeObjects.Count; i++)
					{
						var rpd = activeObjects[i].GetRenderPathData(cam.actualRenderingPath);
						if (rpd == null)
							continue;
						requireDepthTexture |= rpd.RequiresDepthTexture;
						if (rpd.MeshData != null)
							foreach (var md in rpd.MeshData)
								if (CanUseDrawMesh(cam.actualRenderingPath, md, rpd))
									cd.meshData.Add(md);
								else
									cd.meshDataForBuffer.Add(md);
					}
					cam.depthTextureMode = requireDepthTexture ? DepthTextureMode.Depth : DepthTextureMode.None;
					list.Add(cd);
				}
				cameraData = list.ToArray();
			}
		}
		
		// TODO: Don't do this if no manual culling
		protected void RebuildVisibleCommandBuffers()
		{
			var activeObjects = DecalObject.ActiveObjects;
			foreach (var cd in cameraData)
			{
				cd.command?.Clear();
				bool setup = false;
				// ReSharper disable once ForCanBeConvertedToForeach
				for (int i = 0; i < activeObjects.Count; i++)
				{
					var obj = activeObjects[i];
					var rpd = obj.GetRenderPathData(cd.camera.actualRenderingPath);
					if ((rpd?.RendererData == null && cd.meshDataForBuffer.Count <= 0) ||
					    (obj.UseManualCulling && !toRender.Contains(new KeyValuePair<DecalObject, Camera>(obj, cd.camera))))
						continue;
					var cmd = GetCommandBuffer(cd);
					if (!setup)
					{
						SetupCommandBuffer(cd, GetCameraEvent(cd.camera.actualRenderingPath), cmd);
						setup = true;
					}
					if(rpd?.RendererData != null)
						foreach (var rd in rpd.RendererData)
						{
							var passes = rd.instance?.DecalMaterial?.GetKnownPasses(cd.camera.actualRenderingPath);
							if (passes == null)
							{
								Debug.LogError($"Unable to determine pass order for decal in {obj}", obj);
								obj.enabled = false;
								break;
							}
							foreach(var pass in passes)
								cmd.DrawRenderer(rd.renderer, rd.material, rd.submesh, pass);
						}
					foreach (var md in cd.meshDataForBuffer)
					{
						var matrix = md.matrix;
						if (md.transform != null)
							matrix = matrix * md.transform.localToWorldMatrix;
						var passes = md.instance?.DecalMaterial?.GetKnownPasses(cd.camera.actualRenderingPath);
						if (passes == null)
						{
							Debug.LogError($"Unable to determine pass order for decal in {obj}", obj);
							obj.enabled = false;
							break;
						}
						foreach (var pass in passes)
							cmd.DrawMesh(md.mesh, matrix, md.material, md.submesh, pass, md.materialPropertyBlock);
					}
				}
			}
			toRender.Clear();
		}

		protected void DrawMeshes()
		{
			foreach (var cd in cameraData)
			{
				if (cd.meshData == null) continue;
				foreach (var md in cd.meshData)
				{
					var matrix = md.matrix;
					if (md.transform != null)
						matrix = matrix * md.transform.localToWorldMatrix;
					Graphics.DrawMesh(md.mesh, matrix, md.material, 0,
						cd.camera, md.submesh, md.materialPropertyBlock, false, true);
				}
			}
		}

		protected virtual void Update()
		{
			RebuildCameraDataIfNeeded();
			RebuildVisibleCommandBuffers();
			DrawMeshes();
		}
	}
}
