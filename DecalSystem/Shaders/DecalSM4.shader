﻿Shader "Decal/SM4"
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
			"Queue" = "AlphaTest+1"
			"IgnoreProjector" = "True"
			"ForceNoShadowCasting" = "True"
		}

		ZWrite Off
		Offset -1,-1
		Blend One OneMinusSrcAlpha, Zero OneMinusSrcAlpha

		CGPROGRAM
		// Decal shader that uses SM4 features - specifically to use an arbitrary buffer of UV data to
		// draw the decal on a skinned renderer.
		#pragma exclude_renderers d3d9 opengl gles d3d11_9x metal xbox360 ps3
		#pragma multi_compile _ _NORMALMAP _PARALLAXMAP
		#pragma multi_compile _ _METALLICGLOSSMAP
		#pragma multi_compile _ _EMISSION
		#define _SKINNEDBUFFER
		#pragma surface surf DecalStandard nometa vertex:vert finalgbuffer:FinalGBuffer finalcolor:FinalColor keepalpha nolightmap

		#ifdef _PARALLAXMAP
			#define _NORMALMAP
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
			#pragma exclude_renderers d3d9 opengl gles d3d11_9x metal xbox360 ps3
			#pragma multi_compile _ _PARALLAXMAP
			#pragma multi_compile _ _METALLICGLOSSMAP
			#define _SKINNEDBUFFER
			#pragma vertex SmoothnessVert
			#pragma fragment SmoothnessFrag

			#include "DecalSystem.cginc"
			ENDCG
		}
	}
}