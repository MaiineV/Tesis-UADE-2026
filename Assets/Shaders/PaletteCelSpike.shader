// Spike cel-shaded con banding NdotL completo (mismo criterio que
// PaletteCelLit), pero en vez de un único _PaletteSlot fijo mezcla 2 slots
// de PA_MainPalette (base/sangre) según la altura EN ESPACIO OBJETO del
// vértice, no en espacio mundo — así la banda de sangre en la punta queda
// pegada al mesh sin importar dónde se instancie o rote el spike en el
// nivel. Sin ruido/sway/taper: la geometría del spike no se anima ni se
// deforma, a diferencia de PaletteCelFire.
Shader "Rollgeon/PaletteCelSpike"
{
    Properties
    {
        [Header(Spike Palette 2 Slots)]
        [PaletteSlot] _SpikeSlotBase  ("Base Slot",  Float) = 2
        [PaletteSlot] _SpikeSlotBlood ("Blood Slot", Float) = 9

        [Header(Blood Height Relative Object Space)]
        _SpikeHeightOffset ("Height Offset (Y objeto-espacio)",       Float)         = 0
        _SpikeHeightScale  ("Height Scale (Y objeto-espacio)",        Float)         = 1
        _BloodThreshold    ("Blood Threshold (altura relativa 0-1)",  Range(0, 1))   = 0.6
        _BloodSmooth       ("Blood Edge Smooth",                      Range(0, 0.5)) = 0.08

        [Header(Cel Controls)]
        _MidThreshold    ("Mid Threshold",    Range(0, 1))   = 0.65
        _ShadowThreshold ("Shadow Threshold", Range(0, 1))   = 0.35
        _ShadowSmooth    ("Shadow Smooth",    Range(0, 0.3)) = 0.02
        _LightWrap       ("Light Wrap",       Range(-1, 1))  = 0.1

        [Header(Dither)]
        [Toggle] _UseDither ("Border Dither", Float) = 0
        _DitherStrength     ("Border Dither Strength", Range(0, 1)) = 0.15

        [Header(Additional Lights)]
        _LightTintStrength ("Spotlight Tint Color", Range(0,1)) = 0.4
        _SpotDither        ("Edge Dither",          Range(0,1)) = 0.0

        [Header(Alpha Cutoff)]
        _AlphaCutoff ("Alpha Cutoff (1=visible, 0=hidden)", Range(0,1)) = 1
        _DitherScale ("Dither Scale (pixel chunkiness)", Range(1,32)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Forward Lit ──────────────────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _RollgeonLightData[128];

            CBUFFER_START(UnityPerMaterial)
                float _SpikeSlotBase;
                float _SpikeSlotBlood;
                float _SpikeHeightOffset;
                float _SpikeHeightScale;
                float _BloodThreshold;
                float _BloodSmooth;
                float _MidThreshold;
                float _ShadowThreshold;
                float _ShadowSmooth;
                float _LightWrap;
                float _UseDither;
                float _DitherStrength;
                float _LightTintStrength;
                float _SpotDither;
                float _AlphaCutoff;
                float _DitherScale;
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float3 positionOS  : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float BayerDither(float2 screenPos)
            {
                int2 p = int2(floor(screenPos)) & 3;
                int  i = p.y * 4 + p.x;
                const float bayer[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i];
            }

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = vni.normalWS;
                OUT.shadowCoord = GetShadowCoord(vpi);
                OUT.viewDirWS   = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.positionOS  = IN.positionOS.xyz;
                return OUT;
            }

            float CelLight(float3 normalWS, Light light, float wrapBias)
            {
                float NdotL = dot(normalWS, normalize(light.direction));
                float wrapped = saturate(NdotL + wrapBias);
                return wrapped * light.distanceAttenuation * light.shadowAttenuation;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                clip(_AlphaCutoff - (BayerDither(IN.positionCS.xy / _DitherScale) + 1.0/16.0));

                float3 normalWS = normalize(IN.normalWS);

                Light mainLight  = GetMainLight(IN.shadowCoord);
                float lightValue = CelLight(normalWS, mainLight, _LightWrap);

                float3 addTint = float3(0, 0, 0);
                #if defined(_CLUSTER_LIGHT_LOOP) || defined(_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS              = IN.positionWS;
                    inputData.normalWS                = normalWS;
                    inputData.viewDirectionWS         = normalize(GetWorldSpaceViewDir(IN.positionWS));
                    inputData.shadowCoord             = IN.shadowCoord;
                    inputData.shadowMask              = unity_ProbesOcclusion;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    float3 positionWS = IN.positionWS;
                    float spotBayer = BayerDither(IN.positionCS.xy);
                    LIGHT_LOOP_BEGIN(GetAdditionalLightsCount())
                        Light addLt  = GetAdditionalLight(lightIndex, positionWS, inputData.shadowMask);
                        float addVal = addLt.distanceAttenuation;
                        float4 ld       = _RollgeonLightData[lightIndex];
                        float ldSteps   = max(floor(ld.z), 1.0);
                        float shaped    = pow(saturate(addVal * ld.x), lerp(1.0, 8.0, ld.y));
                        float quantStep = (ldSteps > 1.5) ? (1.0 / (ldSteps - 1.0)) : 1.0;
                        float preVal    = saturate(shaped + (spotBayer - 0.5) * _SpotDither * quantStep);
                        float spotVal   = (ldSteps > 1.5) ? (floor(preVal * ldSteps) / (ldSteps - 1.0)) : preVal;
                        spotVal        *= addLt.shadowAttenuation;
                        lightValue      = max(lightValue, spotVal);
                        addTint        += addLt.color * spotVal;
                    LIGHT_LOOP_END
                }
                #endif

                float ditherOffset = 0.0;
                if (_UseDither > 0.5)
                    ditherOffset = (BayerDither(IN.positionCS.xy) - 0.5) * _DitherStrength;

                float celShadow = smoothstep(
                    _ShadowThreshold - _ShadowSmooth + ditherOffset,
                    _ShadowThreshold + _ShadowSmooth + ditherOffset,
                    lightValue);
                float celLight = smoothstep(
                    _MidThreshold - _ShadowSmooth + ditherOffset,
                    _MidThreshold + _ShadowSmooth + ditherOffset,
                    lightValue);

                // ── Banda de sangre por altura RELATIVA (espacio objeto): 0 en la
                // base del mesh, 1 en la punta, sin importar posición/rotación mundo.
                float hNorm = saturate((IN.positionOS.y - _SpikeHeightOffset) / max(_SpikeHeightScale, 0.0001));
                float bloodMask = smoothstep(_BloodThreshold - _BloodSmooth, _BloodThreshold + _BloodSmooth, hNorm);

                int slotBase  = int(_SpikeSlotBase);
                int slotBlood = int(_SpikeSlotBlood);

                float3 lightCol  = lerp(_PaletteLightColors[slotBase].rgb,  _PaletteLightColors[slotBlood].rgb,  bloodMask);
                float3 midCol    = lerp(_PaletteMidColors[slotBase].rgb,    _PaletteMidColors[slotBlood].rgb,    bloodMask);
                float3 shadowCol = lerp(_PaletteShadowColors[slotBase].rgb, _PaletteShadowColors[slotBlood].rgb, bloodMask);

                float3 color = lerp(shadowCol, midCol, celShadow);
                color        = lerp(color,     lightCol, celLight);

                color = saturate(color + addTint * _LightTintStrength);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster ────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SpikeSlotBase;
                float _SpikeSlotBlood;
                float _SpikeHeightOffset;
                float _SpikeHeightScale;
                float _BloodThreshold;
                float _BloodSmooth;
                float _MidThreshold;
                float _ShadowThreshold;
                float _ShadowSmooth;
                float _LightWrap;
                float _UseDither;
                float _DitherStrength;
                float _LightTintStrength;
                float _SpotDither;
                float _AlphaCutoff;
                float _DitherScale;
            CBUFFER_END

            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            float3 _LightDirection;
            float3 _LightPosition;

            float BayerDither(float2 screenPos)
            {
                int2 p = int2(floor(screenPos)) & 3;
                int  i = p.y * 4 + p.x;
                const float bayer[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i];
            }

            struct SCAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SCVary { float4 posCS : SV_POSITION; };

            SCVary ShadowVert(SCAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                SCVary OUT;
                float3 posWS    = TransformObjectToWorld(IN.posOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.posCS = posCS;
                return OUT;
            }
            half4 ShadowFrag(SCVary IN) : SV_Target
            {
                clip(_AlphaCutoff - (BayerDither(IN.posCS.xy / _DitherScale) + 1.0/16.0));
                return 0;
            }
            ENDHLSL
        }

        // ── Depth Only ───────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ColorMask 0
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SpikeSlotBase;
                float _SpikeSlotBlood;
                float _SpikeHeightOffset;
                float _SpikeHeightScale;
                float _BloodThreshold;
                float _BloodSmooth;
                float _MidThreshold;
                float _ShadowThreshold;
                float _ShadowSmooth;
                float _LightWrap;
                float _UseDither;
                float _DitherStrength;
                float _LightTintStrength;
                float _SpotDither;
                float _AlphaCutoff;
                float _DitherScale;
            CBUFFER_END

            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            float BayerDither(float2 screenPos)
            {
                int2 p = int2(floor(screenPos)) & 3;
                int  i = p.y * 4 + p.x;
                const float bayer[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i];
            }

            struct DOAttr { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 posCS : SV_POSITION; };

            DOVary DepthVert(DOAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DOVary OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                return OUT;
            }
            half4 DepthFrag(DOVary IN) : SV_Target
            {
                clip(_AlphaCutoff - (BayerDither(IN.posCS.xy / _DitherScale) + 1.0/16.0));
                return 0;
            }
            ENDHLSL
        }

        // ── Depth Normals ────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SpikeSlotBase;
                float _SpikeSlotBlood;
                float _SpikeHeightOffset;
                float _SpikeHeightScale;
                float _BloodThreshold;
                float _BloodSmooth;
                float _MidThreshold;
                float _ShadowThreshold;
                float _ShadowSmooth;
                float _LightWrap;
                float _UseDither;
                float _DitherStrength;
                float _LightTintStrength;
                float _SpotDither;
                float _AlphaCutoff;
                float _DitherScale;
            CBUFFER_END

            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            float BayerDither(float2 screenPos)
            {
                int2 p = int2(floor(screenPos)) & 3;
                int  i = p.y * 4 + p.x;
                const float bayer[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i];
            }

            struct DNAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVary { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVary DNVert(DNAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DNVary OUT;
                OUT.posCS    = TransformObjectToHClip(IN.posOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 DNFrag(DNVary IN) : SV_Target
            {
                clip(_AlphaCutoff - (BayerDither(IN.posCS.xy / _DitherScale) + 1.0/16.0));
                float3 normalWS = normalize(IN.normalWS);
                float2 encoded  = PackNormalOctRectEncode(TransformWorldToViewDir(normalWS, true));
                return float4(encoded, 0, 0);
            }
            ENDHLSL
        }
    }
}
