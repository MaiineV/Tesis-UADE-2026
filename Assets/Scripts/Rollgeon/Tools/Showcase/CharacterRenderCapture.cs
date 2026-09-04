using System;
using System.IO;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rollgeon.Tools.Showcase
{
    /// <summary>
    /// Exporta un PNG con canal alpha real del personaje parado en <c>SubjectAnchor</c>, usando
    /// las luces/shader/post-proceso reales del proyecto. Vive en la escena
    /// <c>CharacterShowcase</c> — arrastrar cualquier prefab de personaje como hijo del anchor,
    /// encuadrar con <see cref="RenderCamera"/> y apretar el botón.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué dos pasadas.</b> URP compone el post-proceso (bloom, color grading) en un blit
    /// final que no preserva alpha de forma confiable — pedirle "bonito Y transparente" a una sola
    /// captura no es confiable. En cambio: una pasada "bonita" (luces + post-proceso reales, tal
    /// cual se ve en el Inspector) da el RGB; una segunda pasada de reemplazo
    /// (<see cref="MatteShader"/>, blanco unlit sin post-proceso) da un mate blanco/negro que se
    /// usa como canal alpha. El compuesto final tiene el look completo del juego CON transparencia
    /// real, sin tener que resolver ese problema dentro del pipeline de post-proceso.
    /// </para>
    /// <para>
    /// <b>Requisito del mate.</b> <see cref="MatteShader"/> reemplaza por <c>RenderType</c>
    /// (ver <see cref="Camera.RenderWithShader"/>) — cualquier material cuyo shader declare
    /// <c>Tags{"RenderType"="Opaque"}</c> (todos los <c>PaletteCelLit</c>-family del proyecto lo
    /// hacen) se pinta blanco sólido para esa pasada, sin tocar los materiales reales.
    /// </para>
    /// <para>
    /// <b>No Editor/:</b> es un <see cref="MonoBehaviour"/> pensado para vivir colgado de un
    /// GameObject en la escena de showcase (arrastrar prefab, apretar botón desde el Inspector) —
    /// un componente así no puede vivir en una carpeta <c>Editor/</c> (Unity no permite agregarlo
    /// a un GameObject de escena desde ahí). Solo <see cref="CapturarPNG"/> usa API de editor
    /// (<c>EditorUtility.RevealInFinder</c>), aislada con <c>#if UNITY_EDITOR</c> — es una
    /// herramienta de autoría, nunca corre en build.
    /// </para>
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("Rollgeon/Tools/Character Render Capture")]
    public sealed class CharacterRenderCapture : MonoBehaviour
    {
        [Title("Refs")]
        [Required]
        public Camera RenderCamera;

        [Required]
        [Tooltip("Shader de reemplazo para derivar el alpha — Hidden/Rollgeon/UnlitMatteWhite.")]
        public Shader MatteShader;

        [Required]
        [Tooltip("El mismo shader UI/Custom/SharpUpscale que usa Canvas_Display para agrandar el " +
                 "RT nativo del juego (320x180) — sin esto la resolución nativa baja se ve borrosa " +
                 "en vez del look pixel-art nítido real.")]
        public Shader UpscaleShader;

        [Title("Resolución nativa")]
        [Tooltip("El juego real renderiza a 320x180 (Assets/Art/Rendering/320x180.renderTexture) " +
                 "y lo agranda con SharpUpscale — eso, no GodotParityPost, es lo que le da el look " +
                 "'pixel art' de verdad. Un primer plano de personaje a 320x180 literal perdería " +
                 "casi todo el detalle (esa resolución cubre una sala entera); esto es un valor " +
                 "más alto pensado para un solo personaje, con la misma técnica.")]
        public Vector2Int NativeResolution = new Vector2Int(320, 320);

        [Title("Output")]
        [MinValue(64)]
        public int Width = 1920;

        [MinValue(64)]
        public int Height = 1920;

        [Tooltip("Carpeta relativa al proyecto donde se guardan los PNG.")]
        public string OutputFolder = "Renders/Characters";

#if UNITY_EDITOR
        [Title("Acción")]
        [Button(ButtonSizes.Large), GUIColor(0.3f, 0.8f, 0.5f)]
        public void CapturarPNG()
        {
            if (RenderCamera == null) { Debug.LogError("[CharacterRenderCapture] Falta RenderCamera."); return; }
            if (MatteShader == null) { Debug.LogError("[CharacterRenderCapture] Falta MatteShader."); return; }
            if (UpscaleShader == null) { Debug.LogError("[CharacterRenderCapture] Falta UpscaleShader."); return; }

            var beauty = CaptureBeauty();
            var matte = CaptureMatte();
            var final = Composite(beauty, matte);

            UnityEngine.Object.DestroyImmediate(beauty);
            UnityEngine.Object.DestroyImmediate(matte);

            var bytes = final.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(final);

            var dir = Path.Combine(Application.dataPath, "..", OutputFolder);
            Directory.CreateDirectory(dir);
            var fileName = $"{SubjectName()}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var fullPath = Path.Combine(dir, fileName);
            File.WriteAllBytes(fullPath, bytes);

            Debug.Log($"[CharacterRenderCapture] Render guardado: {fullPath}");
            EditorUtility.RevealInFinder(fullPath);
        }

        private string SubjectName()
        {
            var anchor = GameObject.Find("SubjectAnchor")?.transform;
            if (anchor != null && anchor.childCount > 0) return anchor.GetChild(0).name;
            return "Character";
        }

        private static readonly int s_PixelationScreenSize = Shader.PropertyToID("_PixelationScreenSize");

        /// <summary>
        /// Pasada real: luces, shader del juego, post-proceso, tal cual se ve — PERO igual que el
        /// juego real, primero a resolución nativa baja (<see cref="NativeResolution"/>) y recién
        /// después ampliada con <see cref="UpscaleShader"/>. Renderizar directo a
        /// <see cref="Width"/>/<see cref="Height"/> (como hacía antes) se salteaba el paso que
        /// realmente le da el look "pixel art" al juego — <c>GodotParityPost</c> es una capa
        /// secundaria encima, no la fuente del look.
        /// </summary>
        private Texture2D CaptureBeauty()
        {
            int nw = Mathf.Max(1, NativeResolution.x);
            int nh = Mathf.Max(1, NativeResolution.y);

            // _PixelationScreenSize tiene que coincidir con la resolución REAL a la que se
            // renderiza (la nativa, no la de exportación) — mismo motivo que antes, ahora
            // apuntado al target correcto.
            var prevPixelationSize = Shader.GetGlobalVector(s_PixelationScreenSize);
            Shader.SetGlobalVector(s_PixelationScreenSize, new Vector4(nw, nh, 1f / nw, 1f / nh));

            var nativeRt = new RenderTexture(nw, nh, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var prevTarget = RenderCamera.targetTexture;

            RenderCamera.targetTexture = nativeRt;
            RenderCamera.Render();
            RenderCamera.targetTexture = prevTarget;

            Shader.SetGlobalVector(s_PixelationScreenSize, prevPixelationSize);

            // Segundo paso: el mismo shader que usa Canvas_Display para agrandar el RT nativo,
            // vía Graphics.Blit (fullscreen quad con ese material) en vez de un RawImage de UI —
            // misma matemática de snap nítido, sin necesitar Canvas/UI para esta captura puntual.
            var upscaleMat = new Material(UpscaleShader);
            upscaleMat.SetTexture("_MainTex", nativeRt);
            upscaleMat.SetVector("_PixelPanOffset", Vector4.zero);

            var finalRt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            RenderTexture.active = finalRt;
            GL.Clear(true, true, Color.clear);
            Graphics.Blit(nativeRt, finalRt, upscaleMat);

            RenderTexture.active = finalRt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            RenderTexture.active = prevActive;
            nativeRt.Release();
            UnityEngine.Object.DestroyImmediate(nativeRt);
            finalRt.Release();
            UnityEngine.Object.DestroyImmediate(finalRt);
            UnityEngine.Object.DestroyImmediate(upscaleMat);

            return tex;
        }

        /// <summary>
        /// Pasada de mate: mismo encuadre, todo pintado blanco unlit sin post-proceso — el
        /// blanco/negro resultante se usa como alpha (cobertura, con antialiasing incluido).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>Camera.RenderWithShader</c> NO funciona en URP/SRP — es API de Built-in Render
        /// Pipeline únicamente (Unity la ignora en silencio bajo un Scriptable Render Pipeline).
        /// En su lugar, esto reemplaza a mano los materiales de cada <see cref="Renderer"/> bajo
        /// <c>SubjectAnchor</c> por <see cref="MatteShader"/>, renderiza, y los restaura — funciona
        /// en cualquier pipeline porque no depende de esa API.
        /// </para>
        /// <para>
        /// <b>Misma resolución nativa que <see cref="CaptureBeauty"/>, no <see cref="Width"/>
        /// directo.</b> BUG de playtest: capturar el mate a resolución completa (suave) mientras
        /// el color pasaba por <see cref="NativeResolution"/> (en bloques) daba un recorte feo —
        /// el alpha se degradaba suave pero el color de abajo saltaba en bloques, así que el borde
        /// quedaba con flecos incoherentes. Con las dos pasadas cuantizadas al mismo native+upscale,
        /// el corte queda parejo con el color (un borde "de bloques" limpio, no un remiendo).
        /// </para>
        /// </remarks>
        private Texture2D CaptureMatte()
        {
            var camData = RenderCamera.GetUniversalAdditionalCameraData();
            bool prevPost = camData != null && camData.renderPostProcessing;
            var prevClearFlags = RenderCamera.clearFlags;
            var prevBg = RenderCamera.backgroundColor;

            if (camData != null) camData.renderPostProcessing = false;
            RenderCamera.clearFlags = CameraClearFlags.SolidColor;
            RenderCamera.backgroundColor = Color.black;

            var anchor = GameObject.Find("SubjectAnchor");
            var renderers = anchor != null ? anchor.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            var originalMats = new Material[renderers.Length][];
            var matte = new Material(MatteShader);
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMats[i] = renderers[i].sharedMaterials;
                var swapped = new Material[originalMats[i].Length];
                for (int m = 0; m < swapped.Length; m++) swapped[m] = matte;
                renderers[i].sharedMaterials = swapped;
            }

            int nw = Mathf.Max(1, NativeResolution.x);
            int nh = Mathf.Max(1, NativeResolution.y);
            var nativeRt = new RenderTexture(nw, nh, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var prevTarget = RenderCamera.targetTexture;

            RenderCamera.targetTexture = nativeRt;
            RenderCamera.Render();
            RenderCamera.targetTexture = prevTarget;

            var upscaleMat = new Material(UpscaleShader);
            upscaleMat.SetTexture("_MainTex", nativeRt);
            upscaleMat.SetVector("_PixelPanOffset", Vector4.zero);

            var finalRt = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            RenderTexture.active = finalRt;
            GL.Clear(true, true, Color.clear);
            Graphics.Blit(nativeRt, finalRt, upscaleMat);

            RenderTexture.active = finalRt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            RenderTexture.active = prevActive;
            nativeRt.Release();
            UnityEngine.Object.DestroyImmediate(nativeRt);
            finalRt.Release();
            UnityEngine.Object.DestroyImmediate(finalRt);
            UnityEngine.Object.DestroyImmediate(upscaleMat);

            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = originalMats[i];
            UnityEngine.Object.DestroyImmediate(matte);

            if (camData != null) camData.renderPostProcessing = prevPost;
            RenderCamera.clearFlags = prevClearFlags;
            RenderCamera.backgroundColor = prevBg;
            return tex;
        }

        private static Texture2D Composite(Texture2D beauty, Texture2D matte)
        {
            var final = new Texture2D(beauty.width, beauty.height, TextureFormat.RGBA32, false);
            var beautyPixels = beauty.GetPixels32();
            var mattePixels = matte.GetPixels32();
            var outPixels = new Color32[beautyPixels.Length];

            for (int i = 0; i < outPixels.Length; i++)
            {
                var c = beautyPixels[i];
                byte alpha = mattePixels[i].r; // mate es gris puro (r=g=b), r alcanza.
                outPixels[i] = new Color32(c.r, c.g, c.b, alpha);
            }

            final.SetPixels32(outPixels);
            final.Apply();
            return final;
        }
#endif
    }
}
