using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DecalSystem
{
	/// <summary>
	/// Draws decals to the attached camera. Added automatically by the Manager
	/// </summary>
	[RequireComponent(typeof(Camera)), ExecuteInEditMode]
	public class DecalCamera : MonoBehaviour
	{
		/// <summary>
		/// Saves the previous, default depth texture mode here. Some decals require a depth texture
		/// </summary>
		[SerializeField, HideInInspector] protected DepthTextureMode depthTextureMode;

		// Cache command buffers and their associated camera events
		private CommandBuffer cmdDepthTexture, cmdDepthZtest;
		private CameraEvent evtDepthTexture, evtDepthZtest;
		[NonSerialized] private RenderingPath prevRenderingPath = RenderingPath.UsePlayerSettings;

		public Camera Camera { get; private set; }

		protected virtual void OnEnable()
		{
			Camera = GetComponent<Camera>();
			depthTextureMode = Camera.depthTextureMode;
		}

		protected virtual void OnDisable()
		{
			Camera.depthTextureMode = depthTextureMode;
		}

		/// <summary>
		/// Calculates the points in the rendering pipeline decal draws should be added to
		/// </summary>
		/// <param name="path">The rendering path being used</param>
		/// <param name="depthTexture">The event for when depth needs to be read from (typically screen space)</param>
		/// <param name="depthZtest">The event for when we need ZTest to work (non-screen-space)</param>
		/// <remarks>The two events can be the same</remarks>
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
					// In Deferred, the depth buffer from the previous frame is used int the G-Buffer stages
					// so for depthTexture we need to use the next stage - BeforeReflections - instead
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

		/// <summary>
		/// Checks whether <c>Graphics.DrawMesh</c> can be used for the given configuration
		/// </summary>
		/// <param name="rp"></param>
		/// <param name="dmat"></param>
		/// <param name="mat"></param>
		/// <remarks>
		/// This is only false when using deferred shading and a depth texture; we need to put it somewhere specific with a command buffer
		/// </remarks>
		/// <returns></returns>
		protected virtual bool CanUseDrawMesh(RenderingPath rp, DecalMaterial dmat, Material mat)
		{
			return !(rp == RenderingPath.DeferredShading && dmat.RequiresDepthTexture(mat));
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

		/// <summary>
		/// Sets up a command buffer at the given pipeline stage to draw decals
		/// </summary>
		/// <param name="evt">The pipeline stage</param>
		/// <param name="cmd">The buffer to set-up</param>
		protected virtual void SetupCommandBuffer(CameraEvent evt, CommandBuffer cmd)
		{
			// In BeforeReflections, the default render target is the reflection buffer and not the g-buffer
			// so we need to change it here. The target is reset automatically once the commands are executed
			if (evt == CameraEvent.BeforeReflections)
			{
				cmd.SetRenderTarget(gBuffer, depth);
			}
		}

		/// <summary>
		/// Finds an existing command buffer at the given event, or creates a new one
		/// </summary>
		/// <param name="evt">The camera event</param>
		/// <returns>A non-null CommandBuffer</returns>
		protected virtual CommandBuffer GetOrAddBuffer(CameraEvent evt)
		{
			const string commandName = "Draw decals (DecalSystem)";
			var cmd = Camera.GetCommandBuffers(evt).FirstOrDefault(c => c.name == commandName);
			if(cmd == null)
				Camera.AddCommandBuffer(evt, cmd = new CommandBuffer {name = commandName});
			return cmd;
		}

		/// <summary>
		/// This is just before the rendering process starts, so both <c>DrawMesh</c> and <c>CommandBuffer</c> will work
		/// </summary>
		protected virtual void OnPreCull()
		{
			var cam = GetComponent<Camera>();
			if (cmdDepthTexture == null || cmdDepthZtest == null || prevRenderingPath != cam.actualRenderingPath)
			{
				if (cmdDepthTexture != null)
					cam.RemoveCommandBuffer(evtDepthTexture, cmdDepthTexture);
				if (cmdDepthZtest != null)
					cam.RemoveCommandBuffer(evtDepthZtest, cmdDepthZtest);
				prevRenderingPath = cam.actualRenderingPath;
				GetCameraEvents(prevRenderingPath, out evtDepthTexture, out evtDepthZtest);
				cmdDepthTexture = GetOrAddBuffer(evtDepthTexture);
				cmdDepthZtest = GetOrAddBuffer(evtDepthZtest);
			}
			cmdDepthTexture.Clear();
			cmdDepthZtest.Clear();
			SetupCommandBuffer(evtDepthTexture, cmdDepthTexture);
			SetupCommandBuffer(evtDepthZtest, cmdDepthZtest);
			ResetFrame();
			DrawDecals(cmdDepthTexture, cmdDepthZtest);
		}

		// List of non-culled objects
		private readonly HashSet<DecalObject> toRender = new HashSet<DecalObject>();

		/// <summary>
		/// Marks the given object as visible to this camera
		/// </summary>
		/// <param name="obj"></param>
		public virtual void RenderObject(DecalObject obj)
		{
			if (obj != null)
				toRender.Add(obj);
		}

		/// <summary>
		/// Checks whether we can draw Renderers (not just Meshes) directly with the given material
		/// </summary>
		/// <param name="dmat"></param>
		/// <returns><c>true</c> if possible, <c>false</c> if not</returns>
		/// <remarks>
		/// Renderers can only be drawn with Command Buffers, so we must be able to enumerate the shader passes.
		/// </remarks>
		public virtual bool CanDrawRenderers(DecalMaterial dmat)
		{
			return dmat.GetKnownPasses(Camera.actualRenderingPath) != null;
		}

		// Static plane array to avoid allocations
		private readonly Plane[] cameraPlanes = new Plane[6];
		private bool cameraPlanesCalculated;

		/// <summary>
		/// Gets the cached camera frustum planes this frame
		/// </summary>
		/// <returns>The array of planes; this is a cached array, so should not be modified</returns>
		protected Plane[] GetCameraPlanes()
		{
			if (!cameraPlanesCalculated)
			{
				Util.ExtractPlanes(cameraPlanes, Camera);
				cameraPlanesCalculated = true;
			}
			return cameraPlanes;
		}

		// Cache the property interface wrappers
		private readonly PropertyBlockWrapper propertyBlockWrapper = new PropertyBlockWrapper();
		private readonly CommandBufferWrapper commandBufferWrapper = new CommandBufferWrapper();

		// The block cache - this saves allocating new MaterialPropertyBlock instances
		private int blockIdx;
		private readonly List<MaterialPropertyBlock> blockCache = new List<MaterialPropertyBlock>();

		/// <summary>
		/// Acquires a new or cached, empty material property block
		/// </summary>
		/// <returns></returns>
		protected virtual MaterialPropertyBlock GetPropertyBlock()
		{
			while(blockCache.Count <= blockIdx)
				blockCache.Add(new MaterialPropertyBlock());
			return blockCache[blockIdx++];
		}

		protected virtual bool IsObjectVisible(DecalObject obj)
		{
			if (obj.ManualCulling)
			{
				if (!GeometryUtility.TestPlanesAABB(GetCameraPlanes(), obj.Bounds))
					return false;
			}
			else if (!toRender.Contains(obj))
				return false;
			return true;
		}

		/// <summary>
		/// This should be called at least once before drawing begins
		/// </summary>
		protected void ResetFrame()
		{
			cameraPlanesCalculated = false;
			for (int i = 0; i < blockIdx; i++)
				blockCache[i].Clear();
			blockIdx = 0;
		}

		/// <summary>
		/// Draws decals for this camera
		/// </summary>
		/// <param name="cDepthTexture">Where depth-reading decals should be added</param>
		/// <param name="cDepthZtest">Where depth-testing decals should be added</param>
		public virtual void DrawDecals(CommandBuffer cDepthTexture, CommandBuffer cDepthZtest)
		{
			var rp = Camera.actualRenderingPath;
			bool requireDepth = false;
			var activeObjects = DecalObject.ActiveObjects;
			// ReSharper disable once ForCanBeConvertedToForeach
			for (int j = 0; j < activeObjects.Count; j++)
			{
				var obj = activeObjects[j];
				// obj should never be null, but just in case...
				if (obj == null) continue;

				// Object culling
				if (!IsObjectVisible(obj)) continue;

				var draws = obj.GetDecalDraws();
				if (draws == null) continue;
				foreach (var draw in draws)
				{
					try
					{
						if (draw?.Enabled != true) continue;

						// Get all the variables needed for the draw
						Mesh mesh = null;
						Renderer rend = null;
						int submesh = 0;
						Material material = null;
						var matrix = Matrix4x4.identity;
						draw.GetDrawCommand(this, ref mesh, ref rend, ref submesh, ref material, ref matrix);

						// Skip invalid commands
						if (material == null || (rend == null && mesh == null)) continue;

						// Figure out if we need to use a command buffer or not
						material = draw.DecalMaterial?.ModifyMaterial(material, rp) ?? material;
						var useCommandBuffer = rend != null;
						useCommandBuffer |= mesh != null && !CanUseDrawMesh(rp, draw.DecalMaterial, material);
						var thisRequiresDepth = draw.DecalMaterial.RequiresDepthTexture(material);
						requireDepth |= thisRequiresDepth;
						
						if (useCommandBuffer)
						{
							var cmd = thisRequiresDepth ? cDepthTexture : cDepthZtest;
							var passes = draw.DecalMaterial?.GetKnownPasses(rp);
							if (passes == null)
								throw new Exception($"Unable to determine pass order for decal with {draw}");

							if (rend != null)
							{
								commandBufferWrapper.CmdBuf = cmd;
								// Skinned renderers sometimes transform themselves into odd positions between now and the draw call
								// This breaks cases where we rely on a certain object space i.e. parallax
								cmd.SetGlobalMatrix(ShaderKeywords.RealObjectToWorld, rend.localToWorldMatrix);
								cmd.SetGlobalMatrix(ShaderKeywords.RealWorldToObject, rend.worldToLocalMatrix);
								draw.AddShaderProperties(commandBufferWrapper);

								foreach (var pass in passes)
									cmd.DrawRenderer(rend, material, submesh, pass);
							}
							else
							{
								var block = GetPropertyBlock();
								propertyBlockWrapper.PropertyBlock = block;
								commandBufferWrapper.CmdBuf = cmd;
								propertyBlockWrapper.CmdBuf = commandBufferWrapper;
								draw.AddShaderProperties(propertyBlockWrapper);

								foreach (var pass in passes)
									cmd.DrawMesh(mesh, matrix, material, submesh, pass, block);
							}
						}
						else if (mesh != null)
						{
							var block = GetPropertyBlock();
							propertyBlockWrapper.PropertyBlock = block;
							propertyBlockWrapper.CmdBuf = null;
							draw.AddShaderProperties(propertyBlockWrapper);

							Graphics.DrawMesh(mesh, matrix, material, 0, //Default layer
								Camera, submesh, block, false, true);
						}
					}
					catch (Exception e)
					{
						Debug.LogException(e, obj);
						obj.enabled = false;
					}
				}
			}

			toRender.Clear();
			Camera.depthTextureMode = requireDepth ? (depthTextureMode | DepthTextureMode.Depth) : depthTextureMode;
		}
	}
}