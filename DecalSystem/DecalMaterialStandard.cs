using System;
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
		private static Shader sm4Shader, baseShader;

		/// <summary>
		/// Draws skinned decals using a <c>ComputeBuffer</c>
		/// </summary>
		public static Shader Sm4Shader => sm4Shader ?? (sm4Shader = Shader.Find("Decal/SM4"));

		/// <summary>
		/// Draws most decal types
		/// </summary>
		public static Shader BaseShader => baseShader ?? (baseShader = Shader.Find("Decal/Standard"));

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
			switch (mode)
			{
				case SkinnedBuffer:
					return Sm4Shader?.isSupported != true ? null : Sm4Shader;
				case SkinnedUv:
				case FixedSingle:
				case Fixed4:
				case Fixed8:
				case ScreenSpace:
				case "":
					return BaseShader;
				default:
					return null;
			}
		}
	}
}
