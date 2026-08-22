using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Entities;
using Rollgeon.Effects.Selection;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// La mano de dados de La Generala: tira los dados que le queden vivos, la corre por el mismo
    /// detector de combos que la del jugador y la publica en <see cref="IBossDiceHandService"/>.
    /// </summary>
    [TestFixture]
    public class AINode_RollHandTests
    {
        private AttributesManager _attributes;
        private ComboCatalogSO _catalog;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes);

            _catalog = Create<ComboCatalogSO>();
            AddCombo<Combo_Par>(Rollgeon.Combos.ComboId.Par, 10);
            AddCombo<Combo_FullHouse>(Rollgeon.Combos.ComboId.FullHouse, 30);
            AddCombo<Combo_Escalera>(Rollgeon.Combos.ComboId.Straight, 35);
            AddCombo<Combo_Poker>(Rollgeon.Combos.ComboId.Poker, 60);
            AddCombo<Combo_Generala>(Rollgeon.Combos.ComboId.Generala, 100);
            ServiceLocator.AddService<ComboCatalogSO>(_catalog);

            _boss = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _attributes.Dispose();
            foreach (var asset in _created) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();
        }

        // ======================================================================
        // Tirada y detección
        // ======================================================================

        [Test]
        public void Tick_FixedSize_PublishesTheRolledFaces_AndTheDetectedCombo()
        {
            // Arrange — tirada fija [4,4,2,5,1] ⇒ Par.
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            var context = NewContext(new ScriptedRandom(4, 4, 2, 5, 1));

            // Act
            var result = node.Tick(context);

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            var hand = ReadHand();
            Assert.AreEqual(new[] { 4, 4, 2, 5, 1 }, hand.Values);
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, hand.ComboId);
            Assert.IsTrue(hand.Armed, "Un Par arma en el mismo turno en que se tira.");
        }

        [Test]
        public void Tick_NoCombo_PublishesABustHand()
        {
            // Arrange — [1,2,4,5,3] es escalera; [1,2,4,6,3] no forma nada de los registrados.
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            var context = NewContext(new ScriptedRandom(1, 2, 4, 6, 3));

            // Act
            node.Tick(context);

            // Assert
            var hand = ReadHand();
            Assert.IsFalse(hand.HasCombo, $"La mano no debía formar combo, salió '{hand.ComboId}'.");
            Assert.IsTrue(hand.Armed, "El bust también arma: la rama de bust cobra el mínimo.");
        }

        [Test]
        public void Tick_WithFourAliveDice_CannotRollGenerala_AndFallsToPoker()
        {
            // Arrange — cuatro seis: Combo_Generala pide 5 dados en la tirada.
            RegisterAliveDice(4);
            var node = NewNode(AINode_RollHand.HandSizeSource.AliveAllies);
            var context = NewContext(new ScriptedRandom(6, 6, 6, 6));

            // Act
            node.Tick(context);

            // Assert
            var hand = ReadHand();
            Assert.AreEqual(4, hand.DiceCount, "Tira tantos dados como dados vivos le queden.");
            Assert.AreEqual(Rollgeon.Combos.ComboId.Poker, hand.ComboId,
                "Con un dado roto la Generala tiene que dejar de existir.");
        }

        [Test]
        public void Tick_WithThreeAliveDice_LosesPokerToo()
        {
            // Arrange
            RegisterAliveDice(3);
            var node = NewNode(AINode_RollHand.HandSizeSource.AliveAllies);
            var context = NewContext(new ScriptedRandom(6, 6, 6));

            // Act
            node.Tick(context);

            // Assert — Trío no está en la tabla de la Generala, así que tres iguales bajan a Par.
            var hand = ReadHand();
            Assert.AreEqual(3, hand.DiceCount);
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, hand.ComboId,
                "Con dos dados rotos se le cae el Póker.");
        }

        [Test]
        public void Tick_WithTheWholeTableBroken_PublishesAnEmptyBustHand()
        {
            // Arrange
            RegisterAliveDice(0);
            var node = NewNode(AINode_RollHand.HandSizeSource.AliveAllies);

            // Act
            var result = node.Tick(NewContext(new ScriptedRandom(6, 6, 6, 6, 6)));

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result, "Sin dados el turno sigue, no falla.");
            var hand = ReadHand();
            Assert.AreEqual(0, hand.DiceCount);
            Assert.IsFalse(hand.HasCombo);
            Assert.IsTrue(hand.Armed);
        }

        // ======================================================================
        // La ronda extra de aviso
        // ======================================================================

        [Test]
        public void Tick_Generala_IsCalledButNotArmed_SoNothingMarksThatTurn()
        {
            // Arrange — Generala está en SlowCombos.
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);

            // Act
            node.Tick(NewContext(new ScriptedRandom(6, 6, 6, 6, 6)));

            // Assert
            var hand = ReadHand();
            Assert.AreEqual(Rollgeon.Combos.ComboId.Generala, hand.ComboId);
            Assert.IsFalse(hand.Armed, "La mano grande se canta una ronda antes de armarse.");
        }

        [Test]
        public void Tick_TurnAfterCallingGenerala_ArmsTheSameHandWithoutRerolling()
        {
            // Arrange
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            node.Tick(NewContext(new ScriptedRandom(6, 6, 6, 6, 6)));

            // Act — el turno siguiente traería otra tirada; la mano cantada no se re-tira.
            node.Tick(NewContext(new ScriptedRandom(1, 1, 2, 2, 3)));

            // Assert
            var hand = ReadHand();
            Assert.AreEqual(new[] { 6, 6, 6, 6, 6 }, hand.Values,
                "Los dados siguen sobre la mesa: la ronda extra de aviso muestra la misma mano.");
            Assert.AreEqual(Rollgeon.Combos.ComboId.Generala, hand.ComboId);
            Assert.IsTrue(hand.Armed);
        }

        [Test]
        public void Tick_ThirdTurnAfterGenerala_RollsAFreshHand()
        {
            // Arrange — turno 1 canta, turno 2 arma.
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            node.Tick(NewContext(new ScriptedRandom(6, 6, 6, 6, 6)));
            node.Tick(NewContext(new ScriptedRandom(9, 9, 9, 9, 9)));

            // Act — turno 3: la mano armada ya detonó, se vuelve a tirar.
            node.Tick(NewContext(new ScriptedRandom(4, 4, 2, 5, 1)));

            // Assert
            var hand = ReadHand();
            Assert.AreEqual(new[] { 4, 4, 2, 5, 1 }, hand.Values);
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, hand.ComboId);
        }

        // ======================================================================
        // Reroll (Fase 2)
        // ======================================================================

        [Test]
        public void Tick_WithoutReroll_KeepsTheFirstRoll()
        {
            // Arrange — la segunda tanda de caras no se usa.
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            var context = NewContext(new ScriptedRandom(4, 4, 2, 5, 1, 4, 4, 4));

            // Act
            node.Tick(context);

            // Assert
            Assert.AreEqual(new[] { 4, 4, 2, 5, 1 }, ReadHand().Values);
        }

        [Test]
        public void Tick_WithReroll_RerollsOnlyTheNonContributingDice_AndUpgradesTheHand()
        {
            // Arrange — [4,4,2,5,1] es Par (índices 0 y 1 contribuyen); el reroll cambia los otros
            // tres a 4 y la mano pasa a Generala.
            BossDiceHandService.ResolveOrCreate().SetRerollsPerRound(_boss, 1);
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            var context = NewContext(new ScriptedRandom(4, 4, 2, 5, 1, 4, 4, 4));

            // Act
            node.Tick(context);

            // Assert
            var hand = ReadHand();
            Assert.AreEqual(new[] { 4, 4, 4, 4, 4 }, hand.Values,
                "El reroll conserva los dados que forman el combo y re-tira el resto.");
            Assert.AreEqual(Rollgeon.Combos.ComboId.Generala, hand.ComboId);
        }

        [Test]
        public void Tick_WithReroll_KeepsTheBetterHand_WhenTheRerollComesOutWorse()
        {
            // Arrange — [3,3,3,3,1] es Póker; re-tirando el quinto sale un 2 y sigue siendo Póker.
            BossDiceHandService.ResolveOrCreate().SetRerollsPerRound(_boss, 1);
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            var context = NewContext(new ScriptedRandom(3, 3, 3, 3, 1, 2));

            // Act
            node.Tick(context);

            // Assert
            Assert.AreEqual(Rollgeon.Combos.ComboId.Poker, ReadHand().ComboId);
        }

        [Test]
        public void SetHandReroll_EnablesTheRerollForTheOwner()
        {
            // Arrange
            var node = new AINode_SetHandReroll { RerollsPerRound = 1 };

            // Act
            var result = node.Tick(NewContext(new ScriptedRandom()));

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, BossDiceHandService.ResolveOrCreate().GetRerollsPerRound(_boss));
        }

        // ======================================================================
        // Estado por pelea
        // ======================================================================

        [Test]
        public void Hands_AreForgottenOnCombatEnd_SoANewFightStartsWithoutAHand()
        {
            // Arrange
            var node = NewNode(AINode_RollHand.HandSizeSource.Fixed);
            node.Tick(NewContext(new ScriptedRandom(4, 4, 2, 5, 1)));
            var hands = BossDiceHandService.ResolveOrCreate();
            Assert.IsTrue(hands.TryGetHand(_boss, out _));

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.IsFalse(hands.TryGetHand(_boss, out _),
                "La mano es estado de pelea: no debe sobrevivir al fin del combate.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static AINode_RollHand NewNode(AINode_RollHand.HandSizeSource source) => new AINode_RollHand
        {
            SizeSource = source,
            MaxDice = 5,
            DieFaces = 6,
            SlowCombos = new List<string> { Rollgeon.Combos.ComboId.Generala },
        };

        private AIContext NewContext(System.Random rng) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = Guid.NewGuid(),
            Attributes = _attributes,
            Rng = rng,
        };

        private BossDiceHand ReadHand()
        {
            Assert.IsTrue(BossDiceHandService.ResolveOrCreate().TryGetHand(_boss, out var hand),
                "El nodo no publicó ninguna mano.");
            return hand;
        }

        /// <summary><paramref name="alive"/> dados vivos + 1 roto, como aliados del boss.</summary>
        private void RegisterAliveDice(int alive)
        {
            var allies = new List<Guid>();
            for (int i = 0; i < alive; i++)
                allies.Add(RegisterDie(hp: 4));

            // CombatDeathWatcher deja el dado roto registrado con HP 0, y no es tirable.
            allies.Add(RegisterDie(hp: 0));

            ServiceLocator.AddService<IEntityQueryService>(new StubEntityQueryService(allies));
        }

        private Guid RegisterDie(int hp)
        {
            var die = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            _attributes.Register(die, attrs);
            return die;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        private void AddCombo<T>(string comboId, int baseDamage) where T : BaseComboSO
        {
            var combo = Create<T>();
            SetPrivateField(combo, "_comboId", comboId);
            SetPrivateField(combo, "_baseDamage", baseDamage);
            // Priority no defaultea a _baseDamage: sin esto todo empata en 0.
            SetPrivateField(combo, "_priority", baseDamage);
            _catalog.EditorAdd(combo);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"No existe el campo '{fieldName}' en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        /// <summary>RNG con las caras escriteadas.</summary>
        private sealed class ScriptedRandom : System.Random
        {
            private readonly Queue<int> _faces;

            public ScriptedRandom(params int[] faces) => _faces = new Queue<int>(faces);

            public override int Next(int minValue, int maxValue)
                => _faces.Count > 0 ? _faces.Dequeue() : minValue;

            public override int Next(int maxValue) => 0;

            public override double NextDouble() => 0d;
        }

        private sealed class StubEntityQueryService : IEntityQueryService
        {
            private readonly List<Guid> _allies;

            public StubEntityQueryService(List<Guid> allies) => _allies = allies;

            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Array.Empty<Entity>();

            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid)
            {
                var result = new List<Entity>(_allies.Count);
                foreach (var guid in _allies) result.Add(new Entity { Guid = guid });
                return result;
            }

            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Allies;
        }
    }
}
