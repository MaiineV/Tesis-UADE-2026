using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    // El sorteo y el módulo de índices ya los cubre AINode_RotateBlockDirectedTests: acá importa de
    // cuál lista sale el número, porque ConsumeWindup() vacía el windup.
    [TestFixture]
    public class CroupierConfiscationTests
    {
        private const int BagSize = 5;

        private CroupierWheelService _wheel;
        private DiceBlockService _blocks;
        private StubPlayerService _player;
        private DiceBagSO _bag;
        private Guid _bossGuid;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.hideFlags = HideFlags.HideAndDontSave;
            // Uno de cada tipo: ningún tipo pasa su MaxPerBag, que sólo loguearía warnings.
            _bag.Dice = new List<DiceType> { DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12 };
            Assert.AreEqual(BagSize, _bag.Dice.Count, "El módulo del índice se mide contra esta bolsa.");

            _playerGuid = Guid.NewGuid();
            _player = new StubPlayerService { Guid = _playerGuid, Bag = _bag };
            ServiceLocator.AddService<IPlayerService>(_player);

            _blocks = new DiceBlockService();
            _blocks.Register();

            _bossGuid = Guid.NewGuid();
            _wheel = (CroupierWheelService)CroupierWheelService.ResolveOrCreate();
            _wheel.Bind(_bossGuid);
        }

        [TearDown]
        public void TearDown()
        {
            _wheel.Dispose();
            _blocks.Dispose();
            if (_bag != null) UnityEngine.Object.DestroyImmediate(_bag);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Detonated_ReadsTheNumberThatJustFell_AsASlotIndex()
        {
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();

            var reader = Reader(AIReadCroupierWheelNumber.NumberSource.Detonated);

            // Número 3 → slot 2: la bolsa es 0-based y el paño 1-based.
            Assert.AreEqual(2, reader.Read(Context()));
        }

        [Test]
        public void Sung_GoesBlindOnceTheWindupIsConsumed()
        {
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();

            var sung = Reader(AIReadCroupierWheelNumber.NumberSource.Sung);

            Assert.AreEqual(-1, sung.Read(Context()),
                "Después de ConsumeWindup el windup está vacío: Sung no puede saber qué cayó.");
        }

        [Test]
        public void Detonated_IsMinusOne_BeforeAnythingHasFallen()
        {
            _wheel.Sing(new List<int> { 3 });

            var reader = Reader(AIReadCroupierWheelNumber.NumberSource.Detonated);

            Assert.AreEqual(-1, reader.Read(Context()),
                "Turno 1: se cantó pero todavía no detonó nada. -1 = no confisques.");
        }

        [Test]
        public void Detonated_SecondSlot_IsThePhaseTwoSeam()
        {
            _wheel.Sing(new List<int> { 3, 6 });
            _wheel.ConsumeWindup();

            Assert.AreEqual(2, Reader(AIReadCroupierWheelNumber.NumberSource.Detonated, slot: 0).Read(Context()));
            Assert.AreEqual(5, Reader(AIReadCroupierWheelNumber.NumberSource.Detonated, slot: 1).Read(Context()));
        }

        [Test]
        public void Node_BlocksTheDieOfTheNumberThatFell()
        {
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();

            var result = BuildNode().Tick(Context());

            Assert.AreEqual(AIResult.Succeeded, result);
            CollectionAssert.AreEquivalent(new[] { 2 }, _blocks.BlockedIndices);
        }

        [Test]
        public void Node_BlocksNothing_OnATurnWithNothingDetonated()
        {
            _wheel.Sing(new List<int> { 3 }); // Cantado, todavía sin detonar.

            var result = BuildNode().Tick(Context());

            Assert.AreEqual(AIResult.Succeeded, result,
                "Un turno sin detonación es una resolución válida, no un fallo que corte el turno.");
            CollectionAssert.IsEmpty(_blocks.BlockedIndices,
                "Sin número que haya caído no se confisca nada — bloquear al azar acá sería un " +
                "candado que el jugador no puede leer en pantalla.");
        }

        [Test]
        public void Node_TakesTheSixthSectorAroundTheBag_InsteadOfPilingOnTheLastDie()
        {
            _wheel.Sing(new List<int> { 6 });
            _wheel.ConsumeWindup();

            BuildNode().Tick(Context());

            // 6 → índice 5 → 5 % 5 = 0. Clampear le daría al último dado el doble de chances.
            CollectionAssert.AreEquivalent(new[] { 0 }, _blocks.BlockedIndices);
        }

        [Test]
        public void Node_LabelsThePadlockWithTheNumberThatWasSung_NotTheSlotIndex()
        {
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();

            BuildNode().Tick(Context());

            Assert.AreEqual("3", _blocks.LabelOf(2),
                "Con el índice crudo el candado diría '2' para el 3 que salió en la ruleta, que es " +
                "peor que no decir nada.");
        }

        [Test]
        public void Node_LabelSurvivesTheModulo_SoTheSixStillSaysSix()
        {
            _wheel.Sing(new List<int> { 6 });
            _wheel.ConsumeWindup();

            BuildNode().Tick(Context());

            Assert.AreEqual("6", _blocks.LabelOf(0));
        }

        [Test]
        public void Node_ConfiscatesFreshEachTurn_SoTheBlockNeverAccumulates()
        {
            _wheel.Sing(new List<int> { 3 });
            _wheel.ConsumeWindup();
            BuildNode().Tick(Context());

            _wheel.Sing(new List<int> { 1 });
            _wheel.ConsumeWindup();
            BuildNode().Tick(Context());

            CollectionAssert.AreEquivalent(new[] { 0 }, _blocks.BlockedIndices,
                "El bloqueo del turno pasado no se acumula: cada turno confisca uno solo.");
        }

        private static AIReadCroupierWheelNumber Reader(
            AIReadCroupierWheelNumber.NumberSource source, int slot = 0) =>
            new AIReadCroupierWheelNumber { Source = source, Slot = slot };

        private static AINode_RotateBlock BuildNode() => new AINode_RotateBlock
        {
            Target = AINode_RotateBlock.BlockTarget.Dice,
            DirectedIndex = Reader(AIReadCroupierWheelNumber.NumberSource.Detonated),
        };

        private AIContext Context() => new AIContext
        {
            SelfGuid = _bossGuid,
            PlayerGuid = _playerGuid,
            PlayerService = _player,
        };

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid Guid;
            public DiceBagSO Bag;

            public Guid PlayerGuid => Guid;
            public Guid RunId => System.Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => Bag;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) => Bag = bag;
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
