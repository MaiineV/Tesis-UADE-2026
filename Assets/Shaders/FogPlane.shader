// Shader de niebla/nube para un plano horizontal.
//
// Flujo:
//   Vertex — dos noise (A y B) con speed independiente se combinan,
//            se remapean a [-1,1] y se aplica abs() para eliminar valles.
//            El resultado desplaza el plano a lo largo de su normal.
//
//   Fragment — los tres noise se recomputan por pixel para alpha preciso.
//              Cada capa tiene density bias y dither independientes.
//              Screen blending une las capas sin sobre-saturar.
//              La profundidad de escena suaviza la intersección con objetos.
//
// Setup:
//   - Crear un plano subdividido (>= 30x30 quads) en Blender.
//   - Activar "Depth Texture" en el URP Asset.

Shader "Rollgeon/FogPlane"
{
    Properties
    {
        [Header(Color)]
        _Color          ("Fog Color",                        Color)         = (0.75, 0.82, 0.90, 1)
        _Opacity        ("Opacity",                          Range(0,1))    = 0.65
        _AlphaContrast  ("Edge Contrast  menor1 denso mayor1 wispy", Range(0.1,4)) = 1.2

        [Header(Noise A  Capa principal)]
        _NoiseScaleA    ("Scale A  (world units)",           Float)         = 3.5
        _NoiseSpeedAX   ("Speed A  X",                       Float)         = 0.04
        _NoiseSpeedAZ   ("Speed A  Z",                       Float)         = 0.02
        _DensityA       ("Density A",                        Range(0,1))    = 0.25
        _DitherA        ("Dither A",                         Range(0,1))    = 0.0

        [Header(Noise B  Variacion media)]
        _NoiseScaleB    ("Scale B  (world units)",           Float)         = 1.8
        _NoiseSpeedBX   ("Speed B  X",                       Float)         = 0.03
        _NoiseSpeedBZ   ("Speed B  Z",                       Float)         = 0.05
        _NoiseMix       ("Mix AB para displacement",         Range(0,1))    = 0.40
        _DensityB       ("Density B",                        Range(0,1))    = 0.20
        _DitherB        ("Dither B",                         Range(0,1))    = 0.0

        [Header(Vertex Displacement)]
        _DisplaceStr    ("Displace Strength",                Float)         = 0.35

        [Header(Noise C  Detalle fino fragment)]
        _NoiseScaleC    ("Scale C  (world units)",           Float)         = 5.0
        _NoiseSpeedCX   ("Speed C  X",                       Float)         = 0.06
        _NoiseSpeedCZ   ("Speed C  Z",                       Float)         = 0.03
        _DensityC       ("Density C",                        Range(0,1))    = 0.15
        _DitherC        ("Dither C",                         Range(0,1))    = 0.0

        [Header(Depth Intersection)]
        _DepthFade      ("Depth Fade Distance",              Float)         = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+1"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Opacity;
                float  _AlphaContrast;
                float  _NoiseScaleA;
                float  _NoiseSpeedAX;
                float  _NoiseSpeedAZ;
                float  _DensityA;
                float  _DitherA;
                float  _NoiseScaleB;
                float  _NoiseSpeedBX;
                float  _NoiseSpeedBZ;
                float  _NoiseMix;
                float  _DensityB;
                float  _DitherB;
                float  _DisplaceStr;
                float  _NoiseScaleC;
                float  _NoiseSpeedCX;
                float  _NoiseSpeedCZ;
                float  _DensityC;
                float  _DitherC;
                float  _DepthFade;
            CBUFFER_END

            // ════════════════════════════════════════════════════════════════════
            // VALUE NOISE — interpolacion quintica, rango [0,1]
            // ════════════════════════════════════════════════════════════════════

            float Hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = Hash1(i + float2(0, 0));
                float b = Hash1(i + float2(1, 0));
                float c = Hash1(i + float2(0, 1));
                float d = Hash1(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // ════════════════════════════════════════════════════════════════════
            // BAYER 4x4 — threshold para dither por pixel
            // ════════════════════════════════════════════════════════════════════

            float BayerDither(float2 screenPos)
            {
                int2  p = int2(floor(screenPos)) & 3;
                int   i = p.y * 4 + p.x;
                const float bayer[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[i];
            }

            // ════════════════════════════════════════════════════════════════════
            // SCENE DEPTH — compatible con perspectiva y ortografica
            // ════════════════════════════════════════════════════════════════════

            float SampleLinearSceneDepth(float2 screenUV)
            {
                float raw = SampleSceneDepth(screenUV);
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            // ════════════════════════════════════════════════════════════════════
            // STRUCTS
            // ════════════════════════════════════════════════════════════════════

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXZ    : TEXCOORD0;  // XZ world space para recompute de noise en frag
                float  eyeDepth   : TEXCOORD1;  // eye depth valido en persp y ortho
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ════════════════════════════════════════════════════════════════════
            // VERTEX
            // ════════════════════════════════════════════════════════════════════

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                float2 wXZ        = positionWS.xz;

                // Noise A y B para displacement (calculados en vertex)
                float2 uvA = wXZ / max(_NoiseScaleA, 0.01)
                           + _Time.y * float2(_NoiseSpeedAX, _NoiseSpeedAZ);
                float  nA  = ValueNoise(uvA);

                float2 uvB = wXZ / max(_NoiseScaleB, 0.01)
                           + _Time.y * float2(_NoiseSpeedBX, _NoiseSpeedBZ);
                float  nB  = ValueNoise(uvB);

                // Combinar para displacement: remap [-1,1] + abs
                float combined = lerp(nA, nB, _NoiseMix);
                float shaped   = abs(combined * 2.0 - 1.0);
                positionWS    += normalWS * shaped * _DisplaceStr;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.worldXZ    = wXZ;
                // Eye depth desde view space: correcto en persp y ortho
                OUT.eyeDepth   = -TransformWorldToView(positionWS).z;
                return OUT;
            }

            // ════════════════════════════════════════════════════════════════════
            // FRAGMENT
            // ════════════════════════════════════════════════════════════════════

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Recomputar los tres noise por pixel para dither preciso.
                // (El vertex los usa solo para displacement, no los pasa.)
                float2 uvA = IN.worldXZ / max(_NoiseScaleA, 0.01)
                           + _Time.y * float2(_NoiseSpeedAX, _NoiseSpeedAZ);
                float  nA  = ValueNoise(uvA);

                float2 uvB = IN.worldXZ / max(_NoiseScaleB, 0.01)
                           + _Time.y * float2(_NoiseSpeedBX, _NoiseSpeedBZ);
                float  nB  = ValueNoise(uvB);

                float2 uvC = IN.worldXZ / max(_NoiseScaleC, 0.01)
                           + _Time.y * float2(_NoiseSpeedCX, _NoiseSpeedCZ);
                float  nC  = ValueNoise(uvC);

                // ── Shape por capa: abs remap elimina valles ──────────────────
                float shapeA = abs(nA * 2.0 - 1.0);
                float shapeB = abs(nB * 2.0 - 1.0);
                // C se usa directo como detalle fino sin abs
                float shapeC = nC;

                // ── Density bias por capa ─────────────────────────────────────
                float layerA = saturate(shapeA + _DensityA);
                float layerB = saturate(shapeB + _DensityB);
                float layerC = saturate(shapeC + _DensityC);

                // ── Dither por capa ───────────────────────────────────────────
                // lerp entre alpha suave (dither=0) y alpha binarizado (dither=1)
                // step(bayer, layer) = 1 si layer > bayer, 0 si no → pixel on/off
                float bayer = BayerDither(IN.positionCS.xy);
                layerA = lerp(layerA, step(bayer, layerA), _DitherA);
                layerB = lerp(layerB, step(bayer, layerB), _DitherB);
                layerC = lerp(layerC, step(bayer, layerC), _DitherC);

                // ── Combinar capas con screen blending ────────────────────────
                // 1 - (1-A)(1-B)(1-C): union sin sobre-saturar.
                // Cada capa solo puede llenar lo que las anteriores dejaron libre.
                float cloudAlpha = 1.0 - (1.0 - layerA)
                                       * (1.0 - layerB)
                                       * (1.0 - layerC);

                // Contrast global: <1 nube densa, >1 bordes wispies
                cloudAlpha = pow(saturate(cloudAlpha), _AlphaContrast);

                // ── Depth intersection ────────────────────────────────────────
                float2 screenUV   = IN.positionCS.xy / _ScaledScreenParams.xy;
                float  sceneDepth = SampleLinearSceneDepth(screenUV);
                float  depthFade  = saturate((sceneDepth - IN.eyeDepth) / max(_DepthFade, 0.001));

                float finalAlpha = cloudAlpha * _Opacity * depthFade;

                return half4(_Color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
