// Variante de LuzLed para anillos con UV plana (proyección frontal), no cinta
// perimetral: en Cylinder.006 de RouletteShop.fbx los UVs literalmente calcan
// la forma del donut (U = 0.5 + r*cos(ángulo), V = 0.5 + r*sin(ángulo),
// confirmado leyendo vértices reales — 16 gajos a pasos de 22.5°), así que acá
// no alcanza con leer UV.x como ángulo ya desenrollado: hay que recalcularlo
// con atan2 sobre (UV - 0.5) por píxel.
//
// atan2(V-0.5, U-0.5) crece en sentido antihorario tal como se ve la textura
// (convención estándar UV/matemática). Para barrer en sentido horario por
// default (_ChaseDirection = 1) se invierte a angleCW = frac(1 - angleCCW).
//
// Unlit + Emission, sin banding de luz real — mismo criterio que
// PaletteCelSpinFloat: es un efecto de luces tipo marquesina que se auto-
// ilumina, no un prop que reciba sombreado de escena. Los gajos ciclan entre
// 4 colores de paleta (wedgeIndex % 4) y un highlight recorre el anillo
// prendiendo cada gajo a su paso.
Shader "Rollgeon/LuzLedRing"
{
    Properties
    {
        [Header(Palette 4 Colores)]
        [PaletteSlot] _Color0Slot ("Color 0 Slot", Float) = 0
        [PaletteSlot] _Color1Slot ("Color 1 Slot", Float) = 1
        [PaletteSlot] _Color2Slot ("Color 2 Slot", Float) = 2
        [PaletteSlot] _Color3Slot ("Color 3 Slot", Float) = 3

        [Header(Ring Pattern)]
        _WedgeCount ("Wedge Count (gajos en el anillo, Cylinder.006 = 16)", Float) = 16

        [Header(Chase Horario)]
        _ChaseSpeed     ("Chase Speed",                                Float)          = 1.0
        _ChaseDirection ("Chase Direction (1 horario, -1 antihorario)", Float)          = 1
        _ChaseWidth     ("Chase Highlight Width",                      Range(0.01, 1)) = 0.15
        _ChaseCount     ("Chase Lights Count (simultaneos)",           Range(1, 8))    = 1

        [Header(Brightness)]
        _DimStrength      ("Dim (unlit) Brightness", Range(0, 1)) = 0.25
        _EmissionStrength ("Lit Emission Strength",  Range(0, 8)) = 3

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

        // ── Forward Unlit ────────────────────────────────────────────────────────
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Color0Slot;
                float _Color1Slot;
                float _Color2Slot;
                float _Color3Slot;
                float _WedgeCount;
                float _ChaseSpeed;
                float _ChaseDirection;
                float _ChaseWidth;
                float _ChaseCount;
                float _DimStrength;
                float _EmissionStrength;
                float _AlphaCutoff;
                float _DitherScale;
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteLightColors[32];
            float4 _PaletteShadowColors[32];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
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

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                clip(_AlphaCutoff - (BayerDither(IN.positionCS.xy / _DitherScale) + 1.0/16.0));

                // Ángulo polar recalculado desde la UV plana (no perimetral) —
                // ver comentario de cabecera. angleCCW crece antihorario (estándar
                // UV/matemático); angleCW crece horario, que es lo que barre el chase.
                float2 centered  = IN.uv - 0.5;
                float  angleCCW  = frac(atan2(centered.y, centered.x) / TWO_PI + 1.0);
                float  angleCW   = frac(1.0 - angleCCW);

                float wedgeCount  = max(_WedgeCount, 1.0);
                float wedgeIndexF = min(floor(angleCW * wedgeCount), wedgeCount - 1.0);
                float wedgeCenter = (wedgeIndexF + 0.5) / wedgeCount;

                // Chase: uno o más highlights recorriendo el anillo (distancia
                // circular para cruzar sin salto la costura 0/1).
                float chaseBase = frac(_Time.y * _ChaseSpeed * _ChaseDirection);
                float brightness = 0.0;
                int chaseCountInt = clamp((int)_ChaseCount, 1, 8);
                for (int c = 0; c < 8; c++)
                {
                    if (c >= chaseCountInt) break;
                    float highlightPos = frac(chaseBase + (float)c / (float)chaseCountInt);
                    float d = abs(wedgeCenter - highlightPos);
                    d = min(d, 1.0 - d);
                    brightness = max(brightness, 1.0 - smoothstep(0.0, _ChaseWidth, d));
                }

                // 4 colores de paleta, uno cada 4 gajos.
                float slots[4] = { _Color0Slot, _Color1Slot, _Color2Slot, _Color3Slot };
                int   colorIndex = (int)fmod(wedgeIndexF, 4.0);
                int   slot       = (int)slots[colorIndex];

                float3 dimColor = _PaletteShadowColors[slot].rgb * _DimStrength;
                float3 litColor = _PaletteLightColors[slot].rgb * _EmissionStrength;
                float3 color    = lerp(dimColor, litColor, brightness);

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
                float _Color0Slot;
                float _Color1Slot;
                float _Color2Slot;
                float _Color3Slot;
                float _WedgeCount;
                float _ChaseSpeed;
                float _ChaseDirection;
                float _ChaseWidth;
                float _ChaseCount;
                float _DimStrength;
                float _EmissionStrength;
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
                float _Color0Slot;
                float _Color1Slot;
                float _Color2Slot;
                float _Color3Slot;
                float _WedgeCount;
                float _ChaseSpeed;
                float _ChaseDirection;
                float _ChaseWidth;
                float _ChaseCount;
                float _DimStrength;
                float _EmissionStrength;
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
                float _Color0Slot;
                float _Color1Slot;
                float _Color2Slot;
                float _Color3Slot;
                float _WedgeCount;
                float _ChaseSpeed;
                float _ChaseDirection;
                float _ChaseWidth;
                float _ChaseCount;
                float _DimStrength;
                float _EmissionStrength;
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
