using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Corre el árbol REAL del Croupier turno a turno con los servicios reales.
    /// <c>CroupierPhaseWiringTests</c> mira la forma del árbol y eso no demuestra el resultado: acá
    /// se tickea y se cuenta cuántos avisos quedan pendientes y cuántas detonaciones caen.</summary>
    [TestFixture]
    public class CroupierTurnCycleTests
    {
        private const int RoomSide = 11;

        /// <summary>Centro de <c>NavGraph.Rect(11,11)</c>, que es 0-based: (5,5). Sale del lado de la
        /// sala del fixture porque el nodo resuelve el centro del bounding box de lo que haya cargado.</summary>
        private static readonly GridCoord RoomCentre = new GridCoord(RoomSide / 2, RoomSide / 2);

        private static readonly GridCoord BossTile = new GridCoord(8, 5);

        /// <summary>Manhattan 5 del jefe, justo el <see cref="CroupierAssetBuilder.FleeTriggerRange"/>:
        /// el gate de cercanía pasa (es inclusivo) y T1 corre entero. Es además la única X de la fila
        /// que cae fuera del hueco 3×3 del Pleno, así que la ignición del paño le sigue cobrando.</summary>
        private static readonly GridCoord PlayerTile = new GridCoord(3, 5);

        /// <summary>HP con el que los dos gates (70% y 50%) evalúan true.</summary>
        private const int PhaseTwoHp = 80;

        private GridManager _grid;
        private MovementService _movement;
        private ThreatenedAreaService _threat;
        private SpecialTileService _tiles;
        private DiceBlockService _blocks;
        private SpyThreatOverlay _overlay;
        private SpyDamagePipeline _pipeline;
        private AttributesManager _attributes;
        private ModifiableAttributes _bossStats;
        private StubPlayerService _playerService;

        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private SpecialTileDefinitionSO _fire;
        private DiceBagSO _bag;

        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSide, RoomSide));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            // El servicio real y no un stub: el teleport al centro y la fuga son los dos pasos que
            // pueden fallar en este bloque, y un stub que nunca falla escondería justo eso.
            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline, ServiceScope.Global);

            // Registrado ANTES del primer marcado: ThreatTelegraphOverlay.ResolveOrCreate devuelve lo
            // que ya esté en el locator, así que el spy evita parar GameObjects en EditMode.
            _overlay = new SpyThreatOverlay();
            ServiceLocator.AddService<IThreatOverlayService>(_overlay, ServiceScope.Global);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);

            _fire = Create<SpecialTileDefinitionSO>();
            _fire.TileId = "TILE_TEST_CROUPIER_FIRE";
            _fire.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            _fire.DefaultDurationRounds = CroupierAssetBuilder.FireDurationRounds;

            // El candado del 70% pide bolsa y servicio de dados. Sin ellos el paso loguea y falla:
            // el Selector se lo come, pero el ruido tapa los avisos que este fixture sí mira.
            _bag = Create<DiceBagSO>();
            _bag.Dice = new List<DiceType>
            {
                DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12,
            };
            _playerService = new StubPlayerService { Guid = _player, Bag = _bag };
            ServiceLocator.AddService<IPlayerService>(_playerService, ServiceScope.Global);

            _blocks = new DiceBlockService();
            _blocks.Register();

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes);
            _bossStats = new ModifiableAttributes();
            _bossStats.EnsureInitialized();
            _attributes.Register(_boss, _bossStats);
            SetBossHp(CroupierAssetBuilder.MaxHp);

            _grid.Register(_boss, BossTile);
            _grid.Register(_player, PlayerTile);
        }

        [TearDown]
        public void TearDown()
        {
            _blocks?.Dispose();
            _tiles?.Dispose();
            _threat?.Dispose();
            _attributes?.Dispose();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            foreach (var asset in _created) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();
        }

        /// <summary>El tiempo de reparto ya no marca nada, así que cruzar acá no le pisa ningún
        /// aviso: el descarte del armado pasa de largo y tiene que salir por <c>Succeeded</c>.</summary>
        // El orden del ciclo, declarado una vez: rotarlo en el builder se arregla acá y no en cada
        // test. Son numeros de turno, no indices: el primer Tick es el turno 1.
        private const int BombTurn = 1;
        private const int BurnTurn = 2;
        private const int DealTurn = 3;

        /// <summary>Corre el ciclo hasta el turno pedido inclusive y devuelve el resultado de ese
        /// ultimo tick, que es el turno que el test esta mirando.</summary>
        private AIResult TickThrough(AINode_Sequence root, int throughTurn, double roll = RollEdge)
        {
            var result = AIResult.Succeeded;
            for (int turn = 1; turn <= throughTurn; turn++)
                result = root.Tick(NewContext(roundIndex: turn, roll: roll));
            return result;
        }

        /// <summary>El tiempo de reparto no marca nada, así que cruzar ahí no le pisa ningún aviso:
        /// el descarte del armado pasa de largo y tiene que salir por <c>Succeeded</c>.</summary>
        [Test]
        public void CrossingFiftyOnTheDealBeat_LeavesOnlyThePlenoQueued()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            // Los dos primeros turnos con la vida entera: el cono que marca el tiempo de las bombas
            // arde en el de quema, así que al llegar al reparto no queda nada pendiente.
            TickThrough(root, BurnTurn);
            SetBossHp(PhaseTwoHp);

            var result = root.Tick(NewContext(roundIndex: DealTurn));

            Assert.AreNotEqual(AIResult.Failed, result,
                "El turno del jefe se cortó entero. El descarte no encontró nada pendiente, y ese " +
                "no-op tiene que salir por Succeeded o se lleva el marcado que sigue.");
            Assert.IsTrue(_threat.HasPending(PlenoSource),
                "El Pleno no quedó encolado: el descarte se llevó puesto el aviso que tenía que " +
                "encolar, o el marcado nunca corrió.");
            Assert.IsFalse(_threat.HasPending(_boss),
                "Apareció un cono pendiente en el tiempo de reparto, que no marca nada.");
        }

        /// <summary>El único turno en el que el armado pisa un aviso propio: el cono se marca al final
        /// del tiempo de las bombas y el armado corre detrás del ciclo, en el mismo tick.</summary>
        [Test]
        public void CrossingFiftyOnTheBombBeat_DiscardsTheConeItJustMarked()
        {
            SetBossHp(PhaseTwoHp);
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            var result = root.Tick(NewContext(roundIndex: BombTurn));

            Assert.AreNotEqual(AIResult.Failed, result, "El turno del cruce se cortó entero.");
            Assert.IsTrue(_threat.HasPending(PlenoSource), "El Pleno no quedó encolado.");
            Assert.IsFalse(_threat.HasPending(_boss),
                "El cono siguió pendiente junto con el Pleno. Son los DOS avisos prendidos a la vez " +
                "que reportó el jugador, y al turno siguiente detonan los dos.");
            CollectionAssert.Contains(_overlay.Cleared, _boss,
                "Se descartó el área del cono pero quedó su dibujo: un aviso pintado que no va a " +
                "detonar nunca, que se lee igual de mal que los dos avisos.");
        }

        /// <summary><c>AINode_IgniteArea</c> sin marca pendiente sale por <c>Succeeded</c>, no por
        /// <c>Failed</c>: el tiempo de quema se quedó sin banda y no puede cortar el turno.</summary>
        [Test]
        public void TheTurnAfterTheCrossing_DetonatesThePlenoAndNothingElse()
        {
            SetBossHp(PhaseTwoHp);
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);
            root.Tick(NewContext(roundIndex: 1));
            _pipeline.Resolved.Clear();

            var result = root.Tick(NewContext(roundIndex: 2));

            Assert.AreNotEqual(AIResult.Failed, result,
                "El turno de la detonación se cortó. El tiempo de quema quedó sin banda que " +
                "consumir, y ese no-op tiene que salir por Succeeded.");
            Assert.AreEqual(1, FireInstances().Count,
                "Cayeron dos igniciones (o ninguna): el paño del Pleno es la única que este turno " +
                "tenía algo pendiente que prender.");
            Assert.AreEqual(1, EnvironmentalHits().Count,
                "El daño de ignición se cobró dos veces (o ninguna): sólo el Pleno trae Damage, la " +
                "banda va en 0.");
            Assert.AreEqual(CroupierAssetBuilder.PlenoIgnitionDamage, EnvironmentalHits()[0].BaseDamage,
                "El golpe que se cobró no es el del Pleno.");
            Assert.IsFalse(_threat.HasPending(PlenoSource), "El paño quedó pendiente sin prender.");

            // Nada pendiente: el cono que las bombas habían marcado se lo llevó el descarte del
            // armado en el turno anterior, y el de quema no marca.
            Assert.IsFalse(_threat.HasPending(_boss), "Apareció un cono pendiente de la nada.");
        }

        /// <summary>El descarte va desnudo dentro del Sequence del armado: si devolviera <c>Failed</c> al
        /// no encontrar nada, cortaría el turno antes del marcado y dejaría a <c>Once</c> sin latchear.</summary>
        [Test]
        public void CrossingFiftyOnTheBurnBeat_BurnsTheBandAndStillQueuesThePleno()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            // Turno 1 con la vida entera: las bombas siembran y marcan el cono.
            root.Tick(NewContext(roundIndex: BombTurn));
            Assert.IsTrue(_threat.HasPending(_boss),
                "Precondición: el tiempo de las bombas tenía que dejar el cono marcado.");

            SetBossHp(PhaseTwoHp);
            var result = root.Tick(NewContext(roundIndex: BurnTurn));

            Assert.AreNotEqual(AIResult.Failed, result, "El turno del cruce se cortó entero.");
            Assert.AreEqual(1, FireInstances().Count,
                "La banda avisada el turno anterior no ardió: el cruce del 50% no le saca su turno " +
                "de quema al ciclo, sólo reemplaza lo que quedaba avisado.");
            Assert.IsTrue(_threat.HasPending(PlenoSource),
                "El Pleno no quedó encolado. Sin banda pendiente el descarte no tiene nada que " +
                "tirar, y si eso devolviera Failed el marcado que sigue no correría.");
            Assert.IsFalse(_threat.HasPending(_boss),
                "Quedó una banda pendiente en el turno de quema, que es el que las consume.");
        }

        /// <summary>El descarte va detrás del único paso del bloque que puede fallar de verdad: arriba
        /// del teleport, un armado abortado deja el turno sin ninguna amenaza dibujada.</summary>
        [Test]
        public void WhenTheTeleportFails_TheConeKeepsItsWarningAndThePlenoRetriesLater()
        {
            SetBossHp(PhaseTwoHp);
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            // Turno 1, el de las bombas: acá se marca el cono, y el armado falla detrás. Sin
            // servicio de movimiento el teleport al centro falla avisando; el salto del ciclo también
            // se cae, en silencio y aislado en su Selector, así que el tiempo sigue igual.
            LogAssert.Expect(LogType.Warning, new Regex("AINode_TeleportToRoomCenter"));
            root.Tick(NewContextWithoutMovement(roundIndex: BombTurn));

            Assert.IsTrue(_threat.HasPending(_boss),
                "El armado abortó a mitad y se llevó igual el aviso del cono: el jugador cierra " +
                "el turno sin ninguna amenaza dibujada y el jefe sin nada que prender.");
            Assert.IsFalse(_threat.HasPending(PlenoSource), "El Pleno se armó con el teleport fallado.");
            CollectionAssert.DoesNotContain(_overlay.Cleared, _boss,
                "Se apagó el dibujo del cono que sí sigue pendiente: detona a ciegas.");

            // Y el umbral no se perdió: AINode_Once no latchea con Failed.
            var result = root.Tick(NewContext(roundIndex: BurnTurn));

            Assert.AreNotEqual(AIResult.Failed, result, "El reintento cortó el turno.");
            Assert.IsTrue(_threat.HasPending(PlenoSource),
                "El armado no se reintentó: el umbral del 50% se perdió para toda la pelea.");

            // El reintento cayó en el turno de quema, que consume el cono antes de que el armado
            // descarte: el aviso que sobrevivió al fallo alcanza a arder.
            Assert.AreEqual(1, FireInstances().Count,
                "El cono que se salvó del armado fallado no ardió en su turno de quema.");
            Assert.IsFalse(_threat.HasPending(_boss),
                "El armado dejó el cono pendiente además del Pleno: el jugador ve dos áreas " +
                "dibujadas y al turno siguiente detonan las dos.");
        }

        /// <summary>Lo que sostiene que la pelea sea ganable no es un techo a dónde cae el salto, sino
        /// que el sorteo a veces no diga borde: sin tope, el salto siempre lo deja fuera de alcance.</summary>
        [Test]
        public void BothBeats_GateTheirJumpOnProximity_AndNeitherIsCapped()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);
            var beats = Alternate(root).Children;

            foreach (var beat in beats)
            {
                Assert.AreEqual(1, BeatSteps(beat).OfType<AINode_If>()
                        .Count(g => g.Then is AINode_Random),
                    "Un tiempo del ciclo dejó de tener exactamente un sorteo gateado: o el jefe se " +
                    "queda quieto ese turno, o sortea dos veces y el segundo pisa al primero.");

                Assert.AreEqual(0, FleeJumpOf(beat).MaxDistanceFromPlayer,
                    "El salto volvió a tener tope de aterrizaje. Con el gate de cercanía decidiendo " +
                    "si corre, el tope sólo lo hace aterrizar al lado del jugador y huir no compra nada.");

                var gate = FleeGateOf(beat);
                Assert.IsInstanceOf<AINode_Wait>(gate.Else,
                    "El gate perdió su Else: un If sin Else devuelve Failed con el jugador lejos, y " +
                    "ese Failed le corta al jefe el resto del tiempo y desincroniza el ciclo.");

                var proximity = gate.Conditions.OfType<PcTargetInRange>().Single();
                Assert.AreEqual(CroupierAssetBuilder.FleeTriggerRange, proximity.Range,
                    "El radio de fuga dejó de salir de la ficha.");
                Assert.AreEqual(DistanceMetric.Manhattan, proximity.Metric,
                    "El radio se mide en Manhattan, igual que el alcance de los ataques.");
            }

            // El jugador se vuelve a acercar entre tiempos porque el salto de T1 lo deja fuera del
            // radio: sin eso T2 se quedaría quieto y estaríamos midiendo el gate en vez del salto.
            var start = PositionOf(_boss);
            root.Tick(NewContext(roundIndex: 1));
            var afterDeal = PositionOf(_boss);
            Assert.AreNotEqual(start, afterDeal, "El tiempo de reparto dejó al jefe donde estaba.");

            MovePlayerNextTo(afterDeal);
            root.Tick(NewContext(roundIndex: 2));
            Assert.AreNotEqual(afterDeal, PositionOf(_boss),
                "El tiempo de quema dejó al jefe donde estaba.");
        }

        [Test]
        public void WithThePlayerFarAway_TheBossHoldsHisGround_AndStillShoots()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);
            MovePlayerBeyondFleeRadius();
            var start = PositionOf(_boss);

            TickThrough(root, DealTurn);

            Assert.AreEqual(start, PositionOf(_boss),
                "El jefe saltó con el jugador fuera del radio: el turno se le va en huir de nadie y " +
                "a veces aterriza más cerca de lo que estaba.");
            Assert.Greater(ShotCount(), 0,
                "Quedarse quieto le comió el resto del tiempo. El disparo va antes del salto y tiene " +
                "que cobrar igual — si no, el Else del gate está devolviendo Failed.");
        }

        /// <summary>Un <c>Failed</c> abortaría el <c>Sequence</c> del tiempo mientras el <c>Alternate</c>
        /// avanza el índice igual, y el jefe pasaría a repartir dos veces seguidas.</summary>
        [Test]
        public void WithThePlayerFarAway_TheCycleKeepsAlternating()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);
            MovePlayerBeyondFleeRadius();

            TickThrough(root, DealTurn);
            int afterDeal = ShotCount();
            Assert.Greater(afterDeal, 0, "El tiempo de reparto no disparó.");

            // El turno siguiente vuelve a abrir el ciclo por las bombas: si disparara, el Alternate
            // se desincronizó y el jefe reparte dos veces seguidas.
            root.Tick(NewContext(roundIndex: DealTurn + 1));

            Assert.AreEqual(afterDeal, ShotCount(),
                "El tiempo de las bombas disparó: el ciclo se desincronizó y el jefe repartió dos " +
                "veces seguidas en vez de sembrar.");
        }

        /// <summary>Comparación de constantes y no un tick a propósito: el alcance del kit del jugador no
        /// vive en el repo como una constante legible, así que lo que se pinea es la relación.</summary>
        [Test]
        public void TheFleeRadius_StaysBelowWhatThePlayerCanReachInATurn()
        {
            // 4 de movimiento (BFS) + 4 de alcance del ataque especial (Manhattan), una vez cada
            // uno y sin gap-closer.
            const int playerReachPerTurn = 8;

            Assert.Less(CroupierAssetBuilder.FleeTriggerRange, playerReachPerTurn,
                $"El jefe huye en cuanto el jugador entra a {CroupierAssetBuilder.FleeTriggerRange} " +
                $"casillas, y el salto ya no tiene tope de aterrizaje. Con un radio de " +
                $"{playerReachPerTurn} o más se va antes de que el jugador pueda golpearlo desde " +
                "afuera del radio, y la vida del jefe pasa a ser infinita.");
        }

        /// <summary><c>AINode_IgniteArea</c> consume la marca en vez de recalcular la forma: la banda
        /// está anclada en el jefe, así que recalcularla la traería desde la casilla nueva.</summary>
        [Test]
        public void TheFireLandsWhereItWasAnnounced_EvenAfterTheJump()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            root.Tick(NewContext(roundIndex: BombTurn));
            var announced = new HashSet<GridCoord>(_threat.GetPendingTiles(_boss));
            Assert.IsNotEmpty(announced,
                "Precondición: el tiempo de las bombas tenía que marcar el cono.");

            root.Tick(NewContext(roundIndex: BurnTurn));

            var burning = new HashSet<GridCoord>(FireInstances().SelectMany(i => i.Coords));
            CollectionAssert.AreEquivalent(announced, burning,
                "El fuego no cubre el cono que se avisó. Entre el aviso y la ignición el jefe " +
                "saltó otra vez: si el área se recalcula, cae desde donde está ahora y el jugador " +
                "se quema en una casilla que nunca vio marcada.");
        }

        [Test]
        public void WhenTheRouletteSaysCentre_TheBossLandsInTheMiddleOfTheRoom()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            root.Tick(NewContext(roundIndex: 1, roll: RollCentre));

            Assert.AreEqual(RoomCentre, PositionOf(_boss),
                "El sorteo dio centro y el jefe no terminó en el centro: la salida que lo deja al " +
                "alcance del jugador no está moviéndolo.");
        }

        /// <summary>Es el caso que protege el <c>AINode_Wait</c> de la tercera opción: con un <c>Node</c>
        /// nulo el sorteo devolvería <c>Failed</c> y se comería el marcado del cono que sigue.</summary>
        [Test]
        public void WhenTheRouletteSaysStay_TheBossHoldsHisGround_AndStillWorksTheBeat()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);
            var start = PositionOf(_boss);

            // El marcado del cono vive detrás del sorteo del tiempo de las bombas, que es el primero.
            root.Tick(NewContext(roundIndex: BombTurn, roll: RollStay));

            Assert.AreEqual(start, PositionOf(_boss),
                "El sorteo dio quedarse y el jefe se movió igual.");
            Assert.IsNotEmpty(_threat.GetPendingTiles(_boss),
                "Quedarse le comió el marcado del cono: la tercera opción del sorteo está " +
                "devolviendo Failed y abortando el resto del tiempo.");

            // Y el disparo vive detrás del sorteo del tiempo de reparto, que es el último.
            TickThrough(root, DealTurn, RollStay);

            Assert.Greater(ShotCount(), 0,
                "Quedarse le comió el disparo del tiempo de reparto.");
        }

        /// <summary>El marcado va detrás del sorteo por esto: marcando primero, el cono saldría de la
        /// casilla vieja y el aviso no correspondería a dónde está el jefe.</summary>
        [Test]
        public void TheConeIsAnchoredWhereTheRouletteLeftHim()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            root.Tick(NewContext(roundIndex: BombTurn, roll: RollCentre));

            var announced = _threat.GetPendingTiles(_boss).ToList();
            Assert.IsNotEmpty(announced,
                "Precondición: el tiempo de las bombas tenía que marcar el cono.");

            int nearest = announced.Min(t =>
                Math.Abs(t.X - RoomCentre.X) + Math.Abs(t.Y - RoomCentre.Y));
            Assert.AreEqual(1, nearest,
                "El cono no arranca pegado al centro, así que quedó anclado en la casilla vieja: " +
                "el jefe aterrizó en el medio y el aviso salió de donde ya no está.");
        }

        /// <summary>El armado del Pleno es el último paso del turno por esto: cualquier salto colgado
        /// detrás lo arranca del centro donde plantó el hueco, y el ataque se lee al revés.</summary>
        [TestCase(false, TestName = "CrossingFifty_EndsTheTurnWithTheBossInTheCentre_OnTheDealBeat")]
        [TestCase(true, TestName = "CrossingFifty_EndsTheTurnWithTheBossInTheCentre_OnTheBurnBeat")]
        public void CrossingFifty_EndsTheTurnWithTheBossInTheCentre(bool onTheBurnBeat)
        {
            TickTheCrossingTurn(onTheBurnBeat);

            Assert.AreEqual(RoomCentre, PositionOf(_boss),
                "El turno del cruce no terminó con el jefe en el centro de la sala.");
        }

        [TestCase(false, TestName = "ThePlenosHole_SurroundsWhereTheBossEnded_OnTheDealBeat")]
        [TestCase(true, TestName = "ThePlenosHole_SurroundsWhereTheBossEnded_OnTheBurnBeat")]
        public void ThePlenosHole_SurroundsWhereTheBossEnded(bool onTheBurnBeat)
        {
            TickTheCrossingTurn(onTheBurnBeat);

            var threatened = new HashSet<GridCoord>(_threat.GetPendingTiles(PlenoSource));
            Assert.IsNotEmpty(threatened, "Precondición: el Pleno tenía que quedar marcado.");

            var landed = PositionOf(_boss);
            int radius = CroupierAssetBuilder.PlenoHoleRadius;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var tile = new GridCoord(landed.X + dx, landed.Y + dy);
                    Assert.IsFalse(threatened.Contains(tile),
                        $"El paño amenaza {tile}, que está dentro del cuadrado a salvo alrededor " +
                        $"del jefe ({landed}). El hueco quedó anclado en otra casilla: el jugador " +
                        "cruza media sala hasta el único lugar que no arde y se quema igual.");
                }
            }
        }

        private Guid PlenoSource =>
            AINode_TelegraphMark.SourceKey(_boss, CroupierAssetBuilder.PlenoChannelId);

        /// <summary>El <c>Alternate</c> avanza su índice en cada tick, así que sobre qué tiempo cae el
        /// cruce se elige gastando (o no) un turno previo con la vida entera.</summary>
        private void TickTheCrossingTurn(bool onTheBurnBeat)
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            int round = 1;
            if (onTheBurnBeat)
            {
                // Dos turnos para llegar al de quema, no uno: el ciclo es Reparte, Bombas, Quema.
                root.Tick(NewContext(round++));
                root.Tick(NewContext(round++));
            }

            SetBossHp(PhaseTwoHp);
            var result = root.Tick(NewContext(round));
            Assert.AreNotEqual(AIResult.Failed, result, "El turno del cruce se cortó entero.");
        }

        private GridCoord PositionOf(Guid guid)
        {
            Assert.IsTrue(_grid.TryGetPosition(guid, out var coord),
                $"{guid} no está registrado en la grilla.");
            return coord;
        }

        /// <summary>Ciclo de dos tiempos, venga suelto o envuelto en el Selector de aislamiento.</summary>
        private static AINode_Alternate Alternate(AINode_Sequence root)
        {
            foreach (var step in root.Children)
            {
                if (step is AINode_Alternate direct) return direct;
                if (step is AINode_Selector selector)
                {
                    var nested = selector.Children.OfType<AINode_Alternate>().FirstOrDefault();
                    if (nested != null) return nested;
                }
            }

            Assert.Fail("No hay ciclo de dos tiempos en el árbol.");
            return null;
        }

        /// <summary>Los pasos de un tiempo del ciclo, sin el Selector de aislamiento de cada uno.</summary>
        private static List<AIDecisionNode> BeatSteps(AIDecisionNode beat)
        {
            var steps = new List<AIDecisionNode>();
            if (!(beat is AINode_Sequence sequence))
            {
                steps.Add(beat);
                return steps;
            }

            foreach (var child in sequence.Children)
            {
                if (child is AINode_Selector selector) steps.AddRange(selector.Children);
                else steps.Add(child);
            }
            return steps;
        }

        /// <summary>Se filtra por lo que cuelga del <c>Then</c> y no por el primer <c>If</c> del beat: el
        /// de quema tiene además el <c>If</c> que ramifica la duración del fuego por fase.</summary>
        private static AINode_If FleeGateOf(AIDecisionNode beat) =>
            BeatSteps(beat).OfType<AINode_If>().Single(g => g.Then is AINode_Random);

        private static AINode_Random FleeRouletteOf(AIDecisionNode beat) =>
            (AINode_Random)FleeGateOf(beat).Then;

        private static AINode_TeleportAwayToEdge FleeJumpOf(AIDecisionNode beat) =>
            FleeRouletteOf(beat).Options
                .Select(o => o.Node).OfType<AINode_TeleportAwayToEdge>().Single();

        private void MovePlayerBeyondFleeRadius()
        {
            var far = new GridCoord(BossTile.X - CroupierAssetBuilder.FleeTriggerRange - 2, BossTile.Y);
            Assert.IsTrue(_grid.Move(_player, far),
                $"La sala del fixture no llega hasta {far} para alejar al jugador.");
            Assert.Greater(BossTile.Manhattan(far), CroupierAssetBuilder.FleeTriggerRange,
                "El destino elegido sigue dentro del radio de fuga: el escenario no prueba nada.");
        }

        private void MovePlayerNextTo(GridCoord bossCoord)
        {
            var offsets = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            foreach (var (dx, dy) in offsets)
            {
                var candidate = new GridCoord(bossCoord.X + dx, bossCoord.Y + dy);
                if (!_grid.IsWalkable(candidate) || _grid.IsOccupied(candidate)) continue;
                if (_grid.Move(_player, candidate)) return;
            }

            Assert.Fail($"No quedó casilla libre pegada a {bossCoord} para acercar al jugador.");
        }

        /// <summary>Golpes que NO son del paño: sirve para contar disparos del tiempo de reparto.</summary>
        private int ShotCount() =>
            _pipeline.Resolved.Count(c => c.Kind != AttackKind.Environmental);

        private void SetBossHp(int value) =>
            _bossStats.SetAttribute<Health>(new Health(value));

        private List<SpecialTileInfo> FireInstances() =>
            _tiles.ActiveInstances().Where(i => i.Definition == _fire).ToList();

        private List<DamageContext> EnvironmentalHits() =>
            _pipeline.Resolved.Where(c => c.Kind == AttackKind.Environmental).ToList();

        /// <summary>Tirada que fuerza el salto al borde.</summary>
        /// <remarks>
        /// Salen de los pesos y no de literales: <c>AINode_Random</c> parte el 0..1 en tres franjas
        /// proporcionales, así que un número fijo cambia de opción en cuanto se retoquen los pesos.
        /// Cada tirada apunta al <b>medio</b> de su franja para no depender de si el corte es
        /// inclusivo.
        /// </remarks>
        private const double RouletteTotal = CroupierAssetBuilder.FleeWeightEdge +
                                             CroupierAssetBuilder.FleeWeightCenter +
                                             CroupierAssetBuilder.FleeWeightStay;

        private const double RollEdge = CroupierAssetBuilder.FleeWeightEdge * 0.5d / RouletteTotal;

        /// <summary>Tirada que fuerza el aterrizaje en el centro de la sala.</summary>
        private const double RollCentre =
            (CroupierAssetBuilder.FleeWeightEdge + CroupierAssetBuilder.FleeWeightCenter * 0.5d) / RouletteTotal;

        /// <summary>Tirada que fuerza que se quede donde está.</summary>
        private const double RollStay =
            (CroupierAssetBuilder.FleeWeightEdge + CroupierAssetBuilder.FleeWeightCenter +
             CroupierAssetBuilder.FleeWeightStay * 0.5d) / RouletteTotal;

        /// <summary>Un <c>Random(seed)</c> fijo no sirve: cuál opción cae depende de cuántos draws se
        /// consumieron antes en el tick, así que mover un nodo le voltea el resultado al test. El
        /// aterrizaje pasa por <c>Sample()</c> y no por <c>NextDouble()</c>, así que sigue sorteando.</summary>
        private sealed class FixedRoll : System.Random
        {
            private readonly double _value;
            public FixedRoll(double value) : base(1) { _value = value; }
            public override double NextDouble() => _value;
        }

        private AIContext NewContext(int roundIndex) => NewContext(roundIndex, RollEdge);

        private AIContext NewContext(int roundIndex, double roll) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = CroupierAssetBuilder.MaxHp,
            Grid = _grid,
            Movement = _movement,
            Attributes = _attributes,
            DamagePipeline = _pipeline,
            PlayerService = _playerService,
            RoundIndex = roundIndex,
            // El RNG decide una rama real (borde, centro o quedarse): fijo para que el turno sea
            // reproducible, y con la tirada explícita para que cada test diga qué salida mide.
            Rng = new FixedRoll(roll),
        };

        private AIContext NewContextWithoutMovement(int roundIndex)
        {
            var context = NewContext(roundIndex);
            context.Movement = null;
            return context;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            _created.Add(instance);
            return instance;
        }

        /// <summary>Registra qué fuentes se apagaron y no pinta nada. Los spies de overlay son privados
        /// en cada fixture porque cada uno observa una cosa distinta.</summary>
        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public readonly List<Guid> Cleared = new List<Guid>();

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                Color? tint = null) { }
            public void Clear(Guid sourceGuid) => Cleared.Add(sourceGuid);
            public void ClearAll() => Cleared.Clear();
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
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
