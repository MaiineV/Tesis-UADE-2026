Shader "Custom/GodotParity/FoliageBillboard"
{
    Properties
    {
        [Header(Textures)]
        _BaseTexture("Base Texture (spritesheet 2x2)", 2D) = "white" {}

        [Header(Cel Ramp)]
        _CelRamp("Cel Ramp", 2D) = "white" {}
        _CelRampAccent("Cel Ramp Accent", 2D) = "white" {}

        [Header(Albedo)]
        _AlphaScissor("Alpha Scissor", Range(0,1)) = 0.5
        _VerticalOffset("Vertical Offset", Range(0,1)) = 0.25
        _QuadScale("Quad Scale", Range(0,2)) = 1.0

        [Header(Cel Controls)]
        _LightWrap("Light Wrap", Range(-2,2)) = 0.3
        _Steepness("Steepness", Range(1,8)) = 1.0
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0

        [Header(Variation probabilities)]
        _Var1Probability("Var1 Probability", Range(0,1)) = 0.1
        _Var2Probability("Var2 Probability", Range(0,1)) = 0.1

        [Header(Wind)]
        _Framerate("Framerate", Float) = 5.0
        _ViewSwaySpeed("Sway Speed", Range(0,5)) = 0.1
        _ViewSwayAngle("Sway Angle (deg)", Range(0,45)) = 10.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseTexture); SAMPLER(sampler_BaseTexture);
            TEXTURE2D(_CelRamp);     SAMPLER(sampler_CelRamp);
            TEXTURE2D(_CelRampAccent); SAMPLER(sampler_CelRampAccent);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTexture_ST;
                float _AlphaScissor;
                float _VerticalOffset;
                float _QuadScale;
                float _LightWrap;
                float _Steepness;
                float _ShadowStrength;
                float _Var1Probability;
                float _Var2Probability;
                float _Framerate;
                float _ViewSwaySpeed;
                float _ViewSwayAngle;
            CBUFFER_END

            // ── helpers ───────────────────────────────────────────────────
            float LocationSeed(float2 xz)
            {
                return frac(sin(dot(xz, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // Bayer 4x4 ordered dither
            float ToonBayer(float2 coords)
            {
                static const float bayer[16] = {
                    0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                   12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                    3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                   15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                int2 p = int2(floor(coords)) & 3;
                return bayer[p.y * 4 + p.x];
            }

            // Remap quad UV into one 2x2 atlas quadrant
            float2 VariationUV(float2 uv, float varType)
            {
                if (varType < 0.5) return uv * 0.5;
                if (varType < 1.5) return uv * 0.5 + float2(0.5, 0.0);
                if (varType < 2.5) return uv * 0.5 + float2(0.0, 0.5);
                return uv * 0.5 + float2(0.5, 0.5);
            }

            float3 RampFloor(float varType)
            {
                float u = lerp(0.5, 0.0, saturate(_ShadowStrength));
                float3 r1 = SAMPLE_TEXTURE2D_LOD(_CelRamp,       sampler_CelRamp,       float2(u, 0.5), 0).rgb;
                float3 r2 = SAMPLE_TEXTURE2D_LOD(_CelRampAccent, sampler_CelRampAccent, float2(u, 0.5), 0).rgb;
                return varType < 0.5 ? r1 : r2;
            }

            // ── structs ───────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                // Declare SV_InstanceID directly — always valid in vertex shaders regardless of
                // INSTANCING_ON keyword state. UNITY_VERTEX_INPUT_INSTANCE_ID expands to empty
                // when the non-instanced variant is compiled, breaking IN.instanceID references.
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float  varType    : TEXCOORD2;
                float3 quadCenter : TEXCOORD3; // for lighting
            };

            // ── vertex ────────────────────────────────────────────────────
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                // CRITICAL: must be called first so UNITY_MATRIX_M reads THIS instance's
                // transform (not instance 0 for all quads → all grass at same spot).
                UNITY_SETUP_INSTANCE_ID(IN);

                // World origin from the per-instance matrix set by DrawMeshInstanced.
                // mul(M, (0,0,0,1)) transforms the local origin → world position.
                float3 origin = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)).xyz;
                float  seed   = LocationSeed(origin.xz);

                float3 pivotWS = origin + float3(0, _VerticalOffset, 0);

                // Variation selection — use IN.instanceID (the SV_InstanceID semantic declared directly
                // in Attributes), which is always valid regardless of INSTANCING_ON keyword state.
                float varHash = frac(sin(float(IN.instanceID) * 17.382) * 43758.5453);
                float varType = 0.0;
                if (varHash < _Var1Probability) varType = 1.0;
                else if (varHash < _Var1Probability + _Var2Probability) varType = 2.0;

                // Wind sway (quantised, view-space)
                float quantTime = _Time.y;
                quantTime += frac(seed / _Framerate);
                quantTime = round(quantTime * _Framerate) / _Framerate;

                float angle = sin((quantTime + seed) * _ViewSwaySpeed * 6.2831853) * radians(_ViewSwayAngle);
                float cosA = cos(angle), sinA = sin(angle);

                // Rotate quad vertex around pivot (Y in view space = sway)
                float2 swayVert = IN.positionOS.xy * _QuadScale;
                const float pivotY = -0.5;
                swayVert.y -= pivotY;
                float2 rotated = float2(cosA * swayVert.x - sinA * swayVert.y,
                                        sinA * swayVert.x + cosA * swayVert.y);
                rotated.y += pivotY;

                // Billboard: build view-aligned axes
                float3 camRight = UNITY_MATRIX_I_V[0].xyz;
                float3 camUp    = UNITY_MATRIX_I_V[1].xyz;

                float3 worldPos = pivotWS + camRight * rotated.x + camUp * rotated.y;

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.positionWS = worldPos;
                OUT.quadCenter = pivotWS;
                OUT.varType    = varType;
                OUT.uv         = VariationUV(IN.uv, varType);
                return OUT;
            }

            // ── fragment ──────────────────────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                float4 tex   = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, IN.uv);
                clip(tex.a - _AlphaScissor);

                float3 camRight = normalize(UNITY_MATRIX_I_V[0].xyz);
                float3 camUp    = normalize(UNITY_MATRIX_I_V[1].xyz);
                // Billboard normal faces camera
                float3 normalWS = normalize(UNITY_MATRIX_I_V[2].xyz);
                // Use world-up for lighting (matches Godot lighting_normal_mode = 1)
                float3 lightNormal = normalize(TransformObjectToWorldNormal(float3(0, 1, 0)));

                float3 rampFloor = RampFloor(IN.varType);
                float3 diffuse   = float3(0, 0, 0);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float att = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                float luma     = dot(mainLight.color, float3(0.2126, 0.7152, 0.0722));
                float lumaSafe = max(luma, 0.001);
                float3 chroma  = clamp(mainLight.color / lumaSafe, 0.0, 3.0);

                float ndotl   = dot(lightNormal, normalize(mainLight.direction));
                float wrapped = saturate(ndotl + _LightWrap);
                float raw     = saturate(wrapped + (att - 1.0));
                float band    = saturate(raw * max(_Steepness, 1e-4));

                float3 rampColor = SAMPLE_TEXTURE2D_LOD(_CelRamp, sampler_CelRamp, float2(band, 0.5), 0).rgb;
                diffuse += (rampColor - rampFloor) * sqrt(max(luma * 0.32, 0)) * chroma;

                float3 color = tex.rgb * (rampFloor + diffuse);
                return half4(color, tex.a);
            }
            ENDHLSL
        }
    }
}
