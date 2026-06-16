using System;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PCComboAvailableTests
    {
        private const string ComboIdA = "test.combo.a";
        private const string ComboIdB = "test.combo.b";

        private ClassHeroSO _hero;
        private BaseComboSO _comboA;
        private BaseComboSO _comboB;
        private FakePlayerService _player;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _comboA = CreateCombo<Combo_Par>(ComboIdA, 1);
            _comboB = CreateCombo<Combo_Par>(ComboIdB, 2);

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.Sheet = new ContractSheet();
            _hero.Sheet.Combos.Clear();
            _hero.Sheet.Combos.Add(_comboA);
            _hero.Sheet.Combos.Add(_comboB);

            _player = new FakePlayerService { CurrentHero = _hero };
            ServiceLocator.AddService<IPlayerService>(_player);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            if (_hero != null) UnityEngine.Object.DestroyImmediate(_hero);
            if (_comboA != null) UnityEngine.Object.DestroyImmediate(_comboA);
            if (_comboB != null) UnityEngine.Object.DestroyImmediate(_comboB);
        }

        private static T CreateCombo<T>(string comboId, int baseDamage) where T : BaseComboSO
        {
            var instance = ScriptableObject.CreateInstance<T>();
            SetField(instance, "_comboId", comboId);
            SetField(instance, "_baseDamage", baseDamage);
            return instance;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static PreConditionContext Ctx() => new PreConditionContext();

        [Test]
        public void Evaluate_ComboPresent_ReturnsTrue()
        {
            var pc = new PCComboAvailable { ComboId = ComboIdA };
            Assert.IsTrue(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_ComboCrossed_ReturnsFalse()
        {
            _hero.Sheet.CrossCombo(_comboA);
            var pc = new PCComboAvailable { ComboId = ComboIdA };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_OtherComboCrossed_DoesNotAffectThis()
        {
            _hero.Sheet.CrossCombo(_comboB);
            var pc = new PCComboAvailable { ComboId = ComboIdA };
            Assert.IsTrue(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_ComboNotInSheet_ReturnsFalse()
        {
            var pc = new PCComboAvailable { ComboId = "test.combo.unknown" };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_EmptyComboId_ReturnsFalse()
        {
            var pc = new PCComboAvailable { ComboId = "" };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_NoPlayerService_ReturnsFalse()
        {
            ServiceLocator.Clear();
            var pc = new PCComboAvailable { ComboId = ComboIdA };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        [Test]
        public void Evaluate_NullCurrentHero_ReturnsFalse()
        {
            _player.CurrentHero = null;
            var pc = new PCComboAvailable { ComboId = ComboIdA };
            Assert.IsFalse(pc.Evaluate(Ctx()));
        }

        // ----------------------------------------------------------------
        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.Empty;
            public Guid RunId { get; set; } = Guid.Empty;
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }

            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;

            public void SetPlayer(ClassHeroSO hero, Guid runId)
            {
                CurrentHero = hero;
                RunId = runId;
                OnPlayerSet?.Invoke(hero);
            }

            public void SetDiceBag(DiceBagSO bag) => DiceBag = bag;

            public void ClearPlayer()
            {
                CurrentHero = null;
                OnPlayerCleared?.Invoke();
            }
        }
    }
}
