// Maldición de dado: negativo fotográfico + banda oscura con corazón violeta que barre
// la silueta — el gemelo malvado de EnchantHoloUI. Mismo esqueleto: la fase de la banda
// se computa en el VERTEX desde la posición canvas-space, así cada dado cae en una fase
// distinta con un solo material compartido (cero C#, 1 draw call) y el efecto es inmune
// al remapeo de UVs de un sprite atlas. El ángulo default barre en dirección opuesta al
// holo para que el gemelo se distinga de reojo.
//
// La inversión va ANTES del tint de vértice: el gris 0.35 de SetBlocked debe seguir
// oscureciendo. Invertir después del tint volvería BRILLANTE a un dado bloqueado y
// "blocked" dejaría de leerse.
//
// Sin branching — solo frac/saturate/smoothstep/lerp (regla de shaders del proyecto).
// Target: UI (uGUI) en desktop. Budget: 1 texture sample, ~14 ALU.
// Variantes: 2 — UNITY_UI_CLIP_RECT (recorte por RectMask2D); local, no suma keywords globales.
Shader "Rollgeon/UI/EnchantCurseUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Curse)]
        _CurseStrength ("Strength (0 = sprite normal, 1 = negativo pleno)", Range(0, 1)) = 0.9
        _CurseAngle ("Angle (grados; default opuesto al holo)", Range(0, 360)) = 215
        _CurseScale ("Bandas por pixel de canvas", Float) = 0.008
        _CurseSpeed ("Velocidad de scroll", Float) = 0.35
        _CurseBandDepth ("Band depth (oscuridad del valle)", Range(0, 1)) = 0.65
        _CurseBandGlow ("Band glow (corazón violeta)", Range(0, 2)) = 0.6
        _CurseBandColor ("Band color", Color) = (0.45, 0.15, 0.75, 1)

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
            Name "EnchantCurseUI"

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

            float _CurseStrength;
            float _CurseAngle;
            float _CurseScale;
            float _CurseSpeed;
            float _CurseBandDepth;
            float _CurseBandGlow;
            fixed4 _CurseBandColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                // En uGUI el CanvasRenderer batchea los graphics ya transformados, así que
                // v.vertex llega en espacio de canvas: distinto por dado. De ahí sale la fase.
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;

                float a = radians(_CurseAngle);
                float2 dir = float2(cos(a), sin(a));
                o.phase = dot(v.vertex.xy, dir) * _CurseScale + _Time.y * _CurseSpeed;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texcoord);

                // Negativo ANTES del tint de vértice (ver header): el gris de blocked
                // multiplica al final y el dado bloqueado se mantiene oscuro.
                float3 neg = 1.0 - tex.rgb;
                fixed4 col;
                col.rgb = lerp(tex.rgb, neg, _CurseStrength) * i.color.rgb;
                col.a = tex.a * i.color.a;

                // Mismo perfil de pico-por-ciclo que el sheen del holo (0.35/0.5/0.65),
                // pero acá es un VALLE de oscuridad. pulse*pulse angosta el corazón
                // violeta dentro del valle — gratis, sin branch.
                float band = frac(i.phase);
                float pulse = smoothstep(0.35, 0.5, band) * smoothstep(0.65, 0.5, band);
                col.rgb = col.rgb * (1.0 - pulse * _CurseBandDepth)
                        + _CurseBandColor.rgb * (pulse * pulse * _CurseBandGlow);

                // El alfa manda siempre: la maldición no puede escapar de la silueta del sprite.
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
