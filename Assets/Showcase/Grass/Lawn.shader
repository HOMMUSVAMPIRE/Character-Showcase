// The ground of a lawn, under a GrassField's blades. Near the camera it's what's seen between the blades: their root
// colour, shaded. Toward the grass's draw distance it turns to the colour of the grass as a whole (between root and tip),
// so where the blades thin out and stop, the lawn carries on as the same surface. Tinted by the same clumps as the blades
// over it (GrassCommon.hlsl), and mottled a little. The field sets its values from its style (a property block on each
// lawn renderer), so a lawn matches its grass.
Shader "Meridian Motion/Showcase/Lawn"
{
    Properties
    {
        _RootColour ("Root Colour", Color) = (0.21, 0.34, 0.11, 1)
        _TipColour ("Tip Colour", Color) = (0.5, 0.66, 0.26, 1)
        _ClumpSize ("Clump Size", Float) = 0.6
        _ClumpTint ("Clump Tint", Range(0, 1)) = 0.3
        _GroundShade ("Ground Shade", Range(0, 1)) = 0.7
        _ThinFrom ("Thin From", Float) = 8
        _DrawDistance ("Draw Distance", Float) = 60
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GrassCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _RootColour;
            half4 _TipColour;
            float _ClumpSize;
            half _ClumpTint;
            half _GroundShade;
            float _ThinFrom;
            float _DrawDistance;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Smooth value noise, 0 to 1, for the mottling.
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = GrassRandom(uint3(asuint((int)i.x), asuint((int)i.y), 31u)).x;
                float b = GrassRandom(uint3(asuint((int)i.x + 1), asuint((int)i.y), 31u)).x;
                float c = GrassRandom(uint3(asuint((int)i.x), asuint((int)i.y + 1), 31u)).x;
                float d = GrassRandom(uint3(asuint((int)i.x + 1), asuint((int)i.y + 1), 31u)).x;
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Fragment(Varyings i) : SV_Target
            {
                float3 normal = normalize(i.normalWS);
                float2 clumpCentre;
                float clump;
                GrassClump(i.positionWS.xz, _ClumpSize, clumpCentre, clump);
                half3 tint = GrassClumpTint(clump, _ClumpTint);
                half mottle = 0.92 + 0.16 * (0.65 * Noise(i.positionWS.xz * 0.7) + 0.35 * Noise(i.positionWS.xz * 3.1));

                half3 ground = _RootColour.rgb * _GroundShade;
                half3 grass = lerp(_RootColour.rgb, _TipColour.rgb, 0.55);
                float distance = length(_WorldSpaceCameraPos - i.positionWS);
                half3 albedo = lerp(ground, grass, smoothstep(_ThinFrom, _DrawDistance, distance)) * tint * mottle;

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Not times the light's distanceAttenuation: for the main light that's only whether its culling mask
                // takes the renderer in (unity_LightData.z), and it reads 0 for lawns given a property block (as the
                // field gives them), which left them unlit by the sun. The main light is the levels' sun.
                half3 lit = light.color * (saturate(dot(normal, light.direction)) * light.shadowAttenuation);
                half3 ambient = SampleSH(normal);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    ambient *= ao.indirectAmbientOcclusion;
                    lit *= ao.directAmbientOcclusion;
                #endif
                half3 colour = MixFog(albedo * (lit + ambient), i.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            float4 Vertex(Attributes input) : SV_POSITION { return TransformObjectToHClip(input.positionOS.xyz); }
            half Fragment(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 Fragment(Varyings i) : SV_Target
            {
                float3 normal = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octahedral = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
                #else
                    return half4(normal, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
