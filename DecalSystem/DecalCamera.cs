using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DecalSystem
{
	[RequireComponent(typeof(Camera)), ExecuteInEditMode]
	public class DecalCamera : MonoBehaviour
	{
		private CommandBuffer cmdDepthTexture, cmdDepthZtest;
		private CameraEvent evtDepthTexture, evtDepthZtest;
		[NonSerialized] private RenderingPath prevRenderingPath = RenderingPath.UsePlayerSettings;

		public Camera Camera { get; private set; }

		protected virtual void OnEnable()
		{
			Camera = GetComponent<Camera>();
		}

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

		protected virtual void SetupCommandBuffer(CameraEvent evt, CommandBuffer cmd)
		{
			if (evt == CameraEvent.BeforeReflections)
			{
				cmd.SetRenderTarget(gBuffer, depth);
			}
		}

		private CommandBuffer GetOrAddBuffer(CameraEvent evt)
		{
			const string commandName = "Draw decals (DecalSystem)";
			var cmd = Camera.GetCommandBuffers(evt).FirstOrDefault(c => c.name == commandName);
			if(cmd == null)
				Camera.AddCommandBuffer(evt, cmd = new CommandBuffer {name = commandName});
			return cmd;
		}

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
			DrawDecals(cmdDepthTexture, cmdDepthZtest);
		}

		private readonly HashSet<DecalObject> toRender = new HashSet<DecalObject>();

		public virtual void RenderObject(DecalObject obj)
		{
			if (obj != null)
				toRender.Add(obj);
		}

		// Static list for when we pass setBuffers into the decal draws
		private readonly List<KeyValuePair<string, ComputeBuffer>> setBuffers = new List<KeyValuePair<string, ComputeBuffer>>();

		public virtual bool CanDrawRenderers(DecalMaterial dmat)
		{
			return dmat.GetKnownPasses(Camera.actualRenderingPath) != null;
		}

		/// <summary>
		/// Draws decals for this camera
		/// </summary>
		/// <param name="cDepthTexture"></param>
		/// <param name="cDepthZtest"></param>
		public void DrawDecals(CommandBuffer cDepthTexture, CommandBuffer cDepthZtest)
		{
			var rp = Camera.actualRenderingPath;
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
						draw.GetDrawCommand(this, ref mesh, ref rend, ref submesh, ref material, ref block, ref matrix, setBuffers);
						if (material == null || (rend == null && mesh == null)) continue;

						material = draw.DecalMaterial?.ModifyMaterial(material, rp) ?? material;
						var useCommandBuffer = rend != null;
						useCommandBuffer |= mesh != null && !CanUseDrawMesh(rp, draw.DecalMaterial, material);
						var thisRequiresDepth = draw.DecalMaterial.RequiresDepthTexture(material);
						requireDepth |= thisRequiresDepth;

						if (useCommandBuffer)
						{
							if (!toRender.Contains(obj))
								continue; // Object culled
							var cmd = thisRequiresDepth ? cDepthTexture : cDepthZtest;
							var passes = draw.DecalMaterial?.GetKnownPasses(rp);
							if (passes == null)
								throw new Exception($"Unable to determine pass order for decal with {draw}");
							foreach (var pair in setBuffers)
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
								Camera, submesh, block, false, true);
						}
					}
					catch (Exception e)
					{
						Debug.LogException(e, obj);
					}
				}
			}

			toRender.Clear();
			Camera.depthTextureMode = requireDepth ? DepthTextureMode.Depth : DepthTextureMode.None;
		}
	}
}