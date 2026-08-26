using System;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Encuadre del arte dentro de un wrapper de <see cref="BossVisualWrapperBuilder"/>: escala,
    /// levantada del piso, collider y barra.
    /// </summary>
    /// <remarks>
    /// Vive afuera de los builders de cada jefe porque lo necesita cualquier pawn armado sobre un
    /// FBX cuyo pivot no esté en los pies — el dado de La Generala y la bomba del Croupier son el
    /// mismo problema con dos artes distintos.
    /// </remarks>
    public static class BossArtFitter
    {
        /// <summary>Nombre del hijo que envuelve el arte; el default de <c>BossWrapperSpec</c>.</summary>
        public const string ArtChildName = "Art";

        /// <summary>Nombre del hijo de la barra de vida que arma el wrapper.</summary>
        public const string HealthBarChildName = "Canvas";

        /// <summary>Fuera de este rango el arte está mal exportado, no mal encuadrado.</summary>
        public const float MinArtScale = 0.3f;

        public const float MaxArtScale = 3f;

        private const string LogPrefix = "[BossArtFitter] ";

        /// <summary>
        /// Escala, levantada y bounds finales de un prefab de arte dentro de su wrapper.
        /// </summary>
        /// <remarks>
        /// <see cref="BossVisualWrapperBuilder"/> anida el arte a escala 1 en el origen del wrapper,
        /// que es lo correcto para un rig con el pivot en los pies y la altura de un jefe. Un dado o
        /// una bomba tienen el pivot en el centro del volumen, así que apoyados en el origen quedan
        /// medio enterrados. Este struct calcula la corrección a partir de los bounds reales del
        /// arte, y <see cref="Apply"/> la escribe en el prefab.
        /// </remarks>
        public readonly struct ArtFit
        {
            /// <summary>Escala uniforme del hijo <c>Art</c>.</summary>
            public readonly float Scale;

            /// <summary>Y local del hijo <c>Art</c> para que el arte apoye en el piso.</summary>
            public readonly float Lift;

            /// <summary>Bounds del arte ya escalado y apoyado — es lo que tiene que cubrir el collider.</summary>
            public readonly Bounds Bounds;

            public readonly Vector3 HealthBarOffset;

            public ArtFit(float scale, float lift, Bounds bounds, Vector3 healthBarOffset)
            {
                Scale = scale;
                Lift = lift;
                Bounds = bounds;
                HealthBarOffset = healthBarOffset;
            }

            public static ArtFit For(Bounds raw, float targetHeight, float maxWidth, float barClearance)
            {
                float scale = FitScale(raw, targetHeight, maxWidth);
                var scaled = ScaleBounds(raw, scale);

                float lift = -scaled.min.y;
                var grounded = new Bounds(scaled.center + new Vector3(0f, lift, 0f), scaled.size);

                return new ArtFit(scale, lift, grounded,
                    new Vector3(0f, grounded.max.y + barClearance, 0f));
            }

            /// <summary>Fallback cuando el arte no reporta bounds: se deja como lo dejó el wrapper.</summary>
            public static ArtFit Unmeasured(float barHeight) => new ArtFit(
                1f, 0f,
                new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f)),
                new Vector3(0f, barHeight, 0f));
        }

        public static ArtFit Measure(string artPath, float targetHeight, float maxWidth, float barClearance)
        {
            if (TryMeasurePrefab(artPath, out var raw))
                return ArtFit.For(raw, targetHeight, maxWidth, barClearance);

            Debug.LogWarning(LogPrefix + $"No se pudieron medir los bounds de '{artPath}' — el wrapper " +
                             "sale a escala 1 y hay que revisar collider y barra a mano.");
            return ArtFit.Unmeasured(targetHeight + barClearance);
        }

        /// <summary>
        /// Escala para llegar a <paramref name="targetHeight"/> sin pasarse de
        /// <paramref name="maxWidth"/>: manda la restricción más chica, porque un jefe que llega al
        /// alto pedido derramándose sobre las casillas vecinas deja de leerse en su tile.
        /// </summary>
        public static float FitScale(Bounds raw, float targetHeight, float maxWidth)
        {
            float scale = targetHeight / Mathf.Max(raw.size.y, Mathf.Epsilon);

            float widest = Mathf.Max(raw.size.x, raw.size.z);
            if (widest > Mathf.Epsilon) scale = Mathf.Min(scale, maxWidth / widest);

            return Mathf.Clamp(scale, MinArtScale, MaxArtScale);
        }

        /// <summary>Bounds del arte a una escala dada; los props los necesitan para apoyar y tocar.</summary>
        public static Bounds ScaleBounds(Bounds bounds, float scale) =>
            new Bounds(bounds.center * scale, bounds.size * scale);

        /// <summary>
        /// Bounds de los Mesh/SkinnedMesh renderers de un prefab, medidos con el prefab en el origen
        /// y a escala 1 — el mismo encuadre en el que el wrapper anida el arte.
        /// </summary>
        public static bool TryMeasurePrefab(string prefabPath, out Bounds bounds)
        {
            bounds = default;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
            {
                Debug.LogWarning(LogPrefix + $"No hay prefab en '{prefabPath}' — no se puede medir.");
                return false;
            }

            var probe = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (probe == null) return false;

            try
            {
                // El prefab puede traer el transform de la sala donde se autoró (la caja de dados
                // viene en 1.5/0.783/-1.5): sin resetear, los bounds saldrían corridos.
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                probe.transform.localScale = Vector3.one;

                bool any = false;
                foreach (var renderer in probe.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                    if (any) bounds.Encapsulate(renderer.bounds);
                    else { bounds = renderer.bounds; any = true; }
                }

                if (!any || bounds.size.y <= Mathf.Epsilon)
                {
                    Debug.LogWarning(LogPrefix + $"'{prefabPath}' no reporta bounds usables.");
                    return false;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Escribe el fit sobre el wrapper ya guardado: escala y levanta el hijo <c>Art</c>,
        /// re-dimensiona el collider del root y, opcionalmente, encoge la barra y corre un paso extra
        /// sobre el arte.
        /// </summary>
        /// <remarks>
        /// Es una segunda pasada y no un parámetro del spec porque <see cref="BossVisualWrapperBuilder"/>
        /// fija el arte en identidad a propósito (su collider asume eso). Reescribir sobre el mismo path
        /// mantiene el GUID, así que los <c>EnemyDataSO</c> que ya apuntan al wrapper sobreviven.
        /// </remarks>
        public static void Apply(
            string prefabPath,
            ArtFit fit,
            Action<Transform> postProcess = null,
            float barScale = 1f)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning(LogPrefix + $"No se pudo abrir '{prefabPath}' para ajustar el arte.");
                return;
            }

            try
            {
                var art = contents.transform.Find(ArtChildName);
                if (art == null)
                {
                    Debug.LogWarning(LogPrefix + $"'{prefabPath}' no tiene hijo '{ArtChildName}' — " +
                                     "no se ajusta ni la escala ni el collider.");
                    return;
                }

                art.localScale = Vector3.one * fit.Scale;
                art.localPosition = new Vector3(0f, fit.Lift, 0f);

                // El wrapper dimensionó el collider con el arte en identidad: escalado y levantado,
                // ese collider queda chico y corrido respecto de lo que se ve.
                var box = contents.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = fit.Bounds.center;
                    box.size = fit.Bounds.size;
                }

                if (!Mathf.Approximately(barScale, 1f))
                {
                    var bar = contents.transform.Find(HealthBarChildName);
                    if (bar != null) bar.localScale = Vector3.one * barScale;
                }

                postProcess?.Invoke(art);

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
