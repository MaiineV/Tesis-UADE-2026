using NUnit.Framework;

namespace Rollgeon.Combat.Rolls.Tests
{
    /// <summary>
    /// BUG-060: <see cref="RollActionKindExtensions.IsCombatPayable"/> es el único gate
    /// que <c>DiceEnchantmentService</c> usa para decidir si una tirada paga
    /// encantamientos de oro — Attack/Defense/Heal sí, todo lo demás no.
    /// </summary>
    [TestFixture]
    public class RollActionKindTests
    {
        [TestCase(RollActionKind.Attack, true)]
        [TestCase(RollActionKind.Defense, true)]
        [TestCase(RollActionKind.Heal, true)]
        [TestCase(RollActionKind.Movement, false)]
        [TestCase(RollActionKind.EndTurn, false)]
        [TestCase(RollActionKind.ForceDoor, false)]
        [TestCase(RollActionKind.Exploration, false)]
        [TestCase(RollActionKind.Unknown, false)]
        public void IsCombatPayable_ReturnsExpectedValue_ForEachKind(RollActionKind kind, bool expected)
        {
            bool result = kind.IsCombatPayable();

            Assert.AreEqual(expected, result);
        }
    }
}
