using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del cluster de íconos arriba-izquierda de <c>Canvas_PlayerStatus</c>:
    /// contrato, mochila, bolsa de dados y la fila de estados del player (hoy solo la
    /// pasiva de clase). Crea además el prefab del ícono de estado y autora la pasiva del
    /// Warrior. Idempotente — reejecutar actualiza sin duplicar.
    /// </summary>
    /// <remarks>
    /// Va en <c>Canvas_PlayerStatus</c> (sorting 30, siempre activo) y no en el HUD de
    /// exploración: así los estados se ven también en combate, que es donde más importan.
    /// Mismo canvas donde ya vive el cluster de fichas, abajo a la izquierda.
    /// </remarks>
    public static class PlayerIconsSetupTools
    {
        private const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";
        private const string StatusIconPrefabPath = "Assets/Prefabs/UI/StatusEffectIcon.prefab";
        private const string CombatHudPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab";

        private const string UiSheetPath = "Assets/Art/UI/UI-sheet.png";
        private const string ContractSheetPath = "Assets/Art/UI/Contrato/Contrato.png";
        private const string BackpackSheetPath = "Assets/Art/UI/BackPack/Backpack.png";
        private const string DiceBagSheetPath = "Assets/Art/UI/DiceBag/Dicebag.png";
        private const string WarriorPassiveSheetPath = "Assets/Art/UI/Pasives/Warriorpasive.png";
        private const string WarriorPassivePath = "Assets/Rollgeon/Classes/CP_Warrior.asset";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";

        private const string ActiveFrameSlice = "UI-sheet_5";
        private const string InactiveFrameSlice = "UI-sheet_7";

        // Layout del mockup ("ui agregados.png"). Todo en px de la resolución de
        // referencia (1920x1080) y con pivot arriba-izquierda, así que las Y van negativas.
        // Son valores de arranque para playtest — tocar acá y reejecutar.
        private static readonly Vector2 ClusterPos = new Vector2(28f, -24f);
        private static readonly Vector2 NavIconSize = new Vector2(104f, 104f);
        private static readonly Vector2 StatusIconSize = new Vector2(72f, 76f);
        private static readonly Vector2 ContractPos = new Vector2(0f, 0f);
        private static readonly Vector2 StatusRowPos = new Vector2(130f, -15f);
        private static readonly Vector2 BackpackPos = new Vector2(120f, -115f);
        private static readonly Vector2 DiceBagPos = new Vector2(230f, -115f);
        private const float StatusRowSpacing = 8f;

        // Mochila y bolsa van más chicas que el contrato; valores de playtest.
        private const float ContractScale = 1f;
        private const float SmallNavIconScale = 0.8f;

        // El umbral de Furia del Guerrero quedó en 3 cuando PR #66 reescaló la vida x10:
        // se pensó como "30% de vida" sobre un pool de 10 y ahora dispara al 3% de 100.
        private const int WarriorLowHpThreshold = 30;

        [MenuItem("Rollgeon/Player Icons/Setup All")]
        public static void SetupAll()
        {
            CreateStatusIconPrefab();
            SetupPlayerStatusCluster();
            AuthorWarriorPassive();
            RetireLegacyPassiveBadge();
        }

        // ================================================================
        // 1 - Prefab del ícono de estado
        // ================================================================

        [MenuItem("Rollgeon/Player Icons/1 - Create Status Icon Prefab")]
        public static void CreateStatusIconPrefab()
        {
            var activeFrame = LoadSpriteOrError(UiSheetPath, ActiveFrameSlice);
            var inactiveFrame = LoadSpriteOrError(UiSheetPath, InactiveFrameSlice);
            if (activeFrame == null || inactiveFrame == null) return;

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StatusIconPrefabPath);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(StatusIconPrefabPath);
                try
                {
                    BuildStatusIcon(contents, activeFrame, inactiveFrame);
                    PrefabUtility.SaveAsPrefabAsset(contents, StatusIconPrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                Debug.Log($"[PlayerIcons] Prefab actualizado: {StatusIconPrefabPath}");
                return;
            }

            var go = new GameObject("StatusEffectIcon", typeof(RectTransform));
            try
            {
                BuildStatusIcon(go, activeFrame, inactiveFrame);
                PrefabUtility.SaveAsPrefabAsset(go, StatusIconPrefabPath);
                Debug.Log($"[PlayerIcons] Prefab creado: {StatusIconPrefabPath}");
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static void BuildStatusIcon(GameObject root, Sprite activeFrame, Sprite inactiveFrame)
        {
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = StatusIconSize;

            // El marco estira: los slices son barras de 88x24 con borde 9-slice, así que
            // Sliced mantiene el borde a escala 1:1 y solo estira el centro hasta el cuadrado.
            var bg = EnsureChildRect(rootRect, "Background", Vector2.zero, StatusIconSize);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = Vector2.zero;
            bg.offsetMax = Vector2.zero;
            var bgImage = Ensure<Image>(bg.gameObject);
            bgImage.sprite = inactiveFrame;
            bgImage.type = Image.Type.Sliced;
            // Único raycast target del ícono: es lo que le da hover al tooltip, que vive en
            // el root (uGUI sube por la jerarquía buscando quien maneje pointer enter).
            bgImage.raycastTarget = true;

            // El ícono NO estira: preserveAspect sobre un rect con padding, para que el arte
            // de cada estado entre sin deformarse sea cual sea su relación de aspecto.
            var icon = EnsureChildRect(rootRect, "Icon", Vector2.zero, Vector2.zero);
            icon.anchorMin = Vector2.zero;
            icon.anchorMax = Vector2.one;
            icon.offsetMin = new Vector2(8f, 8f);
            icon.offsetMax = new Vector2(-8f, -8f);
            var iconImage = Ensure<Image>(icon.gameObject);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // Duración: colgando del borde inferior del marco, centrada. Pivot arriba y Y
            // negativa la dejan POR DEBAJO del borde, no encima del arte.
            var duration = EnsureChildRect(rootRect, "Duration", new Vector2(0f, -2f), new Vector2(48f, 26f));
            duration.anchorMin = duration.anchorMax = new Vector2(0.5f, 0f);
            duration.pivot = new Vector2(0.5f, 1f);
            duration.anchoredPosition = new Vector2(0f, -2f);
            var durationLabel = Ensure<TextMeshProUGUI>(duration.gameObject);
            durationLabel.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            durationLabel.fontSize = 22f;
            durationLabel.alignment = TextAlignmentOptions.Center;
            durationLabel.raycastTarget = false;
            durationLabel.text = string.Empty;
            duration.gameObject.SetActive(false);

            var tooltip = Ensure<UITooltipTrigger>(root);

            var view = Ensure<StatusEffectIconView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("_background").objectReferenceValue = bgImage;
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_activeBackground").objectReferenceValue = activeFrame;
            so.FindProperty("_inactiveBackground").objectReferenceValue = inactiveFrame;
            so.FindProperty("_durationLabel").objectReferenceValue = durationLabel;
            so.FindProperty("_tooltip").objectReferenceValue = tooltip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================
        // 2 - Cluster arriba-izquierda en Canvas_PlayerStatus
        // ================================================================

        [MenuItem("Rollgeon/Player Icons/2 - Setup PlayerStatus Cluster")]
        public static void SetupPlayerStatusCluster()
        {
            var iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StatusIconPrefabPath);
            if (iconPrefab == null)
            {
                Debug.LogError("[PlayerIcons] Correr primero '1 - Create Status Icon Prefab'.");
                return;
            }
            var iconView = iconPrefab.GetComponent<StatusEffectIconView>();
            if (iconView == null)
            {
                Debug.LogError($"[PlayerIcons] {StatusIconPrefabPath} no tiene StatusEffectIconView.");
                return;
            }

            var contract = LoadSpriteOrError(ContractSheetPath, "Contrato_0");
            var backpack = LoadSpriteOrError(BackpackSheetPath, "Backpack_0");
            var diceBag = LoadSpriteOrError(DiceBagSheetPath, "Dicebag_0");
            if (contract == null || backpack == null || diceBag == null) return;

            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            try
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                {
                    Debug.LogError("[PlayerIcons] Canvas no encontrado en Canvas_PlayerStatus.");
                    return;
                }
                var canvasRect = (RectTransform)canvas.transform;

                var cluster = EnsureChildRect(canvasRect, "TopLeftCluster", ClusterPos, Vector2.zero);
                cluster.anchorMin = cluster.anchorMax = new Vector2(0f, 1f);
                cluster.pivot = new Vector2(0f, 1f);
                cluster.anchoredPosition = ClusterPos;
                cluster.sizeDelta = Vector2.zero;

                EnsureNavIcon(cluster, "ContractIcon", ContractPos, contract, ContractScale,
                    PlayerIconTextKeys.Contract, "Contrato");
                EnsureNavIcon(cluster, "BackpackIcon", BackpackPos, backpack, SmallNavIconScale,
                    PlayerIconTextKeys.Backpack, "Inventario");
                EnsureNavIcon(cluster, "DiceBagIcon", DiceBagPos, diceBag, SmallNavIconScale,
                    PlayerIconTextKeys.DiceBag, "Bolsa de dados");

                // Fila de estados: el layout group ordena los íconos que la vista instancia.
                var row = EnsureChildRect(cluster, "StatusRow", StatusRowPos, StatusIconSize);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 1f);
                row.anchoredPosition = StatusRowPos;
                var layout = Ensure<HorizontalLayoutGroup>(row.gameObject);
                layout.spacing = StatusRowSpacing;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var fitter = Ensure<ContentSizeFitter>(row.gameObject);
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var view = Ensure<PlayerStatusIconsView>(row.gameObject);
                var so = new SerializedObject(view);
                so.FindProperty("_iconPrefab").objectReferenceValue = iconView;
                so.FindProperty("_container").objectReferenceValue = row;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
                Debug.Log("[PlayerIcons] Cluster arriba-izquierda armado en Canvas_PlayerStatus.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureNavIcon(RectTransform cluster, string name, Vector2 pos, Sprite sprite,
            float scale, string tooltipKey, string tooltipFallback)
        {
            var rect = EnsureChildRect(cluster, name, pos, NavIconSize);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = NavIconSize;
            rect.localScale = new Vector3(scale, scale, 1f);

            var image = Ensure<Image>(rect.gameObject);
            image.sprite = sprite;
            image.preserveAspect = true;
            // Necesario para el hover — y para el click, en los que ya lo tienen. Sí roban
            // input al mundo en su esquina, que es lo esperable en un ícono clickeable.
            image.raycastTarget = true;

            // El componente captura la escala de reposo en Awake, así que agregarlo DESPUÉS
            // de fijar localScale importa: la mochila y la bolsa van a 0.8.
            Ensure<IconHoverScale>(rect.gameObject);

            Ensure<UITooltipTrigger>(rect.gameObject);
            var tooltip = Ensure<LocalizedTooltip>(rect.gameObject);
            // Por SerializedObject y no por Configure(): el installer corre en edit mode y
            // hay que dejar el prefab sucio para que el valor se persista.
            var tooltipSo = new SerializedObject(tooltip);
            tooltipSo.FindProperty("_key").stringValue = tooltipKey;
            tooltipSo.FindProperty("_fallback").stringValue = tooltipFallback;
            tooltipSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================
        // 3 - Pasiva del Warrior: íconos, condición de "activa" y umbral
        // ================================================================

        [MenuItem("Rollgeon/Player Icons/3 - Author Warrior Passive")]
        public static void AuthorWarriorPassive()
        {
            var passive = AssetDatabase.LoadAssetAtPath<ClassPassiveSO>(WarriorPassivePath);
            if (passive == null)
            {
                Debug.LogError($"[PlayerIcons] No se encontró {WarriorPassivePath}.");
                return;
            }

            var activeIcon = LoadSpriteOrError(WarriorPassiveSheetPath, "Warriorpasive_1");
            var inactiveIcon = LoadSpriteOrError(WarriorPassiveSheetPath, "Warriorpasive_0");
            if (activeIcon == null || inactiveIcon == null) return;

            passive.ActiveIcon = activeIcon;
            passive.InactiveIcon = inactiveIcon;
            passive.AlwaysActive = false;

            // La condición mira el RESULTADO (el modifier que el effect deja sobre Attack),
            // no el umbral: así el ícono no puede desincronizarse de la pasiva si alguien
            // retunea el HP de disparo. Mismo criterio que EffLowHpAttackBuff.IsActiveFor.
            passive.ActiveConditions = new List<BasePreCondition>
            {
                new PCHasModifier
                {
                    AttributeType = typeof(Attack),
                    SourceIdString = EffLowHpAttackBuff.PassiveSourceId.ToString(),
                    MinCount = 1,
                },
            };

            int patched = RescaleLowHpThreshold(passive, WarriorLowHpThreshold);

            EditorUtility.SetDirty(passive);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerIcons] CP_Warrior autorado: íconos + condición de activa, " +
                      $"{patched} umbral(es) reescalado(s) a {WarriorLowHpThreshold}.");
        }

        // El umbral vive en un campo privado del effect, dentro del blob de Odin — se setea
        // por reflexión sobre la instancia y se deja re-serializar a Odin. Editar el YAML a
        // mano renumera los SerializationNodes y deserializa null en silencio.
        private static int RescaleLowHpThreshold(ClassPassiveSO passive, int threshold)
        {
            var field = typeof(EffLowHpAttackBuff)
                .GetField("_hpThreshold", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogError("[PlayerIcons] EffLowHpAttackBuff._hpThreshold no encontrado — " +
                               "¿se renombró? El umbral queda sin reescalar.");
                return 0;
            }

            int patched = 0;
            foreach (var buff in EnumerateEffects<EffLowHpAttackBuff>(passive))
            {
                field.SetValue(buff, threshold);
                patched++;
            }
            return patched;
        }

        private static IEnumerable<T> EnumerateEffects<T>(ClassPassiveSO passive) where T : class
        {
            if (passive.Hooks == null) yield break;
            foreach (var hook in passive.Hooks)
            {
                var group = hook?.Effect;
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                    if (eff is T match) yield return match;
            }
        }

        // ================================================================
        // 4 - Retirar el badge de pasiva del HUD de combate
        // ================================================================

        /// <summary>
        /// Desactiva el <c>PassiveBadgeView</c> del HUD de combate: la fila de estados lo
        /// reemplaza y se ve además en exploración. Se desactiva en vez de borrarse, igual
        /// que las barras de vida/energía viejas — es el rollback si el diseño no cierra.
        /// </summary>
        [MenuItem("Rollgeon/Player Icons/4 - Retire Legacy Passive Badge")]
        public static void RetireLegacyPassiveBadge()
        {
            var root = PrefabUtility.LoadPrefabContents(CombatHudPrefabPath);
            try
            {
                var badges = root.GetComponentsInChildren<Rollgeon.UI.HUD.PassiveBadgeView>(true);
                if (badges.Length == 0)
                {
                    Debug.Log("[PlayerIcons] No hay PassiveBadgeView en Canvas_CombatHUD — nada que retirar.");
                    return;
                }

                foreach (var badge in badges) badge.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, CombatHudPrefabPath);
                Debug.Log($"[PlayerIcons] {badges.Length} PassiveBadgeView desactivado(s) en Canvas_CombatHUD.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static Sprite LoadSpriteOrError(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
            {
                Debug.LogError($"[PlayerIcons] Slice '{spriteName}' no encontrado en {assetPath}.");
            }
            return sprite;
        }

        private static RectTransform EnsureChildRect(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, worldPositionStays: false);
            }
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();
    }
}
