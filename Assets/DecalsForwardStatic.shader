Shader "Decals/Forward/Static"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo", 2D) = "white" {}

		_BumpScale("Scale", Float) = 1.0
		_BumpMap("Normal Map", 2D) = "bump" {}

		_Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
		_ParallaxMap ("Height Map", 2D) = "black" {}
		
		_BodyAlbedo ("Body albedo", 2D) = "white" {}
	}
	SubShader
	{
		Pass
		{
			ZWrite Off
			Offset -1, -1
			Blend DstColor Zero
		
			CGPROGRAM
			#pragma multi_compile_fog
			#define _FORWARD
			#pragma vertex vert
			#pragma fragment frag
			#include "SkinnedDecals.cginc"
			ENDCG
		}
	}
}
