// A GrassField's blades (see GrassField): drawn indirectly, an instance per blade, its vertices made here from the
// blade GrassBlades.compute wrote. A blade of n segments is 2n + 1 vertices: a pair at each segment's foot (left and
// right), and the tip. It curves forward along a quadratic Bezier by its bend and tapers to the tip. Lit by the main
// light (with its shadows), the ambient light and the fog; seen from both sides. It casts no shadow.
Shader "Meridian Motion/Showcase/Grass"
{
    Properties
    {
        _RootColour ("Root Colour", Color) = (0.24, 0.38, 0.13, 1)
        _TipColour ("Tip Colour", Color) = (0.55, 0.72, 0.28, 1)
        _ColourVariation ("Colour Variation", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Blade
        {
            float3 position;
            float facing;
            float length;
            float width;
            float bend;
            float hash;
        };

        StructuredBuffer<Blade> _Blades;
        uint _Segments;

        CBUFFER_START(UnityPerMaterial)
            half4 _RootColour;
            half4 _TipColour;
            half _ColourVariation;
        CBUFFER_END

        struct BladeVertex
        {
            float3 positionWS;
            float3 normalWS;
            float along;   // 0 at the root, 1 at the tip
            float hash;
        };

        // A blade's vertex (see the file's top).
        BladeVertex MakeVertex(uint vertexID, uint instanceID)
        {
            Blade blade = _Blades[instanceID];
            uint segment = vertexID >> 1;
            bool tip = vertexID >= 2 * _Segments;
            float side = tip ? 0.0 : ((vertexID & 1u) != 0 ? 1.0 : -1.0);
            float t = tip ? 1.0 : (float)segment / (float)_Segments;

            float s, c;
            sincos(blade.facing, s, c);
            float3 forward = float3(c, 0.0, s);
            float3 across = float3(-s, 0.0, c);
            float3 up = float3(0.0, 1.0, 0.0);

            // The Bezier: from the root, through a control point straight above it, to the tip, bent forward and so a
            // little lower (the blade keeps about its length).
            float h = blade.length;
            float lean = blade.bend * h;
            float3 p1 = up * h;
            float3 p2 = forward * lean + up * h * (1.0 - 0.4 * blade.bend * blade.bend);
            float u = 1.0 - t;
            float3 curve = 2.0 * u * t * p1 + t * t * p2;
            float3 tangent = normalize(2.0 * u * p1 + 2.0 * t * (p2 - p1) + float3(0.0, 1e-4, 0.0));

            float halfWidth = 0.5 * blade.width * (1.0 - t * t * 0.9) * (tip ? 0.0 : 1.0);

            BladeVertex v;
            v.positionWS = blade.position + curve + across * side * halfWidth;
            // The blade's face normal, tipped out to its sides so it shades as if rounded across, and half toward the
            // ground's (up): thin blades lit by their own normals alone go dark under a high sun, against the lawn.
            float3 normal = normalize(cross(across, tangent));
            v.normalWS = normalize(normalize(normal + across * side * 0.5) + up);
            v.along = t;
            v.hash = blade.hash;
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
                float2 alongHash : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings Vertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BladeVertex v = MakeVertex(vertexID, instanceID);
                Varyings o;
                o.positionWS = v.positionWS;
                o.positionCS = TransformWorldToHClip(v.positionWS);
                o.normalWS = v.normalWS;
                o.alongHash = float2(v.along, v.hash);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Fragment(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 normal = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                float along = i.alongHash.x;
                half3 albedo = lerp(_RootColour.rgb, _TipColour.rgb, along);
                albedo *= 1.0 + (i.alongHash.y - 0.5) * _ColourVariation;

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Wrapped, since thin blades let light through; darker toward the root, where the blades shade each other.
                half diffuse = saturate(dot(normal, light.direction) * 0.6 + 0.4);
                half3 lit = light.color * (diffuse * light.shadowAttenuation * light.distanceAttenuation);
                half3 colour = albedo * (lit + SampleSH(normal)) * lerp(0.75, 1.0, along);
                colour = MixFog(colour, i.fog);
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
