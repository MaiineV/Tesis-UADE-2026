using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Cubre <see cref="InventoryService.CanActivateItem"/> — el predicado read-only que
    /// el HUD consulta antes del click. Antes no existía: la única forma de saber si un
    /// item se podía usar era intentarlo, así que la barra de items activos no tenía
    /// forma de pintarse ni de explicar el rechazo.
    /// <para>
    /// Los tests corren sin <c>TurnManager</c> registrado a propósito salvo donde el
    /// gate de action economy es lo que se prueba — así se aisla cada rama.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class InventoryActivationGateTests
    {
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        private InventoryService _service;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_playerGuid));
            _service = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // Slot / tipo
        // ------------------------------------------------------------------

        [Test]
        public void test_canActivate_indexOutOfRange_returnsInvalidSlot()
        {
            // Act + Assert — inventario vacío: cualquier índice es inválido.
            Assert.AreEqual(ItemActivationBlock.InvalidSlot, _service.CanActivateItem(0, Ctx()));
            Assert.AreEqual(ItemActivationBlock.InvalidSlot, _service.CanActivateItem(-1, Ctx()));
        }

        [Test]
        public void test_canActivate_freeActiveItemWithNoConditions_returnsNone()
        {
            // Arrange
            _service.AddItem(NewActive("item.free"));

            // Act + Assert
            Assert.AreEqual(ItemActivationBlock.None, _service.CanActivateItem(0, Ctx()));
        }

        // ------------------------------------------------------------------
        // Cooldown
        // ------------------------------------------------------------------

        [Test]
        public void test_canActivate_itemOnCooldown_returnsOnCooldown()
        {
            // Arrange — usarlo prende el cooldown (no se consume, así queda en el slot).
            var item = NewActive("item.cd");
            item.Cooldown = 2;
            _service.AddItem(item);
            Assert.IsTrue(_service.ActivateItem(0, Ctx()), "el primer uso tiene que pasar");

            // Act + Assert
            Assert.AreEqual(ItemActivationBlock.OnCooldown, _service.CanActivateItem(0, Ctx()));
        }

        [Test]
        public void test_activate_whileOnCooldown_isRejected()
        {
            // Arrange
            var item = NewActive("item.cd");
            item.Cooldown = 1;
            _service.AddItem(item);
            _service.ActivateItem(0, Ctx());
            Eff_Count.Runs = 0;

            // Act
            bool ok = _service.ActivateItem(0, Ctx());

            // Assert
            Assert.IsFalse(ok);
            Assert.AreEqual(0, Eff_Count.Runs, "el efecto no debe correr con el item en cooldown");
        }

        // ------------------------------------------------------------------
        // Precondiciones — el "prerequisito" que la UI tiene que poder anticipar
        // ------------------------------------------------------------------

        [Test]
        public void test_canActivate_failingPrecondition_returnsPreconditionFailed()
        {
            // Arrange
            var item = NewActive("item.gated");
            item.OnActivate.PreConditions.Add(new PC_Fixed { Result = false });
            _service.AddItem(item);

            // Act + Assert
            Assert.AreEqual(ItemActivationBlock.PreconditionFailed, _service.CanActivateItem(0, Ctx()));
        }

        [Test]
        public void test_canActivate_passingPrecondition_returnsNone()
        {
            // Arrange
            var item = NewActive("item.gated");
            item.OnActivate.PreConditions.Add(new PC_Fixed { Result = true });
            _service.AddItem(item);

            // Act + Assert
            Assert.AreEqual(ItemActivationBlock.None, _service.CanActivateItem(0, Ctx()));
        }

        [Test]
        public void test_activate_withFailingPrecondition_doesNotConsumeTheItem()
        {
            // Arrange — ConsumedOnUse: el bug clásico sería descontarlo igual.
            var item = NewActive("item.gated");
            item.ConsumedOnUse = true;
            item.OnActivate.PreConditions.Add(new PC_Fixed { Result = false });
            _service.AddItem(item);

            // Act
            bool ok = _service.ActivateItem(0, Ctx());

            // Assert
            Assert.IsFalse(ok);
            Assert.AreEqual(1, _service.ActiveItems.Count, "el item tiene que seguir en el inventario");
        }

        // ------------------------------------------------------------------
        // Action economy — sin TurnManager registrado no hay forma de cobrarla
        // ------------------------------------------------------------------

        [Test]
        public void test_canActivate_consumesActionWithoutTurnManager_returnsForbidden()
        {
            // Arrange
            var item = NewActive("item.action");
            item.ConsumesAction = true;
            _service.AddItem(item);

            // Act + Assert
            Assert.AreEqual(ItemActivationBlock.ForbiddenByRuleset, _service.CanActivateItem(0, Ctx()));
        }

        [Test]
        public void test_canActivate_isPure_doesNotRunTheEffect()
        {
            // Arrange — el HUD lo llama en cada refresh: no puede tener side-effects.
            _service.AddItem(NewActive("item.free"));
            Eff_Count.Runs = 0;

            // Act
            _service.CanActivateItem(0, Ctx());
            _service.CanActivateItem(0, Ctx());

            // Assert
            Assert.AreEqual(0, Eff_Count.Runs);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private EffectContext Ctx()
        {
            return new EffectContext { SourceGuid = _playerGuid, TargetGuid = _playerGuid, lastResult = true };
        }

        private ItemSO NewActive(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Active;
            item.ConsumesAction = false;
            item.ConsumedOnUse = false;
            item.OnActivate = new EffectData();
            item.OnActivate.Effects.Add(new Eff_Count());
            _spawned.Add(item);
            return item;
        }

        /// <summary>Cuenta ejecuciones para separar "se evaluó" de "se ejecutó".</summary>
        [Serializable]
        private sealed class Eff_Count : BaseEffect
        {
            public static int Runs;
            public override string GetEffectName() => "Count";
            public override bool ApplyEffect(EffectContext context)
            {
                Runs++;
                return true;
            }
        }

        /// <summary>Precondición con resultado fijo — el prerequisito bajo control.</summary>
        [Serializable]
        private sealed class PC_Fixed : BasePreCondition
        {
            public bool Result;
            public override string ConditionName => "Fixed";
            public override bool Evaluate(PreConditionContext context) => Result;
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
