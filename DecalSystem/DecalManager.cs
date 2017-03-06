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

		private readonly Dictionary<Camera, CameraData> cameraData = new Dictionary<Camera, CameraData>();

		protected class RenderPathData
		{
			public readonly List<MeshData> meshList = new List<MeshData>();

			public bool requireDepthTexture;
		}

		/// <summary>
		/// The currently enabled manager instance
		/// </summary>
		public static DecalManager Current { get; private set; }

		private static Camera sceneCamera;

		static DecalManager()
		{
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
			foreach (var pair in cameraData)
			{
				var cd = pair.Value;
				if (cd.command != null)
				{
					if(pair.Key != null)
						pair.Key.RemoveCommandBuffer(cd.cameraEvent, cd.command);
					cd.command.Dispose();
				}
				// TODO: Figure out when we change this
				if (pair.Key != null)
					pair.Key.depthTextureMode = DepthTextureMode.None;
			}
			cameraData.Clear();
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
			foreach (var cd in cameraData)
			{
				if (cd.Value.command != null)
				{
					cd.Key.RemoveCommandBuffer(cd.Value.cameraEvent, cd.Value.command);
					cd.Value.command.Dispose();
				}
			}
			cameraData.Clear();
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
				return true;
			}
			foreach(var cam in cameraArray)
				if (cam.GetComponent<DecalCamera>() == null)
					cam.gameObject.AddComponent<DecalCamera>();
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
		protected CommandBuffer GetCommandBuffer(Camera cam, CameraData cd)
		{
			const string commandName = "Draw decals (DecalSystem)";
			if (cd.command == null)
			{
				var evt = GetCameraEvent(cam.actualRenderingPath);
				var cmds = cam.GetCommandBuffers(evt);
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
					cam.AddCommandBuffer(evt, cd.command);
					SetupCommandBuffer(cd, cd.command);
				}
				else
				{
					cd.command.Clear();
				}
			}
			return cd.command;
		}

		protected virtual bool CanUseDrawMesh(RenderingPath rp, DecalMaterial dmat, Material mat)
		{
			return !(rp == RenderingPath.DeferredShading && dmat.RequiresDepthTexture(mat));
		}
		
		private readonly List<KeyValuePair<string, ComputeBuffer>> setBuffers = new List<KeyValuePair<string, ComputeBuffer>>(); 

		public void DrawDecals(Camera cam)
		{
			CameraData cd;
			if (!cameraData.TryGetValue(cam, out cd))
				cameraData.Add(cam, cd = new CameraData());
			if (cd.command != null)
			{
				cd.command.Clear();
				SetupCommandBuffer(cd, cd.command);
			}

			var rp = cam.actualRenderingPath;
			bool requireDepth = false;
			var activeObjects = DecalObject.ActiveObjects;
			// ReSharper disable once ForCanBeConvertedToForeach
			for (int j = 0; j < activeObjects.Count; j++)
			{
				var obj = activeObjects[j];
				foreach (var draw in obj.GetDecalDraws())
				{
					try
					{
						if (draw?.Enabled != true) continue;
						Mesh mesh = null;
						Renderer rend = null;
						int submesh = 0;
						Material material = null;
						MaterialPropertyBlock block = null;
						var matrix = Matrix4x4.identity;

						setBuffers.Clear();
						draw.GetDrawCommand(rp, ref mesh, ref rend, ref submesh, ref material, ref block, ref matrix, setBuffers);
						if (material == null || (rend == null && mesh == null)) continue;

						material = draw.DecalMaterial?.ModifyMaterial(material, rp) ?? material;
						var useCommandBuffer = rend != null;
						useCommandBuffer |= mesh != null && !CanUseDrawMesh(rp, draw.DecalMaterial, material);
						requireDepth |= draw.DecalMaterial.RequiresDepthTexture(material);
						
						if (useCommandBuffer)
						{
							// Culling
							if (obj.UseManualCulling && !toRender.Remove(new KeyValuePair<DecalObject, Camera>(obj, cam)))
								continue;
							var cmd = GetCommandBuffer(cam, cd);
							var passes = draw.DecalMaterial?.GetKnownPasses(rp);
							if (passes == null)
								throw new Exception($"Unable to determine pass order for decal with {draw}");
							foreach(var pair in setBuffers)
								cmd.SetGlobalBuffer(pair.Key, pair.Value);
							if (rend != null)
							{
								foreach (var pass in passes)
									cmd.DrawRenderer(rend, material, submesh, pass);
							}
							else
							{
								foreach (var pass in passes)
									cmd.DrawMesh(mesh, matrix, material, submesh, pass, block);
							}
						}
						else if (mesh != null)
						{
							Graphics.DrawMesh(mesh, matrix, material, 0, //Default layer
								cam, submesh, block, false, true);
						}
					}
					catch (Exception e)
					{
						Debug.LogException(e, obj);
					}
				}
			}

			cam.depthTextureMode = requireDepth ? DepthTextureMode.Depth : DepthTextureMode.None;
		}

		public void Repaint()
		{
			if (!enabled) return;
			if (doClearData)
			{
				ClearDataNow();
				doClearData = false;
			}
			CheckCameras();
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
