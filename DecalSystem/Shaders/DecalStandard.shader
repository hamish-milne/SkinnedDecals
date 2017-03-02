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

		_Cull ("Cull", Float) = 2
	}

	Category
	{
		Tags
		{
			"Queue" = "AlphaTest+1"
			"IgnoreProjector" = "True"
			"ForceNoShadowCasting" = "True"
			"DecalSystem_" = "True"
			"DecalSystem_FIXEDSINGLE" = "True"
			"DecalSystem_FIXED4" = "True"
			"DecalSystem_SCREENSPACE" = "True"
			"DecalSystem_SKINNEDUV" = "True"
		}

		ZWrite Off
		Offset -1,-1
		Blend One OneMinusSrcAlpha, Zero OneMinusSrcAlpha
		Cull [_Cull]

		SubShader
		{
			Tags
			{
				"DecalSystem_FIXED8" = "True"
				"DecalSystem_SKINNEDBUFFER" = "True"
			}

			CGPROGRAM
			#pragma target 4.0
			// For some reason Metal reports as SM4, when it really isn't
			// Might as well exclude it rather than wasting time trying to compile. If Metal gets better try commenting this out.
			#pragma exclude_renderers metal
			#pragma shader_feature _ _PARALLAXMAP _NORMALMAP // Parallax takes priority - needs to be first
			#pragma shader_feature _ _METALLICGLOSSMAP
			#pragma shader_feature _ _EMISSION
			#pragma shader_feature _ _SCREENSPACE _FIXEDSINGLE _FIXED4 _SKINNEDUV _FIXED8 _SKINNEDBUFFER
			#pragma surface surf Standard nometa vertex:vert finalgbuffer:FinalGBuffer finalcolor:FinalColor keepalpha nolightmap

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
				#pragma target 4.0
				#pragma exclude_renderers metal
				#pragma shader_feature _ _PARALLAXMAP
				#pragma shader_feature _ _METALLICGLOSSMAP
				#pragma shader_feature _ _SCREENSPACE _FIXEDSINGLE _FIXED4 _SKINNEDUV _FIXED8 _SKINNEDBUFFER
				#pragma vertex SmoothnessVert
				#pragma fragment SmoothnessFrag

				#include "DecalSystem.cginc"
				ENDCG
			}
		}

		SubShader
		{
			CGPROGRAM
			#pragma target 3.0
			// d3d11_9x basically doesn't work with anything, even a simple surface shader
			#pragma exclude_renderers d3d11_9x
			#pragma shader_feature _ _PARALLAXMAP _NORMALMAP
			#pragma shader_feature _ _METALLICGLOSSMAP
			#pragma shader_feature _ _EMISSION
			#pragma shader_feature _ _SCREENSPACE _FIXEDSINGLE _FIXED4 _SKINNEDUV
			#pragma surface surf Standard nometa vertex:vert finalgbuffer:FinalGBuffer finalcolor:FinalColor keepalpha nolightmap

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
				#pragma exclude_renderers d3d11_9x
				#pragma shader_feature _ _PARALLAXMAP
				#pragma shader_feature _ _METALLICGLOSSMAP
				#pragma shader_feature _ _SCREENSPACE _FIXEDSINGLE _FIXED4 _SKINNEDUV
				#pragma vertex SmoothnessVert
				#pragma fragment SmoothnessFrag

				#include "DecalSystem.cginc"
				ENDCG
			}
		}
	}
}
