Shader "Hidden/Custom/GodotParity/Post"
{
    Properties
    {
        _PixelSize("Pixel Size", Float) = 4
        _TexelOffset("Texel Offset", Vector) = (0,0,0,0)
        _PixelationScreenSize("Screen Size", Vector) = (1920,1080,0.00052,0.00093)

        _LineTint("Line Tint", Color) = (0,0,0,1)
        _CreaseTint("Crease Tint", Color) = (0.833,0.833,0.833,1)
        _FlipPalettes("Flip Palettes", Float) = 0

        _LineOverlay("Line Overlay", Float) = 1
        _LineAlpha("Line Alpha", Range(0,1)) = 0.5

        _CreaseOverlay("Crease Overlay", Float) = 1
        _CreaseAlpha("Crease Alpha", Range(0,1)) = 1

        _KernelRadius("Kernel Radius", Range(0.5,4)) = 1

        _ZDeltaCutoff("Z Delta Cutoff", Range(0,1)) = 0.25
        _AngleZCutoff("Angle Z Cutoff", Range(0,1)) = 0.5
        _AngleZScale("Angle Z Scale", Float) = 2

        _ConvexCutoff("Convex Cutoff", Float) = 0.1
        _CreaseFeather("Crease Feather", Range(0,0.5)) = 0
        _ConcaveCutoff("Concave Cutoff", Float) = 0.01
        _ConcaveZCutoff("Concave Z Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GodotParityPost"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _PixelSize;
            float4 _TexelOffset;
            float4 _PixelationScreenSize;

            float4 _LineTint;
            float4 _CreaseTint;
            float _FlipPalettes;

            float _LineOverlay;
            float _LineAlpha;
            float _CreaseOverlay;
            float _CreaseAlpha;

            float _KernelRadius;
            float _ZDeltaCutoff;
            float _AngleZCutoff;
            float _AngleZScale;
            float _ConvexCutoff;
            float _CreaseFeather;
            float _ConcaveCutoff;
            float _ConcaveZCutoff;

            // Returns linear eye depth in world units, handling both projection modes.
            // LinearEyeDepth() uses the PERSPECTIVE formula and gives wrong results for
            // orthographic cameras (reversed ordering, wrong magnitude).
            // For orthographic (unity_OrthoParams.w = 1):
            //   With UNITY_REVERSED_Z, rawDepth=1 at near, rawDepth=0 at far.
            //   Correct depth = lerp(far, near, rawDepth).
            float GetLinearDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    // _ProjectionParams.y = near, _ProjectionParams.z = far
                    return lerp(_ProjectionParams.z, _ProjectionParams.y, rawDepth);
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float3 Composite(float3 dst, float3 src, float overlay)
            {
                if (overlay > 0.5)
                {
                    float3 multPart = 2.0 * dst * src;
                    float3 screenPart = 1.0 - 2.0 * (1.0 - dst) * (1.0 - src);
                    float3 mask = step(0.5, dst);
                    return saturate(lerp(multPart, screenPart, mask));
                }
                return saturate(src);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 shiftedUv = uv - _TexelOffset.xy * _PixelationScreenSize.zw;
                float2 baseUv = shiftedUv;

                float pixelSize = max(_PixelSize, 1.0);
                if (pixelSize > 1.001)
                {
                    // Sharp-sample filter matching Godot's upscale_and_offset.gdshader:
                    // smoothstep blending within the last fw fraction of each pixel cell
                    // avoids hard-snap jitter while keeping clean pixel art boundaries.
                    float2 px = _PixelationScreenSize.zw * pixelSize;
                    float2 fw = clamp(fwidth(shiftedUv) / px, 1e-5, 1.0);
                    float2 grid = shiftedUv / px - 0.5 * fw;
                    float2 blend = smoothstep(1.0 - fw, float2(1.0, 1.0), frac(grid));
                    baseUv = (floor(grid) + 0.5 + blend) * px;
                }

                float3 px = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, baseUv, 0).rgb;
                float2 stepUv = _PixelationScreenSize.zw * _KernelRadius;

                // Camera axes in world space (from inverse view matrix).
                // We convert world-space normals to a view-space where:
                //   x = camera right, y = camera up, z = -camFwd
                // This matches Godot's normal_roughness_texture convention
                // (normalVS.z > 0 = facing camera) for correct crease detection.
                float3 camRight = normalize(UNITY_MATRIX_I_V[0].xyz);
                float3 camUp    = normalize(UNITY_MATRIX_I_V[1].xyz);
                float3 camFwd   = normalize(UNITY_MATRIX_I_V[2].xyz);

                float depthSamples[9];
                float3 normalSamples[9]; // view-space (z > 0 = toward camera)
                float crossSamples[9];

                [unroll]
                for (int k = 0; k < 9; k++)
                {
                    float2 off = float2((k % 3) - 1, (k / 3) - 1);
                    float2 nuv = saturate(baseUv + off * stepUv);
                    // GetLinearDepth → world-unit distance, correct for both ortho & perspective
                    float rawDepth = SampleSceneDepth(nuv);
                    depthSamples[k] = GetLinearDepth(rawDepth);
                    // Transform world normal → view space matching Godot convention
                    // SafeNormalize: avoids NaN when normals texture is black/unavailable.
                    // normalize(float3(0,0,0)) = NaN via rsqrt(0)=INF; NaN poisons zThresh,
                    // making every depth comparison false → no outlines drawn.
                    float3 nWS = SafeNormalize(SampleSceneNormals(nuv));
                    // If normals unavailable, nWS=(0,0,0) → treat as facing camera (z=1 in view space).
                    if (dot(nWS, nWS) < 1e-5) nWS = camFwd; // fallback: perfectly facing camera
                    normalSamples[k] = float3(dot(nWS, camRight), dot(nWS, camUp), -dot(nWS, camFwd));
                }

                // facing: 0 = perfectly facing camera, 1 = edge-on (matches Godot)
                float facing = 1.0 - normalSamples[4].z;
                float t01 = saturate((facing - _AngleZCutoff) / max(1e-5, 1.0 - _AngleZCutoff));
                float zThresh = _ZDeltaCutoff * (t01 * _AngleZScale + 1.0);

                float concaveSum = 0.0;
                [unroll]
                for (int k = 0; k < 9; k++)
                {
                    float2 off2 = float2((k % 3) - 1, (k / 3) - 1);
                    float3 cr = cross(normalSamples[4], normalSamples[k]);
                    crossSamples[k] = dot(cr, float3(off2.yx, 0.0));
                    concaveSum += step(_ConcaveCutoff, -crossSamples[k]) * step(depthSamples[k] - depthSamples[4], _ConcaveZCutoff);
                }

                float creaseWeight = 0.0;
                [unroll]
                for (int ki = 0; ki < 4; ki++)
                {
                    int sIdx = 1 + ki * 2;
                    float baseNb = (sIdx < 4) ? 1e-5 : 0.0;
                    bool zDiff = abs(depthSamples[sIdx] - depthSamples[4]) < zThresh;
                    bool zFace = normalSamples[sIdx].z + baseNb > normalSamples[4].z;
                    float soft = (_CreaseFeather > 0.0)
                        ? smoothstep(_ConvexCutoff, _ConvexCutoff + _CreaseFeather, crossSamples[sIdx])
                        : (crossSamples[sIdx] > _ConvexCutoff ? 1.0 : 0.0);
                    creaseWeight += (zDiff && zFace) ? soft : 0.0;
                }

                if (concaveSum > 0.0)
                    creaseWeight = 0.0;

                // Center closer than neighbor → edge (depth in world units)
                bool hasLine =
                    (depthSamples[1] - depthSamples[4] > zThresh) ||
                    (depthSamples[3] - depthSamples[4] > zThresh) ||
                    (depthSamples[5] - depthSamples[4] > zThresh) ||
                    (depthSamples[7] - depthSamples[4] > zThresh);

                float3 cLine = (_FlipPalettes > 0.5) ? _CreaseTint.rgb : _LineTint.rgb;
                float3 cCrease = (_FlipPalettes > 0.5) ? _LineTint.rgb : _CreaseTint.rgb;

                float3 result = px;
                if (hasLine)
                {
                    result = lerp(px, Composite(px, cLine, _LineOverlay), saturate(_LineAlpha));
                }
                else if (creaseWeight > 0.0)
                {
                    float a = saturate(creaseWeight) * saturate(_CreaseAlpha);
                    result = lerp(px, Composite(px, cCrease, _CreaseOverlay), min(a, 1.0));
                }

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
