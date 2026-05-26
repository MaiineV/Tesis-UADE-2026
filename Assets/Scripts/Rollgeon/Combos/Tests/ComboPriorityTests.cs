using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using UnityEngine;

namespace Rollgeon.Combos.Tests
{
    /// <summary>
    /// Cubre hard rule #8 / plan §10.7: <see cref="Combo_Generala.Priority"/> es <c>int.MaxValue</c>
    /// de modo que, aunque un designer suba el <see cref="BaseComboSO.BaseDamage"/> del Poker a un
    /// valor altisimo, Generala siempre gana el tie-break downstream en <c>ContractSheet.EvaluateRoll</c>.
    /// </summary>
    [TestFixture]
    public class ComboPriorityTests
    {
        [Test]
        public void Generala_Priority_Is_IntMaxValue()
        {
            var generala = ComboTestUtils.CreateCombo<Combo_Generala>(ComboId.Generala, 100);
            try
            {
                Assert.AreEqual(int.MaxValue, generala.Priority);
            }
            finally
            {
                Object.DestroyImmediate(generala);
            }
        }

        [Test]
        public void Generala_Priority_Beats_Poker_Even_If_Poker_BaseDamage_Is_999()
        {
            var generala = ComboTestUtils.CreateCombo<Combo_Generala>(ComboId.Generala, 100);
            var poker = ComboTestUtils.CreateCombo<Combo_Poker>(ComboId.Poker, 999);
            try
            {
                Assert.Greater(generala.Priority, poker.Priority,
                    "Generala priority must beat poker priority regardless of designer edits.");
                Assert.Greater(generala.Priority, poker.Priority + 1000,
                    "Sanity: int.MaxValue - 1999 >> 999. Garantiza margen absoluto.");
            }
            finally
            {
                Object.DestroyImmediate(generala);
                Object.DestroyImmediate(poker);
            }
        }

        [Test]
        public void Default_Priority_Equals_BaseDamage_For_NonGenerala()
        {
            var par = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
            var fullhouse = ComboTestUtils.CreateCombo<Combo_FullHouse>(ComboId.FullHouse, 40);
            try
            {
                Assert.AreEqual(10, par.Priority);
                Assert.AreEqual(40, fullhouse.Priority);
            }
            finally
            {
                Object.DestroyImmediate(par);
                Object.DestroyImmediate(fullhouse);
            }
        }
    }
}
