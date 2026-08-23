// Tile de velocidad unlit: chevrons/flechas (">") que recorren el tile hacia
// la dirección de viaje, estilo dash panel de Mario Kart. El "V" de cada
// flecha se arma doblando la coordenada de tiling con abs(p.x) antes de
// scrollearla en el tiempo; franjas duras vía smoothstep en vez de un
// gradiente, mismo criterio toon del resto de la familia. Se suman
// "speed streaks" (líneas finas discontinuas a lo largo del recorrido, con
// su propio scroll más rápido) para dar sensación de parallax/rush. Unlit
// puro, mismo criterio que Healing/DamageTile — sin banding NdotL.
Shader "Rollgeon/SpeedTile"
{
    Properties
    {
        [Header(Speed Palette 2 Slots)]
        [PaletteSlot] _TileSlotPad   ("Pad Slot (base)", Float) = 3
        [PaletteSlot] _TileSlotArrow ("Arrow Slot",       Float) = 5

        [Header(Energy Pulse)]
        _PulseSpeed ("Pulse Speed", Float)      = 4
        _PulseMin   ("Pulse Min",   Range(0,1)) = 0.7
        _PulseMax   ("Pulse Max",   Range(0,2)) = 1.3

        [Header(Chevron Arrows)]
        _ChevronRepeat ("Chevron Repeat (filas)",   Float)          = 4
        _ChevronAngle  ("Chevron Angle (forma V)",  Range(0, 8))    = 3
        _ChevronWidth  ("Chevron Width",            Range(0.01,0.5))= 0.18
        _ChevronSmooth ("Chevron Edge Smooth",      Range(0.001,0.3))=0.04
        _ScrollSpeed   ("Scroll Speed (dir. viaje)", Float)         = 1.5

        [Header(Speed Streaks)]
        _StreakCount       ("Streak Count",         Float)         = 10
        _StreakWidth        ("Streak Width",         Range(0.01,0.5))=0.08
        _StreakDashFreq      ("Streak Dash Frequency",Float)         = 6
        _StreakDashWidth    ("Streak Dash Width",    Range(0.01,0.9))=0.4
        _StreakScrollSpeed  ("Streak Scroll Speed",  Float)         = 3

        [Header(Fresnel Rim)]
        _FresnelPower    ("Fresnel Power",    Range(0.1, 8)) = 3
        _FresnelStrength ("Fresnel Strength", Range(0, 4))   = 1.2

        [Header(Emission)]
        _EmissionStrength ("Arrow Emission Strength", Range(0, 8)) = 2.5

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
                float _TileSlotPad;
                float _TileSlotArrow;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _ChevronRepeat;
                float _ChevronAngle;
                float _ChevronWidth;
                float _ChevronSmooth;
                float _ScrollSpeed;
                float _StreakCount;
                float _StreakWidth;
                float _StreakDashFreq;
                float _StreakDashWidth;
                float _StreakScrollSpeed;
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

                int slotPad   = int(_TileSlotPad);
                int slotArrow = int(_TileSlotArrow);

                // Pulso de energía: flashea el color de la flecha al compás.
                float pulse = lerp(_PulseMin, _PulseMax, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);
                float3 arrowBase  = _PaletteMidColors[slotArrow].rgb;
                float3 arrowPulse = arrowBase * pulse;

                // Chevrons ">" que viajan: se dobla la coordenada de tiling con
                // abs(p.x) antes de scrollear en el tiempo, formando una "V" que
                // se repite _ChevronRepeat veces y fluye en la dirección de viaje.
                float2 p = IN.uv - 0.5;
                float  chevronCoord = p.y * _ChevronRepeat + abs(p.x) * _ChevronAngle - _Time.y * _ScrollSpeed;
                float  band = frac(chevronCoord);
                float  arrowMask = smoothstep(0.0, _ChevronSmooth, band)
                                  * (1.0 - smoothstep(_ChevronWidth, _ChevronWidth + _ChevronSmooth, band));

                // Speed streaks: líneas finas a lo largo del recorrido, cortadas en
                // guiones que scrollean más rápido que las flechas (parallax de rush).
                float streakX    = frac(IN.uv.x * _StreakCount);
                float streakLine = 1.0 - smoothstep(0.0, _StreakWidth, abs(streakX - 0.5));
                float dash       = frac(IN.uv.y * _StreakDashFreq - _Time.y * _StreakScrollSpeed);
                float dashMask   = step(dash, _StreakDashWidth);
                float streakMask = streakLine * dashMask;

                float3 padCol = _PaletteMidColors[slotPad].rgb;
                float  t = saturate(arrowMask + streakMask * 0.6);
                float3 color = lerp(padCol, arrowPulse * _EmissionStrength, t);

                // Fresnel: rim que resalta el pad en ángulos rasantes.
                float3 normalWS  = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  fresnel   = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower) * _FresnelStrength;
                color += fresnel * arrowBase;

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
                float _TileSlotPad;
                float _TileSlotArrow;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _ChevronRepeat;
                float _ChevronAngle;
                float _ChevronWidth;
                float _ChevronSmooth;
                float _ScrollSpeed;
                float _StreakCount;
                float _StreakWidth;
                float _StreakDashFreq;
                float _StreakDashWidth;
                float _StreakScrollSpeed;
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
                float _TileSlotPad;
                float _TileSlotArrow;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _ChevronRepeat;
                float _ChevronAngle;
                float _ChevronWidth;
                float _ChevronSmooth;
                float _ScrollSpeed;
                float _StreakCount;
                float _StreakWidth;
                float _StreakDashFreq;
                float _StreakDashWidth;
                float _StreakScrollSpeed;
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
                float _TileSlotPad;
                float _TileSlotArrow;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
                float _ChevronRepeat;
                float _ChevronAngle;
                float _ChevronWidth;
                float _ChevronSmooth;
                float _ScrollSpeed;
                float _StreakCount;
                float _StreakWidth;
                float _StreakDashFreq;
                float _StreakDashWidth;
                float _StreakScrollSpeed;
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
