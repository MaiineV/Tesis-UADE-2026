// Cubo flotante Unlit + Emission: gira sobre su propio eje Y y flota
// arriba/abajo, con PA_MainPalette pero SIN banding NdotL — a pedido
// explícito del usuario, este cubo en particular no depende de la luz de
// escena, siempre brilla con su tono de paleta boosteado por emission
// (mismo criterio que StarCrown/HealingTile). El hermano
// PaletteCelSpinFloatPulse.shader queda Lit con banding real, sin tocar.
// Spin: rotación 2D en XZ (espacio objeto) alrededor del pivot. Bob: seno
// con fase-por-pivot, para desincronizar varias instancias en la misma sala.
Shader "Rollgeon/PaletteCelSpinFloat"
{
    Properties
    {
        [Header(Palette)]
        [ToggleUI] _UsePalette ("Use Global Palette", Float) = 1
        [PaletteSlot] _PaletteSlot ("Palette Slot", Float) = 5

        [Header(Fallback Color)]
        _MidColor ("Color (si no usa paleta)", Color) = (1, 0.8, 0.2, 1)

        [Header(Spin)]
        _SpinSpeed ("Spin Speed (deg/sec)", Float) = 60

        [Header(Float Bob)]
        _BobAmplitude ("Bob Amplitude", Float) = 0.15
        _BobSpeed     ("Bob Speed",     Float) = 1.5

        [Header(Emission)]
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 2

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
                float  _UsePalette;
                float  _PaletteSlot;
                float4 _MidColor;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _EmissionStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

            // Array global subido por GlobalPaletteManager cada frame
            float4 _PaletteMidColors[32];

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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

            // Rotación 2D en XZ (espacio objeto) alrededor del pivot.
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
            // Bob en Y con fase derivada del pivot world-space — desincroniza
            // varias instancias del mismo material.
            float ApplyBobY(float3 pivotWS)
            {
                float phase = frac(sin(dot(pivotWS.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2831853;
                return sin(_Time.y * _BobSpeed + phase) * _BobAmplitude;
            }

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float2 sc = SpinAngleSinCos();
                float3 posOS = ApplySpinPos(IN.positionOS.xyz, sc);
                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                posOS.y += ApplyBobY(pivotWS);

                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                clip(_AlphaCutoff - (BayerDither(IN.positionCS.xy / _DitherScale) + 1.0/16.0));

                int    slot    = int(_PaletteSlot);
                float3 baseCol = _UsePalette > 0.5 ? _PaletteMidColors[slot].rgb : _MidColor.rgb;
                float3 color   = baseCol * _EmissionStrength;

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
                float4 _MidColor;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _EmissionStrength;
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

            struct SCAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SCVary { float4 posCS : SV_POSITION; };

            SCVary ShadowVert(SCAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                SCVary OUT;

                float2 sc = SpinAngleSinCos();
                float3 posOS = ApplySpinPos(IN.posOS.xyz, sc);
                float3 nrmOS = ApplySpinNormal(IN.normalOS, sc);
                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
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
                float4 _MidColor;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _EmissionStrength;
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

            struct DOAttr { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 posCS : SV_POSITION; };

            DOVary DepthVert(DOAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DOVary OUT;

                float2 sc = SpinAngleSinCos();
                float3 posOS = ApplySpinPos(IN.posOS.xyz, sc);
                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
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
                float4 _MidColor;
                float  _SpinSpeed;
                float  _BobAmplitude;
                float  _BobSpeed;
                float  _EmissionStrength;
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

            struct DNAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVary { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVary DNVert(DNAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DNVary OUT;

                float2 sc = SpinAngleSinCos();
                float3 posOS = ApplySpinPos(IN.posOS.xyz, sc);
                float3 nrmOS = ApplySpinNormal(IN.normalOS, sc);
                float3 pivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
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
