// Post process de pantalla completa (FullScreenPassRendererFeature en PC_Renderer).
// Hace 2 cosas independientes sobre el frame ya renderizado:
//   1. Pixelado: snappea el sampleo a una grilla de bloques de N x N pixeles
//      de pantalla real, con blend suave en el borde de cada bloque para
//      evitar shimmer (mismo truco que SharpUpscale.shader).
//   2. Lineas de tinta tipo comic: samplea depth y normal en una grilla de
//      3x3 alrededor de cada pixel para detectar 2 tipos de borde:
//        - Linea (silueta dura): el centro esta mas cerca que un vecino en
//          mas que el umbral de profundidad. Color solido, sin blend con
//          el fondo.
//        - Crease (doblez interno suave): el vecino tiene una normal que se
//          curva de forma convexa respecto al centro, sin ser una silueta
//          dura. Se dibuja mas tenue y se suprime si el mismo punto tambien
//          parece concavo (evita creases falsos en esquinas internas).
// Los umbrales de profundidad se escalan segun que tan de canto mira la
// superficie a la camara, porque una superficie casi de perfil (piso visto
// muy angulado) tiene saltos de profundidad enormes por pura perspectiva,
// no porque haya un borde real ahi.
Shader "Hidden/Custom/GodotParity/Post"
{
    Properties
    {
        [Header(Pixelado)]
        _PixelSize ("Tamano de bloque en pixeles de pantalla", Range(1, 16)) = 4
        _TexelOffset ("Offset sub pixel para paneo suave, normalmente 0", Vector) = (0,0,0,0)
        _PixelationScreenSize ("Ancho, Alto, 1 sobre Ancho, 1 sobre Alto de pantalla", Vector) = (1920,1080,0.00052,0.00093)

        [Header(Lineas de silueta bordes duros)]
        [Toggle] _LineOverlay ("Mezclar con el color de abajo en vez de pintar solido", Float) = 1
        _LineTint ("Color de la linea", Color) = (0,0,0,1)
        _LineAlpha ("Opacidad de la linea", Range(0,1)) = 0.5

        [Header(Creases doblez interno suave)]
        [Toggle] _CreaseOverlay ("Mezclar con el color de abajo en vez de pintar solido", Float) = 1
        _CreaseTint ("Color del crease", Color) = (0.833,0.833,0.833,1)
        _CreaseAlpha ("Opacidad del crease", Range(0,1)) = 1
        _CreaseFeather ("Ancho del degrade en el borde del crease", Range(0,0.5)) = 0

        [Header(Intercambiar colores)]
        [Toggle] _FlipPalettes ("Usar el color de linea para crease y viceversa", Float) = 0

        [Header(Deteccion de bordes)]
        _KernelRadius ("Radio de muestreo en pixeles de la grilla 3x3", Range(0.5, 4)) = 1
        _ZDeltaCutoff ("Diferencia de profundidad minima para contar como borde", Range(0, 1)) = 0.25

        [Header(Angulo de canto)]
        _AngleZCutoff ("A partir de que angulo de canto se vuelve mas permisivo, 0 mirando de frente, 1 de canto total", Range(0, 1)) = 0.5
        _AngleZScale ("Cuanto se relaja el umbral de profundidad en angulos de canto, debe ser positivo", Range(-2, 2)) = 2

        [Header(Bordes convexos)]
        _ConvexCutoff ("Que tan marcada tiene que ser la curva para contar como crease", Range(0, 2)) = 0.1

        [Header(Supresion de bordes concavos)]
        _ConcaveCutoff ("Que tan marcada tiene que ser la curva concava para cancelar el crease cercano", Range(0, 2)) = 0.01
        _ConcaveZCutoff ("Diferencia de profundidad maxima para que la supresion concava aplique", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GodotParityPost"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _PixelSize;              // Tamano de bloque en pixeles de pantalla real
            float4 _TexelOffset;           // Offset sub pixel manual, ver .xy
            float4 _PixelationScreenSize;  // (ancho, alto, 1/ancho, 1/alto) — no se actualiza solo, debe coincidir con la resolucion real

            float4 _LineTint;    // Color de la linea de silueta (borde duro)
            float4 _CreaseTint;  // Color del crease (doblez interno suave)
            float _FlipPalettes; // >0.5: usa _CreaseTint para lineas y _LineTint para creases

            float _LineOverlay;   // >0.5: mezcla con Composite() en vez de pintar solido
            float _LineAlpha;
            float _CreaseOverlay; // >0.5: mezcla con Composite() en vez de pintar solido
            float _CreaseAlpha;

            float _KernelRadius;   // Separacion en pixeles entre las 9 muestras de la grilla de deteccion
            float _ZDeltaCutoff;   // Umbral base de diferencia de profundidad (mundo) para contar como borde
            float _AngleZCutoff;   // Facing (0=de frente, 1=de canto) a partir del cual se relaja el umbral
            float _AngleZScale;    // Cuanto se multiplica el umbral en superficies de canto — DEBE ser positivo,
                                    // negativo hace lo contrario (encoge el umbral en angulos de canto) y
                                    // hasLine se dispara en casi cualquier pixel ahi, oscureciendo toda la pantalla
            float _ConvexCutoff;   // Magnitud minima de curvatura convexa para dibujar un crease
            float _CreaseFeather;  // Ancho del smoothstep sobre _ConvexCutoff (0 = corte duro)
            float _ConcaveCutoff;  // Magnitud minima de curvatura concava para suprimir el crease vecino
            float _ConcaveZCutoff; // Diferencia de profundidad maxima para que la supresion concava aplique

            // Returns linear eye depth in world units, handling both projection modes.
            // LinearEyeDepth() uses the PERSPECTIVE formula and gives wrong results for
            // orthographic cameras (reversed ordering, wrong magnitude).
            // For orthographic (unity_OrthoParams.w = 1):
            //   With UNITY_REVERSED_Z, rawDepth=1 at near, rawDepth=0 at far.
            //   Correct depth = lerp(far, near, rawDepth).
            float GetLinearDepth(float rawDepth)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    // _ProjectionParams.y = near, _ProjectionParams.z = far
                    return lerp(_ProjectionParams.z, _ProjectionParams.y, rawDepth);
                }
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float3 Composite(float3 dst, float3 src, float overlay)
            {
                if (overlay > 0.5)
                {
                    float3 multPart = 2.0 * dst * src;
                    float3 screenPart = 1.0 - 2.0 * (1.0 - dst) * (1.0 - src);
                    float3 mask = step(0.5, dst);
                    return saturate(lerp(multPart, screenPart, mask));
                }
                return saturate(src);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 shiftedUv = uv - _TexelOffset.xy * _PixelationScreenSize.zw;
                float2 baseUv = shiftedUv;

                float pixelSize = max(_PixelSize, 1.0);
                if (pixelSize > 1.001)
                {
                    // Sharp-sample filter matching Godot's upscale_and_offset.gdshader:
                    // smoothstep blending within the last fw fraction of each pixel cell
                    // avoids hard-snap jitter while keeping clean pixel art boundaries.
                    float2 px = _PixelationScreenSize.zw * pixelSize;
                    float2 fw = clamp(fwidth(shiftedUv) / px, 1e-5, 1.0);
                    float2 grid = shiftedUv / px - 0.5 * fw;
                    float2 blend = smoothstep(1.0 - fw, float2(1.0, 1.0), frac(grid));
                    baseUv = (floor(grid) + 0.5 + blend) * px;
                }

                float3 px = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, baseUv, 0).rgb;
                float2 stepUv = _PixelationScreenSize.zw * _KernelRadius;

                // Camera axes in world space (from inverse view matrix).
                // We convert world-space normals to a view-space where:
                //   x = camera right, y = camera up, z = -camFwd
                // This matches Godot's normal_roughness_texture convention
                // (normalVS.z > 0 = facing camera) for correct crease detection.
                float3 camRight = normalize(UNITY_MATRIX_I_V[0].xyz);
                float3 camUp    = normalize(UNITY_MATRIX_I_V[1].xyz);
                float3 camFwd   = normalize(UNITY_MATRIX_I_V[2].xyz);

                float depthSamples[9];
                float3 normalSamples[9]; // view-space (z > 0 = toward camera)
                float crossSamples[9];

                [unroll]
                for (int k = 0; k < 9; k++)
                {
                    float2 off = float2((k % 3) - 1, (k / 3) - 1);
                    float2 nuv = saturate(baseUv + off * stepUv);
                    // GetLinearDepth → world-unit distance, correct for both ortho & perspective
                    float rawDepth = SampleSceneDepth(nuv);
                    depthSamples[k] = GetLinearDepth(rawDepth);
                    // Transform world normal → view space matching Godot convention
                    // SafeNormalize: avoids NaN when normals texture is black/unavailable.
                    // normalize(float3(0,0,0)) = NaN via rsqrt(0)=INF; NaN poisons zThresh,
                    // making every depth comparison false → no outlines drawn.
                    float3 nWS = SafeNormalize(SampleSceneNormals(nuv));
                    // If normals unavailable, nWS=(0,0,0) → treat as facing camera (z=1 in view space).
                    if (dot(nWS, nWS) < 1e-5) nWS = camFwd; // fallback: perfectly facing camera
                    normalSamples[k] = float3(dot(nWS, camRight), dot(nWS, camUp), -dot(nWS, camFwd));
                }

                // facing: 0 = perfectly facing camera, 1 = edge-on (matches Godot)
                float facing = 1.0 - normalSamples[4].z;
                float t01 = saturate((facing - _AngleZCutoff) / max(1e-5, 1.0 - _AngleZCutoff));
                float zThresh = _ZDeltaCutoff * (t01 * _AngleZScale + 1.0);

                float concaveSum = 0.0;
                [unroll]
                for (int k = 0; k < 9; k++)
                {
                    float2 off2 = float2((k % 3) - 1, (k / 3) - 1);
                    float3 cr = cross(normalSamples[4], normalSamples[k]);
                    crossSamples[k] = dot(cr, float3(off2.yx, 0.0));
                    concaveSum += step(_ConcaveCutoff, -crossSamples[k]) * step(depthSamples[k] - depthSamples[4], _ConcaveZCutoff);
                }

                float creaseWeight = 0.0;
                [unroll]
                for (int ki = 0; ki < 4; ki++)
                {
                    int sIdx = 1 + ki * 2;
                    float baseNb = (sIdx < 4) ? 1e-5 : 0.0;
                    bool zDiff = abs(depthSamples[sIdx] - depthSamples[4]) < zThresh;
                    bool zFace = normalSamples[sIdx].z + baseNb > normalSamples[4].z;
                    float soft = (_CreaseFeather > 0.0)
                        ? smoothstep(_ConvexCutoff, _ConvexCutoff + _CreaseFeather, crossSamples[sIdx])
                        : (crossSamples[sIdx] > _ConvexCutoff ? 1.0 : 0.0);
                    creaseWeight += (zDiff && zFace) ? soft : 0.0;
                }

                if (concaveSum > 0.0)
                    creaseWeight = 0.0;

                // Center closer than neighbor → edge (depth in world units)
                bool hasLine =
                    (depthSamples[1] - depthSamples[4] > zThresh) ||
                    (depthSamples[3] - depthSamples[4] > zThresh) ||
                    (depthSamples[5] - depthSamples[4] > zThresh) ||
                    (depthSamples[7] - depthSamples[4] > zThresh);

                float3 cLine = (_FlipPalettes > 0.5) ? _CreaseTint.rgb : _LineTint.rgb;
                float3 cCrease = (_FlipPalettes > 0.5) ? _LineTint.rgb : _CreaseTint.rgb;

                float3 result = px;
                if (hasLine)
                {
                    result = lerp(px, Composite(px, cLine, _LineOverlay), saturate(_LineAlpha));
                }
                else if (creaseWeight > 0.0)
                {
                    float a = saturate(creaseWeight) * saturate(_CreaseAlpha);
                    result = lerp(px, Composite(px, cCrease, _CreaseOverlay), min(a, 1.0));
                }

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
