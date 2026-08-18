using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Wiring del árbol del Croupier <b>en memoria</b>: contra el builder y no contra el
    /// <c>.asset</c>, que ataría el suite a que Unity lo haya reimportado. Cubre lo que un merge
    /// puede romper sin que se note: orden de los gates, fallbacks y números de la ficha.
    /// </summary>
    [TestFixture]
    public class CroupierPhaseWiringTests
    {
        private const float PercentTolerance = 0.0001f;

        private HazardDefinitionSO _fire;
        private HazardDefinitionSO _firePhase2;
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _fire = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _fire.hideFlags = HideFlags.HideAndDontSave;
            _firePhase2 = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _firePhase2.hideFlags = HideFlags.HideAndDontSave;

            _root = CroupierAssetBuilder.BuildAIRoot(_fire, _firePhase2);
            Assert.IsNotNull(_root, "BuildAIRoot debería devolver un Sequence.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_fire != null) Object.DestroyImmediate(_fire);
            if (_firePhase2 != null) Object.DestroyImmediate(_firePhase2);
        }

        // =====================================================================
        // Estructura del turno
        // =====================================================================

        [Test]
        public void Root_ResolvesLastTurnsBet_First()
        {
            // Va suelto (sin Selector) porque siempre devuelve Succeeded.
            Assert.IsInstanceOf<AINode_DetonateSungSectors>(_root.Children[0],
                "El primer hijo del Sequence raíz tiene que detonar lo cantado el turno pasado.");
        }

        [Test]
        public void Root_TicksThePhaseGate_BeforeTheAttack()
        {
            // En el path no-coroutine un Running aborta el Sequence: una fase después del marcado
            // no tickearía nunca.
            int gateIdx = IndexOfGateAtPercent(CroupierAssetBuilder.Phase2HpThreshold);
            int attackIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_MarkSungSectors));

            Assert.Greater(gateIdx, -1, "No hay gate de HP al 50% en el árbol.");
            Assert.Greater(attackIdx, -1, "No se encontró el nodo de marcado.");
            Assert.Greater(attackIdx, gateIdx, "El gate de fase quedó después del ataque.");
        }

        [Test]
        public void Root_IgnitesAfterDetonating()
        {
            int detonateIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_DetonateSungSectors));
            int igniteIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_IgniteDetonatedSectors));

            Assert.Greater(igniteIdx, detonateIdx,
                "El fuego se enciende sobre el sector que detonó este turno: no puede ir antes de detonar.");
        }

        [Test]
        public void Root_SingsBeforeMarking()
        {
            // Arrange / Act — el número tiene que existir antes de que alguien lo lea.
            int spinIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_SpinWheel));
            int markIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_MarkSungSectors));

            // Assert
            Assert.Greater(spinIdx, -1);
            Assert.Greater(markIdx, spinIdx, "El marcado lee el número cantado: va después de cantar.");
        }

        [Test]
        public void EveryFallibleStep_IsIsolatedInASelectorWithWaitFallback()
        {
            // El Sequence corta en el primer Failed: sin fallback, una sala sin bounds o un servicio
            // sin registrar le cancela al jefe el resto del turno.
            var fallible = new[]
            {
                typeof(AINode_SpinWheel),
                typeof(AINode_MarkSungSectors),
                typeof(AINode_IgniteDetonatedSectors),
                typeof(AINode_Move),
                typeof(AINode_If),
            };

            foreach (var type in fallible)
            {
                var wrapper = _root.Children.OfType<AINode_Selector>()
                    .FirstOrDefault(s => s.Children != null && s.Children.Any(c => Descendants(c).Any(n => n.GetType() == type)));

                Assert.IsNotNull(wrapper,
                    $"{type.Name} está suelto en la secuencia raíz: si devuelve Failed, el jefe pierde el " +
                    "resto del turno. Envolverlo en Selector[paso, Wait].");
                Assert.IsTrue(wrapper.Children.Any(c => c is AINode_Wait),
                    $"El Selector que envuelve a {type.Name} no tiene Wait de fallback — devolvería Failed igual.");
            }
        }

        [Test]
        public void PhaseGate_HasAWaitElse_SoItNeverAbortsTheSequence()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.Phase2HpThreshold);

            Assert.IsNotNull(gate);
            Assert.IsInstanceOf<AINode_Wait>(gate.Else,
                "Un If de efecto sin Else devuelve Failed cuando la condición no pasa.");
        }

        [Test]
        public void PhaseGate_IsLatchedByOnce_AndAnnouncesPhase2()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.Phase2HpThreshold);

            Assert.IsInstanceOf<AINode_Once>(gate.Then,
                "El setup de fase tiene que latchear: sin Once vuelve a aplicarse cada turno bajo el umbral.");

            var stat = Descendants(gate.Then).OfType<AINode_ApplyStatModifier>().FirstOrDefault();
            Assert.IsNotNull(stat, "Falta el ApplyStatModifier que dispara el feedback de fase.");
            Assert.AreEqual(2, stat.PhaseIndex);
            Assert.IsTrue(stat.EmitPhaseChangedEvent, "Sin el evento no hay animación ni diálogo de fase 2.");
            Assert.AreEqual(0, stat.AttackDelta, "La fase NO sube el daño del jefe.");
            Assert.AreEqual(0, stat.SpeedDelta, "La fase no lo apura: lo que cambia es la mesa.");
        }

        [Test]
        public void PhaseGate_RigsTheWheelAndDoublesTheNumbers()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.Phase2HpThreshold);
            var mode = Descendants(gate.Then).OfType<AINode_SetWheelMode>().FirstOrDefault();

            Assert.IsNotNull(mode, "Falta el SetWheelMode: sin él la fase 2 no canta dos números.");
            Assert.AreEqual(2, mode.NumbersPerTurn, "Pleno y color: dos números.");
            // Trucada apaga el corrimiento y nada más: la Represalia no mira la fase.
            Assert.IsTrue(mode.Rigged,
                "La rueda queda trucada: en fase 2 cerrar el turno dentro del sector cantado deja de " +
                "correr el número, y el jugador se queda sin la única palanca sobre lo que cae.");
            Assert.AreEqual(2, mode.PhaseIndex);
        }

        // =====================================================================
        // Se reacomoda
        // =====================================================================

        [Test]
        public void Tree_HasNoMeleeAndNoRangedBehavior()
        {
            var all = Descendants(_root);

            Assert.IsEmpty(all.OfType<AINode_Behavior>(),
                "No tiene melee ni rango. Su único daño directo es la Represalia, y esa entra por el hook " +
                "de daño de la rueda, no por el árbol.");
        }

        /// <summary>Regresión de "el crupier no se mueve": el árbol no tenía ningún nodo de
        /// movimiento.</summary>
        [Test]
        public void Tree_Repositions_ClosingInAndBackingOff()
        {
            var move = Descendants(_root).OfType<AINode_Move>().SingleOrDefault();

            Assert.IsNotNull(move, "Sin nodo de movimiento el jefe se queda clavado toda la pelea.");
            Assert.IsInstanceOf<TargetSelector_AlwaysPlayer>(move.TargetSelector,
                "Se reacomoda respecto del jugador, no de otra cosa.");
            Assert.IsTrue(move.Retreat,
                "Sin Retreat sólo cierra distancia: el reporte pedía las dos mitades, que se acerque " +
                "y que se aleje.");
            Assert.IsFalse(move.StopAdjacent,
                "StopAdjacent es el fallback legacy de rango 1 y pisaría la banda si DesiredRange " +
                "quedara en null.");

            Assert.AreEqual(CroupierAssetBuilder.DesiredRange, ReadInt(move.DesiredRange),
                "La banda que sostiene con el jugador sale de la ficha.");
            Assert.AreEqual(CroupierAssetBuilder.MoveSteps, ReadInt(move.MaxSteps));
        }

        /// <summary>
        /// El movimiento es el único paso que puede devolver <c>Running</c> (espera el blink), y un
        /// Running aborta el Sequence en el path no-coroutine: con algo detrás se pierde el turno.
        /// </summary>
        [Test]
        public void Reposition_IsTheLastStep_AndIsIsolated()
        {
            int moveIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_Move));

            Assert.Greater(moveIdx, -1, "No hay reacomodo en el árbol.");
            Assert.AreEqual(_root.Children.Count - 1, moveIdx,
                "El reacomodo tiene que ser el último paso del turno.");

            var wrapper = _root.Children[moveIdx] as AINode_Selector;
            Assert.IsNotNull(wrapper, "El reacomodo tiene que ir envuelto en Selector[paso, Wait].");
            Assert.IsTrue(wrapper.Children.Any(c => c is AINode_Wait),
                "AINode_Move devuelve Failed en el caso benigno 'ya estoy en la banda': sin el Wait de " +
                "fallback el paso propagaría ese Failed.");
        }

        /// <summary>El orden fija que ningún paso de mesa quede detrás del <c>Running</c> del
        /// blink.</summary>
        [Test]
        public void Reposition_RunsAfterEveryTableStep()
        {
            var order = Descendants(_root);

            int move = order.FindIndex(n => n is AINode_Move);
            int ignite = order.FindIndex(n => n is AINode_IgniteDetonatedSectors);
            int mark = order.FindIndex(n => n is AINode_MarkSungSectors);

            Assert.Greater(move, ignite, "El reacomodo va después de la ignición.");
            Assert.Greater(move, mark, "El reacomodo va después del marcado.");
        }

        // =====================================================================
        // Números de la ficha
        // =====================================================================

        /// <summary>Del <b>número que cayó</b> y no de un dado al azar: un sorteo silencioso es
        /// indistinguible del bloqueo del Sunken Grand.</summary>
        [Test]
        public void Confiscation_TakesTheNumberThatFell_NotARandomDie()
        {
            var block = Descendants(_root).OfType<AINode_RotateBlock>().Single();

            Assert.AreEqual(AINode_RotateBlock.BlockTarget.Dice, block.Target);

            var reader = block.DirectedIndex as AIReadCroupierWheelNumber;
            Assert.IsNotNull(reader,
                "Sin el reader de la ruleta el nodo cae al sorteo al azar, que es exactamente la " +
                "versión que se había sacado por leerse como el Sunken Grand.");

            Assert.AreEqual(AIReadCroupierWheelNumber.NumberSource.Sung, reader.Source,
                "Sung y no Detonated: el candado tiene que estar puesto desde el primer turno. Con " +
                "Detonated el turno 1 no tiene nada resuelto, el reader devuelve -1 y RotateDice ya " +
                "hizo Clear() — el jugador arranca sin dado bloqueado y el candado va y viene, que " +
                "desde afuera se lee como un porcentaje.");
        }

        /// <summary><c>SungNumbers</c> se puebla en <c>AINode_SpinWheel</c>: leer antes devolvería
        /// <c>-1</c> y dejaría el turno sin candado.</summary>
        [Test]
        public void Confiscation_RunsAfterTheWheelHasSung_SoThereIsAlwaysANumberToTake()
        {
            var order = Descendants(_root);

            int spin = order.FindIndex(n => n is AINode_SpinWheel);
            int block = order.FindIndex(n => n is AINode_RotateBlock);

            Assert.Greater(spin, -1, "No hay tirada de ruleta en el árbol.");
            Assert.Greater(block, -1, "No hay confiscación en el árbol.");
            Assert.Less(spin, block,
                "La ruleta tiene que cantar antes de la confiscación: SungNumbers se puebla en " +
                "SpinWheel, y leerlo antes deja el turno sin dado bloqueado.");
        }

        /// <summary>Los ids vacíos dejan el nodo mudo — en los jefes viejos eso es a propósito.</summary>
        [Test]
        public void Confiscation_IsPresented()
        {
            var block = Descendants(_root).OfType<AINode_RotateBlock>().Single();

            Assert.AreEqual(BossFeedbackIds.CroupierConfiscaVfx, block.BlockVfxId);
            Assert.AreEqual(BossFeedbackIds.CroupierConfiscaFeel, block.BlockFeelId);
        }

        /// <summary>
        /// <c>AINode_DetonateSungSectors</c> llama a <c>ConsumeWindup()</c>, que vacía
        /// <c>SungNumbers</c>: detrás de la tirada se comería el número recién cantado y la
        /// confiscación no bloquearía nada — sin error, sin warning, sin nada en pantalla.
        /// </summary>
        [Test]
        public void TheWindupIsConsumedBeforeTheWheelSings_SoTheFreshNumberSurvives()
        {
            var order = Descendants(_root);

            int detonate = order.FindIndex(n => n is AINode_DetonateSungSectors);
            int spin = order.FindIndex(n => n is AINode_SpinWheel);

            Assert.Greater(detonate, -1, "No hay detonación en el árbol.");
            Assert.Greater(spin, -1, "No hay tirada de ruleta en el árbol.");
            Assert.Less(detonate, spin,
                "La detonación tiene que consumir el windup ANTES de que la ruleta cante: detrás, " +
                "vaciaría el número recién cantado y la confiscación se quedaría sin nada que leer.");
        }

        [Test]
        public void SectorDamage_MatchesTheSheet_AndTheSeamStaysUnderTheFloorCap()
        {
            var mark = Descendants(_root).OfType<AINode_MarkSungSectors>().Single();

            Assert.AreEqual(20, mark.SectorDamage, "Fase 1: un bloque × 20.");
            Assert.AreEqual(12, mark.SectorDamagePhase2, "Fase 2: dos bloques × 12.");
            Assert.AreEqual(24, mark.SectorDamagePhase2 * 2, "La costura pega 24 en total.");
            Assert.LessOrEqual(mark.SectorDamage, 25, "Techo de daño por golpe del piso 1.");
            Assert.LessOrEqual(mark.SectorDamagePhase2, 25);
        }

        [Test]
        public void Retaliation_Is8()
        {
            var spin = Descendants(_root).OfType<AINode_SpinWheel>().Single();
            Assert.AreEqual(8, spin.RetaliationDamage, "Represalia de mesa: el golpe más chico del documento.");
        }

        [Test]
        public void Fire_HasOneDefinitionPerPhase_AndTheDurationsDifferByTheRoundOffset()
        {
            // La ficha cuenta rondas del JUGADOR y el asset autora rondas de hazard: no son el
            // mismo número. La duración se descuenta en el wrap de ronda y el fuego nace con el
            // turno del jugador ya jugado (CNF-006): DurationRounds = D deja D-1 que sí pegan.
            const int IgnitionRound = 1;
            const int SheetBurnRounds = 2;
            const int SheetBurnRoundsPhase2 = 3;

            var ignite = Descendants(_root).OfType<AINode_IgniteDetonatedSectors>().Single();

            Assert.AreSame(_fire, ignite.Fire);
            Assert.AreSame(_firePhase2, ignite.FirePhase2,
                "La duración vive en la definición, así que la fase 2 necesita su propia def.");
            Assert.IsTrue(ignite.BlastConsumesFlame,
                "La explosión consume la llama: el peor caso de la costura tiene que seguir siendo 24.");

            Assert.AreEqual(SheetBurnRounds, CroupierAssetBuilder.FireDurationRounds - IgnitionRound,
                "Fase 1 arde 2 rondas de jugador. Con una sola, salir del bloque deja de ser una " +
                "decisión: alcanza con no volver.");
            Assert.AreEqual(SheetBurnRoundsPhase2, CroupierAssetBuilder.FireDurationRoundsPhase2 - IgnitionRound,
                "Fase 2 arde 3 rondas de jugador — una más que fase 1, con el mismo corrimiento.");
            Assert.AreEqual(6, CroupierAssetBuilder.FireDamage, "6 por terminar el turno adentro.");
        }

        [Test]
        public void PopulateEnemyData_WritesTheSheet()
        {
            // Arrange
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                // Act — visual y retrato en null: el wiring visual vive en CroupierVisualWiringTests.
                CroupierAssetBuilder.PopulateEnemyData(data, _fire, _firePhase2, null, null);

                // Assert
                Assert.AreEqual("boss.croupier", data.EntityId);
                Assert.AreEqual(120, data.BaseHP,
                    "Jefe de piso 1: ~6 turnos con el golpe base del piso (mediana 20). " +
                    "La simulación que pedía 350 asumía un golpe de 42, que es de run avanzada.");
                Assert.AreEqual(20, data.BaseAttack);
                Assert.AreEqual("combo.pair", data.WeaknessComboId,
                    "El id real del combo Par en el catálogo es combo.pair.");
                Assert.AreEqual(1.5f, data.WeaknessMultiplierOverride, PercentTolerance);
                Assert.AreEqual(15, data.MinGoldDrop, "Oro de piso 1.");
                Assert.AreEqual(23, data.MaxGoldDrop);
                Assert.IsEmpty(data.Behaviors, "Sin behaviors: no tiene melee ni rango.");
                Assert.IsNotNull(data.AIRoot);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static int ReadInt(AIIntReader reader)
        {
            var constant = reader as AIConstantInt;
            Assert.IsNotNull(constant, "Se esperaba un AIConstantInt (valor literal del inspector).");
            return constant.Value;
        }

        /// <summary>Gate de fase por su umbral de HP, venga suelto o envuelto en el Selector de aislamiento.</summary>
        private AINode_If FindGateAtPercent(float percent)
        {
            return _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g?.Conditions != null && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - percent) < PercentTolerance));
        }

        private int IndexOfGateAtPercent(float percent)
        {
            var gate = FindGateAtPercent(percent);
            if (gate == null) return -1;
            return _root.Children.FindIndex(c => ReferenceEquals(Unwrap(c), gate));
        }

        private static AINode_If Unwrap(AIDecisionNode child)
        {
            if (child is AINode_If direct) return direct;
            if (child is AINode_Selector sel && sel.Children != null)
                return sel.Children.OfType<AINode_If>().FirstOrDefault();
            return null;
        }

        /// <summary>Tree-walker por reflexión, sin descender en <see cref="UnityEngine.Object"/>
        /// (no arrastra assets referenciados). Copiado del suite del Sunken Grand.</summary>
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
