using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Random = System.Random;

namespace Rollgeon.Entities.Bosses.Tests
{
    /// <summary>
    /// Cobertura de <see cref="BossPoolSO"/>: el roulette wheel respeta los pesos, las dos
    /// palancas de apagado (Weight = 0 / Enabled = off) filtran, la invariante "≥1 boss
    /// activo por piso" se sostiene con el fallback, y un pool sin autorar devuelve
    /// <c>null</c> para que el resolver caiga a su path de siempre.
    /// </summary>
    [TestFixture]
    public class BossPoolSOTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ---- Helpers --------------------------------------------------------

        private EnemyDataSO MakeBoss(string entityId)
        {
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            boss.name = entityId;
            boss.EntityId = entityId;
            _created.Add(boss);
            return boss;
        }

        private BossPoolSO MakePool(params WeightedBoss[] entries)
        {
            var pool = ScriptableObject.CreateInstance<BossPoolSO>();
            pool.name = "TestBossPool";
            pool.Entries = new List<WeightedBoss>(entries);
            _created.Add(pool);
            return pool;
        }

        private static WeightedBoss Entry(EnemyDataSO boss, float weight = 1f, bool enabled = true)
            => new WeightedBoss { Boss = boss, Weight = weight, Enabled = enabled };

        // ---- Pool vacío / sin autorar ---------------------------------------

        [Test]
        public void Roll_EmptyPool_ReturnsNull()
        {
            // Arrange
            var pool = MakePool();

            // Act
            var result = pool.Roll(new Random(42));

            // Assert — null = "no hay pool", el resolver usa el path de spawn previo.
            Assert.IsNull(result);
        }

        [Test]
        public void Roll_EntriesWithoutBoss_ReturnsNull()
        {
            var pool = MakePool(Entry(boss: null), Entry(boss: null, weight: 5f));

            var result = pool.Roll(new Random(42));

            Assert.IsNull(result);
        }

        // ---- Filtros: las dos palancas de apagado ---------------------------

        [Test]
        public void Roll_WeightZero_SkipsEntry()
        {
            // Arrange
            var active = MakeBoss("boss.active");
            var disabled = MakeBoss("boss.disabled");
            var pool = MakePool(Entry(active, 1f), Entry(disabled, 0f));
            var rng = new Random(42);

            // Act / Assert — con un solo elegible, todos los rolls lo devuelven.
            for (int i = 0; i < 20; i++)
            {
                Assert.AreSame(active, pool.Roll(rng));
            }
        }

        [Test]
        public void Roll_EnabledFalse_SkipsEntryEvenWithWeight()
        {
            // Arrange — peso alto pero apagado por contenido: no debe salir nunca.
            var active = MakeBoss("boss.active");
            var offline = MakeBoss("boss.offline");
            var pool = MakePool(Entry(active, 1f), Entry(offline, 99f, enabled: false));
            var rng = new Random(7);

            // Act / Assert
            for (int i = 0; i < 20; i++)
            {
                Assert.AreSame(active, pool.Roll(rng));
            }
        }

        [Test]
        public void ActiveBosses_ExcludesZeroWeightAndDisabledEntries()
        {
            // Arrange — el layout de piso 2/3 del diseño: 2 activos + 1 desactivado.
            var a = MakeBoss("boss.a");
            var b = MakeBoss("boss.b");
            var off = MakeBoss("boss.off");
            var zero = MakeBoss("boss.zero");
            var pool = MakePool(
                Entry(a, 1f),
                Entry(b, 2f),
                Entry(off, 1f, enabled: false),
                Entry(zero, 0f));

            // Act
            var active = pool.ActiveBosses();

            // Assert
            CollectionAssert.AreEqual(new[] { a, b }, active);
        }

        // ---- Distribución ---------------------------------------------------

        [Test]
        public void Roll_RespectsRelativeWeights()
        {
            // Arrange — 3:1 sobre 2000 rolls; el margen es holgado para que el test no
            // sea flaky, pero suficiente para detectar un roulette invertido o uniforme.
            var heavy = MakeBoss("boss.heavy");
            var light = MakeBoss("boss.light");
            var pool = MakePool(Entry(heavy, 3f), Entry(light, 1f));
            var rng = new Random(1234);

            // Act
            int heavyHits = 0;
            const int rolls = 2000;
            for (int i = 0; i < rolls; i++)
            {
                if (ReferenceEquals(pool.Roll(rng), heavy)) heavyHits++;
            }

            // Assert — esperado 75%.
            float ratio = heavyHits / (float)rolls;
            Assert.That(ratio, Is.InRange(0.70f, 0.80f),
                $"peso 3:1 debería dar ~75% al boss pesado, dio {ratio:P1}.");
        }

        [Test]
        public void Roll_SameSeed_ReturnsSameSequence()
        {
            // El boss tiene que ser estable por seed/sala — el resolver deriva el rng del
            // roomInstanceId y no queremos que re-entrar cambie el boss.
            var a = MakeBoss("boss.a");
            var b = MakeBoss("boss.b");
            var c = MakeBoss("boss.c");
            var pool = MakePool(Entry(a, 1f), Entry(b, 1f), Entry(c, 1f));

            var first = new List<EnemyDataSO>();
            var second = new List<EnemyDataSO>();
            var rng1 = new Random(99);
            var rng2 = new Random(99);
            for (int i = 0; i < 10; i++)
            {
                first.Add(pool.Roll(rng1));
                second.Add(pool.Roll(rng2));
            }

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void Roll_AllEntriesActive_CanReturnEachOfThem()
        {
            // Piso 1 del diseño: 3 bosses, todos alcanzables.
            var a = MakeBoss("boss.a");
            var b = MakeBoss("boss.b");
            var c = MakeBoss("boss.c");
            var pool = MakePool(Entry(a, 1f), Entry(b, 1f), Entry(c, 1f));
            var rng = new Random(2024);

            var seen = new HashSet<EnemyDataSO>();
            for (int i = 0; i < 200; i++) seen.Add(pool.Roll(rng));

            CollectionAssert.AreEquivalent(new[] { a, b, c }, seen);
        }

        // ---- Invariante ≥1 boss activo --------------------------------------

        [Test]
        public void Roll_NoActiveEntries_FallsBackToFirstAuthoredBossWithWarning()
        {
            // Arrange — todo apagado por error de autorado: la sala NO puede quedar vacía.
            var first = MakeBoss("boss.first");
            var second = MakeBoss("boss.second");
            var pool = MakePool(Entry(first, 0f), Entry(second, 1f, enabled: false));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no active bosses"));

            // Act
            var result = pool.Roll(new Random(42));

            // Assert
            Assert.AreSame(first, result,
                "sin entries activas, el pool devuelve la primera no-nula (invariante ≥1 boss).");
        }

        [Test]
        public void Roll_NoActiveEntries_SkipsNullBossesInFallback()
        {
            var authored = MakeBoss("boss.authored");
            var pool = MakePool(Entry(boss: null), Entry(authored, 0f));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no active bosses"));

            var result = pool.Roll(new Random(42));

            Assert.AreSame(authored, result);
        }
    }
}
