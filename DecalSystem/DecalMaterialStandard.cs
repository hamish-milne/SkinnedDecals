using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DecalSystem.ShaderKeywords;

namespace DecalSystem
{
	public struct DecalObjectData
	{
		public Renderer renderer;
		public Mesh mesh;
		public Material material;
		public MaterialPropertyBlock properties;
		public Transform transform;
		public Matrix4x4 matrix;
	}

	public struct DecalRenderCommand
	{
		public DecalObjectData objectData;
		public int[] passes;
		public DepthTextureMode depthTextureMode;
	}

	/// <summary>
	/// Draws decals using the Standard lighting model
	/// </summary>
	[CreateAssetMenu]
	public class DecalMaterialStandard : DecalMaterial
	{
		private static Shader shaderInstance;
		private static HashSet<string> supportedKeywords;

		/// <summary>
		/// Draws most decal types
		/// </summary>
		public virtual Shader ShaderInstance => shaderInstance ?? (shaderInstance = Shader.Find("Decal/Standard"));

		[SerializeField, MaterialProperty("_Color")]            protected Color color = Color.white;
		[SerializeField, MaterialProperty("_EmissionColor")]    protected Color emission = Color.black;
		[SerializeField, MaterialProperty("_Glossiness")]       protected float smoothness;
		[SerializeField, MaterialProperty("_Metallic")]         protected float metallic;
		[SerializeField, MaterialProperty("_Parallax")]         protected float parallax;
		[SerializeField, MaterialProperty("_MainTex")]          protected Texture2D albedo;
		[SerializeField, MaterialProperty("_BumpMap")]          protected Texture2D normal;
		[SerializeField, MaterialProperty("_MetallicGlossMap")] protected Texture2D roughnessMap;
		[SerializeField, MaterialProperty("_ParallaxMap")]      protected Texture2D parallaxMap;
		[SerializeField, MaterialProperty("_EmissionMap")]      protected Texture2D emissionMap;

		public override void SetKeywords(Action<string> addKeyword, Action<string> removeKeyword)
		{
			if (parallaxMap)
				addKeyword("_PARALLAXMAP");
			else if (normal)
				addKeyword("_NORMALMAP");
			else
			{
				removeKeyword("_PARALLAXMAP");
				removeKeyword("_NORMALMAP");
			}
			if (roughnessMap)
				addKeyword("_METALLICGLOSSMAP");
			else
				removeKeyword("_METALLICGLOSSMAP");
			if (emission.maxColorComponent > float.Epsilon)
				addKeyword("_EMISSION");
			else
				removeKeyword("_EMISSION");
		}

		public override Shader GetShaderForMode(string mode)
		{
			if (supportedKeywords == null)
			{
				var mat = new Material(ShaderInstance);
				supportedKeywords = new HashSet<string>(new []
				{
					"_",
					FixedSingle,
					Fixed4,
					Fixed8,
					ScreenSpace,
					SkinnedBuffer,
					SkinnedUv
				}.Where(k => mat.GetTag("DecalSystem" + k, true, "").ToLower() == "true"));
			}
			if (string.IsNullOrEmpty(mode))
				mode = "_";
			return supportedKeywords.Contains(mode) ? shaderInstance : null;
		}

		// Both passes are named 'DEFERRED', so we have to enumerate them
		private static readonly int[] deferredPasses =
		{
			2, // Main Deferred pass
			3  // Smoothness blending
		};
		public override int[] GetKnownPasses(RenderingPath renderingPath)
		{
			if (renderingPath == RenderingPath.DeferredShading)
				return deferredPasses;
			return base.GetKnownPasses(renderingPath);
		}
	}
}
