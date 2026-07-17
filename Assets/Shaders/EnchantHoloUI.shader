// Encantamiento de dado: banda holográfica que barre la silueta. Marca "este dado es
// especial" de un vistazo, sin arte nuevo — el arcoíris es procedural.
//
// La fase de la banda se computa en el VERTEX desde la posición canvas-space, no desde la
// UV del sprite. Dos motivos: cada dado cae en una banda distinta y obtiene un hue propio
// con un solo material compartido (cero C#, 1 draw call); y solo el sampleo de _MainTex usa
// texcoord, así que el efecto es inmune al remapeo de UVs de un sprite atlas — uno en
// espacio-UV se rompería al atlasear.
//
// Sin branching — solo frac/abs/saturate/smoothstep (regla de shaders del proyecto).
// Target: UI (uGUI) en desktop. Budget: 1 texture sample, ~15 ALU.
// Variantes: 2 — UNITY_UI_CLIP_RECT (recorte por RectMask2D); local, no suma keywords globales.
Shader "Rollgeon/UI/EnchantHoloUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Holo)]
        _HoloStrength ("Strength (0 = sin holo, 1 = pisa el tint)", Range(0, 1)) = 0.85
        _HoloAngle ("Angle (grados)", Range(0, 360)) = 35
        _HoloScale ("Bandas por pixel de canvas", Float) = 0.008
        _HoloSpeed ("Velocidad de scroll", Float) = 0.35
        _HoloSheen ("Sheen (brillo del pico de banda)", Range(0, 1)) = 0.35

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
            Name "EnchantHoloUI"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
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
                float4 worldPosition : TEXCOORD1;
                float phase : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _ClipRect;

            float _HoloStrength;
            float _HoloAngle;
            float _HoloScale;
            float _HoloSpeed;
            float _HoloSheen;

            // Hue (0..1) a RGB sin branching: los 3 canales son la misma rampa triangular
            // desfasada 1/3 de vuelta, recortada a [0,1].
            float3 Hue2Rgb(float h)
            {
                float3 k = frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0));
                return saturate(abs(k * 6.0 - 3.0) - 1.0);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                // En uGUI el CanvasRenderer batchea los graphics ya transformados, así que
                // v.vertex llega en espacio de canvas: distinto por dado. De ahí sale el hue.
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;

                float a = radians(_HoloAngle);
                float2 dir = float2(cos(a), sin(a));
                o.phase = dot(v.vertex.xy, dir) * _HoloScale + _Time.y * _HoloSpeed;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;

                float band = frac(i.phase);
                float3 holo = Hue2Rgb(band);

                // Pico de brillo una vez por ciclo — la banda "pasa" por el dado en vez de
                // ser un degradé plano.
                float sheen = smoothstep(0.35, 0.5, band) * smoothstep(0.65, 0.5, band);
                holo = saturate(holo + sheen * _HoloSheen);

                col.rgb = lerp(col.rgb, holo, _HoloStrength);

                // El alfa manda siempre: el holo no puede escapar de la silueta del sprite.
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return col;
            }
            ENDCG
        }
    }
}