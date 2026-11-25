// An IvyField's leaves (see IvyField): drawn indirectly, an instance per leaf, its vertices made here from the leaf
// IvyLeaves.compute wrote. Near the camera a leaf is an ivy leaf's outline (a fan of six triangles round its middle,
// its sides folded back a little along the midrib); far off, a flat diamond of two. It hangs from where its stalk meets it,
// tilted out from the wall, and flutters in the level's wind (the grass's: FloraCommon.hlsl). Lit as the flora is
// (FloraLight: wrapped, through the leaf, a floor to its shadow), with a waxy sheen; coloured between the field's
// Dark and Light colours, by the leaf and by a broad noise over the wall, and toward the level's grass palette by
// Palette Blend; seen from both sides.
Shader "Meridian Motion/Showcase/Ivy"
{
    Properties
    {
        _DarkColour ("Dark Colour", Color) = (0.13, 0.25, 0.07, 1)
        _LightColour ("Light Colour", Color) = (0.36, 0.52, 0.17, 1)
        _ColourVariation ("Colour Variation", Range(0, 1)) = 0.5
        _PaletteBlend ("Palette Blend", Range(0, 1)) = 0.4
        _LeafLuma ("Leaf Luma (the palette's tip colour is matched at)", Range(0.05, 1)) = 0.3
        _Wrap ("Wrap", Range(0, 1)) = 0.3
        _Translucency ("Translucency", Range(0, 1)) = 0.2
        _ShadowFloor ("Shadow Floor", Range(0, 1)) = 0.35
        _Gloss ("Gloss", Range(0, 1)) = 0.25
        _Fold ("Fold", Range(0, 0.5)) = 0.18
        _Flutter ("Flutter", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "../Flora/FloraCommon.hlsl"
        #include "IvyCommon.hlsl"

        StructuredBuffer<Leaf> _Leaves;
        uint _Far;

        CBUFFER_START(UnityPerMaterial)
            half4 _DarkColour;
            half4 _LightColour;
            half _ColourVariation;
            half _PaletteBlend;
            half _LeafLuma;
            half _Wrap;
            half _Translucency;
            half _ShadowFloor;
            half _Gloss;
            float _Fold;
            float _Flutter;
        CBUFFER_END

        // A leaf's outline, in its own space (x across, y from its base to its tip, its length 1): its middle, then
        // round from its base, the two lobes either side and its tip. Far off, a diamond over most of it (a spiky
        // triangle read as thorns from across a lane).
        static const float2 NearShape[7] =
        {
            float2(0.0, 0.45), float2(0.0, 0.0), float2(-0.42, 0.2), float2(-0.32, 0.64), float2(0.0, 1.0), float2(0.32, 0.64), float2(0.42, 0.2),
        };
        static const float2 FarShape[4] = { float2(0.0, 0.0), float2(-0.48, 0.42), float2(0.0, 1.0), float2(0.48, 0.42) };

        struct LeafVertex
        {
            float3 positionWS;
            float3 normalWS;
            float hash;
            float shade;
        };

        LeafVertex MakeVertex(uint vertexID, uint instanceID)
        {
            Leaf leaf = _Leaves[instanceID];
            bool far = _Far != 0u;
            float2 p = far ? FarShape[min(vertexID, 3u)] : NearShape[min(vertexID, 6u)];
            // Far off, flat: a fold too small to see would only turn its normals, and darken it against the near leaves.
            float fold = far ? 0.0 : _Fold;
            float st, ct;
            sincos(leaf.tilt, st, ct);
            // Tilted out: its length leaning away from the wall, its face turned up toward the light.
            float3 lengthWise = leaf.down * ct + leaf.normal * st;
            float3 faceNormal = leaf.normal * ct - leaf.down * st;
            float3 across = normalize(cross(leaf.normal, leaf.down));

            // A flutter in the wind, at the tip: gentle and slow, as the flora's leaves (FloraSway's flutter).
            float scale = _FloraPalette > 0.5 ? _FloraSwayScale : 1.0;
            float wave = sin(_FloraWind.w * _FloraWindFlutter.x * 0.5 + leaf.hash * 6.2831853) * _FloraWindGusts.w * _Flutter * scale * leaf.sway;

            LeafVertex v;
            v.positionWS = leaf.position + (lengthWise * p.y + across * p.x) * leaf.size
                + faceNormal * (wave * p.y - fold * abs(p.x)) * leaf.size;
            // The fold's normals: each side turned a little out from the midrib.
            v.normalWS = normalize(faceNormal + across * (p.x * fold * 2.0));
            v.hash = leaf.hash;
            v.shade = leaf.shade;
            return v;
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
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 hashShade : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                LeafVertex v = MakeVertex(vertexID, instanceID);
                Varyings o;
                o.positionWS = v.positionWS;
                o.positionCS = TransformWorldToHClip(v.positionWS);
                o.normalWS = v.normalWS;
                o.hashShade = float2(v.hash, v.shade);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Fragment(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                // Between the dark and light colours: by the leaf, and by a broad noise over the wall (sunnier and
                // shadier patches of growth), so a wall of it isn't one even green.
                float broad = GrassNoise(float2(i.positionWS.x + i.positionWS.z, i.positionWS.y) * 0.35);
                float t = saturate(0.5 + (i.hashShade.x - 0.5) * _ColourVariation * 1.6 + (broad - 0.5) * 0.8);
                half3 albedo = lerp(_DarkColour.rgb, _LightColour.rgb, t) * saturate(i.hashShade.y);
                albedo = FloraLeafColour(albedo, _LeafLuma, _PaletteBlend);
                half3 colour = FloraLight(albedo, i.positionWS, normal, _Wrap, _Translucency, _ShadowFloor, i.fog);

                // A waxy sheen: ivy leaves are glossy. Saturated before pow, so it's never of a negative.
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 toCamera = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 halfway = normalize(light.direction + toCamera);
                half sheen = pow(saturate(dot(normal, halfway)), 24.0) * _Gloss * light.shadowAttenuation;
                // Faded into the fog as the leaf's colour is.
                colour += light.color * sheen * 0.5 * ComputeFogIntensity(i.fog);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
            {
                LeafVertex v = MakeVertex(vertexID, instanceID);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 toLight = normalize(_LightPosition - v.positionWS);
                #else
                    float3 toLight = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(v.positionWS, v.normalWS, toLight));
                return ApplyShadowClamping(positionCS);
            }

            half Fragment() : SV_Target { return 0; }
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

            float4 Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
            {
                return TransformWorldToHClip(MakeVertex(vertexID, instanceID).positionWS);
            }

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

            Varyings Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                LeafVertex v = MakeVertex(vertexID, instanceID);
                Varyings o;
                o.positionCS = TransformWorldToHClip(v.positionWS);
                o.normalWS = v.normalWS;
                return o;
            }

            half4 Fragment(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(i.normalWS) * (front ? 1.0 : -1.0);
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
