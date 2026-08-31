using NUnit.Framework;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Tests
{
    /// <summary>
    /// BUG-082: wiring test del <see cref="ActionRollFormulaLayoutInstaller"/> — los dos
    /// labels del ActionRoll HUD quedan en filas disjuntas (sin solape vertical) y
    /// <c>_thresholdLabel</c> queda cableado. Corre <c>Apply</c> sobre los contents del
    /// prefab SIN guardarlos: el asset real se regenera con el MenuItem.
    /// </summary>
    [TestFixture]
    public class ActionRollFormulaLayoutInstallerTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = PrefabUtility.LoadPrefabContents(ActionRollFormulaLayoutInstaller.PrefabPath);
            Assert.IsNotNull(_root, "no se pudo cargar Canvas_ActionRoll.prefab");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) PrefabUtility.UnloadPrefabContents(_root);
        }

        private (RectTransform threshold, RectTransform formula, DamageFormulaView view) ApplyAndResolve()
        {
            Assert.IsTrue(ActionRollFormulaLayoutInstaller.Apply(_root), "Apply debe reportar éxito");
            var view = _root.GetComponentInChildren<DamageFormulaView>(includeInactive: true);
            var threshold = (RectTransform)view.transform.Find("ThresholdLabel");
            var formula = (RectTransform)view.transform.Find("FormulaLabel");
            return (threshold, formula, view);
        }

        [Test]
        public void test_installer_apply_wiresThresholdLabelField()
        {
            // Act
            var (threshold, _, view) = ApplyAndResolve();

            // Assert — el campo deja de depender del auto-resolve por nombre del Awake.
            var so = new SerializedObject(view);
            var wired = so.FindProperty("_thresholdLabel").objectReferenceValue;
            Assert.AreEqual(threshold.GetComponent<TextMeshProUGUI>(), wired,
                "_thresholdLabel debe quedar cableado al TMP del hijo ThresholdLabel");
        }

        [Test]
        public void test_installer_apply_leavesRowsVerticallyDisjoint()
        {
            // Act
            var (threshold, formula, _) = ApplyAndResolve();

            // Assert — la fila de la fórmula termina antes de que empiece la del
            // threshold: sin rango Y compartido no hay glifos montados.
            Assert.Less(formula.anchorMax.y, threshold.anchorMin.y,
                "las filas no deben compartir rango vertical");
            Assert.AreEqual(Vector2.zero, threshold.sizeDelta, "threshold stretch puro");
            Assert.AreEqual(Vector2.zero, formula.sizeDelta, "formula stretch puro");
        }

        [Test]
        public void test_installer_apply_isIdempotent()
        {
            // Arrange
            var (threshold1, formula1, _) = ApplyAndResolve();
            var tMin = threshold1.anchorMin; var tMax = threshold1.anchorMax;
            var fMin = formula1.anchorMin; var fMax = formula1.anchorMax;

            // Act — segunda pasada sobre los mismos contents.
            var (threshold2, formula2, _) = ApplyAndResolve();

            // Assert — mismos valores absolutos: correrlo dos veces no acumula nada.
            Assert.AreEqual(tMin, threshold2.anchorMin);
            Assert.AreEqual(tMax, threshold2.anchorMax);
            Assert.AreEqual(fMin, formula2.anchorMin);
            Assert.AreEqual(fMax, formula2.anchorMax);
        }
    }
}
