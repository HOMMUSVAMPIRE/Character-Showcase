// Water effects' particles (see WaterEffects): soft droplets and rings of foam, their shapes built from each particle's
// UVs and broken up by noise, with no textures. Ring Width 0 draws a filled droplet; above 0, a ring that wide (as a share
// of the particle's radius), as a ripple on the surface. Tinted by the particle's colour (its fade over its life). Drawn
// after the water (its queue is a little later), so a ripple shows on the surface rather than under it.
Shader "Meridian Motion/Water Effect"
{
    Properties
    {
        _Colour("Colour", Color) = (0.92, 0.96, 1, 1)
        _RingWidth("Ring Width", Range(0, 1)) = 0
        _Softness("Softness", Range(0.01, 1)) = 0.35
        _Breakup("Noise Breakup", Range(0, 1)) = 0.5
        _BreakupScale("Noise Scale", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Colour;
                float _RingWidth, _Softness, _Breakup, _BreakupScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 colour : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 colour : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth value noise, 0 to 1.
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.colour = input.colour;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centred = input.uv * 2.0 - 1.0;
                float r = length(centred);
                // The outer edge, softened inward; a ring also has an inner edge, its width in from the outer.
                float outer = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                float inner = _RingWidth > 0.0 ? smoothstep(1.0 - _RingWidth - _Softness, 1.0 - _RingWidth, r) : 1.0;
                float shape = outer * inner;
                // Broken up by noise round the shape (by angle) and over the world, so no two look alike.
                float angle = atan2(centred.y, centred.x);
                float noise = Noise(float2(angle * _BreakupScale / 6.2832 * 4.0, r * _BreakupScale) + input.positionWS.xz * 0.7);
                shape *= lerp(1.0, saturate(noise * 1.6 - 0.2), _Breakup);
                half4 colour = _Colour * input.colour;
                colour.a *= shape;
                colour.rgb = MixFog(colour.rgb, input.fogFactor);
                return colour;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
