// Hielo cel-shaded animado: mismo banding NdotL que PaletteCelLit, pero en vez
// de un único _PaletteSlot fijo, mezcla 2 slots de PA_MainPalette (base/franja)
// por una franja diagonal que scrollea — así el trío luz/mid/sombra que
// alimenta el banding alterna entre los dos colores en el tiempo, dando la
// sensación de hielo/caramelo animado en vez de una superficie pintada
// estática. Franjas con borde duro (step, no smoothstep) para calzar con la
// estética pixel-art/dither del resto de shaders del proyecto. Sin
// deformación de vértices — a diferencia de PaletteCelFire, acá las 3 passes
// de sombra/depth quedan igual de simples que en PaletteCelLit base.
Shader "Rollgeon/PaletteCelIce"
{
    Properties
    {
        [Header(Ice Palette 2 Slots)]
        [PaletteSlot] _IceSlotBase   ("Base Slot",   Float) = 3
        [PaletteSlot] _IceSlotStripe ("Stripe Slot", Float) = 1

        [Header(Ice Stripes)]
        // Franjas diagonales en objeto-espacio, ángulo ajustable (0 = horizontal,
        // 90 = vertical, -45 = el diagonal clásico de antes). _StripeWidth y
        // _BaseWidth son el ancho de cada segmento EN LAS MISMAS UNIDADES DE
        // _StripeScale — anchos distintos = franjas de distinto grosor en vez de
        // un candy-stripe 50/50 monótono. El barrido se mueve en la dirección
        // del ángulo, así "hacia dónde se mueven" es el mismo control.
        _StripeAngle       ("Stripe Angle (deg)",  Float)       = -45
        _StripeScale       ("Stripe Scale",        Float)       = 4
        _StripeWidth       ("Stripe Width",        Range(0.05, 3)) = 0.5
        _BaseWidth         ("Base Width",          Range(0.05, 3)) = 0.5
        _StripeScrollSpeed ("Stripe Scroll Speed", Float)       = 0.6

        [Header(Cel Controls)]
        _MidThreshold    ("Mid Threshold",    Range(0, 1))   = 0.65
        _ShadowThreshold ("Shadow Threshold", Range(0, 1))   = 0.35
        _ShadowSmooth    ("Shadow Smooth",    Range(0, 0.3)) = 0.02
        _LightWrap       ("Light Wrap",       Range(-1, 1))  = 0.1

        [Header(Dither)]
        [Toggle] _UseDither ("Border Dither", Float) = 0
        _DitherStrength     ("Border Dither Strength", Range(0, 1)) = 0.15

        [Header(Additional Lights)]
        _LightTintStrength ("Spotlight Tint Color", Range(0,1)) = 0.4
        _SpotDither        ("Edge Dither",          Range(0,1)) = 0.0

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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _RollgeonLightData[128];

            // Luces falsas (FakeAreaLight/FakeAreaLightManager.cs) — props que iluminan sin
            // gastar el presupuesto real de Light (URP limita a 4 adicionales por objeto).
            // 3 float4 por luz: (posA.xyz, intensity), (posB.xyz, range), (color.rgb, 0).
            float  _FakeAreaLightCount;
            float4 _FakeAreaLightData[40];

            CBUFFER_START(UnityPerMaterial)
                float  _IceSlotBase;
                float  _IceSlotStripe;
                float  _StripeAngle;
                float  _StripeScale;
                float  _StripeWidth;
                float  _BaseWidth;
                float  _StripeScrollSpeed;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float3 positionOS  : TEXCOORD4;
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

                OUT.positionCS  = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = vni.normalWS;
                OUT.shadowCoord = GetShadowCoord(vpi);
                OUT.viewDirWS   = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.positionOS  = IN.positionOS.xyz;
                return OUT;
            }

            float CelLight(float3 normalWS, Light light, float wrapBias)
            {
                float NdotL = dot(normalWS, normalize(light.direction));
                float wrapped = saturate(NdotL + wrapBias);
                // Gate por luminancia real — sin luz activa, lightValue debe caer a 0 (Shadow plano).
                float luminance = dot(light.color, half3(0.2126, 0.7152, 0.0722));
                return wrapped * luminance * light.distanceAttenuation * light.shadowAttenuation;
            }

            // Punto más cercano en un segmento a-b.
            float3 ClosestOnSegment(float3 p, float3 a, float3 b)
            {
                float3 ab = b - a;
                float  t  = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-5));
                return a + ab * t;
            }

            // Luces falsas: cada una es una POLILÍNEA de hasta 4 puntos (3 tramos) — se busca
            // el punto más cercano de TODA la curva a este píxel y se lo trata como la posición
            // de una luz puntual real para el banding NdotL, con falloff por distancia. Con 1
            // punto se comporta como point light; con 2+ sigue el trazado real de la tira LED
            // en vez de cortar en línea recta entre extremos.
            float FakeAreaLightContribution(float3 positionWS, float3 normalWS, inout float3 tint)
            {
                float best = 0.0;
                int count = (int)_FakeAreaLightCount;
                [loop]
                for (int i = 0; i < count; i++)
                {
                    int b = i * 5;
                    float3 p0         = _FakeAreaLightData[b].xyz;
                    float  intensity  = _FakeAreaLightData[b].w;
                    float3 p1         = _FakeAreaLightData[b + 1].xyz;
                    float  range      = _FakeAreaLightData[b + 1].w;
                    float3 p2         = _FakeAreaLightData[b + 2].xyz;
                    int    pointCount = (int)_FakeAreaLightData[b + 2].w;
                    float3 p3         = _FakeAreaLightData[b + 3].xyz;
                    float3 color      = _FakeAreaLightData[b + 4].xyz;

                    float3 closest   = p0;
                    float  minDistSq = dot(positionWS - p0, positionWS - p0);

                    if (pointCount >= 2)
                    {
                        float3 c0 = ClosestOnSegment(positionWS, p0, p1);
                        float  d0 = dot(positionWS - c0, positionWS - c0);
                        if (d0 < minDistSq) { minDistSq = d0; closest = c0; }
                    }
                    if (pointCount >= 3)
                    {
                        float3 c1 = ClosestOnSegment(positionWS, p1, p2);
                        float  d1 = dot(positionWS - c1, positionWS - c1);
                        if (d1 < minDistSq) { minDistSq = d1; closest = c1; }
                    }
                    if (pointCount >= 4)
                    {
                        float3 c2 = ClosestOnSegment(positionWS, p2, p3);
                        float  d2 = dot(positionWS - c2, positionWS - c2);
                        if (d2 < minDistSq) { minDistSq = d2; closest = c2; }
                    }

                    float dist  = sqrt(max(minDistSq, 1e-8));
                    float atten = saturate(1.0 - dist / max(range, 1e-4));
                    atten *= atten;

                    float ndotl = saturate(dot(normalWS, (closest - positionWS) / dist));
                    float val   = atten * ndotl * intensity;

                    best  = max(best, val);
                    tint += color * val;
                }
                return best;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                clip(_AlphaCutoff - (BayerDither(IN.positionCS.xy / _DitherScale) + 1.0/16.0));

                float3 normalWS = normalize(IN.normalWS);

                Light mainLight  = GetMainLight(IN.shadowCoord);
                float lightValue = CelLight(normalWS, mainLight, _LightWrap);

                float3 addTint = float3(0, 0, 0);
                #if defined(_CLUSTER_LIGHT_LOOP) || defined(_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS              = IN.positionWS;
                    inputData.normalWS                = normalWS;
                    inputData.viewDirectionWS         = normalize(GetWorldSpaceViewDir(IN.positionWS));
                    inputData.shadowCoord             = IN.shadowCoord;
                    inputData.shadowMask              = unity_ProbesOcclusion;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    float2 normalizedScreenSpaceUV = inputData.normalizedScreenSpaceUV;
                    float3 positionWS = IN.positionWS;
                    float spotBayer = BayerDither(IN.positionCS.xy);
                    LIGHT_LOOP_BEGIN(GetAdditionalLightsCount())
                        Light addLt  = GetAdditionalLight(lightIndex, positionWS, inputData.shadowMask);
                        float addVal = addLt.distanceAttenuation;
                        float4 ld       = _RollgeonLightData[lightIndex];
                        float ldSteps   = max(floor(ld.z), 1.0);
                        float shaped    = pow(saturate(addVal * ld.x), lerp(1.0, 8.0, ld.y));
                        float quantStep = (ldSteps > 1.5) ? (1.0 / (ldSteps - 1.0)) : 1.0;
                        float preVal    = saturate(shaped + (spotBayer - 0.5) * _SpotDither * quantStep);
                        float spotVal   = (ldSteps > 1.5) ? (floor(preVal * ldSteps) / (ldSteps - 1.0)) : preVal;
                        // Gate por luminancia real — ver PaletteCelLit.shader.
                        float addLuminance = dot(addLt.color, half3(0.2126, 0.7152, 0.0722));
                        spotVal        *= addLt.shadowAttenuation * addLuminance;
                        lightValue      = max(lightValue, spotVal);
                        addTint        += addLt.color * spotVal;
                    LIGHT_LOOP_END
                }
                #endif

                // Luces falsas (ver FakeAreaLightManager) — se combinan igual que una luz
                // adicional real, sin pasar por el presupuesto de Light de URP.
                float fakeVal = FakeAreaLightContribution(IN.positionWS, normalWS, addTint);
                lightValue = max(lightValue, fakeVal);

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

                // ── Franjas diagonales animadas: elige entre los 2 slots ANTES de
                // aplicar el banding cel de arriba. Borde duro (step) — nada de
                // antialiasing suave, para que se vea tipo caramelo/pixel-art.
                // El ángulo rota el eje de la franja Y la dirección en la que se
                // mueve (son el mismo vector) — _StripeWidth/_BaseWidth en las
                // mismas unidades que _StripeScale, así se pueden hacer franjas
                // de distinto grosor en vez de un candy-stripe 50/50 repetitivo.
                // XZ (no XY): el primitive Plane de Unity vive en el plano XZ (Y
                // constante en toda la superficie) — con XY la rotación no tenía
                // sobre qué rotar y el patrón colapsaba a 1D.
                float stripeRad = radians(_StripeAngle);
                float2 stripeDir = float2(cos(stripeRad), sin(stripeRad));
                float diag = dot(IN.positionOS.xz, stripeDir) * _StripeScale
                             + _Time.y * _StripeScrollSpeed;

                float period = max(_StripeWidth + _BaseWidth, 0.001);
                float posInPeriod = frac(diag / period) * period;
                float stripeMask = step(posInPeriod, _StripeWidth);

                int slotBase   = int(_IceSlotBase);
                int slotStripe = int(_IceSlotStripe);

                float3 lightCol = lerp(
                    _PaletteLightColors[slotBase].rgb, _PaletteLightColors[slotStripe].rgb, stripeMask);
                float3 midCol = lerp(
                    _PaletteMidColors[slotBase].rgb, _PaletteMidColors[slotStripe].rgb, stripeMask);
                float3 shadowCol = lerp(
                    _PaletteShadowColors[slotBase].rgb, _PaletteShadowColors[slotStripe].rgb, stripeMask);

                float3 color = lerp(shadowCol, midCol, celShadow);
                color        = lerp(color,     lightCol, celLight);

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
                float  _IceSlotBase;
                float  _IceSlotStripe;
                float  _StripeAngle;
                float  _StripeScale;
                float  _StripeWidth;
                float  _BaseWidth;
                float  _StripeScrollSpeed;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

            float4 _PaletteLightColors[32];
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
                float  _IceSlotBase;
                float  _IceSlotStripe;
                float  _StripeAngle;
                float  _StripeScale;
                float  _StripeWidth;
                float  _BaseWidth;
                float  _StripeScrollSpeed;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

            float4 _PaletteLightColors[32];
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
                float  _IceSlotBase;
                float  _IceSlotStripe;
                float  _StripeAngle;
                float  _StripeScale;
                float  _StripeWidth;
                float  _BaseWidth;
                float  _StripeScrollSpeed;
                float  _MidThreshold;
                float  _ShadowThreshold;
                float  _ShadowSmooth;
                float  _LightWrap;
                float  _UseDither;
                float  _DitherStrength;
                float  _LightTintStrength;
                float  _SpotDither;
                float  _AlphaCutoff;
                float  _DitherScale;
            CBUFFER_END

            float4 _PaletteLightColors[32];
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
