using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.ContractMod;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// La "tacha" de El Anotador (<see cref="AINode_ShiftComboToNeighbor"/>): a quién le corre el
    /// combo, cuántos por turno, y cuándo deja de devolverlos.
    /// </summary>
    /// <remarks>
    /// El <see cref="IContractModifierService"/> se reemplaza por un spy: lo que hay que testear son
    /// las decisiones del nodo (qué combo elige, cuántos corrimientos, si limpia los anteriores), no
    /// la aritmética del vecino por daño base, que ya cubre
    /// <see cref="ContractModifierServiceTests"/>.
    /// </remarks>
    [TestFixture]
    public class AnotadorComboShiftTests
    {
        private const string Par = "combo.par";
        private const string Escalera = "combo.escalera";
        private const string Generala = "combo.generala";

        private AttributesManager _attrs;
        private SpyContractModifiers _mods;
        private FakeComboLog _log;
        private Guid _bossGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _bossGuid = Guid.NewGuid();
            _attrs = new AttributesManager();

            var stats = new ModifiableAttributes();
            stats.EnsureInitialized();
            stats.SetAttribute<Health>(new Health(190));
            _attrs.Register(_bossGuid, stats);

            _mods = new SpyContractModifiers();
            ServiceLocator.AddService<IContractModifierService>(_mods);

            _log = new FakeComboLog();
            ServiceLocator.AddService<IComboLogService>(_log);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            _attrs = null;
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // Elección del combo
        // ======================================================================

        [Test]
        public void Tick_ShiftsTheComboThePlayerUsesTheMost()
        {
            // Arrange — del más reciente al más viejo.
            _log.SetHistory(Par, Escalera, Par, Par);

            // Act
            var result = NewNode().Tick(Context());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, _mods.Shifts.Count, "Fase 1: un solo corrimiento por turno.");
            Assert.AreEqual(Par, _mods.Shifts[0].Key, "Debería correr el combo más jugado de la ventana.");
        }

        [Test]
        public void Tick_NeverShiftsGeneralaNorTheNoComboMarker()
        {
            // Arrange — Generala es lo más jugado, pero es la debilidad del jefe: cinco iguales son
            // cinco iguales, se corra la hoja o no.
            _log.SetHistory(Generala, Generala, _log.NoComboMarker, Par);

            // Act
            NewNode().Tick(Context());

            // Assert
            Assert.AreEqual(1, _mods.Shifts.Count);
            Assert.AreEqual(Par, _mods.Shifts[0].Key,
                "Con Generala y 'sin combo' fuera del sorteo, queda el único combo corrible.");
        }

        [Test]
        public void Tick_OnlyImmuneCombosPlayed_SucceedsWithoutShifting()
        {
            // Arrange
            _log.SetHistory(Generala, Generala);

            // Act
            var result = NewNode().Tick(Context());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result,
                "Sin nada que corregir el nodo no puede fallar: un Failed abortaría el turno del boss.");
            CollectionAssert.IsEmpty(_mods.Shifts);
        }

        [Test]
        public void Tick_EmptyLog_SucceedsWithoutShifting()
        {
            // Act — turno 1 del combate, el jugador todavía no atacó.
            var result = NewNode().Tick(Context());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            CollectionAssert.IsEmpty(_mods.Shifts);
        }

        [Test]
        public void Tick_OnlyLooksAtTheConfiguredWindow()
        {
            // Arrange — ventana de 2: Escalera domina el pasado remoto pero no lo reciente.
            _log.SetHistory(Par, Par, Escalera, Escalera, Escalera);
            var node = NewNode();
            node.ComboLogWindow = 2;

            // Act
            node.Tick(Context());

            // Assert
            Assert.AreEqual(Par, _mods.Shifts[0].Key, "La ventana define qué es 'lo que más venís usando'.");
        }

        // ======================================================================
        // Duración y fases
        // ======================================================================

        [Test]
        public void Tick_Phase1_ClearsThePreviousShiftFirst_SoItLastsOneTurn()
        {
            // Arrange
            _log.SetHistory(Par);

            // Act
            NewNode().Tick(Context());

            // Assert — IContractModifierService no tiene expiración por modificador, así que "dura
            // 1 turno" es ClearAll + volver a promulgar (igual que AINode_PromulgateRule).
            Assert.AreEqual(1, _mods.ClearAllCalls, "Fase 1 debería devolver el corrimiento anterior.");
        }

        [Test]
        public void Tick_Phase2_RunsTwoCombos_AndStopsRevertingThem()
        {
            // Arrange — 35% de 190 = 66,5: con 60 de vida está en fase 2.
            SetBossHp(60);
            _log.SetHistory(Par, Par, Escalera);

            // Act
            NewNode().Tick(Context());

            // Assert
            Assert.AreEqual(2, _mods.Shifts.Count, "Fase 2: dos corrimientos por turno.");
            Assert.AreEqual(Par, _mods.Shifts[0].Key);
            Assert.AreEqual(Escalera, _mods.Shifts[1].Key, "El segundo corrimiento va al siguiente más jugado.");
            Assert.AreEqual(0, _mods.ClearAllCalls,
                "En fase 2 los corrimientos se acumulan hasta el final del combate.");
        }

        [Test]
        public void Tick_Phase2_FewerDistinctCombosThanShifts_RunsWhatThereIs()
        {
            // Arrange
            SetBossHp(60);
            _log.SetHistory(Par, Par);

            // Act
            NewNode().Tick(Context());

            // Assert
            Assert.AreEqual(1, _mods.Shifts.Count, "No debería correr el mismo combo dos veces en el turno.");
        }

        [Test]
        public void Tick_AtFullHp_StaysInPhaseOne()
        {
            // Arrange
            _log.SetHistory(Par, Escalera);

            // Act
            NewNode().Tick(Context());

            // Assert
            Assert.AreEqual(1, _mods.Shifts.Count);
            Assert.AreEqual(1, _mods.ClearAllCalls);
        }

        // ======================================================================
        // Dirección
        // ======================================================================

        [Test]
        public void Tick_DirectionDown_MovesTheComboToTheWorseNeighbour()
        {
            // Arrange
            _log.SetHistory(Escalera);
            var node = NewNode();
            node.Direction = AINode_ShiftComboToNeighbor.ShiftDirection.Down;

            // Act
            node.Tick(Context());

            // Assert
            Assert.AreEqual(-1, _mods.Shifts[0].Value, "Down ⇒ el combo paga como el inmediatamente inferior.");
        }

        [Test]
        public void Tick_DirectionUp_MovesTheComboToTheBetterNeighbour()
        {
            // Arrange
            _log.SetHistory(Par);
            var node = NewNode();
            node.Direction = AINode_ShiftComboToNeighbor.ShiftDirection.Up;

            // Act
            node.Tick(Context());

            // Assert
            Assert.AreEqual(+1, _mods.Shifts[0].Value);
        }

        [Test]
        public void Tick_DirectionRandom_OnlyEverPicksAdjacentNeighbours()
        {
            // Arrange — RNG inyectado: la dirección es sorteada pero acotada.
            _log.SetHistory(Par);
            var node = NewNode();
            var context = Context();
            context.Rng = new System.Random(1234);

            // Act
            for (int i = 0; i < 10; i++) node.Tick(context);

            // Assert
            Assert.AreEqual(10, _mods.Shifts.Count);
            foreach (var shift in _mods.Shifts)
            {
                Assert.IsTrue(shift.Value == 1 || shift.Value == -1,
                    $"Dirección {shift.Value}: el corrimiento es siempre a un vecino, nunca a dos casilleros.");
            }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private AINode_ShiftComboToNeighbor NewNode() => new AINode_ShiftComboToNeighbor
        {
            Direction = AINode_ShiftComboToNeighbor.ShiftDirection.Down,
            ComboLogWindow = 5,
            ShiftsPerTurnPhase1 = 1,
            ShiftsPerTurnPhase2 = 2,
            Phase2HpThreshold = 0.35f,
            RevertPreviousShifts = true,
            Phase2ShiftsArePermanent = true,
            ImmuneComboIds = new List<string> { Generala },
        };

        private AIContext Context() => new AIContext
        {
            SelfGuid = _bossGuid,
            PlayerGuid = Guid.NewGuid(),
            SelfMaxHp = 190,
            Attributes = _attrs,
        };

        private void SetBossHp(int value) => _attrs.SetAttributeValue<Health, int>(_bossGuid, value);

        private sealed class SpyContractModifiers : IContractModifierService
        {
            /// <summary>Cada corrimiento como (comboId, dirección), en orden.</summary>
            public readonly List<KeyValuePair<string, int>> Shifts = new List<KeyValuePair<string, int>>();

            public int ClearAllCalls;

            public int GetEffectiveBaseDamage(string comboId, int baseDamage) => baseDamage;
            public bool IsForbidden(string comboId) => false;
            public bool HasAnyModifier => Shifts.Count > 0;
            public void MultiplyCombo(string comboId, float factor) { }
            public void ForbidCombo(string comboId) { }

            public void SetComboToNeighbor(string comboId, int direction)
                => Shifts.Add(new KeyValuePair<string, int>(comboId, direction));

            public void ClearAll() => ClearAllCalls++;
        }

        private sealed class FakeComboLog : IComboLogService
        {
            private readonly List<string> _history = new List<string>();

            public string NoComboMarker => "combo.none";

            /// <summary>Historial del más reciente al más antiguo, como devuelve el service real.</summary>
            public void SetHistory(params string[] mostRecentFirst)
            {
                _history.Clear();
                _history.AddRange(mostRecentFirst);
            }

            public void Record(string comboId) => _history.Insert(0, comboId ?? NoComboMarker);

            public string LastCombo => _history.Count > 0 ? _history[0] : null;

            public IReadOnlyList<string> Last(int count)
            {
                if (count <= 0) return Array.Empty<string>();
                return _history.GetRange(0, Math.Min(count, _history.Count));
            }

            public void Clear() => _history.Clear();
        }
    }
}
