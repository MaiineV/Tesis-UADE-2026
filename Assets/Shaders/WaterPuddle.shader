// Charco de agua unlit-ish (con specular real) para el tile "electrified
// puddle". Sin normal map (no existe ese asset en el proyecto): la
// ondulación se arma con dos ValueNoise (calcada de FogPlane.shader)
// scrolleando en direcciones opuestas — se suman para un patrón caótico
// no lineal, y su derivada finita hace de normal falsa para alimentar un
// specular Blinn-Phong real (el "brillo húmedo" que pedía la referencia).
// Único shader transparente 3D nuevo esta sesión — reusa el patrón de
// FogPlane.shader: 1 sola pass, sin ShadowCaster/DepthOnly/DepthNormals.
Shader "Rollgeon/WaterPuddle"
{
    Properties
    {
        [Header(Water Palette 2 Slots)]
        [PaletteSlot] _WaterSlotDeep     ("Deep Water Slot",    Float) = 0
        [PaletteSlot] _WaterSlotElectric ("Electric Tint Slot", Float) = 3

        [Header(Water Alpha)]
        _WaterAlpha ("Water Alpha", Range(0, 1)) = 0.6

        [Header(Ripple Distortion sin normal map)]
        _RippleScale    ("Ripple Scale",             Float)      = 6
        _RippleSpeedA   ("Ripple Speed A",           Vector)     = (0.3, 0.2, 0, 0)
        _RippleSpeedB   ("Ripple Speed B (opuesta)", Vector)     = (-0.25, 0.15, 0, 0)
        _NormalStrength ("Fake-Normal Strength",     Range(0,1)) = 0.4

        [Header(Specular Phong Wet Glint)]
        _SpecularPower    ("Specular Power",    Range(1, 128)) = 32
        _SpecularStrength ("Specular Strength", Range(0, 4))   = 1.5

        [Header(Electric Shimmer)]
        _ShimmerSpeed    ("Shimmer Speed",    Float)       = 8
        _ShimmerStrength ("Shimmer Strength", Range(0, 2)) = 0.6

        [Header(Fresnel Rim)]
        _FresnelPower    ("Fresnel Power",    Range(0.1, 8)) = 4
        _FresnelStrength ("Fresnel Strength", Range(0, 2))   = 0.8

        [Header(Emission)]
        _EmissionStrength ("Emission Strength", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _WaterSlotDeep;
                float  _WaterSlotElectric;
                float  _WaterAlpha;
                float  _RippleScale;
                float4 _RippleSpeedA;
                float4 _RippleSpeedB;
                float  _NormalStrength;
                float  _SpecularPower;
                float  _SpecularStrength;
                float  _ShimmerSpeed;
                float  _ShimmerStrength;
                float  _FresnelPower;
                float  _FresnelStrength;
                float  _EmissionStrength;
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

            // ── Ruido de valor (calcado de FogPlane.shader) ─────────────────────
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

                int slotDeep     = int(_WaterSlotDeep);
                int slotElectric = int(_WaterSlotElectric);

                // Dos capas de ruido scrolleando en direcciones opuestas — al
                // sumarlas dan un patrón de ondas caótico no lineal (traducción
                // de "sumar dos normal maps offseteados" sin necesitar textura).
                float2 uvA = IN.uv * _RippleScale + _Time.y * _RippleSpeedA.xy;
                float2 uvB = IN.uv * _RippleScale + _Time.y * _RippleSpeedB.xy;
                float  nA  = ValueNoise(uvA);
                float  nB  = ValueNoise(uvB);
                float  combined = nA + nB;

                // Derivada finita del ruido combinado = normal falsa perturbada,
                // mismo resultado visual que perturbar con un normal map sampleado.
                const float eps = 0.05;
                float cX = ValueNoise(uvA + float2(eps, 0)) + ValueNoise(uvB + float2(eps, 0));
                float cY = ValueNoise(uvA + float2(0, eps)) + ValueNoise(uvB + float2(0, eps));
                float2 grad = float2(cX - combined, cY - combined) / eps;
                float3 normalWS = normalize(IN.normalWS + float3(grad.x, 0, grad.y) * _NormalStrength);

                // Specular Blinn-Phong real: el brillo húmedo que pedía la referencia.
                Light mainLight = GetMainLight();
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 halfDir   = normalize(mainLight.direction + viewDirWS);
                float  spec      = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength;

                // Shimmer eléctrico: crackle rápido e intermitente sobre el mismo ruido.
                float shimmerNoise = ValueNoise(uvA * 2.0 + _Time.y * _ShimmerSpeed);
                float shimmer = step(0.92, shimmerNoise) * (sin(_Time.y * _ShimmerSpeed * 3.0) * 0.5 + 0.5);

                float3 deepCol     = _PaletteMidColors[slotDeep].rgb;
                float3 electricCol = _PaletteMidColors[slotElectric].rgb;

                float3 color = deepCol;
                color += spec * mainLight.color;
                color += shimmer * electricCol * _ShimmerStrength;

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower) * _FresnelStrength;
                color += fresnel * electricCol;

                color *= _EmissionStrength;

                return half4(color, _WaterAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
