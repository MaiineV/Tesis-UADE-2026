using NUnit.Framework;
using Rollgeon.Combat.Rolls;
using Rollgeon.UI.HUD;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// BUG-060: <see cref="HeroActionBehavior.ResolveRollActionKind"/> — el discriminante
    /// que viaja en <c>EffectContext.ActionKind</c> hacia <c>OnRollResolved</c>/
    /// <c>ComboPlayedPayload</c>. Deriva de <see cref="HeroBehaviorSlot"/> para los 4
    /// behaviors base (identidad estable ya usada por la UI); sin eso, cae a
    /// <see cref="DiceBoardType"/> explícito.
    /// </summary>
    [TestFixture]
    public class HeroActionBehaviorRollActionKindTests
    {
        [TestCase(HeroBehaviorSlot.Movement, RollActionKind.Movement)]
        [TestCase(HeroBehaviorSlot.BaseAttack, RollActionKind.Attack)]
        [TestCase(HeroBehaviorSlot.ClassSkill, RollActionKind.ClassSkill)]
        [TestCase(HeroBehaviorSlot.Healing, RollActionKind.Heal)]
        [TestCase(HeroBehaviorSlot.ForceDoor, RollActionKind.ForceDoor)]
        [TestCase(HeroBehaviorSlot.Defense, RollActionKind.Defense)]
        public void ResolveRollActionKind_BaseBehavior_DerivesFromSlot(
            HeroBehaviorSlot slot, RollActionKind expected)
        {
            // Arrange — un trío tirado para MOVERSE (BUG-060) debe reportar Movement,
            // no Attack, aunque comparta el mismo ContractSheet/bag que un ataque.
            var behavior = new HeroActionBehavior { IsBaseBehavior = true, Slot = slot };

            // Act
            var kind = behavior.ResolveRollActionKind();

            // Assert
            Assert.AreEqual(expected, kind);
        }

        [TestCase(DiceBoardType.Attack, RollActionKind.Attack)]
        [TestCase(DiceBoardType.Defense, RollActionKind.Defense)]
        [TestCase(DiceBoardType.Default, RollActionKind.Unknown)]
        public void ResolveRollActionKind_NonBaseBehavior_FallsBackToBoardType(
            DiceBoardType boardType, RollActionKind expected)
        {
            // Arrange — behavior custom (no uno de los 4 slots base): sin BoardType
            // explícito no hay forma de saber si es de combate ⇒ Unknown (fail-safe,
            // no pagable).
            var behavior = new HeroActionBehavior { IsBaseBehavior = false, BoardType = boardType };

            // Act
            var kind = behavior.ResolveRollActionKind();

            // Assert
            Assert.AreEqual(expected, kind);
        }
    }
}
