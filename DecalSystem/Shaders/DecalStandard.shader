Shader "Decal/Standard"
{
	Properties
	{
		[HDR] _Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo", 2D) = "white" {}	
	
		_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
		[Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_MetallicGlossMap("Metallic", 2D) = "white" {}

		_BumpScale("Scale", Float) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}

		_Parallax ("Height Scale", Range (0.0005, 0.08)) = 0.02
		_ParallaxMap ("Height Map", 2D) = "black" {}

		[HDR] _EmissionColor("Emission color", Color) = (0,0,0)
		_EmissionMap("Emission map", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"Queue" = "AlphaTest+1"
			"IgnoreProjector" = "True"
			"ForceNoShadowCasting" = "True"
		}

		ZWrite Off
		Offset -1,-1
		Blend One OneMinusSrcAlpha, Zero OneMinusSrcAlpha

		CGPROGRAM
		#pragma target 3.0
		#pragma shader_feature _ _NORMALMAP _PARALLAXMAP
		#pragma shader_feature _ _METALLICGLOSSMAP
		#pragma shader_feature _ _EMISSION
		#pragma shader_feature _ _SCREENSPACE _FIXEDSINGLE _FIXED4 _FIXED8 _SKINNEDUV
		#pragma surface surf DecalStandard nometa vertex:vert finalgbuffer:FinalGBuffer finalcolor:FinalColor keepalpha nolightmap

		#ifdef _PARALLAXMAP
			#define _NORMALMAP
		#endif

		#ifdef _FIXED8
			#define _FIXEDMULTI
			#define FIXED_COUNT (8)
		#elif defined(_FIXED4)
			#define _FIXEDMULTI
			#define FIXED_COUNT (4)
		#endif

		#include "DecalSystem.cginc"
        ENDCG

		Pass
		{
			Name "DEFERRED"
			Tags { "LightMode" = "Deferred" }

			ZWrite Off
			Offset -1,-1
			Blend Zero One, One One 
			
			CGPROGRAM
			#pragma target 3.0
			#pragma multi_compile _ _PARALLAXMAP
			#pragma multi_compile _ _METALLICGLOSSMAP
			#pragma multi_compile _ _SCREENSPACE _FIXEDSINGLE _FIXED4 _FIXED8 _SKINNEDUV
			#pragma vertex SmoothnessVert
			#pragma fragment SmoothnessFrag

			#ifdef _FIXED8
				#define _FIXEDMULTI
				#define FIXED_COUNT (8)
			#elif defined(_FIXED4)
				#define _FIXEDMULTI
				#define FIXED_COUNT (4)
			#endif

			#include "DecalSystem.cginc"
			ENDCG
		}
	}
}
