// The levels' water (Graybox.Surface.Water): a see-through surface whose whole look is built from noise in world space,
// with no textures, as the NoiseConcrete shader builds the levels' other surfaces. Layers of value noise, drifting
// different ways, make a rippling height field: its slopes bend the surface's normal (and its broadest layer the surface
// itself, a little), which shapes the sky's reflection and the sun's glints. The camera's depth texture says how much
// water is under each point: shallows show what's below, tinted, and deep water darkens to the deep colour, with foam
// where the water meets what stands in it, broken up by noise. Seen from under the surface it's a murky ceiling.
//
// The depth effects need the render pipeline asset's Depth Texture on (it is on the PC asset). Without it, all the water
// looks deep and there's no foam.
Shader "Character Showcase/Water"
{
    Properties
    {
        [Header(Colour)]
        _ShallowColour("Shallow Colour", Color) = (0.16, 0.52, 0.6, 1)
        _DeepColour("Deep Colour", Color) = (0.02, 0.13, 0.22, 1)
        _DepthToDeep("Depth To Deep (m)", Float) = 3
        _ShallowOpacity("Shallow Opacity", Range(0, 1)) = 0.35
        _DeepOpacity("Deep Opacity", Range(0, 1)) = 0.93

        [Header(Ripples)]
        _RippleSize("Ripple Size (m)", Float) = 1.6
        _RippleSpeed("Ripple Speed (m per s)", Float) = 0.4
        _RippleStrength("Ripple Strength", Range(0, 3)) = 0.6
        _WaveHeight("Wave Height (m)", Float) = 0.04

        [Header(Surface)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.92
        _Reflection("Sky Reflection", Range(0, 1)) = 0.9
        _Glints("Sun Glints", Range(0, 4)) = 1.5

        [Header(Foam)]
        _FoamColour("Foam Colour", Color) = (0.9, 0.95, 0.97, 1)
        _FoamDistance("Foam Distance (m)", Float) = 0.15
        _FoamBreakup("Foam Breakup", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Both sides: from under the water, its surface is a murky ceiling.
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColour, _DeepColour, _FoamColour;
                float _DepthToDeep, _ShallowOpacity, _DeepOpacity;
                float _RippleSize, _RippleSpeed, _RippleStrength, _WaveHeight;
                float _Smoothness, _Reflection, _Glints;
                float _FoamDistance, _FoamBreakup;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- Noise ----

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise (0 to 1) and its slope (d/dx, d/dy), smoothly interpolated between random values at whole
            // coordinates: the slope comes out exactly, so normals need no extra samples.
            float3 NoiseWithSlope(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f), du = 6.0 * f * (1.0 - f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                float k1 = b - a, k2 = c - a, k4 = a - b - c + d;
                return float3(a + k1 * u.x + k2 * u.y + k4 * u.x * u.y, du * float2(k1 + k4 * u.y, k2 + k4 * u.x));
            }

            // One layer of ripples: noise at a scale (cells per ripple size), turned so the layers' grids don't line up,
            // drifting its own way. Returns its height (0 to 1) and slope in the water's metres.
            float3 RippleLayer(float2 xz, float scale, float2x2 turn, float2 drift, float t)
            {
                float2 p = mul(turn, xz) * (scale / _RippleSize) + drift * (t * _RippleSpeed * scale / _RippleSize);
                float3 n = NoiseWithSlope(p);
                return float3(n.x, mul(transpose(turn), n.yz) * (scale / _RippleSize));
            }

            // The rippling height field (about 0 to 1) and its slope (per metre), from three layers, each finer, weaker
            // and quicker than the last.
            float3 Ripples(float2 xz, float t)
            {
                const float2x2 turn1 = float2x2(1, 0, 0, 1);
                const float2x2 turn2 = float2x2(0.8, -0.6, 0.6, 0.8);
                const float2x2 turn3 = float2x2(0.28, 0.96, -0.96, 0.28);
                float3 h = RippleLayer(xz, 1.0, turn1, float2(1.0, 0.45), t) * 0.55;
                h += RippleLayer(xz, 2.3, turn2, float2(-0.7, 1.0), t) * 0.3;
                h += RippleLayer(xz, 5.1, turn3, float2(0.4, -1.0), t) * 0.15;
                return h;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // The broadest ripples lift and lower the surface a little (about its middle, so the water's level holds).
                positionWS.y += (RippleLayer(positionWS.xz, 1.0, float2x2(1, 0, 0, 1), float2(1.0, 0.45), _Time.y).x - 0.5) * _WaveHeight;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 positionWS = input.positionWS;
                float3 view = normalize(GetWorldSpaceViewDir(positionWS));

                float3 ripples = Ripples(positionWS.xz, _Time.y);
                float2 slope = ripples.yz * _RippleStrength * 0.25;
                float3 normal = normalize(float3(-slope.x, 1.0, -slope.y));
                if (!front) { normal = -normal; }

                Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half3 sunLight = sun.color * (sun.distanceAttenuation * sun.shadowAttenuation);
                half3 ambient = SampleSH(normal);

                // How much water there is under this point: from the scene's depth behind it, along the view.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = -TransformWorldToView(positionWS).z;
                float under = max(0.0, sceneDepth - surfaceDepth);
                float deep = saturate(under / max(_DepthToDeep, 0.01));
                deep = 1.0 - (1.0 - deep) * (1.0 - deep); // Darkening quickly at first, as light fades in water.

                // The water itself: tinted from the shallows' colour to the deep colour, lit by the sky and a little sun.
                half3 body = lerp(_ShallowColour.rgb, _DeepColour.rgb, deep) * (ambient + sunLight * 0.35 * saturate(dot(normal, sun.direction)));
                half alpha = lerp(_ShallowOpacity, _DeepOpacity, deep);

                half3 colour;
                if (front)
                {
                    // The sky's reflection, stronger at a glancing look (Schlick's Fresnel), and the sun's glints.
                    half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(normal, view)), 5.0);
                    half roughness = 1.0 - _Smoothness;
                    // Only the sky above the horizon: a ripple steep enough to reflect below it would show the sky's
                    // ground colour, which no water reflects (it would see the far bank).
                    float3 reflected = reflect(-view, normal);
                    reflected.y = max(reflected.y, 0.02);
                    half3 sky = GlossyEnvironmentReflection(normalize(reflected), roughness, 1.0);
                    colour = lerp(body, sky, fresnel * _Reflection);
                    alpha = max(alpha, fresnel * _Reflection);
                    float3 halfway = normalize(sun.direction + view);
                    float shininess = exp2(10.0 * _Smoothness + 1.0);
                    colour += sunLight * pow(saturate(dot(normal, halfway)), shininess) * _Glints;

                    // Foam where the water meets what stands in it, broken up by noise into patches that drift.
                    float edge = 1.0 - saturate(under / max(_FoamDistance, 0.001));
                    float breakup = NoiseWithSlope(positionWS.xz * 3.0 + _Time.y * 0.3).x;
                    float foam = saturate((edge - breakup * _FoamBreakup) * 4.0);
                    colour = lerp(colour, _FoamColour.rgb * (ambient + sunLight), foam);
                    alpha = max(alpha, foam);
                }
                else
                {
                    // From below: the deep colour, a little sunlight through it.
                    colour = _DeepColour.rgb * (ambient + sunLight * 0.5);
                    alpha = max(alpha, 0.75);
                }

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
