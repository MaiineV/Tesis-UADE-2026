using System.IO;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Autora el prefab de una tarjeta del tooltip, la columna del panel y la banda de identidad.
    /// </summary>
    /// <remarks>
    /// Idempotente. Columna, banda y pie comparten un solo ancho: el que se salga del
    /// acuerdo decide él solo cuánto mide el tooltip.
    /// </remarks>
    public static class TooltipCardSetupTools
    {
        private const string CardPrefabPath = "Assets/Prefabs/UI/TooltipCard.prefab";
        private const string TooltipPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_Tooltip.prefab";

        private const string SheetGuid = "cca52ed63b2fdae4ca26627a5c6beed8";
        private const string ShieldIconGuid = "c2fddca92856efb4f8356cb0ce73e042";

        // NO la placa del panel: dos placas iguales anidadas se leen como un campo vacío.
        private const long CardPlateId = -78205987;   // UI-Sheet-sheet_11

        // Ficha oscura redonda: sin sprite, el Image del badge dibuja un cuadrado blanco.
        private const long BadgePlateId = -125824097; // UI-Sheet-sheet_2

        private const long HeartChipId = 1611900147;  // UI-Sheet-sheet_4

        // Un solo ancho para todo lo apilado: el pie es TMP con wrap y, sin ancho que lo ate,
        // su preferido estira el panel hasta su texto entero en un renglón.
        private const float ContentWidth = 350f;

        // Aspect 37:53 de los sub-sprites v3 (otra proporción los deforma), a 0.6x: chip
        // inline del alto del renglón del nombre.
        private static readonly Vector2 HealthBarSize = new Vector2(37f, 53f) * 0.6f;

        // Fijo, un paso abajo del nombre (31) para que la vida no le compita.
        private const float HealthBarFontSize = 29f;

        // El rect de un TMP guarda 15/90 del cuerpo para el descender: alineado por rects, el
        // número quedaba ~1px por encima de la línea de base del nombre. Puro ajuste óptico.
        private const int VitalsBaselineDrop = 1;

        // Más angosta que el panel: tarjetas del ancho de la caja pesan más que el bicho.
        private const float CardWidth = 300f;

        // Chico a propósito: más lejos deja de leerse como algo de ESTE bicho.
        private const float SideColumnGap = 14f;

        // La fila del pie entra de los bordes de la caja (left/right del rect): el marco
        // 9-slice del panel pinta más grueso a la izquierda y la fila lo pisaba al fijar
        // el tooltip.
        private const float BottomInsetLeft = 20f;
        private const float BottomInsetRight = 6f;

        // El aire interno del header: el tooltip entero se escala desde acá.
        private const int HeaderPadX = 26;
        private const int HeaderPadY = 22;

        // El párrafo del panel: este archivo no lo crea pero sí lo dimensiona.
        private const float ParagraphFont = 26f;


        /// <summary>
        /// Cuánto se mete la columna de tarjetas para adentro de cada lado: las placas 9-slice
        /// del header y de las tarjetas no dibujan sus marcos igual de gruesos. Ajuste óptico.
        /// </summary>
        private const int CardsInset = 8;

        // Chico: vive en la fila del label, al lado de "PLAYER CURSE", no encabezando el título.
        private const float IconSize = 26f;
        private const float BadgeSize = 22f;

        // La placa cuadrada de la fila de estados al pie del panel — sólo ícono.
        private const string SlotPrefabPath = "Assets/Prefabs/UI/TooltipStatusSlot.prefab";
        private const float SlotSize = 52f;
        private const float SlotIconPadding = 10f;

        // Crema: el blanco default de TMP se pierde sobre el panel hueso.
        private static readonly Color CardInk = new Color(0.94f, 0.90f, 0.82f);
        private static readonly Color DividerInk = new Color(0.83f, 0.68f, 0.33f, 0.45f);

        // Dorado pleno para el label del bloque — NEXT TURN, PLAYER CURSE.
        private static readonly Color LabelInk = new Color(0.87f, 0.72f, 0.38f);

        // El marrón del párrafo del panel, para que la banda pertenezca al mismo tooltip.
        private static readonly Color PanelInk = new Color(0.14f, 0.10f, 0.07f);
        private static readonly Color PanelInkSoft = new Color(0.14f, 0.10f, 0.07f, 0.72f);

        // Opaca y en otro tono que el nombre: etiqueta, no título; el alfa la lavaba en chico.
        private static readonly Color PanelInkFamily = new Color(0.36f, 0.26f, 0.17f);

        [MenuItem("Rollgeon/Tooltips/1 - Author Tooltip Card Prefab")]
        public static void AuthorCardPrefab()
        {
            var root = new GameObject("TooltipCard", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(CardWidth, 66f);

            var background = Ensure<Image>(root);
            background.sprite = LoadSlice(CardPlateId);
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            // Vertical: la regla recibe el ancho entero, legible con números en letra chica.
            var layout = Ensure<VerticalLayoutGroup>(root);
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 7;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Ensure<LayoutElement>(root).preferredWidth = CardWidth;

            // El label del bloque arriba de todo: se lee antes que el contenido.
            var labelRow = EnsureChildRect(rootRect, "LabelRow", Vector2.zero, Vector2.zero);
            var labelLayout = Ensure<HorizontalLayoutGroup>(labelRow.gameObject);
            labelLayout.spacing = 6;
            labelLayout.childAlignment = TextAnchor.MiddleLeft;
            labelLayout.childControlWidth = true;
            labelLayout.childControlHeight = true;
            labelLayout.childForceExpandWidth = false;
            labelLayout.childForceExpandHeight = false;

            var iconRect = EnsureChildRect(labelRow, "Icon", Vector2.zero, new Vector2(IconSize, IconSize));
            var iconLayout = Ensure<LayoutElement>(iconRect.gameObject);
            iconLayout.preferredWidth = IconSize;
            iconLayout.preferredHeight = IconSize;
            var iconImage = Ensure<Image>(iconRect.gameObject);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // El badge pisa la esquina del ícono, así que queda fuera del layout de la fila.
            var badgeRect = EnsureChildRect(iconRect, "Badge", Vector2.zero, new Vector2(BadgeSize, BadgeSize));
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            Ensure<LayoutElement>(badgeRect.gameObject).ignoreLayout = true;
            var badgeImage = Ensure<Image>(badgeRect.gameObject);
            badgeImage.sprite = LoadSlice(BadgePlateId);
            // Simple y no Sliced: la ficha es redonda y el 9-slice le comería el canto.
            badgeImage.type = Image.Type.Simple;
            badgeImage.preserveAspect = true;
            badgeImage.raycastTarget = false;
            var badgeLabel = EnsureLabel(badgeRect, "Value", 22f, TextAlignmentOptions.Center, CardInk);

            // Estirado: con el rect 0x0 de EnsureLabel, TMP parte el texto en un renglón por letra.
            badgeLabel.rectTransform.anchorMin = Vector2.zero;
            badgeLabel.rectTransform.anchorMax = Vector2.one;
            badgeLabel.rectTransform.sizeDelta = Vector2.zero;
            badgeLabel.rectTransform.anchoredPosition = Vector2.zero;
            badgeLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // Mayúsculas por estilo: ningún idioma tiene que autorar gritado.
            var eyebrowLabel = EnsureLabel(labelRow, "Eyebrow", 17f, TextAlignmentOptions.Left,
                                           LabelInk);
            eyebrowLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            Ensure<LayoutElement>(eyebrowLabel.gameObject).flexibleWidth = 1f;

            // Subraya el label del bloque; TooltipCardView lo prende sólo con contenido debajo.
            var dividerRect = EnsureChildRect(rootRect, "Divider", Vector2.zero, new Vector2(0f, 3f));
            Ensure<LayoutElement>(dividerRect.gameObject).preferredHeight = 3f;
            var dividerImage = Ensure<Image>(dividerRect.gameObject);
            dividerImage.color = DividerInk;
            dividerImage.raycastTarget = false;

            var headerRect = EnsureChildRect(rootRect, "Header", Vector2.zero, Vector2.zero);
            var headerLayout = Ensure<HorizontalLayoutGroup>(headerRect.gameObject);
            headerLayout.spacing = 8;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            // Mas chico que la regla a proposito: la regla es lo que se lee.
            var titleLabel = EnsureLabel(headerRect, "Title", 24f, TextAlignmentOptions.Left, CardInk);
            titleLabel.fontStyle = FontStyles.Bold;
            Ensure<LayoutElement>(titleLabel.gameObject).flexibleWidth = 1f;

            // Sin flexibleWidth: el título se queda con el sobrante y el número queda al borde.
            var damageLabel = EnsureLabel(headerRect, "Damage", 31f, TextAlignmentOptions.Right, CardInk);
            damageLabel.fontStyle = FontStyles.Bold;

            // Centrada: bajo un divisor se lee como regla, no como continuación del título.
            var ruleLabel = EnsureLabel(rootRect, "Rule", 25f, TextAlignmentOptions.Center, CardInk);
            ruleLabel.enableWordWrapping = true;

            var view = Ensure<TooltipCardView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("_titleLabel").objectReferenceValue = titleLabel;
            so.FindProperty("_eyebrowLabel").objectReferenceValue = eyebrowLabel;
            so.FindProperty("_ruleLabel").objectReferenceValue = ruleLabel;
            so.FindProperty("_iconRoot").objectReferenceValue = iconRect.gameObject;
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_badge").objectReferenceValue = badgeRect.gameObject;
            so.FindProperty("_badgeLabel").objectReferenceValue = badgeLabel;
            so.FindProperty("_damageLabel").objectReferenceValue = damageLabel;
            so.FindProperty("_divider").objectReferenceValue = dividerRect.gameObject;
            so.FindProperty("_labelRow").objectReferenceValue = labelRow.gameObject;
            so.FindProperty("_headerRow").objectReferenceValue = headerRect.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(CardPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TooltipCardSetupTools] Prefab de tarjeta autorado en {CardPrefabPath}.");
        }

        [MenuItem("Rollgeon/Tooltips/2 - Wire Card Column Into Tooltip Panel")]
        public static void WireCardColumn()
        {
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (cardPrefab == null)
            {
                Debug.LogError($"[TooltipCardSetupTools] Falta {CardPrefabPath}. Corré el paso 1 primero.");
                return;
            }

            EditPanel(panel =>
            {
                var cards = EnsureChildRect(panel, "Cards", Vector2.zero, Vector2.zero);

                var layout = Ensure<VerticalLayoutGroup>(cards.gameObject);
                layout.spacing = 8;
                layout.padding = new RectOffset(CardsInset, CardsInset, 0, 0);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                Ensure<LayoutElement>(cards.gameObject).preferredWidth = ContentWidth;

                return (so, _) =>
                {
                    so.FindProperty("_cardsContainer").objectReferenceValue = cards;
                    so.FindProperty("_cardPrefab").objectReferenceValue =
                        cardPrefab.GetComponent<TooltipCardView>();
                };
            });

            Debug.Log("[TooltipCardSetupTools] Columna de tarjetas cableada en el panel del tooltip.");
        }

        /// <summary>
        /// La segunda columna: los estados aplicados, al costado. Fuera del layout del panel
        /// (<c>ignoreLayout</c>): no recalibra ancho, colgado ni recorte a pantalla.
        /// </summary>
        [MenuItem("Rollgeon/Tooltips/4 - Wire Side Column")]
        public static void WireSideColumn()
        {
            EditPanel(panel =>
            {
                var side = EnsureChildRect(panel, "SideCards", Vector2.zero, Vector2.zero);

                Ensure<LayoutElement>(side.gameObject).ignoreLayout = true;
                side.anchorMin = new Vector2(1f, 1f);
                side.anchorMax = new Vector2(1f, 1f);
                side.pivot = new Vector2(0f, 1f);
                side.anchoredPosition = new Vector2(SideColumnGap, 0f);

                var layout = Ensure<VerticalLayoutGroup>(side.gameObject);
                layout.spacing = 8;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                // Fuera del layout, nada más la dimensiona.
                var fitter = Ensure<ContentSizeFitter>(side.gameObject);
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                return (so, _) =>
                {
                    so.FindProperty("_sideCardsContainer").objectReferenceValue = side;
                    side.SetAsLastSibling();
                };
            });

            Debug.Log("[TooltipCardSetupTools] Columna del costado cableada en el panel.");
        }

        /// <summary>
        /// El candado de fijado; nace apagado, lo prende <see cref="TooltipController.SetPinned"/>.
        /// </summary>
        [MenuItem("Rollgeon/Tooltips/6 - Wire Pin Indicator")]
        public static void WirePinIndicator()
        {
            var padlock = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Unlocks/Padlock.png");
            if (padlock == null)
            {
                Debug.LogError("[TooltipCardSetupTools] Falta Assets/Art/UI/Unlocks/Padlock.png " +
                               "(o no está importado como Sprite).");
                return;
            }

            EditPanel(panel =>
            {
                var pin = EnsureChildRect(panel, "PinIndicator", Vector2.zero, new Vector2(22f, 22f));

                // Fuera del layout: un indicador no puede reacomodar la caja.
                Ensure<LayoutElement>(pin.gameObject).ignoreLayout = true;
                pin.anchorMin = new Vector2(1f, 1f);
                pin.anchorMax = new Vector2(1f, 1f);
                pin.pivot = new Vector2(1f, 1f);
                pin.anchoredPosition = new Vector2(-6f, -6f);

                var image = Ensure<Image>(pin.gameObject);
                image.sprite = padlock;
                image.preserveAspect = true;
                image.raycastTarget = false;

                pin.gameObject.SetActive(false);

                return (so, _) =>
                {
                    so.FindProperty("_pinIndicator").objectReferenceValue = pin.gameObject;
                    pin.SetAsLastSibling();
                };
            });

            Debug.Log("[TooltipCardSetupTools] Candado de fijado cableado en el panel.");
        }

        [MenuItem("Rollgeon/Tooltips/5 - Wire Bottom Cards")]
        public static void WireBottomCards()
        {
            EditPanel(panel =>
            {
                var bottom = EnsureChildRect(panel, "BottomCards",
                    new Vector2(0f, -SideColumnGap), Vector2.zero);

                // Anclada a los dos bordes: mide lo que mida el panel. ignoreLayout por lo
                // mismo que el costado: lo que cuelga no reacomoda la caja.
                Ensure<LayoutElement>(bottom.gameObject).ignoreLayout = true;
                bottom.anchorMin = new Vector2(0f, 0f);
                bottom.anchorMax = new Vector2(1f, 0f);
                bottom.pivot = new Vector2(0.5f, 1f);
                bottom.sizeDelta = new Vector2(0f, 0f);
                bottom.anchoredPosition = new Vector2(0f, -SideColumnGap);
                bottom.offsetMin = new Vector2(BottomInsetLeft, bottom.offsetMin.y);
                bottom.offsetMax = new Vector2(-BottomInsetRight, bottom.offsetMax.y);

                // Dos layout groups en el mismo GO pelean: un VerticalLayoutGroup previo se
                // tira antes de asegurar el horizontal.
                var stale = bottom.GetComponent<VerticalLayoutGroup>();
                if (stale != null) Object.DestroyImmediate(stale, true);

                var layout = Ensure<HorizontalLayoutGroup>(bottom.gameObject);
                layout.spacing = 8;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                // Solo el alto: el ancho lo dan los anchors (el de la caja) y lo impone el layout.
                var fitter = Ensure<ContentSizeFitter>(bottom.gameObject);
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                return (so, _) =>
                {
                    so.FindProperty("_bottomCardsContainer").objectReferenceValue = bottom;
                    bottom.SetAsLastSibling();
                };
            });

            Debug.Log("[TooltipCardSetupTools] Fila de estados de abajo cableada en el panel.");
        }

        /// <summary>
        /// La placa cuadrada de la fila de estados: sólo ícono y badge; el detalle vive en el juego.
        /// </summary>
        [MenuItem("Rollgeon/Tooltips/7 - Author Status Slot Prefab And Wire It")]
        public static void AuthorStatusSlotPrefab()
        {
            var root = new GameObject("TooltipStatusSlot", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(SlotSize, SlotSize);

            var background = Ensure<Image>(root);
            background.sprite = LoadSlice(CardPlateId);
            background.type = Image.Type.Sliced;
            // La ÚNICA superficie del tooltip que intercepta el mouse: el hover de la placa
            // abre la burbuja de detalle (TooltipStatusSlotHover). Icon/Badge quedan en false.
            background.raycastTarget = true;

            var element = Ensure<LayoutElement>(root);
            element.preferredWidth = SlotSize;
            element.preferredHeight = SlotSize;

            // Sin layout group: un slot es una placa y un sprite, no una fila.
            var iconRect = EnsureChildRect(rootRect, "Icon", Vector2.zero, Vector2.zero);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(SlotIconPadding, SlotIconPadding);
            iconRect.offsetMax = new Vector2(-SlotIconPadding, -SlotIconPadding);
            var iconImage = Ensure<Image>(iconRect.gameObject);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var badgeRect = EnsureChildRect(iconRect, "Badge", Vector2.zero,
                                            new Vector2(BadgeSize, BadgeSize));
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            var badgeImage = Ensure<Image>(badgeRect.gameObject);
            badgeImage.sprite = LoadSlice(BadgePlateId);
            badgeImage.type = Image.Type.Simple;
            badgeImage.preserveAspect = true;
            badgeImage.raycastTarget = false;
            var badgeLabel = EnsureLabel(badgeRect, "Value", 16f, TextAlignmentOptions.Center,
                                         CardInk);
            badgeLabel.rectTransform.anchorMin = Vector2.zero;
            badgeLabel.rectTransform.anchorMax = Vector2.one;
            badgeLabel.rectTransform.sizeDelta = Vector2.zero;
            badgeLabel.rectTransform.anchoredPosition = Vector2.zero;
            badgeLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // Sin labels a propósito: TooltipCardView null-guardea cada pieza.
            var view = Ensure<TooltipCardView>(root);
            Ensure<TooltipStatusSlotHover>(root);
            var so = new SerializedObject(view);
            so.FindProperty("_iconRoot").objectReferenceValue = iconRect.gameObject;
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_badge").objectReferenceValue = badgeRect.gameObject;
            so.FindProperty("_badgeLabel").objectReferenceValue = badgeLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(SlotPrefabPath));
            var saved = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
            Object.DestroyImmediate(root);

            EditPanel(panel => (controllerSo, unused) =>
            {
                controllerSo.FindProperty("_bottomCardPrefab").objectReferenceValue =
                    saved.GetComponent<TooltipCardView>();
            });
            AssetDatabase.SaveAssets();

            Debug.Log($"[TooltipCardSetupTools] Slot de estado autorado en {SlotPrefabPath} y " +
                      "cableado como prefab de la fila de abajo.");
        }

        /// <summary>
        /// La placa envuelve SOLO el header; las tarjetas cuelgan debajo como cajas propias y
        /// el panel raíz queda transparente: es un apilador, no una caja.
        /// </summary>
        [MenuItem("Rollgeon/Tooltips/8 - Split The Header Box")]
        public static void SplitHeaderBox()
        {
            EditPanel(panel =>
            {
                bool creating = panel.Find("HeaderBox") == null;
                var header = EnsureChildRect(panel, "HeaderBox", Vector2.zero, Vector2.zero);

                var panelLayout = Ensure<VerticalLayoutGroup>(panel.gameObject);
                var headerLayout = Ensure<VerticalLayoutGroup>(header.gameObject);

                if (creating)
                {
                    // La placa del panel se muda ENTERA a la caja del header. Sólo en el
                    // primer corte: después de esto el panel ya no tiene placa que mudar.
                    var panelImage = panel.GetComponent<Image>();
                    var headerImage = Ensure<Image>(header.gameObject);
                    if (panelImage != null)
                    {
                        headerImage.sprite = panelImage.sprite;
                        headerImage.type = panelImage.type;
                        headerImage.color = panelImage.color;
                        Object.DestroyImmediate(panelImage, true);
                    }
                    headerImage.raycastTarget = false;

                    headerLayout.spacing = panelLayout.spacing;
                    headerLayout.childAlignment = TextAnchor.UpperCenter;
                    headerLayout.childControlWidth = true;
                    headerLayout.childControlHeight = true;
                    headerLayout.childForceExpandWidth = true;
                    headerLayout.childForceExpandHeight = false;

                    panelLayout.padding = new RectOffset(0, 0, 0, 0);
                    // El aire ENTRE cajas, más grande que el interno: las hace leerse separadas.
                    panelLayout.spacing = 10f;
                }

                // Fuera del alta: re-correr el menú puede cambiar el tamaño de la caja.
                headerLayout.padding = new RectOffset(HeaderPadX, HeaderPadX,
                                                      HeaderPadY, HeaderPadY);

                // Idempotente: si ya viven en el header, Reparent no hace nada.
                Reparent(panel, "Identity", header);
                Reparent(panel, "Text", header);
                Reparent(panel, "Footer", header);

                return (so, p) =>
                {
                    header.SetSiblingIndex(0);
                    var cards = p.Find("Cards");
                    if (cards != null) cards.SetSiblingIndex(1);

                    var identity = header.Find("Identity");
                    if (identity != null) identity.SetAsFirstSibling();
                    var text = header.Find("Text");
                    if (text != null) text.SetSiblingIndex(1);
                    var footer = header.Find("Footer");
                    if (footer != null) footer.SetAsLastSibling();
                };
            });

            Debug.Log("[TooltipCardSetupTools] Header partido: la placa envuelve sólo la " +
                      "identidad y la frase; las tarjetas cuelgan afuera como cajas propias.");
        }

        private const string CurseAssetPath = "Assets/Rollgeon/Enemies/BC_Croupier_DiceBlock.asset";
        private const string CroupierPath = "Assets/Rollgeon/Enemies/ED_Boss_Croupier.asset";
        private const string PadlockPath = "Assets/Art/UI/Unlocks/Padlock.png";

        /// <summary>La maldición del Croupier (PLAYER CURSE), colgada en la data del jefe.</summary>
        [MenuItem("Rollgeon/Tooltips/9 - Author Croupier Curse Asset")]
        public static void AuthorCroupierCurse()
        {
            var data = AssetDatabase.LoadAssetAtPath<Rollgeon.Entities.EnemyDataSO>(CroupierPath);
            if (data == null)
            {
                Debug.LogError($"[TooltipCardSetupTools] Falta {CroupierPath}.");
                return;
            }

            var curse = AssetDatabase.LoadAssetAtPath<Rollgeon.Entities.BossCurseSO>(CurseAssetPath);
            if (curse != null && curse is not Rollgeon.Entities.DiceBlockCurseSO)
            {
                // El tipo cambió: se recrea. El guid nuevo no rompe nada porque este mismo
                // menú lo vuelve a colgar en la data del jefe.
                AssetDatabase.DeleteAsset(CurseAssetPath);
                curse = null;
            }

            if (curse == null)
            {
                var fresh = ScriptableObject.CreateInstance<Rollgeon.Entities.DiceBlockCurseSO>();
                // Mutado ANTES de CreateAsset: crear sobre un path recién borrado pierde las
                // mutaciones posteriores.
                Fill(fresh);
                AssetDatabase.CreateAsset(fresh, CurseAssetPath);
                curse = fresh;
            }
            else
            {
                Fill(curse);
                EditorUtility.SetDirty(curse);
            }

            static void Fill(Rollgeon.Entities.BossCurseSO target)
            {
                target.CurseId = "status.dice_block";
                target.DisplayName = "Candado de dados";
                target.Description = "Te traba un dado.";
                target.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(PadlockPath);
            }

            // SerializedObject y no asignación directa: escribe donde Unity lee, sin tocar
            // los SerializationNodes de Odin.
            var so = new SerializedObject(data);
            var prop = so.FindProperty("Curse");
            if (prop == null)
            {
                Debug.LogError("[TooltipCardSetupTools] ED_Boss_Croupier no expone 'Curse' a " +
                               "SerializedObject.");
                return;
            }
            prop.objectReferenceValue = curse;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TooltipCardSetupTools] Curse autorado en {CurseAssetPath} y colgado en " +
                      "ED_Boss_Croupier.Curse.");
        }

        private const string CajeroCursePath = "Assets/Rollgeon/Enemies/BC_Cajero_BankKeeps.asset";
        private const string CajeroPath = "Assets/Rollgeon/Enemies/ED_Boss_Cajero.asset";
        private const string CoinPath = "Assets/Art/UI/Inventory/CoinStyle1.1.png";
        private const string GeneralaCursePath = "Assets/Rollgeon/Enemies/BC_Generala_RepeatBan.asset";
        private const string GeneralaPath = "Assets/Rollgeon/Enemies/ED_Boss_Generala.asset";

        /// <summary>
        /// Las maldiciones del Cajero (siempre activa) y de la Generala (gateada por contrato).
        /// </summary>
        [MenuItem("Rollgeon/Tooltips/10 - Author Cajero And Generala Curses")]
        public static void AuthorCajeroAndGeneralaCurses()
        {
            AuthorCurse<Rollgeon.Entities.BossCurseSO>(CajeroCursePath, CajeroPath, curse =>
            {
                curse.CurseId = "curse.bank_keeps";
                curse.DisplayName = "La banca retiene";
                curse.Description = "El oro que dejás vencer se lo queda la banca.";
                curse.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(CoinPath);
            });

            AuthorCurse<Rollgeon.Entities.RepeatBanCurseSO>(GeneralaCursePath, GeneralaPath, curse =>
            {
                curse.CurseId = "curse.repeat_ban";
                curse.DisplayName = "Mano vetada";
                curse.Description = "No podés repetir el combo que acabás de anotar.";
                // Sin ícono hasta que haya arte: la tarjeta es label + regla igual.
            });
        }

        private static void AuthorCurse<T>(string cursePath, string bossPath,
                                           System.Action<Rollgeon.Entities.BossCurseSO> fill)
            where T : Rollgeon.Entities.BossCurseSO
        {
            var data = AssetDatabase.LoadAssetAtPath<Rollgeon.Entities.EnemyDataSO>(bossPath);
            if (data == null)
            {
                Debug.LogError($"[TooltipCardSetupTools] Falta {bossPath}.");
                return;
            }

            var curse = AssetDatabase.LoadAssetAtPath<Rollgeon.Entities.BossCurseSO>(cursePath);
            if (curse != null && curse.GetType() != typeof(T))
            {
                // El tipo define el gate (IsActive): si cambió, se recrea; el menú lo recuelga.
                AssetDatabase.DeleteAsset(cursePath);
                curse = null;
            }

            if (curse == null)
            {
                var fresh = ScriptableObject.CreateInstance<T>();
                // Mutado ANTES de CreateAsset: crear sobre un path recién borrado pierde las
                // mutaciones posteriores.
                fill(fresh);
                AssetDatabase.CreateAsset(fresh, cursePath);
                curse = fresh;
            }
            else
            {
                fill(curse);
                EditorUtility.SetDirty(curse);
            }

            var so = new SerializedObject(data);
            var prop = so.FindProperty("Curse");
            if (prop == null)
            {
                Debug.LogError($"[TooltipCardSetupTools] {bossPath} no expone 'Curse' a " +
                               "SerializedObject.");
                return;
            }
            prop.objectReferenceValue = curse;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TooltipCardSetupTools] Curse autorado en {cursePath} y colgado en " +
                      $"{System.IO.Path.GetFileNameWithoutExtension(bossPath)}.Curse.");
        }

        private const string MimicPrefabPath = "Assets/Prefabs/Enemies/ChestMimic_Prefab.prefab";

        /// <summary>
        /// Collider trigger en el mímico REVELADO: sin collider, AttachTooltip lo saltea y pelea
        /// sin panel. Trigger y no sólido para no molestar el pick de celda; el camuflado recibe
        /// su hover por ChestService, como el cofre real.
        /// </summary>
        [MenuItem("Rollgeon/Tooltips/11 - Wire Mimic Reveal Collider")]
        public static void WireMimicRevealCollider()
        {
            var root = PrefabUtility.LoadPrefabContents(MimicPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[TooltipCardSetupTools] Falta {MimicPrefabPath}.");
                return;
            }

            try
            {
                if (root.GetComponentInChildren<Collider>(true) != null)
                {
                    Debug.Log("[TooltipCardSetupTools] El mímico ya tiene collider — nada que hacer.");
                    return;
                }

                var box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;

                // Dimensionado al arte cuando computa; si no, una caja de pawn parado.
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    box.center = root.transform.InverseTransformPoint(bounds.center);
                    box.size = bounds.size;
                }
                else
                {
                    box.center = new Vector3(0f, 0.5f, 0f);
                    box.size = new Vector3(0.9f, 1f, 0.9f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, MimicPrefabPath);
                Debug.Log("[TooltipCardSetupTools] Collider trigger agregado al mímico revelado: " +
                          "su panel de enemigo se cuelga solo en SpawnEnemy.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Rollgeon/Tooltips/3 - Wire Identity Band And Footer")]
        public static void WireIdentityBand()
        {
            EditPanel(panel =>
            {
                // Con el header partido (menú 8), la identidad y el pie viven adentro de la
                // caja del header; sin partir, directo en el panel como siempre.
                var host = panel.Find("HeaderBox") as RectTransform ?? panel;
                var identity = EnsureChildRect(host, "Identity", Vector2.zero, Vector2.zero);
                var identityLayout = Ensure<VerticalLayoutGroup>(identity.gameObject);
                identityLayout.spacing = 4;
                identityLayout.childAlignment = TextAnchor.UpperCenter;
                identityLayout.childControlWidth = true;
                identityLayout.childControlHeight = true;
                identityLayout.childForceExpandWidth = true;
                identityLayout.childForceExpandHeight = false;
                Ensure<LayoutElement>(identity.gameObject).preferredWidth = ContentWidth;

                // Alineados abajo: la vida comparte la línea de base del nombre.
                var titleRow = EnsureChildRect(identity, "TitleRow", Vector2.zero, Vector2.zero);
                var titleRowLayout = Ensure<HorizontalLayoutGroup>(titleRow.gameObject);
                titleRowLayout.spacing = 12f;
                titleRowLayout.childAlignment = TextAnchor.LowerLeft;
                titleRowLayout.childControlWidth = true;
                titleRowLayout.childControlHeight = true;
                titleRowLayout.childForceExpandWidth = false;
                titleRowLayout.childForceExpandHeight = false;

                // En un prefab viejo los hijos pueden vivir en otro nivel; sin moverlos, los
                // Ensure* crearían un segundo juego. Type baja del renglón del nombre.
                Reparent(identity, "Name", titleRow);
                Reparent(titleRow, "Type", identity);

                var nameLabel = EnsureLabel(titleRow, "Name", 31f, TextAlignmentOptions.Left, PanelInk);
                nameLabel.fontStyle = FontStyles.Bold;
                nameLabel.textWrappingMode = TextWrappingModes.NoWrap;

                // La familia, en su propio renglón debajo del nombre.
                var typeLabel = EnsureLabel(identity, "Type", 22f, TextAlignmentOptions.Left,
                                            PanelInkFamily);
                typeLabel.textWrappingMode = TextWrappingModes.NoWrap;
                // Sin ancho propio: un preferredWidth fijo impondría el ancho del panel entero.
                Ensure<LayoutElement>(typeLabel.gameObject).preferredWidth = -1f;

                // El spacer flexible empuja la vida al borde derecho del renglón.
                var spacer = EnsureChildRect(titleRow, "Spacer", Vector2.zero, Vector2.zero);
                Ensure<LayoutElement>(spacer.gameObject).flexibleWidth = 1f;

                Reparent(identity, "Vitals", titleRow);
                var vitals = EnsureChildRect(titleRow, "Vitals", Vector2.zero, Vector2.zero);
                var vitalsLayout = Ensure<HorizontalLayoutGroup>(vitals.gameObject);
                vitalsLayout.spacing = 10;
                vitalsLayout.childAlignment = TextAnchor.MiddleLeft;
                // Padding negativo: el grupo cuelga por debajo del renglón (VitalsBaselineDrop).
                vitalsLayout.padding = new RectOffset(0, 0, 0, -VitalsBaselineDrop);
                vitalsLayout.childControlWidth = true;
                vitalsLayout.childControlHeight = true;
                vitalsLayout.childForceExpandWidth = false;
                vitalsLayout.childForceExpandHeight = false;

                // La vida es LITERAL la pila de la cabeza (BossVisualWrapperBuilder.BuildHealthBar):
                // mismos sprites y mismo orden de capas.
                var heart = vitals.Find("HeartIcon");
                if (heart != null) Object.DestroyImmediate(heart.gameObject);

                var barSprites = BossVisualWrapperBuilder.LoadHealthBarSprites();
                var barRect = EnsureChildRect(vitals, "HealthBar", Vector2.zero, HealthBarSize);
                var barElement = Ensure<LayoutElement>(barRect.gameObject);
                barElement.preferredWidth = HealthBarSize.x;
                barElement.preferredHeight = HealthBarSize.y;

                var lifeBackground = EnsureStretchedImage(barRect, "LifeBackground", barSprites.fill);
                lifeBackground.color = BossVisualWrapperBuilder.HealthBarBackgroundTint;
                var lifeFill = EnsureStretchedImage(barRect, "LifeFill", barSprites.fill);
                lifeFill.type = Image.Type.Filled;
                lifeFill.fillMethod = Image.FillMethod.Vertical;
                lifeFill.fillOrigin = (int)Image.OriginVertical.Bottom;
                lifeFill.fillAmount = 1f;
                var lifeFrame = EnsureStretchedImage(barRect, "Frame", barSprites.frame);
                lifeBackground.transform.SetSiblingIndex(0);
                lifeFill.transform.SetSiblingIndex(1);
                lifeFrame.transform.SetSiblingIndex(2);

                // El número afuera de la pila: el par entra entero sin achicarse en el ícono.
                Reparent(barRect, "Hp", vitals);
                var hpLabel = EnsureLabel(vitals, "Hp", HealthBarFontSize,
                                          TextAlignmentOptions.Right, PanelInk);
                hpLabel.fontStyle = FontStyles.Bold;
                hpLabel.enableAutoSizing = false;
                hpLabel.textWrappingMode = TextWrappingModes.NoWrap;
                var hpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    BossVisualWrapperBuilder.HealthBarFontPath);
                if (hpFont != null) hpLabel.font = hpFont;
                hpLabel.transform.SetSiblingIndex(0);
                barRect.SetSiblingIndex(1);

                var shield = EnsureChildRect(vitals, "Shield", Vector2.zero, Vector2.zero);
                var shieldLayout = Ensure<HorizontalLayoutGroup>(shield.gameObject);
                shieldLayout.spacing = 5;
                shieldLayout.childAlignment = TextAnchor.MiddleCenter;
                shieldLayout.childControlWidth = true;
                shieldLayout.childControlHeight = true;
                shieldLayout.childForceExpandWidth = false;
                shieldLayout.childForceExpandHeight = false;
                EnsureIcon(shield, "ShieldIcon", LoadFirstSprite(ShieldIconGuid), new Vector2(34f, 34f));
                var shieldLabel = EnsureLabel(shield, "Value", 34f, TextAlignmentOptions.Left, PanelInk);
                shieldLabel.fontStyle = FontStyles.Bold;

                var footer = EnsureLabel(host, "Footer", 24f, TextAlignmentOptions.Center, PanelInkSoft);
                footer.enableWordWrapping = true;
                Ensure<LayoutElement>(footer.gameObject).preferredWidth = ContentWidth;

                // El párrafo no se crea acá pero sí se dimensiona, y al mismo ancho que el
                // resto: con uno propio decidiría él cuánto mide el panel.
                var paragraph = host.Find("Text")?.GetComponent<TextMeshProUGUI>();
                if (paragraph != null)
                {
                    paragraph.fontSize = ParagraphFont;
                    Ensure<LayoutElement>(paragraph.gameObject).preferredWidth = ContentWidth;

                    // A la izquierda: arranca en la misma vertical que el nombre.
                    paragraph.alignment = TextAlignmentOptions.TopLeft;
                }

                return (so, p) =>
                {
                    so.FindProperty("_nameLabel").objectReferenceValue = nameLabel;
                    so.FindProperty("_typeLabel").objectReferenceValue = typeLabel;
                    so.FindProperty("_vitalsRoot").objectReferenceValue = vitals.gameObject;
                    so.FindProperty("_hpLabel").objectReferenceValue = hpLabel;
                    so.FindProperty("_hpFill").objectReferenceValue = lifeFill;
                    so.FindProperty("_shieldRoot").objectReferenceValue = shield.gameObject;
                    so.FindProperty("_shieldLabel").objectReferenceValue = shieldLabel;
                    so.FindProperty("_footerLabel").objectReferenceValue = footer;

                    // Orden explícito porque Ensure* agrega al final.
                    titleRow.SetSiblingIndex(0);
                    typeLabel.transform.SetSiblingIndex(1);
                    nameLabel.transform.SetSiblingIndex(0);
                    spacer.SetSiblingIndex(1);
                    vitals.SetSiblingIndex(2);

                    // Lo que necesitás mientras peleás va primero.
                    identity.SetAsFirstSibling();
                    footer.transform.SetAsLastSibling();
                    OrderMiddle(p);
                };
            });

            Debug.Log("[TooltipCardSetupTools] Banda de identidad y pie cableados en el panel.");
        }

        // Párrafo antes que la columna, con o sin el header partido.
        private static void OrderMiddle(RectTransform panel)
        {
            var header = panel.Find("HeaderBox");
            if (header != null)
            {
                header.SetSiblingIndex(0);
                var outerCards = panel.Find("Cards");
                if (outerCards != null) outerCards.SetSiblingIndex(1);

                var innerText = header.Find("Text");
                if (innerText != null) innerText.SetSiblingIndex(1);
                return;
            }

            var text = panel.Find("Text");
            var cards = panel.Find("Cards");
            if (text != null) text.SetSiblingIndex(1);
            if (cards != null) cards.SetSiblingIndex(text != null ? 2 : 1);
        }

        /// <summary>
        /// Abre el prefab del tooltip, deja que <paramref name="build"/> arme la jerarquía, y
        /// aplica lo que devuelve sobre el <see cref="TooltipController"/> ya encontrado.
        /// </summary>
        private static void EditPanel(
            System.Func<RectTransform, System.Action<SerializedObject, RectTransform>> build)
        {
            var contents = PrefabUtility.LoadPrefabContents(TooltipPrefabPath);
            try
            {
                var controller = contents.GetComponentInChildren<TooltipController>(includeInactive: true);
                if (controller == null)
                {
                    Debug.LogError($"[TooltipCardSetupTools] {TooltipPrefabPath} no tiene TooltipController.");
                    return;
                }

                var so = new SerializedObject(controller);
                var panel = (RectTransform)so.FindProperty("_root").objectReferenceValue;
                if (panel == null)
                {
                    Debug.LogError("[TooltipCardSetupTools] El TooltipController no tiene _root cableado.");
                    return;
                }

                var apply = build(panel);
                apply?.Invoke(so, panel);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, TooltipPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Image EnsureIcon(RectTransform parent, string name, Sprite sprite, Vector2 size)
        {
            var rect = EnsureChildRect(parent, name, Vector2.zero, size);
            var element = Ensure<LayoutElement>(rect.gameObject);
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            var image = Ensure<Image>(rect.gameObject);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Capa de la pila estirada al rect del padre: las tres calzan pixel-perfect.</summary>
        private static Image EnsureStretchedImage(RectTransform parent, string name, Sprite sprite)
        {
            var rect = EnsureChildRect(parent, name, Vector2.zero, Vector2.zero);
            Stretch(rect);
            var image = Ensure<Image>(rect.gameObject);
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite LoadSlice(long internalId)
        {
            var path = AssetDatabase.GUIDToAssetPath(SheetGuid);
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Sprite sprite) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long id)
                    && id == internalId) return sprite;
            }
            return null;
        }

        private static Sprite LoadFirstSprite(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;
            return null;
        }

        private static TextMeshProUGUI EnsureLabel(RectTransform parent, string name, float size,
                                                   TextAlignmentOptions alignment, Color ink)
        {
            var rect = EnsureChildRect(parent, name, Vector2.zero, Vector2.zero);
            var label = Ensure<TextMeshProUGUI>(rect.gameObject);
            label.fontSize = size;
            label.alignment = alignment;
            label.color = ink;
            label.raycastTarget = false;
            return label;
        }

        private static void Reparent(RectTransform parent, string child, RectTransform newParent)
        {
            var found = parent.Find(child);
            if (found != null && found.parent != newParent)
                found.SetParent(newParent, worldPositionStays: false);
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

        private static T Ensure<T>(RectTransform rect) where T : Component => Ensure<T>(rect.gameObject);
    }
}
