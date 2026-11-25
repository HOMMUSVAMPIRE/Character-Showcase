// The flora's bark: trunks, branches and stems, tiling bark with its normal map. Swaying in the level's wind with the
// leaves on it (FloraCommon.hlsl; a trunk's foot doesn't move), dithered between LODs, instanced.
Shader "Meridian Motion/Showcase/Flora Bark"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Wrap ("Wrap", Range(0, 1)) = 0.15
        _Sway ("Sway (how far it moves in the wind, per metre of plant)", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #include "FloraCommon.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Wrap;
            float _Sway;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float4 colour : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float4 tangentWS : TEXCOORD3;
            float fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings Vertex(Attributes input)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, o);
            VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            // Bark doesn't flutter: only the leaf cards' colour.g says how much, and bark's is 0.
            float3 positionWS = FloraSway(TransformObjectToWorld(input.positionOS.xyz), normals.normalWS, input.colour, _Sway, 0.0);
            o.positionWS = positionWS;
            o.positionCS = TransformWorldToHClip(positionWS);
            o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            o.normalWS = normals.normalWS;
            o.tangentWS = float4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            o.fog = ComputeFogFactor(o.positionCS.z);
            return o;
        }

        void Fade(Varyings i)
        {
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(i.positionCS);
            #endif
        }

        float3 BarkNormal(Varyings i)
        {
            float3 tangent = normalize(i.tangentWS.xyz);
            float3 normal = normalize(i.normalWS);
            float3 bitangent = cross(normal, tangent) * i.tangentWS.w;
            half3 mapped = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
            return normalize(mapped.x * tangent + mapped.y * bitangent + mapped.z * normal);
        }
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
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            half4 Fragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Fade(i);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                return half4(FloraLight(albedo, i.positionWS, BarkNormal(i), _Wrap, 0.0, 0.0, i.fog), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings ShadowVertex(Attributes input)
            {
                Varyings o = Vertex(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 toLight = normalize(_LightPosition - o.positionWS);
                #else
                    float3 toLight = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS, o.normalWS, toLight)));
                return o;
            }

            half ShadowFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Fade(i);
                return 0;
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
            #pragma fragment DepthFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            half DepthFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Fade(i);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment NormalsFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            half4 NormalsFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Fade(i);
                float3 normal = BarkNormal(i);
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
