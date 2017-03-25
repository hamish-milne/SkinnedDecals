#pragma once
#include "UnityCG.cginc"
#include "UnityPBSLighting.cginc"

// Infer some defines from the shader keywords
#ifdef _FIXED4
#define _FIXEDMULTI
#define FIXED_COUNT (4)
#elif defined(_FIXED8)
#define _FIXEDMULTI
#define FIXED_COUNT (8)
#endif

#ifdef _PARALLAXMAP
#include "ParallaxOcclusion.cginc"
#endif

#ifdef _PARALLAXMAP
#define _NORMALMAP
#endif

// The surface shader generator doesn't like
// some syntax, such as [unroll] and Buffer
#ifdef SHADER_API_D3D11
#define UNROLL [unroll]
#else

// Massage the defines to work for the surface shader pass
#undef _SKINNEDBUFFER
#ifdef _FIXED8
#undef _FIXED8
#undef FIXED_COUNT
#define FIXED_COUNT 4
#endif

#define UNROLL
#endif

struct Input
{
#ifdef _PARALLAXMAP
	float4 viewDirForParallax : TEXCOORD0;
#endif
#ifdef _SCREENSPACE
	float4 screen : TEXCOORD1;
	float3 ray : TEXCOORD2;
#endif
#if defined(_FIXEDMULTI)
	// We need to store 3 components so we can clip the bounding box
	float4 decal1X : TEXCOORD3;
	float4 decal1Y : TEXCOORD4;
	float4 decal1Z : TEXCOORD5;
#ifdef _FIXED8
	float4 decal2X : TEXCOORD6;
	float4 decal2Y : TEXCOORD7;
	float4 decal2Z : TEXCOORD8;
#endif
#elif defined(_SCREENSPACE)
	float3 localCameraPos : TEXCOORD3;
#else
	float3 decalPos : TEXCOORD3;
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
#define M_PARALLAX _BDecalMatrix
#define M_TRANSFORM
uniform Buffer<float2> _Buffer;

#elif defined(_SKINNEDUV)
#define M_PARALLAX _PrSingle
#define M_TRANSFORM
uniform uint _UvChannel;

#elif defined(_FIXEDMULTI)
uniform float4x4 _PrMulti[FIXED_COUNT];

#elif defined(_FIXEDSINGLE)
#define M_PARALLAX _PrSingle

#elif defined(_SCREENSPACE)
uniform sampler2D_float _CameraDepthTexture;

#else // Default

#endif

#ifdef M_PARALLAX
uniform float4x4 M_PARALLAX;
#endif

#ifdef M_TRANSFORM
uniform float4x4 real_ObjectToWorld;
uniform float4x4 real_WorldToObject;
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
uniform uint _ParallaxSampleMin;
uniform uint _ParallaxSampleMax;
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

float3 GetDecalPos(float4x4 mat, float4 vertex)
{
	float4 uv = UNITY_PROJ_COORD(mul(mat, vertex));
	float3 ret = (uv.xyz / uv.w) + float3(0.5, 0.5, 0.5);
	FIX_ORIENTATION(ret);
	return ret;
}

void vert(inout appdata_full v, out Input o)
{
	UNITY_INITIALIZE_OUTPUT(Input, o);

#ifdef _SKINNEDBUFFER
	o.decalPos = float3(_Buffer[v.id], 0);
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
	o.decalPos = float3(channels[_UvChannel], 0);
	FIX_ORIENTATION(o.decalPos);
#elif defined(_FIXEDSINGLE)
	o.decalPos = GetDecalPos(_PrSingle, v.vertex);
#elif defined(_FIXEDMULTI)

#define FIXED_DECAL(I, A, B) { float3 p = GetDecalPos(_PrMulti[I], v.vertex); A##X.##B = p.x; A##Y.##B = p.y; A##Z.##B = p.z; }

	FIXED_DECAL(0, o.decal1, x);
	FIXED_DECAL(1, o.decal1, y);
	FIXED_DECAL(2, o.decal1, z);
	FIXED_DECAL(3, o.decal1, w);
#ifdef _FIXED8
	FIXED_DECAL(4, o.decal2, x);
	FIXED_DECAL(5, o.decal2, y);
	FIXED_DECAL(6, o.decal2, z);
	FIXED_DECAL(7, o.decal2, w);
#endif

#undef FIXED_DECAL

#elif defined(_SCREENSPACE)
	o.screen = ComputeScreenPos(mul(UNITY_MATRIX_MVP, v.vertex));
	o.localCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1));
	o.ray = v.vertex - o.localCameraPos;

	// Fix the vertex normal and tangent to always be top-facing
	v.normal = fixed3(0, 0, -1);
	v.tangent = fixed4(-1, 0, 0, -1);
#else
	o.decalPos = v.texcoord.xyz;
#endif

#if !defined(_SCREENSPACE) && !defined(_FIXEDMULTI)
	if (o.decalPos.x < -1e20) // Cull out of bounds vertices
		v.vertex = float4(0, 0, 0, 0);
#endif

#ifdef _PARALLAXMAP
	float4 pos = v.vertex;
	float3 normal = v.normal;
	float4 tangent = v.tangent;
	float3 binormal;
	float3 objSpaceCameraPos;

#ifdef M_TRANSFORM
	objSpaceCameraPos = mul(real_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
	pos = mul(real_WorldToObject, mul(unity_ObjectToWorld, pos));
	normal = mul(real_WorldToObject, mul(unity_ObjectToWorld, normal));
	#ifndef M_PARALLAX // Otherwise, the tangent is generated from the M_PARALLAX matrix
	tangent.xyz = mul(real_WorldToObject, mul(unity_ObjectToWorld, tangent.xyz));
	#endif
#else
	objSpaceCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
#endif

	float3 objSpaceViewDir = objSpaceCameraPos - pos.xyz;

	// For POM, UV space needs to match tangent space
	// In screen space, this is assured due to the geometry we're drawing
	// but for an arbitrary matrix, we need to manually bring it into decal space
	#ifdef M_PARALLAX
		tangent.xyz = normalize((float3)M_PARALLAX[0]);
		binormal    = normalize((float3)M_PARALLAX[1]) * unity_WorldTransformParams.w;
	#else
		binormal = cross(normalize(normal), normalize(tangent.xyz)) * tangent.w * unity_WorldTransformParams.w;
	#endif

	// Get tangent-space rotation matrix
	float3x3 rotation = float3x3(tangent.xyz, binormal, normal);

	float sampleRatio = 1-dot(normalize(objSpaceViewDir), normal);
	o.viewDirForParallax = float4(mul(rotation, objSpaceViewDir), sampleRatio);

#endif
}

#ifdef _PARALLAXMAP
#define OUTPUT_PARALLAX(UV) { UV.xy += parallax_offset(_Parallax, IN.viewDirForParallax.xyz, IN.viewDirForParallax.w, UV, _ParallaxMap, _ParallaxSampleMin, _ParallaxSampleMax); }
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
	if (any(IN.decalPos != saturate(IN.decalPos))) discard;
	uv = IN.decalPos.xy;
#endif
#if defined(_SCREENSPACE)
	uv = float2(1, 1) - uv;
#endif
}

void surf(Input IN, inout SurfaceOutputStandard o)
{

#ifdef _FIXEDMULTI
	float3 decalPos[FIXED_COUNT] = {
#define DECAL_ARRAY_ITEM(N, C) float3(N##X.##C, N##Y.##C, N##Z.##C)
		DECAL_ARRAY_ITEM(IN.decal1, x),
		DECAL_ARRAY_ITEM(IN.decal1, y),
		DECAL_ARRAY_ITEM(IN.decal1, z),
		DECAL_ARRAY_ITEM(IN.decal1, w)
#ifdef _FIXED8
		,
		DECAL_ARRAY_ITEM(IN.decal2, x),
		DECAL_ARRAY_ITEM(IN.decal2, y),
		DECAL_ARRAY_ITEM(IN.decal2, z),
		DECAL_ARRAY_ITEM(IN.decal2, w)
#endif
#undef DECAL_ARRAY_ITEM
	};

	UNROLL for (uint i = 0; i < FIXED_COUNT; i++)
	{
		float3 uv1 = decalPos[i];
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

	// For Forward, approximate blending of smoothness and metallic
	// by tending toward 0.5
#if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD) // Not sure if these defines are working..
	o.Smoothness = lerp(0.5, o.Smoothness, o.Alpha);
	o.Metallic = lerp(0.5, o.Metallic, o.Alpha);
#endif

	o.Albedo /= o.Alpha;
#ifdef _NORMALMAP
	o.Normal /= o.Alpha;
#endif
	o.Smoothness /= o.Alpha;
	o.Metallic /= o.Alpha;
	o.Emission /= o.Alpha;
#else


	float2 uv;
	CalculateUv(IN, uv);

	OUTPUT_PARALLAX(uv);
	OUTPUT_ALBEDO(uv, o.Albedo, o.Alpha);
	OUTPUT_NORMAL(uv, o.Normal);
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
	float3 decalPos[FIXED_COUNT] = {
#define DECAL_ARRAY_ITEM(N, C) float3(N##X.##C, N##Y.##C, N##Z.##C)
		DECAL_ARRAY_ITEM(IN.decal1, x),
		DECAL_ARRAY_ITEM(IN.decal1, y),
		DECAL_ARRAY_ITEM(IN.decal1, z),
		DECAL_ARRAY_ITEM(IN.decal1, w)
#ifdef _FIXED8
		,
		DECAL_ARRAY_ITEM(IN.decal2, x),
		DECAL_ARRAY_ITEM(IN.decal2, y),
		DECAL_ARRAY_ITEM(IN.decal2, z),
		DECAL_ARRAY_ITEM(IN.decal2, w)
#endif
#undef DECAL_ARRAY_ITEM
	};

	UNROLL for (uint i = 0; i < FIXED_COUNT; i++)
	{
		float3 uv1 = decalPos[i];
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

void FinalGBuffer(Input IN, SurfaceOutputStandard o,
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

void FinalColor(Input IN, SurfaceOutputStandard o, inout half4 color)
{
	color.rgb *= o.Alpha;
}
