using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using UnityEngine;
using UnityEngine.TestTools;

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

        [Test]
        public void ALaneThatSwallowsTheOldOneWhole_RelightsThatGroundWithItsOwnClock()
        {
            // La geometría normal de este jefe, no un borde: huye sobre el mismo eje y la banda le
            // sale de atrás con la profundidad de la sala, así que cada banda nueva CONTIENE a la
            // anterior. La vieja llega viva a la ignición siguiente (dura una ronda más que el
            // intervalo entre igniciones) con su reloj casi agotado.
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4));
            Ignite(durationRounds: 1);
            var oldId = Instances()[0].InstanceId;

            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4));
            var result = IgniteRelaying(durationRounds: 3);

            Assert.AreEqual(AIResult.Succeeded, result, "La ignición que reemplaza una banda falló.");

            var instances = Instances();
            Assert.AreEqual(1, instances.Count,
                "La banda vieja quedó puesta al lado de la nueva: entera adentro del área nueva no " +
                "aporta superficie, y dos instancias sobre la misma casilla cobran dos veces.");
            Assert.AreNotEqual(oldId, instances[0].InstanceId,
                "Sobrevivió la instancia vieja en vez de la nueva.");
            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4) },
                new List<GridCoord>(instances[0].Coords),
                "La banda avisada no ardió entera: lo que la vieja ya tapaba quedó afuera.");
            Assert.AreEqual(3, instances[0].RemainingRounds,
                "El terreno compartido heredó el reloj de la banda vieja —el más corto—, así que la " +
                "banda que el jugador acaba de ver avisada se apaga en el próximo wrap sin haber " +
                "ardido lo que prometió, y el turno de quema no muestra nada.");
            AssertNoSameDefinitionOverlap();
        }

        /// <summary>
        /// El jefe prende cada dos rondas una banda que contiene a la anterior: después del wrap
        /// siguiente el paño de la banda nueva tiene que estar encendido entero.
        /// </summary>
        /// <remarks>
        /// Una banda plantada en la ronda <c>r</c> con <c>DurationRounds = D</c> está viva durante
        /// <c>r … r+D-1</c> — la duración baja en el wrap de ronda. Con D = 3 y una ignición cada 2
        /// rondas, la banda vieja <b>llega viva</b> a la ignición siguiente, con una sola ronda de
        /// reloj encima.
        /// </remarks>
        [Test]
        public void TwoRelayedBands_LeaveTheWholeNewBandLitAfterTheNextRoundWrap()
        {
            // Los números del Croupier: FireDurationRounds y el ciclo de dos tiempos del Alternate.
            const int duration = 3;
            const int ignitionIntervalRounds = 2;

            var oldBand = new[] { new GridCoord(4, 4), new GridCoord(5, 4) };
            var newBand = new[] { new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4) };

            Mark(0, oldBand);
            IgniteRelaying(durationRounds: duration);

            for (int round = 1; round <= ignitionIntervalRounds; round++) WrapRound(round);

            Assert.AreEqual(1, Instances().Count, "Fixture roto: la primera banda no sobrevivió.");
            Assert.AreEqual(duration - ignitionIntervalRounds, Instances()[0].RemainingRounds,
                "Fixture roto: la banda vieja tendría que llegar a la ignición siguiente con una " +
                "ronda de reloj. Si esto cambió, la premisa del test (llega viva y casi agotada) " +
                "dejó de valer y hay que recalcular el escenario.");

            Mark(0, newBand);
            IgniteRelaying(durationRounds: duration);

            WrapRound(ignitionIntervalRounds + 1);

            var instances = Instances();
            var lit = new HashSet<GridCoord>();
            foreach (var instance in instances)
                foreach (var coord in instance.Coords) lit.Add(coord);

            CollectionAssert.AreEquivalent(newBand, new List<GridCoord>(lit),
                "Después del wrap el paño quedó apagado donde la banda nueva ya había sido avisada. " +
                "El terreno compartido se quedó con el reloj de la banda vieja y expiró con ella, " +
                "así que el turno de quema no muestra nada y el jugador ve al jefe perder un turno.");
            Assert.AreEqual(1, instances.Count,
                "La banda quedó partida en dos instancias: el relevo tiene que dejar una sola, o esas " +
                "casillas cobran dos veces.");
            Assert.AreEqual(duration - 1, instances[0].RemainingRounds,
                "La banda nueva arrancó con menos reloj del que autoró: hereda la cuenta de la " +
                "vieja en vez de la propia.");
            AssertNoSameDefinitionOverlap();
        }

        /// <summary>
        /// El caso que rompía con el cono del Croupier: dos áreas casi nunca se contienen entre sí,
        /// así que la vieja asoma afuera de la nueva en vez de quedar tapada entera. Tiene que
        /// encogerse, no sobrevivir intacta ni desaparecer.
        /// </summary>
        [Test]
        public void ALaneThatOnlyGrazesTheOldOne_ShrinksTheOldInstance_KeepingItsClockOnWhatEscapes()
        {
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4));
            IgniteRelaying(durationRounds: 5);
            var oldId = Instances()[0].InstanceId;

            Mark(damage: 0, new GridCoord(6, 4), new GridCoord(7, 4), new GridCoord(8, 4));
            IgniteRelaying(durationRounds: 3);

            var instances = Instances();
            Assert.AreEqual(2, instances.Count,
                "La vieja que asoma afuera del área nueva tenía que sobrevivir encogida, no " +
                "desaparecer ni quedarse entera al lado de la nueva.");
            CollectionAssert.AreEquivalent(new[] { new GridCoord(4, 4), new GridCoord(5, 4) },
                new List<GridCoord>(InstanceById(oldId).Coords),
                "La instancia vieja no se encogió a lo que le queda afuera del área nueva: (6,4) es " +
                "de la nueva ignición ahora.");
            Assert.AreEqual(5, InstanceById(oldId).RemainingRounds,
                "Encoger la instancia vieja le tocó el reloj: las casillas que le quedan afuera " +
                "tienen que seguir ardiendo con la cuenta que ya traían.");
            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(6, 4), new GridCoord(7, 4), new GridCoord(8, 4) },
                new List<GridCoord>(InstanceOtherThan(oldId).Coords),
                "La casilla compartida (6,4) se quedó afuera de la ignición nueva: el fuego nuevo " +
                "tiene que ganar la casilla en disputa.");
            Assert.AreEqual(3, InstanceOtherThan(oldId).RemainingRounds,
                "La casilla compartida no arrancó con el reloj nuevo.");
            AssertNoSameDefinitionOverlap();
        }

        /// <summary>
        /// El síntoma reportado: la vieja llega a la ignición siguiente casi agotada (reloj corto)
        /// y, sin encogerla, la casilla compartida heredaba ese reloj y se apagaba en el wrap
        /// siguiente aunque la ignición nueva le hubiera prometido más rondas.
        /// </summary>
        [Test]
        public void ALaneThatOnlyGrazesTheOldOne_TheSharedTileOutlivesTheOldsShorterClock()
        {
            const int oldDuration = 2;
            const int newDuration = 4;
            var shared = new GridCoord(6, 4);

            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4), shared);
            IgniteRelaying(durationRounds: oldDuration);

            Mark(damage: 0, shared, new GridCoord(7, 4));
            IgniteRelaying(durationRounds: newDuration);

            WrapRound(1);
            WrapRound(2); // oldDuration rondas: si (6,4) siguiera bajo la vieja, ya se habría apagado.

            Assert.IsTrue(TryFindInstanceCovering(shared, out var covering),
                "La casilla compartida se apagó en el wrap: heredó el reloj de la vieja —el más " +
                "corto— en vez de arrancar con el de la ignición que la acaba de prender.");
            Assert.AreEqual(newDuration - 2, covering.RemainingRounds,
                "La casilla compartida no está corriendo el reloj de la ignición nueva.");
        }

        /// <summary>
        /// El filtro de owner se mantiene con el encogido: apagar o encoger fuego que plantó otra
        /// entidad no es asunto de esta ignición, aunque su área lo tape.
        /// </summary>
        [Test]
        public void RetireFullyReplaced_DoesNotShrinkOrRemoveFireFromAnotherOwner()
        {
            var otherOwner = Guid.NewGuid();
            var otherOwnersCoords = new[] { new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4) };
            _tiles.Place(_fire, otherOwnersCoords, new TilePlacementOptions
            {
                Owner = otherOwner,
                DurationRounds = 5,
            });

            Mark(damage: 0, new GridCoord(5, 4), new GridCoord(6, 4), new GridCoord(7, 4));
            IgniteRelaying(durationRounds: 3);

            var instances = Instances();
            Assert.AreEqual(2, instances.Count,
                "El relevo tocó fuego de otro dueño: apagar o encoger lo que plantó otra entidad no " +
                "es asunto de esta ignición.");
            var untouched = InstanceOwnedBy(otherOwner);
            CollectionAssert.AreEquivalent(otherOwnersCoords, new List<GridCoord>(untouched.Coords),
                "La instancia de otro dueño perdió casillas: el filtro de owner tiene que dejarla " +
                "intacta.");
            Assert.AreEqual(5, untouched.RemainingRounds, "El reloj de otro dueño se tocó.");
            CollectionAssert.AreEquivalent(new[] { new GridCoord(7, 4) },
                new List<GridCoord>(InstanceOtherThan(untouched.InstanceId).Coords),
                "La ignición del jefe sólo puede prender lo que no ardía: (5,4) y (6,4) ya ardían " +
                "con el fuego de otro dueño.");
        }

        /// <summary>
        /// El filtro de definición se mantiene con el encogido: dos sustancias en una casilla son
        /// dos efectos, no un duplicado, así que el hielo no se toca cuando prende el fuego.
        /// </summary>
        [Test]
        public void RetireFullyReplaced_DoesNotShrinkOrRemoveAnotherDefinitionsInstance()
        {
            _ice = NewDefinition("TILE_TEST_ICE");
            var iceCoords = new[] { new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4) };
            Mark(damage: 0, iceCoords);
            Tick(new AINode_IgniteArea
            {
                Definition = _ice,
                DurationRounds = 5,
                RetireFullyReplaced = true,
            });

            Mark(damage: 0, new GridCoord(5, 4), new GridCoord(6, 4), new GridCoord(7, 4));
            IgniteRelaying(durationRounds: 3);

            var ice = InstanceWith(_ice);
            CollectionAssert.AreEquivalent(iceCoords, new List<GridCoord>(ice.Coords),
                "El relevo de fuego encogió una instancia de otra definición: dos sustancias en una " +
                "casilla son dos efectos, no un duplicado.");
            Assert.AreEqual(5, ice.RemainingRounds, "El reloj del hielo se tocó por una ignición de fuego.");
            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(5, 4), new GridCoord(6, 4), new GridCoord(7, 4) },
                new List<GridCoord>(InstanceWith(_fire).Coords),
                "El fuego nuevo tiene que plantar donde avisó: el hielo no cuenta como 'ya ardiendo' " +
                "para otra definición.");
        }

        /// <summary>
        /// <b>El relevo es opt-in y arranca apagado.</b> Sin tocar el flag, una banda vieja que el
        /// área nueva tapa por completo se queda donde está, con su propio reloj, y la nueva sólo
        /// prende lo que no ardía.
        /// </summary>
        /// <remarks>
        /// Este nodo lo monta cada jefe que prende piso —La Bandida entre ellos— y los
        /// <c>ED_Boss_*.asset</c> ya están serializados: Odin no corre field initializers al
        /// deserializar, así que el default de un campo nuevo es el que esos assets van a tener. Un
        /// default en <c>true</c> haría desaparecer, sin que nadie lo pidiera, fuego que el jugador
        /// ya tiene en pantalla en peleas que nadie tocó.
        /// </remarks>
        [Test]
        public void RetireFullyReplaced_IsOffByDefault_SoNobodyElsesFireDisappears()
        {
            var old = new[] { new GridCoord(4, 4), new GridCoord(5, 4) };

            Mark(damage: 0, old);
            Ignite(durationRounds: 2);
            var oldId = Instances()[0].InstanceId;

            // El área nueva contiene entera a la vieja: es el caso donde el relevo, si estuviera
            // prendido, la retiraría.
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4));
            Ignite(durationRounds: 3);

            Assert.AreEqual(2, Instances().Count,
                "El relevo se prendió solo: el default tiene que reproducir exactamente lo de antes, " +
                "porque es el valor que ya tienen serializado todos los jefes que montan este nodo.");
            CollectionAssert.AreEquivalent(old, new List<GridCoord>(InstanceById(oldId).Coords),
                "La instancia vieja perdió casillas sin que nadie pidiera relevarla.");
            Assert.AreEqual(2, InstanceById(oldId).RemainingRounds,
                "Con el relevo apagado la instancia vieja conserva su propio reloj: es lo que evita " +
                "que el corredor por el que el jefe huye quede encendido para siempre.");
            CollectionAssert.AreEquivalent(new[] { new GridCoord(6, 4) },
                new List<GridCoord>(InstanceOtherThan(oldId).Coords),
                "Con el relevo apagado la banda nueva sólo prende lo que no ardía.");
            AssertNoSameDefinitionOverlap();
        }

        // =====================================================================
        // El turno que se va en blanco
        // =====================================================================

        /// <summary>
        /// Consumió la marca y no plantó nada porque la sala no tiene esas casillas: sale con
        /// Succeeded —el default— pero <b>deja dicho en el log</b> que se comió el turno.
        /// </summary>
        [Test]
        public void AMarkedAreaTheRoomDoesNotHave_LogsWhyTheBurnTurnWentBlank()
        {
            var outside = new[]
            {
                new GridCoord(RoomWidth + 5, RoomHeight + 5),
                new GridCoord(RoomWidth + 6, RoomHeight + 5),
            };
            Mark(0, outside);

            LogAssert.Expect(LogType.Warning,
                new Regex("sin plantar nada: ninguna de las 2 casillas marcadas"));

            var result = Ignite(durationRounds: 3);

            Assert.AreEqual(AIResult.Succeeded, result,
                "El resultado por default tiene que seguir siendo Succeeded: este nodo se monta " +
                "desnudo dentro de Sequences y un Failed ahí corta el turno del jefe y deja al " +
                "AINode_Once sin latchear.");
            Assert.IsEmpty(Instances(), "Fixture roto: la sala no tenía ninguna de esas casillas.");
        }

        /// <summary>
        /// La otra causa: el área nueva ya ardía entera. Mismo aviso, distinto texto — acá lo que hay
        /// que revisar es el ritmo de las igniciones, no la forma ni la sala.
        /// </summary>
        [Test]
        public void AnAreaThatWasAlreadyBurningWhole_LogsWhyTheBurnTurnWentBlank()
        {
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4), new GridCoord(6, 4));
            Ignite(durationRounds: 3);
            Assert.AreEqual(1, Instances().Count, "Fixture roto: la primera ignición no plantó.");

            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4));

            LogAssert.Expect(LogType.Warning,
                new Regex("sin plantar nada: las 2 casillas de la sala ya ardían"));

            var result = Ignite(durationRounds: 3);

            Assert.AreEqual(AIResult.Succeeded, result,
                "Prender fuego donde ya hay fuego no es un fallo: es la geometría normal de una " +
                "banda que apunta al jugador dos veces seguidas.");
            Assert.AreEqual(1, Instances().Count,
                "Fixture roto: el aviso salió pero además apareció una instancia nueva.");
        }

        /// <summary>
        /// <c>FailWhenNothingToBurn</c> es lo que convierte el turno absorbido en un Failed que el
        /// Selector de arriba puede gastar en otra cosa. El aviso al log sale igual, y nombra con qué
        /// resultado salió.
        /// </summary>
        /// <remarks>
        /// Opt-in por lo mismo que el resto de los campos nuevos: apagado es lo que ya tienen
        /// serializado todos los <c>ED_Boss_*.asset</c> —Odin no corre field initializers al
        /// deserializar— y prenderlo en un paso montado desnudo dentro de una Sequence le cortaría el
        /// turno al jefe.
        /// </remarks>
        [Test]
        public void FailWhenNothingToBurn_ReportsTheAbsorbedTurnInsteadOfSwallowingIt()
        {
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4));
            Ignite(durationRounds: 3);

            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4));

            LogAssert.Expect(LogType.Warning, new Regex("sin plantar nada.*Resultado: Failed"));

            var result = Tick(new AINode_IgniteArea
            {
                Definition = _fire,
                DurationRounds = 3,
                FailWhenNothingToBurn = true,
            });

            Assert.AreEqual(AIResult.Failed, result,
                "Con el flag prendido el paso tiene que admitir que no hizo nada, que es lo único que " +
                "le permite al Selector que lo envuelve resolver el turno con otra cosa.");
        }

        // =====================================================================
        // El turno de aviso
        // =====================================================================

        /// <summary>
        /// Con <c>AnnounceTurns = 1</c> la marca levantada en el turno N <b>sobrevive</b> al turno del
        /// jugador y prende en el N+1. El turno del aviso sale sin consumir y sin plantar nada.
        /// </summary>
        /// <remarks>
        /// Es la mitad que hace que la telegrafía exista: con 0 —el default— no hay yield entre el
        /// <c>Show</c> del telegraph y el <c>Clear</c> de este nodo, así que un aviso marcado y
        /// prendido en el mismo tick no se dibuja <b>ni un frame</b>. Y el reloj es la cuenta de
        /// activaciones del propio nodo, no <c>context.RoundIndex</c>: ahí un 0 constante (EditMode, o
        /// cualquier driver que no lo popule) dejaría la marca pendiente para siempre.
        /// </remarks>
        [Test]
        public void AMarkRaisedOnTurnN_SurvivesTheAnnounceTurn_AndLightsOnTurnNPlusOne()
        {
            var node = new AINode_IgniteArea
            {
                Definition = _fire,
                DurationRounds = 3,
                AnnounceTurns = 1,
            };
            // El único test que registra overlay: el dibujo ES el aviso, así que acá hay que poder
            // ver si se apagó. El resto del fixture corre sin servicio (ver remarks de la clase).
            var overlay = new SpyThreatOverlay();
            ServiceLocator.AddService<IThreatOverlayService>(overlay, ServiceScope.Global);
            Mark(MarkDamage, AttackKind.Environmental, _playerCoord, new GridCoord(3, 2));

            // Turno N: el aviso.
            var announce = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, announce,
                "El turno del aviso devolvió Failed y le cortó el Sequence al jefe: avisar no es fallar.");
            Assert.IsEmpty(Instances(),
                "Prendió en el turno del aviso: eso es el mismo tick de siempre y el overlay no llega " +
                "a dibujarse.");
            Assert.IsEmpty(_pipeline.Resolved,
                "Cobró en el turno del aviso. El golpe de la marca se paga cuando prende, no cuando " +
                "se anuncia.");
            Assert.IsTrue(_threat.HasPending(_boss),
                "El aviso se consumió igual. La marca ES el aviso: sin ella pendiente el overlay se " +
                "apaga y el jugador ve un turno del jefe en blanco seguido de un incendio sin causa.");
            CollectionAssert.Contains(new List<GridCoord>(_threat.GetPendingTiles(_boss)), _playerCoord,
                "El área pendiente cambió durante el turno del aviso.");

            // Turno N+1: la detonación.
            var ignite = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, ignite, "La detonación del turno siguiente falló.");
            Assert.IsFalse(_threat.HasPending(_boss),
                "La marca sigue pendiente después de detonar: el aviso quedaría pintado para siempre " +
                "y el paño se prendería de nuevo cada turno.");
            Assert.AreEqual(1, _pipeline.Resolved.Count,
                "El golpe de la marca no se cobró al prender, o se cobró dos veces.");
            CollectionAssert.AreEquivalent(new[] { _playerCoord, new GridCoord(3, 2) },
                new List<GridCoord>(Instances()[0].Coords),
                "Lo que prendió no es el área que se había avisado.");
        }

        // =====================================================================
        // Canales: dos avisos del mismo jefe a la vez
        // =====================================================================

        /// <summary>
        /// La derivación de la fuente. Sin canal devuelve el guid <b>tal cual</b>, que es lo que hace
        /// que los ocho jefes ya autorados sigan marcando donde marcaban; con canal devuelve algo
        /// distinto y estable, para que el que consume pueda resolver la misma key.
        /// </summary>
        [Test]
        public void SourceKey_IsTheBossGuidWithoutAChannel_AndSomethingStableWithOne()
        {
            const string channel = "pleno";

            Assert.AreEqual(_boss, AINode_TelegraphMark.SourceKey(_boss, null),
                "Un canal vacío dejó de significar 'la marca del propio jefe': todo lo ya autorado " +
                "pasaría a marcar en una fuente que nadie consume.");
            Assert.AreEqual(_boss, AINode_TelegraphMark.SourceKey(_boss, string.Empty),
                "String vacío y null tienen que dar lo mismo: en el Inspector un campo sin tocar es " +
                "el vacío, no el null.");
            Assert.AreNotEqual(_boss, AINode_TelegraphMark.SourceKey(_boss, channel),
                "El canal no separó nada: la marca cae en el guid pelado y se pisa con la principal.");
            Assert.AreEqual(AINode_TelegraphMark.SourceKey(_boss, channel),
                AINode_TelegraphMark.SourceKey(_boss, channel),
                "La derivación no es estable: el paso que consume resuelve una key distinta de la que " +
                "marcó y el aviso nunca detona.");
            Assert.AreNotEqual(AINode_TelegraphMark.SourceKey(_boss, channel),
                AINode_TelegraphMark.SourceKey(_boss, "otro"),
                "Dos canales distintos dieron la misma fuente: vuelven a pisarse entre ellos.");
            Assert.AreEqual(Guid.Empty, AINode_TelegraphMark.SourceKey(Guid.Empty, channel),
                "Un canal derivado de un guid vacío guarda un área que nadie puede consumir: sin " +
                "dueño, Mark tiene que seguir siendo no-op.");
        }

        /// <summary>
        /// Dos avisos del mismo jefe en el mismo turno, uno en su canal y otro en el guid pelado: cada
        /// uno se consume por separado y con su propio daño.
        /// </summary>
        /// <remarks>
        /// <c>IThreatenedAreaService</c> guarda <b>un</b> área por fuente y la <b>sobrescribe</b>, así
        /// que sin canal el segundo marcado del turno destruye al primero. Y no se arregla mergeando:
        /// el que detona consume por fuente y cobra el <c>Damage</c> de lo que consumió, así que dos
        /// áreas fundidas en una entrada se resolverían como un solo golpe con un solo número.
        /// </remarks>
        [Test]
        public void AChannelledMarkAndTheBossOwnMark_DoNotOverwriteEachOther()
        {
            const string channel = "pleno";
            var band = new[] { new GridCoord(6, 6), new GridCoord(7, 6) };
            var channelled = new[] { _playerCoord, new GridCoord(3, 2) };

            Mark(damage: 0, band);
            _threat.Mark(AINode_TelegraphMark.SourceKey(_boss, channel), channelled,
                MarkDamage, AttackKind.Environmental);

            Assert.IsTrue(_threat.HasPending(_boss),
                "La marca del canal se llevó puesta la del guid pelado: la segunda del turno pisó a " +
                "la primera, que es el bug que el canal existe para evitar.");
            Assert.IsTrue(_threat.HasPending(AINode_TelegraphMark.SourceKey(_boss, channel)),
                "La marca del canal no quedó pendiente en ninguna parte.");

            // Sólo el canal: la banda tiene que seguir esperando su propio tiempo de quema.
            var onChannel = Tick(new AINode_IgniteArea
            {
                Definition = _fire,
                DurationRounds = 3,
                ChannelId = channel,
            });

            Assert.AreEqual(AIResult.Succeeded, onChannel, "La ignición del canal falló.");
            Assert.IsTrue(_threat.HasPending(_boss),
                "La ignición del canal se comió la marca del guid pelado. Consume por la fuente que " +
                "resuelve: con la key equivocada prende el área del otro aviso, con la duración de éste.");
            CollectionAssert.AreEquivalent(channelled, new List<GridCoord>(Instances()[0].Coords),
                "La ignición del canal plantó algo que no es lo que su canal tenía avisado.");
            Assert.AreEqual(1, _pipeline.Resolved.Count,
                "El golpe salió del área equivocada: la marca del canal cobra y la banda no.");
            Assert.AreEqual(MarkDamage, _pipeline.Resolved[0].BaseDamage);

            // Y ahora la banda, por el guid pelado, sin canal.
            Ignite(durationRounds: 3);

            Assert.IsFalse(_threat.HasPending(_boss), "La banda no llegó a consumirse.");
            Assert.AreEqual(2, Instances().Count,
                "Los dos avisos tenían que terminar en dos fuegos: uno por área marcada.");
            AssertNoSameDefinitionOverlap();
        }

        [Test]
        public void GroundBurningUnderAnotherSubstance_IsStillCountedAsBurning()
        {
            _ice = NewDefinition("TILE_TEST_ICE");

            // Hielo y fuego sobre la misma casilla: legítimo, son dos efectos. Lo que no puede pasar
            // es que el hielo esconda al fuego cuando se pregunta si esa casilla ya arde.
            Mark(damage: 0, new GridCoord(5, 4));
            Ignite(_ice, durationRounds: 3);
            Mark(damage: 0, new GridCoord(4, 4), new GridCoord(5, 4));
            Ignite(durationRounds: 3);

            // La banda nueva comparte (5,4) y deja (4,4) afuera, así que la vieja se conserva y el
            // camino que corre es el del skip.
            Mark(damage: 0, new GridCoord(5, 4), new GridCoord(6, 4));
            Ignite(durationRounds: 3);

            // El invariante alcanza para pinchar el bug: preguntando con TryGetTileAt esto pasaba o
            // fallaba según qué instancia devolviera primero el diccionario —orden que nadie
            // garantiza—, y con el hielo primero la casilla contestaba "libre" y cobraba doble.
            AssertNoSameDefinitionOverlap();
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void Mark(int damage, params GridCoord[] coords) =>
            Mark(damage, AttackKind.Environmental, coords);

        private void Mark(int damage, AttackKind kind, params GridCoord[] coords) =>
            _threat.Mark(_boss, coords, damage, kind);

        private AIResult Ignite(int durationRounds) => Ignite(_fire, durationRounds);

        /// <summary>
        /// Ignición con todo lo opcional en su default, o sea el nodo tal como lo tienen serializado
        /// los <c>ED_Boss_*.asset</c> que nadie tocó.
        /// </summary>
        private AIResult Ignite(SpecialTileDefinitionSO definition, int durationRounds) =>
            Tick(new AINode_IgniteArea
            {
                Definition = definition,
                DurationRounds = durationRounds,
            });

        /// <summary>
        /// Ignición con el relevo prendido: lo que autora el Croupier en sus tres pasos que prenden
        /// (ver <c>CroupierPhaseWiringTests.Ignitions_RelayTheBandTheyReplace</c>). Apagado —el
        /// default— es lo que cubre
        /// <see cref="RetireFullyReplaced_IsOffByDefault_SoNobodyElsesFireDisappears"/>.
        /// </summary>
        private AIResult IgniteRelaying(int durationRounds) =>
            Tick(new AINode_IgniteArea
            {
                Definition = _fire,
                DurationRounds = durationRounds,
                RetireFullyReplaced = true,
            });

        private AIResult Tick(AINode_IgniteArea node) => node.Tick(NewContext());

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
        };

        private List<SpecialTileInfo> Instances() => new List<SpecialTileInfo>(_tiles.ActiveInstances());

        /// <summary>
        /// Cierre de ronda. Es lo único que baja las duraciones, y sólo cuenta si el índice es el
        /// último visto + 1: <c>SpecialTileService</c> distingue el wrap real del re-broadcast de la
        /// cola (el Append de refuerzos re-dispara el evento con el MISMO round). Por eso las rondas
        /// van consecutivas y desde 1.
        /// </summary>
        private static void WrapRound(int roundIndex) =>
            EventManager.Trigger(EventName.OnTurnQueueBuilt, new List<Guid>().AsReadOnly(), roundIndex);

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

        private SpecialTileInfo InstanceOwnedBy(Guid owner)
        {
            foreach (var instance in Instances())
                if (instance.OwnerGuid == owner) return instance;

            Assert.Fail("No quedó ninguna instancia de ese dueño: se apagó o se encogió por una " +
                        "ignición que no era suya.");
            return default;
        }

        /// <summary>Busca sin fallar: a diferencia de <see cref="InstanceById"/>, acá "no está" es
        /// justo lo que un test de regresión necesita poder afirmar.</summary>
        private bool TryFindInstanceCovering(GridCoord coord, out SpecialTileInfo covering)
        {
            foreach (var instance in Instances())
            {
                foreach (var tile in instance.Coords)
                {
                    if (!tile.Equals(coord)) continue;
                    covering = instance;
                    return true;
                }
            }
            covering = default;
            return false;
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

        /// <summary>
        /// El overlay no se inspecciona: el test que lo registra sólo necesita que el servicio
        /// exista, porque el nodo lo resuelve para apagar el dibujo y sin servicio ese paso no corre.
        /// </summary>
        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                Color? tint = null) { }
            public void Clear(Guid sourceGuid) { }
            public void ClearAll() { }
        }

    }
}
