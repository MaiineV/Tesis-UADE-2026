using System.Collections.Generic;
using System.Linq;
using Rollgeon.UI.HUD.Contract;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del drawer de contrato in-game: crea el settings con las manos de ejemplo,
    /// los prefabs del dado y de la fila, arma el panel deslizante dentro de
    /// <c>Canvas_PlayerStatus</c> y hace clickeable el ícono de contrato.
    /// Idempotente — reejecutar actualiza sin duplicar.
    /// </summary>
    /// <remarks>
    /// La pantalla de selección de clase (<c>ContractDisplayView</c>, 01_MainMenu) no se
    /// toca acá, pero desde su rework comparte esta fila vía <see cref="BuildRowPrefab"/>:
    /// su installer genera una variante con columna de descripción en otro prefab.
    /// </remarks>
    public static class ContractDrawerSetupTools
    {
        private const string SettingsPath = "Assets/Rollgeon/Services/ContractSheetUiSettings.asset";
        private const string DiePrefabPath = "Assets/Prefabs/UI/ContractDie.prefab";
        private const string RowPrefabPath = "Assets/Prefabs/UI/ContractComboRow.prefab";
        private const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";

        private const string UiSheetPath = "Assets/Art/UI/UI-sheet.png";
        private const string DiceSheetPath = "Assets/Art/UI/Dice/d6.png";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";

        private const string PanelSlice = "UI-sheet_5";
        private const string RowSlice = "UI-sheet_9";
        private const string DiceAreaSlice = "UI-sheet_10";
        private const string DamageBoxSlice = "UI-sheet_11";
        private const string DieFrameSlice = "d6_6";

        // Layout en px de referencia (1920x1080).
        //
        // El drawer es una cheat sheet para jugar CON ella abierta, así que ocupa el tercio
        // izquierdo y nada más. Los dados van a escala x2 exacta de su arte (22x22 la cara,
        // 24x24 el marco): a escala no entera el pixel art queda con pixeles desparejos.
        private const int MaxRows = 9;
        private static readonly Vector2 DieSize = new Vector2(44f, 44f);
        private static readonly Vector2 DieFrameSize = new Vector2(48f, 48f);
        private const float DiceSpacing = 4f;
        private const float DiceAreaPadding = 10f;
        private static readonly Vector2 DiceAreaSize = new Vector2(
            5f * DieSize.x + 4f * DiceSpacing + 2f * DiceAreaPadding, 56f);
        // Ajustada al nombre más largo ("Número Mayor") con el auto-size del label; lo que
        // sobraba acá era ancho muerto en un panel que tiene que estorbar lo menos posible.
        private const float NameWidth = 150f;
        private static readonly Vector2 DamageBoxSize = new Vector2(66f, 56f);

        private const float RowPadding = 10f;
        private const float RowHeight = 60f;
        private const float RowSpacing = 4f;
        private static readonly Vector2 RowSize = new Vector2(
            RowPadding + DiceAreaSize.x + RowPadding + NameWidth + 6f + DamageBoxSize.x + RowPadding,
            RowHeight);

        // Columnas, medidas desde el borde izquierdo de la fila.
        private const float DiceAreaX = RowPadding;
        private static readonly float NameX = RowPadding + DiceAreaSize.x + RowPadding;
        private static readonly float DamageX = NameX + NameWidth + 6f;

        // 44 y no 32: "Daño base" entra en dos líneas sobre una columna de 66 px.
        private const float HeaderHeight = 44f;
        private const float PanelPadding = 12f;
        // Se deriva del contenido para que las filas NO se salgan de la caja.
        private static readonly Vector2 PanelSize = new Vector2(
            RowSize.x + 2f * PanelPadding,
            PanelPadding + HeaderHeight + 6f + MaxRows * RowHeight + (MaxRows - 1) * RowSpacing + PanelPadding);
        private const float PanelOpenX = 24f;
        private static readonly float PanelClosedX = -(PanelSize.x + 40f);

        // Colgado del borde superior y no centrado: sale del ícono de contrato (que termina
        // en -128) y así el panel no baja hasta las pilas de fichas, que viven abajo a la
        // izquierda. Cuesta tapar la mochila y la bolsa mientras está abierto.
        private const float PanelTopY = -136f;

        [MenuItem("Rollgeon/Contract Drawer/Setup All")]
        public static void SetupAll()
        {
            CreateSettings();
            CreateDiePrefab();
            CreateRowPrefab();
            SetupDrawer();
        }

        // ================================================================
        // 1 - Settings: arte de los dados y manos de ejemplo
        // ================================================================

        [MenuItem("Rollgeon/Contract Drawer/1 - Create Settings")]
        public static void CreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ContractSheetUiSettingsSO>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ContractSheetUiSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            // d6_0..d6_5 son las caras 1..6; d6_6 es el marco.
            var faces = new Sprite[6];
            for (int i = 0; i < 6; i++)
            {
                faces[i] = LoadSpriteOrError(DiceSheetPath, $"d6_{i}");
                if (faces[i] == null) return;
            }
            var frame = LoadSpriteOrError(DiceSheetPath, DieFrameSlice);
            if (frame == null) return;

            settings.SetArt(faces, frame);
            settings.SetHands(BuildDefaultHands());

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ContractDrawer] Settings actualizado: {SettingsPath}");
        }

        /// <summary>
        /// Manos de ejemplo por defecto. Entre corchetes, los dados que forman el combo —
        /// esos llevan marco y el resto va opaco.
        /// </summary>
        /// <remarks>
        /// Reejecutar el paso 1 las PISA. Si diseño las retoca a mano en el asset, hay que
        /// traer el cambio acá o se pierde en el próximo setup.
        /// </remarks>
        private static List<ComboExampleHand> BuildDefaultHands() => new List<ComboExampleHand>
        {
            Hand("combo.higher_number", new[] { 2, 3, 3, 4, 6 }, 4),
            Hand("combo.pair",          new[] { 5, 5, 2, 3, 4 }, 0, 1),
            Hand("combo.double_pair",   new[] { 2, 2, 5, 5, 3 }, 0, 1, 2, 3),
            Hand("combo.trio",          new[] { 6, 6, 6, 2, 3 }, 0, 1, 2),
            // Fuerza Bruta suma la mitad superior: se resaltan los >= 4.
            Hand("combo.brute_force",   new[] { 4, 5, 6, 2, 3 }, 0, 1, 2),
            Hand("combo.full_house",    new[] { 3, 3, 3, 5, 5 }, 0, 1, 2, 3, 4),
            Hand("combo.ladder",        new[] { 1, 2, 3, 4, 5 }, 0, 1, 2, 3, 4),
            Hand("combo.poker",         new[] { 2, 2, 2, 2, 5 }, 0, 1, 2, 3),
            Hand("combo.generala",      new[] { 6, 6, 6, 6, 6 }, 0, 1, 2, 3, 4),
        };

        private static ComboExampleHand Hand(string comboId, int[] faces, params int[] highlighted)
        {
            var mask = new bool[faces.Length];
            foreach (int i in highlighted)
                if (i >= 0 && i < mask.Length) mask[i] = true;

            return new ComboExampleHand { ComboId = comboId, Faces = faces, Highlighted = mask };
        }

        // ================================================================
        // 2 - Prefab del dado
        // ================================================================

        [MenuItem("Rollgeon/Contract Drawer/2 - Create Die Prefab")]
        public static void CreateDiePrefab()
        {
            RebuildPrefab(DiePrefabPath, "ContractDie", root =>
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = DieSize;

                // El marco va primero (queda detrás) y es 4 px más grande que la cara — la
                // misma diferencia que tienen sus sprites (24 vs 22, a escala x2).
                var frame = EnsureChildRect(rootRect, "Frame", Vector2.zero, DieFrameSize);
                frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
                frame.pivot = new Vector2(0.5f, 0.5f);
                frame.anchoredPosition = Vector2.zero;
                frame.sizeDelta = DieFrameSize;
                var frameImage = Ensure<Image>(frame.gameObject);
                frameImage.preserveAspect = true;
                frameImage.raycastTarget = false;

                var face = EnsureChildRect(rootRect, "Face", Vector2.zero, DieSize);
                face.anchorMin = face.anchorMax = new Vector2(0.5f, 0.5f);
                face.pivot = new Vector2(0.5f, 0.5f);
                face.anchoredPosition = Vector2.zero;
                face.sizeDelta = DieSize;
                var faceImage = Ensure<Image>(face.gameObject);
                faceImage.preserveAspect = true;
                faceImage.raycastTarget = false;

                var view = Ensure<ContractDieView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_face").objectReferenceValue = faceImage;
                so.FindProperty("_frame").objectReferenceValue = frameImage;
                so.ApplyModifiedPropertiesWithoutUndo();

                var layout = Ensure<LayoutElement>(root);
                layout.preferredWidth = DieSize.x;
                layout.preferredHeight = DieSize.y;
            });
        }

        // ================================================================
        // 3 - Prefab de la fila
        // ================================================================

        [MenuItem("Rollgeon/Contract Drawer/3 - Create Row Prefab")]
        public static void CreateRowPrefab() => BuildRowPrefab(RowPrefabPath, descriptionWidth: 0f);

        /// <summary>
        /// Construye un prefab de fila de contrato en <paramref name="path"/>. Con
        /// <paramref name="descriptionWidth"/> &gt; 0 agrega la columna de descripción a la
        /// derecha de la caja de daño (variante de selección de clase); con 0 la fila queda
        /// idéntica a la del drawer in-game.
        /// </summary>
        internal static void BuildRowPrefab(string path, float descriptionWidth)
        {
            var rowSprite = LoadSpriteOrError(UiSheetPath, RowSlice);
            var diceAreaSprite = LoadSpriteOrError(UiSheetPath, DiceAreaSlice);
            var damageBoxSprite = LoadSpriteOrError(UiSheetPath, DamageBoxSlice);
            var diePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiePrefabPath);
            if (rowSprite == null || diceAreaSprite == null || damageBoxSprite == null) return;
            if (diePrefab == null)
            {
                Debug.LogError("[ContractDrawer] Correr primero '2 - Create Die Prefab'.");
                return;
            }
            var dieView = diePrefab.GetComponent<ContractDieView>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            bool withDescription = descriptionWidth > 0f;
            var rowSize = withDescription
                ? new Vector2(RowSize.x + 8f + descriptionWidth, RowSize.y)
                : RowSize;

            RebuildPrefab(path, System.IO.Path.GetFileNameWithoutExtension(path), root =>
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = rowSize;

                var bg = Ensure<Image>(root);
                bg.sprite = rowSprite;
                bg.type = Image.Type.Sliced;
                bg.raycastTarget = false;

                // -- Zona de dados --
                var diceArea = EnsureChildRect(rootRect, "DiceArea", new Vector2(DiceAreaX, 0f), DiceAreaSize);
                AnchorLeftMiddle(diceArea, new Vector2(DiceAreaX, 0f), DiceAreaSize);
                var diceBg = Ensure<Image>(diceArea.gameObject);
                diceBg.sprite = diceAreaSprite;
                diceBg.type = Image.Type.Sliced;
                diceBg.raycastTarget = false;

                var diceRow = EnsureChildRect(diceArea, "Dice", Vector2.zero, Vector2.zero);
                Stretch(diceRow, DiceAreaPadding);
                var diceLayout = Ensure<HorizontalLayoutGroup>(diceRow.gameObject);
                diceLayout.spacing = DiceSpacing;
                diceLayout.childAlignment = TextAnchor.MiddleCenter;
                diceLayout.childControlWidth = false;
                diceLayout.childControlHeight = false;
                diceLayout.childForceExpandWidth = false;
                diceLayout.childForceExpandHeight = false;

                // -- Nombre --
                var nameSize = new Vector2(NameWidth, 44f);
                var nameRect = EnsureChildRect(rootRect, "Name", new Vector2(NameX, 0f), nameSize);
                AnchorLeftMiddle(nameRect, new Vector2(NameX, 0f), nameSize);
                var nameLabel = EnsureLabel(nameRect.gameObject, font, 24f, TextAlignmentOptions.Left);
                // Los nombres largos (Fuerza Bruta, Doble Par) se achican antes que
                // desbordar la columna hacia la caja del daño.
                nameLabel.enableAutoSizing = true;
                nameLabel.fontSizeMin = 16f;
                nameLabel.fontSizeMax = 24f;

                // -- Daño base --
                var damageBox = EnsureChildRect(rootRect, "DamageBox", new Vector2(DamageX, 0f), DamageBoxSize);
                AnchorLeftMiddle(damageBox, new Vector2(DamageX, 0f), DamageBoxSize);
                var damageBg = Ensure<Image>(damageBox.gameObject);
                damageBg.sprite = damageBoxSprite;
                damageBg.type = Image.Type.Sliced;
                damageBg.raycastTarget = false;

                var damageRect = EnsureChildRect(damageBox, "Value", Vector2.zero, Vector2.zero);
                Stretch(damageRect, 0f);
                var damageLabel = EnsureLabel(damageRect.gameObject, font, 28f, TextAlignmentOptions.Center);

                // -- Descripción (solo la variante de selección de clase) --
                TextMeshProUGUI descriptionLabel = null;
                if (withDescription)
                {
                    float descriptionX = DamageX + DamageBoxSize.x + 8f;
                    var descriptionSize = new Vector2(descriptionWidth, 48f);
                    var descriptionRect = EnsureChildRect(rootRect, "Description",
                        new Vector2(descriptionX, 0f), descriptionSize);
                    AnchorLeftMiddle(descriptionRect, new Vector2(descriptionX, 0f), descriptionSize);
                    descriptionLabel = EnsureLabel(descriptionRect.gameObject, font, 17f,
                        TextAlignmentOptions.MidlineLeft);
                    descriptionLabel.enableAutoSizing = true;
                    descriptionLabel.fontSizeMin = 13f;
                    descriptionLabel.fontSizeMax = 17f;
                    descriptionLabel.textWrappingMode = TextWrappingModes.Normal;
                    descriptionLabel.overflowMode = TextOverflowModes.Ellipsis;
                    // Tinta oscura: la fila (UI-sheet_9) es naranja y el gris del ComboRow
                    // legacy no contrasta sobre ella.
                    descriptionLabel.color = new Color32(0x3A, 0x2E, 0x24, 0xFF);
                }

                var view = Ensure<ContractComboRowView>(root);
                var so = new SerializedObject(view);
                so.FindProperty("_diceContainer").objectReferenceValue = diceRow;
                so.FindProperty("_diePrefab").objectReferenceValue = dieView;
                so.FindProperty("_nameLabel").objectReferenceValue = nameLabel;
                so.FindProperty("_damageLabel").objectReferenceValue = damageLabel;
                so.FindProperty("_descriptionLabel").objectReferenceValue = descriptionLabel;
                so.ApplyModifiedPropertiesWithoutUndo();

                var layout = Ensure<LayoutElement>(root);
                layout.preferredWidth = rowSize.x;
                layout.preferredHeight = rowSize.y;
            });
        }

        // ================================================================
        // 4 - Drawer dentro de Canvas_PlayerStatus + ícono clickeable
        // ================================================================

        [MenuItem("Rollgeon/Contract Drawer/4 - Setup Drawer")]
        public static void SetupDrawer()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ContractSheetUiSettingsSO>(SettingsPath);
            var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
            var panelSprite = LoadSpriteOrError(UiSheetPath, PanelSlice);
            if (settings == null || rowPrefab == null || panelSprite == null)
            {
                Debug.LogError("[ContractDrawer] Faltan pasos previos (settings / row prefab / slice).");
                return;
            }
            var rowView = rowPrefab.GetComponent<ContractComboRowView>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            try
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                {
                    Debug.LogError("[ContractDrawer] Canvas no encontrado en Canvas_PlayerStatus.");
                    return;
                }
                var canvasRect = (RectTransform)canvas.transform;

                var drawerRect = EnsureChildRect(canvasRect, "ContractDrawer", Vector2.zero, Vector2.zero);
                Stretch(drawerRect, 0f);
                // Primero en la jerarquía del canvas dibujaría DEBAJO del cluster de íconos;
                // lo mandamos al final para que el panel tape el HUD mientras está abierto.
                drawerRect.SetAsLastSibling();

                // -- Backdrop: captura el click de afuera --
                var backdropRect = EnsureChildRect(drawerRect, "Backdrop", Vector2.zero, Vector2.zero);
                Stretch(backdropRect, 0f);
                var backdropImage = Ensure<Image>(backdropRect.gameObject);
                // Casi transparente y no negro: no queremos oscurecer (no es modal), pero un
                // alpha 0 exacto deja de recibir raycasts en algunas versiones de uGUI.
                backdropImage.color = new Color(0f, 0f, 0f, 0.01f);
                backdropImage.raycastTarget = true;
                var backdropButton = Ensure<Button>(backdropRect.gameObject);
                backdropButton.transition = Selectable.Transition.None;
                backdropRect.gameObject.SetActive(false);

                // -- Panel --
                var panel = EnsureChildRect(drawerRect, "Panel", new Vector2(PanelClosedX, PanelTopY), PanelSize);
                panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
                panel.pivot = new Vector2(0f, 1f);
                panel.anchoredPosition = new Vector2(PanelClosedX, PanelTopY);
                panel.sizeDelta = PanelSize;
                var panelImage = Ensure<Image>(panel.gameObject);
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Sliced;
                // El panel come los clicks que le caen encima; si no, atravesarían al tablero.
                panelImage.raycastTarget = true;

                EnsureHeader(panel, font, out var exampleHeader, out var nameHeader, out var damageHeader);

                float rowsY = -(PanelPadding + HeaderHeight + 6f);
                var rowsRect = EnsureChildRect(panel, "Rows", new Vector2(0f, rowsY), new Vector2(RowSize.x, 0f));
                rowsRect.anchorMin = new Vector2(0.5f, 1f);
                rowsRect.anchorMax = new Vector2(0.5f, 1f);
                rowsRect.pivot = new Vector2(0.5f, 1f);
                rowsRect.anchoredPosition = new Vector2(0f, rowsY);
                var rowsLayout = Ensure<VerticalLayoutGroup>(rowsRect.gameObject);
                rowsLayout.spacing = RowSpacing;
                rowsLayout.childAlignment = TextAnchor.UpperCenter;
                rowsLayout.childControlWidth = false;
                rowsLayout.childControlHeight = false;
                rowsLayout.childForceExpandWidth = false;
                rowsLayout.childForceExpandHeight = false;
                var rowsFitter = Ensure<ContentSizeFitter>(rowsRect.gameObject);
                rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // El gesto (deslizar, backdrop, Esc) vive en SlidingDrawer; la view solo
                // pone el contenido y se engancha a su evento Opened.
                var slider = Ensure<Rollgeon.UI.HUD.SlidingDrawer>(drawerRect.gameObject);
                var sliderSo = new SerializedObject(slider);
                sliderSo.FindProperty("_panel").objectReferenceValue = panel;
                sliderSo.FindProperty("_backdrop").objectReferenceValue = backdropButton;
                sliderSo.FindProperty("_closedX").floatValue = PanelClosedX;
                sliderSo.FindProperty("_openX").floatValue = PanelOpenX;
                sliderSo.ApplyModifiedPropertiesWithoutUndo();

                var drawer = Ensure<ContractDrawerView>(drawerRect.gameObject);
                var so = new SerializedObject(drawer);
                so.FindProperty("_rowsContainer").objectReferenceValue = rowsRect;
                so.FindProperty("_rowPrefab").objectReferenceValue = rowView;
                so.FindProperty("_settings").objectReferenceValue = settings;
                so.FindProperty("_exampleHeader").objectReferenceValue = exampleHeader;
                so.FindProperty("_nameHeader").objectReferenceValue = nameHeader;
                so.FindProperty("_damageHeader").objectReferenceValue = damageHeader;
                so.ApplyModifiedPropertiesWithoutUndo();

                WireContractIcon(root, slider);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
                Debug.Log("[ContractDrawer] Drawer armado en Canvas_PlayerStatus.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // El ícono se creó como Image decorativa (raycastTarget off). Para abrir el drawer
        // necesita recibir clicks y un Button, y el onClick se persiste al prefab.
        private static void WireContractIcon(GameObject root, Rollgeon.UI.HUD.SlidingDrawer drawer)
        {
            var icon = FindDeep(root.transform, "ContractIcon");
            if (icon == null)
            {
                Debug.LogWarning("[ContractDrawer] ContractIcon no encontrado — correr antes " +
                                 "'Rollgeon/Player Icons/Setup All'. El drawer queda sin disparador.");
                return;
            }

            if (icon.TryGetComponent<Image>(out var image)) image.raycastTarget = true;

            var button = Ensure<Button>(icon.gameObject);
            button.transition = Selectable.Transition.None;

            // Persistente y no AddListener: el listener runtime no sobrevive al prefab.
            // Se limpia primero para que reejecutar no apile toggles (abriría y cerraría).
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddVoidPersistentListener(button.onClick, drawer.Toggle);
        }

        // El texto que se autora acá es el FALLBACK. Lo definitivo lo pone
        // ContractDrawerView desde la tabla UI, y se re-resuelve al cambiar de idioma.
        private static void EnsureHeader(RectTransform panel, TMP_FontAsset font,
            out TextMeshProUGUI example, out TextMeshProUGUI name, out TextMeshProUGUI damage)
        {
            var header = EnsureChildRect(panel, "Header", new Vector2(0f, -PanelPadding),
                new Vector2(RowSize.x, HeaderHeight));
            header.anchorMin = header.anchorMax = new Vector2(0.5f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = new Vector2(0f, -PanelPadding);
            header.sizeDelta = new Vector2(RowSize.x, HeaderHeight);

            example = EnsureHeaderLabel(header, "ExampleHeader", DiceAreaX, DiceAreaSize.x,
                "Ejemplo", font, TextAlignmentOptions.Center);
            name = EnsureHeaderLabel(header, "NameHeader", NameX, NameWidth,
                "Combo", font, TextAlignmentOptions.Center);
            damage = EnsureHeaderLabel(header, "DamageHeader", DamageX, DamageBoxSize.x,
                "Daño base", font, TextAlignmentOptions.Center);
            // "Daño base" no entra de una línea en 66 px: se parte en dos, como el mock.
            damage.textWrappingMode = TextWrappingModes.Normal;
            damage.fontSize = 16f;
        }

        private static TextMeshProUGUI EnsureHeaderLabel(RectTransform header, string name, float x, float width,
            string text, TMP_FontAsset font, TextAlignmentOptions alignment)
        {
            var size = new Vector2(width, HeaderHeight);
            var rect = EnsureChildRect(header, name, new Vector2(x, 0f), size);
            AnchorLeftMiddle(rect, new Vector2(x, 0f), size);
            var label = EnsureLabel(rect.gameObject, font, 20f, alignment);
            label.text = text;
            return label;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static void RebuildPrefab(string path, string rootName, System.Action<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    build(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                Debug.Log($"[ContractDrawer] Prefab actualizado: {path}");
                return;
            }

            var go = new GameObject(rootName, typeof(RectTransform));
            try
            {
                build(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[ContractDrawer] Prefab creado: {path}");
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static TextMeshProUGUI EnsureLabel(GameObject go, TMP_FontAsset font, float size,
            TextAlignmentOptions alignment)
        {
            var label = Ensure<TextMeshProUGUI>(go);
            if (font != null) label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void AnchorLeftMiddle(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static Sprite LoadSpriteOrError(string assetPath, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError($"[ContractDrawer] Slice '{spriteName}' no encontrado en {assetPath}.");
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
