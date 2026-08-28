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
        public void LaFilaDeFamiliaNoDecideElAnchoDelPanel()
        {
            var panel = LoadPanel();

            Assert.AreEqual(WidthOf(panel, "Cards"), WidthOf(panel, "Identity/Type"), 0.5f,
                "La familia es un TMP más: sin ancho propio, un 'Jefe · Rango' largo vuelve a " +
                "ensanchar el panel — exactamente el bug que tenía el pie.");
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
