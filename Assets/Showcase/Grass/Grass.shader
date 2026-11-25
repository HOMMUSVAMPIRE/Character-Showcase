// A GrassField's blades (see GrassField): drawn indirectly, an instance per blade, its vertices made here from the
// blade GrassBlades.compute wrote. A draw's blades have up to _Segments segments: 2 _Segments + 1 vertices, a pair at
// each segment's foot (left and right) and the tip. A blade uses as many as its length needs (a segment per
// _SegmentLength), the rest folded into its tip, so short blades cost little and long ones curve smoothly. It curves
// forward along a quadratic Bezier by its bend, long blades drooping under their own weight, and tapers to the tip.
// Bowed by its push (the wind's, and what walks through it). Lit by the main light (with its shadows, and through the
// blade from behind), the ambient light and the fog; seen from both sides. The field's style sets its material's values
// every frame.
Shader "Meridian Motion/Showcase/Grass"
{
    Properties
    {
        _RootColour ("Root Colour", Color) = (0.21, 0.34, 0.11, 1)
        _TipColour ("Tip Colour", Color) = (0.5, 0.66, 0.26, 1)
        _ColourVariation ("Colour Variation", Range(0, 1)) = 0.25
        _ClumpTint ("Clump Tint", Range(0, 1)) = 0.3
        _RootShade ("Root Shade", Range(0, 1)) = 0.75
        _Translucency ("Translucency", Range(0, 1)) = 0.15
        _SegmentLength ("Segment Length", Float) = 0.06
        _Droop ("Droop", Range(0, 1)) = 0.1
        _DrawDistance ("Draw Distance", Float) = 60
        _DetailDistance ("Detail Distance", Float) = 14
        _ThinFrom ("Thin From", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GrassCommon.hlsl"

        StructuredBuffer<Blade> _Blades;
        uint _Segments;

        CBUFFER_START(UnityPerMaterial)
            half4 _RootColour;
            half4 _TipColour;
            half _ColourVariation;
            half _ClumpTint;
            half _RootShade;
            half _Translucency;
            float _SegmentLength;
            float _Droop;
            float _DrawDistance;
            float _DetailDistance;
            float _ThinFrom;
        CBUFFER_END

        struct BladeVertex
        {
            float3 positionWS;
            float3 normalWS;
            float along;   // 0 at the root, 1 at the tip
            float hash;
            float clump;
            float far;     // 0 where the grass is drawn in full, 1 at the draw distance (as the lawn's colour turns)
        };

        // A blade's vertex (see the file's top).
        BladeVertex MakeVertex(uint vertexID, uint instanceID)
        {
            Blade blade = _Blades[instanceID];
            float h = blade.length;
            uint segments = clamp((uint)ceil(h / max(_SegmentLength, 0.005)), 1u, _Segments);
            uint segment = vertexID >> 1;
            bool tip = vertexID >= 2u * _Segments || segment >= segments;
            float side = tip ? 0.0 : ((vertexID & 1u) != 0 ? 1.0 : -1.0);
            float t = tip ? 1.0 : (float)segment / (float)segments;

            float s, c;
            sincos(blade.facing, s, c);
            float3 forward = float3(c, 0.0, s);
            float3 across = float3(-s, 0.0, c);
            float3 up = float3(0.0, 1.0, 0.0);

            // The Bezier: from the root, through a control point above it, to the tip, bent forward and so a little
            // lower (the blade keeps about its length). Long blades droop: their tip falls further and reaches further.
            float droop = _Droop * h / (h + 0.25);
            float lean = (blade.bend + droop * 0.6) * h;
            float3 p2 = forward * lean + up * h * (1.0 - 0.4 * blade.bend * blade.bend - 0.55 * droop);
            // Straightening toward the detail distance, where near blades (curved) give way to far ones (a straight
            // triangle each), so they don't change shape all at once as the camera moves.
            float3 toCamera = _WorldSpaceCameraPos - blade.position;
            float distance = length(toCamera);
            float curved = _Segments > 1u ? saturate((_DetailDistance - distance) / max(0.3 * _DetailDistance, 0.01)) : 0.0;
            float3 p1 = lerp(0.5 * p2, up * h * (1.0 - 0.15 * droop), curved);
            // Pushed by the wind and by what walks through it (worked out per blade by GrassBlades.compute): the tip
            // moves with the push, the blade keeping its length, so a hard push bows it over rather than stretching it.
            float2 push = blade.push;
            float pushed = length(push);
            if (pushed > 1.5) { push *= 1.5 / pushed; }
            float3 offset = float3(push.x, 0.0, push.y) * h;
            float reach = length(p2);
            p2 = normalize(p2 + offset + float3(0.0, 1e-4, 0.0)) * reach;
            p1 += offset * 0.35;
            float u = 1.0 - t;
            float3 curve = 2.0 * u * t * p1 + t * t * p2;
            float3 tangent = normalize(2.0 * u * p1 + 2.0 * t * (p2 - p1) + float3(0.0, 1e-4, 0.0));

            // Seen edge-on, a blade's a sliver: widened then (by up to half), so the grass keeps its cover. And never
            // much under a pixel wide: thinner, a blade only covers some pixels' middles from frame to frame, and
            // flickers as the camera moves.
            float3 face = normalize(cross(across, tangent));
            float edgeOn = 1.0 - abs(dot(normalize(float3(toCamera.x, 0.0, toCamera.z) + 1e-5), forward));
            float taper = (1.0 - t * t * 0.9) * (tip ? 0.0 : 1.0);
            float pixel = 2.0 * distance / (UNITY_MATRIX_P[1][1] * _ScreenParams.y);
            float halfWidth = max(0.5 * blade.width * (1.0 + 0.5 * edgeOn * edgeOn), 0.6 * pixel) * taper;

            BladeVertex v;
            v.positionWS = blade.position + curve + across * side * halfWidth;
            // The face normal, tipped out to its sides so it shades as if rounded across, and toward the ground's (up):
            // half near the camera, all of it at the draw distance, where blades are a pixel or two and their own
            // normals only shimmer. (Thin blades lit by their own normals alone also go dark under a high sun.)
            float far = saturate(distance / max(_DrawDistance, 1.0));
            float3 rounded = normalize(face + across * side * 0.5);
            v.normalWS = normalize(lerp(normalize(rounded + up), up, far));
            v.along = t;
            v.hash = blade.hash;
            v.clump = blade.clump;
            v.far = smoothstep(_ThinFrom, _DrawDistance, distance);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 alongHashClump : TEXCOORD2;
                float fog : TEXCOORD3;
                float far : TEXCOORD4;
            };

            Varyings Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BladeVertex v = MakeVertex(vertexID, instanceID);
                Varyings o;
                o.positionWS = v.positionWS;
                o.positionCS = TransformWorldToHClip(v.positionWS);
                o.normalWS = v.normalWS;
                o.alongHashClump = float3(v.along, v.hash, v.clump);
                o.far = v.far;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Fragment(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                // Facing the camera either way, the rounded normal's up part kept.
                normal.y = abs(normal.y);
                // Clamped: interpolated across a triangle, 0 at the root can come out a hair below it, and pow of a
                // negative is NaN. One NaN pixel is enough for bloom to spread it into a flash across the screen (the
                // flashes the user saw, 2026-11-24: GrassFlashDiagnostic).
                float along = saturate(i.alongHashClump.x);
                // With distance, every blade turns to the colour of the grass as a whole, as the lawn under it does
                // (Lawn.shader): far off, a blade's bright tip against the ground between is only flicker.
                half3 albedo = lerp(lerp(_RootColour.rgb, _TipColour.rgb, pow(along, 0.8)), lerp(_RootColour.rgb, _TipColour.rgb, 0.55), i.far);
                albedo *= GrassClumpTint(i.alongHashClump.z, _ClumpTint);
                albedo *= 1.0 + (i.alongHashClump.y - 0.5) * _ColourVariation * (1.0 - i.far);

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Not times distanceAttenuation, as in Lawn.shader: the main light is the levels' sun.
                half shadow = light.shadowAttenuation;
                // Wrapped, since thin blades let light through; darker toward the root, where the blades shade each other.
                half diffuse = saturate(dot(normal, light.direction) * 0.6 + 0.4);
                // The sun through the blade, seen from the side away from it: strongest toward the tip, where it's thin.
                float3 toCamera = normalize(_WorldSpaceCameraPos - i.positionWS);
                half through = pow(saturate(dot(-toCamera, light.direction)), 3.0) * _Translucency * along * (1.0 - i.far);
                half3 lit = light.color * shadow * (diffuse + through);
                half3 colour = albedo * (lit + SampleSH(normal)) * lerp(lerp(_RootShade, 1.0, along), 1.0, i.far);
                colour = MixFog(colour, i.fog);
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
                BladeVertex v = MakeVertex(vertexID, instanceID);
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
                BladeVertex v = MakeVertex(vertexID, instanceID);
                Varyings o;
                o.positionCS = TransformWorldToHClip(v.positionWS);
                o.normalWS = v.normalWS;
                return o;
            }

            half4 Fragment(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                normal.y = abs(normal.y);
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
