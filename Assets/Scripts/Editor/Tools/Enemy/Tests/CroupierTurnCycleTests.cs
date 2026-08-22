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
using Rollgeon.Tiles;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Corre el árbol REAL del Croupier turno a turno, con los servicios reales de amenaza,
    /// casillas y movimiento. Lo que cubre es el <b>cruce del 50%</b>: cuántos avisos quedan
    /// pendientes al cerrar ese turno y cuántas detonaciones caen al siguiente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CroupierPhaseWiringTests</c> mira la forma del árbol: que el descarte exista, que tenga el
    /// canal correcto y que caiga entre el teleport y el marcado. Nada de eso demuestra el
    /// resultado, que es lo que el jugador reportó: "prepara 2 ataques y después ejecuta 2". Acá se
    /// tickea y se cuenta.
    /// </para>
    /// <para>
    /// El cruce cae en un turno cualquiera del ciclo de dos tiempos, así que hay un caso por beat:
    /// sobre el reparto (T1) hay una banda recién marcada que descartar, y sobre la quema (T2) no
    /// hay ninguna porque ese mismo turno la consumió. Los dos tienen que terminar con
    /// <b>exactamente un</b> aviso pendiente.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CroupierTurnCycleTests
    {
        /// <summary>La sala del jefe. El centro (5,5) es donde lo planta el armado del Pleno.</summary>
        private const int RoomSide = 11;

        /// <summary>
        /// Centro de <c>NavGraph.Rect(11,11)</c>, que es 0-based: (5,5). Sale del lado de la sala
        /// del fixture y no de la de producción — el nodo resuelve el centro del bounding box de
        /// lo que haya cargado.
        /// </summary>
        private static readonly GridCoord RoomCentre = new GridCoord(RoomSide / 2, RoomSide / 2);

        private static readonly GridCoord BossTile = new GridCoord(8, 5);

        /// <summary>
        /// Manhattan 6 del jefe: dentro del alcance del disparo, así que T1 corre entero — dispara,
        /// salta al borde y marca la banda. Y queda fuera del hueco del Pleno, así que la ignición
        /// del paño le cobra.
        /// </summary>
        private static readonly GridCoord PlayerTile = new GridCoord(2, 5);

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

            // Registrado ANTES del primer marcado: ThreatTelegraphOverlay.ResolveOrCreate devuelve
            // lo que ya esté en el locator, así que el spy evita que el fixture pare GameObjects de
            // overlay en EditMode y además deja observar qué se apagó.
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

        // =====================================================================
        // El cruce del 50% sobre el tiempo de reparto — el caso reportado
        // =====================================================================

        /// <summary>
        /// El turno que cruza el 50% cayendo en T1 cierra con <b>un</b> aviso pendiente, no dos: el
        /// Pleno reemplaza a la banda que ese mismo turno acababa de marcar.
        /// </summary>
        /// <remarks>
        /// Es el bug tal como se reportó: la banda marcada y el paño marcado prendidos a la vez, y
        /// al turno siguiente las dos detonaciones. El aviso de la banda vale menos que nada acá —
        /// el Pleno prende todo salvo el hueco, así que su terreno ya arde y su beat se absorbe sin
        /// mostrar nada.
        /// </remarks>
        [Test]
        public void CrossingFiftyOnTheDealBeat_LeavesOnlyThePlenoQueued()
        {
            SetBossHp(PhaseTwoHp);
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            var result = root.Tick(NewContext(roundIndex: 1));

            Assert.AreNotEqual(AIResult.Failed, result, "El turno del jefe se cortó entero.");
            Assert.IsTrue(_threat.HasPending(PlenoSource),
                "El Pleno no quedó encolado: el descarte se llevó puesto el aviso que tenía que " +
                "reemplazar a la banda, o el marcado nunca corrió.");
            Assert.IsFalse(_threat.HasPending(_boss),
                "La banda de T1 siguió pendiente junto con el Pleno. Son los DOS avisos prendidos " +
                "a la vez que reportó el jugador, y al turno siguiente detonan los dos.");
            CollectionAssert.Contains(_overlay.Cleared, _boss,
                "Se descartó el área de la banda pero quedó su dibujo: un aviso pintado que no va a " +
                "detonar nunca, que se lee igual de mal que los dos avisos.");
        }

        /// <summary>
        /// Y el turno siguiente cobra <b>una</b> detonación: prende el paño y nada más.
        /// </summary>
        /// <remarks>
        /// La otra mitad de lo reportado ("después ejecuta 2 ataques"). Además fija que el tiempo de
        /// quema, que se quedó sin banda que consumir, no corte el turno: <c>AINode_IgniteArea</c>
        /// sin marca pendiente sale por <c>Succeeded</c>, no por <c>Failed</c>.
        /// </remarks>
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
            Assert.IsFalse(_threat.HasPending(_boss), "Apareció una banda pendiente de la nada.");
        }

        // =====================================================================
        // El cruce sobre el tiempo de quema
        // =====================================================================

        /// <summary>
        /// Cruzando el 50% en T2 no hay banda que descartar —ese mismo turno la consumió la
        /// quema—, y el descarte de más no puede tocar el aviso del Pleno.
        /// </summary>
        /// <remarks>
        /// El caso que hace que el paso no pueda devolver <c>Failed</c> cuando no encuentra nada: va
        /// desnudo dentro del Sequence del armado, así que un Failed acá corta el turno antes del
        /// marcado y deja a <c>AINode_Once</c> sin latchear.
        /// </remarks>
        [Test]
        public void CrossingFiftyOnTheBurnBeat_BurnsTheBandAndStillQueuesThePleno()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            // Turno 1 con la vida entera: T1 marca la banda y ningún gate abre.
            root.Tick(NewContext(roundIndex: 1));
            Assert.IsTrue(_threat.HasPending(_boss), "Precondición: T1 tenía que dejar la banda marcada.");

            SetBossHp(PhaseTwoHp);
            var result = root.Tick(NewContext(roundIndex: 2));

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

        // =====================================================================
        // El armado que no se completa
        // =====================================================================

        /// <summary>
        /// Si el teleport al centro falla, la banda <b>conserva</b> su aviso: el descarte va detrás
        /// del único paso del bloque que puede fallar de verdad.
        /// </summary>
        /// <remarks>
        /// Lo que esto protege es el peor resultado posible del turno: descartar el aviso del ciclo
        /// y no dejar nada en su lugar. El jugador terminaría el turno sin ninguna amenaza dibujada
        /// y el jefe sin nada que cobrar al siguiente — un turno en blanco para los dos, sin un solo
        /// error en consola. Mover el descarte arriba del teleport es todo lo que hace falta para
        /// producirlo.
        /// </remarks>
        [Test]
        public void WhenTheTeleportFails_TheBandKeepsItsWarningAndThePlenoRetriesLater()
        {
            SetBossHp(PhaseTwoHp);
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            // Sin servicio de movimiento el teleport al centro falla avisando, que es el modo de
            // fallo que el propio nodo documenta. El salto de T1 también se cae, en silencio y
            // aislado en su Selector, así que el beat sigue y la banda igual se marca.
            LogAssert.Expect(LogType.Warning, new Regex("AINode_TeleportToRoomCenter"));
            root.Tick(NewContextWithoutMovement(roundIndex: 1));

            Assert.IsTrue(_threat.HasPending(_boss),
                "El armado abortó a mitad y se llevó igual el aviso de la banda: el jugador cierra " +
                "el turno sin ninguna amenaza dibujada y el jefe sin nada que prender.");
            Assert.IsFalse(_threat.HasPending(PlenoSource),
                "El Pleno quedó encolado con el jefe fuera del centro: el hueco a salvo cae donde " +
                "estaba parado, contra la pared, y el paño deja de leerse.");
            CollectionAssert.DoesNotContain(_overlay.Cleared, _boss,
                "Se apagó el dibujo de la banda que sí sigue pendiente: detona a ciegas.");

            // Y el umbral no se perdió: AINode_Once no latchea con Failed.
            var result = root.Tick(NewContext(roundIndex: 2));

            Assert.AreNotEqual(AIResult.Failed, result, "El reintento cortó el turno.");
            Assert.AreEqual(1, FireInstances().Count, "La banda que conservó su aviso no ardió.");
            Assert.IsTrue(_threat.HasPending(PlenoSource),
                "El armado no se reintentó: el umbral del 50% se perdió para toda la pelea.");
        }

        // =====================================================================
        // El salto de todos los turnos
        // =====================================================================

        /// <summary>
        /// El jefe salta en los <b>dos</b> tiempos, y sólo el de quema lleva tope.
        /// </summary>
        /// <remarks>
        /// El tope es la ventana entera de la pelea: el jugador amenaza 8 casillas por turno, así
        /// que el tiempo sin tope siempre lo deja fuera de alcance y el otro es el único en que se
        /// lo puede tocar.
        /// </remarks>
        [Test]
        public void BothBeats_TeleportHimAway_AndOnlyTheBurnOneIsCapped()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);
            var beats = Alternate(root).Children;

            foreach (var beat in beats)
            {
                Assert.AreEqual(1, BeatSteps(beat).OfType<AINode_TeleportAwayToEdge>().Count(),
                    "Un tiempo del ciclo dejó de tener exactamente un salto: o el jefe se queda " +
                    "quieto ese turno, o salta dos veces y el segundo pisa al primero.");
            }

            // Por descarte y no por el nodo que prende: la ignición cuelga de un If que ramifica
            // por fase, así que no es un paso suelto del beat.
            var burn = beats.Single(b => !BeatSteps(b).Any(n => n is AINode_RangedShot));
            var capped = BeatSteps(burn).OfType<AINode_TeleportAwayToEdge>().Single();
            Assert.AreEqual(CroupierAssetBuilder.QuemaTeleportMaxDistance, capped.MaxDistanceFromPlayer,
                "El salto del tiempo de quema perdió su tope.");

            // Y salta de verdad los dos turnos, no sólo en la forma del árbol.
            var start = PositionOf(_boss);
            root.Tick(NewContext(roundIndex: 1));
            var afterDeal = PositionOf(_boss);
            Assert.AreNotEqual(start, afterDeal, "El tiempo de reparto dejó al jefe donde estaba.");

            root.Tick(NewContext(roundIndex: 2));
            Assert.AreNotEqual(afterDeal, PositionOf(_boss),
                "El tiempo de quema dejó al jefe donde estaba.");
        }

        /// <summary>
        /// El tope del salto de quema tiene que caber dentro de lo que el jugador alcanza en un
        /// turno.
        /// </summary>
        /// <remarks>
        /// Es una comparación de constantes y no un tick a propósito: el número del kit del jugador
        /// no vive en este repo como una constante que se pueda leer, así que lo que se pinea es la
        /// relación.
        /// </remarks>
        [Test]
        public void TheBurnJumpStaysInsideWhatThePlayerCanReachInATurn()
        {
            // 4 de movimiento (BFS) + 4 de alcance del ataque especial (Manhattan), una vez cada
            // uno y sin gap-closer.
            const int playerReachPerTurn = 8;

            Assert.LessOrEqual(CroupierAssetBuilder.QuemaTeleportMaxDistance, playerReachPerTurn,
                $"El jefe salta en los dos tiempos y el de quema es el único con tope. Por encima " +
                $"de {playerReachPerTurn} aterriza fuera de alcance todos los turnos: no queda " +
                "ninguno en el que el jugador pueda llegar a golpearlo y los 200 de vida del jefe " +
                "pasan a ser infinitos.");
        }

        /// <summary>
        /// El fuego cae en las casillas que se avisaron, aunque entre el aviso y la ignición el
        /// jefe haya saltado dos veces.
        /// </summary>
        /// <remarks>
        /// <c>AINode_IgniteArea</c> consume la marca en vez de recalcular la forma. Recalculándola
        /// —la banda está anclada en el jefe y apunta al jugador— el fuego caería desde la casilla
        /// nueva y el aviso que el jugador leyó todo el turno no valdría nada.
        /// </remarks>
        [Test]
        public void TheFireLandsWhereItWasAnnounced_EvenAfterTheJump()
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            root.Tick(NewContext(roundIndex: 1));
            var announced = new HashSet<GridCoord>(_threat.GetPendingTiles(_boss));
            Assert.IsNotEmpty(announced, "Precondición: el tiempo de reparto tenía que marcar la banda.");

            root.Tick(NewContext(roundIndex: 2));

            var burning = new HashSet<GridCoord>(FireInstances().SelectMany(i => i.Coords));
            CollectionAssert.AreEquivalent(announced, burning,
                "El fuego no cubre la banda que se avisó. Entre el aviso y la ignición el jefe " +
                "saltó dos veces: si el área se recalcula, cae desde donde está ahora y el jugador " +
                "se quema en una casilla que nunca vio marcada.");
        }

        // =====================================================================
        // El cruce del 50% y el hueco del Pleno
        // =====================================================================

        /// <summary>
        /// El turno que cruza el 50% termina con el jefe <b>en el centro</b>, caiga el cruce sobre
        /// el tiempo que caiga.
        /// </summary>
        /// <remarks>
        /// El armado del Pleno es el último paso del turno justamente por esto: cualquier salto
        /// colgado detrás lo arranca del centro después de haber plantado ahí el hueco, y el ataque
        /// más grande de la pelea se lee al revés — el único lugar a salvo queda donde el jefe no
        /// está.
        /// </remarks>
        [TestCase(false, TestName = "CrossingFifty_EndsTheTurnWithTheBossInTheCentre_OnTheDealBeat")]
        [TestCase(true, TestName = "CrossingFifty_EndsTheTurnWithTheBossInTheCentre_OnTheBurnBeat")]
        public void CrossingFifty_EndsTheTurnWithTheBossInTheCentre(bool onTheBurnBeat)
        {
            TickTheCrossingTurn(onTheBurnBeat);

            Assert.AreEqual(RoomCentre, PositionOf(_boss),
                "El turno del cruce no terminó con el jefe en el centro de la sala.");
        }

        /// <summary>
        /// Y el hueco que el Pleno deja sin marcar rodea la casilla <b>final</b> del jefe.
        /// </summary>
        /// <remarks>
        /// El gemelo por comportamiento del test de arriba: aquél mira dónde quedó parado, éste
        /// mira dónde quedó el único lugar a salvo. Con un salto detrás del armado los dos dejan de
        /// coincidir sin que falle nada.
        /// </remarks>
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

        // =====================================================================
        // Helpers
        // =====================================================================

        private Guid PlenoSource =>
            AINode_TelegraphMark.SourceKey(_boss, CroupierAssetBuilder.PlenoChannelId);

        /// <summary>Corre el turno que cruza el 50% sobre el tiempo pedido.</summary>
        /// <remarks>
        /// El <c>Alternate</c> avanza su índice en cada tick, así que sobre qué tiempo cae el cruce
        /// se elige gastando (o no) un turno previo con la vida entera.
        /// </remarks>
        private void TickTheCrossingTurn(bool onTheBurnBeat)
        {
            var root = CroupierAssetBuilder.BuildAIRoot(_fire);

            int round = 1;
            if (onTheBurnBeat) root.Tick(NewContext(round++));

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

        private void SetBossHp(int value) =>
            _bossStats.SetAttribute<Health>(new Health(value));

        private List<SpecialTileInfo> FireInstances() =>
            _tiles.ActiveInstances().Where(i => i.Definition == _fire).ToList();

        private List<DamageContext> EnvironmentalHits() =>
            _pipeline.Resolved.Where(c => c.Kind == AttackKind.Environmental).ToList();

        private AIContext NewContext(int roundIndex) => new AIContext
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
            // Fijo: el candado del 70% es dirigido, pero el nodo igual pide RNG para el sorteo que
            // no usa, y un turno del jefe tiene que ser reproducible.
            Rng = new System.Random(1),
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

        /// <summary>
        /// Registra qué fuentes se apagaron y no pinta nada. Ver el gemelo de
        /// <c>AINode_CancelTelegraphTests</c>: los spies de overlay son privados en cada fixture
        /// porque cada uno observa una cosa distinta.
        /// </summary>
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
