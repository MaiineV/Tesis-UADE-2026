using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    [TestFixture]
    public class EffAddShieldTests
    {
        private AttributesManager _attrManager;
        private Guid _sourceId;
        private readonly List<ScriptableObject> _createdObjects = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attrManager = new AttributesManager();
            _sourceId = Guid.NewGuid();

            var sourceAttrs = new ModifiableAttributes();
            sourceAttrs.EnsureInitialized();
            sourceAttrs.SetAttribute<Shield>(new Shield(0));
            _attrManager.Register(_sourceId, sourceAttrs);

            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);
            AttributesManager.LogMissingEntityAsWarning = true;
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            foreach (var so in _createdObjects)
                if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _createdObjects.Clear();
        }

        // ── Constant source ─────────────────────────────────────────────

        [Test]
        public void Constant_AddsShieldToTarget()
        {
            var eff = CreateConstantEffect(10);
            var ctx = MakeCtx();

            eff.ApplyEffect(ctx);

            Assert.AreEqual(10, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void Constant_AddsToExistingShield()
        {
            _attrManager.SetAttributeValue<Shield, int>(_sourceId, 5);
            var eff = CreateConstantEffect(10);
            var ctx = MakeCtx();

            eff.ApplyEffect(ctx);

            Assert.AreEqual(15, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void Constant_FiresOnShieldChangedEvent()
        {
            var eff = CreateConstantEffect(7);
            var ctx = MakeCtx();

            int capturedShield = -1;
            Guid capturedGuid = Guid.Empty;
            EventManager.Subscribe(EventName.OnShieldChanged, args =>
            {
                capturedGuid = (Guid)args[0];
                capturedShield = (int)args[1];
            });

            eff.ApplyEffect(ctx);

            Assert.AreEqual(_sourceId, capturedGuid);
            Assert.AreEqual(7, capturedShield);
        }

        [Test]
        public void Constant_StoresFloatingShieldOnBehavior()
        {
            var bh = new TestBehavior();
            var eff = CreateConstantEffect(12);
            var ctx = MakeCtx(bh);

            eff.ApplyEffect(ctx);

            Assert.IsTrue(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingShield, out var list));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(12f, list[0].Value);
            Assert.AreEqual(_sourceId, list[0].TargetEntityGuid);
        }

        [Test]
        public void ZeroAmount_ReturnsTrue_NoSideEffects()
        {
            var eff = CreateConstantEffect(0);
            var ctx = MakeCtx();

            bool result = eff.ApplyEffect(ctx);

            Assert.IsTrue(result);
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void NullContext_ReturnsFalse()
        {
            var eff = CreateConstantEffect(5);

            bool result = eff.ApplyEffect(null);

            Assert.IsFalse(result);
        }

        [Test]
        public void NoSelection_FallsBackToSourceGuid()
        {
            var eff = CreateConstantEffect(20);
            var ctx = MakeCtx();
            ctx.SelectionResult = null;

            eff.ApplyEffect(ctx);

            Assert.AreEqual(20, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        // ── ComboValue source ───────────────────────────────────────────

        // Spec Escudo v2: el escudo sale de la tabla escudo_combo_base del ContractSheet,
        // NUNCA del BaseDamage del ComboResult (tabla de ATAQUE — regression BUG-021).
        [Test]
        public void ComboValue_UsesShieldTable_IgnoresAttackBaseDamage()
        {
            // Arrange — ComboResult trae BaseDamage 99 (ataque); la tabla de escudo dice 4.
            var bh = new TestBehavior();
            var ctx = MakeCtx(bh);
            ctx.ComboResult = ComboDetectionResult.Match("combo.par", 99, 2, null);
            RegisterHeroWithShieldTable(shieldBase: 4);

            var eff = CreateComboEffect(1.5f); // multiplier deprecado — no debe participar

            // Act
            eff.ApplyEffect(ctx);

            // Assert — 4 × multi 1.0 (sin dice service) = 4; jamás 99 ni 99×1.5
            Assert.AreEqual(4, _attrManager.GetAttribute<Shield>(_sourceId).Value);
            Assert.IsTrue(bh.TryGetBehaviorValues<FloatingNumberBehaviorValue>(
                BehaviorValueKey.FloatingShield, out var list));
            Assert.AreEqual(4f, list[0].Value);
        }

        [Test]
        public void ComboValue_NoShieldTableEntry_AddsZero()
        {
            // Arrange — combo matcheado pero la clase no define escudo para ese combo
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.Match("combo.par", 90, 5, null);
            RegisterHeroWithShieldTable(shieldBase: null);

            var eff = CreateComboEffect(1f);

            // Act
            bool result = eff.ApplyEffect(ctx);

            // Assert — sin entrada = 0 escudo (fallback explícito, no deriva del daño)
            Assert.IsTrue(result);
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void ComboValue_NeverExceedsShieldCap()
        {
            // Arrange — base tabla 50: 50 × 1.0 = 50 → cap
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.Match("combo.par", 50, 2, null);
            RegisterHeroWithShieldTable(shieldBase: 50);

            var eff = CreateComboEffect(1f);

            // Act
            eff.ApplyEffect(ctx);

            // Assert
            Assert.AreEqual(PlayerComboShield.ShieldCap,
                _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void ComboValue_NoPlayerService_AddsZero()
        {
            // Arrange — sin IPlayerService no hay sheet → no hay tabla → 0
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.Match("combo.par", 20, 2, null);

            var eff = CreateComboEffect(1f);

            // Act
            bool result = eff.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(result, "Amount 0 is a no-op, not a chain failure");
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void ComboValue_SyntheticResultWithoutComboId_AddsZero()
        {
            // Arrange — resultado sintético (factory legacy, ej. action rolls): sin ComboId
            // no hay lookup en la tabla de escudo, aunque el hero la tenga configurada.
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.Match(30, 2);
            RegisterHeroWithShieldTable(shieldBase: 4);

            var eff = CreateComboEffect(1f);

            // Act
            bool result = eff.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(result, "Amount 0 is a no-op, not a chain failure");
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        // ComboValue sin combo matched → 0 (NO cae al _baseAmount): caer al base
        // defeateaba el bloqueo de combos (boss inmune a Par seguía recibiendo el
        // _baseAmount). Contrato cambiado en 23e162a y propagado en 8f820c7.
        [Test]
        public void ComboValue_AddsNoShield_WhenNoCombo()
        {
            var eff = CreateComboEffect(2f, baseAmount: 8);
            var ctx = MakeCtx();
            ctx.ComboResult = null;

            bool result = eff.ApplyEffect(ctx);

            Assert.IsTrue(result, "Amount 0 is a no-op, not a chain failure");
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void ComboValue_AddsNoShield_WhenComboNoMatch()
        {
            var eff = CreateComboEffect(2f, baseAmount: 5);
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.NoMatch();

            bool result = eff.ApplyEffect(ctx);

            Assert.IsTrue(result, "Amount 0 is a no-op, not a chain failure");
            Assert.AreEqual(0, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        [Test]
        public void ComboValue_AddsToExistingShield()
        {
            // Arrange
            _attrManager.SetAttributeValue<Shield, int>(_sourceId, 10);
            var ctx = MakeCtx();
            ctx.ComboResult = ComboDetectionResult.Match("combo.par", 15, 2, null);
            RegisterHeroWithShieldTable(shieldBase: 5);

            var eff = CreateComboEffect(1f);

            // Act
            eff.ApplyEffect(ctx);

            // Assert — 10 existente + 5 de la tabla de escudo (no 15 del ataque)
            Assert.AreEqual(15, _attrManager.GetAttribute<Shield>(_sourceId).Value);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        // Hero con contrato mínimo [Par] y (opcional) entrada de escudo para combo.par.
        // Los SOs creados se destruyen en TearDown.
        private void RegisterHeroWithShieldTable(int? shieldBase)
        {
            var par = ScriptableObject.CreateInstance<Combo_Par>();
            SetField(par, "_comboId", "combo.par");
            _createdObjects.Add(par);

            var sheet = new ContractSheet();
            sheet.Combos.Add(par);
            // Ruido deliberado: la tabla de DAÑO siempre presente y alta, para que
            // cualquier regresión que derive escudo del daño reviente estos tests.
            sheet.BaseDamageTable.Add(new ComboBaseDamageEntry
            {
                ComboId = "combo.par", BaseDamage = 99,
            });
            if (shieldBase.HasValue)
            {
                sheet.ShieldBaseTable.Add(new ComboShieldBaseEntry
                {
                    ComboId = "combo.par", ShieldBase = shieldBase.Value,
                });
            }

            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.Sheet = sheet;
            _createdObjects.Add(hero);

            ServiceLocator.AddService<IPlayerService>(
                new StubPlayerService { CurrentHero = hero }, ServiceScope.Run);
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public Rollgeon.Dice.DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
#pragma warning disable 67
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }

        private EffAddShield CreateConstantEffect(int amount)
        {
            var eff = new EffAddShield();
            SetField(eff, "_shieldSource", DamageSource.Constant);
            SetField(eff, "_baseAmount", amount);
            return eff;
        }

        private EffAddShield CreateComboEffect(float multiplier, int baseAmount = 0)
        {
            var eff = new EffAddShield();
            SetField(eff, "_shieldSource", DamageSource.ComboValue);
            SetField(eff, "_comboMultiplier", multiplier);
            SetField(eff, "_baseAmount", baseAmount);
            return eff;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private EffectContext MakeCtx(BaseBehavior bh = null)
        {
            return new EffectContext
            {
                SourceGuid = _sourceId,
                TargetGuid = Guid.Empty,
                lastResult = true,
                SourceBehavior = bh,
            };
        }
    }
}
