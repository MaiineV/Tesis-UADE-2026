using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Items.Active.Blood;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// <see cref="BloodD6Service"/> (Feature#0084, Blood D6): arma la carga sobre el próximo
    /// combo de Ataque, la consume al resolverse el daño real (payloads a mano, patrón
    /// <c>ComboPassiveGenericTriggerTests</c>) y reparte el bonus entre el primario y
    /// secundarios con LoS/rango, sin re-disparar la propia carga.
    /// </summary>
    [TestFixture]
    public class BloodD6ServiceTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private FakeEntityQuery _query;
        private AttributesManager _attrs;
        private GridManager _grid;
        private DamagePipeline _damage;
        private BloodD6Service _service;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs, ServiceScope.Global);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(30, 5));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _damage = new DamagePipeline(_attrs);
            ServiceLocator.AddService<IDamagePipeline>(_damage, ServiceScope.Global);

            _service = new BloodD6Service();
            _service.ConfigureForTests();

            _owner = Guid.NewGuid();
            _grid.Register(_owner, new GridCoord(0, 0));
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboPlayedPayload>.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
        }

        private Guid SpawnEnemy(GridCoord coord, int hp)
        {
            var guid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            _attrs.Register(guid, attrs);
            _grid.Register(guid, coord);
            _query.Enemies.Add(new Entity { Guid = guid });
            return guid;
        }

        private void RaiseComboPlayed(Guid primary, string comboId, RollActionKind kind = RollActionKind.Attack)
        {
            TypedEvent<ComboPlayedPayload>.Raise(new ComboPlayedPayload
            {
                SourceGuid = _owner,
                TargetGuid = primary,
                ComboId = comboId,
                ActionKind = kind,
            });
        }

        private void RaiseDamageResolved(Guid primary, string comboId, int finalDamage, int shieldAbsorbed = 0,
            AttackKind kind = AttackKind.ComboAttack)
        {
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _owner,
                TargetGuid = primary,
                ComboId = comboId,
                Kind = kind,
                FinalDamage = finalDamage,
                ShieldAbsorbed = shieldAbsorbed,
            });
        }

        [Test]
        public void test_arm_setsPendingBonusPct_forFaceOneAndFaceSix()
        {
            _service.Arm(_owner, 1);
            Assert.IsTrue(_service.TryGetPendingBonusPct(_owner, out var pctFace1));
            Assert.AreEqual(10, pctFace1);

            _service.Arm(_owner, 6);
            Assert.IsTrue(_service.TryGetPendingBonusPct(_owner, out var pctFace6));
            Assert.AreEqual(66, pctFace6);
        }

        [Test]
        public void test_hasPending_trueAfterArm_falseAfterClear()
        {
            _service.Arm(_owner, 3);
            Assert.IsTrue(_service.HasPending(_owner));

            _service.Clear(_owner);
            Assert.IsFalse(_service.HasPending(_owner));
        }

        [Test]
        public void test_fullFlow_face1_splitsBetweenPrimaryAndNearbySecondary_remainderToPrimary()
        {
            // face 1 → bonus 10%, maxReceivers 6. bonus = floor(0.10 * 50) = 5.
            var primary = SpawnEnemy(new GridCoord(2, 0), hp: 100);
            var near = SpawnEnemy(new GridCoord(3, 0), hp: 100);   // Manhattan 1 del primario
            var far = SpawnEnemy(new GridCoord(10, 0), hp: 100);   // Manhattan 8 — fuera de rango (<=4)

            _service.Arm(_owner, 1);
            RaiseComboPlayed(primary, "combo.a");
            RaiseDamageResolved(primary, "combo.a", finalDamage: 50);

            Assert.AreEqual(97, _attrs.GetAttribute<Health>(primary).Value, "100 - (share 2 + resto 1) = 97.");
            Assert.AreEqual(98, _attrs.GetAttribute<Health>(near).Value, "100 - share 2.");
            Assert.AreEqual(100, _attrs.GetAttribute<Health>(far).Value, "fuera de rango Manhattan <= 4, no recibe nada.");
            Assert.IsFalse(_service.HasPending(_owner), "la carga se consume tras el golpe.");
        }

        [Test]
        public void test_fullFlow_face6_onlyPrimaryReceives_maxReceiversIsOne()
        {
            var primary = SpawnEnemy(new GridCoord(2, 0), hp: 100);
            var near = SpawnEnemy(new GridCoord(3, 0), hp: 100);

            _service.Arm(_owner, 6);
            RaiseComboPlayed(primary, "combo.a");
            RaiseDamageResolved(primary, "combo.a", finalDamage: 50);

            // bonus = floor(0.66 * 50) = 33, maxReceivers = 1 → todo concentrado en el primario.
            Assert.AreEqual(67, _attrs.GetAttribute<Health>(primary).Value);
            Assert.AreEqual(100, _attrs.GetAttribute<Health>(near).Value, "maxReceivers 1 excluye al secundario.");
        }

        [Test]
        public void test_bonusFormula_includesShieldAbsorbed_asPartOfNormalDamage()
        {
            // "daño normal" = FinalDamage + ShieldAbsorbed. bonus = floor(0.20 * (30 + 20)) = 10.
            var primary = SpawnEnemy(new GridCoord(2, 0), hp: 100);

            _service.Arm(_owner, 2);
            RaiseComboPlayed(primary, "combo.a");
            RaiseDamageResolved(primary, "combo.a", finalDamage: 30, shieldAbsorbed: 20);

            Assert.AreEqual(90, _attrs.GetAttribute<Health>(primary).Value, "100 - 10 de bonus.");
        }

        [Test]
        public void test_invalidCombo_emptyComboId_doesNotConsumeCharge()
        {
            var primary = SpawnEnemy(new GridCoord(2, 0), hp: 100);

            _service.Arm(_owner, 3);
            RaiseComboPlayed(primary, comboId: string.Empty);
            RaiseDamageResolved(primary, comboId: string.Empty, finalDamage: 50, kind: AttackKind.BasicAttack);

            Assert.AreEqual(100, _attrs.GetAttribute<Health>(primary).Value, "sin ComboId válido no hay bonus.");
            Assert.IsTrue(_service.HasPending(_owner), "un combo inválido no consume la carga.");
        }

        [Test]
        public void test_mismatchedComboId_doesNotConsumeCharge()
        {
            var primary = SpawnEnemy(new GridCoord(2, 0), hp: 100);

            _service.Arm(_owner, 3);
            RaiseComboPlayed(primary, "combo.a");
            RaiseDamageResolved(primary, "combo.b", finalDamage: 50);

            Assert.AreEqual(100, _attrs.GetAttribute<Health>(primary).Value);
            Assert.IsTrue(_service.HasPending(_owner), "el ComboId no coincide — la carga sigue viva.");
        }

        [Test]
        public void test_nonAttackAction_doesNotArmAwaiting()
        {
            // Un trío tirado para Defensa/Movimiento no debe dejar el combo "en espera".
            var primary = SpawnEnemy(new GridCoord(2, 0), hp: 100);

            _service.Arm(_owner, 3);
            RaiseComboPlayed(primary, "combo.a", kind: RollActionKind.Defense);
            RaiseDamageResolved(primary, "combo.a", finalDamage: 50);

            Assert.AreEqual(100, _attrs.GetAttribute<Health>(primary).Value);
            Assert.IsTrue(_service.HasPending(_owner), "sin ventana de Ataque armada, la carga sigue pendiente.");
        }

        [Test]
        public void test_onCombatEnd_clearsPendingCharge()
        {
            _service.Arm(_owner, 2);

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsFalse(_service.HasPending(_owner));
        }

        [Test]
        public void test_distributedBonus_doesNotReTriggerItself()
        {
            // El propio reparto dispara otro DamageResolvedPayload (ComboId vacío, Kind
            // ScriptedAbility) — no debe volver a intentar consumir una carga ya limpia.
            var primary = SpawnEnemy(new GridCoord(2, 0), hp: 100);

            _service.Arm(_owner, 1);
            RaiseComboPlayed(primary, "combo.a");
            RaiseDamageResolved(primary, "combo.a", finalDamage: 50);

            Assert.AreEqual(95, _attrs.GetAttribute<Health>(primary).Value, "100 - 5 de bonus, una sola vez.");
            Assert.IsFalse(_service.HasPending(_owner));
        }
    }
}
