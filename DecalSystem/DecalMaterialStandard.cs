using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DecalSystem.ShaderKeywords;

namespace DecalSystem
{
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
				addKeyword("_POM"); // TODO: Replace with user option
			else if (normal)
				addKeyword("_NORMALMAP");
			else
			{
				removeKeyword("_POM");
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
			if(ShaderInstance == null)
				Debug.LogError("Decal shader not found, or has errors!");
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
			return supportedKeywords.Contains(mode) ? ShaderInstance : null;
		}

		private Material screenSpaceDeferred;

		public override void Refresh()
		{
			base.Refresh();
			if (screenSpaceDeferred != null)
			{
				SetKeywords(screenSpaceDeferred);
				CopyTo(screenSpaceDeferred);
			}
		}

		// The screen-space deferred material needs to use front-face culling so it doesn't
		// vanish when the camera moves within the bounding box
		public override Material ModifyMaterial(Material m, RenderingPath rp)
		{
			if (m.IsKeywordEnabled(ScreenSpace) && rp == RenderingPath.DeferredShading)
			{
				if(screenSpaceDeferred == null)
					screenSpaceDeferred = Instantiate(m);
				screenSpaceDeferred.SetFloat("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
				return screenSpaceDeferred;
			}
			return m;
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

		public override bool RequiresDepthTexture(Material mat)
		{
			return mat?.IsKeywordEnabled(ScreenSpace) ?? false;
		}
	}
}
