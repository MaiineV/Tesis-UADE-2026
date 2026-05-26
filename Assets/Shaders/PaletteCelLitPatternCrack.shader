// Variante de PaletteCelLitPattern con grietas procedurales.
// Usa Voronoi (F2-F1) para generar líneas que se bifurcan en Y/T
// como grietas reales de piedra/concreto.
// Un segundo noise de baja frecuencia controla dónde aparecen las grietas,
// evitando que la superficie quede completamente agrietada.
// World-space coordinates: seamless entre tiles adyacentes.
//
// Feature parity con PaletteCelLit:
//   ✅ 3 bandas cel (shadow/mid/light) — multiplicativas
//   ✅ Mid threshold separado
//   ✅ Border Dither / Shadow Dither
//   ✅ Crease (NdotV)

Shader "Rollgeon/PaletteCelLitPatternCrack"
{
    Properties
    {
        [Header(Pattern Shape)]
        _ShapeType   ("Shape   (0=Sq 1=Diamond 2=Hex 3=Circle 4=Cross)", Range(0,4)) = 1
        _PatternType ("Pattern (0=Grid 1=Checker 2=Brick 3=StripeH 4=StripeV)", Range(0,4)) = 1
        _DetailType  ("Detail  (0=Flat 1=BorderOnly 2=Fill+Border 3=Radial)", Range(0,3)) = 2

        _TileScale   ("Tile Scale",                          Float)         = 5
        _ShapeSize   ("Shape Size  (>1=se intersecan)",      Range(0.1,3))  = 0.94
        _ShapeScaleX ("Shape Width  (escala horizontal)",    Range(0.1,3))  = 1.0
        _ShapeScaleY ("Shape Height (escala vertical)",      Range(0.1,3))  = 1.0
        _ShapeOpacity("Shape Opacity",                       Range(0,1))    = 1.0
        _BorderWidth ("Border Width",                        Range(0,0.49)) = 0.05
        _SolidRows   ("Solid Rows (sin alternancia, abajo)", Range(0,20))   = 1

        [Header(Colors)]
        _ColorA      ("Color A  (base / celdas pares)",        Color) = (0.38, 0.10, 0.10, 1)
        _ColorB      ("Color B  (celdas impares)",             Color) = (0.26, 0.06, 0.06, 1)
        _DetailColor ("Detail Color  (borde / centro radial)", Color) = (0.55, 0.42, 0.05, 1)
        _BGColor     ("Background Color (fuera de la forma)", Color)  = (0.18, 0.04, 0.04, 1)

        [Header(Cel Controls)]
        _ShadowThreshold ("Shadow Threshold", Range(0,1))    = 0.35
        _MidThreshold    ("Mid Threshold",    Range(0,1))    = 0.65
        _ShadowSmooth    ("Shadow Smooth",    Range(0,0.3))  = 0.02
        _LightWrap       ("Light Wrap",       Range(-1,1))   = 0.1
        _ShadowDarken    ("Shadow Darken",    Range(0,1))    = 0.50
        _LightBrighten   ("Light Brighten",   Range(0,1))    = 0.18

        [Header(Dither)]
        [Toggle] _UseDither       ("Border Dither",          Float)      = 0
        _DitherStrength           ("Border Dither Strength", Range(0,1)) = 0.15
        [Toggle] _UseShadowDither ("Shadow Dither",          Float)      = 0
        _ShadowDitherDensity      ("Shadow Dither Density",  Range(0,1)) = 0.3

        [Header(Additional Lights)]
        _LightTintStrength        ("Spotlight Tint",         Range(0,1)) = 0.4

        [Header(Crease)]
        [Toggle] _EnableCrease ("Enable Crease",    Float)       = 0
        _CreaseDarken          ("Crease Darken",    Range(0,1))  = 0.4
        _CreaseThreshold       ("Crease Threshold", Range(0,1))  = 0.35
        _CreaseSmooth          ("Crease Smooth",    Range(0,0.3))= 0.05
        _CreaseAlpha           ("Crease Alpha",     Range(0,1))  = 0.8
        [Toggle] _CreaseDither ("Crease Dither",    Float)       = 0

        [Header(Cracks)]
        [Toggle] _EnableCracks  ("Enable Cracks",                    Float)        = 1
        _CrackScale             ("Crack Scale      (tamano celda)",  Float)        = 3.0
        _CrackWidth             ("Crack Width      (grosor grieta)", Range(0,0.3)) = 0.08
        _CrackDarken            ("Crack Darken",                     Range(0,1))   = 0.55
        _CrackDensity           ("Crack Density    (0=todo 1=nada)", Range(0,1))   = 0.45
        _CrackDensityScale      ("Crack Density Scale",              Float)        = 0.7
        _CrackSeed              ("Crack Seed",                       Float)        = 0.0

        [Header(Alpha Cutoff)]
        // 1 = totalmente visible, 0 = totalmente oculto.
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _ShapeType;
                float  _PatternType;
                float  _DetailType;
                float  _TileScale;
                float  _ShapeSize;
                float  _ShapeScaleX;
                float  _ShapeScaleY;
                float  _ShapeOpacity;
                float  _BorderWidth;
                float  _SolidRows;
                float4 _ColorA;
                float4 _ColorB;
                float4 _DetailColor;
                float4 _BGColor;
                float  _ShadowThreshold;
                float  _MidThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _ShadowDarken;
                float  _LightBrighten;
                float  _UseDither;
                float  _DitherStrength;
                float  _UseShadowDither;
                float  _ShadowDitherDensity;
                float  _EnableCrease;
                float  _CreaseDarken;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
                float  _EnableCracks;
                float  _CrackScale;
                float  _CrackWidth;
                float  _CrackDarken;
                float  _CrackDensity;
                float  _CrackDensityScale;
                float  _CrackSeed;
                float  _LightTintStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

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
                float2 uv          : TEXCOORD3;
                float3 viewDirWS   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ════════════════════════════════════════════════════════════════════
            // BAYER 4×4
            // ════════════════════════════════════════════════════════════════════

            float BayerDither(float2 screenPos)
            {
                int2 px = int2(floor(screenPos)) & 3;
                int  idx = px.y * 4 + px.x;
                const float bayer[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[idx];
            }

            // ════════════════════════════════════════════════════════════════════
            // SHAPE SDFs
            // ════════════════════════════════════════════════════════════════════

            float SDF_Square(float2 p, float r)  { float2 d = abs(p) - r; return max(d.x, d.y); }
            float SDF_Diamond(float2 p, float r) { return abs(p.x) + abs(p.y) - r; }
            float SDF_Circle(float2 p, float r)  { return length(p) - r * 0.94; }

            float SDF_Hexagon(float2 p, float r)
            {
                const float2 k = float2(-0.8660254, 0.5);
                p = abs(p);
                p -= 2.0 * min(dot(k, p), 0.0) * k;
                p -= float2(clamp(p.x, -0.5773503 * r, 0.5773503 * r), r);
                return length(p) * sign(p.y);
            }

            float SDF_Cross(float2 p, float r)
            {
                float w = r * 0.34;
                float2 d = abs(p);
                return min(max(d.x - w, d.y - r), max(d.y - w, d.x - r));
            }

            float GetSDF(float2 p)
            {
                float r = _ShapeSize * 0.5;
                if (_ShapeType < 0.5) return SDF_Square(p,   r);
                if (_ShapeType < 1.5) return SDF_Diamond(p,  r);
                if (_ShapeType < 2.5) return SDF_Hexagon(p,  r);
                if (_ShapeType < 3.5) return SDF_Circle(p,   r);
                return SDF_Cross(p, r);
            }

            // ════════════════════════════════════════════════════════════════════
            // PATTERN UV & COLOR
            // ════════════════════════════════════════════════════════════════════

            float2 GetCellUV(float2 uv, out float2 cellID)
            {
                float2 s = uv * _TileScale;
                if (_PatternType >= 1.5 && _PatternType < 2.5)
                    s.x += fmod(floor(s.y), 2.0) * 0.5;
                cellID = floor(s);
                return frac(s) - 0.5;
            }

            float3 PatternColor(float2 p, float2 cellID, float sdf)
            {
                float altIdx = 0.0;
                if      (_PatternType < 0.5)  altIdx = 0.0;
                else if (_PatternType < 2.5)  altIdx = fmod(abs(floor(cellID.x)) + abs(floor(cellID.y)), 2.0);
                else if (_PatternType < 3.5)  altIdx = fmod(abs(floor(cellID.y)), 2.0);
                else                          altIdx = fmod(abs(floor(cellID.x)), 2.0);

                if (cellID.y < _SolidRows) altIdx = 0.0;

                float3 baseCol  = altIdx > 0.5 ? _ColorB.rgb : _ColorA.rgb;
                bool   inside   = sdf <  0.0;
                bool   onBorder = sdf >= -_BorderWidth && sdf < 0.0;

                if (_DetailType < 0.5) return inside ? baseCol : _BGColor.rgb;
                if (_DetailType < 1.5) { if (onBorder) return _DetailColor.rgb; return _BGColor.rgb; }
                if (_DetailType < 2.5) { if (onBorder) return _DetailColor.rgb; if (inside) return baseCol; return _BGColor.rgb; }

                if (inside) { float t = saturate(length(p) / (_ShapeSize * 0.47)); return lerp(_DetailColor.rgb, baseCol, t); }
                return _BGColor.rgb;
            }

            // ════════════════════════════════════════════════════════════════════
            // VORONOI (F1, F2) — grietas en bordes de celda
            // ════════════════════════════════════════════════════════════════════

            float2 HashVec2(float2 p)
            {
                float2 q = float2(dot(p, float2(127.1, 311.7)),
                                  dot(p, float2(269.5, 183.3)));
                return frac(sin(q) * 43758.5453);
            }

            void VoronoiF1F2(float2 p, out float F1, out float F2)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                F1 = 8.0;
                F2 = 8.0;

                // Vecinos 3×3
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 cell  = float2(float(i), float(j));
                    float2 pt    = HashVec2(ip + cell); // punto aleatorio en la celda
                    float2 r     = cell + pt - fp;
                    float  d     = dot(r, r);           // distancia² al punto
                    if      (d < F1) { F2 = F1; F1 = d; }
                    else if (d < F2) { F2 = d; }
                }
                F1 = sqrt(F1);
                F2 = sqrt(F2);
            }

            // ════════════════════════════════════════════════════════════════════
            // VALUE NOISE — máscara de densidad de grietas
            // ════════════════════════════════════════════════════════════════════

            float HashScalar(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float DensityNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(HashScalar(i + float2(0,0)), HashScalar(i + float2(1,0)), u.x),
                    lerp(HashScalar(i + float2(0,1)), HashScalar(i + float2(1,1)), u.x),
                    u.y);
            }

            // ════════════════════════════════════════════════════════════════════
            // PROYECCIÓN POR NORMAL DOMINANTE
            // ════════════════════════════════════════════════════════════════════

            float2 WorldUV(float3 posWS, float3 normalWS)
            {
                float3 absN = abs(normalWS);
                if (absN.y > absN.x && absN.y > absN.z) return posWS.xz;
                if (absN.z > absN.x)                    return posWS.xy;
                return posWS.zy;
            }

            // ════════════════════════════════════════════════════════════════════
            // VERTEX / FRAGMENT
            // ════════════════════════════════════════════════════════════════════

            float CelLightVal(float3 normalWS, Light lt, float wrap)
            {
                float NdotL = dot(normalWS, normalize(lt.direction));
                return saturate(NdotL + wrap) * lt.distanceAttenuation * lt.shadowAttenuation;
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

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Alpha cutoff dithered — usado por WallOccluder/runtime via MPB.
                // +1/16 garantiza apagado total en cutoff=0; _DitherScale agranda celdas.
                clip(_AlphaCutoff - (BayerDither(IN.positionCS.xy / _DitherScale) + 1.0/16.0));

                float3 normalWS = normalize(IN.normalWS);

                // ── Patrón procedural ────────────────────────────────────────────
                float2 cellID;
                float2 p   = GetCellUV(IN.uv, cellID);
                float2 pSc = float2(p.x / _ShapeScaleX, p.y / _ShapeScaleY);
                float  sdf = GetSDF(pSc);
                float3 patternColor = PatternColor(pSc, cellID, sdf);
                patternColor = lerp(patternColor, _BGColor.rgb,
                                    step(0.0, -sdf) * (1.0 - _ShapeOpacity));

                // ── Grietas Voronoi (world-space) ────────────────────────────────
                if (_EnableCracks > 0.5)
                {
                    float2 wUV = WorldUV(IN.positionWS, normalWS);

                    // F1, F2 Voronoi para las grietas
                    float F1, F2;
                    VoronoiF1F2(wUV * _CrackScale + _CrackSeed * 7.13, F1, F2);

                    // F2-F1 es pequeño en los bordes de celda → grieta
                    float crackVal  = F2 - F1;
                    float crackMask = smoothstep(_CrackWidth, 0.0, crackVal);

                    // Máscara de densidad: noise lento decide dónde aparecen grietas
                    float densNoise = DensityNoise(wUV * _CrackDensityScale + _CrackSeed * 3.71);
                    // _CrackDensity=0 → todo agrietado, =1 → sin grietas
                    float densMask  = smoothstep(_CrackDensity,
                                                 _CrackDensity + 0.15,
                                                 densNoise);

                    float finalCrack = crackMask * densMask;

                    // Oscurecer el color base donde hay grieta
                    patternColor = lerp(patternColor,
                                        patternColor * (1.0 - _CrackDarken),
                                        finalCrack);
                }

                // ── Luz ──────────────────────────────────────────────────────────
                Light mainLight  = GetMainLight(IN.shadowCoord);
                float lightValue = CelLightVal(normalWS, mainLight, _LightWrap);

                float3 addTint = float3(0, 0, 0);
                #if defined(_FORWARD_PLUS) || defined(_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    inputData.positionWS  = IN.positionWS;
                    inputData.shadowCoord = IN.shadowCoord;
                    float2 normalizedScreenSpaceUV = inputData.normalizedScreenSpaceUV;
                    float3 positionWS = IN.positionWS;
                    LIGHT_LOOP_BEGIN(GetAdditionalLightsCount())
                        Light addLt  = GetAdditionalLight(lightIndex, positionWS);
                        float addVal = CelLightVal(normalWS, addLt, _LightWrap);
                        lightValue   = max(lightValue, addVal);
                        addTint     += addLt.color * addVal;
                    LIGHT_LOOP_END
                }
                #endif

                // ── Border Dither ────────────────────────────────────────────────
                float ditherOffset = 0.0;
                if (_UseDither > 0.5)
                    ditherOffset = (BayerDither(IN.positionCS.xy) - 0.5) * _DitherStrength;

                // ── 3 bandas cel ─────────────────────────────────────────────────
                float celShadow = smoothstep(
                    _ShadowThreshold - _ShadowSmooth + ditherOffset,
                    _ShadowThreshold + _ShadowSmooth + ditherOffset,
                    lightValue);

                float celLight = smoothstep(
                    _MidThreshold - _ShadowSmooth + ditherOffset,
                    _MidThreshold + _ShadowSmooth + ditherOffset,
                    lightValue);

                float3 shadowColor = patternColor * (1.0 - _ShadowDarken);
                float3 lightColor  = min(patternColor * (1.0 + _LightBrighten), 1.0);

                float3 color = lerp(shadowColor, patternColor, celShadow);
                color        = lerp(color,       lightColor,   celLight);

                // ── Shadow Dither ────────────────────────────────────────────────
                if (_UseShadowDither > 0.5)
                {
                    float bayer    = BayerDither(IN.positionCS.xy);
                    float inShadow = 1.0 - celShadow;
                    float d        = step(bayer, _ShadowDitherDensity) * inShadow;
                    color          = lerp(color, patternColor, d);
                }

                // ── Crease (NdotV) ───────────────────────────────────────────────
                if (_EnableCrease > 0.5)
                {
                    float3 viewDir = normalize(IN.viewDirWS);
                    float  NdotV   = abs(dot(normalWS, viewDir));

                    float creaseDitherOff = 0.0;
                    if (_CreaseDither > 0.5)
                        creaseDitherOff = (BayerDither(IN.positionCS.xy) - 0.5) * _DitherStrength;

                    float creaseVal = smoothstep(
                        _CreaseThreshold + _CreaseSmooth + creaseDitherOff,
                        _CreaseThreshold - _CreaseSmooth + creaseDitherOff,
                        NdotV);

                    float3 creaseColor = patternColor * (1.0 - _CreaseDarken);
                    color = lerp(color, creaseColor, creaseVal * _CreaseAlpha);
                }

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
                float _ShapeType; float _PatternType; float _DetailType;
                float _TileScale; float _ShapeSize; float _ShapeScaleX; float _ShapeScaleY; float _ShapeOpacity;
                float _BorderWidth; float _SolidRows;
                float4 _ColorA; float4 _ColorB; float4 _DetailColor; float4 _BGColor;
                float _ShadowThreshold; float _MidThreshold; float _ShadowSmooth;
                float _LightWrap; float _ShadowDarken; float _LightBrighten;
                float _UseDither; float _DitherStrength;
                float _UseShadowDither; float _ShadowDitherDensity;
                float _EnableCrease; float _CreaseDarken;
                float _CreaseThreshold; float _CreaseSmooth; float _CreaseAlpha; float _CreaseDither;
                float _EnableCracks; float _CrackScale; float _CrackWidth;
                float _CrackDarken; float _CrackDensity; float _CrackDensityScale; float _CrackSeed;
                float _LightTintStrength;
                float _AlphaCutoff;
                float _DitherScale;
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
                float _ShapeType; float _PatternType; float _DetailType;
                float _TileScale; float _ShapeSize; float _ShapeScaleX; float _ShapeScaleY; float _ShapeOpacity;
                float _BorderWidth; float _SolidRows;
                float4 _ColorA; float4 _ColorB; float4 _DetailColor; float4 _BGColor;
                float _ShadowThreshold; float _MidThreshold; float _ShadowSmooth;
                float _LightWrap; float _ShadowDarken; float _LightBrighten;
                float _UseDither; float _DitherStrength;
                float _UseShadowDither; float _ShadowDitherDensity;
                float _EnableCrease; float _CreaseDarken;
                float _CreaseThreshold; float _CreaseSmooth; float _CreaseAlpha; float _CreaseDither;
                float _EnableCracks; float _CrackScale; float _CrackWidth;
                float _CrackDarken; float _CrackDensity; float _CrackDensityScale; float _CrackSeed;
                float _LightTintStrength;
                float _AlphaCutoff;
                float _DitherScale;
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

            struct DOAttr { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 posCS : SV_POSITION; };
            DOVary DepthVert(DOAttr IN) { UNITY_SETUP_INSTANCE_ID(IN); DOVary OUT; OUT.posCS = TransformObjectToHClip(IN.posOS.xyz); return OUT; }
            half4  DepthFrag(DOVary IN) : SV_Target
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
                float _ShapeType; float _PatternType; float _DetailType;
                float _TileScale; float _ShapeSize; float _ShapeScaleX; float _ShapeScaleY; float _ShapeOpacity;
                float _BorderWidth; float _SolidRows;
                float4 _ColorA; float4 _ColorB; float4 _DetailColor; float4 _BGColor;
                float _ShadowThreshold; float _MidThreshold; float _ShadowSmooth;
                float _LightWrap; float _ShadowDarken; float _LightBrighten;
                float _UseDither; float _DitherStrength;
                float _UseShadowDither; float _ShadowDitherDensity;
                float _EnableCrease; float _CreaseDarken;
                float _CreaseThreshold; float _CreaseSmooth; float _CreaseAlpha; float _CreaseDither;
                float _EnableCracks; float _CrackScale; float _CrackWidth;
                float _CrackDarken; float _CrackDensity; float _CrackDensityScale; float _CrackSeed;
                float _LightTintStrength;
                float _AlphaCutoff;
                float _DitherScale;
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
