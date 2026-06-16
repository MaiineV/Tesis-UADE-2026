// Sharp pixel-art upscale shader for UI RawImage.
// Uses fwidth derivatives to produce mathematically crisp pixel boundaries
// at any screen resolution — no aliasing at non-integer scale factors.
// Also applies _PixelPanOffset (set by CameraRig each frame) to compensate
// the sub-texel snap error and recover smooth sub-pixel motion.

Shader "UI/Custom/SharpUpscale"
{
    Properties
    {
        [PerRendererData] _MainTex ("Render Texture", 2D) = "white" {}
        // Injected every frame by CameraRig.ApplyPixelSnap()
        // XY = sub-texel UV compensation in RT-UV space
        _PixelPanOffset ("Pixel Pan Offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SharpUpscale"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target   3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            // (1/w, 1/h, w, h) of the source RenderTexture
            float4 _MainTex_TexelSize;
            float4 _PixelPanOffset;

            struct Attributes
            {
                float4 posOS  : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                OUT.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Apply sub-texel compensation from pixel snap
                float2 uv = IN.uv + _PixelPanOffset.xy;

                float2 px = _MainTex_TexelSize.xy; // (1/rtWidth, 1/rtHeight)

                // fwidth tells us how fast UV changes per screen pixel.
                // Dividing by px gives us how many RT texels fit per screen pixel.
                // fw < 1  → zoomed in (more screen pixels than RT pixels) — go sharp
                // fw >= 1 → zoomed out — bilinear fallback (clamp prevents issues)
                float2 fw = clamp(fwidth(uv) / px, 1e-5, 1.0);

                // Position within the texel grid, offset by half the blend window
                float2 grid = uv / px - 0.5 * fw;

                // Smooth transition across the texel boundary (fw-wide window)
                float2 blend = smoothstep(1.0 - fw, float2(1.0, 1.0), frac(grid));

                // Reconstruct final UV snapped to nearest texel center + sub-texel blend
                float2 finalUV = (floor(grid) + 0.5 + blend) * px;
                finalUV = clamp(finalUV, 0.0, 1.0);

                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV) * IN.color;
            }
            ENDHLSL
        }
    }
}
