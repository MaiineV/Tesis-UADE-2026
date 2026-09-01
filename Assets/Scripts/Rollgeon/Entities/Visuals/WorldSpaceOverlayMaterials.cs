using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// Materiales overlay (ZTest Always) para la UI world-space que tiene que dibujarse por encima
    /// de la geometría del mundo.
    /// </summary>
    /// <remarks>
    /// BUG-050: un Canvas World Space hereda el ZTest LEqual del canvas y la geometría del mundo lo
    /// recorta — incluida la pared "oculta" de <c>WallOccluder</c>, que sigue escribiendo depth vía
    /// su clip() dithered. Materiales compartidos y cacheados estáticamente: un solo Material de
    /// Image y un clon del material de fuente por material de origen distinto, reusados por TODAS
    /// las barras e íconos del juego en vez de instanciar uno por spawn.
    /// </remarks>
    internal static class WorldSpaceOverlayMaterials
    {
        private static Material s_OverlayImageMaterial;
        private static bool s_OverlayImageMaterialResolved;
        private static readonly Dictionary<Material, Material> s_OverlayTmpMaterials = new();

        /// <summary>
        /// Asigna el material overlay a todas las <see cref="Image"/> y TMP hijas de
        /// <paramref name="root"/>. Fallback silencioso al material original si el shader no está
        /// disponible (build sin el shader stripeado, o falta de importación).
        /// </summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;

            var overlayImageMat = GetOverlayImageMaterial();
            if (overlayImageMat != null)
            {
                foreach (var img in root.GetComponentsInChildren<Image>(includeInactive: true))
                    img.material = overlayImageMat;
            }

            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true))
            {
                var overlayTmpMat = GetOverlayTmpMaterial(tmp.fontSharedMaterial);
                if (overlayTmpMat != null)
                    tmp.fontSharedMaterial = overlayTmpMat;
            }
        }

        private static Material GetOverlayImageMaterial()
        {
            if (s_OverlayImageMaterialResolved) return s_OverlayImageMaterial;
            s_OverlayImageMaterialResolved = true;

            var shader = Shader.Find("OverlayUI");
            if (shader == null) return null;

            s_OverlayImageMaterial = new Material(shader) { name = "WorldSpace UI (Overlay)" };
            return s_OverlayImageMaterial;
        }

        // Clona el material de fuente UNA vez por material de origen distinto (no por barra ni por
        // TMP_FontAsset con caras multiples que comparten material) y reusa el clon — instanciar por
        // barra hubiera significado un Material nuevo por cada enemigo/cofre spawneado en cada sala.
        private static Material GetOverlayTmpMaterial(Material source)
        {
            if (source == null) return null;
            if (s_OverlayTmpMaterials.TryGetValue(source, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (shader == null) return null;

            // El clon preserva atlas/face/outline del material de origen; solo el shader cambia a
            // la variante Overlay (ZTest Always).
            var overlay = new Material(source) { shader = shader, name = source.name + " (Overlay)" };
            s_OverlayTmpMaterials[source] = overlay;
            return overlay;
        }
    }
}
