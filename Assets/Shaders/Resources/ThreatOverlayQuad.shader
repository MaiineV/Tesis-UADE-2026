// Quad del telegraph de amenazas (ver ThreatTelegraphOverlay).
//
// Por qué existe en vez de reusar Sprites/Default: un quad por casilla amenazada
// llega a ~112 quads simultáneos, y Sprites/Default no tiene cbuffer
// UnityPerMaterial, así que nunca fue compatible con el SRP Batcher. Con este
// shader los quads de un mismo par (estado, matiz) comparten material y entran
// todos en un batch: los SetPass calls / Batches del telegraph bajan de ~112 a
// unos pocos. No baja la cantidad de DrawIndexed — el SRP Batcher no hace eso —
// lo que colapsa es el setup de CPU por draw y el rebind del cbuffer.
//
// El alpha NO sale de _Color: lo pone _PulseAlpha, que el latido reescribe una vez
// por material y por frame. _Color aporta solo el matiz. Esa separación es la que
// permite que el matiz sea per-material (compartido) y el latido siga siendo un
// único float por frame, sin un MaterialPropertyBlock por renderer — que es
// justamente lo que sacaba a estos renderers del batcher.
//
// Compatibilidad con el SRP Batcher: TODA propiedad de material vive dentro del
// único CBUFFER_START(UnityPerMaterial). Las texturas y los samplers van afuera a
// propósito: no cuentan para el cbuffer, y declararlos adentro sí lo rompería.
//
// Sin ShadowCaster, sin DepthOnly, sin DepthNormals y con FallBack Off: estos
// quads no tienen que alimentar el prepass de depth-normals que consume el SSAO.
//
// Un solo variant: ni un multi_compile, incluido multi_compile_instancing (el SRP
// Batcher no lo usa y duplicaría los variants por nada).
Shader "Rollgeon/ThreatOverlayQuad"
{
    Properties
    {
        [Header(Overlay)]
        // El alpha de este color se ignora a propósito: lo pisa _PulseAlpha. Acá
        // sirve solo para que el matiz se lea entero en el inspector del material.
        _Color ("Tint (alpha ignorado)", Color) = (1, 0.45, 0.1, 1)

        // NoScaleOffset a propósito: sin _MainTex_ST el cbuffer se queda en dos
        // propiedades. El default "white" ES el caso "sin patrón": Unity bindea
        // blanco cuando el material no tiene textura, y multiplicar por blanco es
        // no-op — así el patrón opcional no cuesta ni un keyword ni un branch.
        [NoScaleOffset] _MainTex ("Pattern (sin textura = quad plano)", 2D) = "white" {}

        _PulseAlpha ("Pulse Alpha", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _PulseAlpha;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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

                half4 pattern = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Mismo resultado en pantalla que hacía Sprites/Default (tex * _Color)
                // con el alpha del tint reemplazado por el del latido. El patrón sigue
                // modulando por su propio alpha: de ahí salen el punteado y el damero.
                // Salida NO premultiplicada + Blend SrcAlpha OneMinusSrcAlpha da el
                // mismo pixel que la salida premultiplicada + One OneMinusSrcAlpha de
                // Sprites/Default.
                return half4(_Color.rgb * pattern.rgb, _PulseAlpha * pattern.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
