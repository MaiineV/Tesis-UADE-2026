Shader "Custom/GodotParity/CelLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Cel Ramp)]
        _CelRamp("Cel Ramp", 2D) = "white" {}
        _CelSpecularRamp("Cel Specular Ramp", 2D) = "white" {}

        [Header(Cel Controls)]
        _LightWrap("Light Wrap", Range(-2, 2)) = 0.3
        _Steepness("Steepness", Range(1, 8)) = 1.0
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 1.0
        _PointLightAttenuationCurve("Point Light Attenuation Curve", Range(0.1, 4)) = 1.0

        [Header(Specular)]
        _SpecularShininess("Specular Shininess", Range(1, 256)) = 32
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.0

        [Header(Dither)]
        [Toggle] _UseDither("Use Dither", Float) = 0
        _DitherStrength("Dither Strength", Range(0, 1)) = 0.1
        [Toggle] _DitherDirectional("Dither Directional", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 viewDirWS  : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                float4 shadowCoord: TEXCOORD5; // required for cast shadows from directional light
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CelRamp);
            SAMPLER(sampler_CelRamp);
            TEXTURE2D(_CelSpecularRamp);
            SAMPLER(sampler_CelSpecularRamp);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _LightWrap;
                float _Steepness;
                float _ShadowStrength;
                float _PointLightAttenuationCurve;
                float _SpecularShininess;
                float _SpecularStrength;
                float _UseDither;
                float _DitherStrength;
                float _DitherDirectional;
            CBUFFER_END

            float ToonBayer(float2 coords)
            {
                int2 p = int2(floor(coords)) & 3;
                int idx = p.y * 4 + p.x;

                if (idx == 0) return 0.0 / 16.0;
                if (idx == 1) return 8.0 / 16.0;
                if (idx == 2) return 2.0 / 16.0;
                if (idx == 3) return 10.0 / 16.0;
                if (idx == 4) return 12.0 / 16.0;
                if (idx == 5) return 4.0 / 16.0;
                if (idx == 6) return 14.0 / 16.0;
                if (idx == 7) return 6.0 / 16.0;
                if (idx == 8) return 3.0 / 16.0;
                if (idx == 9) return 11.0 / 16.0;
                if (idx == 10) return 1.0 / 16.0;
                if (idx == 11) return 9.0 / 16.0;
                if (idx == 12) return 15.0 / 16.0;
                if (idx == 13) return 7.0 / 16.0;
                if (idx == 14) return 13.0 / 16.0;
                return 5.0 / 16.0;
            }

            float2 ToonWorldPixel(float3 worldPos, float3 camRight, float3 camUp)
            {
                float3 dx = ddx(worldPos);
                float3 dy = ddy(worldPos);

                float sx = dot(dx, camRight);
                float sy = dot(dy, camUp);

                return float2(
                    floor(dot(worldPos, camRight) / max(abs(sx), 1e-6)),
                    floor(dot(worldPos, camUp) / max(abs(sy), 1e-6))
                );
            }

            float3 RampFloor()
            {
                float u = lerp(0.5, 0.0, saturate(_ShadowStrength));
                return SAMPLE_TEXTURE2D(_CelRamp, sampler_CelRamp, float2(u, 0.5)).rgb;
            }

            void AccumulateCelLight(
                float3 normalWS,
                float3 lightDirWS,
                float3 viewDirWS,
                float3 lightColor,
                float attenuation,
                bool lightIsDirectional,
                float3 worldPos,
                float3 camRight,
                float3 camUp,
                inout float3 diffuseLight,
                inout float3 specularLight,
                out float litAmount)
            {
                bool doDither = (_UseDither > 0.5) && ((_DitherDirectional > 0.5) || !lightIsDirectional);

                float att = lightIsDirectional
                    ? attenuation
                    : pow(saturate(attenuation), max(_PointLightAttenuationCurve, 1e-4));

                float luma = dot(lightColor, float3(0.2126, 0.7152, 0.0722));
                float lumaSafe = max(luma, 0.001);
                float3 chromaticity = clamp(lightColor / lumaSafe, 0.0, 3.0);

                float ndotl = dot(normalWS, lightDirWS);
                float diffuseWrapped = saturate(ndotl + _LightWrap);
                float diffuseRaw = saturate(diffuseWrapped + (att - 1.0));
                float bandEval = saturate(diffuseRaw * max(_Steepness, 1e-4));

                if (doDither)
                {
                    float bayer = ToonBayer(ToonWorldPixel(worldPos, camRight, camUp));
                    bandEval = saturate(bandEval + (bayer - 0.5) * _DitherStrength * 0.25);
                }

                float3 rampColor = SAMPLE_TEXTURE2D(_CelRamp, sampler_CelRamp, float2(bandEval, 0.5)).rgb;
                float3 rampFloor = RampFloor();

                diffuseLight += (rampColor - rampFloor)
                    * sqrt(max(luma * 0.32, 0.0))
                    * chromaticity;

                float3 halfDir = normalize(lightDirWS + viewDirWS);
                float ndoth = saturate(dot(normalWS, halfDir));
                float specUv = pow(ndoth, max(_SpecularShininess, 1.0));
                float3 specColor = SAMPLE_TEXTURE2D(_CelSpecularRamp, sampler_CelSpecularRamp, float2(specUv, 0.5)).rgb;

                float specShadowMask = smoothstep(0.0, 1.0, bandEval * 4.0);
                specularLight += specColor * specShadowMask * _SpecularStrength * chromaticity;

                litAmount = saturate(bandEval * sqrt(max(luma * 0.32, 0.0)));
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = normalize(nrm.normalWS);
                OUT.viewDirWS   = SafeNormalize(GetWorldSpaceViewDir(pos.positionWS));
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(pos.positionCS.z);
                OUT.shadowCoord = GetShadowCoord(pos); // vertex shadow coord for all shadow modes
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);
                float3 camRight = normalize(UNITY_MATRIX_I_V[0].xyz);
                float3 camUp = normalize(UNITY_MATRIX_I_V[1].xyz);

                float3 diffuseLight = 0.0;
                float3 specularLight = 0.0;
                float litAmount;

                // Pass shadow coord so cast shadows from the directional light are evaluated
                Light mainLight = GetMainLight(IN.shadowCoord);
                float mainAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                AccumulateCelLight(
                    normalWS,
                    normalize(mainLight.direction),
                    viewDirWS,
                    mainLight.color,
                    mainAttenuation,
                    true,
                    IN.positionWS,
                    camRight,
                    camUp,
                    diffuseLight,
                    specularLight,
                    litAmount
                );

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0; li < lightCount; li++)
                {
                    Light addLight = GetAdditionalLight(li, IN.positionWS);
                    float addAttenuation = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    AccumulateCelLight(
                        normalWS,
                        normalize(addLight.direction),
                        viewDirWS,
                        addLight.color,
                        addAttenuation,
                        false,
                        IN.positionWS,
                        camRight,
                        camUp,
                        diffuseLight,
                        specularLight,
                        litAmount
                    );
                }
                #endif

                float3 rampFloor = RampFloor();
                float3 color = baseTex.rgb * (rampFloor + diffuseLight) + specularLight;
                color = MixFog(color, IN.fogFactor);
                return half4(color, baseTex.a);
            }
            ENDHLSL
        }

        // ── DepthOnly pass ───────────────────────────────────────────────────────
        // Required so URP's depth prepass (ForwardPlus / ForcePrepass) can render
        // these objects correctly and populate _CameraDepthTexture.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ColorMask 0
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Must match the UnityPerMaterial CBUFFER in the Forward pass
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _LightWrap;
                float _Steepness;
                float _ShadowStrength;
                float _PointLightAttenuationCurve;
                float _SpecularShininess;
                float _SpecularStrength;
                float _UseDither;
                float _DitherStrength;
                float _DitherDirectional;
            CBUFFER_END

            struct DOAttribs  { float4 posOS : POSITION; };
            struct DOVaryings { float4 posCS : SV_POSITION; };

            DOVaryings DepthOnlyVert(DOAttribs IN)
            {
                DOVaryings OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                return OUT;
            }

            half4 DepthOnlyFrag(DOVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ── DepthNormals pass ─────────────────────────────────────────────────
        // Required so URP's depth-normals prepass populates _CameraNormalsTexture,
        // enabling normal-based crease detection in GodotParityPost.shader.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Packing.hlsl is already included transitively via Core.hlsl → Common.hlsl

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _LightWrap;
                float _Steepness;
                float _ShadowStrength;
                float _PointLightAttenuationCurve;
                float _SpecularShininess;
                float _SpecularStrength;
                float _UseDither;
                float _DitherStrength;
                float _DitherDirectional;
            CBUFFER_END

            struct DNAttribs  { float4 posOS : POSITION; float3 normalOS : NORMAL; };
            struct DNVaryings { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVaryings DepthNormalsVert(DNAttribs IN)
            {
                DNVaryings OUT;
                OUT.posCS    = TransformObjectToHClip(IN.posOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 DepthNormalsFrag(DNVaryings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                // URP DeclareNormalsTexture uses octahedral encoding in view space
                float3 normalVS = TransformWorldToViewDir(normalWS, true);
                float2 encoded  = PackNormalOctRectEncode(normalVS);
                return float4(encoded, 0, 0);
            }
            ENDHLSL
        }
    }
}
