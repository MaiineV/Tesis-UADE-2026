// Fuego cel-shaded animado: mismo banding NdotL que PaletteCelLit, pero en vez
// de un único _PaletteSlot fijo, mezcla 3 slots de PA_MainPalette (base/mid/tip)
// por altura del objeto + ruido que scrollea hacia arriba — así el trío
// luz/mid/sombra que alimenta el banding cambia en el tiempo y da la sensación
// de llama en vez de una superficie pintada estática. Top Taper angosta la tapa
// plana de arriba hacia una punta irregular y animada (se replica en las 4
// passes para que la sombra matchee la silueta). Sway de vértices es opcional
// (Toggle, off por default) y solo afecta la pass Forward — las passes de
// shadow/depth no lo replican, aceptable para un prop chico experimental.
Shader "Rollgeon/PaletteCelFire"
{
    Properties
    {
        [Header(Fire Palette 3 Slots)]
        [PaletteSlot] _FireSlotBase ("Base Slot (abajo)", Float) = 9
        [PaletteSlot] _FireSlotMid  ("Mid Slot",          Float) = 7
        [PaletteSlot] _FireSlotTip  ("Tip Slot (arriba)", Float) = 5

        [Header(Fire Shape)]
        // Altura objeto-espacio donde arranca/termina el degradé base->tip.
        _FireHeightOffset ("Height Offset (Y objeto-espacio)", Float) = 0
        _FireHeightScale  ("Height Scale (Y objeto-espacio)",  Float) = 1

        [Header(Fire Animation)]
        _FireNoiseScale     ("Noise Scale",           Float)          = 3
        _FireScrollSpeed    ("Noise Scroll Speed",    Float)          = 1.2
        _FireFlickerAmount  ("Flicker Amount",        Range(0, 1))    = 0.35

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

        [Header(Emission)]
        _FireEmissionStrength ("Fire Emission Strength", Range(0, 4)) = 1

        [Header(Sway Experimental)]
        [Toggle] _EnableSway ("Enable Sway", Float) = 0
        _SwayAmount ("Sway Amount", Range(0, 0.3)) = 0.05
        _SwaySpeed  ("Sway Speed",  Float)          = 2

        [Header(Top Taper Flame Tip)]
        // Angosta la tapa plana de arriba hacia una punta irregular y móvil —
        // 0 = tapa sin tocar (cilindro/cono tal cual), 1 = colapsada al eje.
        _TopTaperAmount       ("Tip Taper Amount", Range(0, 1))   = 0.5
        _TopTaperStart        ("Tip Taper Start Height (0-1)", Range(0, 1)) = 0.65
        _TopTaperIrregularity ("Tip Irregularity", Range(0, 0.3)) = 0.08
        _TopTaperSpeed        ("Tip Irregular Speed", Float)       = 3

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
                float  _FireSlotBase;
                float  _FireSlotMid;
                float  _FireSlotTip;
                float  _FireHeightOffset;
                float  _FireHeightScale;
                float  _FireNoiseScale;
                float  _FireScrollSpeed;
                float  _FireFlickerAmount;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _FireEmissionStrength;
                float  _EnableSway;
                float  _SwayAmount;
                float  _SwaySpeed;
                float  _TopTaperAmount;
                float  _TopTaperStart;
                float  _TopTaperIrregularity;
                float  _TopTaperSpeed;
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

            // ── Ruido barato para el flicker ─────────────────────────────────────
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 2 octavas alcanzan para un flicker orgánico sin costo de textura.
            float FireNoise(float2 p)
            {
                float n = ValueNoise(p) * 0.7;
                n += ValueNoise(p * 2.3) * 0.3;
                return n;
            }

            // Offset de sway: nulo en la base, máximo en la punta — mismo criterio
            // que el sway de CurtainUI.shader pero en objeto-espacio 3D.
            float3 SwayOffset(float3 positionOS)
            {
                if (_EnableSway < 0.5) return float3(0, 0, 0);
                float hNorm = saturate((positionOS.y - _FireHeightOffset) / max(_FireHeightScale, 0.0001));
                float phase = _Time.y * _SwaySpeed + positionOS.y * 3.0;
                float sway  = sin(phase) * _SwayAmount * hNorm;
                return float3(sway, 0, sway * 0.6);
            }

            // Angosta la tapa plana de arriba hacia una punta irregular: encoge XZ
            // radialmente hacia el eje vertical (solo por encima de _TopTaperStart) y
            // le suma un wobble animado con fase/dirección propia por vértice (via
            // hash de su XZ original), así no todos los vértices de la punta se
            // mueven sincronizados. No recalcula normales — aceptable para un prop
            // chico estilizado, mismo criterio que SwayOffset.
            float3 ApplyTopTaper(float3 posOS)
            {
                float hNorm  = saturate((posOS.y - _FireHeightOffset) / max(_FireHeightScale, 0.0001));
                float taperT = smoothstep(_TopTaperStart, 1.0, hNorm);
                if (taperT <= 0.0) return posOS;

                float hash = Hash21(posOS.xz * 13.7);

                float shrink = taperT * _TopTaperAmount;
                float2 xz = posOS.xz * (1.0 - shrink);

                float phase = _Time.y * _TopTaperSpeed + hash * 6.2831853;
                float2 wobbleDir = float2(cos(hash * 17.0), sin(hash * 23.0));
                xz += wobbleDir * (sin(phase) * _TopTaperIrregularity * taperT);

                return float3(xz.x, posOS.y, xz.y);
            }

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posOS = ApplyTopTaper(IN.positionOS.xyz);
                posOS += SwayOffset(posOS);

                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
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

                // ── Gradiente de llama: altura + ruido ascendente eligen el punto
                // entre los 3 slots, ANTES de aplicar el banding cel de arriba.
                float hNorm = saturate((IN.positionOS.y - _FireHeightOffset) / max(_FireHeightScale, 0.0001));
                float2 noiseUV = IN.positionOS.xz * _FireNoiseScale + float2(0, -_Time.y * _FireScrollSpeed);
                float n = FireNoise(noiseUV);
                float h = saturate(hNorm + (n - 0.5) * _FireFlickerAmount);

                int slotBase = int(_FireSlotBase);
                int slotMid  = int(_FireSlotMid);
                int slotTip  = int(_FireSlotTip);

                float t1 = saturate(h * 2.0);
                float t2 = saturate(h * 2.0 - 1.0);

                float3 lightCol = lerp(
                    lerp(_PaletteLightColors[slotBase].rgb, _PaletteLightColors[slotMid].rgb, t1),
                    _PaletteLightColors[slotTip].rgb, t2);
                float3 midCol = lerp(
                    lerp(_PaletteMidColors[slotBase].rgb, _PaletteMidColors[slotMid].rgb, t1),
                    _PaletteMidColors[slotTip].rgb, t2);
                float3 shadowCol = lerp(
                    lerp(_PaletteShadowColors[slotBase].rgb, _PaletteShadowColors[slotMid].rgb, t1),
                    _PaletteShadowColors[slotTip].rgb, t2);

                float3 color = lerp(shadowCol, midCol, celShadow);
                color        = lerp(color,     lightCol, celLight);

                color = saturate(color + addTint * _LightTintStrength);
                color += color * _FireEmissionStrength;

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
                float  _FireSlotBase;
                float  _FireSlotMid;
                float  _FireSlotTip;
                float  _FireHeightOffset;
                float  _FireHeightScale;
                float  _FireNoiseScale;
                float  _FireScrollSpeed;
                float  _FireFlickerAmount;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _FireEmissionStrength;
                float  _EnableSway;
                float  _SwayAmount;
                float  _SwaySpeed;
                float  _TopTaperAmount;
                float  _TopTaperStart;
                float  _TopTaperIrregularity;
                float  _TopTaperSpeed;
                float  _AlphaCutoff;
                float  _DitherScale;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Idéntica a la de la pass Forward — ver comentario ahí. Se duplica para
            // que la silueta de la sombra matchee la del taper visible.
            float3 ApplyTopTaper(float3 posOS)
            {
                float hNorm  = saturate((posOS.y - _FireHeightOffset) / max(_FireHeightScale, 0.0001));
                float taperT = smoothstep(_TopTaperStart, 1.0, hNorm);
                if (taperT <= 0.0) return posOS;

                float hash = Hash21(posOS.xz * 13.7);

                float shrink = taperT * _TopTaperAmount;
                float2 xz = posOS.xz * (1.0 - shrink);

                float phase = _Time.y * _TopTaperSpeed + hash * 6.2831853;
                float2 wobbleDir = float2(cos(hash * 17.0), sin(hash * 23.0));
                xz += wobbleDir * (sin(phase) * _TopTaperIrregularity * taperT);

                return float3(xz.x, posOS.y, xz.y);
            }

            struct SCAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SCVary { float4 posCS : SV_POSITION; };

            SCVary ShadowVert(SCAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                SCVary OUT;
                float3 taperedOS = ApplyTopTaper(IN.posOS.xyz);
                float3 posWS    = TransformObjectToWorld(taperedOS);
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
                float  _FireSlotBase;
                float  _FireSlotMid;
                float  _FireSlotTip;
                float  _FireHeightOffset;
                float  _FireHeightScale;
                float  _FireNoiseScale;
                float  _FireScrollSpeed;
                float  _FireFlickerAmount;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _FireEmissionStrength;
                float  _EnableSway;
                float  _SwayAmount;
                float  _SwaySpeed;
                float  _TopTaperAmount;
                float  _TopTaperStart;
                float  _TopTaperIrregularity;
                float  _TopTaperSpeed;
                float  _AlphaCutoff;
                float  _DitherScale;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 ApplyTopTaper(float3 posOS)
            {
                float hNorm  = saturate((posOS.y - _FireHeightOffset) / max(_FireHeightScale, 0.0001));
                float taperT = smoothstep(_TopTaperStart, 1.0, hNorm);
                if (taperT <= 0.0) return posOS;

                float hash = Hash21(posOS.xz * 13.7);

                float shrink = taperT * _TopTaperAmount;
                float2 xz = posOS.xz * (1.0 - shrink);

                float phase = _Time.y * _TopTaperSpeed + hash * 6.2831853;
                float2 wobbleDir = float2(cos(hash * 17.0), sin(hash * 23.0));
                xz += wobbleDir * (sin(phase) * _TopTaperIrregularity * taperT);

                return float3(xz.x, posOS.y, xz.y);
            }

            struct DOAttr { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 posCS : SV_POSITION; };

            DOVary DepthVert(DOAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DOVary OUT;
                OUT.posCS = TransformObjectToHClip(ApplyTopTaper(IN.posOS.xyz));
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
                float  _FireSlotBase;
                float  _FireSlotMid;
                float  _FireSlotTip;
                float  _FireHeightOffset;
                float  _FireHeightScale;
                float  _FireNoiseScale;
                float  _FireScrollSpeed;
                float  _FireFlickerAmount;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _FireEmissionStrength;
                float  _EnableSway;
                float  _SwayAmount;
                float  _SwaySpeed;
                float  _TopTaperAmount;
                float  _TopTaperStart;
                float  _TopTaperIrregularity;
                float  _TopTaperSpeed;
                float  _AlphaCutoff;
                float  _DitherScale;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 ApplyTopTaper(float3 posOS)
            {
                float hNorm  = saturate((posOS.y - _FireHeightOffset) / max(_FireHeightScale, 0.0001));
                float taperT = smoothstep(_TopTaperStart, 1.0, hNorm);
                if (taperT <= 0.0) return posOS;

                float hash = Hash21(posOS.xz * 13.7);

                float shrink = taperT * _TopTaperAmount;
                float2 xz = posOS.xz * (1.0 - shrink);

                float phase = _Time.y * _TopTaperSpeed + hash * 6.2831853;
                float2 wobbleDir = float2(cos(hash * 17.0), sin(hash * 23.0));
                xz += wobbleDir * (sin(phase) * _TopTaperIrregularity * taperT);

                return float3(xz.x, posOS.y, xz.y);
            }

            struct DNAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVary { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVary DNVert(DNAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DNVary OUT;
                OUT.posCS    = TransformObjectToHClip(ApplyTopTaper(IN.posOS.xyz));
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
