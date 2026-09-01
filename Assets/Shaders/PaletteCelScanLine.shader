// Cel shader con paleta global (mismo esqueleto que PaletteCelLit) + una banda de luz
// que recorre la coordenada V de las UV del mesh de arriba a abajo, en loop infinito —
// pensado para efectos de "escaneo"/carga de energía sobre cualquier prop.
//
// Uso:
//   - Igual que PaletteCelLit para el color base (paleta o colores manuales).
//   - La banda de luz usa su propio slot de paleta — puede ser un acento distinto
//     al color base del objeto (ej. objeto gris + banda dorada).
//   - La banda avanza sola con el tiempo (_ScanSpeed), no depende de mundo/cámara,
//     así que sigue funcionando igual sin importar cómo esté rotado/posicionado el prop.

Shader "Rollgeon/PaletteCelScanLine"
{
    Properties
    {
        [Header(Palette)]
        [ToggleUI] _UsePalette ("Use Global Palette", Float) = 0
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

        [Header(Scan Line)]
        // Barrido en loop sobre la V de las UV — 0 abajo, 1 arriba (según cómo esté
        // armado el UV del mesh, ver el editor de UV para confirmar la orientación).
        [PaletteSlot] _ScanSlot   ("Scan Line Palette Slot",           Float) = 5
        _ScanColor     ("Scan Line Color (si Use Palette está OFF)", Color)  = (1, 0.9, 0.5, 1)
        _ScanSpeed     ("Scan Speed (loops por segundo)",             Float) = 0.5
        _ScanWidth     ("Scan Band Width (UV, 0-1)",       Range(0.01, 1))   = 0.08
        _ScanSoftness  ("Scan Edge Softness (UV)",         Range(0.001, 0.5)) = 0.04
        _ScanIntensity ("Scan Intensity",                  Range(0, 8))      = 2.0

        [Header(Dither)]
        [ToggleUI] _UseDither       ("Border Dither",          Float) = 0
        _DitherStrength           ("Border Dither Strength",  Range(0, 1)) = 0.15
        [ToggleUI] _UseShadowDither ("Shadow Dither",           Float) = 0
        _ShadowDitherDensity      ("Shadow Dither Density",   Range(0, 1)) = 0.3

        [Header(Additional Lights)]
        _LightTintStrength        ("Spotlight Tint Color",                Range(0,1)) = 0.4
        _SpotDither               ("Edge Dither",             Range(0,1))  = 0.0

        [Header(Crease)]
        [ToggleUI] _EnableCrease  ("Enable Crease",  Float) = 0
        _CreaseColor            ("Crease Color",   Color) = (0.15, 0.15, 0.2, 1)
        _CreaseThreshold        ("Crease Threshold", Range(0, 1)) = 0.35
        _CreaseSmooth           ("Crease Smooth",    Range(0, 0.3)) = 0.05
        _CreaseAlpha            ("Crease Alpha",     Range(0, 1))   = 0.8
        [ToggleUI] _CreaseDither  ("Crease Dither",    Float) = 0

        [Header(Alpha Cutoff)]
        _AlphaCutoff ("Alpha Cutoff (1=visible, 0=hidden)", Range(0,1)) = 1
        _DitherScale ("Dither Scale (pixel chunkiness)", Range(1,32)) = 1

        [Header(Hit Flash)]
        _HitFlashAmount ("Hit Flash Amount", Range(0,1))   = 0
        _HitFlashColor  ("Hit Flash Color",  Color)        = (1,1,1,1)

        [Header(Emission)]
        [ToggleUI] _EnableEmission ("Enable Emission", Float) = 0
        [HDR] _EmissionColor     ("Emission Color",  Color) = (0,0,0,0)
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

            // Per-light quantization data uploaded by LightDataRendererFeature every frame.
            float4 _RollgeonLightData[128];

            // Luces falsas (FakeAreaLight/FakeAreaLightManager.cs) — ver PaletteCelLit.shader
            // para la explicación completa del packing.
            float  _FakeAreaLightCount;
            float4 _FakeAreaLightData[40];

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
                float  _ScanSlot;
                float4 _ScanColor;
                float  _ScanSpeed;
                float  _ScanWidth;
                float  _ScanSoftness;
                float  _ScanIntensity;
                float  _UseDither;
                float  _DitherStrength;
                float  _UseShadowDither;
                float  _ShadowDitherDensity;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
                float  _LightTintStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
                float  _SpotDither;
                float  _HitFlashAmount;
                float4 _HitFlashColor;
                float  _EnableEmission;
                float4 _EmissionColor;
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float2 uv          : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Bayer 4×4 para dither ────────────────────────────────────────────
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
                OUT.uv          = IN.uv;
                return OUT;
            }

            float CelLight(float3 normalWS, Light light, float wrapBias)
            {
                float NdotL = dot(normalWS, normalize(light.direction));
                float wrapped = saturate(NdotL + wrapBias);
                float luminance = dot(light.color, half3(0.2126, 0.7152, 0.0722));
                return wrapped * luminance * light.distanceAttenuation * light.shadowAttenuation;
            }

            float3 ClosestOnSegment(float3 p, float3 a, float3 b)
            {
                float3 ab = b - a;
                float  t  = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-5));
                return a + ab * t;
            }

            float FakeAreaLightContribution(float3 positionWS, float3 normalWS, inout float3 tint)
            {
                float best = 0.0;
                int count = (int)_FakeAreaLightCount;
                [loop]
                for (int i = 0; i < count; i++)
                {
                    int b = i * 5;
                    float3 p0         = _FakeAreaLightData[b].xyz;
                    float  intensity  = _FakeAreaLightData[b].w;
                    float3 p1         = _FakeAreaLightData[b + 1].xyz;
                    float  range      = _FakeAreaLightData[b + 1].w;
                    float3 p2         = _FakeAreaLightData[b + 2].xyz;
                    int    pointCount = (int)_FakeAreaLightData[b + 2].w;
                    float3 p3         = _FakeAreaLightData[b + 3].xyz;
                    float3 color      = _FakeAreaLightData[b + 4].xyz;

                    float3 closest   = p0;
                    float  minDistSq = dot(positionWS - p0, positionWS - p0);

                    if (pointCount >= 2)
                    {
                        float3 c0 = ClosestOnSegment(positionWS, p0, p1);
                        float  d0 = dot(positionWS - c0, positionWS - c0);
                        if (d0 < minDistSq) { minDistSq = d0; closest = c0; }
                    }
                    if (pointCount >= 3)
                    {
                        float3 c1 = ClosestOnSegment(positionWS, p1, p2);
                        float  d1 = dot(positionWS - c1, positionWS - c1);
                        if (d1 < minDistSq) { minDistSq = d1; closest = c1; }
                    }
                    if (pointCount >= 4)
                    {
                        float3 c2 = ClosestOnSegment(positionWS, p2, p3);
                        float  d2 = dot(positionWS - c2, positionWS - c2);
                        if (d2 < minDistSq) { minDistSq = d2; closest = c2; }
                    }

                    float dist  = sqrt(max(minDistSq, 1e-8));
                    float atten = saturate(1.0 - dist / max(range, 1e-4));
                    atten *= atten;

                    float ndotl = saturate(dot(normalWS, (closest - positionWS) / dist));
                    float val   = atten * ndotl * intensity;

                    best  = max(best, val);
                    tint += color * val;
                }
                return best;
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
                    float2 normalizedScreenSpaceUV = inputData.normalizedScreenSpaceUV;
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
                        float addLuminance = dot(addLt.color, half3(0.2126, 0.7152, 0.0722));
                        spotVal        *= addLt.shadowAttenuation * addLuminance;
                        lightValue      = max(lightValue, spotVal);
                        addTint        += addLt.color * spotVal;
                    LIGHT_LOOP_END
                }
                #endif

                float fakeVal = FakeAreaLightContribution(IN.positionWS, normalWS, addTint);
                lightValue = max(lightValue, fakeVal);

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

                float3 color = lerp(shadowCol, midCol,    celShadow);
                color        = lerp(color,     lightCol,  celLight);

                if (_UseShadowDither > 0.5)
                {
                    float bayer    = BayerDither(IN.positionCS.xy);
                    float inShadow = 1.0 - celShadow;
                    float dot      = step(bayer, _ShadowDitherDensity) * inShadow;
                    color          = lerp(color, _MidColor.rgb, dot);
                }

                if (_EnableCrease > 0.5)
                {
                    float3 viewDirWS = normalize(IN.viewDirWS);
                    float NdotV = abs(dot(normalWS, viewDirWS));
                    float creaseDitherOff = 0.0;
                    if (_CreaseDither > 0.5)
                        creaseDitherOff = (BayerDither(IN.positionCS.xy) - 0.5) * _DitherStrength;
                    float creaseVal = smoothstep(
                        _CreaseThreshold + _CreaseSmooth + creaseDitherOff,
                        _CreaseThreshold - _CreaseSmooth + creaseDitherOff,
                        NdotV);
                    color = lerp(color, _CreaseColor.rgb, creaseVal * _CreaseAlpha);
                }

                color = saturate(color + addTint * _LightTintStrength);

                // ── Scan Line ────────────────────────────────────────────────────
                // Banda que recorre la V de las UV del mesh en loop infinito — avanza
                // sola con el tiempo, sin depender de mundo/cámara. El wrap-around
                // (min(distV, 1-distV)) hace que la banda también se vea cruzando el
                // límite 0/1 del loop, en vez de "desaparecer" y reaparecer de golpe.
                float scanPos  = frac(_Time.y * _ScanSpeed);
                float distV    = abs(IN.uv.y - scanPos);
                distV          = min(distV, 1.0 - distV);
                float halfW    = _ScanWidth * 0.5;
                float scanBand = 1.0 - smoothstep(halfW, halfW + _ScanSoftness, distV);

                int    scanSlot = int(_ScanSlot);
                float3 scanCol  = _UsePalette > 0.5 ? _PaletteLightColors[scanSlot].rgb : _ScanColor.rgb;

                // Sin saturate() a propósito: la banda puede pasarse de 1.0 (HDR) para
                // que el Bloom la agarre — mismo criterio que el resto de la familia
                // (fuego, emission), ver docs/setup — bloom espera valores >1.0 reales.
                color += scanCol * scanBand * _ScanIntensity;

                color = lerp(color, _HitFlashColor.rgb, _HitFlashAmount);
                color += _EmissionColor.rgb * _EnableEmission;
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
                float  _ScanSlot;
                float4 _ScanColor;
                float  _ScanSpeed;
                float  _ScanWidth;
                float  _ScanSoftness;
                float  _ScanIntensity;
                float  _UseDither;
                float  _DitherStrength;
                float  _UseShadowDither;
                float  _ShadowDitherDensity;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
                float  _LightTintStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
                float  _SpotDither;
                float  _HitFlashAmount;
                float4 _HitFlashColor;
                float  _EnableEmission;
                float4 _EmissionColor;
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
                float  _UsePalette;
                float  _PaletteSlot;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _ScanSlot;
                float4 _ScanColor;
                float  _ScanSpeed;
                float  _ScanWidth;
                float  _ScanSoftness;
                float  _ScanIntensity;
                float  _UseDither;
                float  _DitherStrength;
                float  _UseShadowDither;
                float  _ShadowDitherDensity;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
                float  _LightTintStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
                float  _SpotDither;
                float  _HitFlashAmount;
                float4 _HitFlashColor;
                float  _EnableEmission;
                float4 _EmissionColor;
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
                float  _UsePalette;
                float  _PaletteSlot;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _ScanSlot;
                float4 _ScanColor;
                float  _ScanSpeed;
                float  _ScanWidth;
                float  _ScanSoftness;
                float  _ScanIntensity;
                float  _UseDither;
                float  _DitherStrength;
                float  _UseShadowDither;
                float  _ShadowDitherDensity;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
                float  _LightTintStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
                float  _SpotDither;
                float  _HitFlashAmount;
                float4 _HitFlashColor;
                float  _EnableEmission;
                float4 _EmissionColor;
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
