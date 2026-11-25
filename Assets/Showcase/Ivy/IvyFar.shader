// An IvyField's far layer (see IvyField): a quad over each wall face, a little out from it, coloured as the ivy is where
// its mask says it grows, so far off (where the leaves have thinned out and stopped, at the style's Draw Distance) a wall
// still carries its ivy, as a lawn carries on under the grass where the blades stop. It comes in, dithered, over the end
// of the draw distance (the style's Far Fade), while the last leaves shrink away; nearer, it's not drawn at all. Its
// edge follows the mask, broken up by a noise, so it's as uneven as the growth. The face's mask layer and the share of
// the layer its mask fills are in the quad's uv (z, and uv1). Lit as the leaves are (FloraLight), facing out from the wall.
Shader "Meridian Motion/Showcase/Ivy Far"
{
    Properties
    {
        _DarkColour ("Dark Colour", Color) = (0.13, 0.25, 0.07, 1)
        _LightColour ("Light Colour", Color) = (0.36, 0.52, 0.17, 1)
        _PaletteBlend ("Palette Blend", Range(0, 1)) = 0.4
        _LeafLuma ("Leaf Luma", Range(0.05, 1)) = 0.3
        _Wrap ("Wrap", Range(0, 1)) = 0.3
        _ShadowFloor ("Shadow Floor", Range(0, 1)) = 0.35
        _DrawDistance ("Draw Distance", Float) = 90
        _FarFade ("Far Fade", Range(0.05, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../Flora/FloraCommon.hlsl"

            TEXTURE2D_ARRAY(_IvyMask); SAMPLER(sampler_IvyMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColour;
                half4 _LightColour;
                half _PaletteBlend;
                half _LeafLuma;
                half _Wrap;
                half _ShadowFloor;
                float _DrawDistance;
                float _FarFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 uv : TEXCOORD0;   // across the face (0 to 1), and its mask's layer
                float2 share : TEXCOORD1; // the share of the layer its mask fills
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 maskUV : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.maskUV = float3(input.uv.xy * input.share, input.uv.z);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Fragment(Varyings i) : SV_Target
            {
                // Only where the leaves have thinned out: in over the end of the draw distance, dithered (a hash of the
                // pixel), and in full beyond it.
                float distance = length(i.positionWS - _WorldSpaceCameraPos);
                float fadeFrom = _DrawDistance * (1.0 - _FarFade);
                float shown = saturate((distance - fadeFrom) / max(_DrawDistance - fadeFrom, 0.01));
                float dither = GrassRandom(uint3(asuint(i.positionCS.x), asuint(i.positionCS.y), 7u)).x;
                clip(shown - dither);

                float density = SAMPLE_TEXTURE2D_ARRAY(_IvyMask, sampler_IvyMask, i.maskUV.xy, i.maskUV.z).r;
                // Its edge as uneven as the growth's: where the mask's thin, a noise over the wall decides.
                float2 across = float2(i.positionWS.x + i.positionWS.z, i.positionWS.y);
                float broad = GrassNoise(across * 0.35), fine = GrassNoise(across * 3.1);
                clip(density - lerp(0.2, 0.5, fine));

                float t = saturate(0.45 + (broad - 0.5) * 0.8);
                half3 albedo = lerp(_DarkColour.rgb, _LightColour.rgb, t) * lerp(0.72, 0.9, density);
                albedo = FloraLeafColour(albedo, _LeafLuma, _PaletteBlend);
                half3 colour = FloraLight(albedo, i.positionWS, normalize(i.normalWS), _Wrap, 0.0, _ShadowFloor, i.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
