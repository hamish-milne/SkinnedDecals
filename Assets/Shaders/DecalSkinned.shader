﻿Shader "Decal/Skinned"
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

		// Blending state
		[HideInInspector] _SrcBlend("__src", Float) = 1.0
		[HideInInspector] _DstBlend("__dst", Float) = 0.0
	}

	SubShader
	{
		Tags { "DecalMode" = "Skinned" }
		Pass
		{
			ZWrite Off
			Offset -1, -1
			Blend [_SrcBlend] [_DstBlend]
		
			CGPROGRAM
			#pragma multi_compile_fog
			#pragma multi_compile _ _FORWARD
			#pragma multi_compile _ _NORMALMAP
			#pragma multi_compile _ _METALLICGLOSSMAP
			#pragma multi_compile _ _PARALLAXMAP
			#pragma multi_compile _ _EMISSION
			#define _SKINNED
			#pragma vertex vert
			#pragma fragment frag
			#include "SkinnedDecals.cginc"
			ENDCG
		}
	}

	SubShader
	{
		Tags{ "DecalMode" = "Static" }
		Pass
		{
			ZWrite Off
			Offset -1, -1
			Blend [_SrcBlend] [_DstBlend]
		
			CGPROGRAM
			#pragma multi_compile_fog
			#pragma multi_compile _ _FORWARD
			#pragma multi_compile _ _NORMALMAP
			#pragma multi_compile _ _METALLICGLOSSMAP
			#pragma multi_compile _ _PARALLAXMAP
			#pragma multi_compile _ _EMISSION
			#undef _SKINNED
			#pragma vertex vert
			#pragma fragment frag
			#include "SkinnedDecals.cginc"
			ENDCG
		}
	}
}
