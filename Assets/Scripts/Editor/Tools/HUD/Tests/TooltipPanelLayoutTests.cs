using NUnit.Framework;
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
        public void LaFilaDeFamiliaNoDecideElAnchoDelPanel()
        {
            var panel = LoadPanel();

            Assert.AreEqual(WidthOf(panel, "Cards"), WidthOf(panel, "Identity/Type"), 0.5f,
                "La familia es un TMP más: sin ancho propio, un 'Jefe · Rango' largo vuelve a " +
                "ensanchar el panel — exactamente el bug que tenía el pie.");
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

        private static float FontSizeOf(Transform card, string child)
        {
            var found = card.Find(child);
            Assert.IsNotNull(found, $"La tarjeta del tooltip no tiene '{child}'.");

            var label = found.GetComponent<TMPro.TMP_Text>();
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
