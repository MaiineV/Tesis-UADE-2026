using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Dice;
using Rollgeon.Feedback;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_RotateBlock.AnnounceOnce"/>: el candado del dado tiene que seguir
    /// trabando todos los turnos, pero el VFX/Feel de "te confisqué un dado" sólo puede salir la
    /// primera vez que la instancia del nodo bloquea algo.
    /// </summary>
    [TestFixture]
    public class AINode_RotateBlockAnnounceOnceTests
    {
        private const int BagSize = 5;

        private DiceBlockService _dice;
        private FakeFeedbackService _feedback;
        private StubPlayerService _playerService;
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
            _bag.Dice = new List<DiceType> { DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12 };
            Assert.AreEqual(BagSize, _bag.Dice.Count, "El sorteo de estos tests se mide contra esta bolsa.");

            _playerGuid = Guid.NewGuid();
            _bossGuid = Guid.NewGuid();
            _playerService = new StubPlayerService { Guid = _playerGuid, Bag = _bag };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            _dice = new DiceBlockService();
            _dice.Register();

            _feedback = new FakeFeedbackService();
            ServiceLocator.AddService<IFeedbackService>(_feedback);
        }

        [TearDown]
        public void TearDown()
        {
            _dice.Dispose();
            if (_bag != null) UnityEngine.Object.DestroyImmediate(_bag);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void AnnounceOnce_ThreeTicks_AnnouncesOnce_ButBlocksAllThree()
        {
            var node = BuildNode(announceOnce: true);

            for (int turn = 0; turn < 3; turn++)
            {
                var result = RunCoroutine(node, Context());
                Assert.AreEqual(AIResult.Succeeded, result);
                Assert.AreEqual(1, _dice.BlockedIndices.Count, $"Turno {turn}: el candado tiene que seguir trabando.");
            }

            Assert.AreEqual(1, _feedback.RequestCount,
                "Con AnnounceOnce el cartel de confiscación sólo puede salir en la primera emisión.");
        }

        [Test]
        public void AnnounceOnceFalse_ThreeTicks_AnnouncesEveryTime()
        {
            var node = BuildNode(announceOnce: false);

            for (int turn = 0; turn < 3; turn++)
                RunCoroutine(node, Context());

            Assert.AreEqual(3, _feedback.RequestCount,
                "Default histórico: sin AnnounceOnce el cartel sale todos los turnos.");
        }

        [Test]
        public void FirstEmissionBlocksNothing_DoesNotConsumeTheAnnounce()
        {
            // El reader devuelve -1 en el primer turno (nada que cantar todavía) y recién confisca
            // desde el segundo: ese segundo turno tiene que ser el que avisa, no quedarse mudo.
            var index = new SequenceIntReader(-1, 2, 2);
            var node = new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Dice,
                DirectedIndex = index,
                BlockVfxId = "vfx_block",
                BlockFeelId = "feel_block",
                AnnounceOnce = true,
            };

            RunCoroutine(node, Context()); // Turno 1: no bloquea nada.
            Assert.IsEmpty(_dice.BlockedIndices);
            Assert.AreEqual(0, _feedback.RequestCount);

            RunCoroutine(node, Context()); // Turno 2: primera confiscación real.
            Assert.AreEqual(1, _dice.BlockedIndices.Count);
            Assert.AreEqual(1, _feedback.RequestCount, "El primer bloqueo real es el que tiene que avisar.");

            RunCoroutine(node, Context()); // Turno 3: sigue trabando, en silencio.
            Assert.AreEqual(1, _dice.BlockedIndices.Count);
            Assert.AreEqual(1, _feedback.RequestCount, "El segundo bloqueo real ya no vuelve a avisar.");
        }

        private static AINode_RotateBlock BuildNode(bool announceOnce) => new AINode_RotateBlock
        {
            Target = AINode_RotateBlock.BlockTarget.Dice,
            Count = 1,
            BlockVfxId = "vfx_block",
            BlockFeelId = "feel_block",
            AnnounceOnce = announceOnce,
        };

        private AIContext Context() => new AIContext
        {
            SelfGuid = _bossGuid,
            PlayerGuid = _playerGuid,
            PlayerService = _playerService,
            Rng = new System.Random(1),
        };

        private static AIResult RunCoroutine(AINode_RotateBlock node, AIContext context)
        {
            var result = AIResult.Failed;
            var routine = node.TickCoroutine(context, r => result = r);
            while (routine.MoveNext()) { }
            return result;
        }

        /// <summary>Reader de prueba: devuelve valores fijos en orden, y repite el último al agotarse.</summary>
        private sealed class SequenceIntReader : AIIntReader
        {
            private readonly int[] _values;
            private int _cursor;

            public SequenceIntReader(params int[] values) => _values = values;

            public override int Read(AIContext context)
            {
                int value = _values[Math.Min(_cursor, _values.Length - 1)];
                _cursor++;
                return value;
            }
        }

        private sealed class FakeFeedbackService : IFeedbackService
        {
            public int RequestCount { get; private set; }

            public void RequestFeedbackBlocking(FeedbackRequest request, Action onComplete)
            {
                RequestCount++;
                onComplete?.Invoke();
            }
        }

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
