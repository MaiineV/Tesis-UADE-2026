using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Wiring del árbol de La Bandida, armado EN MEMORIA por
    /// <see cref="BandidaAssetBuilder.BuildAIRoot"/> — sin cargar ningún asset.
    /// </summary>
    /// <remarks>
    /// Mismo criterio que <c>SunkenGrandPhaseWiringTests</c>: lo que se fija acá es el orden de los
    /// gates, los fallbacks que evitan que un <c>Failed</c> le cancele el turno al jefe, y los
    /// números que el diseño trata como contrato (25 en 7×7, 9 en 3×3, cuenta de 2, reposición 2 → 1).
    /// Un test rojo acá significa que el árbol se desarmó en un merge o que alguien movió un número
    /// de la ficha sin querer.
    /// </remarks>
    [TestFixture]
    public class BandidaTreeWiringTests
    {
        private EnemyDataSO _reelData;
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _reelData = ScriptableObject.CreateInstance<EnemyDataSO>();
            _root = BandidaAssetBuilder.BuildAIRoot(_reelData);
            Assert.IsNotNull(_root, "BuildAIRoot debería devolver un Sequence raíz.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_reelData != null) Object.DestroyImmediate(_reelData);
        }

        // ======================================================================
        // Estructura del turno
        // ======================================================================

        [Test]
        public void Root_StartsWithExecuteTelegraph_SoTheMarkOfThePreviousTurnResolvesFirst()
        {
            Assert.IsNotEmpty(_root.Children);
            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El primer hijo del Sequence raíz debe cobrar el telegráfico del turno anterior.");
        }

        [Test]
        public void PhaseGate_ComesBeforeTheActionPool()
        {
            int phaseIdx = IndexOfChildContaining<AINode_LockReel>();
            int poolIdx = IndexOfChildContaining<AINode_TelegraphMark>();

            Assert.Greater(phaseIdx, -1, "No se encontró el gate de Fase 2 (LockReel).");
            Assert.Greater(poolIdx, -1, "No se encontró el pool de acción (TelegraphMark).");
            Assert.Less(phaseIdx, poolIdx,
                "El gate de fase quedó después del ataque: en el path no-coroutine un Running del " +
                "ataque abortaría la secuencia y la fase nunca tickearía.");
        }

        [Test]
        public void TickJackpot_RunsOnceAndBeforeTheReelRow_SoARespawnedCountKeepsBothWarningRounds()
        {
            int tickIdx = IndexOfChildContaining<AINode_TickJackpot>();
            int reelIdx = IndexOfChildContaining<AINode_SpawnReels>();
            int poolIdx = IndexOfChildContaining<AINode_TelegraphMark>();

            Assert.AreEqual(1, Descendants(_root).OfType<AINode_TickJackpot>().Count(),
                "La cuenta debe bajar una sola vez por turno del jefe.");
            Assert.Greater(tickIdx, -1);
            Assert.Less(tickIdx, reelIdx,
                "TickJackpot quedó después de la fila: el rearme de la reposición se comería un " +
                "turno de aviso en el mismo turno en que el rodillo vuelve.");
            Assert.Less(tickIdx, poolIdx, "La cuenta tiene que bajar antes de evaluar el jackpot.");
        }

        [Test]
        public void RiskyChildren_AreWrappedInSelectorWithWaitFallback()
        {
            foreach (var payload in new AIDecisionNode[]
                     {
                         Descendants(_root).OfType<AINode_SpawnReels>().FirstOrDefault(),
                         Descendants(_root).OfType<AINode_LockReel>().FirstOrDefault(),
                     })
            {
                Assert.IsNotNull(payload, "Falta un nodo de riesgo en el árbol.");

                var wrapper = _root.Children.OfType<AINode_Selector>()
                    .FirstOrDefault(s => s.Children != null && Descendants(s).Any(n => ReferenceEquals(n, payload)));

                Assert.IsNotNull(wrapper,
                    $"{payload.GetType().Name} no está dentro de un Selector de la secuencia raíz: " +
                    "si devuelve Failed, el jefe pierde el resto del turno.");
                Assert.IsTrue(wrapper.Children.Any(c => c is AINode_Wait),
                    $"El Selector que envuelve a {payload.GetType().Name} no tiene Wait de fallback.");
            }
        }

        [Test]
        public void ReelRow_IsNotWrappedInOnce_SoBrokenReelsComeBack()
        {
            var reelRowOwner = _root.Children.FirstOrDefault(c =>
                Descendants(c).OfType<AINode_SpawnReels>().Any());

            Assert.IsNotNull(reelRowOwner);
            Assert.IsFalse(Descendants(reelRowOwner).OfType<AINode_Once>().Any(),
                "La fila de rodillos quedó bajo un Once: latchea tras el primer spawn y ningún " +
                "rodillo vuelve nunca (mismo accidente que el loop de refuerzos).");
        }

        [Test]
        public void PhaseSetup_IsWrappedInOnce_SoItIsAOneShot()
        {
            var gate = FindPhaseGate();
            Assert.IsNotNull(gate, "No hay gate de HP para la Fase 2.");
            Assert.IsInstanceOf<AINode_Once>(gate.Then,
                "El setup de fase debe ir en un Once — si no, re-aplica el HOLD cada turno.");
        }

        [Test]
        public void Boss_NeverMoves_NoMoveOrKeepDistanceInTheTree()
        {
            var all = Descendants(_root);
            Assert.IsEmpty(all.OfType<AINode_Move>().ToList(),
                "La Bandida está atornillada a la pared: no puede tener Move.");
            Assert.IsEmpty(all.OfType<AINode_KeepDistance>().ToList(),
                "La Bandida está atornillada a la pared: no puede tener KeepDistance.");
        }

        // ======================================================================
        // Números de la ficha
        // ======================================================================

        [Test]
        public void PhaseGate_TriggersAt50Percent_AndDoesNotTouchDamageNumbers()
        {
            var gate = FindPhaseGate();
            Assert.IsNotNull(gate);

            var hpBelow = gate.Conditions.OfType<PcOwnerHpBelow>().First();
            Assert.AreEqual(BandidaAssetBuilder.Phase2HpThreshold, hpBelow.Percent, 0.0001f);

            var statMod = Descendants(gate.Then).OfType<AINode_ApplyStatModifier>().FirstOrDefault();
            Assert.IsNotNull(statMod, "La fase debe emitir OnBossPhaseChanged para el feedback.");
            Assert.AreEqual(0, statMod.AttackDelta, "La Fase 2 no cambia un solo número de daño.");
            Assert.AreEqual(0, statMod.SpeedDelta, "La Fase 2 cambia frecuencia y distancia, no stats.");
            Assert.AreEqual(2, statMod.PhaseIndex);
            Assert.IsTrue(statMod.EmitPhaseChangedEvent);
        }

        [Test]
        public void PhaseTwo_HoldsTheMiddleReel_AndDropsRespawnToOneTurn()
        {
            var gate = FindPhaseGate();
            var lockReel = Descendants(gate.Then).OfType<AINode_LockReel>().FirstOrDefault();
            var delay = Descendants(gate.Then).OfType<AINode_SetReelRespawnDelay>().FirstOrDefault();

            Assert.IsNotNull(lockReel, "Fase 2 = HOLD: falta el LockReel.");
            Assert.AreEqual(ReelSide.Middle, lockReel.Side,
                "El HOLD traba el rodillo del medio — quedan los dos de la punta, los más lejanos.");
            Assert.Greater(lockReel.LockedHp, 25 * 4,
                "El rodillo trabado tiene que aguantar toda la pelea: su pool de vida debe estar " +
                "muy por encima del techo de daño del jugador.");

            Assert.IsNotNull(delay, "Fase 2 baja la reposición a un turno: falta el SetReelRespawnDelay.");
            Assert.AreEqual(BandidaAssetBuilder.RespawnDelayPhase2, delay.Value);
        }

        [Test]
        public void ReelRow_IsThreeReelsWithTwoTurnRespawn_AndRearmsTheCountAtTwo()
        {
            var reels = Descendants(_root).OfType<AINode_SpawnReels>().Single();

            Assert.AreEqual(BandidaAssetBuilder.ReelCount, reels.Count, "Tres rodillos en fila.");
            Assert.AreEqual(BandidaAssetBuilder.RespawnDelayPhase1, reels.RespawnDelayTurns,
                "Fase 1: el rodillo roto vuelve a los dos turnos del jefe.");
            Assert.AreEqual(BandidaAssetBuilder.CountdownStart, reels.CountdownOnRespawn,
                "La reposición devuelve la cuenta a 2 en el mismo paso en que devuelve el rodillo.");
            Assert.AreSame(_reelData, reels.ReelData, "La fila tiene que apuntar al SO del rodillo.");
        }

        [Test]
        public void Jackpot_Is25InA7x7OnThePlayer_GatedByTheCounterNotByReelHp()
        {
            var jackpotGate = FindJackpotGate();
            Assert.IsNotNull(jackpotGate, "No hay gate de jackpot en el pool de acción.");

            var pc = jackpotGate.Conditions.OfType<PcJackpotCountdown>().FirstOrDefault();
            Assert.IsNotNull(pc,
                "El jackpot tiene que gatearse por el contador. Un chequeo por HP de los rodillos " +
                "nunca vería la cancelación: con el mínimo de 6 contra 3 de vida, 'dañado y vivo' " +
                "no existe.");
            Assert.AreEqual(IntComparison.Equal, pc.Comparison);
            Assert.AreEqual(0, pc.Value, "El jackpot se marca cuando la cuenta llega a 0.");
            Assert.IsTrue(pc.RequireCounting,
                "Sin RequireCounting el jackpot dispararía con la cuenta ya cancelada.");

            var mark = Descendants(jackpotGate.Then).OfType<AINode_TelegraphMark>().FirstOrDefault();
            Assert.IsNotNull(mark, "El jackpot va como TelegraphMark normal: la cuenta avisa, el mark confirma.");
            Assert.AreEqual(ThreatShape.SquareAroundPlayer, mark.Shape);
            Assert.AreEqual(3, mark.Size, "Size 3 ⇒ 7×7.");
            Assert.AreEqual(25, mark.Damage, "Jackpot = 25.");
            Assert.LessOrEqual(mark.Damage, 25, "Techo de daño de piso 1 = 25 por golpe.");
        }

        [Test]
        public void Jackpot_RearmsInPlace_NoDeadRoundForTankingIt()
        {
            var jackpotGate = FindJackpotGate();
            var sequence = jackpotGate.Then as AINode_Sequence;

            Assert.IsNotNull(sequence, "El jackpot debe ser Sequence[TelegraphMark, ResetCountdown].");
            int markIdx = sequence.Children.FindIndex(c => c is AINode_TelegraphMark);
            int resetIdx = sequence.Children.FindIndex(c => c is AINode_ResetJackpotCountdown);

            Assert.Greater(markIdx, -1);
            Assert.Greater(resetIdx, markIdx,
                "El rearme va después del mark, en el mismo turno: la cuenta que dispara se rearma " +
                "en el acto. La pausa es el premio de cancelar, tanquear no la recibe.");

            var reset = (AINode_ResetJackpotCountdown)sequence.Children[resetIdx];
            Assert.AreEqual(BandidaAssetBuilder.CountdownStart, reset.Value);
        }

        [Test]
        public void Arm_Is9InA3x3AroundSelf_GatedByAdjacency()
        {
            var armGate = _root.Children
                .SelectMany(c => Descendants(c).OfType<AINode_If>())
                .FirstOrDefault(i => i.Conditions != null && i.Conditions.OfType<PcTargetInRange>().Any());

            Assert.IsNotNull(armGate, "No hay gate de adyacencia para el brazo.");
            var range = armGate.Conditions.OfType<PcTargetInRange>().First();
            Assert.AreEqual(1, range.Range, "El brazo es adyacente: un paso atrás lo esquiva.");
            Assert.AreEqual(DistanceMetric.Chebyshev, range.Metric,
                "El gate tiene que cubrir el mismo 3×3 que marca el brazo, diagonales incluidas.");

            var mark = Descendants(armGate.Then).OfType<AINode_TelegraphMark>().FirstOrDefault();
            Assert.IsNotNull(mark);
            Assert.AreEqual(ThreatShape.SquareAroundSelf, mark.Shape, "El brazo sale del propio jefe.");
            Assert.AreEqual(1, mark.Size, "Size 1 ⇒ 3×3.");
            Assert.AreEqual(9, mark.Damage, "Brazo = 9.");
        }

        [Test]
        public void ActionPool_IsASelectorEndingInWait_SoJackpotAndArmNeverShareATurn()
        {
            var pool = _root.Children.OfType<AINode_Selector>()
                .First(s => Descendants(s).OfType<AINode_TelegraphMark>().Any());

            Assert.IsInstanceOf<AINode_Wait>(pool.Children.Last(),
                "El pool tiene que cerrar en Wait: sin fallback el Selector devuelve Failed y " +
                "aborta el turno.");

            int jackpotIdx = pool.Children.FindIndex(c =>
                c is AINode_If i && i.Conditions.OfType<PcJackpotCountdown>().Any());
            int armIdx = pool.Children.FindIndex(c =>
                c is AINode_If i && i.Conditions.OfType<PcTargetInRange>().Any());

            Assert.Greater(jackpotIdx, -1);
            Assert.Greater(armIdx, jackpotIdx,
                "El jackpot va primero en el Selector: una amenaza por turno, y la grande manda.");
        }

        // ======================================================================
        // Populate
        // ======================================================================

        [Test]
        public void PopulateEnemyData_WritesTheSheetNumbers()
        {
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                BandidaAssetBuilder.PopulateEnemyData(boss, _reelData, null);

                Assert.AreEqual("boss.one_armed", boss.EntityId);
                Assert.AreEqual(140, boss.BaseHP);
                Assert.AreEqual(20, boss.BaseAttack);
                Assert.AreEqual("combo.ladder", boss.WeaknessComboId,
                    "Debilidad: la mano que no alinea (escalera).");
                Assert.AreEqual(1.5f, boss.WeaknessMultiplierOverride, 0.0001f);
                Assert.AreEqual(15, boss.MinGoldDrop);
                Assert.AreEqual(23, boss.MaxGoldDrop);
                Assert.IsInstanceOf<AINode_Sequence>(boss.AIRoot);
            }
            finally { Object.DestroyImmediate(boss); }
        }

        [Test]
        public void PopulateReelData_IsAThreeHpObjectThatDoesNothing()
        {
            var reel = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                BandidaAssetBuilder.PopulateReelData(reel, null);

                Assert.AreEqual(3, reel.BaseHP, "Tu mínimo de 6 ya parte un rodillo de 3.");
                Assert.AreEqual(0, reel.BaseAttack, "El rodillo es un objeto: no pega.");
                Assert.IsInstanceOf<AINode_Wait>(reel.AIRoot, "El rodillo no actúa en su turno.");
            }
            finally { Object.DestroyImmediate(reel); }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private AINode_If FindPhaseGate() =>
            _root.Children.SelectMany(c => Descendants(c).OfType<AINode_If>())
                .FirstOrDefault(i => i.Conditions != null && i.Conditions.OfType<PcOwnerHpBelow>().Any());

        private AINode_If FindJackpotGate() =>
            _root.Children.SelectMany(c => Descendants(c).OfType<AINode_If>())
                .FirstOrDefault(i => i.Conditions != null && i.Conditions.OfType<PcJackpotCountdown>().Any());

        private int IndexOfChildContaining<T>() where T : class =>
            _root.Children.FindIndex(c => Descendants(c).OfType<T>().Any());

        /// <summary>Tree-walker por reflexión (mismo helper que <c>SunkenGrandPhaseWiringTests</c>):
        /// todo lo alcanzable desde <paramref name="root"/>, sin descender en
        /// <see cref="Object"/> para no arrastrar assets referenciados.</summary>
        private static List<object> Descendants(object root)
        {
            var all = new List<object>();
            var visited = new HashSet<object>(RefComparer.Instance);

            void Walk(object o)
            {
                if (o == null || o is string || o is Object) return;

                var type = o.GetType();
                if (type.IsPrimitive || type.IsEnum) return;
                if (!type.IsValueType && !visited.Add(o)) return;

                all.Add(o);

                if (o is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) Walk(item);
                    return;
                }

                if (!(type.Namespace ?? string.Empty).StartsWith("Rollgeon")) return;

                foreach (var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object value;
                    try { value = field.GetValue(o); }
                    catch { continue; }
                    Walk(value);
                }
            }

            Walk(root);
            return all;
        }

        private sealed class RefComparer : IEqualityComparer<object>
        {
            public static readonly RefComparer Instance = new RefComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
