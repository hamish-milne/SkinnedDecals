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

			public CameraEvent cameraEvent;

			/// <summary>
			/// The command buffer to bind, if any
			/// </summary>
			public CommandBuffer command;
		}

		/// <summary>
		/// Whether to draw decals on the scene camera
		/// </summary>
		[SerializeField] protected bool renderSceneCamera = true;

		private CameraData[] cameraData;

		protected class RenderPathData
		{
			public readonly List<MeshData> meshList = new List<MeshData>();

			public bool requireDepthTexture;
		}

		private readonly Dictionary<RenderingPath, RenderPathData> renderPathData
			= new Dictionary<RenderingPath, RenderPathData>();

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

		protected virtual void OnDisable()
		{
			if (Current == this)
				Current = null;
			if(cameraData != null)
				foreach (var cd in cameraData)
				{
					if (cd.camera == null) return;
					if (cd.command != null)
					{
						cd.camera.RemoveCommandBuffer(cd.cameraEvent, cd.command);
						cd.command.Dispose();
					}
					// TODO: Figure out when we change this
					cd.camera.depthTextureMode = DepthTextureMode.None;
				}
			cameraData = null;
			cameraArray = null;
			prevCameraArray = null;
			prevRenderingPaths.Clear();
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

		protected virtual void SetupCommandBuffer(CameraData cd, CommandBuffer cmd)
		{
			// BeforeReflections is after the G-buffer *and* after depth is resolved
			// But, we need to set the render target back to the G-buffer to draw decals
			// Just using AfterGBuffer, the depth is from the previous frame, which causes flickering
			if (cd.cameraEvent == CameraEvent.BeforeReflections)
			{
				cmd.SetRenderTarget(gBuffer, depth);
			}
		}

		private bool doClearData;

		/// <summary>
		/// Clears the cached camera data, rebuilding command lists the next frame
		/// </summary>
		public void ClearData()
		{
			doClearData = true;
		}

		public void ClearDataNow()
		{
			if (cameraData != null)
				foreach (var cd in cameraData)
				{
					if (cd.command != null)
					{
						cd.camera.RemoveCommandBuffer(cd.cameraEvent, cd.command);
						cd.command.Dispose();
					}
				}
			cameraData = null;
			renderPathData.Clear();
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
			if (renderSceneCamera && SceneCamera != null)
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
					{
						cd.command = cmd;
						cd.cameraEvent = evt;
					}
				if (cd.command == null)
				{
					cd.command = new CommandBuffer {name = commandName};
					cd.cameraEvent = evt;
					cd.camera.AddCommandBuffer(evt, cd.command);
					SetupCommandBuffer(cd, cd.command);
				}
				else
				{
					cd.command.Clear();
				}
			}
			return cd.command;
		}

		protected virtual bool CanUseDrawMesh(RenderingPath rp, MeshData md)
		{
			return !(rp == RenderingPath.DeferredShading && md.instance.DecalMaterial.RequiresDepthTexture(md.material));
		}

		protected void RebuildCameraDataIfNeeded()
		{
			if (CheckCameras())
			{
				Debug.Log("Rebuilding camera data");
				var activeObjects = DecalObject.ActiveObjects;
				ClearDataNow();
				var list = new List<CameraData>();
				foreach (var cam in cameraArray)
				{
					var cd = new CameraData { camera = cam };
					if (cam.GetComponent<DecalCamera>() == null)
						cam.gameObject.AddComponent<DecalCamera>();
					var rp = cam.actualRenderingPath;
					RenderPathData data;
					if (!renderPathData.TryGetValue(rp, out data))
					{
						renderPathData.Add(rp, data = new RenderPathData());
						// ReSharper disable once ForCanBeConvertedToForeach
						for (int i = 0; i < activeObjects.Count; i++)
						{
							var rpd = activeObjects[i].GetRenderPathData(rp);
							if (rpd == null)
								continue;
							foreach (var md in rpd)
							{
								if (md.mesh == null) continue;
								if (md.instance == null)
								{
									Debug.LogError($"Mesh data from {activeObjects[i]} does not set an instance");
									continue;
								}
								data.requireDepthTexture |= md.instance.DecalMaterial?.RequiresDepthTexture(md.material) ?? false;
								data.meshList.Add(md);
							}
						}
					}
						
					cam.depthTextureMode = data.requireDepthTexture ? DepthTextureMode.Depth : DepthTextureMode.None;
					list.Add(cd);
				}
				cameraData = list.ToArray();
			}
		}
		
		public void DrawDecals(Camera cam)
		{
			var cd = cameraData.FirstOrDefault(o => o.camera = cam);
			if (cd == null) return;
			{
				if (cd.command != null)
				{
					cd.command.Clear();
					SetupCommandBuffer(cd, cd.command);
				}

				var rp = cd.camera.actualRenderingPath;
				RenderPathData data;
				if (!renderPathData.TryGetValue(rp, out data)) return;
				for (var i = 0; i < data.meshList.Count; i++)
				{
					var md = data.meshList[i];
					var obj = md.instance?.DecalObject;
					try
					{
						if (obj == null)
							throw new Exception($"Decal {md.instance} has no parent object");
						var useCommandBuffer = md.renderer != null;
						useCommandBuffer |= md.mesh != null && !CanUseDrawMesh(rp, md);
						var material = md.instance.DecalMaterial?.ModifyMaterial(md.material, rp) ??
						               md.material;
						if (useCommandBuffer)
						{
							// Culling
							if (obj.UseManualCulling && toRender.Contains(new KeyValuePair<DecalObject, Camera>(obj, cd.camera)))
								continue;
							var cmd = GetCommandBuffer(cd);
							var passes = md.instance.DecalMaterial?.GetKnownPasses(rp);
							if (passes == null)
								throw new Exception($"Unable to determine pass order for decal with {md.instance}");
							if (md.renderer != null)
							{
								foreach (var pass in passes)
									cmd.DrawRenderer(md.renderer, material, md.submesh, pass);
							}
							else
							{
								var matrix = md.GetFinalMatrix();
								if (matrix == null)
									throw new Exception($"No matrix for decal {md.instance}");
								foreach (var pass in passes)
									cmd.DrawMesh(md.mesh, matrix.Value, material, md.submesh, pass, md.materialPropertyBlock);
							}
						}
						else if (md.mesh != null)
						{
							var matrix = md.GetFinalMatrix();
							if (matrix == null)
								throw new Exception($"No matrix for decal {md.instance}");
							Graphics.DrawMesh(md.mesh, matrix.Value, material, 0, //Default layer
								cd.camera, md.submesh, md.materialPropertyBlock, false, true);
						}
					}
					catch (Exception e)
					{
						Debug.LogException(e, obj);
						data.meshList.RemoveAt(i--);
					}
				}
				//Debug.Log(data.meshList.Count);
			}
			toRender.Clear();
		}

		public void Repaint()
		{
			if (!enabled) return;
			if (doClearData)
			{
				ClearDataNow();
				doClearData = false;
			}
			RebuildCameraDataIfNeeded();
		}

		protected virtual void Update()
		{
			Repaint();
		}
	}

	[RequireComponent(typeof(Camera)), ExecuteInEditMode]
	public class DecalCamera : MonoBehaviour
	{
		protected virtual void OnPreCull()
		{
			DecalManager.Current?.DrawDecals(GetComponent<Camera>());
		}
	}
}
