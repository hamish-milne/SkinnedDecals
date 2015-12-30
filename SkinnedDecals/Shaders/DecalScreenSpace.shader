Shader "Decal/ScreenSpace"
{
	Properties{
		_MainTex("Albedo", 2D) = "white" {}
	}
	SubShader{
		ZTest LEqual
		Cull Back
		ZWrite Off
		Fog{ Mode Off }
		Blend Off
		Tags { "Queue" = "Overlay" }
		Pass {
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _CameraDepthTexture;

			float4x4 _ViewProjectInverse;

			struct v2f
			{
				float4 pos : POSITION;
				float4 screenPos : TEXCOORD0;
				float4 ray : TEXCOORD1;
				float4 cameraPos : TEXCOORD2;
				float4 wPos : TEXCOORD3;
			};

			struct vdata
			{
				float4 vertex : POSITION;
			};

			v2f vert(vdata v)
			{
				v2f o;
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
				o.screenPos = o.pos;
				o.screenPos.z = COMPUTE_DEPTH_01;
				o.cameraPos = mul(_World2Object, _WorldSpaceCameraPos);
				o.ray = v.vertex - o.cameraPos;
				o.wPos = mul(_Object2World, v.vertex);
				return o;
			}

			float3 frag(v2f i) : COLOR
			{
				float4 coords = UNITY_PROJ_COORD(i.screenPos);
				float2 screenSpace = ((coords.xy / coords.w) / 2) + float2(0.5, 0.5);
				float sceneDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenSpace);
				sceneDepth = LinearEyeDepth(sceneDepth);
				float3 worldPixelPos = _WorldSpaceCameraPos + normalize(i.wPos - _WorldSpaceCameraPos) * distance(i.wPos, _WorldSpaceCameraPos);

				return float3(pow(distance(i.wPos, _WorldSpaceCameraPos) / 5, 2), 0, 0);
				/*float4 coords = UNITY_PROJ_COORD(i.screenPos);
				float2 screenSpace = ((coords.xy / coords.w) / 2) + float2(0.5, 0.5);
				float sceneDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenSpace);
				sceneDepth = Linear01Depth(sceneDepth);
				//sceneDepth = abs((sceneDepth / coords.w) - 1);
				float4 pixelPos = i.cameraPos + i.ray * (sceneDepth / coords.z);
				//clip(any(pixelPos > 0.5) || any(pixelPos < -0.5) ? -1 : 1);
				float4 uvCoords = UNITY_PROJ_COORD(pixelPos);
				return tex2D(_MainTex, (uvCoords.xy) + float2(0.5,0.5));*/

			}

			ENDCG
		}
	}
}
