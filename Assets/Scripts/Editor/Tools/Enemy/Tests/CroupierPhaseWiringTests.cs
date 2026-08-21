using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
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
    /// quedarse quieto, la duración del fuego de la que cuelga todo el plan del jefe, y el orden
    /// del bloque de "Pleno y color" — donde mover un nodo un lugar le cambia el efecto entero.
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
        /// El orden de los cuatro pasos de la raíz, que es el que define el turno: <b>detonar lo
        /// avisado → candado → la acción normal del turno → armar el Pleno</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El armado del Pleno va <b>último, después</b> del Alternate, y no es un detalle de
        /// prolijidad: es lo que hace que el turno del aviso no sea un turno perdido para el jefe
        /// —dispara o prende su banda igual— y lo que le deja al jugador el turno entero para cruzar
        /// la sala. Adelante del ciclo, el aviso y la acción del turno se pisarían en el mismo tick.
        /// </para>
        /// <para>
        /// La detonación va <b>primera</b> por dos razones distintas, las dos load-bearing: arriba del
        /// marcado es lo que separa el marcar del prender por un turno (ver
        /// <see cref="PlenoGate_IgnitesTheTurnAfterItMarked"/>), y arriba del Alternate es lo que
        /// evita que le pase el trapo al overlay de la banda que T1 acaba de levantar —
        /// <c>Show</c>/<c>Clear</c> del overlay son por fuente y la ignición limpia la de su canal.
        /// </para>
        /// <para>
        /// El candado, en cambio, sí tiene que estar <b>delante</b> del Alternate: el primer paso del
        /// ciclo que devuelve <c>Running</c> (el blink de la fuga) aborta el Sequence raíz en el path
        /// no-coroutine, y un candado que se saltea un turno se ve parpadear.
        /// </para>
        /// </remarks>
        [Test]
        public void TurnOrder_DetonatesFirst_ThenLocks_ThenActs_AndArmsThePlenoLast()
        {
            int detonationIdx = IndexOfStep<AINode_IgniteArea>();
            int lockIdx = IndexOfGateAtPercent(CroupierAssetBuilder.LockHpThreshold);
            int alternateIdx = IndexOfStep<AINode_Alternate>();
            int armIdx = IndexOfGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);

            Assert.Greater(detonationIdx, -1,
                "No hay paso de ignición en la raíz: lo que 'Pleno y color' marca no lo prende nadie.");
            Assert.Greater(lockIdx, -1, "No hay gate de HP al 70% (el candado) en el árbol.");
            Assert.Greater(alternateIdx, -1, "No hay ciclo de dos tiempos en la raíz del árbol.");
            Assert.Greater(armIdx, -1, "No hay gate de HP al 50% (Pleno y color) en el árbol.");

            Assert.AreEqual(0, detonationIdx,
                "La detonación de lo avisado dejó de ser el primer paso del turno. Detrás del " +
                "Alternate le apaga el overlay a la banda que T1 acaba de levantar (Clear y Show son " +
                "por fuente), y detrás del armado marca y prende en el mismo tick.");
            Assert.Less(lockIdx, alternateIdx,
                "El candado quedó detrás del Alternate: el Running de la fuga le corta el Sequence.");
            Assert.Greater(armIdx, alternateIdx,
                "El armado del Pleno se adelantó al ciclo. Ahí el jefe se planta en el centro y marca " +
                "el paño en vez de repartir o quemar, y el turno del aviso pasa a ser un turno " +
                "regalado — con el agregado de que el jugador ya no tiene el turno entero para cruzar.");
            Assert.AreEqual(_root.Children.Count - 1, armIdx,
                "El armado del Pleno tiene que ser el último paso: es el único que puede quedar " +
                "detrás del Alternate porque prende al turno siguiente, no en este.");
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
        /// <remarks>
        /// El tamaño del hueco no se escribe acá: sale de <c>PlenoHoleRadius</c> (hoy 1, o sea el 3×3
        /// que describe la ficha). El nombre del test se quedó genérico a propósito — cruzarlo contra
        /// la constante es lo que hace que mover el radio no deje el nombre mintiendo.
        /// </remarks>
        [Test]
        public void PlenoGate_BurnsTheWholeTableExceptTheSquareAroundHim()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            var mark = Descendants(gate.Then).OfType<AINode_TelegraphMark>().Single();

            Assert.AreEqual(ThreatShape.AllExceptSquareAroundSelf, mark.Shape);
            Assert.AreEqual(CroupierAssetBuilder.PlenoHoleRadius, mark.Size,
                "El Size de esta shape es el hueco que NO se prende, y sale de la ficha.");
            Assert.Greater(mark.Size, 0,
                "Con hueco 0 se prende su propia casilla: el jefe queda parado en el fuego y " +
                "cualquier regresión de OwnerBossImmune lo mata solo.");

            // Esta marca SÍ cobra y la banda no, y las dos avisan un turno antes: lo que las
            // diferencia es cuánto cuesta obedecer el aviso. Salirse de la banda es un paso al
            // costado; salirse de esto es cruzar media sala hasta el hueco. El número lo cobra
            // AINode_IgniteArea al consumir la marca, y un 0 acá deja el momento más grande de la
            // pelea sin acuse de recibo para quien no se movió.
            Assert.AreEqual(CroupierAssetBuilder.PlenoIgnitionDamage, mark.Damage,
                "El Pleno dejó de cobrar al prender: quien estaba parado adentro no se entera hasta " +
                "su próximo turno.");
        }

        /// <summary>
        /// El hueco a salvo se calcula desde la casilla del jefe <b>en el momento del tick</b>, así
        /// que el teleport tiene que correr <b>antes</b> del marcado. Detrás, el hueco vuelve a caer
        /// donde el jefe había terminado de huir —contra una pared— y el 50% es el mecanismo viejo
        /// con un nodo de más.
        /// </summary>
        [Test]
        public void PlenoGate_TeleportsToTheCentreBeforeItRaisesTheTelegraph()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            var order = Descendants(gate.Then);

            int teleport = order.FindIndex(n => n is AINode_TeleportToRoomCenter);
            int mark = order.FindIndex(n => n is AINode_TelegraphMark);

            Assert.Greater(teleport, -1,
                "El Pleno dejó de plantar al jefe en el centro: el hueco cae donde haya terminado " +
                "de huir, así que la figura sale distinta cada pelea y a veces no hay sala que cruzar.");
            Assert.Greater(mark, teleport,
                "El teleport quedó DESPUÉS del marcado. AINode_TelegraphMark ancla la forma en la " +
                "casilla del jefe al tickear, y AINode_IgniteArea consume esa marca sin recalcularla: " +
                "moverlo después deja el cuadrado a salvo vacío en el medio de la sala.");
        }

        /// <summary>
        /// El teleport consume el movimiento del turno. Sin eso el jefe se va del hueco que acaba
        /// de plantar, y el área ya quedó anclada donde estaba: el cuadrado a salvo se queda vacío
        /// en el medio de la sala y deja de leerse como "donde está el jefe".
        /// </summary>
        [Test]
        public void PlenoTeleport_SpendsTheTurnsMovement_SoHeStaysInHisOwnHole()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            var teleport = Descendants(gate.Then).OfType<AINode_TeleportToRoomCenter>().Single();

            Assert.IsTrue(teleport.ConsumeMoveAction,
                "El mismo turno en que cruza el 50% el Alternate puede caer en T1, y ese beat tiene " +
                "un KeepDistance con FleeIdealDistance 8 que huye casi siempre: sin gastarle el " +
                "movimiento, lo saca del centro justo después de plantarlo ahí.");
        }

        /// <summary>
        /// <b>Marca en el turno N y prende en el N+1.</b> Era el bug reportado: marcaba y prendía en
        /// el mismo tick, y como no hay yield entre el <c>Show</c> del telegraph y el <c>Clear</c> de
        /// la ignición, el aviso no se dibujaba <i>ni un frame</i> — el paño se prendía entero sin
        /// aviso ninguno.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El turno de separación lo da el <b>orden de los hijos de la raíz</b>, no un contador: el
        /// paso que prende está <b>arriba</b> del que marca, así que en el turno N pasa primero y no
        /// encuentra nada, la marca se levanta después y queda pendiente con su overlay puesto todo
        /// el turno del jugador, y recién la encuentra en el N+1.
        /// </para>
        /// <para>
        /// Por eso <c>AnnounceTurns</c> tiene que quedarse en <b>0</b>: el nodo cuenta sus propias
        /// activaciones, así que un 1 le sumaría SU turno de espera arriba del que ya da el orden y
        /// la detonación caería en N+2 — el paño quedaría avisado dos turnos y el jugador dejaría de
        /// creerle a la telegrafía.
        /// </para>
        /// <para>
        /// Y por eso el paso que prende <b>no</b> puede quedar latcheado ni gateado: el turno en que
        /// tickea con algo pendiente es el siguiente al del aviso, y ahí ya cruzó el umbral hace un
        /// turno. Un <c>Once</c> o un gate encima y la marca se queda pintada para siempre.
        /// </para>
        /// </remarks>
        [Test]
        public void PlenoGate_IgnitesTheTurnAfterItMarked()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            var arm = Descendants(gate.Then);

            Assert.IsNotEmpty(arm.OfType<AINode_TelegraphMark>(),
                "Pleno y color no marca nada: AINode_IgniteArea consume la marca, no la calcula.");
            Assert.IsEmpty(arm.OfType<AINode_IgniteArea>(),
                "La ignición volvió adentro del bloque que marca: eso es marcar y prender en el " +
                "mismo tick, o sea prender el paño entero sin que el aviso llegue a dibujarse un " +
                "solo frame. Es el bug que este orden arregla.");

            int detonationIdx = IndexOfStep<AINode_IgniteArea>();
            var detonationStep = _root.Children[detonationIdx];
            var detonation = Unwrap<AINode_IgniteArea>(detonationStep);

            Assert.Less(detonationIdx, IndexOfGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold),
                "El paso que prende quedó DEBAJO del que marca: en el mismo turno encuentra la marca " +
                "que se acaba de levantar y vuelven a caer en el mismo tick.");

            Assert.AreEqual(0, detonation.AnnounceTurns,
                "El turno de espera ya lo da el orden de los hijos. Con AnnounceTurns en 1 el nodo " +
                "suma su propia espera encima y el paño prende en N+2, dos turnos después del aviso.");
            Assert.IsEmpty(Descendants(detonationStep).OfType<AINode_Once>(),
                "El paso que prende quedó latcheado. Tickea con algo pendiente el turno DESPUÉS del " +
                "aviso, así que un Once (o un gate de HP) lo saltea justo entonces y la marca se " +
                "queda pintada para siempre.");
        }

        /// <summary>
        /// Las dos marcas del jefe conviven: el Pleno marca en su propio canal y la banda de T1 en el
        /// guid pelado. Los dos avisos se levantan en el <b>mismo</b> turno —el que cruza el 50%— y
        /// <c>IThreatenedAreaService</c> guarda un área por fuente <b>sobrescribiendo</b>, así que sin
        /// canal el segundo marcado del turno destruye al primero, en el estado lógico y en el overlay.
        /// </summary>
        /// <remarks>
        /// Y el que consume tiene que pedir el mismo canal: la ignición del tiempo de quema va sin
        /// canal porque consume la banda, y la del paso 1 va con el del Pleno. Cruzados, cada uno
        /// prende el área del otro con la duración del otro.
        /// </remarks>
        [Test]
        public void ThePlenoMarkAndTheBandMark_LiveOnDifferentChannels()
        {
            var plenoMark = Descendants(FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold).Then)
                .OfType<AINode_TelegraphMark>().Single();
            var bandMark = Descendants(DealBeat()).OfType<AINode_TelegraphMark>().Single();

            Assert.AreEqual(CroupierAssetBuilder.PlenoChannelId, plenoMark.ChannelId,
                "La marca del Pleno perdió su canal: cae bajo el guid pelado del jefe, o sea encima " +
                "de la banda que T1 marcó este mismo turno — y la que sobrevive es una sola.");
            Assert.IsTrue(string.IsNullOrEmpty(bandMark.ChannelId),
                "La banda estrenó canal. El tiempo de quema la consume por el guid pelado, así que " +
                "un canal acá deja la banda avisada y sin prender para siempre.");
            Assert.AreNotEqual(plenoMark.ChannelId, bandMark.ChannelId,
                "Los dos avisos comparten canal: se pisan igual que si ninguno lo tuviera.");

            var detonation = Unwrap<AINode_IgniteArea>(
                _root.Children[IndexOfStep<AINode_IgniteArea>()]);
            Assert.AreEqual(plenoMark.ChannelId, detonation.ChannelId,
                "El paso que prende busca en un canal distinto del que marca el Pleno: nunca " +
                "encuentra nada y el 50% no prende jamás.");

            foreach (var burn in Descendants(Alternate()).OfType<AINode_IgniteArea>())
            {
                Assert.IsTrue(string.IsNullOrEmpty(burn.ChannelId),
                    "Una ignición del ciclo pasó a buscar en un canal: la banda se marca en el guid " +
                    "pelado, así que el tiempo de quema deja de prender nada.");
            }
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
                "La banda no puede cobrar al prender: se marca un turno antes, así que el jugador " +
                "tuvo su turno para salirse y quedarse adentro ya es una decisión suya. El daño lo " +
                "cobran las casillas que planta.");
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
        /// <para>
        /// El jefe prende en uno de cada dos tiempos, o sea cada 2 rondas, y nadie apaga las bandas
        /// anteriores. Con la duración base <b>igual</b> al intervalo, una banda se apaga justo cuando
        /// nace la siguiente: nunca conviven dos y el paño vuelve a estar limpio. Con la de fase 2,
        /// una ronda más, conviven durante la ronda del relevo — el único momento en que el piso útil
        /// se achica. Que la base <b>supere</b> el intervalo es el bug: las bandas se apilan ronda a
        /// ronda hasta que no queda dónde plantarse a defender.
        /// </para>
        /// <para>
        /// <b>Convivir no es cobrar doble.</b> Es lo único que cambió en esta corrida: donde se pisan,
        /// la banda nueva sólo prende lo que no ardía, y si la vieja queda <b>entera</b> adentro del
        /// área nueva la retira (<c>AINode_IgniteArea.RetireFullyReplaced</c>, que este jefe prende;
        /// ver <see cref="Ignitions_RelayTheBandTheyReplace"/>). O sea que una casilla es siempre un
        /// fuego. Lo que sigue creciendo con la ronda de convivencia es la <b>superficie</b>, y eso es
        /// exactamente el escalón de fase 2 — no una excusa para pasarse del intervalo.
        /// </para>
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
                "convivan durante el relevo, y ése es el único escalón de dificultad del umbral. " +
                "Dos rondas más y vuelve a apilarse sin techo.");
        }

        /// <summary>
        /// Las tres igniciones relevan lo que reemplazan: una banda vieja que quede <b>entera</b>
        /// adentro del área nueva se retira en vez de quedarse con su reloj.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Es el caso normal de este jefe, no un borde: huye sobre el mismo eje y la banda le sale de
        /// atrás con la profundidad de la sala, así que cada banda nueva contiene a la anterior. Sin
        /// el relevo, el terreno compartido se queda con el reloj más viejo —el más corto— y la banda
        /// que el jugador acaba de ver avisada se apaga en el wrap siguiente sin haber ardido: el
        /// tiempo de quema no muestra nada.
        /// </para>
        /// <para>
        /// Va cableado y no por default porque el default es "no retirar" (ver
        /// <c>AINode_IgniteArea.RetireFullyReplaced</c>): el nodo lo monta cada jefe que prende piso,
        /// y apagar fuego que el jugador ya tiene en pantalla es una decisión de <b>esta</b> pelea.
        /// </para>
        /// </remarks>
        [Test]
        public void Ignitions_RelayTheBandTheyReplace()
        {
            foreach (var ignite in Descendants(_root).OfType<AINode_IgniteArea>())
            {
                Assert.IsTrue(ignite.RetireFullyReplaced,
                    "Una ignición dejó de relevar la banda que tapa por completo. Ese terreno se " +
                    "queda con el reloj de la banda vieja, así que la recién avisada se apaga en el " +
                    "wrap siguiente y el turno de quema pasa en blanco.");
            }
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
                Assert.AreEqual(200, data.BaseHP,
                    "Es el número que ya usan los otros tres jefes del juego. Los 170 de antes eran " +
                    "un descuento por una debilidad que se cobraba casi todos los turnos (el Par); " +
                    "con la debilidad movida al Poker ese descuento se quedó sin motivo. Si hay que " +
                    "hacerlo aguantar más, la palanca es ésta y no el multiplicador.");
                Assert.AreEqual(24, data.BaseAttack);
                Assert.AreEqual(ComboId.Poker, data.WeaknessComboId,
                    "La debilidad es el Poker (cuatro dados iguales), no el Par. El id canónico del " +
                    "catálogo es combo.poker.");
                // Es un override propio del jefe, no el global de WeaknessConfig, así que moverlo
                // no le toca la debilidad a ningún otro enemigo.
                Assert.AreEqual(2.0f, data.WeaknessMultiplierOverride, PercentTolerance,
                    "El ×2 no es una perilla de dificultad: es si la debilidad existe o no. El Par " +
                    "salía 9 de cada 10 primeras tiradas —era un piso que se cobraba todos los " +
                    "turnos, y ahí un ×1.5 ya se sentía—; el Poker sale ~2 de cada 10 gastando el " +
                    "pozo entero, así que con el mismo ×1.5 el bono casi desaparece del daño de la " +
                    "pelea. A ×2 el Poker (55 de base) sale a 110 y vale ir a buscarlo. Si el jefe " +
                    "queda blando, la palanca es BaseHP.");
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

        /// <summary>
        /// La debilidad es el <b>Poker</b>, y el multiplicador es la mitad de esa decisión. Era el
        /// Par a ×1.5: el Par salía 9 de cada 10 tiradas, así que la debilidad era un piso que el
        /// jugador cobraba todos los turnos. El Poker sale ~2 de cada 10 gastando el pozo entero —
        /// el bono pasa de aplicarse siempre a aplicarse a veces, y el ×2 es lo que compensa en la
        /// otra dirección para que siga valiendo ir a buscarlo.
        /// </summary>
        /// <remarks>
        /// Cruza las dos constantes contra lo escrito porque el modo de romper esto es tipear el
        /// valor en <c>PopulateEnemyData</c>: ahí la ficha y el asset se van cada uno para su lado y
        /// el <c>ED_</c> queda con una debilidad que ningún comentario del builder menciona. Y van
        /// juntas en un test porque son <b>una</b> decisión: mover la mano sin mover el multiplicador
        /// (o al revés) es lo que deja la debilidad en decoración o en daño gratis.
        /// </remarks>
        [Test]
        public void Weakness_IsThePokerFromTheSheet_NotAHardcodedId()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                CroupierAssetBuilder.PopulateEnemyData(data, _fire, null, null);

                Assert.AreEqual(ComboId.Poker, CroupierAssetBuilder.WeaknessComboId,
                    "La ficha volvió a colgar la debilidad de otra mano. Si el cambio es a propósito, " +
                    "mover también el multiplicador: el ×2 está calibrado contra una mano que sale " +
                    "poco, y sobre una frecuente es daño gratis todos los turnos.");
                Assert.AreEqual(CroupierAssetBuilder.WeaknessComboId, data.WeaknessComboId,
                    "PopulateEnemyData escribe un id que no es el de la ficha: el asset del jefe y " +
                    "la constante quedaron diciendo cosas distintas.");
                Assert.AreEqual(CroupierAssetBuilder.WeaknessMultiplier,
                    data.WeaknessMultiplierOverride, PercentTolerance,
                    "PopulateEnemyData escribe un multiplicador que no es el de la ficha.");
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
            var gate = _root.Children.Select(Unwrap<AINode_If>).FirstOrDefault(g =>
                g?.Conditions != null && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - percent) < PercentTolerance));
            Assert.IsNotNull(gate, $"No hay gate de HP al {percent:P0} en el árbol.");
            return gate;
        }

        private int IndexOfGateAtPercent(float percent)
        {
            var gate = FindGateAtPercent(percent);
            return _root.Children.FindIndex(c => ReferenceEquals(Unwrap<AINode_If>(c), gate));
        }

        /// <summary>
        /// Índice del paso de la raíz que <b>es</b> un <typeparamref name="T"/>. Deliberadamente
        /// superficial: con una búsqueda en profundidad, la ignición del tiempo de quema haría que el
        /// paso del Alternate contara como "el paso que prende", y el orden que este archivo cuida
        /// —quién detona lo avisado y dónde está— se volvería incomprobable.
        /// </summary>
        private int IndexOfStep<T>() where T : class =>
            _root.Children.FindIndex(c => Unwrap<T>(c) != null);

        private static T Unwrap<T>(AIDecisionNode child) where T : class
        {
            if (child is T direct) return direct;
            if (child is AINode_Selector sel && sel.Children != null)
                return sel.Children.OfType<T>().FirstOrDefault();
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
