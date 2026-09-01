using System;
using NUnit.Framework;
using Rollgeon.Combat.Rolls;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Feature#0055 — el slot 2 pasó de SpecialAttack a ClassSkill conservando el valor: los
    /// ClassHeroSO (Odin) y el prefab del HUD serializan el int, así que un cambio de valor
    /// rompería el wiring silenciosamente.
    /// </summary>
    [TestFixture]
    public sealed class HeroBehaviorSlotTests
    {
        [Test]
        public void ClassSkill_KeepsSerializedValueTwo()
        {
            Assert.AreEqual(2, (int)HeroBehaviorSlot.ClassSkill);
        }

        [Test]
        public void Slots_AreSixAndContiguous()
        {
            var values = (HeroBehaviorSlot[])Enum.GetValues(typeof(HeroBehaviorSlot));
            Assert.AreEqual(6, values.Length);
            for (int i = 0; i < values.Length; i++)
                Assert.AreEqual(i, (int)values[i]);
        }

        [Test]
        public void RollActionKind_ClassSkill_IsNotCombatPayable()
        {
            // Un empuje no es un golpe de combo: no paga encantamientos de oro ni hooks de ítem
            // filtrados por Attack.
            Assert.IsFalse(RollActionKind.ClassSkill.IsCombatPayable());
        }
    }
}
