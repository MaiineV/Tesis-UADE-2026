Shader "Custom/Pixelation"
{
    Properties
    {
        _PixelSize("Pixel Size", Float) = 6
        _NormalEdgeStrength("Normal Edge Strength", Float) = 0.3
        _DepthEdgeStrength("Depth Edge Strength", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Pixelation"
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _PixelSize;
            float _NormalEdgeStrength;
            float _DepthEdgeStrength;
            float4 _PixelationScreenSize;

            float getDepth(float2 baseUV, int x, int y)
            {
                return SampleSceneDepth(baseUV + float2(x, y) * _PixelationScreenSize.zw * _PixelSize);
            }

            float3 getNormal(float2 baseUV, int x, int y)
            {
                return SampleSceneNormals(baseUV + float2(x, y) * _PixelationScreenSize.zw * _PixelSize);
            }

            float depthEdgeIndicator(float2 baseUV, float depth)
            {
                float diff = 0.0;
                // In reversed-Z (Unity default on PC): near=1, far=0
                // Farther neighbors have smaller depth, so flip subtraction
                #if UNITY_REVERSED_Z
                    diff += clamp(depth - getDepth(baseUV, 1, 0), 0.0, 1.0);
                    diff += clamp(depth - getDepth(baseUV, -1, 0), 0.0, 1.0);
                    diff += clamp(depth - getDepth(baseUV, 0, 1), 0.0, 1.0);
                    diff += clamp(depth - getDepth(baseUV, 0, -1), 0.0, 1.0);
                #else
                    diff += clamp(getDepth(baseUV, 1, 0) - depth, 0.0, 1.0);
                    diff += clamp(getDepth(baseUV, -1, 0) - depth, 0.0, 1.0);
                    diff += clamp(getDepth(baseUV, 0, 1) - depth, 0.0, 1.0);
                    diff += clamp(getDepth(baseUV, 0, -1) - depth, 0.0, 1.0);
                #endif
                return floor(smoothstep(0.01, 0.02, diff) * 2.0) / 2.0;
            }

            float neighborNormalEdgeIndicator(float2 baseUV, int x, int y, float depth, float3 normal)
            {
                float depthDiff = getDepth(baseUV, x, y) - depth;
                float3 neighborNormal = getNormal(baseUV, x, y);

                float3 normalEdgeBias = float3(1.0, 1.0, 1.0);
                float normalDiff = dot(normal - neighborNormal, normalEdgeBias);
                float normalIndicator = clamp(smoothstep(-0.01, 0.01, normalDiff), 0.0, 1.0);

                // depthIndicator: only the shallower pixel should detect normal edge
                // In reversed-Z, shallower = higher value, so negative depthDiff means current is shallower
                #if UNITY_REVERSED_Z
                    float depthIndicator = clamp(sign(-depthDiff * 0.25 + 0.0025), 0.0, 1.0);
                #else
                    float depthIndicator = clamp(sign(depthDiff * 0.25 + 0.0025), 0.0, 1.0);
                #endif

                return (1.0 - dot(normal, neighborNormal)) * depthIndicator * normalIndicator;
            }

            float normalEdgeIndicator(float2 baseUV, float depth, float3 normal)
            {
                float indicator = 0.0;
                indicator += neighborNormalEdgeIndicator(baseUV, 0, -1, depth, normal);
                indicator += neighborNormalEdgeIndicator(baseUV, 0, 1, depth, normal);
                indicator += neighborNormalEdgeIndicator(baseUV, -1, 0, depth, normal);
                indicator += neighborNormalEdgeIndicator(baseUV, 1, 0, depth, normal);
                return step(0.1, indicator);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Pixelate UV
                float2 uv = input.texcoord;
                float2 screenPixel = uv * _PixelationScreenSize.xy;
                float2 snappedPixel = floor(screenPixel / _PixelSize) * _PixelSize + _PixelSize * 0.5;
                float2 pixelatedUV = snappedPixel * _PixelationScreenSize.zw;

                half4 texel = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, pixelatedUV, 0);

                float depth = 0.0;
                float3 normal = float3(0.0, 0.0, 0.0);

                if (_DepthEdgeStrength > 0.0 || _NormalEdgeStrength > 0.0)
                {
                    depth = getDepth(pixelatedUV, 0, 0);
                    normal = getNormal(pixelatedUV, 0, 0);
                }

                float dei = 0.0;
                if (_DepthEdgeStrength > 0.0)
                    dei = depthEdgeIndicator(pixelatedUV, depth);

                float nei = 0.0;
                if (_NormalEdgeStrength > 0.0)
                    nei = normalEdgeIndicator(pixelatedUV, depth, normal);

                float strength = dei > 0.0 ? (1.0 - _DepthEdgeStrength * dei) : (1.0 + _NormalEdgeStrength * nei);

                return texel * strength;
            }
            ENDHLSL
        }
    }
}
