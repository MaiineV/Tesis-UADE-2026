// Shader de superficie para pisos y paredes sin patrón geométrico.
// Genera variedad de color mediante 3 capas de noise independientes
// que oscurecen el color base multiplicativamente — donde las manchas
// se superponen el resultado es más oscuro de forma natural.
//
// World-space coordinates: seamless entre tiles adyacentes.
// Overlay de grietas Voronoi (igual que PaletteCelLitPatternCrack).
//
// Feature parity con PaletteCelLit:
//   ✅ 3 bandas cel (shadow/mid/light) — multiplicativas
//   ✅ Mid threshold separado
//   ✅ Border Dither / Shadow Dither
//   ✅ Crease (NdotV)

Shader "Rollgeon/PaletteCelFloor"
{
    Properties
    {
        [Header(Surface)]
        _BaseColor       ("Base Color", Color) = (0.30, 0.55, 0.20, 1)

        [Header(Patches)]
        _PatchScale      ("Patch Scale      (world units)",        Float)        = 1.5
        _PatchThreshold  ("Patch Coverage   (0=nada  1=todo)",     Range(0,1))   = 0.50
        _PatchSoftness   ("Patch Softness   (bordes de mancha)",   Range(0,0.5)) = 0.15
        _PatchDarken     ("Patch Darken     (oscurece por capa)",  Range(0,0.8)) = 0.25
        _PatchAnisotropy ("Patch Anisotropy (1=redondo >1=vetas)", Range(0.1,5)) = 1.0
        _PatchSeed       ("Patch Seed",                            Float)        = 0.0

        [Header(Cel Controls)]
        _ShadowThreshold ("Shadow Threshold", Range(0,1))    = 0.35
        _MidThreshold    ("Mid Threshold",    Range(0,1))    = 0.65
        _ShadowSmooth    ("Shadow Smooth",    Range(0,0.3))  = 0.02
        _LightWrap       ("Light Wrap",       Range(-1,1))   = 0.1
        _ShadowDarken    ("Shadow Darken",    Range(0,1))    = 0.45
        _LightBrighten   ("Light Brighten",   Range(0,1))    = 0.15

        [Header(Dither)]
        [Toggle] _UseDither       ("Border Dither",          Float)      = 0
        _DitherStrength           ("Border Dither Strength", Range(0,1)) = 0.15
        [Toggle] _UseShadowDither ("Shadow Dither",          Float)      = 0
        _ShadowDitherDensity      ("Shadow Dither Density",  Range(0,1)) = 0.3

        [Header(Additional Lights)]
        _LightTintStrength        ("Spotlight Tint Color",                Range(0,1)) = 0.4
        _SpotDither               ("Edge Dither",                         Range(0,1)) = 0.0

        [Header(Crease)]
        [Toggle] _EnableCrease ("Enable Crease",    Float)        = 0
        _CreaseDarken          ("Crease Darken",    Range(0,1))   = 0.4
        _CreaseThreshold       ("Crease Threshold", Range(0,1))   = 0.35
        _CreaseSmooth          ("Crease Smooth",    Range(0,0.3)) = 0.05
        _CreaseAlpha           ("Crease Alpha",     Range(0,1))   = 0.8
        [Toggle] _CreaseDither ("Crease Dither",    Float)        = 0

        [Header(Cracks)]
        [Toggle] _EnableCracks ("Enable Cracks",                    Float)        = 0
        _CrackScale            ("Crack Scale      (tamano celda)",  Float)        = 3.0
        _CrackWidth            ("Crack Width      (grosor grieta)", Range(0,0.3)) = 0.08
        _CrackDarken           ("Crack Darken",                     Range(0,1))   = 0.55
        _CrackDensity          ("Crack Density    (0=todo 1=nada)", Range(0,1))   = 0.45
        _CrackDensityScale     ("Crack Density Scale",              Float)        = 0.7
        _CrackSeed             ("Crack Seed",                       Float)        = 0.0
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
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Per-light quantization data uploaded by LightDataRendererFeature every frame.
            // x = preQuantizeIntensity, y = falloff, z = falloffSteps, w = unused
            float4 _RollgeonLightData[128];

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _PatchScale;
                float  _PatchThreshold;
                float  _PatchSoftness;
                float  _PatchDarken;
                float  _PatchAnisotropy;
                float  _PatchSeed;
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
                float  _SpotDither;
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
                float3 viewDirWS   : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ════════════════════════════════════════════════════════════════════
            // BAYER 4×4
            // ════════════════════════════════════════════════════════════════════

            float BayerDither(float2 screenPos)
            {
                int2 px  = int2(floor(screenPos)) & 3;
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
            // VALUE NOISE — usado para manchas y densidad de grietas
            // ════════════════════════════════════════════════════════════════════

            float HashS(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(HashS(i + float2(0,0)), HashS(i + float2(1,0)), u.x),
                    lerp(HashS(i + float2(0,1)), HashS(i + float2(1,1)), u.x),
                    u.y);
            }

            // ════════════════════════════════════════════════════════════════════
            // VORONOI F1/F2 — grietas
            // ════════════════════════════════════════════════════════════════════

            float2 HashV2(float2 p)
            {
                float2 q = float2(dot(p, float2(127.1, 311.7)),
                                  dot(p, float2(269.5, 183.3)));
                return frac(sin(q) * 43758.5453);
            }

            void VoronoiF1F2(float2 p, out float F1, out float F2)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                F1 = 8.0; F2 = 8.0;
                for (int j = -1; j <= 1; j++)
                for (int i2 = -1; i2 <= 1; i2++)
                {
                    float2 cell = float2(float(i2), float(j));
                    float2 pt   = HashV2(ip + cell);
                    float2 r    = cell + pt - fp;
                    float  d    = dot(r, r);
                    if      (d < F1) { F2 = F1; F1 = d; }
                    else if (d < F2) { F2 = d; }
                }
                F1 = sqrt(F1);
                F2 = sqrt(F2);
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
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                float3 normalWS = normalize(IN.normalWS);

                float2 wUV = WorldUV(IN.positionWS, normalWS);

                // ── Manchas: 3 capas independientes acumuladas ───────────────────
                // Anisotropía: estira el UV en X antes del noise
                float2 uvA = float2(wUV.x * _PatchAnisotropy, wUV.y) * _PatchScale;

                // Cada capa usa un offset de seed diferente → manchas independientes
                float n1 = ValueNoise(uvA + _PatchSeed * 7.13);
                float n2 = ValueNoise(uvA + _PatchSeed * 7.13 + float2(17.31, 43.71));
                float n3 = ValueNoise(uvA + _PatchSeed * 7.13 + float2(31.17, 71.39));

                // Soft threshold: qué tan "activa" está cada mancha en este pixel
                float soft = max(_PatchSoftness, 0.001);
                float p1 = smoothstep(_PatchThreshold + soft, _PatchThreshold - soft, n1);
                float p2 = smoothstep(_PatchThreshold + soft, _PatchThreshold - soft, n2);
                float p3 = smoothstep(_PatchThreshold + soft, _PatchThreshold - soft, n3);

                // Acumulación multiplicativa: cada capa oscurece el resultado anterior
                // → donde se superponen, el color es más oscuro de forma natural
                float3 surfaceColor = _BaseColor.rgb;
                surfaceColor *= 1.0 - p1 * _PatchDarken;
                surfaceColor *= 1.0 - p2 * _PatchDarken;
                surfaceColor *= 1.0 - p3 * _PatchDarken;

                // ── Grietas Voronoi (world-space) ────────────────────────────────
                if (_EnableCracks > 0.5)
                {
                    float F1, F2;
                    VoronoiF1F2(wUV * _CrackScale + _CrackSeed * 7.13, F1, F2);

                    float crackVal  = F2 - F1;
                    float crackMask = smoothstep(_CrackWidth, 0.0, crackVal);

                    float densNoise = ValueNoise(wUV * _CrackDensityScale + _CrackSeed * 3.71);
                    float densMask  = smoothstep(_CrackDensity,
                                                 _CrackDensity + 0.15,
                                                 densNoise);

                    surfaceColor = lerp(surfaceColor,
                                        surfaceColor * (1.0 - _CrackDarken),
                                        crackMask * densMask);
                }

                // ── Luz ──────────────────────────────────────────────────────────
                Light mainLight  = GetMainLight(IN.shadowCoord);
                float lightValue = CelLightVal(normalWS, mainLight, _LightWrap);

                float3 addTint = float3(0, 0, 0);
                #if defined(_FORWARD_PLUS) || defined(_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS              = IN.positionWS;
                    inputData.normalWS                = normalWS;
                    inputData.viewDirectionWS         = normalize(GetWorldSpaceViewDir(IN.positionWS));
                    inputData.shadowCoord             = IN.shadowCoord;
                    inputData.shadowMask              = unity_ProbesOcclusion;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    // Required local variable names for LIGHT_LOOP_BEGIN in Forward+
                    float2 normalizedScreenSpaceUV = inputData.normalizedScreenSpaceUV;
                    float3 positionWS = IN.positionWS;
                    float spotBayer = BayerDither(IN.positionCS.xy);
                    LIGHT_LOOP_BEGIN(GetAdditionalLightsCount())
                        Light addLt  = GetAdditionalLight(lightIndex, positionWS, inputData.shadowMask);
                        // Distance only for range — shadow multiplied in AFTER quantization
                        float addVal = addLt.distanceAttenuation;
                        float4 ld       = _RollgeonLightData[lightIndex];
                        float ldSteps   = max(floor(ld.z), 1.0);
                        float shaped    = pow(saturate(addVal * ld.x), lerp(1.0, 8.0, ld.y));
                        float quantStep = (ldSteps > 1.5) ? (1.0 / (ldSteps - 1.0)) : 1.0;
                        float preVal    = saturate(shaped + (spotBayer - 0.5) * _SpotDither * quantStep);
                        float spotVal   = (ldSteps > 1.5) ? (floor(preVal * ldSteps) / (ldSteps - 1.0)) : preVal;
                        // Shadow attenuation applied after quantization (0=shadowed, 1=lit)
                        spotVal        *= addLt.shadowAttenuation;
                        lightValue      = max(lightValue, spotVal);
                        addTint        += addLt.color * spotVal;
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

                float3 shadowColor = surfaceColor * (1.0 - _ShadowDarken);
                float3 lightColor  = min(surfaceColor * (1.0 + _LightBrighten), 1.0);

                float3 color = lerp(shadowColor, surfaceColor, celShadow);
                color        = lerp(color,       lightColor,   celLight);

                // ── Shadow Dither ────────────────────────────────────────────────
                if (_UseShadowDither > 0.5)
                {
                    float bayer    = BayerDither(IN.positionCS.xy);
                    float inShadow = 1.0 - celShadow;
                    float d        = step(bayer, _ShadowDitherDensity) * inShadow;
                    color          = lerp(color, surfaceColor, d);
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

                    float3 creaseColor = surfaceColor * (1.0 - _CreaseDarken);
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
                float4 _BaseColor;
                float _PatchScale; float _PatchThreshold; float _PatchSoftness;
                float _PatchDarken; float _PatchAnisotropy; float _PatchSeed;
                float _ShadowThreshold; float _MidThreshold; float _ShadowSmooth;
                float _LightWrap; float _ShadowDarken; float _LightBrighten;
                float _UseDither; float _DitherStrength;
                float _UseShadowDither; float _ShadowDitherDensity;
                float _EnableCrease; float _CreaseDarken;
                float _CreaseThreshold; float _CreaseSmooth; float _CreaseAlpha; float _CreaseDither;
                float _EnableCracks; float _CrackScale; float _CrackWidth;
                float _CrackDarken; float _CrackDensity; float _CrackDensityScale; float _CrackSeed;
                float _LightTintStrength; float _SpotDither;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

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
            half4 ShadowFrag(SCVary IN) : SV_Target { return 0; }
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
                float4 _BaseColor;
                float _PatchScale; float _PatchThreshold; float _PatchSoftness;
                float _PatchDarken; float _PatchAnisotropy; float _PatchSeed;
                float _ShadowThreshold; float _MidThreshold; float _ShadowSmooth;
                float _LightWrap; float _ShadowDarken; float _LightBrighten;
                float _UseDither; float _DitherStrength;
                float _UseShadowDither; float _ShadowDitherDensity;
                float _EnableCrease; float _CreaseDarken;
                float _CreaseThreshold; float _CreaseSmooth; float _CreaseAlpha; float _CreaseDither;
                float _EnableCracks; float _CrackScale; float _CrackWidth;
                float _CrackDarken; float _CrackDensity; float _CrackDensityScale; float _CrackSeed;
                float _LightTintStrength; float _SpotDither;
            CBUFFER_END

            struct DOAttr { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 posCS : SV_POSITION; };
            DOVary DepthVert(DOAttr IN) { UNITY_SETUP_INSTANCE_ID(IN); DOVary OUT; OUT.posCS = TransformObjectToHClip(IN.posOS.xyz); return OUT; }
            half4  DepthFrag(DOVary IN) : SV_Target { return 0; }
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
                float4 _BaseColor;
                float _PatchScale; float _PatchThreshold; float _PatchSoftness;
                float _PatchDarken; float _PatchAnisotropy; float _PatchSeed;
                float _ShadowThreshold; float _MidThreshold; float _ShadowSmooth;
                float _LightWrap; float _ShadowDarken; float _LightBrighten;
                float _UseDither; float _DitherStrength;
                float _UseShadowDither; float _ShadowDitherDensity;
                float _EnableCrease; float _CreaseDarken;
                float _CreaseThreshold; float _CreaseSmooth; float _CreaseAlpha; float _CreaseDither;
                float _EnableCracks; float _CrackScale; float _CrackWidth;
                float _CrackDarken; float _CrackDensity; float _CrackDensityScale; float _CrackSeed;
                float _LightTintStrength; float _SpotDither;
            CBUFFER_END

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
                float3 normalWS = normalize(IN.normalWS);
                float2 encoded  = PackNormalOctRectEncode(TransformWorldToViewDir(normalWS, true));
                return float4(encoded, 0, 0);
            }
            ENDHLSL
        }
    }
}
