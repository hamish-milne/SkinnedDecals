Shader "Decal/Renderer"
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
		ZWrite Off
		Offset -1,-1
		Blend SrcAlpha OneMinusSrcAlpha
       
        CGPROGRAM
		#pragma shader_feature _NORMALMAP
		#pragma shader_feature _METALLICGLOSSMAP
		#pragma shader_feature _PARALLAXMAP
		#pragma shader_feature _EMISSION
        #pragma surface surf Standard vertex:vert
		#include "DecalSurface.cginc"
        ENDCG
	}
}
