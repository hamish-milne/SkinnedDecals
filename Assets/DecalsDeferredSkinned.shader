Shader "Decals/Deferred/Skinned"
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

		_Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
		_ParallaxMap ("Height Map", 2D) = "black" {}

		_EmissionColor("Color", Color) = (0,0,0)
		_EmissionMap("Emission", 2D) = "white" {}
	}
	SubShader
	{
		Pass
		{
			ZWrite Off
			Offset -1, -1
			Blend SrcAlpha OneMinusSrcAlpha
		
			CGPROGRAM
			#pragma exclude_renderers d3d9 opengl gles d3d11_9x metal xbox360 ps3
			#define _SKINNED
			#pragma vertex vert
			#pragma fragment frag
			#include "SkinnedDecals.cginc"
			ENDCG
		}
	}
}
