// Portal de vórtice espiral, unlit/emisivo, colores de PA_MainPalette. Traducción
// HLSL a mano del método de coordenadas polares (radio + ángulo, frac() para el
// loop infinito hacia el centro, borde duro tipo toon) — sin Shader Graph, mismo
// criterio que el resto de los shaders del proyecto (PaletteCelFire/Ice). A
// diferencia de esos dos, este es UNLIT: no hay banding NdotL ni luces
// adicionales — el portal brilla con color propio, ignora la luz de escena.
// Opaco (sin Blend) por requisito explícito.
Shader "Rollgeon/PortalVortex"
{
    Properties
    {
        [Header(Portal Palette 2 Slots)]
        [PaletteSlot] _PortalSlotBase   ("Background Slot", Float) = 0
        [PaletteSlot] _PortalSlotSpiral ("Spiral Slot",      Float) = 3

        [Header(Vortex Spiral)]
        // Campo de espiral = angulo/2pi * _SpiralArms + radio * _SpiralTightness
        // - tiempo*velocidad, repetido con frac(). _SpiralWidth es el ancho del
        // brazo dentro de ese ciclo (step, borde duro tipo toon).
        _SpiralArms      ("Spiral Arms",      Float)      = 3
        _SpiralTightness ("Spiral Tightness", Float)      = 4
        _SpinSpeed       ("Spin Speed",       Float)      = 1.2
        _SpiralWidth     ("Spiral Arm Width", Range(0,1)) = 0.5

        [Header(Portal Void)]
        // "Ojo de gato": el centro se oscurece hacia el Shadow del slot de fondo
        // (no un 3er color manual) para dar sensacion de abismo profundo.
        _VoidRadius ("Void Radius",        Range(0.05, 1)) = 0.5
        _VoidPower  ("Void Falloff Power", Range(0.1, 8))  = 2

        [Header(Emission)]
        _EmissionStrength ("Emission Strength", Range(0, 4)) = 1.2

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
                float  _PortalSlotBase;
                float  _PortalSlotSpiral;
                float  _SpiralArms;
                float  _SpiralTightness;
                float  _SpinSpeed;
                float  _SpiralWidth;
                float  _VoidRadius;
                float  _VoidPower;
                float  _EmissionStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteMidColors[32];
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

                // ── Coordenadas polares centradas en el quad/disco ───────────────
                float2 centered = IN.uv - 0.5;
                float radius = length(centered);
                float angleFrac = atan2(centered.y, centered.x) / (2.0 * PI);

                // Campo de espiral: angulo repetido _SpiralArms veces + radio por
                // tightness, menos el tiempo -> gira. frac() lo repite infinito.
                float spiralField = frac(angleFrac * _SpiralArms + radius * _SpiralTightness - _Time.y * _SpinSpeed);
                float spiralMask = step(spiralField, _SpiralWidth);

                // ── Vacio central: se oscurece hacia el Shadow del slot de fondo ──
                // saturate() ANTES del pow (no despues) — pow con base negativa es
                // comportamiento indefinido en algunas GPUs.
                float voidT = pow(saturate(1.0 - radius / max(_VoidRadius, 0.001)), _VoidPower);

                int slotBase   = int(_PortalSlotBase);
                int slotSpiral = int(_PortalSlotSpiral);

                float3 baseCol   = _PaletteMidColors[slotBase].rgb;
                float3 spiralCol = _PaletteMidColors[slotSpiral].rgb;
                float3 voidCol   = _PaletteShadowColors[slotBase].rgb;

                float3 color = lerp(baseCol, spiralCol, spiralMask);
                color        = lerp(color, voidCol, voidT);
                color       *= _EmissionStrength;

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
                float  _PortalSlotBase;
                float  _PortalSlotSpiral;
                float  _SpiralArms;
                float  _SpiralTightness;
                float  _SpinSpeed;
                float  _SpiralWidth;
                float  _VoidRadius;
                float  _VoidPower;
                float  _EmissionStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

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
                float  _PortalSlotBase;
                float  _PortalSlotSpiral;
                float  _SpiralArms;
                float  _SpiralTightness;
                float  _SpinSpeed;
                float  _SpiralWidth;
                float  _VoidRadius;
                float  _VoidPower;
                float  _EmissionStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

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
                float  _PortalSlotBase;
                float  _PortalSlotSpiral;
                float  _SpiralArms;
                float  _SpiralTightness;
                float  _SpinSpeed;
                float  _SpiralWidth;
                float  _VoidRadius;
                float  _VoidPower;
                float  _EmissionStrength;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

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
