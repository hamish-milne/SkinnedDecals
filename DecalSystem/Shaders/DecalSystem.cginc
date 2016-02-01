#pragma once
#include "UnityCG.cginc"
#include "DecalLighting.cginc"

// The surface shader generator doesn't like
// some syntax, such as [unroll] and Buffer
#ifdef SHADER_API_D3D11
#define UNROLL [unroll]
#else
#undef _SKINNEDBUFFER
#define UNROLL
#endif

struct Input
{
#ifdef _PARALLAXMAP
	float3 viewDirForParallax : TEXCOORD0;
#endif
#ifdef _SCREENSPACE
	float4 screen : TEXCOORD1;
	float3 ray : TEXCOORD2;
#endif
#if defined(_FIXED4) || defined(_FIXED8)
	float4 decal1 : TEXCOORD3;
	float4 decal2 : TEXCOORD4;
	#ifdef _FIXED8
	float4 decal3 : TEXCOORD5;
	float4 decal4 : TEXCOORD6;
	#endif
#elif defined(_SCREENSPACE)
	float3 localCameraPos : TEXCOORD3;
#else
	float2 decalPos : TEXCOORD3;
#endif
};

// Define a new vertex shader input, allowing us to use SV_VertexID
// This is vital for the buffer-based method
struct new_appdata_full {
	float4 vertex : POSITION;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
	float4 texcoord : TEXCOORD0;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
#ifdef _SKINNEDBUFFER
	uint id : SV_VertexID;
#endif
};
#define appdata_full new_appdata_full

uniform float4 _Color;
uniform sampler2D _MainTex;

#ifdef _SKINNEDBUFFER
uniform Buffer<float2> _Buffer;

#elif defined(_SKINNEDUV)
uniform uint _UvChannel;

#elif defined(_FIXEDMULTI)
uniform float4x4 _Projectors[FIXED_COUNT];

#elif defined(_FIXEDSINGLE)
uniform float4x4 _Projector;

#elif defined(_SCREENSPACE)
uniform sampler2D_float _CameraDepthTexture;
#endif

#ifdef _NORMALMAP
uniform sampler2D _BumpMap;
uniform float _BumpScale;
#endif

#ifdef _METALLICGLOSSMAP
uniform sampler2D _MetallicGlossMap;
#else
uniform float _Glossiness;
uniform float _Metallic;
#endif

#ifdef _PARALLAXMAP
uniform float _Parallax;
uniform sampler2D _ParallaxMap;
#endif

#ifdef _EMISSION
uniform sampler2D _EmissionMap;
uniform float3 _EmissionColor;
#endif

// When using projection-based coordinates, they translate
// into UV coordinates differently depending on the API.
#ifndef UNITY_UV_STARTS_AT_TOP
#define FIX_ORIENTATION(UV) UV.y = 1 - UV.y
#else
#define FIX_ORIENTATION(UV)
#endif

float2 GetDecalPos(float4x4 mat, float4 vertex)
{
	float4 uv = UNITY_PROJ_COORD(mul(mat, vertex));
	float2 ret = (uv.xy / uv.w) + float2(0.5, 0.5);
	FIX_ORIENTATION(ret);
	return ret;
}

void vert(inout appdata_full v, out Input o)
{
	UNITY_INITIALIZE_OUTPUT(Input, o);
#ifdef _PARALLAXMAP
	TANGENT_SPACE_ROTATION;
	o.viewDirForParallax = mul(rotation, ObjSpaceViewDir(v.vertex));
#endif

#ifdef _SKINNEDBUFFER
	o.decalPos = _Buffer[v.id];
	FIX_ORIENTATION(o.decalPos);
#elif defined(_SKINNEDUV)
	float2 channels[8] = {
		v.texcoord.xy,
		v.texcoord.zw,
		v.texcoord1.xy,
		v.texcoord1.zw,
		v.texcoord2.xy,
		v.texcoord2.zw,
		v.texcoord3.xy,
		v.texcoord3.zw
	};
	o.decalPos = channels[_UvChannel];
	FIX_ORIENTATION(o.decalPos);
#elif defined(_FIXEDSINGLE)
	o.decalPos = GetDecalPos(_Projector, v.vertex);
#elif defined(_FIXED4) || defined(_FIXED8)

#define FIXED_DECAL(I, O) O = GetDecalPos(_Projectors[I], v.vertex)

	FIXED_DECAL(0, o.decal1.xy);
	FIXED_DECAL(1, o.decal1.zw);
	FIXED_DECAL(2, o.decal2.xy);
	FIXED_DECAL(3, o.decal2.zw);
	#ifdef _FIXED8
	FIXED_DECAL(4, o.decal3.xy);
	FIXED_DECAL(5, o.decal3.zw);
	FIXED_DECAL(6, o.decal4.xy);
	FIXED_DECAL(7, o.decal4.zw);
	#endif

#elif defined(_SCREENSPACE)
	o.screen = ComputeScreenPos(mul(UNITY_MATRIX_MVP, v.vertex));
	o.localCameraPos = mul(_World2Object, float4(_WorldSpaceCameraPos, 1));
	o.ray = v.vertex - o.localCameraPos;
#else
	o.decalPos = v.texcoord.xy;
#endif
}

#ifdef _PARALLAXMAP
#define OUTPUT_PARALLAX(UV) { UV += ParallaxOffset(tex2D(_ParallaxMap, UV).g, _Parallax, IN.viewDirForParallax); }
#else
#define OUTPUT_PARALLAX(UV)
#endif

#define OUTPUT_ALBEDO(UV, ALBEDO, ALPHA) { float4 _albedo = tex2D(_MainTex, UV) * _Color; ALBEDO = _albedo.rgb; ALPHA = _albedo.a; }

#ifdef _NORMALMAP
#define OUTPUT_NORMAL(UV, NORMAL) { NORMAL = UnpackScaleNormal(tex2D(_BumpMap, UV), _BumpScale); }
#else
#define OUTPUT_NORMAL(UV, NORMAL)
#endif

#ifdef _METALLICGLOSSMAP
#define OUTPUT_METALLIC(UV, SMOOTH, METAL) { float4 attribute = tex2D(_MetallicGlossMap, UV); SMOOTH = attribute.r; METAL = attribute.a; }
#else
#define OUTPUT_METALLIC(UV, SMOOTH, METAL) { SMOOTH = _Glossiness; METAL = _Metallic; }
#endif

#ifdef _EMISSION
#define OUTPUT_EMISSION(UV, EMISSION) { EMISSION = tex2D(_EmissionMap, UV) * _EmissionColor; }
#else
#define OUTPUT_EMISSION(UV, EMISSION)
#endif

float Linear01ToEyeDepth(float z)
{
	return (z * _ZBufferParams.x) /
		((z * (_ZBufferParams.w*_ZBufferParams.x - _ZBufferParams.y*_ZBufferParams.z)) + _ZBufferParams.z);
}

void CalculateUv(Input IN, inout float2 uv)
{
	uv = float2(0, 0);
#ifdef _SCREENSPACE
	IN.screen.xy /= IN.screen.w;
	float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, IN.screen.xy));
	float3 decalPos = IN.localCameraPos + IN.ray * (sceneDepth / IN.screen.w);
	if (any(abs(decalPos.xyz) > 0.5)) discard;
	uv = decalPos.xy + float2(0.5, 0.5);
#elif !defined(_FIXEDMULTI)
	uv = IN.decalPos;
	if (any(uv != saturate(uv))) discard;
#endif
}

#ifdef _FIXED4
#define DEFINE_DECAL_ARRAY float2 decalPos[FIXED_COUNT] = { \
	IN.decal1.xy, IN.decal1.zw, IN.decal2.xy, IN.decal2.zw }
#else
#define DEFINE_DECAL_ARRAY float2 decalPos[FIXED_COUNT] = { \
	IN.decal1.xy, IN.decal1.zw, IN.decal2.xy, IN.decal2.zw, IN.decal3.xy, IN.decal3.zw, IN.decal4.xy, IN.decal4.zw }
#endif

void surf(Input IN, inout DecalSurfaceOutputStandard o)
{
	float2 uv;
	CalculateUv(IN, uv);

#ifdef _FIXEDMULTI
	DEFINE_DECAL_ARRAY;

	UNROLL for (uint i = 0; i < FIXED_COUNT; i++)
	{
		float2 uv1 = decalPos[i];
		OUTPUT_PARALLAX(uv1);
		float alpha1, smooth1, metal1;
		float3 albedo1, normal1 = 0, emission1 = 0;
		OUTPUT_ALBEDO(uv1, albedo1, alpha1);
		OUTPUT_NORMAL(uv1, normal1);
		OUTPUT_METALLIC(uv1, smooth1, metal1);
		OUTPUT_EMISSION(uv1, emission1);
		float dstAlpha = 1 - alpha1;
		o.Alpha += alpha1;
		o.Albedo = (albedo1 * alpha1) + (o.Albedo * dstAlpha);
	#ifdef _NORMALMAP
		o.Normal = (normal1 * alpha1) + (o.Normal * dstAlpha);
	#endif
		o.Smoothness = (smooth1 * alpha1) + (o.Smoothness * dstAlpha);
		o.Metallic = (metal1 * alpha1) + (o.Metallic * dstAlpha);
	#ifdef _EMISSION
		o.Emission = (emission1 * alpha1) + (o.Emission * dstAlpha);
	#endif
	}
	o.Albedo /= o.Alpha;
	#ifdef _NORMALMAP
	o.Normal /= o.Alpha;
	#endif
	o.Smoothness /= o.Alpha;
	o.Metallic /= o.Alpha;
	o.Emission /= o.Alpha;
#else
	OUTPUT_PARALLAX(uv);
	OUTPUT_ALBEDO(uv, o.Albedo, o.Alpha);
	#ifdef _SCREENSPACE
		float3 nor = float3(0,0,1);
		OUTPUT_NORMAL(uv, nor);
		o.OutputNormal = normalize(mul(-nor, transpose((half3x3)_Object2World)));
	#else
		OUTPUT_NORMAL(uv, o.Normal);
	#endif
	OUTPUT_METALLIC(uv, o.Smoothness, o.Metallic);
	OUTPUT_EMISSION(uv, o.Emission);
#endif
}

// We need an extra pass for deferred to blend the alpha channels
// diffuse.a: occlusion
// smoothness.a: alpha
void SmoothnessFrag(Input IN, out half4 diffuse : SV_Target0, out half4 specSmoothness : SV_Target1)
{
	float2 uv;
	CalculateUv(IN, uv);
	float alpha = 0, smoothness = 0;

#ifdef _FIXEDMULTI
	DEFINE_DECAL_ARRAY;

	UNROLL for (uint i = 0; i < FIXED_COUNT; i++)
	{
		float2 uv1 = decalPos[i];
		OUTPUT_PARALLAX(uv1);
		float alpha1, smooth1, metal1;
		float3 albedo1;
		OUTPUT_ALBEDO(uv1, albedo1, alpha1);
		OUTPUT_METALLIC(uv1, smooth1, metal1);
		float dstAlpha = 1 - alpha1;
		alpha += alpha1;
		smoothness = (smooth1 * alpha1) + (smoothness * dstAlpha);
	}
#else
	float metallic;
	float3 albedo;
	OUTPUT_PARALLAX(uv);
	OUTPUT_ALBEDO(uv, albedo, alpha);
	OUTPUT_METALLIC(uv, smoothness, metallic);
	smoothness *= alpha;
#endif

	diffuse = float4(0, 0, 0, alpha);
	specSmoothness = float4(0, 0, 0, smoothness);
}

// The smoothness pass isn't a surface shader (for performance reasons)
// so we need to manually output the position here
void SmoothnessVert(in appdata_full v, out Input o, out float4 position : POSITION)
{
	vert(v, o);
	position = mul(UNITY_MATRIX_MVP, v.vertex);
}

void FinalGBuffer(Input IN, DecalSurfaceOutputStandard o,
	inout half4 diffuse, inout half4 specSmoothness, inout half4 normal, inout half4 emission)
{
#ifdef _NORMALMAP
	normal.xyz *= o.Alpha;
	normal.a = o.Alpha;
#else
	normal = float4(0, 0, 0, 0);
#endif

	diffuse.rgb *= o.Alpha;
	diffuse.a = o.Alpha;

	specSmoothness.rgb *= o.Alpha;
	specSmoothness.a = o.Alpha;

	emission.rgb *= o.Alpha;
	emission.a = o.Alpha;
}

void FinalColor(Input IN, DecalSurfaceOutputStandard o, inout half4 color)
{
	color.rgb *= o.Alpha;
}
