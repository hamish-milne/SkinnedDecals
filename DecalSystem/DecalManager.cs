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
		/// Whether to draw decals on the scene camera
		/// </summary>
		[SerializeField] protected bool renderSceneCamera = true;

		/// <summary>
		/// The currently enabled manager instance
		/// </summary>
		public static DecalManager Current { get; private set; }

		private static Camera sceneCamera;

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
			cameraArray = null;
		}

		// Holds whether a manually culled object is rendered for each camera
		private readonly HashSet<Pair<DecalObject, Camera>> toRender
			= new HashSet<Pair<DecalObject, Camera>>();   

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
			toRender.Add(new Pair<DecalObject, Camera>(obj, renderCamera));
		}
		
		private Camera[] cameraArray;

		public virtual bool CanDrawRenderers(DecalMaterial material)
		{
			GetAllCameras();
			foreach(var c in cameraArray)
				if (!material.CanDrawRenderers(c.actualRenderingPath))
					return false;
			return true;
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
		protected void AddCameraComponents()
		{
			GetAllCameras();
			foreach(var cam in cameraArray)
				if (cam.GetComponent<DecalCamera>() == null)
					cam.gameObject.AddComponent<DecalCamera>();
		}

		protected virtual bool CanUseDrawMesh(RenderingPath rp, DecalMaterial dmat, Material mat)
		{
			return !(rp == RenderingPath.DeferredShading && dmat.RequiresDepthTexture(mat));
		}
		
		private readonly List<KeyValuePair<string, ComputeBuffer>> setBuffers = new List<KeyValuePair<string, ComputeBuffer>>(); 

		public void DrawDecals(Camera cam, CommandBuffer cmdDepthTexture, CommandBuffer cmdDepthZtest)
		{
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
						var thisRequiresDepth = draw.DecalMaterial.RequiresDepthTexture(material);
						requireDepth |= thisRequiresDepth;
						
						if (useCommandBuffer)
						{
							// Culling
							if (obj.UseManualCulling && !toRender.Remove(new Pair<DecalObject, Camera>(obj, cam)))
								continue;
							var cmd = thisRequiresDepth ? cmdDepthTexture : cmdDepthZtest;
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
			AddCameraComponents();
		}

		protected virtual void Update()
		{
			Repaint();
		}
	}

	[RequireComponent(typeof(Camera)), ExecuteInEditMode]
	public class DecalCamera : MonoBehaviour
	{
		private CommandBuffer cmdDepthTexture, cmdDepthZtest;
		private CameraEvent evtDepthTexture, evtDepthZtest;
		private RenderingPath prevRenderingPath = RenderingPath.UsePlayerSettings;

		protected virtual void GetCameraEvents(RenderingPath path, out CameraEvent depthTexture, out CameraEvent depthZtest)
		{
			bool hasDepthZtest = false;
			depthZtest = default(CameraEvent);
			switch (path)
			{
				case RenderingPath.Forward:
					depthTexture = CameraEvent.AfterForwardOpaque;
					break;
				case RenderingPath.DeferredShading:
					depthTexture = CameraEvent.BeforeReflections;
					depthZtest = CameraEvent.AfterGBuffer;
					hasDepthZtest = true;
					break;
				case RenderingPath.DeferredLighting:
					depthTexture = CameraEvent.AfterFinalPass;
					break;
				case RenderingPath.VertexLit:
					depthTexture = CameraEvent.BeforeImageEffects;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			if (!hasDepthZtest)
				depthZtest = depthTexture;
		}

		private static readonly RenderTargetIdentifier[] gBuffer = new[]
		{
			BuiltinRenderTextureType.GBuffer0,
			BuiltinRenderTextureType.GBuffer1,
			BuiltinRenderTextureType.GBuffer2,
			BuiltinRenderTextureType.GBuffer3
		}.Select(b => new RenderTargetIdentifier(b)).ToArray();

		// This unbinds the depth buffer, allowing it to be read as a texture
		private static readonly RenderTargetIdentifier depth =
			new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive);

		protected virtual void SetupCommandBuffer(CameraEvent evt, CommandBuffer cmd)
		{
			if (evt == CameraEvent.BeforeReflections)
			{
				cmd.SetRenderTarget(gBuffer, depth);
			}
		}

		private static CommandBuffer GetOrAddBuffer(Camera cam, CameraEvent evt)
		{
			const string commandName = "Draw decals (DecalSystem)";
			var cmd = cam.GetCommandBuffers(evt).FirstOrDefault(c => c.name == commandName);
			if(cmd == null)
				cam.AddCommandBuffer(evt, cmd = new CommandBuffer {name = commandName});
			return cmd;
		}

		protected virtual void OnPreCull()
		{
			var cam = GetComponent<Camera>();
			if (prevRenderingPath != cam.actualRenderingPath)
			{
				if (cmdDepthTexture != null)
					cam.RemoveCommandBuffer(evtDepthTexture, cmdDepthTexture);
				if (cmdDepthZtest != null)
					cam.RemoveCommandBuffer(evtDepthZtest, cmdDepthZtest);
				prevRenderingPath = cam.actualRenderingPath;
				GetCameraEvents(prevRenderingPath, out evtDepthTexture, out evtDepthZtest);
				cmdDepthTexture = GetOrAddBuffer(cam, evtDepthTexture);
				cmdDepthZtest = GetOrAddBuffer(cam, evtDepthZtest);
			}
			SetupCommandBuffer(evtDepthTexture, cmdDepthTexture);
			SetupCommandBuffer(evtDepthZtest, cmdDepthZtest);
			DecalManager.Current?.DrawDecals(cam, cmdDepthTexture, cmdDepthZtest);
		}

		protected virtual void OnPostRender()
		{
			cmdDepthTexture?.Clear();
			cmdDepthZtest?.Clear();
		}
	}
}
