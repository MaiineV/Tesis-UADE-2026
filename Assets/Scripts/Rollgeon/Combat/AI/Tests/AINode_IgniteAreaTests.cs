using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El nodo que convierte un área telegrafiada en casillas especiales, contra los servicios
    /// reales de amenaza y de casillas. Lo que se verifica acá es el <b>comportamiento</b>: qué se
    /// planta y a quién se le cobra. Lo que autora cada jefe en sus campos lo cubren los suites de
    /// wiring.
    /// </summary>
    /// <remarks>
    /// Sin <c>IThreatOverlayService</c> registrado a propósito: el nodo lo resuelve con TryGet y sin
    /// él es no-op, así que el fixture no pare GameObjects de overlay en EditMode.
    /// </remarks>
    [TestFixture]
    public class AINode_IgniteAreaTests
    {
        private const int RoomWidth = 9;
        private const int RoomHeight = 9;
        private const int MarkDamage = 4;

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private SpecialTileService _tiles;
        private SpyDamagePipeline _pipeline;
        private SpecialTileDefinitionSO _fire;
        private SpecialTileDefinitionSO _ice;

        private Guid _boss;
        private Guid _player;
        private GridCoord _playerCoord;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomWidth, RoomHeight));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline, ServiceScope.Global);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _playerCoord = new GridCoord(2, 2);
            _grid.Register(_boss, new GridCoord(6, 6));
            _grid.Register(_player, _playerCoord);

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);

            _fire = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _fire.hideFlags = HideFlags.HideAndDontSave;
            _fire.TileId = "TILE_TEST_FIRE";
            _fire.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            _fire.DefaultDurationRounds = 3;
        }

        [TearDown]
        public void TearDown()
        {
            _tiles?.Dispose();
            _threat?.Dispose();
            if (_fire != null) UnityEngine.Object.DestroyImmediate(_fire);
            _fire = null;
            if (_ice != null) UnityEngine.Object.DestroyImmediate(_ice);
            _ice = null;

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // =====================================================================
        // Qué se planta
        // =====================================================================

        [Test]
        public void WithNoPendingMark_ItSucceedsWithoutPlantingOrCharging()
        {
            var result = Ignite(durationRounds: 3);

            // Failed acá abortaría la Sequence del turno del jefe: el primer turno de una pelea
            // telegrafía y todavía no hay nada que encender, y eso no es un fallo.
            Assert.AreEqual(AIResult.Succeeded, result,
                "Sin marca pendiente el nodo falló, y un Failed en este paso se lleva puesto el " +
                "resto del turno del jefe.");
            Assert.IsEmpty(Instances(), "Se plantó fuego sin que nadie lo hubiera telegrafiado.");
            Assert.IsEmpty(_pipeline.Resolved, "Se cobró daño sin marca de la que sacarlo.");
        }

        [Test]
        public void ItPlantsExactlyTheMarkedTiles_AndFiltersWhatTheRoomDoesNotHave()
        {
            // Una casilla adentro (la del jugador) y una que la sala no tiene. La forma
            // telegrafiada puede pasarse de los bordes; plantar afuera deja instancias que nadie
            // puede pisar ni ver expirar.
            var outside = new GridCoord(RoomWidth + 5, RoomHeight + 5);
            Mark(damage: 0, _playerCoord, outside);

            Ignite(durationRounds: 3);

            var instances = Instances();
            Assert.AreEqual(1, instances.Count, "Tiene que quedar una sola instancia de fuego.");
            CollectionAssert.AreEquivalent(new[] { _playerCoord }, new List<GridCoord>(instances[0].Coords),
                "Se plantó fuera de la grilla: esas casillas no las pisa nadie y no se las ve " +
                "expirar, así que el fuego queda contado y no jugado.");
        }

        [Test]
        public void ItPassesTheAuthoredDuration_AndNeverAZero()
        {
            Mark(damage: 0, _playerCoord);

            Ignite(durationRounds: 0);

            // En ISpecialTileService.Place un 0 significa PERMANENTE, no "sin duración". Un campo
            // nuevo nace en 0 en todo ED_Boss_*.asset ya serializado, así que un 0 que llegue al
            // servicio deja fuego encendido para siempre.
            Assert.AreEqual(_fire.DefaultDurationRounds, Instances()[0].RemainingRounds,
                "Un 0 en el nodo llegó al servicio como 0: en Place eso es PERMANENTE y la pelea " +
                "queda invivible. Tiene que caer al default del SO.");
        }

        // =====================================================================
        // A quién se le cobra
        // =====================================================================

        [Test]
        public void AMarkWithoutDamage_ChargesNothingWhenItLights()
        {
            // Es el caso de una banda telegrafiada un turno antes: el jugador tuvo su turno para
            // salirse, así que prenderla no puede además pegarle.
            Mark(damage: 0, _playerCoord);

            Ignite(durationRounds: 3);

            Assert.IsNotEmpty(Instances(), "Fixture roto: no se plantó nada.");
            Assert.IsEmpty(_pipeline.Resolved,
                "Una marca en 0 cobró al prender: el aviso de un turno pasa a ser un golpe gratis " +
                "encima, del que el jugador ya no podía escapar.");
        }

        [Test]
        public void AMarkWithDamage_ChargesItOnceToWhoeverIsStandingInside()
        {
            // El caso de un área que marca y enciende en el mismo tick: nadie tuvo un turno para
            // caminar afuera, así que el golpe al prender es el único acuse de recibo que existe.
            Mark(MarkDamage, AttackKind.Environmental, _playerCoord, new GridCoord(3, 2));

            Ignite(durationRounds: 3);

            Assert.AreEqual(1, _pipeline.Resolved.Count,
                "La ignición no cobró exactamente una vez: el área tiene varias casillas y el " +
                "jugador está parado en una sola.");

            var hit = _pipeline.Resolved[0];
            Assert.AreEqual(MarkDamage, hit.BaseDamage,
                "El daño no salió de la marca. El número vive ahí justamente para que cada " +
                "ignición decida desde el paso que la telegrafió.");
            Assert.AreEqual(_player, hit.TargetId, "Le cobró a otro.");
            Assert.AreEqual(_boss, hit.SourceId,
                "Sin el jefe como source el golpe no se le atribuye a nadie: el crédito de muerte " +
                "y los modificadores de fuente quedan sueltos.");
            Assert.AreEqual(AttackKind.Environmental, hit.Kind,
                "El Kind también viaja en la marca — es lo que distingue el fuego de un golpe.");
            Assert.IsNotEmpty(Instances(), "Cobró pero no plantó: el golpe no reemplaza al fuego.");
        }

        [Test]
        public void AMarkWithDamage_ChargesNothingWhenThePlayerIsOutsideIt()
        {
            Mark(MarkDamage, AttackKind.Environmental, new GridCoord(7, 7));

            Ignite(durationRounds: 3);

            Assert.IsNotEmpty(Instances(), "Fixture roto: no se plantó nada.");
            Assert.IsEmpty(_pipeline.Resolved,
                "Cobró a alguien que estaba afuera del área. Es el síntoma de un Contains dado " +
                "vuelta o directamente perdido, y convierte cualquier ignición en daño garantizado.");
        }

        [Test]
        public void ItChargesEvenWhenNothingSurvivesTheRoomFilter()
        {
            // Se marca la casilla del jugador y después la sala se encoge hasta dejarla afuera. El
            // golpe no puede colgar de que quede algo plantable: son dos pasos distintos.
            Mark(MarkDamage, AttackKind.Environmental, _playerCoord);
            _grid.LoadRoom(NavGraph.Rect(1, 1));
            _grid.Register(_player, _playerCoord);

            Ignite(durationRounds: 3);

            Assert.IsEmpty(Instances(),
                "Fixture roto: la casilla del jugador tendría que haber quedado afuera de la sala.");
            Assert.AreEqual(1, _pipeline.Resolved.Count,
                "El golpe de la ignición quedó colgado de que hubiera casillas plantables. Son dos " +
                "pasos distintos: se cobra a quien estaba adentro, se planta donde la sala deja.");
        }

        // =====================================================================
        // Solapamiento: una casilla, un fuego
        // =====================================================================

        [Test]
        public void ASecondAreaOverlappingTheFirst_PlantsOnlyWhatWasNotBurningYet()
        {
            // La banda apunta al jugador y este jefe la prende cada dos turnos, así que la
            // siguiente cae encima de la anterior todo el tiempo. Lo compartido ya arde: plantarlo
            // de nuevo no agrega fuego, agrega una segunda instancia que cobra por separado.
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4));
            Ignite(durationRounds: 3);
            var firstId = Instances()[0].InstanceId;

            Mark(damage: 0, new GridCoord(5, 4), new GridCoord(6, 4), new GridCoord(7, 4));
            Ignite(durationRounds: 3);

            var instances = Instances();
            Assert.AreEqual(2, instances.Count,
                "La segunda ignición no dejó instancia propia: la parte nueva de la banda quedó " +
                "apagada y la telegrafía ya había prometido fuego ahí.");

            CollectionAssert.AreEquivalent(new[] { new GridCoord(7, 4) },
                new List<GridCoord>(InstanceOtherThan(firstId).Coords),
                "La segunda banda replantó casillas que ya ardían. Cada instancia cobra aparte " +
                "—ResolveStand y ResolveEntries recorren TODAS las instancias que contienen la " +
                "casilla y disparan una por una—, así que esa casilla pega el doble y dibuja dos " +
                "visuales encimados.");
            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4) },
                new List<GridCoord>(InstanceById(firstId).Coords),
                "La segunda ignición le sacó casillas a la primera: el fuego que el jugador ya vio " +
                "encendido no puede apagarse porque el jefe prendió la banda de al lado.");
            AssertNoSameDefinitionOverlap();
        }

        [Test]
        public void ASecondAreaFullyInsideTheFirst_PlantsNothingAndStillSucceeds()
        {
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4));
            Ignite(durationRounds: 3);
            Assert.AreEqual(1, Instances().Count, "Fixture roto: la primera ignición no plantó.");

            Mark(damage: 0, new GridCoord(5, 4), new GridCoord(6, 4));
            var result = Ignite(durationRounds: 3);

            // Con el área nueva entera ardiendo la lista de plantables queda vacía y el nodo sale
            // por el early return de placeable.Count == 0. Un Failed acá abortaría la Sequence
            // entera del turno del jefe, y prender fuego donde ya hay fuego no es un fallo: es la
            // geometría normal de una banda que apunta al jugador dos veces seguidas.
            Assert.AreEqual(AIResult.Succeeded, result,
                "Un área que ya ardía entera devolvió Failed: eso corta la Sequence y el jefe " +
                "pierde el resto del turno (moverse, atacar) por no tener nada nuevo que encender.");
            Assert.AreEqual(1, Instances().Count,
                "Apareció una instancia duplicada encima de la primera. Place NO valida " +
                "solapamiento (sólo CreateRuntime lo hace), así que el filtro del nodo es lo único " +
                "que evita que esas casillas cobren dos veces.");
        }

        [Test]
        public void NoCoordEverBurnsUnderTwoInstancesOfTheSameDefinition()
        {
            Mark(damage: 0, new GridCoord(3, 5), new GridCoord(4, 5), new GridCoord(5, 5));
            Ignite(durationRounds: 3);

            Mark(damage: 0, new GridCoord(4, 5), new GridCoord(5, 5), new GridCoord(6, 5));
            Ignite(durationRounds: 4);

            // El invariante, no el mecanismo: no importa si el filtro vive en el nodo, en Place o
            // en otro lado; lo que no puede pasar es que una casilla termine bajo dos fuegos.
            AssertNoSameDefinitionOverlap();
        }

        [Test]
        public void ADifferentSubstance_StillPlantsOnGroundThatIsAlreadyBurning()
        {
            _ice = NewDefinition("TILE_TEST_ICE");

            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4));
            Ignite(durationRounds: 3);

            Mark(damage: 0, new GridCoord(5, 4));
            Ignite(_ice, durationRounds: 3);

            Assert.AreEqual(2, Instances().Count,
                "La segunda sustancia no se plantó en ningún lado: el filtro dejó de mirar la " +
                "definición y pasó a rechazar cualquier casilla especial.");
            CollectionAssert.Contains(new List<GridCoord>(InstanceWith(_ice).Coords), new GridCoord(5, 4),
                "El filtro se comió una sustancia distinta sobre una casilla que ya ardía. Dos " +
                "sustancias en una misma casilla es legítimo —son dos efectos, no uno duplicado—; " +
                "filtrar por cualquier casilla especial convierte esto en una regla global sobre el " +
                "hielo y el veneno de las salas autoradas, que usan el mismo servicio.");
        }

        [Test]
        public void ALaneCrossingOldGround_DoesNotRefreshTheFireThatWasAlreadyThere()
        {
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4));
            Ignite(durationRounds: 2);
            var oldId = Instances()[0].InstanceId;
            Assert.AreEqual(2, InstanceById(oldId).RemainingRounds,
                "Fixture roto: la primera instancia no arrancó con la duración pedida.");

            // Casilla compartida y duración más larga, que es la tentación de pisar el reloj viejo.
            Mark(damage: 0, new GridCoord(5, 4), new GridCoord(6, 4));
            Ignite(durationRounds: 9);

            Assert.AreEqual(2, InstanceById(oldId).RemainingRounds,
                "La banda nueva le renovó el reloj a la vieja. El jefe huye siempre por el mismo " +
                "corredor y lo vuelve a prender cada dos turnos, así que refrescar deja ese fuego " +
                "prácticamente eterno y el corredor cerrado por el resto de la pelea.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void Mark(int damage, params GridCoord[] coords) =>
            Mark(damage, AttackKind.Environmental, coords);

        private void Mark(int damage, AttackKind kind, params GridCoord[] coords) =>
            _threat.Mark(_boss, coords, damage, kind);

        private AIResult Ignite(int durationRounds) => Ignite(_fire, durationRounds);

        private AIResult Ignite(SpecialTileDefinitionSO definition, int durationRounds)
        {
            var node = new AINode_IgniteArea
            {
                Definition = definition,
                DurationRounds = durationRounds,
            };
            return node.Tick(new AIContext
            {
                SelfGuid = _boss,
                PlayerGuid = _player,
                Grid = _grid,
                DamagePipeline = _pipeline,
            });
        }

        private List<SpecialTileInfo> Instances() => new List<SpecialTileInfo>(_tiles.ActiveInstances());

        /// <summary>
        /// El invariante: una casilla, un fuego. Compara sólo pares de la misma definición —dos
        /// sustancias distintas conviviendo en una casilla es legítimo (ver
        /// <see cref="ADifferentSubstance_StillPlantsOnGroundThatIsAlreadyBurning"/>).
        /// </summary>
        private void AssertNoSameDefinitionOverlap()
        {
            var instances = Instances();
            for (var i = 0; i < instances.Count; i++)
            {
                for (var j = i + 1; j < instances.Count; j++)
                {
                    if (instances[i].Definition != instances[j].Definition) continue;

                    var other = new HashSet<GridCoord>(instances[j].Coords);
                    var shared = new List<GridCoord>();
                    foreach (var coord in instances[i].Coords)
                        if (other.Contains(coord)) shared.Add(coord);

                    Assert.IsEmpty(shared,
                        $"Casillas bajo dos instancias de {instances[i].Definition.TileId}: " +
                        $"{string.Join(", ", shared)}. Cada instancia dispara sus triggers por " +
                        "separado, así que ahí el jugador paga el doble de daño y ve dos visuales " +
                        "encimados por un solo fuego.");
                }
            }
        }

        private SpecialTileInfo InstanceById(Guid instanceId)
        {
            foreach (var instance in Instances())
                if (instance.InstanceId == instanceId) return instance;

            Assert.Fail("La instancia plantada antes ya no existe: la ignición siguiente se llevó " +
                        "puesto un fuego que el jugador ya tenía en pantalla.");
            return default;
        }

        private SpecialTileInfo InstanceOtherThan(Guid instanceId)
        {
            foreach (var instance in Instances())
                if (instance.InstanceId != instanceId) return instance;

            Assert.Fail("No hay una segunda instancia: la ignición que tenía casillas nuevas para " +
                        "prender no plantó nada.");
            return default;
        }

        private SpecialTileInfo InstanceWith(SpecialTileDefinitionSO definition)
        {
            foreach (var instance in Instances())
                if (instance.Definition == definition) return instance;

            Assert.Fail($"No quedó ninguna instancia de {definition.TileId}: la sustancia no llegó " +
                        "a plantarse.");
            return default;
        }

        /// <summary>Otra sustancia, con el mismo tratamiento de vida que <c>_fire</c> en SetUp.</summary>
        private static SpecialTileDefinitionSO NewDefinition(string tileId)
        {
            var definition = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.TileId = tileId;
            definition.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            definition.DefaultDurationRounds = 3;
            return definition;
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
    }
}
