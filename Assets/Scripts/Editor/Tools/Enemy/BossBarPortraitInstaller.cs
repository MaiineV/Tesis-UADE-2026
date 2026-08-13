using Rollgeon.UI.HUD;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Agrega el slot de retrato a <c>Canvas_BossBar.prefab</c> y lo cablea a
    /// <c>BossBarView._portrait</c> (<c>Tools → Rollgeon → Bosses → Add BossBar Portrait Slot</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// La barra de jefe mostraba nombre y HP pero nada visual: con seis jefes nuevos que comparten
    /// prefab de pawn (placeholders), el nombre era el único dato que los distinguía. El retrato sale
    /// del mismo pipeline que la cola de turnos (<c>BaseEntitySO.Portrait</c> →
    /// <c>IEntityPortraitResolver</c>), así que no hace falta autorar nada por jefe.
    /// </para>
    /// <para>
    /// <b>Idempotente.</b> Si <c>_portrait</c> ya apunta a algo, no toca el prefab — respeta el rect
    /// que un artista haya movido a mano.
    /// </para>
    /// <para>
    /// <b>Rect de arranque.</b> Arriba-izquierda del Root (520x140), a la izquierda del
    /// <c>NameText</c> (que está centrado y ocupa todo el ancho). Pisa un poco el borde izquierdo del
    /// <c>LifeBorder</c> a propósito — el retrato va como medallón por encima del marco, y por eso se
    /// agrega como último hijo (orden de dibujado). Números pensados para nudgear en el editor.
    /// </para>
    /// </remarks>
    public static class BossBarPortraitInstaller
    {
        private const string LogPrefix = "[BossBarPortraitInstaller] ";
        private const string BossBarPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_BossBar.prefab";
        private const string PortraitName = "Portrait";

        private static readonly Vector2 PortraitSize = new Vector2(56f, 56f);
        private static readonly Vector2 PortraitPos = new Vector2(12f, -8f);

        [MenuItem("Tools/Rollgeon/Bosses/Add BossBar Portrait Slot")]
        public static void AddPortraitSlot()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossBarPrefabPath) == null)
            {
                Debug.LogError(LogPrefix + $"No existe '{BossBarPrefabPath}'.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(BossBarPrefabPath);
            try
            {
                var view = root.GetComponentInChildren<BossBarView>(includeInactive: true);
                if (view == null)
                {
                    Debug.LogError(LogPrefix + "El prefab no tiene BossBarView.");
                    return;
                }

                var viewSo = new SerializedObject(view);
                var portraitProp = viewSo.FindProperty("_portrait");
                if (portraitProp == null)
                {
                    Debug.LogError(LogPrefix + "BossBarView no tiene el campo '_portrait' — " +
                                   "¿quedó sin compilar el cambio de la view?");
                    return;
                }
                if (portraitProp.objectReferenceValue != null)
                {
                    Debug.Log(LogPrefix + "El retrato ya está cableado — prefab sin tocar.");
                    return;
                }

                var barRoot = ResolveBarRoot(view, root);
                if (barRoot == null)
                {
                    Debug.LogError(LogPrefix + "No se pudo resolver el Root de la barra.");
                    return;
                }

                var portrait = BuildPortrait(barRoot);
                portraitProp.objectReferenceValue = portrait;
                viewSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BossBarPrefabPath);
                Debug.Log(LogPrefix + $"Slot de retrato agregado bajo '{barRoot.name}' y cableado a BossBarView.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// El Root de la barra sale de <c>BossBarView._root</c> (lo que la view prende y apaga); si
        /// ese campo estuviera vacío, cae al hijo llamado "Root".
        /// </summary>
        private static RectTransform ResolveBarRoot(BossBarView view, GameObject prefabRoot)
        {
            var so = new SerializedObject(view);
            var rootProp = so.FindProperty("_root");
            if (rootProp?.objectReferenceValue is GameObject wired && wired != null)
                return wired.transform as RectTransform;

            return prefabRoot.transform.Find("Root") as RectTransform;
        }

        private static Image BuildPortrait(RectTransform barRoot)
        {
            var rect = barRoot.Find(PortraitName) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(PortraitName, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(barRoot, worldPositionStays: false);
            }
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = PortraitPos;
            rect.sizeDelta = PortraitSize;
            rect.localScale = Vector3.one;

            var image = rect.gameObject.TryGetComponent<Image>(out var existing)
                ? existing
                : rect.gameObject.AddComponent<Image>();
            // Arranca apagada y sin sprite: la view la prende sólo cuando resuelve un retrato,
            // así el prefab nunca muestra el cuadro blanco del default de uGUI.
            image.sprite = null;
            image.enabled = false;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }
}
