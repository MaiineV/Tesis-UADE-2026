using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combos.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Combos.Tests
{
    /// <summary>
    /// Auditoría Fix#0047 parte 2 (priority desacoplada del daño): <c>Priority</c> ahora es
    /// el campo serializado <c>_priority</c>, editable por diseño, y NO defaultea a
    /// <c>BaseDamage</c>. Estos tests corren sobre los assets REALES del proyecto para
    /// atrapar assets sin migrar (un <c>_priority</c> en 0 haría que ese combo pierda
    /// contra todos en <c>MatchBest</c>) y para congelar los invariantes estructurales que
    /// no son decisión de balance: Higher Number es el fallback (pierde contra todo) y
    /// Generala siempre gana (hard rule #8). El orden relativo del RESTO es editable por
    /// el designer y a propósito no se asserta acá.
    /// </summary>
    [TestFixture]
    public class ComboPriorityAuditTests
    {
        private const string CombosFolder = "Assets/Rollgeon/Combos";

        private static List<BaseComboSO> LoadAllComboAssets()
        {
            var combos = new List<BaseComboSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:BaseComboSO", new[] { CombosFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BaseComboSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) combos.Add(asset);
            }
            return combos;
        }

        [Test]
        public void AllComboAssets_HaveExplicitPriority()
        {
            // Arrange
            var combos = LoadAllComboAssets();
            Assert.IsNotEmpty(combos, $"No se encontraron BaseComboSO en {CombosFolder}.");

            // Assert: _priority en 0 = asset sin migrar a Fix#0047 parte 2.
            foreach (var combo in combos)
            {
                Assert.Greater(combo.Priority, 0,
                    $"'{combo.name}' ({combo.ComboId}) tiene Priority 0 — falta autorar " +
                    "_priority en el asset (antes heredaba BaseDamage).");
            }
        }

        [Test]
        public void HigherNumber_IsTheLowestPriority_FallbackInvariant()
        {
            // Arrange
            var combos = LoadAllComboAssets();
            BaseComboSO higherNumber = null;
            foreach (var combo in combos)
                if (combo is Combo_HigherNumber) higherNumber = combo;
            Assert.IsNotNull(higherNumber, "Combo_HigherNumber.asset no encontrado.");

            // Assert: matchea con CUALQUIER selección — si no es estrictamente el de menor
            // prioridad, taparía combos reales en MatchBest.
            foreach (var combo in combos)
            {
                if (ReferenceEquals(combo, higherNumber)) continue;
                Assert.Less(higherNumber.Priority, combo.Priority,
                    $"Higher Number (fallback) debe perder contra '{combo.name}'.");
            }
        }

        [Test]
        public void Generala_PriorityOverride_AlwaysWins()
        {
            // Arrange
            var combos = LoadAllComboAssets();
            BaseComboSO generala = null;
            foreach (var combo in combos)
                if (combo is Combo_Generala) generala = combo;
            Assert.IsNotNull(generala, "Combo_Generala.asset no encontrado.");

            // Assert: hard rule #8 — el override ignora el campo serializado.
            Assert.AreEqual(int.MaxValue, generala.Priority);
        }

        [Test]
        public void FuerzaBrutaAsset_MigratedDamageAndPriority()
        {
            // Arrange: el asset real. Antes del rename _baseDamage era 30 (solo priority) y
            // el daño real (5) vivía en _baseDamageConfigurable.
            var asset = AssetDatabase.LoadAssetAtPath<BaseComboSO>(
                $"{CombosFolder}/Combo_FuerzaBruta.asset");
            Assert.IsNotNull(asset, "Combo_FuerzaBruta.asset no encontrado.");

            // Assert: el campo obvio hace lo obvio.
            Assert.AreEqual(5, asset.BaseDamage,
                "BaseDamage debe ser el piso de DAÑO (5), no el viejo valor de prioridad (30).");
            Assert.AreEqual(30, asset.Priority,
                "La prioridad autorada (30, entre Trío y Full House) vive en _priority.");
        }

        [Test]
        public void Priority_IsDecoupledFromBaseDamage()
        {
            // Arrange
            var combo = ComboTestUtils.CreateCombo<Combo_Par>("combo.par", 8, priority: 8);
            try
            {
                // Act: un designer sube el daño en el inspector.
                ComboTestUtils.SetField(combo, "_baseDamage", 400);

                // Assert: la selección de combo no se reordena por tunear daño.
                Assert.AreEqual(8, combo.Priority);
                Assert.AreEqual(400, combo.BaseDamage);
            }
            finally
            {
                Object.DestroyImmediate(combo);
            }
        }
    }
}
