// Tile de curación unlit: pulso dorado (Seno remapeado), círculo de área de
// efecto (length de UV centradas + smoothstep), destellos ascendentes
// (UV scrolleada en el tiempo + Voronoi + Step duro) y fresnel de borde,
// todo mezclado sobre una base de piedra plana por Lerp. Unlit puro por
// decisión del usuario — no hay banding NdotL, la luz de escena no afecta
// al tile. Voronoi copiado de CelVenom.shader ya con la distancia
// euclidiana corregida (no la Manhattan vieja de BackGround1).
Shader "Rollgeon/HealingTile"
{
    Properties
    {
        [Header(Healing Palette 2 Slots)]
        [PaletteSlot] _TileSlotStone ("Stone Slot", Float) = 2
        [PaletteSlot] _TileSlotGold  ("Gold Slot",  Float) = 5

        [Header(Golden Pulse)]
        _PulseSpeed ("Pulse Speed", Float)      = 2
        _PulseMin   ("Pulse Min",   Range(0,1)) = 0.6
        _PulseMax   ("Pulse Max",   Range(0,2)) = 1.0

        [Header(Healing Circle)]
        _Radius ("Radius",      Range(0, 0.71))    = 0.35
        _Smooth ("Edge Smooth", Range(0.001, 0.5)) = 0.1

        [Header(Rising Sparkles)]
        _SparkleScale     ("Sparkle Tiling",    Float)       = 12
        _ScrollSpeed      ("Scroll Speed Y",     Float)       = 0.5
        _SparkleThreshold ("Sparkle Threshold",  Range(0,1))  = 0.8

        [Header(Fresnel Rim)]
        _FresnelPower    ("Fresnel Power",    Range(0.1, 8)) = 3
        _FresnelStrength ("Fresnel Strength", Range(0, 4))   = 1.5

        [Header(Emission)]
        _EmissionStrength ("Gold Emission Strength", Range(0, 8)) = 2

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
                float _TileSlotStone;
                float _TileSlotGold;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _Radius;
                float _Smooth;
                float _SparkleScale;
                float _ScrollSpeed;
                float _SparkleThreshold;
                float _FresnelPower;
                float _FresnelStrength;
                float _EmissionStrength;
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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
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

            // ── Voronoi (calcado de CelVenom.shader, distancia euclidiana) ──────
            float2 voronoihash39(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float voronoi39(float2 v, float time, inout float2 id, inout float2 mr, float smoothness)
            {
                float2 n = floor(v);
                float2 f = frac(v);
                float F1 = 8.0;
                float F2 = 8.0; float2 mg = 0;
                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 o = voronoihash39(n + g);
                        o = (sin(time + o * 6.2831) * 0.5 + 0.5); float2 r = f - g - o;
                        float d = length(r);
                        if (d < F1) { F2 = F1; F1 = d; mg = g; mr = r; id = o; }
                        else if (d < F2) { F2 = d; }
                    }
                }
                return F1;
            }

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS   = vni.normalWS;
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                clip(_AlphaCutoff - (BayerDither(IN.positionCS.xy / _DitherScale) + 1.0/16.0));

                int slotStone = int(_TileSlotStone);
                int slotGold  = int(_TileSlotGold);

                // 1. Latido: Seno del tiempo remapeado de -1..1 a Min..Max.
                float pulse = lerp(_PulseMin, _PulseMax, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);
                float3 goldBase  = _PaletteMidColors[slotGold].rgb;
                float3 goldPulse = goldBase * pulse;

                // 2. Círculo de curación: UV centradas -> length -> smoothstep.
                float2 centered  = IN.uv - 0.5;
                float  dist       = length(centered);
                float  circleMask = 1.0 - smoothstep(_Radius, _Radius + _Smooth, dist);

                // 3. Destellos ascendentes: UV scrolleada en Y por el tiempo -> Voronoi -> Step duro.
                float2 sparkleUV = IN.uv * _SparkleScale + float2(0, _Time.y * _ScrollSpeed);
                float2 id = 0, mr = 0;
                float  voro       = voronoi39(sparkleUV, 0.0, id, mr, 0);
                float  brightness = saturate(1.0 - voro);
                float  sparkleMask = step(_SparkleThreshold, brightness);

                // 4. Piedra <-> dorado, factor = círculo + destellos.
                float3 stoneCol = _PaletteMidColors[slotStone].rgb;
                float  t = saturate(circleMask + sparkleMask);
                float3 color = lerp(stoneCol, goldPulse * _EmissionStrength, t);

                // 5. Fresnel: rim dorado sumado al final.
                float3 normalWS  = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  fresnel   = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower) * _FresnelStrength;
                color += fresnel * goldBase;

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
                float _TileSlotStone;
                float _TileSlotGold;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _Radius;
                float _Smooth;
                float _SparkleScale;
                float _ScrollSpeed;
                float _SparkleThreshold;
                float _FresnelPower;
                float _FresnelStrength;
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
                float _TileSlotStone;
                float _TileSlotGold;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _Radius;
                float _Smooth;
                float _SparkleScale;
                float _ScrollSpeed;
                float _SparkleThreshold;
                float _FresnelPower;
                float _FresnelStrength;
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
                float _TileSlotStone;
                float _TileSlotGold;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _Radius;
                float _Smooth;
                float _SparkleScale;
                float _ScrollSpeed;
                float _SparkleThreshold;
                float _FresnelPower;
                float _FresnelStrength;
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
