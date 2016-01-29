Shader "Decal/Test"
{
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}
	SubShader {
		Tags{ "RenderType" = "Opaque" "Queue" = "Geometry+1" "ForceNoShadowCasting" = "True" }
		LOD 200
		Offset -1, -1

		CGPROGRAM
		#pragma surface surf Standard exclude_path:forward exclude_path:prepass nometa

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutputStandard o) {
			half4 c = tex2D(_MainTex, IN.uv_MainTex);
			o.Albedo = float3(1,0,1);
			o.Alpha = 1;
		}
		ENDCG
	}
}
