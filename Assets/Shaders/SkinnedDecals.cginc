#ifdef _FORWARD
	#include "UnityCG.cginc"
#endif
	
#ifdef _SKINNED
	Buffer<float3> _UvBuffer1, _UvBuffer2;
	uint _BufferOffset1, _BufferOffset2;
#else
	float4x4 _Object2Projector;
#endif
	
	sampler2D _MainTex;
	float4 _Color;
#ifdef _FORWARD
	sampler2D _BodyAlbedo;
#else
	#ifdef _NORMALMAP
		float _BumpScale;
		sampler2D _BumpMap;
	#endif
	#ifdef _METALLICGLOSSMAP
		sampler2D _MetallicGlossMap;
	#else
		float _Metallic;
		float _Glossiness;
	#endif
	#ifdef _EMISSION
		sampler2D _EmissionMap;
		float3 _EmissionColor;
	#endif
#endif

	struct v2f
	{
		float2 uv : TEXCOORD0;
#ifdef _FORWARD
		float2 bodyUv : TEXCOORD1;
		UNITY_FOG_COORDS(2)
#endif
		float4 position : POSITION;
#ifdef _SKINNED
		float clip : SV_ClipDistance;
#endif
	};
	
	v2f vert(float4 vertex : POSITION
#ifdef _SKINNED
		,uint id : SV_VertexID
#endif
#ifdef _FORWARD
		,float2 bodyUv : TEXCOORD0
#endif
	)
	{
		v2f o;
		o.position = mul(UNITY_MATRIX_MVP, vertex);
#ifdef _FORWARD
		o.bodyUv = bodyUv;
		UNITY_TRANSFER_FOG(o, o.position);
#endif
#ifdef _SKINNED
		float3 value;
		if(id >= _BufferOffset2)
			value = _UvBuffer2[id - _BufferOffset2];
		else
			value = _UvBuffer1[id - _BufferOffset1];
		o.uv = value.xy;
		o.clip = value.z > 0 ? 1e-20 : -1.#INF;
#else
		float4 uv = UNITY_PROJ_COORD(mul(_Object2Projector, vertex));
		o.uv = (uv.xy / uv.w) + float2(0.5,0.5);
#endif
#ifndef UNITY_UV_STARTS_AT_TOP
		o.uv.y = 1-o.uv.y;
#endif
		return o;
	}
	
	struct output
	{
		float4 diffuse : COLOR0;
#ifndef _FORWARD
		float4 specular : COLOR1;
	#ifdef _NORMALMAP
		float4 normal : COLOR2;
	#endif
	#ifdef _EMISSION
		float4 emission : COLOR3;
	#endif
#endif
	};
	
	float4 frag(v2f i) : COLOR
	{
		clip(all(i.uv == saturate(i.uv)) ? 1:-1);
		float4 color = tex2D(_MainTex, i.uv) * _Color;
#ifdef _FORWARD
		color.rgb /= tex2D(_BodyAlbedo, i.bodyUv).rgb;
		color = lerp(fixed4(1,1,1,0), color, color.a);
		UNITY_APPLY_FOG_COLOR(i.fogCoord, color, float4(1,1,1,1));
#endif
		return color;
	}
	