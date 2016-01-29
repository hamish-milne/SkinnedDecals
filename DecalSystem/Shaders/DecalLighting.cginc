#pragma once
#include "UnityPBSLighting.cginc"

/*

This defines a custom lighting model, which is just a wrapper around the existing
Standard model. This allows us to output an extra parameter in the surface shader,
'OutputNormal'.

Why do we need this? Well, if a parameter called 'Normal' is written to in the
surface function, Unity will assume it's a tangent space normal, and convert it to
world space.

But this presents an issue: in screen space, the geometry doesn't really matter -
it's just a bounding box. We want to output the world normal directly and have full
control over it. The solution is to write to a different parameter, and then in
these lighting functions, decide which parameter to use as the world normal.

*/

struct DecalSurfaceOutputStandard
{
	fixed3 Albedo;		// base (diffuse or specular) color
	fixed3 Normal;		// tangent space normal, if written
	half3 Emission;
	half Metallic;		// 0=non-metal, 1=metal
	half Smoothness;	// 0=rough, 1=smooth
	half Occlusion;		// occlusion (default 1)
	fixed Alpha;		// alpha for transparencies

	half3 OutputNormal; // Override normal
};

#ifdef _SCREENSPACE
#define TRANSFER_OUTPUT SurfaceOutputStandard o; \
	o.Albedo = s.Albedo; \
	o.Normal = s.OutputNormal; \
	o.Emission = s.Emission; \
	o.Metallic = s.Metallic; \
	o.Smoothness = s.Smoothness; \
	o.Occlusion = s.Occlusion; \
	o.Alpha = s.Alpha;
#else
#define TRANSFER_OUTPUT SurfaceOutputStandard o; \
	o.Albedo = s.Albedo; \
	o.Normal = s.Normal; \
	o.Emission = s.Emission; \
	o.Metallic = s.Metallic; \
	o.Smoothness = s.Smoothness; \
	o.Occlusion = s.Occlusion; \
	o.Alpha = s.Alpha;
#endif

inline half4 LightingDecalStandard(DecalSurfaceOutputStandard s, half3 viewDir, UnityGI gi)
{
	TRANSFER_OUTPUT
	return LightingStandard(o, viewDir, gi);
}

inline half4 LightingDecalStandard_Deferred(DecalSurfaceOutputStandard s,
	half3 viewDir, UnityGI gi, out half4 outDiffuseOcclusion,
	out half4 outSpecSmoothness, out half4 outNormal)
{
	TRANSFER_OUTPUT
	return LightingStandard_Deferred(o, viewDir, gi, outDiffuseOcclusion, outSpecSmoothness, outNormal);
}

inline void LightingDecalStandard_GI(DecalSurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
{
	TRANSFER_OUTPUT
	UNITY_GI(gi, o, data);
}
