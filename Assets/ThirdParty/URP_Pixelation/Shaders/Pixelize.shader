Shader "Hidden/Pixelize"
{
    // URP 17(Unity 6)의 RenderGraph Blit 규약을 따릅니다.
    // 정점은 Blit.hlsl의 Vert(SV_VertexID 기반 풀스크린 삼각형)가 생성합니다.
    // 예전처럼 TransformObjectToHClip으로 카메라 VP를 곱하면 쿼드가 화면 밖으로
    // 날아가 아무것도 그려지지 않고 화면이 검게 나옵니다.
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off

        Pass
        {
            Name "Pixelation"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Vert, Varyings, _BlitTexture, _BlitMipLevel, sampler_PointClamp를 제공합니다.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            uniform float2 _BlockCount;
            uniform float2 _BlockSize;
            uniform float2 _HalfBlockSize;

            float4 Frag(Varyings input) : SV_Target0
            {
                // XR 플랫폼의 텍스처 배열 처리 차이를 보정합니다.
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 화면을 _BlockCount 개의 블록으로 나누고 각 블록의 중심색을 취합니다.
                float2 blockPos = floor(input.texcoord.xy * _BlockCount);
                float2 blockCenter = blockPos * _BlockSize + _HalfBlockSize;

                // 픽셀이 뭉개지지 않도록 최근접(Point) 샘플러를 사용합니다.
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, blockCenter, _BlitMipLevel);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
