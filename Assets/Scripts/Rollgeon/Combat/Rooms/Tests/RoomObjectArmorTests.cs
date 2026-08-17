using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// La armadura de la mesa: mientras los objetos de sala de un jefe sigan en pie, el daño que él
    /// recibe se reduce, y romper uno se lo devuelve al jugador para siempre.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lo que protegen: que la reducción escale con las ranuras <b>nunca rotas</b> y no con las vivas
    /// (progreso permanente), que baje en el mismo instante en que el objeto llega a 0 HP y no en el
    /// próximo turno del jefe, que el techo impida un jefe invulnerable, y que sin
    /// <c>AttributesManager</c> no se latchee nada.
    /// </para>
    /// <para>
    /// Los números de La Generala (5 dados × 0.14 = 70%) se pinean en <c>GeneralaAssetBuilderTests</c>;
    /// acá la mecánica se prueba con valores redondos.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class RoomObjectArmorTests
    {
        /// <summary>0.2 × 5 ranuras = 100% en crudo, para que el techo tenga algo que cortar.</summary>
        private const float PerObject = 0.2f;

        private AttributesManager _attributes;
        private RoomObjectArmorService _armor;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes);

            _armor = new RoomObjectArmorService();
            ServiceLocator.AddService<IIncomingDamageMultiplierProvider>(_armor);

            _boss = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            // Queda suscripto a EventManager, que ServiceLocator.Clear() no desengancha.
            _armor.Dispose();
            _attributes.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ---- Helpers -----------------------------------------------------

        /// <summary>Registra <paramref name="count"/> objetos con vida y publica la mesa.</summary>
        private Guid[] PublishTable(int count, float perObject = PerObject)
        {
            var guids = new Guid[count];
            for (int i = 0; i < count; i++) guids[i] = Spawn();

            _armor.Publish(_boss, guids, perObject);
            return guids;
        }

        private Guid Spawn(int hp = 45)
        {
            var id = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            _attributes.Register(id, attrs);
            return id;
        }

        private void Break(Guid guid) =>
            _attributes.SetAttributeValue<Health, int>(guid, 0);

        // ---- La escala ---------------------------------------------------

        [Test]
        public void FullTable_ReducesByThePerObjectShareTimesTheSlots()
        {
            // Arrange — 4 ranuras × 0.2 = 80%, por debajo del techo.
            PublishTable(4);

            // Assert
            Assert.IsTrue(_armor.TryGetMultiplier(_boss, out float multiplier));
            Assert.AreEqual(0.2f, multiplier, 0.001f, "80% de reducción deja el 20% pasando.");
            Assert.AreEqual(4, _armor.IntactCountFor(_boss));
        }

        [Test]
        public void EachBrokenObject_GivesItsShareBack()
        {
            // Arrange
            var slots = PublishTable(4);

            // Act
            Break(slots[0]);

            // Assert — 3 en pie × 0.2 = 60%.
            Assert.IsTrue(_armor.TryGetMultiplier(_boss, out float multiplier));
            Assert.AreEqual(0.4f, multiplier, 0.001f);
            Assert.AreEqual(3, _armor.IntactCountFor(_boss));
        }

        [Test]
        public void WholeTableBroken_LeavesNoReductionAtAll()
        {
            // Arrange
            var slots = PublishTable(4);

            // Act
            foreach (var slot in slots) Break(slot);

            // Assert — false y no "multiplicador 1": sin reducción el pipeline se saltea el stage
            // entero, así que el jugador no ve un "-0%" colgado en pantalla.
            Assert.IsFalse(_armor.TryGetMultiplier(_boss, out float multiplier));
            Assert.AreEqual(1f, multiplier, 0.001f);
            Assert.AreEqual(0f, _armor.ReductionFor(_boss));
        }

        // ---- Progreso permanente ------------------------------------------

        [Test]
        public void RespawnedObject_DoesNotGiveTheReductionBack()
        {
            // Arrange — se rompe la ranura 0 y el jefe la repone: guid nuevo, misma ranura.
            var slots = PublishTable(4);
            Break(slots[0]);
            Assert.AreEqual(0.6f, _armor.ReductionFor(_boss), 0.001f);

            // Act — el publish del turno siguiente trae el guid del dado repuesto.
            slots[0] = Spawn();
            _armor.Publish(_boss, slots, PerObject);

            // Assert — es lo que hace que romper la mesa compre algo estable. Con la reducción
            // reponiéndose sería una noria: limpiás cinco dados, se reponen, volvés a empezar.
            Assert.AreEqual(0.6f, _armor.ReductionFor(_boss), 0.001f,
                "El dado repuesto vuelve a bloquear y a darle la categoría, pero su parte de la " +
                "armadura no vuelve.");
            Assert.AreEqual(3, _armor.IntactCountFor(_boss));
        }

        [Test]
        public void AnEmptySlot_IsNotCountedAsBroken_UntilItHasHeldSomething()
        {
            // Arrange — primer tick del jefe: las ranuras existen pero todavía no se llenaron.
            _armor.Publish(_boss, new[] { Guid.Empty, Guid.Empty }, PerObject);

            // Assert — una ranura vacía por no haberse llenado nunca no es una ranura rota; si lo
            // fuera, el jefe arrancaría la pelea sin armadura por un orden de tick.
            Assert.AreEqual(2, _armor.IntactCountFor(_boss));
            Assert.AreEqual(0.4f, _armor.ReductionFor(_boss), 0.001f);
        }

        // ---- Instantáneo ---------------------------------------------------

        [Test]
        public void BreakingAnObject_DropsTheReduction_WithoutWaitingForAPublish()
        {
            // Arrange — el jugador rompe un dado en SU turno; el árbol del jefe no volvió a tickear.
            var slots = PublishTable(5);
            float before = _armor.ReductionFor(_boss);

            // Act
            Break(slots[0]);

            // Assert — si la cuenta se congelara en el publish, el golpe siguiente del jugador seguiría
            // reducido y se leería como que el juego no registró el impacto.
            Assert.Less(_armor.ReductionFor(_boss), before,
                "La reducción tiene que bajar en el mismo instante en que el objeto llega a 0 HP.");
        }

        // ---- El techo -------------------------------------------------------

        [Test]
        public void Reduction_IsCappedSoTheBossIsNeverInvulnerable()
        {
            // Arrange — 5 × 0.2 = 100% en crudo.
            PublishTable(5);

            // Assert
            Assert.AreEqual(RoomObjectArmorService.MaxReduction, _armor.ReductionFor(_boss), 0.001f,
                "Una reducción del 100% no es una mecánica dura, es una pelea que no termina.");
            Assert.IsTrue(_armor.TryGetMultiplier(_boss, out float multiplier));
            Assert.Greater(multiplier, 0f);
        }

        // ---- Degradados -----------------------------------------------------

        [Test]
        public void WithoutAPublishedTable_ReportsNoMultiplier()
        {
            Assert.IsFalse(_armor.TryGetMultiplier(Guid.NewGuid(), out float multiplier));
            Assert.AreEqual(1f, multiplier, 0.001f);
        }

        [Test]
        public void PublishWithZeroShare_DropsTheTable_InsteadOfLeavingStateBehind()
        {
            // Arrange
            PublishTable(4);

            // Act — una definición sin armadura.
            _armor.Publish(_boss, new[] { Spawn() }, 0f);

            // Assert
            Assert.IsFalse(_armor.TryGetMultiplier(_boss, out _));
        }

        [Test]
        public void WithoutAttributesManager_KeepsTheReductionItHad()
        {
            // Arrange
            var slots = PublishTable(4);
            Break(slots[0]);
            Assert.AreEqual(0.6f, _armor.ReductionFor(_boss), 0.001f);

            // Act — se cae el AttributesManager: no hay forma de saber qué está roto.
            ServiceLocator.Clear();
            ServiceLocator.AddService<IIncomingDamageMultiplierProvider>(_armor);
            Break(slots[1]);

            // Assert — el latch de lo ya visto se conserva y lo nuevo no se latchea. Fallar hacia "el
            // jefe conserva su armadura" y no hacia "la perdió": lo segundo le regalaría la pelea sin
            // que nada lo explique.
            Assert.AreEqual(0.6f, _armor.ReductionFor(_boss), 0.001f);
        }

        [Test]
        public void OnCombatEnd_ForgetsEveryTable()
        {
            // Arrange — el servicio es Global y sobrevive a la pelea.
            PublishTable(4);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.IsFalse(_armor.TryGetMultiplier(_boss, out _),
                "Una mesa que sobreviva le daría armadura a un guid que ya no existe.");
        }
    }
}
