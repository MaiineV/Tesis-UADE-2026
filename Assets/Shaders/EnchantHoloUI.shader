// Encantamiento: plasma procedural que cubre la silueta del sprite, no una banda
// que la barre. Traducción a HLSL sin texturas de ruido (ValueNoise a mano, mismo
// criterio "sin texturas" del resto del proyecto) de la técnica de referencia:
//   1. Una capa de warp (ondas, no deslizamiento lineal) distorsiona el UV base.
//   2. Una segunda distorsión de UV adicional sobre el resultado del warp.
//   3. Dos capas de "energía" (ValueNoise) moviéndose en direcciones OPUESTAS
//      sobre ese UV ya distorsionado — el contraste entre ambas es lo que da la
//      sensación de plasma/nubes en vez de una textura deslizándose.
//   4. Gradiente de 3 tonos: Shadow -> Mid -> Light del slot de paleta (en vez de
//      oscuro->púrpura->magenta fijos), según cuánta "energía" hay en el pixel.
//   5. Rim tipo fresnel en el borde de la silueta (un sprite UI es plano, sin
//      normal 3D real — se aproxima con el gradiente del alfa, fwidth).
//   6. Emission: el color final se multiplica por encima de 1.0 para que un
//      Bloom en la cámara/URP Volume (si está activo) le saque el halo — el
//      shader no controla el Bloom en sí, solo entrega los valores HDR.
//
// La fase de todo el ruido se computa en el VERTEX desde la posición canvas-space
// (no la UV del sprite) — mismo motivo que el shader original: inmune al remapeo
// de un sprite atlas. El sprite en sí solo aporta el alfa (máscara de silueta),
// el color visible es 100% el plasma procedural.
//
// Target: UI (uGUI) en desktop. Variantes: 2 — UNITY_UI_CLIP_RECT.
Shader "Rollgeon/UI/EnchantHoloUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Alpha Mask", 2D) = "white" {}

        [Header(Palette)]
        [ToggleUI] _UsePalette ("Use Global Palette", Float) = 1
        [PaletteSlot] _PaletteSlot ("Palette Slot", Float) = 20

        [Header(Colores Libres si Use Palette esta OFF)]
        _DarkColor  ("Dark Color (sombra)", Color) = (0.05, 0.0, 0.12, 1)
        _MidColor   ("Mid Color (energia)", Color) = (0.45, 0.0, 1.0, 1)
        _LightColor ("Light Color (hot)",   Color) = (1.0, 0.0, 0.85, 1)

        [Header(Base Sprite Visibility)]
        // 1 = el plasma tapa todo el sprite (como hasta ahora). Bajarlo deja ver
        // el arte original del sprite por debajo del efecto.
        _EffectOpacity ("Effect Opacity (1=solo shader, 0=solo sprite)", Range(0, 1)) = 1

        [Header(Noise Base espacio canvas)]
        _NoiseScale ("Noise Scale (canvas-space)", Float) = 0.01

        [Header(Pixel Art)]
        // Snapea las coordenadas de TODO el ruido a una grilla antes de samplear:
        // en vez de gradientes suaves da un mosaico de bloques sólidos, acorde al
        // pixel art. lerp binario (0/1), no branching real.
        [Toggle] _PixelatedNoise ("Pixelated Noise (Block/Mosaic)", Float) = 0
        _PixelBlockSize ("Pixel Block Size (bloques del mosaico)", Float) = 24

        [Header(Warp UV Tercera Capa Distorsiona el Resto)]
        _WarpScale    ("Warp Scale",    Float)         = 1.5
        _WarpSpeed    ("Warp Speed",    Vector)        = (0.05, 0.02, 0, 0)
        _WarpStrength ("Warp Strength", Range(0, 0.3)) = 0.06

        [Header(Distortion UV adicional)]
        _DistortScale    ("Distort Scale",    Float)         = 2.0
        _DistortSpeed    ("Distort Speed",    Vector)        = (0.08, 0.03, 0, 0)
        _DistortStrength ("Distort Strength", Range(0, 0.3)) = 0.12

        [Header(Capas de Energia Opuestas)]
        _Noise1Scale ("Noise 1 Scale", Float)  = 2.8
        _Noise1Speed ("Noise 1 Speed", Vector) = (0.03, -0.10, 0, 0)
        _Noise2Scale ("Noise 2 Scale", Float)  = 5.0
        _Noise2Speed ("Noise 2 Speed", Vector) = (-0.08, 0.05, 0, 0)
        _EnergyLow   ("Energy Contrast Low",  Range(0,1)) = 0.25
        _EnergyHigh  ("Energy Contrast High", Range(0,1)) = 0.85

        [Header(Edge Glow tipo Fresnel)]
        _EdgeGlowWidth    ("Edge Glow Width",    Range(0, 8)) = 2
        _EdgeGlowStrength ("Edge Glow Strength", Range(0, 4)) = 1.4

        [Header(Emission)]
        _EmissionBase        ("Emission Base",         Range(0, 4)) = 1.5
        _EmissionEnergyBoost ("Emission Energy Boost",  Range(0, 6)) = 2.5

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
                float2 noiseUV : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _ClipRect;

            float _UsePalette;
            float _PaletteSlot;
            float4 _DarkColor;
            float4 _MidColor;
            float4 _LightColor;

            float _EffectOpacity;

            float _NoiseScale;

            float _PixelatedNoise;
            float _PixelBlockSize;

            float _WarpScale;
            float4 _WarpSpeed;
            float _WarpStrength;

            float _DistortScale;
            float4 _DistortSpeed;
            float _DistortStrength;

            float _Noise1Scale;
            float4 _Noise1Speed;
            float _Noise2Scale;
            float4 _Noise2Speed;
            float _EnergyLow;
            float _EnergyHigh;

            float _EdgeGlowWidth;
            float _EdgeGlowStrength;

            float _EmissionBase;
            float _EmissionEnergyBoost;

            // Arrays globales subidos por GlobalPaletteManager cada frame
            float4 _PaletteLightColors[32];
            float4 _PaletteMidColors[32];
            float4 _PaletteShadowColors[32];

            // ── Ruido de valor barato (hash + interpolación cúbica), sin textura.
            float Hash1(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash1(i + float2(0, 0));
                float b = Hash1(i + float2(1, 0));
                float c = Hash1(i + float2(0, 1));
                float d = Hash1(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Snapea a grilla — todo pixel dentro del mismo bloque samplea la
            // MISMA coordenada, así que el ruido sale en mosaico sólido en vez
            // de degradé continuo.
            float2 Pixelate(float2 p, float blockSize)
            {
                return floor(p * blockSize) / max(blockSize, 0.0001);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                // En uGUI el CanvasRenderer batchea los graphics ya transformados, así que
                // v.vertex llega en espacio de canvas: base de ruido propia por instancia.
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                o.noiseUV = v.vertex.xy * _NoiseScale;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseTex = tex2D(_MainTex, i.texcoord) * i.color;

                int slot = int(_PaletteSlot);
                float3 darkCol  = _UsePalette > 0.5 ? _PaletteShadowColors[slot].rgb : _DarkColor.rgb;
                float3 midCol   = _UsePalette > 0.5 ? _PaletteMidColors[slot].rgb    : _MidColor.rgb;
                float3 lightCol = _UsePalette > 0.5 ? _PaletteLightColors[slot].rgb  : _LightColor.rgb;

                // Pixel Art: snapea la coordenada base a grilla ANTES de todo lo
                // demás — warp/distortion/energía heredan el mosaico. lerp binario
                // (Toggle es 0 o 1), no branching real.
                float2 baseUV = lerp(i.noiseUV, Pixelate(i.noiseUV, _PixelBlockSize), _PixelatedNoise);

                // 1. Warp: ondas (sin/cos de un ruido) en vez de deslizamiento lineal.
                float warp = ValueNoise(baseUV * _WarpScale + _Time.y * _WarpSpeed.xy);
                float2 warpedUV = baseUV + float2(sin(warp * 6.2831853), cos(warp * 6.2831853)) * _WarpStrength;

                // 2. Distorsión de UV adicional sobre el resultado del warp.
                float distortion = ValueNoise(warpedUV * _DistortScale + _Time.y * _DistortSpeed.xy);
                float2 distortedUV = warpedUV + (distortion - 0.5) * _DistortStrength;

                // 3. Dos capas de energía en direcciones OPUESTAS — el contraste entre
                // ambas (no una sola) es lo que lee como plasma/nubes en vez de textura.
                float noise1 = ValueNoise(distortedUV * _Noise1Scale + _Time.y * _Noise1Speed.xy);
                float noise2 = ValueNoise(distortedUV * _Noise2Scale + _Time.y * _Noise2Speed.xy);
                float energy = noise1 * 0.65 + noise2 * 0.35;
                energy = smoothstep(_EnergyLow, _EnergyHigh, energy);

                // 4. Gradiente de 3 tonos del slot de paleta según la energía.
                float3 color = lerp(darkCol, midCol, smoothstep(0.2, 0.7, energy));
                color        = lerp(color,   lightCol, smoothstep(0.65, 0.95, energy));

                // 5. Rim tipo fresnel en el borde de la silueta (sin normal 3D real).
                float edgeGlow = saturate(fwidth(baseTex.a) * _EdgeGlowWidth);
                color += lightCol * edgeGlow * _EdgeGlowStrength;

                // 6. Emission — valores por encima de 1.0 a propósito, para Bloom.
                color *= _EmissionBase + energy * _EmissionEnergyBoost;

                fixed4 col;
                // _EffectOpacity=1 -> solo el plasma (como antes). Bajarlo deja ver
                // el arte del sprite original por debajo del efecto.
                col.rgb = lerp(baseTex.rgb, color, _EffectOpacity);
                col.a = baseTex.a;

                // El alfa manda siempre: el plasma no puede escapar de la silueta del sprite.
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
