// The flora's leaves: leaf cards (alpha-clipped, from their atlas), a bush's dense middle (a flat colour), and each
// plant's last LOD, a billboard. Swaying in the level's wind and recoloured to its palette (FloraCommon.hlsl); lit with
// a normal map, and a normal rounded toward the canopy's (so a crown of cards shades as a crown, not card by card).
// Dithered between LODs (LOD Group cross-fade), and instanced (and DOTS-instanced, for the GPU Resident Drawer). Leaf cards are two-sided geometry already, so faces are
// culled as usual.
Shader "Meridian Motion/Showcase/Flora Leaf"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map (alpha: the leaves' shape)", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _LeafLuma ("Leaf Luma (the texture's brightest greens: the palette's tip colour)", Range(0.01, 1)) = 0.25
        _PaletteBlend ("Palette Blend (0: the texture's own greens, 1: the level's)", Range(0, 1)) = 1
        _Brightness ("Brightness (the leaves' colour, times)", Range(0, 2)) = 0.7
        _Wrap ("Wrap (light through thin leaves)", Range(0, 1)) = 0.25
        _Translucency ("Translucency (sun through the leaves, from behind)", Range(0, 1)) = 0.2
        _CanopyNormal ("Canopy Normal (normals rounded toward the canopy's)", Range(0, 1)) = 0.35
        _ShadowFloor ("Shadow Floor (sun a leaf in shadow still gets: light through the crown's gaps)", Range(0, 1)) = 0.45
        _Sway ("Sway (how far it moves in the wind, per metre of plant)", Range(0, 4)) = 1
        _Flutter ("Flutter (how much its leaves flutter)", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

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
            half _Cutoff;
            half _LeafLuma;
            half _PaletteBlend;
            half _Brightness;
            half _Wrap;
            half _Translucency;
            half _CanopyNormal;
            half _ShadowFloor;
            float _Sway;
            float _Flutter;
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
            float3 positionWS = FloraSway(TransformObjectToWorld(input.positionOS.xyz), normals.normalWS, input.colour, _Sway, _Flutter);
            o.positionWS = positionWS;
            o.positionCS = TransformWorldToHClip(positionWS);
            o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            o.normalWS = normals.normalWS;
            o.tangentWS = float4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            o.fog = ComputeFogFactor(o.positionCS.z);
            return o;
        }

        // The base map's colour and its alpha, clipped (and dithered between LODs).
        half4 Leaf(Varyings i)
        {
            half4 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
            clip(colour.a - _Cutoff);
            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(i.positionCS);
            #endif
            return colour;
        }

        // The normal: the map's, then rounded toward the canopy's (out from the plant's foot, a little up).
        float3 LeafNormal(Varyings i, bool front)
        {
            float3 tangent = normalize(i.tangentWS.xyz);
            float3 normal = normalize(i.normalWS) * (front ? 1.0 : -1.0);
            float3 bitangent = cross(normal, tangent) * i.tangentWS.w;
            half3 mapped = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
            normal = normalize(mapped.x * tangent + mapped.y * bitangent + mapped.z * normal);
            float3 canopy = i.positionWS - TransformObjectToWorld(float3(0.0, 0.0, 0.0)) + float3(0.0, 0.5, 0.0);
            return normalize(lerp(normal, normalize(canopy), _CanopyNormal));
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half4 Fragment(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 colour = Leaf(i);
                half3 albedo = FloraLeafColour(colour.rgb, _LeafLuma, _PaletteBlend) * _Brightness;
                return half4(FloraLight(albedo, i.positionWS, LeafNormal(i, front), _Wrap, _Translucency, _ShadowFloor, i.fog), 1.0);
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
                Leaf(i);
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half DepthFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Leaf(i);
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            half4 NormalsFragment(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Leaf(i);
                float3 normal = LeafNormal(i, front);
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
