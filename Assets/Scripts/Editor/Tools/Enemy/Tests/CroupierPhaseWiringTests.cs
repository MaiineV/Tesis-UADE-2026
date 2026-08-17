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
    /// Valida el wiring del árbol del Croupier <b>en memoria</b>, armándolo con
    /// <see cref="CroupierAssetBuilder.BuildAIRoot"/>. No carga el <c>.asset</c> a propósito: el
    /// contrato que hay que proteger es el que produce el builder, y depender del asset ataría el
    /// suite a que Unity lo haya reimportado (el accidente que ya hizo fallar tests verdes).
    /// </summary>
    /// <remarks>
    /// Cubre lo que un merge puede romper sin que se note: el orden de los gates de fase, los
    /// fallbacks que evitan que un paso fallido le cancele el turno al jefe, y los números de la ficha.
    /// </remarks>
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
            // El telegráfico se cobra al abrir el turno, como ExecuteTelegraph en el resto de los
            // bosses. Va suelto (sin Selector) porque siempre devuelve Succeeded.
            Assert.IsInstanceOf<AINode_DetonateSungSectors>(_root.Children[0],
                "El primer hijo del Sequence raíz tiene que detonar lo cantado el turno pasado.");
        }

        [Test]
        public void Root_TicksThePhaseGate_BeforeTheAttack()
        {
            // En el path no-coroutine un Running aborta el Sequence, y el ataque de este jefe es el
            // marcado: una fase ubicada después no tickearía nunca en tests ni en simulación.
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
            // El Sequence corta en el primer Failed: sin el fallback, una sala sin bounds o un servicio
            // sin registrar le cancela al jefe todo lo que viene después en el turno.
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
            // Trucada apaga el corrimiento y nada más: la Represalia no mira la fase, y
            // CroupierWheelService cobra los 8 en todo golpe con la rueda trabada igual.
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

        /// <summary>
        /// La regresión del reporte "el crupier no se mueve": el árbol no tenía un solo nodo de
        /// movimiento y el jefe era una estatua.
        /// </summary>
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
        /// El movimiento va <b>último</b> y aislado. Es el único paso del turno que puede devolver
        /// <c>Running</c> (espera el blink), y un <c>Running</c> aborta el Sequence en el path
        /// no-coroutine: con cualquier cosa detrás, el jefe perdería el resto del turno. El
        /// <c>Selector[paso, Wait]</c> es el mismo idiom que documenta <c>SunkenGrandPhaseWiringTests</c>.
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

        /// <summary>
        /// El reacomodo va después de encender el paño: el fuego se prende sobre el sector que
        /// detonó, que no depende de dónde termine parado el jefe, pero el orden fija que ningún paso
        /// de mesa quede detrás del <c>Running</c> del blink.
        /// </summary>
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

        /// <summary>
        /// La confiscación es del <b>número que cayó</b>, no de un dado al azar. Ese es todo el
        /// motivo por el que puede existir: un sorteo silencioso es indistinguible del bloqueo del
        /// Sunken Grand, y el jugador termina creyendo que pelea contra el jefe viejo.
        /// </summary>
        [Test]
        public void Confiscation_TakesTheNumberThatFell_NotARandomDie()
        {
            var block = Descendants(_root).OfType<AINode_RotateBlock>().Single();

            Assert.AreEqual(AINode_RotateBlock.BlockTarget.Dice, block.Target);

            var reader = block.DirectedIndex as AIReadCroupierWheelNumber;
            Assert.IsNotNull(reader,
                "Sin el reader de la ruleta el nodo cae al sorteo al azar, que es exactamente la " +
                "versión que se había sacado por leerse como el Sunken Grand.");

            Assert.AreEqual(AIReadCroupierWheelNumber.NumberSource.Detonated, reader.Source,
                "Detonated y no Sung: el dado se lo lleva el número que YA resolvió. Leer Sung acá " +
                "devuelve el número siguiente sin fallar — la peor forma de estar mal.");
        }

        /// <summary>
        /// La otra mitad de la condición para que vuelva: que se vea. Los ids vacíos dejan el nodo
        /// mudo (es un nodo compartido con los jefes viejos, ahí vacío = silencio a propósito).
        /// </summary>
        [Test]
        public void Confiscation_IsPresented()
        {
            var block = Descendants(_root).OfType<AINode_RotateBlock>().Single();

            Assert.AreEqual(BossFeedbackIds.CroupierConfiscaVfx, block.BlockVfxId);
            Assert.AreEqual(BossFeedbackIds.CroupierConfiscaFeel, block.BlockFeelId);
        }

        /// <summary>
        /// El orden que hace que el reader tenga algo que leer.
        /// </summary>
        /// <remarks>
        /// <c>AINode_IgniteDetonatedSectors</c> consume la lista con <c>ClearDetonated()</c>. Si la
        /// confiscación quedara después, el reader leería una lista vacía, devolvería <c>-1</c> y el
        /// nodo no bloquearía nada — sin error, sin warning, sin nada en pantalla. Este test es el
        /// que atrapa ese reordenamiento.
        /// </remarks>
        [Test]
        public void Confiscation_RunsBeforeTheIgnitionConsumesTheDetonatedSectors()
        {
            var order = Descendants(_root);

            int block = order.FindIndex(n => n is AINode_RotateBlock);
            int ignite = order.FindIndex(n => n is AINode_IgniteDetonatedSectors);

            Assert.Greater(block, -1, "No hay confiscación en el árbol.");
            Assert.Greater(ignite, -1, "No hay ignición en el árbol.");
            Assert.Less(block, ignite,
                "La confiscación tiene que correr antes de la ignición: la ignición consume " +
                "DetonatedSectors y dejaría al reader leyendo una lista vacía.");
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
            // La ficha cuenta rondas del JUGADOR (2 en fase 1, 3 en fase 2) y el asset autora rondas de
            // hazard: no son el mismo número. HazardService.TickInstanceDurations descuenta una en cada
            // OnTurnQueueBuilt — o sea en cada wrap de ronda — y el fuego nace en el turno del jefe, con
            // el turno del jugador de esa ronda ya jugado (CNF-006 lo fuerza al frente de la cola). La
            // ronda del encendido no le llega a cobrar nunca: DurationRounds = D deja D-1 cierres de
            // turno que sí pegan. Ese es el corrimiento del nombre del test. Que queden 2 y no 1 es lo
            // que hace que el bloque anterior siga ardiendo cuando cae el siguiente: el paño se gasta
            // en vez de volver a foja cero cada turno.
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
                // Act — prefab visual y retrato van en null: son assets, y lo que se afirma acá son los
                // números de la ficha. El wiring visual vive en CroupierVisualWiringTests.
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

        /// <summary>
        /// Tree-walker por reflexión: todo lo alcanzable desde <paramref name="root"/>, sin descender en
        /// <see cref="UnityEngine.Object"/> (no arrastra assets referenciados). Copiado del suite del
        /// Sunken Grand, que es el patrón de este tipo de test.
        /// </summary>
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
