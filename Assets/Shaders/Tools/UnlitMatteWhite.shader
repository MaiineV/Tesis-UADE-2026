// Shader de reemplazo puro para CharacterRenderCapture: pinta CUALQUIER material Opaque de
// blanco unlit, sin luces ni post-proceso. Camera.RenderWithShader lo usa para derivar el canal
// alpha real de la foto (blanco = personaje, negro = fondo), en un segundo pase separado del
// pase "bonito" (con luces/bloom) que da el color final. No se asigna a mano a ningún material.
Shader "Hidden/Rollgeon/UnlitMatteWhite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Matte"
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
