// The Interact target's outline (TinCan.Features.TargetOutline). Pass 0 draws the marked renderers white into the mask;
// pass 1 runs full screen and draws the outline colour on pixels outside the mask that have mask within the width.
Shader "Hidden/TinCan/TargetOutline"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Mask"

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            MaskVaryings MaskVert(MaskAttributes input)
            {
                MaskVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 MaskFrag(MaskVaryings input) : SV_Target
            {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Edge"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment EdgeFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _OutlineColor;
            float _OutlineWidth;

            float Mask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
            }

            half4 EdgeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (Mask(uv) > 0.5) discard; // inside the object: leave it as it is

                // Two rings of eight taps (full and half width) catch thin parts without a wide kernel.
                float2 texel = _BlitTexture_TexelSize.xy * _OutlineWidth;
                float near = 0;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float angle = i * 0.78539816;
                    float2 direction = float2(cos(angle), sin(angle));
                    near = max(near, Mask(uv + direction * texel));
                    near = max(near, Mask(uv + direction * texel * 0.5));
                }
                if (near < 0.5) discard;
                return half4(_OutlineColor.rgb, _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
