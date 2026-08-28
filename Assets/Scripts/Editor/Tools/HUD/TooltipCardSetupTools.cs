using System.IO;
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
    /// Idempotente: correrlo dos veces deja el mismo resultado. La columna, la banda y el pie
    /// comparten un solo ancho: el panel se dimensiona por su hijo más ancho, así que el que se
    /// salga del acuerdo decide él solo cuánto mide el tooltip.
    /// </remarks>
    public static class TooltipCardSetupTools
    {
        private const string CardPrefabPath = "Assets/Prefabs/UI/TooltipCard.prefab";
        private const string TooltipPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_Tooltip.prefab";

        private const string SheetGuid = "cca52ed63b2fdae4ca26627a5c6beed8";
        private const string ShieldIconGuid = "c2fddca92856efb4f8356cb0ce73e042";

        // La tarjeta NO usa la placa del panel: dos placas iguales, una adentro de la otra, se
        // leen como un campo vacío y no como una tarjeta. Esta es la azul de marco dorado, la
        // misma familia que las fichas de acción y la barra del jefe.
        private const long CardPlateId = -78205987;   // UI-Sheet-sheet_11

        // Ficha oscura redonda: sin sprite, el Image del badge dibuja un cuadrado blanco.
        private const long BadgePlateId = -125824097; // UI-Sheet-sheet_2

        private const long HeartChipId = 1611900147;  // UI-Sheet-sheet_4

        // Un solo ancho para todo lo que va apilado en el panel. El pie es TMP con wrap: sin un
        // ancho que lo ate, su preferido es el del texto ENTERO en un renglón, y el panel se
        // estiraba hasta ahí — el color del bicho decidía el ancho del tooltip.
        private const float ContentWidth = 330f;

        // Aire entre el panel y la columna del costado. Chico a propósito: más lejos y deja de
        // leerse como algo de ESTE bicho.
        private const float SideColumnGap = 16f;
        private const float IconSize = 44f;
        private const float BadgeSize = 34f;

        // Crema sobre la placa oscura. Los labels salían en el blanco default de TMP, que sobre
        // el panel color hueso quedaba invisible.
        private static readonly Color CardInk = new Color(0.94f, 0.90f, 0.82f);
        private static readonly Color CardInkSoft = new Color(0.94f, 0.90f, 0.82f, 0.65f);
        private static readonly Color DividerInk = new Color(0.83f, 0.68f, 0.33f, 0.45f);

        // El marrón del párrafo del panel, para que la banda pertenezca al mismo tooltip.
        private static readonly Color PanelInk = new Color(0.14f, 0.10f, 0.07f);
        private static readonly Color PanelInkSoft = new Color(0.14f, 0.10f, 0.07f, 0.72f);

        [MenuItem("Rollgeon/Tooltips/1 - Author Tooltip Card Prefab")]
        public static void AuthorCardPrefab()
        {
            var root = new GameObject("TooltipCard", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(ContentWidth, 64f);

            var background = Ensure<Image>(root);
            background.sprite = LoadSlice(CardPlateId);
            background.type = Image.Type.Sliced;
            background.raycastTarget = false;

            // Vertical y no horizontal: la referencia pone el ícono chico en la línea del título
            // y le da a la regla el ancho entero, que es lo que hace legible una frase con
            // números adentro a 13px.
            var layout = Ensure<VerticalLayoutGroup>(root);
            layout.padding = new RectOffset(17, 17, 13, 13);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Ensure<LayoutElement>(root).preferredWidth = ContentWidth;

            var headerRect = EnsureChildRect(rootRect, "Header", Vector2.zero, Vector2.zero);
            var headerLayout = Ensure<HorizontalLayoutGroup>(headerRect.gameObject);
            headerLayout.spacing = 11;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            var iconRect = EnsureChildRect(headerRect, "Icon", Vector2.zero, new Vector2(IconSize, IconSize));
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

            // Estirado a la ficha, y no el 0x0 que deja EnsureLabel: con un rect sin ancho TMP
            // acomoda el texto contra el pivote y parte cualquier cosa de más de un carácter en un
            // renglón por letra. Es lo que hacía que el badge se leyera vertical al lado del ícono.
            badgeLabel.rectTransform.anchorMin = Vector2.zero;
            badgeLabel.rectTransform.anchorMax = Vector2.one;
            badgeLabel.rectTransform.sizeDelta = Vector2.zero;
            badgeLabel.rectTransform.anchoredPosition = Vector2.zero;
            badgeLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // Chico y apagado a proposito: es una fecha, no un segundo titulo. Y arriba de todo
            // porque dice cuando pasa lo que la tarjeta describe -- se lee antes que el que.
            var eyebrowLabel = EnsureLabel(rootRect, "Eyebrow", 18f, TextAlignmentOptions.Center,
                                           CardInkSoft);
            eyebrowLabel.transform.SetSiblingIndex(0);

            // Por debajo de la regla (27) a proposito: el titulo nombra la cosa y la regla es
            // lo que se lee, asi que un titulo mas grande que ella se lleva el ojo primero.
            var titleLabel = EnsureLabel(headerRect, "Title", 26f, TextAlignmentOptions.Left, CardInk);
            titleLabel.fontStyle = FontStyles.Bold;
            Ensure<LayoutElement>(titleLabel.gameObject).flexibleWidth = 1f;

            // Último de la fila y sin flexibleWidth: el título se queda con el sobrante, así que
            // el número termina pegado al borde derecho sin un spacer de por medio.
            var damageLabel = EnsureLabel(headerRect, "Damage", 34f, TextAlignmentOptions.Right, CardInk);
            damageLabel.fontStyle = FontStyles.Bold;

            var dividerRect = EnsureChildRect(rootRect, "Divider", Vector2.zero, new Vector2(0f, 3f));
            Ensure<LayoutElement>(dividerRect.gameObject).preferredHeight = 3f;
            var dividerImage = Ensure<Image>(dividerRect.gameObject);
            dividerImage.color = DividerInk;
            dividerImage.raycastTarget = false;

            // Centrada como la referencia: bajo un divisor, una frase de dos renglones centrada
            // se lee como una regla y no como la continuación del título.
            var ruleLabel = EnsureLabel(rootRect, "Rule", 27f, TextAlignmentOptions.Center, CardInk);
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
                layout.spacing = 10;
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
        /// La segunda columna: los estados que le aplicaste, al costado y no debajo.
        /// </summary>
        /// <remarks>
        /// <b>Fuera del layout del panel</b> (<c>ignoreLayout</c>) y anclada a su esquina superior
        /// derecha. Es la decisión que mantiene el riesgo en cero: el panel sigue midiendo lo que
        /// mide su columna de arriba, así que <see cref="ContentWidth"/>, el punto del que cuelga y
        /// el recorte contra el borde de la pantalla siguen siendo exactamente los que se
        /// calibraron. Un panel horizontal habría vuelto a poner las tres cosas en juego.
        /// <para>
        /// Lo que sí queda afuera: el recorte a pantalla mide el panel y no la columna, así que
        /// pegado al borde derecho el costado puede irse de la pantalla. Es el mismo problema que
        /// el modo Beside ya resuelve colgando del otro lado, y se arregla ahí el día que aparezca.
        /// </para>
        /// </remarks>
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
                layout.spacing = 10;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                // Propio porque nada más la dimensiona: está fuera del layout del panel, así que
                // sin esto queda del tamaño que tenía el RectTransform al crearse.
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

        [MenuItem("Rollgeon/Tooltips/3 - Wire Identity Band And Footer")]
        public static void WireIdentityBand()
        {
            EditPanel(panel =>
            {
                var identity = EnsureChildRect(panel, "Identity", Vector2.zero, Vector2.zero);
                var identityLayout = Ensure<VerticalLayoutGroup>(identity.gameObject);
                identityLayout.spacing = 5;
                identityLayout.childAlignment = TextAnchor.UpperCenter;
                identityLayout.childControlWidth = true;
                identityLayout.childControlHeight = true;
                identityLayout.childForceExpandWidth = true;
                identityLayout.childForceExpandHeight = false;
                Ensure<LayoutElement>(identity.gameObject).preferredWidth = ContentWidth;

                var nameLabel = EnsureLabel(identity, "Name", 44f, TextAlignmentOptions.Center, PanelInk);
                nameLabel.fontStyle = FontStyles.Bold;

                // Pegada al nombre y no al título: las dos son identidad. Y chica, porque el hijo
                // más ancho decide cuánto mide el panel — una familia larga adentro del título es
                // lo que lo ensancha.
                var typeLabel = EnsureLabel(identity, "Type", 26f, TextAlignmentOptions.Center, PanelInkSoft);
                Ensure<LayoutElement>(typeLabel.gameObject).preferredWidth = ContentWidth;

                var vitals = EnsureChildRect(identity, "Vitals", Vector2.zero, Vector2.zero);
                var vitalsLayout = Ensure<HorizontalLayoutGroup>(vitals.gameObject);
                vitalsLayout.spacing = 10;
                vitalsLayout.childAlignment = TextAnchor.MiddleCenter;
                vitalsLayout.childControlWidth = true;
                vitalsLayout.childControlHeight = true;
                vitalsLayout.childForceExpandWidth = false;
                vitalsLayout.childForceExpandHeight = false;

                EnsureIcon(vitals, "HeartIcon", LoadSlice(HeartChipId), new Vector2(56f, 38f));
                var hpLabel = EnsureLabel(vitals, "Hp", 34f, TextAlignmentOptions.Left, PanelInk);
                hpLabel.fontStyle = FontStyles.Bold;

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

                var footer = EnsureLabel(panel, "Footer", 24f, TextAlignmentOptions.Center, PanelInkSoft);
                footer.enableWordWrapping = true;
                Ensure<LayoutElement>(footer.gameObject).preferredWidth = ContentWidth;

                return (so, p) =>
                {
                    so.FindProperty("_nameLabel").objectReferenceValue = nameLabel;
                    so.FindProperty("_typeLabel").objectReferenceValue = typeLabel;
                    so.FindProperty("_vitalsRoot").objectReferenceValue = vitals.gameObject;
                    so.FindProperty("_hpLabel").objectReferenceValue = hpLabel;
                    so.FindProperty("_shieldRoot").objectReferenceValue = shield.gameObject;
                    so.FindProperty("_shieldLabel").objectReferenceValue = shieldLabel;
                    so.FindProperty("_footerLabel").objectReferenceValue = footer;

                    // La familia va pegada al nombre y los vitales después: las dos primeras son
                    // identidad, la tercera es estado. Explícito porque Ensure* agrega al final, y
                    // en un panel que ya tenía Vitals la fila nueva caería debajo de los números.
                    typeLabel.transform.SetSiblingIndex(1);

                    // Identidad arriba, párrafo y columna en el medio, color al pie. El orden es
                    // el contenido del tooltip: lo que necesitás mientras peleás va primero.
                    identity.SetAsFirstSibling();
                    footer.transform.SetAsLastSibling();
                    OrderMiddle(p);
                };
            });

            Debug.Log("[TooltipCardSetupTools] Banda de identidad y pie cableados en el panel.");
        }

        // Párrafo antes que la columna: el párrafo es de los tooltips de texto y no convive con
        // las tarjetas, pero si algún día conviven el orden ya está decidido.
        private static void OrderMiddle(RectTransform panel)
        {
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
