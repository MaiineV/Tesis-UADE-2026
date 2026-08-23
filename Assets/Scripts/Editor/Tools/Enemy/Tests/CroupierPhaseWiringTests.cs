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
    /// <summary>Wiring del árbol del Croupier en memoria: contra el builder y no contra el
    /// <c>.asset</c>, que ataría el suite a que Unity lo haya reimportado.</summary>
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

        /// <summary>El Sequence raíz corta en el primer Failed y el Alternate avanza el índice igual:
        /// un paso suelto que falla cancela el resto del turno <b>y</b> desincroniza el ciclo.</summary>
        [Test]
        public void EveryStepTheRootTicks_IsIsolatedInASelectorWithWaitFallback()
        {
            AssertChildrenAreGuarded(_root.Children, "el Sequence raíz");
        }

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

        /// <summary><c>AllExceptSquareAroundSelf</c> usa el <c>Size</c> como radio del <b>hueco</b> que
        /// se salva, no del área amenazada: leerlo al revés hace exactamente lo contrario.</summary>
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

            // Esta marca SÍ cobra y la banda no: salirse de la banda es un paso al costado,
            // salirse de esto es cruzar media sala hasta el hueco.
            Assert.AreEqual(CroupierAssetBuilder.PlenoIgnitionDamage, mark.Damage,
                "El Pleno dejó de cobrar al prender: quien estaba parado adentro no se entera hasta " +
                "su próximo turno.");
        }

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

        [Test]
        public void PlenoTeleport_SpendsTheTurnsMovement_SoHeStaysInHisOwnHole()
        {
            var gate = FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold);
            var teleport = Descendants(gate.Then).OfType<AINode_TeleportToRoomCenter>().Single();

            Assert.IsTrue(teleport.ConsumeMoveAction,
                "Reubicarse ES el movimiento del turno: sin gastarlo, cualquier paso de movimiento " +
                "que quede detrás lo saca del centro justo después de plantarlo ahí, y el área ya " +
                "quedó anclada donde estaba.");
        }

        /// <summary>El turno de separación entre marcar y prender lo da el <b>orden de los hijos de la
        /// raíz</b>, no un contador: el paso que prende está arriba del que marca.</summary>
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

        /// <summary><c>IThreatenedAreaService</c> guarda un área por fuente <b>sobrescribiendo</b>, y
        /// los dos avisos se levantan el mismo turno: sin canal el segundo destruye al primero.</summary>
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

        /// <summary>El armado corre en el mismo turno que el Alternate, así que el 50% puede cruzarse
        /// sobre un tiempo de reparto y dejar dos áreas marcadas a la vez.</summary>
        [Test]
        public void ThePleno_DropsTheCyclesPendingMark_BeforeRaisingItsOwn()
        {
            var arm = Descendants(FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold).Then);

            var cancel = arm.OfType<AINode_CancelTelegraph>().SingleOrDefault();
            Assert.IsNotNull(cancel,
                "El armado del Pleno no descarta nada: la banda que T1 marcó este mismo turno queda " +
                "pendiente, así que el jugador ve dos avisos y al turno siguiente detonan los dos.");

            Assert.IsTrue(string.IsNullOrEmpty(cancel.ChannelId),
                "El descarte estrenó canal. La banda del ciclo se marca en el guid pelado del jefe: " +
                "con un canal acá no descarta nada, y si el canal es el del Pleno se cancela a sí " +
                "mismo y el 50% no prende jamás.");

            int cancelIdx = arm.FindIndex(n => n is AINode_CancelTelegraph);
            int markIdx = arm.FindIndex(n => n is AINode_TelegraphMark);
            int teleportIdx = arm.FindIndex(n => n is AINode_TeleportToRoomCenter);

            Assert.Less(cancelIdx, markIdx,
                "El descarte quedó DESPUÉS del marcado. Hoy funciona porque los canales difieren, " +
                "pero deja el orden load-bearing: un canal repetido por error se apagaría a sí mismo.");
            Assert.Greater(cancelIdx, teleportIdx,
                "El descarte quedó ANTES del teleport, que es el único paso de acá que falla de " +
                "verdad. Con el teleport fallado el Pleno no se arma, así que la banda tiene que " +
                "conservar su aviso en vez de desaparecer sin nada que la reemplace.");
        }

        /// <summary>Los pasos de adentro van <b>desnudos</b> a propósito, al revés del resto del árbol:
        /// el bloque entero ya está envuelto en <c>Selector[If, Wait]</c>.</summary>
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

        /// <summary>El candado es "permanente" por re-emisión, no por latch: <c>AINode_RotateBlock</c>
        /// hace <c>dice.Clear()</c> por tick y <c>DiceBlockService</c> se limpia al cerrar el turno.</summary>
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

        /// <summary>Dispara antes de saltar: al revés el jugador vería el fogonazo salir de donde el
        /// jefe ya no está.</summary>
        [Test]
        public void DealBeat_ShootsBeforeFleeing_AndMarksTheBandFromWhereItLands()
        {
            var order = Descendants(DealBeat());

            int shot = order.FindIndex(n => n is AINode_RangedShot);
            int flee = order.FindIndex(n => n is AINode_TeleportAwayToEdge);
            int mark = order.FindIndex(n => n is AINode_TelegraphMark);

            Assert.Greater(shot, -1, "El tiempo de reparto no dispara.");
            Assert.Greater(flee, shot, "El salto va después del disparo.");
            Assert.Greater(mark, flee,
                "La banda está anclada en el jefe: marcarla antes de saltar la dejaría apuntando " +
                "desde la casilla vieja y el fuego no caería donde se anunció.");
        }

        [Test]
        public void DealBeat_ShotOutrangesHisOwnFlight()
        {
            var shot = Descendants(_root).OfType<AINode_RangedShot>().Single();
            var flight = Descendants(DealBeat()).OfType<AINode_TeleportAwayToEdge>().Single();

            Assert.AreEqual(CroupierAssetBuilder.ShotDamage, shot.Damage);
            Assert.AreEqual(CroupierAssetBuilder.ShotRange, shot.Range);
            Assert.AreEqual(DistanceMetric.Manhattan, shot.Metric,
                "Misma métrica que la distancia al jugador del salto: con otra, la distancia a la " +
                "que aterriza y la que alcanza el tiro dejan de ser el mismo número.");
            Assert.AreEqual(0, flight.MaxDistanceFromPlayer,
                "El salto de reparto estrenó tope de aterrizaje. Ninguno de los dos saltos de " +
                "fuga lo lleva: con el gate de cercanía, la ventana en la que se le puede entrar " +
                "la abre el jugador acercándose, no un techo a dónde cae el salto.");

            Assert.GreaterOrEqual(shot.Range, flight.MinPlayerDistance,
                "El salto se lleva al jefe hasta MinPlayerDistance del jugador, y sin tope puede " +
                "aterrizar aún más lejos: con un alcance menor se sale solo de rango y el tiempo " +
                "de reparto no hace nada.");
        }

        [Test]
        public void DealBeat_MarksTheAuthoredConeFromTheSheet()
        {
            var mark = Descendants(DealBeat()).OfType<AINode_TelegraphMark>().Single();

            Assert.AreEqual(ThreatShape.DirectionalCone, mark.Shape,
                "El reparto dejó de marcar un cono. La banda uniforme cobraba igual pegado al " +
                "jefe que en el fondo; el cono deja su casilla como refugio.");
            Assert.AreEqual(CroupierAssetBuilder.ConeApexHalfWidth, mark.Size,
                "Size es el semi-ancho del APEX, y sale de la ficha.");
            Assert.AreEqual(CroupierAssetBuilder.ConeDepth, mark.Depth,
                "La profundidad del nodo dejó de ser la de la ficha. Es cuánto paño quema cada " +
                "turno de reparto: más corto y el cono no llega a cruzarse en el camino del " +
                "jugador, más largo y barre la sala de punta a punta desde el borde en el que el " +
                "jefe aterrizó.");
            Assert.AreEqual(0, mark.Damage,
                "La banda no puede cobrar al prender: se marca un turno antes, así que el jugador " +
                "tuvo su turno para salirse y quedarse adentro ya es una decisión suya. El daño lo " +
                "cobran las casillas que planta.");
        }

        [Test]
        public void BurnBeat_IgnitesThenTeleports_AndDoesNotShoot()
        {
            var burn = Descendants(BurnBeat());

            int ignite = burn.FindIndex(n => n is AINode_IgniteArea);
            int flight = burn.FindIndex(n => n is AINode_TeleportAwayToEdge);

            Assert.Greater(ignite, -1, "El tiempo de quema no prende nada.");
            Assert.Greater(flight, ignite,
                "El salto se adelantó a la ignición. El fuego cae en las casillas guardadas el turno " +
                "anterior, así que el área no cambia — pero el jefe se va antes de prender lo suyo.");
            Assert.IsEmpty(burn.OfType<AINode_RangedShot>(),
                "Un disparo en el turno de quema le suma un golpe al único turno que lo deja al " +
                "alcance del jugador.");

            var jump = burn.OfType<AINode_TeleportAwayToEdge>().Single();
            Assert.AreEqual(0, jump.MaxDistanceFromPlayer,
                "El salto del tiempo de quema volvió a tener tope de aterrizaje. Con el gate de " +
                "cercanía activo, un techo acá ya no hace ganable la pelea: sólo hace que el jefe " +
                "aterrice más cerca de lo que la fuga tendría que dejarlo.");
            Assert.IsTrue(jump.ConsumeMoveAction,
                "Sin gastar el movimiento del turno, cualquier paso de reacomodo posterior lo saca " +
                "del borde en el mismo turno en que saltó.");
        }

        /// <summary>Sólo huye si el jugador está cerca: de lejos el disparo no tiene techo y el cono se
        /// marca desde donde esté parado, así que tepearse no le compra nada.</summary>
        [Test]
        public void BothFlights_AreGatedByProximity_UsingTheSheetThreshold()
        {
            foreach (var gate in new[] { FleeGateOf(DealBeat()), FleeGateOf(BurnBeat()) })
            {
                var proximity = gate.Conditions.OfType<PcTargetInRange>().SingleOrDefault();

                Assert.IsNotNull(proximity,
                    "El salto de fuga dejó de estar gateado por distancia al jugador.");
                Assert.AreEqual(CroupierAssetBuilder.FleeTriggerRange, proximity.Range,
                    "El umbral del gate no sale de la constante de la ficha: quedó un número " +
                    "suelto que puede desincronizarse de lo que documenta el builder.");
                Assert.AreEqual(DistanceMetric.Manhattan, proximity.Metric,
                    "El umbral se decidió en Manhattan; otra métrica cambia a qué distancia real " +
                    "el jefe deja de huir.");
            }
        }

        /// <summary>Un <c>If</c> sin <c>Else</c> devuelve <c>Failed</c> cuando la condición no pasa.</summary>
        [Test]
        public void BothFlights_GateHasAWaitElse_SoBeingFarNeverAbortsTheBeat()
        {
            Assert.IsInstanceOf<AINode_Wait>(FleeGateOf(DealBeat()).Else,
                "El gate del salto de reparto no tiene Wait de Else: con el jugador lejos, corta " +
                "el tiempo entero y el jefe ni dispara.");
            Assert.IsInstanceOf<AINode_Wait>(FleeGateOf(BurnBeat()).Else,
                "El gate del salto de quema no tiene Wait de Else: con el jugador lejos, corta el " +
                "tiempo entero y el jefe ni prende lo marcado.");
        }

        /// <summary>Sin tope a propósito: con el gate de cercanía la pelea es ganable porque el jugador
        /// maneja el tempo de acercarse, no porque el salto tenga un techo a dónde cae.</summary>
        [Test]
        public void NeitherFlight_HasALandingCap()
        {
            var dealFlight = Descendants(DealBeat()).OfType<AINode_TeleportAwayToEdge>().Single();
            var burnFlight = Descendants(BurnBeat()).OfType<AINode_TeleportAwayToEdge>().Single();

            Assert.AreEqual(0, dealFlight.MaxDistanceFromPlayer,
                "El salto de reparto volvió a tener tope de aterrizaje.");
            Assert.AreEqual(0, burnFlight.MaxDistanceFromPlayer,
                "El salto de quema volvió a tener tope de aterrizaje.");
        }

        /// <summary>El sorteo de la fuga también tiene un <c>AINode_TeleportToRoomCenter</c>, así que el
        /// tipo de nodo no alcanza para distinguirlos: hay que garantizar instancias distintas.</summary>
        [Test]
        public void PlenoTeleport_IsNeverGated_AndIsADifferentNodeFromTheFleeTeleports()
        {
            var arm = Descendants(FindGateAtPercent(CroupierAssetBuilder.PlenoHpThreshold).Then);

            var plenoTeleport = arm.OfType<AINode_TeleportToRoomCenter>().SingleOrDefault();
            Assert.IsNotNull(plenoTeleport, "El Pleno dejó de plantarse en el centro.");

            Assert.IsEmpty(arm.OfType<AINode_TeleportAwayToEdge>(),
                "El armado del Pleno pasó a compartir nodo con los saltos de fuga: quedaría " +
                "gateado por cercanía y a veces no se plantaría en el centro al cruzar el 50%.");

            foreach (var beat in new[] { DealBeat(), BurnBeat() })
            {
                foreach (var fromRoulette in Descendants(FleeRouletteOf(beat))
                             .OfType<AINode_TeleportToRoomCenter>())
                {
                    Assert.AreNotSame(plenoTeleport, fromRoulette,
                        "El plantado del Pleno y el aterrizaje al centro del sorteo son la MISMA " +
                        "instancia: el del Pleno quedó colgado del gate de cercanía y deja de " +
                        "plantarse al cruzar el 50% si el jugador está lejos.");
                }
            }
        }

        /// <summary>El orden de las opciones es contrato: <c>AINode_Random</c> acumula pesos y devuelve
        /// la primera que pasa el corte, así que reordenarlas cambia qué sale con cada tirada.</summary>
        [Test]
        public void BothBeats_RollTheAuthoredFleeOdds_InTheAuthoredOrder()
        {
            foreach (var beat in new[] { DealBeat(), BurnBeat() })
            {
                var options = FleeRouletteOf(beat).Options;

                Assert.AreEqual(3, options.Count,
                    "El sorteo de la fuga dejó de tener tres salidas.");

                Assert.AreEqual(CroupierAssetBuilder.FleeWeightEdge, options[0].Weight,
                    "El peso de irse al borde no sale de la constante de la ficha.");
                Assert.AreEqual(CroupierAssetBuilder.FleeWeightCenter, options[1].Weight,
                    "El peso de saltar al centro no sale de la constante de la ficha.");
                Assert.AreEqual(CroupierAssetBuilder.FleeWeightStay, options[2].Weight,
                    "El peso de quedarse no sale de la constante de la ficha.");

                Assert.IsInstanceOf<AINode_TeleportAwayToEdge>(options[0].Node,
                    "La primera salida del sorteo dejó de ser el salto al borde.");
                Assert.IsInstanceOf<AINode_TeleportToRoomCenter>(options[1].Node,
                    "La segunda salida del sorteo dejó de ser el salto al centro.");
                Assert.IsInstanceOf<AINode_Wait>(options[2].Node,
                    "La tercera salida del sorteo tiene que ser un Wait explícito: un Node null " +
                    "devuelve Failed y se comería el resto del tiempo del jefe.");
            }
        }

        /// <summary>Sin gastar el movimiento del turno, cualquier paso de reacomodo posterior lo saca
        /// del centro en el mismo turno en que se plantó ahí.</summary>
        [Test]
        public void TheCentreLanding_ConsumesTheTurnMovement()
        {
            foreach (var beat in new[] { DealBeat(), BurnBeat() })
            {
                var landing = Descendants(FleeRouletteOf(beat))
                    .OfType<AINode_TeleportToRoomCenter>().Single();

                Assert.IsTrue(landing.ConsumeMoveAction,
                    "El aterrizaje al centro dejó de gastar el movimiento del turno.");
            }
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
                // Un 0 cae al default del SO, y en ISpecialTileService.Place 0 significa PERMANENTE.
                Assert.Contains(ignite.DurationRounds,
                    new[]
                    {
                        CroupierAssetBuilder.FireDurationRounds,
                        CroupierAssetBuilder.FireDurationRoundsPhase2,
                        CroupierAssetBuilder.PlenoFireDurationRounds,
                    },
                    $"Una ignición pasa {ignite.DurationRounds} rondas, que no es la duración base, " +
                    "ni la de fase 2, ni la del Pleno: o es un número suelto que nadie va a " +
                    "mantener, o es un 0 que deja el fuego encendido para siempre.");
            }
        }

        /// <summary>Nadie apaga las bandas anteriores: con la duración base igual al intervalo nunca
        /// conviven dos, y por encima del intervalo se apilan hasta que no queda piso.</summary>
        [Test]
        public void FireDuration_MatchesTheIgnitionInterval_AndOnlyPhaseTwoOverlaps()
        {
            var alternate = Alternate();
            int burningBeats = alternate.Children.Count(c => Descendants(c).Any(n => n is AINode_IgniteArea));

            Assert.AreEqual(1, burningBeats,
                "Prende en uno de los tiempos y sólo uno: el intervalo entre igniciones sale de ahí.");

            int ignitionIntervalRounds = alternate.Children.Count;

            // "Arde N rondas" se autora como N + 1 (la ronda en que nace no le deja al jugador ningún
            // arranque de turno por delante), así que contra el intervalo va la duración menos uno.
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

            Assert.AreEqual(1, CroupierAssetBuilder.PlenoFireDurationRounds - 1,
                "El Pleno arde una sola ronda. Prende el paño entero menos el 3x3 del jefe: dos " +
                "rondas y el jugador pasa un turno completo sin ninguna casilla a la que moverse.");
        }

        /// <summary>Va cableado y no por default: el default de <c>RetireFullyReplaced</c> es "no
        /// retirar", y apagar fuego que el jugador ya tiene en pantalla es decisión de esta pelea.</summary>
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

        /// <summary>El nodo no lee el HP: la duración la elige un <c>AINode_If</c>, así que colapsar las
        /// dos ramas pierde el escalón de fase 2 sin que falle nada.</summary>
        [Test]
        public void PhaseTwo_IsTheOnlyThingThatLengthensAFire()
        {
            var ignitions = Descendants(_root).OfType<AINode_IgniteArea>().ToList();

            Assert.AreEqual(1, ignitions.Count(i => i.DurationRounds == CroupierAssetBuilder.FireDurationRounds),
                "Tiene que haber exactamente una ignición con la duración base: la de la banda " +
                "mientras el jefe está por encima del 50%.");
            Assert.AreEqual(1, ignitions.Count(i => i.DurationRounds == CroupierAssetBuilder.FireDurationRoundsPhase2),
                "Y exactamente una con la de fase 2: la banda por debajo del 50%.");

            Assert.AreEqual(1, ignitions.Count(i => i.DurationRounds == CroupierAssetBuilder.PlenoFireDurationRounds),
                "El Pleno lleva duración propia y más corta que las dos bandas: prende el paño " +
                "entero salvo el 3x3 del jefe, así que si ardiera como una banda no quedaría dónde " +
                "pararse. Igualarlo a fase 2 lo convierte en terreno y borra la sala.");

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

        /// <summary>Sin <c>IsBoss</c> el jefe no es jefe para <c>SpecialTileService.ShouldAffect</c>, que
        /// exige <c>OwnerBossImmune &amp;&amp; IsBoss</c> y ser el dueño.</summary>
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
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                // Visual y retrato en null: el wiring visual vive en CroupierVisualWiringTests.
                CroupierAssetBuilder.PopulateEnemyData(data, _fire, null, null);

                Assert.AreEqual("boss.croupier", data.EntityId);
                Assert.AreEqual(CroupierAssetBuilder.MaxHp, data.BaseHP,
                    "La vida que autora el builder dejó de ser la que termina en la ficha.");
                // El par de melee espeja el disparo porque el jefe no tiene melee: nadie lo lee en
                // runtime, pero con un número propio el bloque de stats miente sobre cuánto pega.
                Assert.AreEqual(CroupierAssetBuilder.ShotDamage, data.BaseAttack,
                    "El stat de ataque dejó de espejar el disparo, que es lo único con lo que pega.");
                Assert.AreEqual(CroupierAssetBuilder.ShotRange, data.BaseAttackRange,
                    "El alcance del stat dejó de espejar el del disparo: un 1 acá lo hace parecer " +
                    "un jefe de contacto, y no llega nunca a distancia 1.");
                Assert.AreEqual(ComboId.Poker, data.WeaknessComboId,
                    "La debilidad es el Poker (cuatro dados iguales), no el Par. El id canónico del " +
                    "catálogo es combo.poker.");
                // Es un override propio del jefe, no el global de WeaknessConfig, así que moverlo
                // no le toca la debilidad a ningún otro enemigo.
                Assert.AreEqual(CroupierAssetBuilder.WeaknessMultiplier,
                    data.WeaknessMultiplierOverride, PercentTolerance,
                    "El multiplicador que autora el builder dejó de ser el que termina en la ficha.");
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

        /// <summary>El Poker sale ~2 de cada 10 tiradas gastando el pozo entero: de ahí el ×2, que es lo
        /// que hace que valga ir a buscarlo.</summary>
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

        private AINode_Alternate Alternate()
        {
            var alternate = Descendants(_root).OfType<AINode_Alternate>().SingleOrDefault();
            Assert.IsNotNull(alternate, "No hay ciclo de dos tiempos en el árbol.");
            Assert.AreEqual(2, alternate.Children.Count, "El jefe es de DOS tiempos: reparte y quema.");
            return alternate;
        }

        private AIDecisionNode DealBeat()
        {
            var beat = Alternate().Children
                .FirstOrDefault(c => Descendants(c).Any(n => n is AINode_RangedShot));
            Assert.IsNotNull(beat, "Ningún tiempo dispara.");
            return beat;
        }

        private AIDecisionNode BurnBeat()
        {
            var beat = Alternate().Children
                .FirstOrDefault(c => Descendants(c).Any(n => n is AINode_IgniteArea));
            Assert.IsNotNull(beat, "Ningún tiempo prende el paño.");
            return beat;
        }

        /// <summary>Se filtra por el <c>AINode_Random</c> del <c>Then</c> y no por el primer <c>If</c> del
        /// tiempo: el de quema tiene además el <c>If</c> que ramifica la duración del fuego.</summary>
        private static AINode_If FleeGateOf(AIDecisionNode beat)
        {
            var gate = Descendants(beat).OfType<AINode_If>()
                .SingleOrDefault(g => g.Then is AINode_Random);
            Assert.IsNotNull(gate, "No hay gate de cercanía envolviendo el sorteo de la fuga.");
            return gate;
        }

        private static AINode_Random FleeRouletteOf(AIDecisionNode beat) =>
            (AINode_Random)FleeGateOf(beat).Then;

        /// <summary>Todo hijo de un contenedor que la raíz tickea tiene que ser <c>Selector[paso, Wait]</c>.
        /// No entra en los Selectors: lo que cuelga adentro ya está aislado.</summary>
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

        /// <summary>Deliberadamente superficial: en profundidad, la ignición del tiempo de quema haría
        /// que el paso del Alternate contara como "el paso que prende".</summary>
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
        /// (no arrastra assets referenciados).</summary>
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
