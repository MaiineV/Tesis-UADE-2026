// Rayito eléctrico unlit aditivo: mismo patrón de 1 sola pass transparente
// que WaterPuddle.shader/FogPlane.shader (sin ShadowCaster/DepthOnly/
// DepthNormals). Blend One One para que brille con luz propia sumándose a
// lo que haya detrás, Cull Off para que la tira de mesh tenga volumen
// desde cualquier ángulo. El vertex shader vibra con un seno basado en
// _Time.y (traducción directa del "desplazamiento de vértices por código"
// de la referencia); el fragment parpadea con step() sobre un seno —
// cortes secos de encendido/apagado, no un fade.
Shader "Rollgeon/ElectricBolt"
{
    Properties
    {
        [Header(Electric Palette 1 Slot)]
        [PaletteSlot] _BoltSlot ("Bolt Slot", Float) = 3

        [Header(Vertex Jitter)]
        _JitterAmount ("Jitter Amount", Range(0, 0.3)) = 0.05
        _JitterSpeed  ("Jitter Speed",  Float)         = 20

        [Header(Flicker)]
        _FlickerSpeed     ("Flicker Speed",     Float)        = 20
        _FlickerThreshold ("Flicker Threshold", Range(-1, 1)) = 0.1

        [Header(Emission)]
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 3
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

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BoltSlot;
                float _JitterAmount;
                float _JitterSpeed;
                float _FlickerSpeed;
                float _FlickerThreshold;
                float _EmissionStrength;
            CBUFFER_END

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

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

            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posOS = IN.positionOS.xyz;
                float  offset = sin(_Time.y * _JitterSpeed + posOS.y * 10.0) * _JitterAmount;
                posOS.x += offset;

                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                int slotBolt = int(_BoltSlot);

                float flicker = step(_FlickerThreshold, sin(_Time.y * _FlickerSpeed));
                float3 boltCol = _PaletteMidColors[slotBolt].rgb;
                float3 color = boltCol * _EmissionStrength * flicker;

                return half4(color, flicker);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
