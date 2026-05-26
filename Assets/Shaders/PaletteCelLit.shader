// Cel shader con colores directos en el material (sin ramp texture).
// Inspirado en el approach de Pelixir: cada material elige sus colores de sombra
// y luz de una paleta, en vez de depender de una textura gradiente.
//
// Uso:
//   - Crear un material por parte del personaje (traje, cuerpo, cabeza, etc.)
//   - Asignar _LightColor y _ShadowColor según la parte
//   - Opcionalmente activar dither para transición suavizada

Shader "Rollgeon/PaletteCelLit"
{
    Properties
    {
        [Header(Palette)]
        [Toggle] _UsePalette ("Use Global Palette", Float) = 0
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

        [Header(Dither)]
        [Toggle] _UseDither       ("Border Dither",          Float) = 0
        _DitherStrength           ("Border Dither Strength",  Range(0, 1)) = 0.15
        [Toggle] _UseShadowDither ("Shadow Dither",           Float) = 0
        _ShadowDitherDensity      ("Shadow Dither Density",   Range(0, 1)) = 0.3

        [Header(Additional Lights)]
        _LightTintStrength        ("Spotlight Tint",          Range(0,1))  = 0.4

        [Header(Crease)]
        [Toggle] _EnableCrease  ("Enable Crease",  Float) = 0
        _CreaseColor            ("Crease Color",   Color) = (0.15, 0.15, 0.2, 1)
        _CreaseThreshold        ("Crease Threshold", Range(0, 1)) = 0.35
        _CreaseSmooth           ("Crease Smooth",    Range(0, 0.3)) = 0.05
        _CreaseAlpha            ("Crease Alpha",     Range(0, 1))   = 0.8
        [Toggle] _CreaseDither  ("Crease Dither",    Float) = 0

        [Header(Alpha Cutoff)]
        // 1 = totalmente visible, 0 = totalmente oculto.
        // El CameraService/WallOccluder modula esta property en runtime para fade-out.
        _AlphaCutoff ("Alpha Cutoff (1=visible, 0=hidden)", Range(0,1)) = 1
        // 1 = pixel-perfect (default); 4 = chunks de 4×4; 32 = bloques grandes.
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── DEBUG ─────────────────────────────────────────────────────────────
            // Cambiá el 0 por 1 para activar el diagnóstico de luces adicionales:
            //   AZUL   → _ADDITIONAL_LIGHTS no está definido (URP Asset issue)
            //   ROJO   → keyword definido pero GetAdditionalLightsCount() == 0
            //   VERDE  → count > 0 pero addTint es casi 0 (atenuación o ángulo)
            //   Color  → addTint amplificado 5× (el spotlight SÍ llega al shader)
            #define DEBUG_ADDITIONAL_LIGHTS 0
            // ─────────────────────────────────────────────────────────────────────

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Bayer 4×4 para dither ────────────────────────────────────────────
            float BayerDither(float2 screenPos)
            {
                int2 p = int2(floor(screenPos)) & 3;
                int  i = p.y * 4 + p.x;
                // Bayer matrix 4×4 (índice → valor en 16 pasos)
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
                return OUT;
            }

            // Acumula contribución cel de una luz sobre el valor [0,1] actual
            float CelLight(float3 normalWS, Light light, float wrapBias)
            {
                float NdotL = dot(normalWS, normalize(light.direction));
                float wrapped = saturate(NdotL + wrapBias);
                return wrapped * light.distanceAttenuation * light.shadowAttenuation;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Alpha cutoff dithered — usado por WallOccluder via MaterialPropertyBlock.
                // _AlphaCutoff = 1 ⇒ todos los pixeles pasan; 0 ⇒ todos clippeados.
                // El +1/16 garantiza apagado total en cutoff=0 (sin él quedaba 1/16 de píxeles).
                // _DitherScale agranda el tamaño de las celdas del dither (1=1px, 4=4×4px chunks).
                clip(_AlphaCutoff - (BayerDither(IN.positionCS.xy / _DitherScale) + 1.0/16.0));

                float3 normalWS = normalize(IN.normalWS);

                // Luz principal
                Light mainLight  = GetMainLight(IN.shadowCoord);
                float lightValue = CelLight(normalWS, mainLight, _LightWrap);

                // Luces adicionales: actualiza lightValue + acumula tinte de color
                // LIGHT_LOOP_BEGIN maneja Forward (usa _ADDITIONAL_LIGHTS) y
                // Forward+ (usa tile clustering — requiere normalizedScreenSpaceUV).
                float3 addTint = float3(0, 0, 0);
                #if defined(_FORWARD_PLUS) || defined(_ADDITIONAL_LIGHTS)
                {
                    // URP 17 Forward+ requiere 'inputData' en scope internamente.
                    InputData inputData = (InputData)0;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    inputData.positionWS  = IN.positionWS;
                    inputData.shadowCoord = IN.shadowCoord;
                    // LIGHT_LOOP_BEGIN necesita estas variables locales con estos nombres exactos.
                    float2 normalizedScreenSpaceUV = inputData.normalizedScreenSpaceUV;
                    float3 positionWS = IN.positionWS;
                    LIGHT_LOOP_BEGIN(GetAdditionalLightsCount())
                        Light addLt  = GetAdditionalLight(lightIndex, positionWS);
                        float addVal = CelLight(normalWS, addLt, _LightWrap);
                        lightValue   = max(lightValue, addVal);
                        addTint     += addLt.color * addVal;
                    LIGHT_LOOP_END
                }
                #endif

                // Dither: desplaza ambos thresholds con el patrón Bayer
                float ditherOffset = 0.0;
                if (_UseDither > 0.5)
                    ditherOffset = (BayerDither(IN.positionCS.xy) - 0.5) * _DitherStrength;

                // Step 1: sombra → mid
                float celShadow = smoothstep(
                    _ShadowThreshold - _ShadowSmooth + ditherOffset,
                    _ShadowThreshold + _ShadowSmooth + ditherOffset,
                    lightValue);

                // Step 2: mid → luz
                float celLight = smoothstep(
                    _MidThreshold - _ShadowSmooth + ditherOffset,
                    _MidThreshold + _ShadowSmooth + ditherOffset,
                    lightValue);

                // Selección de colores: paleta global o colores del material
                int    slot      = int(_PaletteSlot);
                float3 lightCol  = _UsePalette > 0.5 ? _PaletteLightColors[slot].rgb  : _LightColor.rgb;
                float3 midCol    = _UsePalette > 0.5 ? _PaletteMidColors[slot].rgb    : _MidColor.rgb;
                float3 shadowCol = _UsePalette > 0.5 ? _PaletteShadowColors[slot].rgb : _ShadowColor.rgb;

                // Shadow → Mid → Light
                float3 color = lerp(shadowCol, midCol,    celShadow);
                color        = lerp(color,     lightCol,  celLight);

                // Shadow Dither: puntea el interior de la zona de sombra con el patrón Bayer.
                // Píxeles donde bayer < density "saltan" al color mid, creando un efecto
                // de sombra granulada / halftone. Solo actúa donde celShadow < 1 (zona oscura).
                if (_UseShadowDither > 0.5)
                {
                    float bayer    = BayerDither(IN.positionCS.xy);
                    float inShadow = 1.0 - celShadow;
                    float dot      = step(bayer, _ShadowDitherDensity) * inShadow;
                    color          = lerp(color, _MidColor.rgb, dot);
                }

                // Crease (NdotV): highlights silhouette edges per-object
                if (_EnableCrease > 0.5)
                {
                    float3 viewDirWS = normalize(IN.viewDirWS);
                    float NdotV = abs(dot(normalWS, viewDirWS));
                    // Dither opcional sobre el umbral del crease
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

                // ── DEBUG (activar con #define DEBUG_ADDITIONAL_LIGHTS 1) ────────
                // NARANJA → renderer Forward+, luces 0 o sin atenuación
                // AZUL    → ni Forward ni Forward+ activos (URP Asset issue)
                // VERDE   → luces detectadas pero addTint≈0 (ángulo / rango / sombra)
                // COLOR   → addTint amplificado 5× — iluminación llegando correctamente
                #if DEBUG_ADDITIONAL_LIGHTS
                #if defined(_FORWARD_PLUS)
                {
                    float _dbgMag = dot(addTint, addTint);
                    if (_dbgMag < 0.0001)
                        return half4(1, 0.5, 0, 1);            // NARANJA → Forward+ pero tint≈0
                    return half4(addTint * 5.0, 1.0);          // COLOR   → Forward+ OK
                }
                #elif defined(_ADDITIONAL_LIGHTS)
                {
                    uint _dbgCount = GetAdditionalLightsCount();
                    if (_dbgCount == 0)
                        return half4(1, 0, 0, 1);              // ROJO  → count==0
                    float _dbgMag = dot(addTint, addTint);
                    if (_dbgMag < 0.0001)
                        return half4(0, 1, 0, 1);              // VERDE → tint≈0
                    return half4(addTint * 5.0, 1.0);          // COLOR → Forward OK
                }
                #else
                    return half4(0, 0, 1, 1);                  // AZUL → sin keyword
                #endif
                #endif
                // ─────────────────────────────────────────────────────────────────

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
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            float3 _LightDirection;
            float3 _LightPosition;

            // Sin esto el shadow del objeto queda visible aunque el forward esté dithered out.
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
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
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
        // Necesario para que GodotParityPost detecte outlines con normales.
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
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
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
