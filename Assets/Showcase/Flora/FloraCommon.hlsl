// What the flora's shaders (FloraLeaf.shader, FloraBark.shader) share: swaying in the level's wind, the leaves'
// colours from the level's palette, and their lighting. Included after URP's Core.hlsl and Lighting.hlsl.
//
// The wind is the grass's (GrassWind publishes it as globals each frame, as _FloraWind...), so trees, bushes and
// grass move in the same gusts. How much each part of a plant moves is in its mesh's vertex colours, which the flora's
// generator (Tools/Flora/generate_flora.py) bakes: R how much it sways (0 at the trunk's foot, 1 at the top), G how much
// a leaf card flutters, B a phase per card. How much it all moves is the level's grass's Sway too (its style's), so
// one setting moves the grass and the flora alike.
//
// The palette is the level's grass's colours (GrassField publishes its style's root and tip colours as
// _FloraRootColour and _FloraTipColour, and _FloraPalette 1 while there are any): the leaves' greens are recoloured to
// them, so trees, bushes and lawns read as one garden. Other colours (flowers, bark) are left as they are; and without a
// palette (a level with no grass), the leaves keep their textures' own colours.
#ifndef MERIDIAN_FLORA_COMMON_INCLUDED
#define MERIDIAN_FLORA_COMMON_INCLUDED

#include "../Grass/GrassCommon.hlsl"

float4 _FloraWind;        // the wind's direction (x, z), strength, time (s)
float4 _FloraWindGusts;   // the gusts' size (m), speed (m/s), strength, and the flutter
float4 _FloraWindFlutter; // x: the flutter's speed (radians a second)
half4 _FloraRootColour, _FloraTipColour;
half _FloraPalette;
float _FloraSwayScale;    // the grass style's Sway (GrassField publishes it with the palette): 1 without grass

// Where a vertex is in the wind: the whole plant swaying with the gust at its foot (so its parts move together), by
// how much its part sways (colour.r) and the plant's size, and each leaf card fluttering along its normal.
float3 FloraSway(float3 positionWS, float3 normalWS, float4 colour, float sway, float flutter)
{
    float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float size = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
    float2 direction = _FloraWind.xy;
    float2 across = float2(-direction.y, direction.x);
    float2 gustAt = float2(dot(originWS.xz, direction) - _FloraWind.w * _FloraWindGusts.y, dot(originWS.xz, across)) / max(_FloraWindGusts.x, 0.01);
    float gust = GrassNoise(gustAt);
    // And a slow sway of each plant's own, so a still day isn't frozen.
    float own = sin(_FloraWind.w * 0.9 + dot(originWS.xz, float2(0.37, 0.61))) * 0.25;
    // As much as the level's grass sways (its style's Sway), so one setting moves both.
    float scale = _FloraPalette > 0.5 ? _FloraSwayScale : 1.0;
    float bend = _FloraWind.z * (0.35 + _FloraWindGusts.z * gust + own) * colour.r * sway * size * scale;
    // A leaf card's flutter: gentle and slow. Every card shaking at the wind's flutter rate, each at its own phase, read
    // as a crown in constant agitation, far busier than the grass beside it (the user, 2026-11-24).
    float wave = sin(_FloraWind.w * _FloraWindFlutter.x * 0.5 + colour.b * 6.2831853) * _FloraWindGusts.w * 0.35 * colour.g * flutter * size * scale;
    return positionWS + float3(direction.x, 0.0, direction.y) * bend + normalWS * wave;
}

// A leaf's colour: its greens recoloured to the palette (a gradient from the root colour, for the darkest, to the tip
// colour, at luma lumaRef), by how green it is; by blend.
half3 FloraLeafColour(half3 colour, half lumaRef, half blend)
{
    half green = saturate((colour.g - max(colour.r, colour.b)) * 8.0);
    half luma = dot(colour, half3(0.2126, 0.7152, 0.0722));
    half3 palette = lerp(_FloraRootColour.rgb, _FloraTipColour.rgb, saturate(luma / max(lumaRef, 0.01)));
    return lerp(colour, palette, green * blend * _FloraPalette);
}

// Lit by the main light (wrapped, as thin leaves let light through; with its shadows; and through the leaf from behind,
// by translucency), the ambient light, and the fog. Not times the main light's distanceAttenuation: for the main light
// it's only whether its culling mask takes the renderer in, which reads 0 for renderers with a property block (see
// docs/decisions.md, the grass's shaders); the main light is the levels' sun.
// shadowFloor: how much sun a part in shadow still gets (0: none). A crown of leaf cards shadows itself all through,
// which leaves most leaves lit by the sky alone (a cold grey-green); light gets through a real crown's gaps.
half3 FloraLight(half3 albedo, float3 positionWS, float3 normalWS, half wrap, half translucency, half shadowFloor, float fog)
{
    Light light = GetMainLight(TransformWorldToShadowCoord(positionWS));
    half diffuse = saturate(dot(normalWS, light.direction) * (1.0 - wrap) + wrap);
    float3 toCamera = normalize(_WorldSpaceCameraPos - positionWS);
    half through = pow(saturate(dot(-toCamera, light.direction)), 3.0) * translucency;
    half3 lit = light.color * lerp(shadowFloor, 1.0, light.shadowAttenuation) * (diffuse + through);
    return MixFog(albedo * (lit + SampleSH(normalWS)), fog);
}

#endif
