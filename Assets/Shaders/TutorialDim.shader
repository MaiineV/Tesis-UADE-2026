// Overlay de tutorial: oscurece toda la pantalla salvo un recorte circular con
// borde feathered. Trabaja en PÍXELES de pantalla (centro/radio/feather) para
// evitar la distorsión elíptica de trabajar en UV con aspect ratio != 1.
// Sin branching — solo smoothstep (regla de shaders del proyecto).
Shader "Rollgeon/UI/TutorialDim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _DimColor ("Dim Color", Color) = (0, 0, 0, 0.78)
        _CutoutCenter ("Cutout Center (px)", Vector) = (960, 540, 0, 0)
        _CutoutRadius ("Cutout Radius (px)", Float) = 120
        _Feather ("Feather (px)", Float) = 24
        _Show ("Show", Range(0, 1)) = 0

        // uGUI stencil plumbing (mismo bloque que los shaders UI del proyecto).
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "TutorialDim"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            fixed4 _DimColor;
            float4 _CutoutCenter;
            float _CutoutRadius;
            float _Feather;
            float _Show;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Posición del fragment en píxeles reales de pantalla.
                float2 pixelPos = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy;

                float dist = distance(pixelPos, _CutoutCenter.xy);

                // 0 dentro del círculo, 1 afuera, transición en el feather.
                // Con _CutoutRadius = 0 no hay agujero (dim uniforme).
                float feather = max(_Feather, 0.0001);
                float outside = smoothstep(_CutoutRadius - feather, _CutoutRadius, dist);

                fixed4 col = _DimColor;
                col.a *= outside * _Show * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
