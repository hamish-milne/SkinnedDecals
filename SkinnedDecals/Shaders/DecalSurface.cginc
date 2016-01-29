#include "UnityCG.cginc"

#ifndef SHADER_API_D3D11
#undef _SKINNED
#endif

struct Input
{
#ifndef _SCREENSPACE
	float2 decalPos;
#endif
#ifdef _PARALLAXMAP
	float3 viewDirForParallax;
#endif
#ifdef _SCREENSPACE
	float4 screen;
	float3 ray;
#endif
};

struct new_appdata_full {
	float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD0;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
#ifdef _SKINNED
	uint id : SV_VertexID;
#endif
};
#define appdata_full new_appdata_full

uniform float4 _Color;
uniform sampler2D _MainTex;

#ifdef _SKINNEDBUFFER
uniform Buffer<float2> _Buffer1, _Buffer2;
uniform uint _Offset1, _Offset2;

#elif defined(_STATIC)
uniform float4x4 _Object2Decal;

#elif defined(_SCREENSPACE)
uniform sampler2D _CameraDepthTexture;
#endif

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


void vert(inout appdata_full v, out Input o)
{
	UNITY_INITIALIZE_OUTPUT(Input, o);
#ifdef _PARALLAXMAP
	TANGENT_SPACE_ROTATION;
	o.viewDirForParallax = mul(rotation, ObjSpaceViewDir(v.vertex));
#endif
#ifdef _SKINNEDBUFFER
	float2 value;
	uint id = v.id;
	if (id >= _Offset2)
		value = _Buffer2[id - _Offset2];
	else
		value = _Buffer1[id - _Offset1];
	if (value.x == 0 && value.y == 0)
		value = float2(-1, -1);
	value -= float2(4, 4);
	o.decalPos = value;
#elif defined(_SKINNEDUV0)
	o.decalPos = v.texcoord.zw;
#elif defined(_SKINNEDUV1)
	o.decalPos = v.texcoord1.xy;
#elif defined(_SKINNEDUV2)
	o.decalPos = v.texcoord1.zw;
#elif defined(_SKINNEDUV3)
	o.decalPos = v.texcoord2.xy;
#elif defined(_SKINNEDUV4)
	o.decalPos = v.texcoord2.zw;
#elif defined(_SKINNEDUV5)
	o.decalPos = v.texcoord3.xy;
#elif defined(_SKINNEDUV6)
	o.decalPos = v.texcoord3.zw;
#elif defined(_STATIC)
	float4 uv = UNITY_PROJ_COORD(mul(_Object2Decal, v.vertex));
	o.decalPos = (uv.xy / uv.w) + float2(0.5, 0.5);
#elif defined(_SCREENSPACE)
	o.screen = ComputeScreenPos(mul(UNITY_MATRIX_MVP, v.vertex));
	o.ray = v.vertex - mul(_World2Object, float4(_WorldSpaceCameraPos, 1));
#else
	o.decalPos = v.texcoord.xy;
#endif
#if !defined(UNITY_UV_STARTS_AT_TOP) && (defined(_SKINNED) || defined(_STATIC))
	o.decalPos.y = 1 - o.decalPos.y;
#endif
}

void surf(Input IN, inout SurfaceOutputStandard o)
{
	float2 uv;
#ifdef _SCREENSPACE
	IN.screen.xy /= IN.screen.w;
	float sceneDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, IN.screen.xy);
	sceneDepth = LinearEyeDepth(sceneDepth);
	float3 decalPos = _LocalCameraPos + IN.ray * (sceneDepth / IN.screen.w);
	if (any(abs(decalPos.xyz) > 0.5)) discard;
	uv = decalPos.xy;
#else
	uv = IN.decalPos;
#endif

#ifdef _PARALLAXMAP
	uv += ParallaxOffset(tex2D(_ParallaxMap, uv).g, _Parallax, IN.viewDirForParallax);
#endif

#ifndef _SCREENSPACE
	if (any(uv != saturate(uv))) discard;
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

void FinalGBuffer(Input IN, SurfaceOutputStandard o,
	inout half4 diffuse, inout half4 specSmoothness, inout half4 normal, inout half4 emission)
{
	diffuse.a = o.Alpha;
	specSmoothness.a = o.Alpha;
	normal.a = o.Alpha;
	emission.a = o.Alpha;
}
