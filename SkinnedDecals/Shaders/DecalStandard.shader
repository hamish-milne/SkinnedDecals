Shader "Decal/Standard"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo", 2D) = "white" {}	
	
		_Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
		[Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
		_MetallicGlossMap("Metallic", 2D) = "white" {}

		_BumpScale("Scale", Float) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}

		_Parallax ("Height Scale", Range (0.0005, 0.08)) = 0.02
		_ParallaxMap ("Height Map", 2D) = "black" {}

		_EmissionColor("Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"IgnoreProjector" = "True"
			"ForceNoShadowCasting" = "True"
		}

		ZWrite Off
		Offset -1,-1
		Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
		#pragma target 3.0
		#pragma multi_compile _ _NORMALMAP _PARALLAXMAP
		#pragma multi_compile _ _EMISSION
		#pragma multi_compile _ _STATIC _SCREENSPACE _SKINNEDUV0 _SKINNEDUV1 _SKINNEDUV2 _SKINNEDUV3 _SKINNEDUV4 _SKINNEDUV5 _SKINNEDUV6
        #pragma surface surf Standard nometa exclude_path:prepass vertex:vert finalgbuffer:FinalGBuffer keepalpha nolightmap
		// Reduce the shader variant count by cutting uncommon use cases
		#ifdef _PARALLAXMAP
		#define _NORMALMAP
		#endif
		#ifdef _NORMALMAP
		#define _METALLICGLOSSMAP
		#endif
		#include "DecalSurface.cginc"
        ENDCG
	}
}
