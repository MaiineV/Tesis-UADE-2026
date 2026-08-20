using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Wiring del árbol del Croupier <b>en memoria</b>: contra el builder y no contra el
    /// <c>.asset</c>, que ataría el suite a que Unity lo haya reimportado. El jefe es un kiter de
    /// dos tiempos, y lo que se cubre acá es lo que un merge puede romper sin que se note: el
    /// candado que tiene que re-emitirse todos los turnos, el tiempo de quema que tiene que
    /// quedarse quieto, y la duración del fuego de la que cuelga todo el plan del jefe.
    /// </summary>
    [TestFixture]
    public class CroupierPhaseWiringTests
    {
        private const float PercentTolerance = 0.0001f;

        private SpecialTileDefinitionSO _fire;
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _fire = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _fire.hideFlags = HideFlags.HideAndDontSave;

            _root = CroupierAssetBuilder.BuildAIRoot(_fire);
            Assert.IsNotNull(_root, "BuildAIRoot debería devolver un Sequence.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_fire != null) Object.DestroyImmediate(_fire);
        }

        // =====================================================================
        // Estructura del turno
        // =====================================================================

        /// <summary>
        /// El primer paso del ciclo que devuelve <c>Running</c> (el blink de la fuga) aborta el
        /// Sequence raíz en el path no-coroutine, así que todo lo que quede detrás del Alternate no
        /// tickea nunca. Los dos gates de HP son justamente lo que no se puede perder.
        /// </summary>
        [Test]
        public void PhaseGates_TickBeforeTheTwoBeatCycle()
        {
            int alternateIdx = _root.Children.FindIndex(c => c is AINode_Alternate);
            int plenoIdx = IndexOfGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            int lockIdx = IndexOfGateAtPercent(CroupierAssetBuilder.LockHpThreshold);

            Assert.Greater(alternateIdx, -1, "No hay ciclo de dos tiempos en la raíz del árbol.");
            Assert.Greater(plenoIdx, -1, "No hay gate de HP al 50% (Pleno y color) en el árbol.");
            Assert.Greater(lockIdx, -1, "No hay gate de HP al 70% (el candado) en el árbol.");

            Assert.Less(plenoIdx, alternateIdx,
                "Pleno y color quedó detrás del Alternate: el turno de fuga devuelve Running y le " +
                "corta el Sequence, así que el gate no se evaluaría.");
            Assert.Less(lockIdx, alternateIdx,
                "El candado quedó detrás del Alternate: el Running de la fuga le corta el Sequence.");
        }

        /// <summary>
        /// El Sequence raíz corta en el primer Failed y el Alternate avanza el índice igual: un paso
        /// suelto que falla le cancela al jefe el resto del turno <b>y</b> desincroniza el ciclo.
        /// </summary>
        [Test]
        public void EveryStepTheRootTicks_IsIsolatedInASelectorWithWaitFallback()
        {
            AssertChildrenAreGuarded(_root.Children, "el Sequence raíz");
        }

        // =====================================================================
        // Pleno y color (50%)
        // =====================================================================

        [Test]
        public void PlenoGate_HasAWaitElse_SoItNeverAbortsTheSequence()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);

            Assert.IsNotNull(gate);
            Assert.IsInstanceOf<AINode_Wait>(gate.Else,
                "Un If de efecto sin Else devuelve Failed cuando la condición no pasa.");
        }

        [Test]
        public void PlenoGate_IsLatchedByOnce_AndAnnouncesPhase2()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);

            Assert.IsInstanceOf<AINode_Once>(gate.Then,
                "Pleno y color prende TODO el paño: sin Once se re-aplicaría cada turno bajo el " +
                "umbral y la sala no volvería a apagarse nunca.");

            var stat = Descendants(gate.Then).OfType<AINode_ApplyStatModifier>().FirstOrDefault();
            Assert.IsNotNull(stat, "Falta el ApplyStatModifier que dispara el feedback de fase.");
            Assert.AreEqual(2, stat.PhaseIndex);
            Assert.IsTrue(stat.EmitPhaseChangedEvent, "Sin el evento no hay animación ni diálogo de fase 2.");
            Assert.AreEqual(0, stat.AttackDelta, "La fase NO sube el daño del jefe.");
            Assert.AreEqual(0, stat.SpeedDelta, "La fase no lo apura: lo que cambia es el paño.");
        }

        /// <summary>
        /// <c>AllExceptSquareAroundSelf</c> usa el <c>Size</c> como radio del <b>hueco</b> que se
        /// salva, no del área amenazada: leerlo al revés le prende debajo de los pies y le deja el
        /// resto del paño limpio, que es exactamente el efecto contrario.
        /// </summary>
        [Test]
        public void PlenoGate_BurnsTheWholeTableExceptHisOwn3x3()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            var mark = Descendants(gate.Then).OfType<AINode_TelegraphMark>().Single();

            Assert.AreEqual(ThreatShape.AllExceptSquareAroundSelf, mark.Shape);
            Assert.AreEqual(CroupierAssetBuilder.PlenoHoleRadius, mark.Size,
                "El Size de esta shape es el hueco que NO se prende, y sale de la ficha.");
            Assert.Greater(mark.Size, 0,
                "Con hueco 0 se prende su propia casilla: el jefe queda parado en el fuego y " +
                "cualquier regresión de OwnerBossImmune lo mata solo.");
        }

        /// <summary>
        /// Marca y enciende en el mismo turno: el fuego <i>es</i> su propia telegrafía (se ve en el
        /// piso y sólo cobra al pisarlo o al arrancar el turno adentro), así que no hace falta el
        /// turno de aviso que sí necesita un golpe que cobra de una.
        /// </summary>
        [Test]
        public void PlenoGate_MarksAndIgnitesInTheSameTurn()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            var order = Descendants(gate.Then);

            int mark = order.FindIndex(n => n is AINode_TelegraphMark);
            int ignite = order.FindIndex(n => n is AINode_IgniteArea);

            Assert.Greater(mark, -1, "Pleno y color no marca nada: IgniteArea consume la marca, no la calcula.");
            Assert.Greater(ignite, mark,
                "La ignición va después del marcado: consume el área telegrafiada, así que antes no " +
                "tendría nada que plantar y el 50% pasaría en silencio.");
        }

        /// <summary>
        /// Los pasos de adentro van <b>desnudos</b> a propósito, al revés del resto del árbol: el
        /// bloque entero ya está envuelto en <c>Selector[If, Wait]</c>, y un Wait de fallback acá
        /// haría que el Sequence devuelva Succeeded aunque no haya marcado ni prendido nada —
        /// <c>AINode_Once</c> latchearía sobre esa mentira y el 50% no volvería a intentarse.
        /// </summary>
        [Test]
        public void PlenoBlock_IsGuardedAsAWhole_NotStepByStep()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);

            Assert.IsEmpty(Descendants(gate.Then).OfType<AINode_Wait>(),
                "Un Wait dentro del bloque de Pleno y color le deja latchear el Once sin haber " +
                "prendido el paño: el efecto se pierde para toda la pelea, sin error ni warning.");

            var wrapper = _root.Children.OfType<AINode_Selector>()
                .Single(s => s.Children.Contains(gate));
            Assert.IsTrue(wrapper.Children.Any(c => c is AINode_Wait),
                "El aislamiento del bloque vive en el Selector de afuera: sin su Wait, un fallo " +
                "adentro le corta el turno al jefe.");
        }

        // =====================================================================
        // El candado (70%)
        // =====================================================================

        /// <summary>
        /// La de más valor del archivo: el candado es "permanente" por re-emisión, no por latch.
        /// <c>AINode_RotateBlock</c> hace <c>dice.Clear()</c> antes de bloquear en cada tick y
        /// <c>DiceBlockService</c> se limpia solo al cerrar cada turno del jugador.
        /// </summary>
        [Test]
        public void DieLock_IsNotLatchedByOnce_SoItSurvivesTheEndOfEveryPlayerTurn()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.LockHpThreshold);

            Assert.IsInstanceOf<AINode_RotateBlock>(gate.Then,
                "El candado tiene que colgar directo del If: cualquier decorador en el medio " +
                "cambia cada cuánto se re-emite.");
            Assert.IsEmpty(Descendants(gate).OfType<AINode_Once>(),
                "Con Once el candado duraría UN turno: RotateBlock hace Clear() antes de bloquear y " +
                "DiceBlockService se limpia al cerrar cada turno del jugador, así que 'permanente' " +
                "se consigue re-emitiéndolo todos los turnos.");
            Assert.IsInstanceOf<AINode_Wait>(gate.Else,
                "Por encima del 70% el gate tiene que ser transparente, no Failed.");
        }

        /// <summary>Adentro del Alternate sólo se emitiría uno de cada dos turnos, y el candado
        /// parpadearía: puesto en el turno del jefe, borrado al cerrar el del jugador.</summary>
        [Test]
        public void DieLock_LivesOutsideTheAlternate_SoThePadlockDoesNotBlink()
        {
            var alternate = Alternate();

            Assert.IsEmpty(Descendants(alternate).OfType<AINode_RotateBlock>(),
                "El candado quedó adentro del ciclo de dos tiempos: se emitiría un turno sí y otro " +
                "no, y el jugador vería el dado bloqueado parpadear.");
            Assert.AreEqual(1, Descendants(_root).OfType<AINode_RotateBlock>().Count(),
                "Un solo nodo de candado en todo el árbol.");
        }

        /// <summary>Índice fijo y no un sorteo: el candado se tiene que leer como "me saco ESE".</summary>
        [Test]
        public void DieLock_TakesAFixedDie_AndIsPresented()
        {
            var block = Descendants(_root).OfType<AINode_RotateBlock>().Single();

            Assert.AreEqual(AINode_RotateBlock.BlockTarget.Dice, block.Target);
            Assert.AreEqual(CroupierAssetBuilder.LockedDieIndex, ReadInt(block.DirectedIndex),
                "Un sorteo por turno se lee como un porcentaje, no como una confiscación.");
            Assert.AreEqual(BossFeedbackIds.CroupierConfiscaVfx, block.BlockVfxId);
            Assert.AreEqual(BossFeedbackIds.CroupierConfiscaFeel, block.BlockFeelId);
        }

        [Test]
        public void DieLock_ArmsBeforeTheHalfHpBurn_SoTheTwoThresholdsDoNotCollide()
        {
            Assert.Greater(CroupierAssetBuilder.LockHpThreshold, CroupierAssetBuilder.PlenoHpThreshold,
                "El candado tiene que llegar antes que Pleno y color: si cayeran juntos, el jugador " +
                "come las dos escaladas en el mismo turno.");
        }

        // =====================================================================
        // Los dos tiempos
        // =====================================================================

        /// <summary>
        /// Dispara <b>antes</b> de huir: al revés, el tiro saldría desde la casilla nueva y el
        /// jugador vería el fogonazo salir de donde el jefe ya no está.
        /// </summary>
        [Test]
        public void DealBeat_ShootsBeforeFleeing_AndMarksTheBandFromWhereItLands()
        {
            var order = Descendants(DealBeat());

            int shot = order.FindIndex(n => n is AINode_RangedShot);
            int flee = order.FindIndex(n => n is AINode_KeepDistance);
            int mark = order.FindIndex(n => n is AINode_TelegraphMark);

            Assert.Greater(shot, -1, "El tiempo de reparto no dispara.");
            Assert.Greater(flee, shot, "La fuga va después del disparo.");
            Assert.Greater(mark, flee,
                "La banda está anclada en el jefe: marcarla antes de huir la dejaría apuntando desde " +
                "la casilla vieja y el fuego no caería donde se anunció.");
        }

        /// <summary>
        /// El disparo se auto-gatea por rango. Si el alcance no cubriera la distancia que él mismo
        /// sostiene, se kitearía fuera de su propio rango y el tiempo de reparto quedaría mudo.
        /// </summary>
        [Test]
        public void DealBeat_ShotOutrangesHisOwnFlight()
        {
            var shot = Descendants(_root).OfType<AINode_RangedShot>().Single();

            Assert.AreEqual(CroupierAssetBuilder.ShotDamage, shot.Damage);
            Assert.AreEqual(CroupierAssetBuilder.ShotRange, shot.Range);
            Assert.AreEqual(DistanceMetric.Manhattan, shot.Metric,
                "Misma métrica que AINode_KeepDistance: con otra, la distancia que sostiene y la " +
                "que alcanza el tiro dejan de ser el mismo número.");
            Assert.GreaterOrEqual(shot.Range, CroupierAssetBuilder.FleeIdealDistance,
                "Huye hasta FleeIdealDistance: con un alcance menor se sale solo de rango y el " +
                "tiempo de reparto no hace nada.");
        }

        [Test]
        public void DealBeat_MarksAFullDepthBandFromTheSheet()
        {
            var mark = Descendants(DealBeat()).OfType<AINode_TelegraphMark>().Single();

            Assert.AreEqual(ThreatShape.DirectionalBand, mark.Shape);
            Assert.AreEqual(CroupierAssetBuilder.BandHalfWidth, mark.Size,
                "Size es el SEMI-ancho de la banda, y sale de la ficha.");
            Assert.AreEqual(CroupierAssetBuilder.BandDepth, mark.Depth,
                "La banda tiene que llegar a la pared: más corta deja un pedazo de pasillo sin " +
                "quemar por donde rodearla de una.");
            Assert.AreEqual(0, mark.Damage,
                "La marca no cobra nada: el daño lo cobran las casillas de fuego que planta el " +
                "turno siguiente, y un Damage acá se anunciaría como un golpe que nunca llega.");
        }

        /// <summary>
        /// El tiempo de quema es el único turno en que se queda quieto, y eso es lo que lo hace
        /// matable: sin este turno el jefe es un kiter perpetuo.
        /// </summary>
        [Test]
        public void BurnBeat_NeitherMovesNorShoots()
        {
            var burn = Descendants(BurnBeat());

            Assert.IsNotEmpty(burn.OfType<AINode_IgniteArea>(), "El tiempo de quema no prende nada.");
            Assert.IsEmpty(burn.OfType<AINode_RangedShot>(),
                "Un disparo en el turno de quema le saca la ventana en la que se le puede entrar.");
            Assert.IsEmpty(burn.OfType<AINode_KeepDistance>(),
                "Si también huye en el turno de quema no hay turno en que se lo pueda alcanzar.");
            Assert.IsEmpty(burn.OfType<AINode_Move>(),
                "Ídem con un Move: quedarse quieto es lo que lo hace matable.");
        }

        [Test]
        public void Ignitions_PlantTheBossOwnFireDefinition()
        {
            var ignitions = Descendants(_root).OfType<AINode_IgniteArea>().ToList();

            Assert.IsNotEmpty(ignitions);
            foreach (var ignite in ignitions)
            {
                Assert.AreSame(_fire, ignite.Definition,
                    "Los dos tiempos que prenden plantan la MISMA definición, la propia del jefe: " +
                    "Tile_FireTemp es la genérica y tunearla ahí le cambiaría el fuego a todo el juego.");
                // No se compara contra una sola constante porque ahora hay dos duraciones: la
                // base y la de fase 2. Lo que no puede pasar nunca es el 0 — en el nodo cae al
                // default del SO y en ISpecialTileService.Place un 0 significa PERMANENTE.
                Assert.Contains(ignite.DurationRounds,
                    new[] { CroupierAssetBuilder.FireDurationRounds, CroupierAssetBuilder.FireDurationRoundsPhase2 },
                    $"Una ignición pasa {ignite.DurationRounds} rondas, que no es ni la duración " +
                    "base ni la de fase 2: o es un número suelto que nadie va a mantener, o es un " +
                    "0 que deja el fuego encendido para siempre.");
            }
        }

        /// <summary>
        /// La relación entre cuánto arde una banda y cada cuánto prende una nueva. No es un detalle
        /// de balance: es la diferencia entre un fuego que se esquiva y un piso que se achica.
        /// </summary>
        /// <remarks>
        /// El jefe prende en uno de cada dos tiempos, o sea cada 2 rondas, y nadie apaga las bandas
        /// anteriores. Con la duración base <b>igual</b> al intervalo, una banda se apaga justo
        /// cuando nace la siguiente: nunca conviven dos y el paño vuelve a estar limpio. Con la de
        /// fase 2, un ronda más, conviven durante la ronda del relevo — el único momento en que el
        /// piso útil se achica. Que la base <b>supere</b> el intervalo es el bug: las bandas se
        /// apilan ronda a ronda hasta que no queda dónde plantarse a defender.
        /// </remarks>
        [Test]
        public void FireDuration_MatchesTheIgnitionInterval_AndOnlyPhaseTwoOverlaps()
        {
            var alternate = Alternate();
            int burningBeats = alternate.Children.Count(c => Descendants(c).Any(n => n is AINode_IgniteArea));

            Assert.AreEqual(1, burningBeats,
                "Prende en uno de los tiempos y sólo uno: el intervalo entre igniciones sale de ahí.");

            int ignitionIntervalRounds = alternate.Children.Count;

            // "Arde N rondas" se autora como N + 1 (la ronda en que nace no le deja al jugador
            // ningún arranque de turno por delante), así que lo que se compara contra el intervalo
            // es la duración menos uno.
            Assert.AreEqual(ignitionIntervalRounds, CroupierAssetBuilder.FireDurationRounds - 1,
                $"La banda base arde {CroupierAssetBuilder.FireDurationRounds - 1} rondas y prende " +
                $"cada {ignitionIntervalRounds}. Por encima del intervalo las bandas se apilan y la " +
                "sala se queda sin piso; por debajo el paño está limpio la mitad de la pelea y el " +
                "fuego deja de ser una amenaza que hay que rodear.");

            Assert.AreEqual(CroupierAssetBuilder.FireDurationRounds + 1,
                CroupierAssetBuilder.FireDurationRoundsPhase2,
                "Fase 2 tiene que ser exactamente una ronda más: es lo que hace que dos bandas " +
                "convivan durante el relevo. Dos rondas más y vuelve a apilarse sin techo.");
        }

        /// <summary>
        /// Cuál de las dos duraciones usa cada ignición. El nodo no lee el HP: la elige un
        /// <c>AINode_If</c>, así que si alguien colapsa las dos ramas en una la pelea pierde el
        /// escalón de fase 2 sin que falle nada.
        /// </summary>
        [Test]
        public void PhaseTwo_IsTheOnlyThingThatLengthensAFire()
        {
            var ignitions = Descendants(_root).OfType<AINode_IgniteArea>().ToList();

            Assert.AreEqual(1, ignitions.Count(i => i.DurationRounds == CroupierAssetBuilder.FireDurationRounds),
                "Tiene que haber exactamente una ignición con la duración base: la de la banda " +
                "mientras el jefe está por encima del 50%.");
            Assert.AreEqual(2, ignitions.Count(i => i.DurationRounds == CroupierAssetBuilder.FireDurationRoundsPhase2),
                "Y dos con la de fase 2: la banda por debajo del 50% y el propio Pleno, que prende " +
                "justo al cruzar el umbral y por lo tanto ya está en fase 2.");

            // El If que ramifica: sin él las dos duraciones existirían en el árbol pero el jefe
            // usaría siempre la misma.
            var gates = Descendants(Alternate())
                .OfType<AINode_If>()
                .Where(g => Descendants(g.Then).Any(n => n is AINode_IgniteArea)
                            && Descendants(g.Else).Any(n => n is AINode_IgniteArea))
                .ToList();

            Assert.AreEqual(1, gates.Count,
                "El tiempo de quema dejó de ramificar por fase: las dos duraciones siguen escritas " +
                "en el árbol pero el jefe usa una sola.");
            Assert.IsTrue(gates[0].Conditions.OfType<PcOwnerHpBelow>()
                    .Any(pc => Mathf.Approximately(pc.Percent, CroupierAssetBuilder.PlenoHpThreshold)),
                "La banda se alarga en un umbral distinto al del Pleno: el jugador vería el fuego " +
                "durar más sin que nada en pantalla se lo haya anunciado.");
        }

        // =====================================================================
        // Números de la ficha
        // =====================================================================

        /// <summary>
        /// Sin <c>IsBoss</c> el jefe no es jefe para <c>SpecialTileService.ShouldAffect</c>, que
        /// exige <c>OwnerBossImmune &amp;&amp; IsBoss &amp;&amp;</c> ser el dueño: el Croupier huye
        /// pegado a la banda que acaba de prender, así que se quema con su propio fuego.
        /// </summary>
        [Test]
        public void PopulateEnemyData_MarksHimAsBoss_SoHeDoesNotBurnInHisOwnFire()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                CroupierAssetBuilder.PopulateEnemyData(data, _fire, null, null);

                Assert.IsTrue(data.IsBoss,
                    "Ningún builder venía escribiendo IsBoss y el jefe contaba como enemigo común: " +
                    "OwnerBossImmune no lo protege y muere en su propio Pleno y color.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
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
                CroupierAssetBuilder.PopulateEnemyData(data, _fire, null, null);

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
                Assert.IsEmpty(data.Behaviors,
                    "Sin behaviors: su único golpe directo es el disparo del árbol, y un behavior de " +
                    "melee le sumaría un ataque más por turno además del ciclo.");
                Assert.IsNotNull(data.AIRoot);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Tree_HasNoBehaviorNodes()
        {
            Assert.IsEmpty(Descendants(_root).OfType<AINode_Behavior>(),
                "El disparo entra por AINode_RangedShot, que se auto-gatea por rango. Un " +
                "AINode_Behavior en el árbol traería su propio alcance y su propio número de daño.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private AINode_Alternate Alternate()
        {
            var alternate = Descendants(_root).OfType<AINode_Alternate>().SingleOrDefault();
            Assert.IsNotNull(alternate, "No hay ciclo de dos tiempos en el árbol.");
            Assert.AreEqual(2, alternate.Children.Count, "El jefe es de DOS tiempos: reparte y quema.");
            return alternate;
        }

        /// <summary>Tiempo 1: dispara, huye y marca la banda.</summary>
        private AIDecisionNode DealBeat()
        {
            var beat = Alternate().Children
                .FirstOrDefault(c => Descendants(c).Any(n => n is AINode_RangedShot));
            Assert.IsNotNull(beat, "Ningún tiempo dispara.");
            return beat;
        }

        /// <summary>Tiempo 2: prende lo que marcó el tiempo pasado y no hace nada más.</summary>
        private AIDecisionNode BurnBeat()
        {
            var beat = Alternate().Children
                .FirstOrDefault(c => Descendants(c).Any(n => n is AINode_IgniteArea));
            Assert.IsNotNull(beat, "Ningún tiempo prende el paño.");
            return beat;
        }

        /// <summary>
        /// Todo hijo de un contenedor que la raíz tickea tiene que ser <c>Selector[paso, Wait]</c>.
        /// No entra en los Selectors: lo que cuelga adentro ya está aislado (ver
        /// <see cref="PlenoBlock_IsGuardedAsAWhole_NotStepByStep"/>).
        /// </summary>
        private static void AssertChildrenAreGuarded(IEnumerable<AIDecisionNode> children, string container)
        {
            foreach (var child in children)
            {
                if (child is AINode_Wait) continue;

                if (child is AINode_Sequence sequence)
                {
                    AssertChildrenAreGuarded(sequence.Children, $"el Sequence de {container}");
                    continue;
                }

                if (child is AINode_Alternate alternate)
                {
                    AssertChildrenAreGuarded(alternate.Children, "el Alternate");
                    continue;
                }

                var wrapper = child as AINode_Selector;
                Assert.IsNotNull(wrapper,
                    $"{child?.GetType().Name} está suelto en {container}: si devuelve Failed o " +
                    "Running, el jefe pierde el resto del turno. Envolverlo en Selector[paso, Wait].");
                Assert.IsTrue(wrapper.Children.Any(c => c is AINode_Wait),
                    $"El Selector de {container} que envuelve a " +
                    $"{wrapper.Children.FirstOrDefault()?.GetType().Name} no tiene Wait de fallback " +
                    "— devolvería Failed igual.");
            }
        }

        private static int ReadInt(AIIntReader reader)
        {
            var constant = reader as AIConstantInt;
            Assert.IsNotNull(constant, "Se esperaba un AIConstantInt (valor literal del inspector).");
            return constant.Value;
        }

        /// <summary>Gate de HP por su umbral, venga suelto o envuelto en el Selector de aislamiento.</summary>
        private AINode_If FindGateAtPercent(float percent)
        {
            var gate = _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g?.Conditions != null && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - percent) < PercentTolerance));
            Assert.IsNotNull(gate, $"No hay gate de HP al {percent:P0} en el árbol.");
            return gate;
        }

        private int IndexOfGateAtPercent(float percent)
        {
            var gate = FindGateAtPercent(percent);
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
