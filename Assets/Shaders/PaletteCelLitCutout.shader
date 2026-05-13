// Variante cutout de PaletteCelLit.
// Acepta una textura en blanco/negro: los píxeles blancos se dibujan con cel shading,
// los negros se descartan (clip). Ideal para detalles con forma: corazones, logos,
// bordados, tatuajes, etc. aplicados sobre un quad o parte de una mesh.
//
// Uso:
//   - Asignar a un quad plano o a un submesh de detalle
//   - _MaskTex: textura B/N donde blanco = visible, negro = transparente
//   - _Cutoff:  umbral de recorte (default 0.5)
//   - El resto de propiedades idénticas a PaletteCelLit

Shader "Rollgeon/PaletteCelLitCutout"
{
    Properties
    {
        [Header(Mask Texture)]
        _MaskTex  ("Mask Texture (B/N)", 2D) = "white" {}
        _Cutoff   ("Alpha Cutoff",  Range(0, 1)) = 0.5
        _Rotation ("Rotation (degrees)", Range(0, 360)) = 0

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
        [Toggle] _UseDither    ("Use Dither",    Float) = 0
        _DitherStrength        ("Dither Strength", Range(0, 1)) = 0.15

        [Header(Crease)]
        [Toggle] _EnableCrease  ("Enable Crease",  Float) = 0
        _CreaseColor            ("Crease Color",   Color) = (0.15, 0.15, 0.2, 1)
        _CreaseThreshold        ("Crease Threshold", Range(0, 1)) = 0.35
        _CreaseSmooth           ("Crease Smooth",    Range(0, 0.3)) = 0.05
        _CreaseAlpha            ("Crease Alpha",     Range(0, 1))   = 0.8
        [Toggle] _CreaseDither  ("Crease Dither",    Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float  _Cutoff;
                float  _Rotation;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
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
                float2 uv          : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Rotación de UV alrededor del centro (0.5, 0.5) ──────────────────
            float2 RotateUV(float2 uv, float degrees)
            {
                float rad = degrees * (3.14159265 / 180.0);
                float s   = sin(rad);
                float c   = cos(rad);
                uv       -= 0.5;
                uv        = float2(uv.x * c - uv.y * s,
                                   uv.x * s + uv.y * c);
                uv       += 0.5;
                return uv;
            }

            // ── Bayer 4×4 ────────────────────────────────────────────────────────
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

                OUT.positionCS  = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = vni.normalWS;
                OUT.shadowCoord = GetShadowCoord(vpi);
                OUT.viewDirWS   = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.uv          = RotateUV(TRANSFORM_TEX(IN.uv, _MaskTex), _Rotation);
                return OUT;
            }

            float CelLight(float3 normalWS, Light light, float wrapBias)
            {
                float NdotL  = dot(normalWS, normalize(light.direction));
                float wrapped = saturate(NdotL + wrapBias);
                return wrapped * light.distanceAttenuation * light.shadowAttenuation;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ── Máscara: descarta píxeles negros ─────────────────────────────
                // dot(..., 0.333) = luminancia promedio del texel
                float mask = dot(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).rgb,
                                 float3(0.333, 0.333, 0.333));
                clip(mask - _Cutoff);

                // ── Cel shading (idéntico a PaletteCelLit) ───────────────────────
                float3 normalWS = normalize(IN.normalWS);

                Light mainLight  = GetMainLight(IN.shadowCoord);
                float lightValue = CelLight(normalWS, mainLight, _LightWrap);

                #if defined(_ADDITIONAL_LIGHTS)
                uint addCount = GetAdditionalLightsCount();
                for (uint li = 0; li < addCount; li++)
                {
                    Light addLight = GetAdditionalLight(li, IN.positionWS);
                    lightValue = max(lightValue, CelLight(normalWS, addLight, _LightWrap));
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

                float3 color = lerp(_ShadowColor.rgb, _MidColor.rgb, celShadow);
                color        = lerp(color,            _LightColor.rgb, celLight);

                if (_EnableCrease > 0.5)
                {
                    float3 viewDirWS = normalize(IN.viewDirWS);
                    float NdotV = abs(dot(normalWS, viewDirWS));
                    float creaseDitherOff = 0.0;
                    if (_CreaseDither > 0.5)
                        creaseDitherOff = (BayerDither(IN.positionCS.xy) - 0.5) * _DitherStrength;
                    float creaseVal = smoothstep(
                        _CreaseThreshold + _CreaseSmooth + creaseDitherOff,
                        _CreaseThreshold - _CreaseSmooth + creaseDitherOff,
                        NdotV);
                    color = lerp(color, _CreaseColor.rgb, creaseVal * _CreaseAlpha);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster ────────────────────────────────────────────────────────
        // También hace clip con la máscara → la sombra proyectada respeta la forma.
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

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float  _Cutoff;
                float  _Rotation;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct SCAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SCVary { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float2 RotateUV(float2 uv, float degrees)
            {
                float rad = degrees * (3.14159265 / 180.0);
                float s = sin(rad); float c = cos(rad);
                uv -= 0.5;
                uv  = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
                return uv + 0.5;
            }

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
                OUT.uv    = RotateUV(TRANSFORM_TEX(IN.uv, _MaskTex), _Rotation);
                return OUT;
            }

            half4 ShadowFrag(SCVary IN) : SV_Target
            {
                float mask = dot(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).rgb,
                                 float3(0.333, 0.333, 0.333));
                clip(mask - _Cutoff);
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

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float  _Cutoff;
                float  _Rotation;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
            CBUFFER_END

            struct DOAttr { float4 posOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float2 RotateUV(float2 uv, float degrees)
            {
                float rad = degrees * (3.14159265 / 180.0);
                float s = sin(rad); float c = cos(rad);
                uv -= 0.5;
                uv  = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
                return uv + 0.5;
            }

            DOVary DepthVert(DOAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DOVary OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                OUT.uv    = RotateUV(TRANSFORM_TEX(IN.uv, _MaskTex), _Rotation);
                return OUT;
            }

            half4 DepthFrag(DOVary IN) : SV_Target
            {
                float mask = dot(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).rgb,
                                 float3(0.333, 0.333, 0.333));
                clip(mask - _Cutoff);
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

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float  _Cutoff;
                float  _Rotation;
                float4 _LightColor;
                float4 _MidColor;
                float4 _ShadowColor;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _EnableCrease;
                float4 _CreaseColor;
                float  _CreaseThreshold;
                float  _CreaseSmooth;
                float  _CreaseAlpha;
                float  _CreaseDither;
            CBUFFER_END

            struct DNAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVary { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            float2 RotateUV(float2 uv, float degrees)
            {
                float rad = degrees * (3.14159265 / 180.0);
                float s = sin(rad); float c = cos(rad);
                uv -= 0.5;
                uv  = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
                return uv + 0.5;
            }

            DNVary DNVert(DNAttr IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DNVary OUT;
                OUT.posCS    = TransformObjectToHClip(IN.posOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv       = RotateUV(TRANSFORM_TEX(IN.uv, _MaskTex), _Rotation);
                return OUT;
            }

            float4 DNFrag(DNVary IN) : SV_Target
            {
                float mask = dot(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).rgb,
                                 float3(0.333, 0.333, 0.333));
                clip(mask - _Cutoff);
                float3 normalWS = normalize(IN.normalWS);
                float2 encoded  = PackNormalOctRectEncode(TransformWorldToViewDir(normalWS, true));
                return float4(encoded, 0, 0);
            }
            ENDHLSL
        }
    }
}
