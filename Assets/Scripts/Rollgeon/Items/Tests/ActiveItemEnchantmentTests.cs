using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Items.Active;
using Rollgeon.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Encantamiento del ítem activo (GDD "Ítems Activos" §14, §20, §25, §28, §34).
    /// Las reglas duras: máximo uno, se pisa, ajusta el resultado <b>antes</b> de la
    /// banda, no puede sacarlo del rango del dado, se va con el ítem descartado, y sus
    /// usos limitados resetean entre combates.
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemEnchantmentTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private EquippedActiveItemService _equipped;
        private ActiveItemActivationService _service;
        private FakeRollPool _rolls;
        private FakeDieRoller _roller;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player));

            _rolls = new FakeRollPool { InCombat = true };
            _rolls.Current[_player] = 20;
            ServiceLocator.AddService<IRollPoolService>(_rolls);

            _equipped = new EquippedActiveItemService(catalog: null);
            _roller = new FakeDieRoller();
            _service = new ActiveItemActivationService(_equipped, _roller);
            _service.ResolveScheduler = (seconds, callback) => callback();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _equipped?.Dispose();
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // Slot único, se pisa (§25)
        // ------------------------------------------------------------------

        [Test]
        public void test_enchantment_onEmptySlot_isRejected()
        {
            // Act — un encantamiento sin item no tiene donde vivir.
            LogAssert.ignoreFailingMessages = true;
            bool ok = _equipped.ApplyEnchantment(NewEnchantment("e.a", new RollFlatBonus { Amount = 1 }));

            // Assert
            Assert.IsFalse(ok);
            Assert.IsNull(_equipped.Enchantment);
        }

        [Test]
        public void test_enchantment_applyingASecond_overwritesTheFirst()
        {
            // Arrange — "el nuevo encantamiento reemplaza al anterior. Se pisa, no
            // coexisten".
            EquipItem(DiceType.D6);
            var first = NewEnchantment("e.a", new RollFlatBonus { Amount = 1 });
            var second = NewEnchantment("e.b", new RollFlatBonus { Amount = 2 });
            _equipped.ApplyEnchantment(first);

            // Act
            _equipped.ApplyEnchantment(second);

            // Assert
            Assert.AreSame(second, _equipped.Enchantment);
        }

        [Test]
        public void test_replacingTheItem_dropsTheEnchantment()
        {
            // Arrange — "el encantamiento se queda con el ítem descartado, no se
            // transfiere al nuevo ítem equipado".
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.a", new RollFlatBonus { Amount = 1 }));

            // Act
            _equipped.Equip(NewActiveItem("item.otro", DiceType.D6));

            // Assert
            Assert.IsNull(_equipped.Enchantment);
        }

        // ------------------------------------------------------------------
        // Orden de operaciones (§14): ajusta antes de la banda
        // ------------------------------------------------------------------

        [Test]
        public void test_enchantment_adjustsTheRollBeforeTheBandIsDecided()
        {
            // Arrange — cara 4 sobre D6 es mixta; con +1 pasa a 5, que es positiva. Si el
            // ajuste corriera despues de la banda no cambiaria nada.
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.calibracion", new RollFlatBonus { Amount = 1 }));
            _roller.Next = 4;

            // Act
            var result = ConfirmAndAccept();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(4, result.Value.RawRoll, "la cara cruda del dado");
            Assert.AreEqual(5, result.Value.Roll, "la cara ya ajustada");
            Assert.AreEqual(ActiveItemBand.Positive, result.Value.Band);
            Assert.IsTrue(result.Value.WasEnchanted);
        }

        [Test]
        public void test_theEnchantmentsOwnCap_keepsTheTopBandOutOfReach()
        {
            // Arrange — la "Calibración" del GDD: +1 con máximo 5. Sobre D6 eso significa
            // que el encantamiento nunca alcanza el 6.
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.calibracion",
                new RollFlatBonus { Amount = 1, MaxResult = 5 }));
            _roller.Next = 5;

            // Act
            var result = ConfirmAndAccept();

            // Assert
            Assert.AreEqual(5, result.Value.Roll, "el tope propio lo frena en 5");
        }

        [Test]
        public void test_theResultNeverLeavesTheDieRange()
        {
            // Arrange — un item mal autorado (sin tope propio, bonus enorme) no puede
            // romper la regla del sistema.
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.roto", new RollFlatBonus { Amount = 99 }));
            _roller.Next = 3;

            // Act
            var result = ConfirmAndAccept();

            // Assert
            Assert.AreEqual(6, result.Value.Roll, "clampeado al maximo del dado");
        }

        [Test]
        public void test_negativeBonus_neverGoesBelowOne()
        {
            // Arrange
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.maldito", new RollFlatBonus { Amount = -99 }));
            _roller.Next = 3;

            // Act
            var result = ConfirmAndAccept();

            // Assert
            Assert.AreEqual(1, result.Value.Roll);
        }

        [Test]
        public void test_rollFloor_softensTheBottomWithoutRemovingTheBadBand()
        {
            // Arrange — el "Seguro flojo" del GDD: si sacás 1, tratá como 2. Sobre D6 el
            // 2 sigue siendo banda negativa, que es el punto: suaviza el piso pero no
            // elimina el riesgo.
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.seguro",
                new RollFloor { Threshold = 1, TreatAs = 2 }));
            _roller.Next = 1;

            // Act
            var result = ConfirmAndAccept();

            // Assert
            Assert.AreEqual(2, result.Value.Roll);
            Assert.AreEqual(ActiveItemBand.Negative, result.Value.Band,
                "sigue siendo banda negativa — la proteccion es parcial, no elimina el riesgo");
        }

        // ------------------------------------------------------------------
        // Usos limitados (§28, §34)
        // ------------------------------------------------------------------

        [Test]
        public void test_limitedEnchantment_stopsApplyingWhenItRunsOut()
        {
            // Arrange — un uso por combate.
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.unavez",
                new RollFlatBonus { Amount = 1 }, usesPerCombat: 1));
            _roller.Next = 3;

            // Act
            var first = ConfirmAndAccept();
            var second = ConfirmAndAccept();

            // Assert
            Assert.AreEqual(4, first.Value.Roll, "el primer uso ajusta");
            Assert.AreEqual(3, second.Value.Roll, "el segundo ya no");
            Assert.IsFalse(second.Value.WasEnchanted);
        }

        [Test]
        public void test_limitedUses_resetOnCombatStart()
        {
            // Arrange — el GDD: "el reroll 'una vez por combate' debe resetear entre
            // combates, no persistir indefinidamente".
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.unavez",
                new RollFlatBonus { Amount = 1 }, usesPerCombat: 1));
            _roller.Next = 3;
            ConfirmAndAccept();
            Assert.AreEqual(0, _equipped.EnchantmentUsesLeft);

            // Act
            EventManager.Trigger(EventName.OnCombatStart, _player);

            // Assert
            Assert.AreEqual(1, _equipped.EnchantmentUsesLeft);
            Assert.AreEqual(4, ConfirmAndAccept().Value.Roll);
        }

        [Test]
        public void test_anAdjustmentThatChangesNothing_doesNotBurnAUse()
        {
            // Arrange — con el tope en 3 y una tirada de 3, el ajuste no mueve nada.
            // Gastar el uso ahi seria regalar el limite.
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.tope",
                new RollFlatBonus { Amount = 1, MaxResult = 3 }, usesPerCombat: 1));
            _roller.Next = 3;

            // Act
            var result = ConfirmAndAccept();

            // Assert
            Assert.AreEqual(3, result.Value.Roll);
            Assert.AreEqual(1, _equipped.EnchantmentUsesLeft, "el uso sigue disponible");
        }

        [Test]
        public void test_unlimitedEnchantment_keepsApplying()
        {
            // Arrange
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.siempre", new RollFlatBonus { Amount = 1 }));
            _roller.Next = 2;

            // Act + Assert
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(3, ConfirmAndAccept().Value.Roll);
        }

        [Test]
        public void test_noEnchantment_leavesTheRollUntouched()
        {
            // Arrange
            EquipItem(DiceType.D6);
            _roller.Next = 4;

            // Act
            var result = ConfirmAndAccept();

            // Assert
            Assert.AreEqual(4, result.Value.Roll);
            Assert.AreEqual(4, result.Value.RawRoll);
            Assert.IsFalse(result.Value.WasEnchanted);
        }

        // ------------------------------------------------------------------
        // Persistencia (§34)
        // ------------------------------------------------------------------

        [Test]
        public void test_captureState_includesTheEnchantmentAndItsRemainingUses()
        {
            // Arrange
            EquipItem(DiceType.D6);
            _equipped.ApplyEnchantment(NewEnchantment("e.unavez",
                new RollFlatBonus { Amount = 1 }, usesPerCombat: 2));

            // Act
            var state = _equipped.CaptureState() as Dictionary<string, object>;

            // Assert
            Assert.IsNotNull(state);
            Assert.AreEqual("item.test", state["itemId"]);
            Assert.AreEqual("e.unavez", state["enchantmentId"]);
            Assert.AreEqual(2, state["enchantmentUsesLeft"]);
        }

        [Test]
        public void test_restoreState_acceptsTheOldPlainStringFormat()
        {
            // Arrange — los saves anteriores guardaban solo el ItemId. Un save viejo no
            // puede romper la carga de la run.
            var catalog = ScriptableObject.CreateInstance<ItemCatalogSO>();
            _spawned.Add(catalog);
            var service = new EquippedActiveItemService(catalog);

            // Act — sin el item en el catalogo avisa y deja el slot vacio, que es el
            // comportamiento esperado; lo que importa es que no explote con el formato.
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => service.RestoreState("item.viejo"));

            // Assert
            Assert.IsFalse(service.HasItem);
            service.Dispose();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void EquipItem(DiceType die) => _equipped.Equip(NewActiveItem("item.test", die));

        /// <summary>
        /// Activacion completa: confirma y, con el scheduler sincronico del SetUp, la
        /// resolucion (donde corre el encantamiento) llega en la misma llamada.
        /// </summary>
        private ActiveItemActivationResult? ConfirmAndAccept()
        {
            ActiveItemActivationResult? result = null;
            Action<ActiveItemActivationResult> capture = r => result = r;
            _service.OnResolved += capture;
            _service.Confirm(selection: null);
            _service.OnResolved -= capture;
            return result;
        }

        private ItemSO NewActiveItem(string id, DiceType die)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.ActiveDie = die;
            item.ActiveFamily = ActiveItemFamily.Potencia;
            item.OnNegativeBand = new EffectData();
            item.OnMixedBand = new EffectData();
            item.OnPositiveBand = new EffectData();
            _spawned.Add(item);
            return item;
        }

        private ActiveItemEnchantmentSO NewEnchantment(string id, ActiveItemRollModifier modifier,
            int usesPerCombat = 0)
        {
            var e = ScriptableObject.CreateInstance<ActiveItemEnchantmentSO>();
            e.EnchantmentId = id;
            e.DisplayName = id;
            e.Modifier = modifier;
            e.UsesPerCombat = usesPerCombat;
            _spawned.Add(e);
            return e;
        }

        private sealed class FakeDieRoller : IActiveItemDieRoller
        {
            public int Next = 1;
            public int Roll(DiceType die) => Next;
        }

        private sealed class FakeRollPool : IRollPoolService
        {
            public readonly Dictionary<Guid, int> Current = new Dictionary<Guid, int>();
            public bool InCombat = true;

            public bool IsCombatActive => InCombat;

            public void InitializeForEntity(Guid entityId) => Current[entityId] = 5;

            public bool TrySpendRolls(Guid entityId, int count)
            {
                if (!Current.TryGetValue(entityId, out var have) || count > have) return false;
                Current[entityId] = have - count;
                return true;
            }

            public int Drain(Guid entityId, int amount) => 0;
            public void AddRolls(Guid entityId, int amount) { }
            public int GetCurrent(Guid entityId) => Current.TryGetValue(entityId, out var v) ? v : 0;
            public int GetMax(Guid entityId) => 99;
            public int GetRollsPerTurn(Guid entityId) => 5;
            public void AddRollPoolBonus(int amount) { }
            public void RestoreCurrent(Guid entityId, int value) => Current[entityId] = value;
        }

        private sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
