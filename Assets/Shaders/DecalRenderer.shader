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
        Tags
        {
            "Queue" = "Overlay"
        }
		
		ZWrite Off
		Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha
       
        CGPROGRAM
		#pragma shader_feature _NORMALMAP
		#pragma shader_feature _METALLICGLOSSMAP
		#pragma shader_feature _PARALLAXMAP
		#pragma shader_feature _EMISSION
        #pragma surface surf Standard alpha:fade
 
		#include "UnityPBSLighting.cginc"
		#include "UnityCG.cginc"

		struct Input
		{
			float2 uv_MainTex;
			#ifdef _PARALLAXMAP
			float3 viewDir;
			#endif
		};

		uniform float4 _Color;
		uniform sampler2D _MainTex;

		#ifdef _NORMALMAP
		uniform sampler2D _BumpMap;
		uniform float _BumpScale;
		#endif

		#ifdef _METALLICGLOSSMAP
		uniform sampler2D _MetallicGlossMap;
		#endif

		#ifdef _PARALLAXMAP
		uniform float _Parallax;
		uniform sampler2D _ParallaxMap;
		#endif

		#ifdef _EMISSION
		uniform sampler2D _EmissionMap;
		uniform float3 _EmissionColor;
		#endif

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			float2 uv = IN.uv_MainTex;
			#ifdef _PARALLAXMAP
			uv += ParallaxOffset(tex2D(_ParallaxMap, IN.uv_MainTex).a, _Parallax, IN.viewDir);
			#endif

			float4 albedo = tex2D(_MainTex, uv) * _Color;
			o.Albedo = albedo.rgb;
			o.Alpha = albedo.a;
	
			#ifdef _NORMALMAP
			float3 normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
			o.Normal = normal;
			#endif
	
			#ifdef _METALLICGLOSSMAP
			float4 attribute = tex2D(_MetallicGlossMap, uv);
			o.Smoothness = attribute.r;
			o.Metallic = attribute.a;
			#endif
	
			#ifdef _EMISSION
			float4 emission = tex2D(_EmissionMap, uv);
			o.Emission = emission * _EmissionColor;
			#endif
		}
        ENDCG
	}
}
