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
		Blend SrcAlpha OneMinusSrcAlpha
		Tags { "Queue" = "Overlay" }
		Pass {
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _CameraDepthTexture;

			float3 _LocalCameraPos;

			struct v2f
			{
				float4 pos : POSITION;
				float4 screenPos : TEXCOORD0;
				float3 ray : TEXCOORD1;
			};

			struct vdata
			{
				float4 vertex : POSITION;
			};

			v2f vert(vdata v)
			{
				v2f o;
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
				o.screenPos = ComputeScreenPos(o.pos);
				o.ray = v.vertex - _LocalCameraPos;
				return o;
			}

			float4 frag(v2f i) : COLOR
			{
                i.screenPos.xy /= i.screenPos.w;
				float sceneDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.screenPos.xy);
				sceneDepth = LinearEyeDepth(sceneDepth);

				float3 decalPos = _LocalCameraPos + i.ray * (sceneDepth / i.screenPos.w);

                if (any(abs(decalPos.xyz) > 0.5)) discard;

				//return float4(1, 0, 0, 1);
				float4 color = tex2D(_MainTex, decalPos.xy + 0.5);
                //float alpha = max(0, 0.5 - decalPos.z) * 4 - 2;
				return color;
			}

			ENDCG
		}
	}
}
