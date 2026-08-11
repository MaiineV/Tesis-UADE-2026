using MoreMountains.Feedbacks;
using Rollgeon.UI.ChestReveal;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Breakdown;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.Chests
{
    /// <summary>
    /// Installer del reveal gacha del cofre: settings SO, prefab de celda del reel
    /// y <c>Canvas_ChestReveal.prefab</c> con todo el wiring de
    /// <see cref="ChestRevealView"/>. Idempotente (regenera los prefabs).
    /// Paso manual restante: arrastrar el canvas bajo <c>ScreenHost</c> en
    /// <c>02_Gameplay.unity</c> (o correr el paso 3 con la escena abierta).
    /// </summary>
    public static class ChestRevealSetupTools
    {
        private const string LogPrefix = "[ChestRevealSetup] ";

        private const string SettingsPath = "Assets/Rollgeon/Services/ChestRevealUiSettings.asset";
        private const string CellPrefabPath = "Assets/Prefabs/UI/ChestReelCell.prefab";
        private const string CanvasPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_ChestReveal.prefab";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";
        private const string BurstParticlePrefabPath = "Assets/Prefabs/UI/DiceThrowParticle.prefab";

        // Clips PLACEHOLDER (reusados de dados/breakdown) — reemplazar cuando haya
        // SFX propios del cofre. Nunca MMF_Sound (TECHNICAL.md §17).
        private const string TickClipPath = "Assets/Sounds/Dice/sfx_dice_reveal_tick.wav";
        private const string WhooshClipPath = "Assets/Sounds/Breakdown/sfx_breakdown_whoosh.wav";
        private const string LandThunkClipPath = "Assets/Sounds/Breakdown/sfx_breakdown_thunk.wav";
        private const string ChimeClipPath = "Assets/Sounds/Dice/sfx_combo_chime.wav";
        private const string CountTickClipPath = "Assets/Sounds/Breakdown/sfx_breakdown_tick.wav";
        private const string CardSlideClipPath = "Assets/Sounds/Breakdown/sfx_breakdown_card_slide.wav";
        private const string ClickClipPath = "Assets/Sounds/Dice/sfx_ui_click.wav";

        // Paleta (Rollgeon_Paleta_de_Color.md) — mismos grises del resto de la UI.
        private static readonly Color TextColor = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        private static readonly Color PanelColor = new Color32(0x1E, 0x22, 0x28, 0xF2);
        private static readonly Color CellColor = new Color32(0x2A, 0x2F, 0x36, 0xFF);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);

        [MenuItem("Rollgeon/Chests/Reveal/Setup All")]
        public static void SetupAll()
        {
            CreateSettings();
            CreateCellPrefab();
            CreateCanvasPrefab();
            Debug.Log(LogPrefix + "Setup del reveal completo. Falta: canvas bajo ScreenHost en 02_Gameplay.");
        }

        [MenuItem("Rollgeon/Chests/Reveal/1 - Create Settings")]
        public static void CreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ChestRevealUiSettingsSO>(SettingsPath);
            if (settings != null)
            {
                Debug.Log(LogPrefix + "Settings ya existía — se respeta el tuning actual.");
                return;
            }
            settings = ScriptableObject.CreateInstance<ChestRevealUiSettingsSO>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + SettingsPath + " creado con defaults.");
        }

        [MenuItem("Rollgeon/Chests/Reveal/2 - Create Reel Cell Prefab")]
        public static void CreateCellPrefab()
        {
            var settings = LoadSettings();
            float w = settings != null ? settings.CellWidth : 96f;

            var root = new GameObject("ChestReelCell", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(w, w);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var bg = CreateImage(root.transform, "CellBg", CellColor, Vector2.zero, new Vector2(w, w));
                var frame = CreateImage(root.transform, "RarityFrame", Color.white, Vector2.zero, new Vector2(w, w));
                frame.type = Image.Type.Sliced;
                var icon = CreateImage(root.transform, "Icon", Color.white, Vector2.zero, new Vector2(w - 24f, w - 24f));
                icon.preserveAspect = true;
                var goldIcon = CreateImage(root.transform, "GoldIcon", new Color32(0xD9, 0xA4, 0x4E, 0xFF),
                    new Vector2(0f, 8f), new Vector2(w * 0.4f, w * 0.4f));
                var goldLabel = CreateLabel(root.transform, "GoldAmountLabel", "+0", 20f,
                    new Vector2(0f, -w * 0.28f), new Vector2(w, 24f));

                var view = root.AddComponent<ChestReelCellView>();
                var so = new SerializedObject(view);
                so.FindProperty("_cellBg").objectReferenceValue = bg;
                so.FindProperty("_rarityFrame").objectReferenceValue = frame;
                so.FindProperty("_icon").objectReferenceValue = icon;
                so.FindProperty("_goldIcon").objectReferenceValue = goldIcon;
                so.FindProperty("_goldAmountLabel").objectReferenceValue = goldLabel;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
                Debug.Log(LogPrefix + CellPrefabPath + " creado/actualizado.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Rollgeon/Chests/Reveal/3 - Create Canvas Prefab")]
        public static void CreateCanvasPrefab()
        {
            var settings = LoadSettings();
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
            if (settings == null || cellPrefab == null)
            {
                Debug.LogError(LogPrefix + "Faltan settings o cell prefab — correr los pasos 1 y 2 primero.");
                return;
            }

            float cellW = settings.CellWidth;
            float reelH = cellW + 32f;

            var root = new GameObject("Canvas_ChestReveal",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Sobre el CombatHUD, debajo de Pause/Tooltip (ver órdenes en ScreenHost).
                canvas.sortingOrder = 60;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                // Root apagable — todo el reveal cuelga de acá.
                var viewRoot = new GameObject("Root", typeof(RectTransform));
                var viewRootRect = Stretch(viewRoot, root.transform);

                // Dim full-screen: bloquea input y es el click-target del skip.
                var dim = CreateImage(viewRoot.transform, "Dim", DimColor, Vector2.zero, Vector2.zero);
                Stretch(dim.gameObject, viewRoot.transform);
                var dimButton = dim.gameObject.AddComponent<Button>();
                dimButton.transition = Selectable.Transition.None;

                // Panel central.
                var panel = CreateImage(viewRoot.transform, "Panel", PanelColor,
                    Vector2.zero, new Vector2(900f, 420f));
                var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();

                var title = CreateLabel(panel.transform, "Title", "Chest opened!", 34f,
                    new Vector2(0f, 160f), new Vector2(860f, 48f));

                // Viewport del reel con máscara + tira + puntero.
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                var viewportRect = (RectTransform)viewport.transform;
                viewport.transform.SetParent(panel.transform, false);
                viewportRect.sizeDelta = new Vector2(860f, reelH);
                viewportRect.anchoredPosition = new Vector2(0f, 30f);

                var strip = new GameObject("Strip", typeof(RectTransform));
                var stripRect = (RectTransform)strip.transform;
                strip.transform.SetParent(viewport.transform, false);
                stripRect.anchorMin = new Vector2(0f, 0.5f);
                stripRect.anchorMax = new Vector2(0f, 0.5f);
                stripRect.pivot = new Vector2(0f, 0.5f);
                stripRect.sizeDelta = new Vector2(0f, reelH);

                var pointer = CreateImage(viewport.transform, "Pointer", new Color32(0xE7, 0xE3, 0xE2, 0xC0),
                    Vector2.zero, new Vector2(4f, reelH));
                var pointerRect = (RectTransform)pointer.transform;
                pointerRect.anchorMin = new Vector2(0.5f, 0.5f);
                pointerRect.anchorMax = new Vector2(0.5f, 0.5f);

                // Card del reward + hint.
                var card = new GameObject("RewardCard", typeof(RectTransform), typeof(CanvasGroup));
                card.transform.SetParent(panel.transform, false);
                var cardRect = (RectTransform)card.transform;
                cardRect.sizeDelta = new Vector2(860f, 60f);
                cardRect.anchoredPosition = new Vector2(0f, -110f);
                var cardGroup = card.GetComponent<CanvasGroup>();
                var rewardName = CreateLabel(card.transform, "RewardName", string.Empty, 30f,
                    Vector2.zero, new Vector2(860f, 48f));

                var hint = CreateLabel(panel.transform, "Hint", "Click to skip", 20f,
                    new Vector2(0f, -175f), new Vector2(860f, 30f));
                hint.color = new Color32(0xB8, 0xC0, 0xC8, 0xFF);

                // Capa de partículas del landing (pool de Images — ParticleSystem no
                // dibuja sobre un canvas Overlay) + flash propio del canvas (el del
                // HUD queda debajo del sorting order 60).
                var burstLayer = new GameObject("BurstLayer", typeof(RectTransform));
                Stretch(burstLayer, viewRoot.transform);
                var burst = burstLayer.AddComponent<DiceThrowImpactBurst>();
                var burstParticle = AssetDatabase.LoadAssetAtPath<GameObject>(BurstParticlePrefabPath);
                var burstSo = new SerializedObject(burst);
                burstSo.FindProperty("_particlePrefab").objectReferenceValue =
                    burstParticle != null ? burstParticle.GetComponent<Image>() : null;
                burstSo.FindProperty("_container").objectReferenceValue = (RectTransform)burstLayer.transform;
                burstSo.ApplyModifiedPropertiesWithoutUndo();
                if (burstParticle == null)
                    Debug.LogWarning(LogPrefix + BurstParticlePrefabPath + " no existe — burst sin partículas.");

                var flashImage = CreateImage(viewRoot.transform, "Flash", new Color(1f, 1f, 1f, 0f),
                    Vector2.zero, Vector2.zero);
                Stretch(flashImage.gameObject, viewRoot.transform);
                flashImage.raycastTarget = false;
                var flash = flashImage.gameObject.AddComponent<ScreenFlashView>();
                var flashSo = new SerializedObject(flash);
                flashSo.FindProperty("_image").objectReferenceValue = flashImage;
                flashSo.ApplyModifiedPropertiesWithoutUndo();

                // Springs MMF (canales posición/rotación — PrimeTween hace scale/alpha).
                // Bajo el canvas SIEMPRE activo, no bajo Root: PlayFeedbacks en un GO
                // inactivo es no-op. Frecuencias ≤8 Hz / damping ≤0.4 — PUL-015.
                var openPlayer = CreateSpringPlayer(root.transform, "Juice_Open", player =>
                {
                    var spring = (MMF_RotationSpring)player.AddFeedback(typeof(MMF_RotationSpring));
                    spring.Label = "Title Wobble";
                    spring.AnimateRotationTarget = title.transform;
                    spring.RotationSpace = Space.Self;
                    spring.Mode = MMF_RotationSpring.Modes.Bump;
                    spring.BumpRotationMin = new Vector3(0f, 0f, 300f);
                    spring.BumpRotationMax = new Vector3(0f, 0f, 500f);
                    spring.DampingZ = 0.35f;
                    spring.FrequencyZ = 7f;
                });
                var revealPlayer = CreateSpringPlayer(root.transform, "Juice_Reveal", player =>
                {
                    var spring = (MMF_PositionSpring)player.AddFeedback(typeof(MMF_PositionSpring));
                    spring.Label = "Reward Card Bump";
                    spring.AnimatePositionTarget = card.transform;
                    spring.Mode = MMF_PositionSpring.Modes.Bump;
                    spring.BumpPositionMin = new Vector3(0f, 14f, 0f);
                    spring.BumpPositionMax = new Vector3(0f, 22f, 0f);
                    spring.DampingY = 0.4f;
                    spring.FrequencyY = 7f;
                });

                // View + juice.
                var view = root.AddComponent<ChestRevealView>();
                var juice = root.AddComponent<ChestRevealJuice>();
                var so = new SerializedObject(view);
                so.FindProperty("_settings").objectReferenceValue = settings;
                so.FindProperty("_root").objectReferenceValue = viewRoot;
                so.FindProperty("_panelRect").objectReferenceValue = (RectTransform)panel.transform;
                so.FindProperty("_panelCanvasGroup").objectReferenceValue = panelGroup;
                so.FindProperty("_dimBackground").objectReferenceValue = dimButton;
                so.FindProperty("_viewport").objectReferenceValue = viewportRect;
                so.FindProperty("_stripRoot").objectReferenceValue = stripRect;
                so.FindProperty("_cellPrefab").objectReferenceValue = cellPrefab.GetComponent<ChestReelCellView>();
                so.FindProperty("_titleLabel").objectReferenceValue = title;
                so.FindProperty("_rewardNameLabel").objectReferenceValue = rewardName;
                so.FindProperty("_rewardCardGroup").objectReferenceValue = cardGroup;
                so.FindProperty("_hintLabel").objectReferenceValue = hint;
                so.FindProperty("_dimImage").objectReferenceValue = dim;
                so.FindProperty("_juice").objectReferenceValue = juice;
                so.ApplyModifiedPropertiesWithoutUndo();

                var juiceSo = new SerializedObject(juice);
                juiceSo.FindProperty("_settings").objectReferenceValue = settings;
                juiceSo.FindProperty("_flash").objectReferenceValue = flash;
                juiceSo.FindProperty("_burst").objectReferenceValue = burst;
                juiceSo.FindProperty("_shakeTarget").objectReferenceValue = (RectTransform)panel.transform;
                juiceSo.FindProperty("_pointer").objectReferenceValue = pointerRect;
                juiceSo.FindProperty("_titleTransform").objectReferenceValue = title.transform;
                juiceSo.FindProperty("_zoomTarget").objectReferenceValue = viewportRect;
                juiceSo.FindProperty("_cardPulseTarget").objectReferenceValue = cardRect;
                juiceSo.FindProperty("_hintLabel").objectReferenceValue = hint;
                juiceSo.FindProperty("_openPlayer").objectReferenceValue = openPlayer;
                juiceSo.FindProperty("_revealPlayer").objectReferenceValue = revealPlayer;
                juiceSo.FindProperty("_tickClip").objectReferenceValue = LoadClip(TickClipPath);
                juiceSo.FindProperty("_whooshClip").objectReferenceValue = LoadClip(WhooshClipPath);
                juiceSo.FindProperty("_landThunkClip").objectReferenceValue = LoadClip(LandThunkClipPath);
                juiceSo.FindProperty("_chimeClip").objectReferenceValue = LoadClip(ChimeClipPath);
                juiceSo.FindProperty("_countTickClip").objectReferenceValue = LoadClip(CountTickClipPath);
                juiceSo.FindProperty("_cardSlideClip").objectReferenceValue = LoadClip(CardSlideClipPath);
                juiceSo.FindProperty("_clickClip").objectReferenceValue = LoadClip(ClickClipPath);
                juiceSo.ApplyModifiedPropertiesWithoutUndo();

                viewRoot.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
                Debug.Log(LogPrefix + CanvasPrefabPath + " creado/actualizado.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- helpers ----------------------------------------------------

        private static ChestRevealUiSettingsSO LoadSettings() =>
            AssetDatabase.LoadAssetAtPath<ChestRevealUiSettingsSO>(SettingsPath);

        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                Debug.LogWarning(LogPrefix + path + " no existe — ese SFX queda sin wirear.");
            return clip;
        }

        private static MMF_Player CreateSpringPlayer(
            Transform parent, string name, System.Action<MMF_Player> author)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var player = go.AddComponent<MMF_Player>();
            author(player);
            return player;
        }

        private static RectTransform Stretch(GameObject go, Transform parent)
        {
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            if (size != Vector2.zero) rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text, float size, Vector2 pos, Vector2 area)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = area;
            rect.anchoredPosition = pos;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = TextColor;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            return label;
        }
    }
}
