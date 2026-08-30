using NUnit.Framework;
using TMPro;
using Rollgeon.UI.Tooltips;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Editor.Tools.HUD.Tests
{
    /// <summary>
    /// De qué sale el ancho del panel del tooltip.
    /// </summary>
    /// <remarks>
    /// El panel se dimensiona por su contenido (ContentSizeFitter sobre un VerticalLayoutGroup),
    /// así que el hijo más ancho manda. Un TMP con wrap y sin ancho propio reporta como preferido
    /// el del texto entero en UN renglón — es el ancho que hay que atar, no el que se mide.
    /// </remarks>
    [TestFixture]
    public sealed class TooltipPanelLayoutTests
    {
        private const string TooltipPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_Tooltip.prefab";
        private const string CardPrefabPath = "Assets/Prefabs/UI/TooltipCard.prefab";

        [Test]
        public void ElPieNoDecideElAnchoDelPanel()
        {
            var panel = LoadPanel();

            Assert.AreEqual(WidthOf(panel, "Cards"), WidthOf(panel, "Footer"), 0.5f,
                "El pie no tiene ancho propio, y es un TMP con wrap: su ancho preferido es el " +
                "del color del bicho ENTERO en un renglón. Con eso, la frase de sabor terminaba " +
                "decidiendo cuánto mide el tooltip.");
        }

        [Test]
        public void LaBandaDeIdentidadMideLoMismoQueLaColumna()
        {
            var panel = LoadPanel();

            Assert.AreEqual(WidthOf(panel, "Cards"), WidthOf(panel, "Identity"), 0.5f,
                "El nombre y los vitales se leen centrados sobre la columna: si la banda mide " +
                "otra cosa, el panel se ensancha por ella y el nombre queda descentrado de las " +
                "tarjetas que describe.");
        }

        [Test]
        public void LaColumnaDelCostadoNoPuedeEnsancharElPanel()
        {
            var panel = LoadPanel();

            var side = panel.Find("SideCards");
            Assert.IsNotNull(side, "El panel del tooltip no tiene 'SideCards'.");

            var element = side.GetComponent<LayoutElement>();
            Assert.IsNotNull(element, "'SideCards' no tiene LayoutElement.");
            Assert.IsTrue(element.ignoreLayout,
                "La columna del costado entró al layout del panel: desde ahí su ancho se suma al " +
                "del panel, y aturdir a un enemigo vuelve a mover todo lo que ya se calibró.");
        }

        [Test]
        public void LaFamiliaVaDebajoDelNombre()
        {
            var panel = LoadPanel();

            // El mockup del spec: "The Croupier" arriba y "Boss · Ranged" en su propio renglón,
            // debajo. Si esto falla con 'Identity/Name' ausente, falta re-correr el menú
            // Rollgeon/Tooltips/3 sobre el prefab.
            var name = panel.Find("Identity/Name");
            var type = panel.Find("Identity/Type");
            Assert.IsNotNull(name, "El panel no tiene 'Identity/Name' como renglón propio.");
            Assert.IsNotNull(type,
                "La familia no está debajo del nombre: 'Boss · Ranged' va en su propio renglón.");
            Assert.Greater(type.GetSiblingIndex(), name.GetSiblingIndex(),
                "La familia quedó ARRIBA del nombre: se lee 'Boss · Ranged' antes que " +
                "'The Croupier'.");

            var element = type.GetComponent<LayoutElement>();
            if (element != null)
                Assert.Less(element.preferredWidth, 0f,
                    "El Type conserva un preferredWidth fijo: le impondría su ancho al panel " +
                    "entero en vez de dejar que el layout lo estire.");
        }

        [Test]
        public void ElTituloDeLaTarjetaNoLeGanaALaRegla()
        {
            var card = LoadCard();

            Assert.Less(FontSizeOf(card, "Header/Title"), FontSizeOf(card, "Rule"),
                "El título nombra la cosa y la regla es lo que se lee. Con el título más grande " +
                "que ella se lleva el ojo primero, y la tarjeta pasa a ser un encabezado con una " +
                "nota al pie.");
        }

        [Test]
        public void LaTarjeta_EsLabelDivisorContenido()
        {
            // El mockup: el label del bloque arriba (ícono + NEXT TURN/PLAYER CURSE), el divisor
            // subrayándolo, y recién después el contenido. Si falta 'LabelRow', falta re-correr
            // los menús Rollgeon/Tooltips/1 y 2.
            var card = LoadCard();

            var label = card.Find("LabelRow");
            var divider = card.Find("Divider");
            var header = card.Find("Header");
            var rule = card.Find("Rule");
            Assert.IsNotNull(label, "La tarjeta no tiene 'LabelRow'.");
            Assert.IsNotNull(divider, "La tarjeta no tiene 'Divider'.");
            Assert.IsNotNull(header, "La tarjeta no tiene 'Header'.");
            Assert.IsNotNull(rule, "La tarjeta no tiene 'Rule'.");
            Assert.IsNotNull(card.Find("LabelRow/Eyebrow"),
                "El eyebrow no vive en la fila del label.");
            Assert.IsNotNull(card.Find("LabelRow/Icon"),
                "El ícono no vive en la fila del label — es el candado al lado de PLAYER CURSE.");

            Assert.Less(label.GetSiblingIndex(), divider.GetSiblingIndex(),
                "El divisor quedó arriba del label: tiene que subrayarlo.");
            Assert.Less(divider.GetSiblingIndex(), header.GetSiblingIndex(),
                "El contenido quedó arriba del divisor.");
            Assert.Less(header.GetSiblingIndex(), rule.GetSiblingIndex(),
                "La regla quedó arriba de la fila del título.");
        }

        [Test]
        public void LaFilaDeAbajo_EsUnaFilaYTieneSuPropioSlot()
        {
            var panel = LoadPanel();

            // Slots cuadrados en horizontal, no una pila de tarjetas. Si esto falla, falta
            // re-correr los menús Rollgeon/Tooltips/5 y 7.
            var bottom = panel.Find("BottomCards");
            Assert.IsNotNull(bottom, "El panel no tiene 'BottomCards'.");
            Assert.IsNotNull(bottom.GetComponent<HorizontalLayoutGroup>(),
                "La fila de abajo no es horizontal: los estados saldrían apilados como tarjetas.");
            Assert.IsNull(bottom.GetComponent<VerticalLayoutGroup>(),
                "Quedó el VerticalLayoutGroup viejo conviviendo con la fila: dos layout groups " +
                "en el mismo GO pelean por los hijos.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            var controller = prefab.GetComponentInChildren<TooltipController>(includeInactive: true);
            var slot = new SerializedObject(controller)
                .FindProperty("_bottomCardPrefab").objectReferenceValue;
            Assert.IsNotNull(slot,
                "El panel no tiene cableado el prefab del slot de estado: la fila de abajo " +
                "saldría con las tarjetas de texto.");
        }

        private static float FontSizeOf(Transform card, string child)
        {
            var found = card.Find(child);
            Assert.IsNotNull(found, $"La tarjeta del tooltip no tiene '{child}'.");

            var label = found.GetComponent<TMP_Text>();
            Assert.IsNotNull(label, $"'{child}' no es un TMP_Text.");
            return label.fontSize;
        }

        private static Transform LoadCard()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.IsNotNull(prefab, $"Falta {CardPrefabPath}.");
            return prefab.transform;
        }

        private static float WidthOf(Transform panel, string child)
        {
            var found = panel.Find(child);
            Assert.IsNotNull(found, $"El panel del tooltip no tiene '{child}'.");

            var element = found.GetComponent<LayoutElement>();
            Assert.IsNotNull(element, $"'{child}' no tiene LayoutElement, así que su ancho lo " +
                                      "decide su propio contenido.");
            return element.preferredWidth;
        }

        private static Transform LoadPanel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            Assert.IsNotNull(prefab, $"Falta {TooltipPrefabPath}.");

            var controller = prefab.GetComponentInChildren<TooltipController>(includeInactive: true);
            Assert.IsNotNull(controller, "El prefab del tooltip no tiene TooltipController.");

            var root = new SerializedObject(controller)
                .FindProperty("_root").objectReferenceValue as RectTransform;
            Assert.IsNotNull(root, "El TooltipController no tiene _root cableado.");
            return root;
        }
    }
}
