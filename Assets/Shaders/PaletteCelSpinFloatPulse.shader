// Igual que PaletteCelSpinFloat.shader (gira sobre su eje + flota, banding
// PA_MainPalette con gate de luminancia real) pero además "respira": escala
// uniforme oscilando con un seno alrededor de 1.0, período configurable en
// segundos (_PulsePeriod). La escala se aplica ANTES del spin/bob en espacio
// objeto — al ser uniforme y centrada en el pivot, conmuta con la rotación
// sin distorsionar el mesh. Fase propia (hash de pivot world-space, semilla
// distinta a la del bob) para que el pulso no quede sincronizado con el
// float ni con otras instancias del mismo material en la sala.
Shader "Rollgeon/PaletteCelSpinFloatPulse"
{
    Properties
    {
        [Header(Palette)]
        [ToggleUI] _UsePalette ("Use Global Palette", Float) = 1
        [PaletteSlot] _PaletteSlot ("Palette Slot", Float) = 0

        [Header(Palette Colors)]
        _LightColor  ("Light Color",  Color) = (1.0, 1.0, 1.0, 1)
        _MidColor    ("Mid Color",    Color) = (0.6, 0.6, 0.65, 1)
        _ShadowColor ("Shadow Color", Color) = (0.3, 0.3, 0.4, 1)

        [Header(Cel Controls)]
        _MidThreshold    ("Mid Threshold",    Range(0, 1))   = 0.65
        _ShadowThreshold ("Shadow Threshold", Range(0, 1))   = 0.35
        _ShadowSmooth    ("Shadow Smooth",    Range(0, 0.3)) = 0.02
        _LightWrap       ("Light Wrap",       Range(-1, 1))  = 0.1

        [Header(Spin)]
        _SpinSpeed ("Spin Speed (deg/sec)", Float) = 60

        [Header(Float Bob)]
        _BobAmplitude ("Bob Amplitude", Float) = 0.15
        _BobSpeed     ("Bob Speed",     Float) = 1.5

        [Header(Breathing Pulse)]
        _PulsePeriod    ("Pulse Period (seconds)",      Float)      = 3
        _PulseAmplitude ("Pulse Amplitude (scale delta)", Range(0,1)) = 0.15

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
                float  _UsePalette;
                float  _PaletteSlot;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _PulsePeriod;
                float  _PulseAmplitude;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
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

            float2 SpinAngleSinCos()
            {
                float angle = radians(_Time.y * _SpinSpeed);
                return float2(sin(angle), cos(angle));
            }
            float3 ApplySpinPos(float3 v, float2 sc)
            {
                v.xz = float2(v.x * sc.y - v.z * sc.x, v.x * sc.x + v.z * sc.y);
                return v;
            }
            float3 ApplySpinNormal(float3 n, float2 sc)
            {
                n.xz = float2(n.x * sc.y - n.z * sc.x, n.x * sc.x + n.z * sc.y);
                return n;
            }
            float ApplyBobY(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
                return sin(_Time.y * _BobSpeed + phase) * _BobAmplitude;
            }
            // Escala uniforme "respirando" alrededor de 1.0 — semilla de hash
            // distinta a la del bob para que no queden en fase.
            float ApplyPulseScale(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(39.346, 11.135))) * 24634.6345) * 6.2831853;
                float freq  = 6.2831853 / max(_PulsePeriod, 0.0001);
                return 1.0 + sin(_Time.y * freq + phase) * _PulseAmplitude;
            }

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;

                float  pulse = ApplyPulseScale(pivotWS);
                float2 sc    = SpinAngleSinCos();
                float3 posOS = ApplySpinPos(IN.positionOS.xyz * pulse, sc);
                float3 nrmOS = ApplySpinNormal(IN.normalOS, sc); // escala uniforme no distorsiona la normal

                posOS.y += ApplyBobY(pivotWS);

                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                VertexNormalInputs   vni = GetVertexNormalInputs(nrmOS);

                OUT.positionCS  = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = vni.normalWS;
                OUT.shadowCoord = GetShadowCoord(vpi);
                return OUT;
            }

            float CelLight(float3 normalWS, Light light, float wrapBias)
            {
                float NdotL = dot(normalWS, normalize(light.direction));
                float wrapped = saturate(NdotL + wrapBias);
                // Gate por luminancia real — sin luz activa, lightValue debe caer a 0 (Shadow plano).
                float luminance = dot(light.color, half3(0.2126, 0.7152, 0.0722));
                return wrapped * luminance * light.distanceAttenuation * light.shadowAttenuation;
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
                        // Gate por luminancia real — ver PaletteCelLit.shader.
                        float addLuminance = dot(addLt.color, half3(0.2126, 0.7152, 0.0722));
                        spotVal        *= addLt.shadowAttenuation * addLuminance;
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

                int    slot      = int(_PaletteSlot);
                float3 lightCol  = _UsePalette > 0.5 ? _PaletteLightColors[slot].rgb  : _LightColor.rgb;
                float3 midCol    = _UsePalette > 0.5 ? _PaletteMidColors[slot].rgb    : _MidColor.rgb;
                float3 shadowCol = _UsePalette > 0.5 ? _PaletteShadowColors[slot].rgb : _ShadowColor.rgb;

                float3 color = lerp(shadowCol, midCol,   celShadow);
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
                float  _UsePalette;
                float  _PaletteSlot;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _PulsePeriod;
                float  _PulseAmplitude;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

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

            float2 SpinAngleSinCos()
            {
                float angle = radians(_Time.y * _SpinSpeed);
                return float2(sin(angle), cos(angle));
            }
            float3 ApplySpinPos(float3 v, float2 sc)
            {
                v.xz = float2(v.x * sc.y - v.z * sc.x, v.x * sc.x + v.z * sc.y);
                return v;
            }
            float3 ApplySpinNormal(float3 n, float2 sc)
            {
                n.xz = float2(n.x * sc.y - n.z * sc.x, n.x * sc.x + n.z * sc.y);
                return n;
            }
            float ApplyBobY(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
                return sin(_Time.y * _BobSpeed + phase) * _BobAmplitude;
            }
            float ApplyPulseScale(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(39.346, 11.135))) * 24634.6345) * 6.2831853;
                float freq  = 6.2831853 / max(_PulsePeriod, 0.0001);
                return 1.0 + sin(_Time.y * freq + phase) * _PulseAmplitude;
            }

            struct SCAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SCVary { float4 posCS : SV_POSITION; };

            SCVary ShadowVert(SCAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                SCVary OUT;

                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float  pulse   = ApplyPulseScale(pivotWS);
                float2 sc      = SpinAngleSinCos();
                float3 posOS   = ApplySpinPos(IN.posOS.xyz * pulse, sc);
                float3 nrmOS   = ApplySpinNormal(IN.normalOS, sc);
                posOS.y += ApplyBobY(pivotWS);

                float3 posWS    = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(nrmOS);
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
                float  _UsePalette;
                float  _PaletteSlot;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _PulsePeriod;
                float  _PulseAmplitude;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

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

            float2 SpinAngleSinCos()
            {
                float angle = radians(_Time.y * _SpinSpeed);
                return float2(sin(angle), cos(angle));
            }
            float3 ApplySpinPos(float3 v, float2 sc)
            {
                v.xz = float2(v.x * sc.y - v.z * sc.x, v.x * sc.x + v.z * sc.y);
                return v;
            }
            float ApplyBobY(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
                return sin(_Time.y * _BobSpeed + phase) * _BobAmplitude;
            }
            float ApplyPulseScale(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(39.346, 11.135))) * 24634.6345) * 6.2831853;
                float freq  = 6.2831853 / max(_PulsePeriod, 0.0001);
                return 1.0 + sin(_Time.y * freq + phase) * _PulseAmplitude;
            }

            struct DOAttr { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 posCS : SV_POSITION; };

            DOVary DepthVert(DOAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DOVary OUT;

                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float  pulse   = ApplyPulseScale(pivotWS);
                float2 sc      = SpinAngleSinCos();
                float3 posOS   = ApplySpinPos(IN.posOS.xyz * pulse, sc);
                posOS.y += ApplyBobY(pivotWS);

                OUT.posCS = TransformObjectToHClip(posOS);
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
                float  _UsePalette;
                float  _PaletteSlot;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _PulsePeriod;
                float  _PulseAmplitude;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

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

            float2 SpinAngleSinCos()
            {
                float angle = radians(_Time.y * _SpinSpeed);
                return float2(sin(angle), cos(angle));
            }
            float3 ApplySpinPos(float3 v, float2 sc)
            {
                v.xz = float2(v.x * sc.y - v.z * sc.x, v.x * sc.x + v.z * sc.y);
                return v;
            }
            float3 ApplySpinNormal(float3 n, float2 sc)
            {
                n.xz = float2(n.x * sc.y - n.z * sc.x, n.x * sc.x + n.z * sc.y);
                return n;
            }
            float ApplyBobY(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
                return sin(_Time.y * _BobSpeed + phase) * _BobAmplitude;
            }
            float ApplyPulseScale(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(39.346, 11.135))) * 24634.6345) * 6.2831853;
                float freq  = 6.2831853 / max(_PulsePeriod, 0.0001);
                return 1.0 + sin(_Time.y * freq + phase) * _PulseAmplitude;
            }

            struct DNAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVary { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVary DNVert(DNAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DNVary OUT;

                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float  pulse   = ApplyPulseScale(pivotWS);
                float2 sc      = SpinAngleSinCos();
                float3 posOS   = ApplySpinPos(IN.posOS.xyz * pulse, sc);
                float3 nrmOS   = ApplySpinNormal(IN.normalOS, sc);
                posOS.y += ApplyBobY(pivotWS);

                OUT.posCS    = TransformObjectToHClip(posOS);
                OUT.normalWS = TransformObjectToWorldNormal(nrmOS);
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
