// The showcase levels' height shading: a full-screen pass (a URP Full Screen Pass Renderer Feature, added to the
// project's renderers by TutorialRingAssets) that darkens what's low in the world and leaves what's high as it is: each
// pixel's colour is multiplied by a factor from _HeightShade.z at _HeightShade.x (the world height it's darkest at) up
// to 1 at _HeightShade.y, worked out from the depth buffer. The sky is left alone. Off unless _HeightShade.w is 1,
// which a scene's HeightShade component sets, so every other scene looks as it did.
Shader "Hidden/Meridian Motion/Showcase/Height Shade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "Height Shade"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // x: the height it's darkest at, y: the height it's untinted from, z: the darkest factor, w: 1 when on.
            float4 _HeightShade;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 colour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_HeightShade.w < 0.5) { return colour; }
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (depth <= 0.0) { return colour; }
                #else
                    if (depth >= 1.0) { return colour; }
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float t = saturate((world.y - _HeightShade.x) / max(0.001, _HeightShade.y - _HeightShade.x));
                colour.rgb *= lerp(_HeightShade.z, 1.0, t);
                return colour;
            }
            ENDHLSL
        }
    }
}
