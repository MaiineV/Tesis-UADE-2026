using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles.Visuals;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Pooling de los visuales de casilla especial: la ignición de un jefe enciende ~112
    /// casillas en un frame y las bandas la repiten cada dos rondas, así que del segundo
    /// encendido en adelante no puede haber ni un <c>Instantiate</c>.
    /// </summary>
    /// <remarks>
    /// Mismo seam que <c>HazardPersistentVfxTests</c>: el efecto observable es el clon en la
    /// escena, contado por prefijo de nombre, y el "prefab" es un marker armado a mano para no
    /// depender de cómo esté autorado <c>VFX_Fire.prefab</c>. Ojo con el conteo: los clones
    /// estacionados están apagados, y <c>FindObjectsByType</c> los excluye si no se le pide
    /// explícitamente <see cref="FindObjectsInactive.Include"/>.
    /// </remarks>
    [TestFixture]
    public class SpecialTileVisualPoolTests
    {
        private const string MarkerName = "VFX_TileVisualMarker";
        private const string FixturePoolRootName = "SpecialTileVisualPoolFixture";

        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private SpecialTileService _svc;

        private GameObject _markerPrefab;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);

            // El marker lleva los dos componentes opcionales: sin ellos el pool no podría
            // demostrar que re-bindea, que es la mitad del contrato.
            _markerPrefab = new GameObject(MarkerName);
            _markerPrefab.AddComponent<SpecialTileVisualBinding>();
            _markerPrefab.AddComponent<SpecialTileTooltipInfo>();

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;

            DestroyClones();
            DestroyPoolRoots();

            if (_markerPrefab != null) Object.DestroyImmediate(_markerPrefab);
            _markerPrefab = null;

            foreach (var asset in _createdAssets)
                if (asset != null) Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // Lo que motiva el pool: la segunda ignición no asigna
        // ======================================================================

        [Test]
        public void ReplacingAnExpiredTile_ReusesItsClonesInsteadOfInstantiatingAgain()
        {
            // Arrange
            var def = MakeVisualDefinition();
            var tiles = Block(2, 2, 3, 3);
            var first = _svc.Place(def, tiles, Owned());
            Assume.That(CloneCount(), Is.EqualTo(tiles.Length));

            // Act
            _svc.Remove(first);
            _svc.Place(def, tiles, Owned());

            // Assert
            Assert.AreEqual(tiles.Length, CloneCount(),
                "El segundo encendido volvió a instanciar: el jefe sigue pagando el hitch de un frame cada vez que prende sus casillas.");
            Assert.AreEqual(tiles.Length, ActiveCloneCount(),
                "El segundo encendido dejó casillas sin visual: la trampa queda invisible.");
        }

        [Test]
        public void ExpiringATile_ParksItsCloneSwitchedOff()
        {
            // Arrange
            var def = MakeVisualDefinition();
            var id = _svc.Place(def, Block(2, 2, 2, 1), Owned());
            Assume.That(ActiveCloneCount(), Is.EqualTo(2));

            // Act
            _svc.Remove(id);

            // Assert
            Assert.AreEqual(2, CloneCount(),
                "El clon se destruyó en vez de estacionarse: la próxima ignición vuelve a pagar el Instantiate.");
            Assert.AreEqual(0, ActiveCloneCount(),
                "Un clon estacionado quedó encendido: se ve arder una casilla que ya se apagó.");
        }

        // ======================================================================
        // Lo que un clon reciclado no puede arrastrar
        // ======================================================================

        [Test]
        public void AReusedClone_IsRenamedForItsNewTile()
        {
            // Arrange
            var def = MakeVisualDefinition();
            var id = _svc.Place(def, One(2, 2), Owned());
            Assume.That(CloneNamedFor(new GridCoord(2, 2)), Is.Not.Null);

            // Act
            _svc.Remove(id);
            _svc.Place(def, One(4, 4), Owned());

            // Assert
            Assert.IsNotNull(CloneNamedFor(new GridCoord(4, 4)),
                "El clon reciclado no quedó firmado con su casilla nueva: debuguear cuál arde es imposible.");
            Assert.IsNull(CloneNamedFor(new GridCoord(2, 2)),
                "El clon reciclado sigue firmado con la casilla vieja: la jerarquía miente sobre qué está encendido.");
        }

        [Test]
        public void AReusedClone_StopsAnsweringForTheInstanceThatExpired()
        {
            // Arrange
            var def = MakeVisualDefinition();
            var expiring = _svc.Place(def, One(2, 2), Owned());

            var binding = SingleActiveClone().GetComponent<SpecialTileVisualBinding>();
            binding.OnExpiring ??= new UnityEvent();

            int fired = 0;
            binding.OnExpiring.AddListener(() => fired++);

            // Sin esta sanity el resto del test pasaría en verde con el visual desconectado
            // desde el principio.
            EventManager.Trigger(EventName.OnSpecialTileExpired, expiring);
            Assume.That(fired, Is.EqualTo(1));

            // Act — estacionar no llama OnDestroy, así que el binding tiene que soltarse solo.
            _svc.Remove(expiring);

            // Assert
            Assert.AreEqual(1, fired,
                "El clon estacionado sigue reaccionando a eventos de una casilla apagada: el arte se dispara en el aire.");

            var reborn = _svc.Place(def, One(4, 4), Owned());
            Assume.That(SingleActiveClone(), Is.Not.Null);

            EventManager.Trigger(EventName.OnSpecialTileExpired, expiring);
            Assert.AreEqual(1, fired,
                "El clon reciclado sigue atado a la instancia vieja: se apaga cuando expira una casilla que no es la suya.");

            EventManager.Trigger(EventName.OnSpecialTileExpired, reborn);
            Assert.AreEqual(2, fired,
                "El clon reciclado no se re-bindeó: la casilla nueva nunca va a poder despedirse.");
        }

        [Test]
        public void AReusedClone_ReportsTheNewTilesDurationInItsTooltip()
        {
            // Arrange — la definición no tiene ningún otro número, así que el único dígito del
            // tooltip es la duración.
            var def = MakeVisualDefinition();
            var shortLived = _svc.Place(def, One(2, 2), Owned(durationRounds: 3));
            Assume.That(TooltipOfSingleActiveClone(), Does.Contain("3"));

            // Act
            _svc.Remove(shortLived);
            _svc.Place(def, One(4, 4), Owned(durationRounds: 7));

            // Assert
            var tooltip = TooltipOfSingleActiveClone();
            Assert.IsTrue(tooltip.Contains("7"),
                "El tooltip del clon reciclado no tomó la casilla nueva: el jugador lee datos de otra casilla.");
            Assert.IsFalse(tooltip.Contains("3"),
                "El tooltip del clon reciclado sigue anunciando la duración de la casilla anterior.");
        }

        [Test]
        public void MovingAnInstance_CarriesItsClonesToTheNewCoords()
        {
            // Arrange
            var def = MakeVisualDefinition();
            var id = _svc.Place(def, Block(2, 2, 2, 1), Owned());
            Assume.That(CloneCount(), Is.EqualTo(2));

            // Act
            _svc.MoveInstance(id, new[] { new GridCoord(6, 6), new GridCoord(7, 6) });

            // Assert
            Assert.AreEqual(2, CloneCount(),
                "Mover la instancia instanció visuales nuevos y tiró los viejos.");
            Assert.AreEqual(2, ActiveCloneCount(),
                "Mover la instancia dejó casillas sin visual encendido.");
            Assert.IsNotNull(CloneNamedFor(new GridCoord(6, 6)),
                "El visual no siguió a la instancia: quedó firmado con la casilla de la que se fue.");
            Assert.IsNull(CloneNamedFor(new GridCoord(2, 2)),
                "Quedó un visual encendido en la casilla vieja.");
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        [Test]
        public void CombatEnd_KeepsThePermanentTilesVisualLit()
        {
            // Arrange — permanente (duración 0) + temporal.
            var def = MakeVisualDefinition();
            _svc.Place(def, One(2, 2), Owned());
            _svc.Place(def, One(4, 4), Owned(durationRounds: 3));
            Assume.That(ActiveCloneCount(), Is.EqualTo(2));

            // Act
            EventManager.Trigger(EventName.OnCombatEnd);

            // Assert
            Assert.AreEqual(1, ActiveCloneCount(),
                "El fin del combate se llevó el visual de la casilla permanente: la sala queda con una trampa invisible en exploración.");
            Assert.AreEqual(2, CloneCount(),
                "El clon de la casilla temporal se destruyó en vez de estacionarse.");
        }

        [Test]
        public void DisposingTheService_LeavesNoCloneInTheScene()
        {
            // Arrange — un ciclo completo, para que haya clones alquilados Y estacionados.
            var def = MakeVisualDefinition();
            var id = _svc.Place(def, Block(2, 2, 2, 2), Owned());
            _svc.Remove(id);
            _svc.Place(def, One(6, 6), Owned());
            Assume.That(CloneCount(), Is.GreaterThan(0));

            // Act
            _svc.Dispose();
            _svc = null;

            // Assert
            Assert.AreEqual(0, CloneCount(),
                "Los visuales sobrevivieron al servicio: cada sala deja basura acumulada en la jerarquía.");
        }

        // ======================================================================
        // El pool a solas: tope e higiene de referencias muertas
        // ======================================================================

        [Test]
        public void Rent_AttachesTheHoverTooltip_WithColliderAndTrigger()
        {
            // Arrange — un prefab pelado, sin tooltip ni collider: el caso real de todos los
            // prefabs de casilla del proyecto (VFX_Fire no trae ninguno de los dos).
            var bare = new GameObject("VFX_BareTile");
            try
            {
                using (var pool = new SpecialTileVisualPool(FixturePoolRootName))
                {
                    // Act
                    var visual = pool.Rent(bare, Vector3.zero);

                    // Assert — regla del spec de tooltips: toda casilla con efecto de juego tiene
                    // tooltip. El trigger sin collider sería un Update por frame que nunca acierta.
                    Assert.IsNotNull(visual.Tooltip,
                        "El clon quedó sin SpecialTileTooltipInfo: no hay qué mostrar en el hover.");
                    var trigger = visual.Go.GetComponent<Rollgeon.UI.Tooltips.WorldTooltipTrigger>();
                    Assert.IsNotNull(trigger, "El clon quedó sin trigger de hover.");
                    Assert.AreEqual(Rollgeon.UI.Tooltips.WorldTooltipMode.Hover, trigger.Mode);
                    Assert.AreEqual(Rollgeon.UI.Tooltips.TooltipPlacementMode.ScreenTopRight,
                        trigger.Placement);
                    Assert.IsNotNull(trigger.ContentProvider,
                        "El clon quedó sin header estructurado: la casilla volvería al párrafo " +
                        "plano con los números incrustados.");
                    Assert.IsNotNull(trigger.CardsProvider,
                        "El clon quedó sin tarjetas de números: los precios de la casilla no " +
                        "tienen dónde mostrarse como dato.");
                    Assert.IsNotNull(visual.Go.GetComponentInChildren<Collider>(true),
                        "Sin collider el raycast del hover no puede tocar la casilla.");
                }
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void ParkingPastTheCap_DestroysTheSurplusInsteadOfHoardingItForever()
        {
            // Arrange — tope chico a propósito: el default (128) exigiría 129 GameObjects.
            using (var pool = new SpecialTileVisualPool(FixturePoolRootName, maxFreePerPrefab: 2))
            {
                var rented = new List<PooledTileVisual>();
                for (int i = 0; i < 5; i++) rented.Add(pool.Rent(_markerPrefab, Vector3.zero));
                Assume.That(CloneCount(), Is.EqualTo(5));

                // Act
                foreach (var entry in rented) pool.Park(entry);

                // Assert
                Assert.AreEqual(2, pool.FreeCount,
                    "La free list crece sin techo, y en producción nadie llama a Dispose: el proceso nunca la vacía.");
                Assert.AreEqual(2, CloneCount(),
                    "Los clones que no entraron en el tope quedaron vivos en la escena en vez de destruirse.");
            }
        }

        [Test]
        public void RentAfterTheSceneTookThePoolRoot_HandsBackALiveClone()
        {
            using (var pool = new SpecialTileVisualPool(FixturePoolRootName))
            {
                // Arrange
                var parked = pool.Rent(_markerPrefab, Vector3.zero);
                pool.Park(parked);
                Assume.That(pool.FreeCount, Is.EqualTo(1));

                // Un cambio de escena se lleva el root y con él todo clon estacionado; lo que
                // queda en el pool son referencias en fake-null.
                Object.DestroyImmediate(GameObject.Find(FixturePoolRootName));
                Assume.That(parked.Go == null, Is.True);

                // Act
                var fresh = pool.Rent(_markerPrefab, Vector3.zero);

                // Assert
                Assert.IsFalse(fresh.Go == null,
                    "El pool entregó un clon que murió con la escena: la casilla queda invisible hasta que se cambie de sala.");
                Assert.AreEqual(0, pool.FreeCount,
                    "El pool sigue contando como disponibles clones que ya no existen.");
            }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private SpecialTileDefinitionSO MakeVisualDefinition()
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;

            // Sin dígitos en ningún otro campo: el tooltip solo puede aportar la duración.
            def.TileId = "TILE_POOLED_VISUAL";
            def.DisplayName = "Casilla pooleada";
            def.TileType = SpecialTileType.Fire;
            def.Triggers = TileTrigger.OnEnter;
            def.Category = TileEffectCategory.Damage;
            def.Affinity = TileAffinity.All;
            def.EnterDamage = 0;
            def.TurnStartDamage = 0;
            def.HealAmount = 0;
            def.DefaultDurationRounds = 0; // 0 = permanente salvo override del Place.
            def.VisualPrefab = _markerPrefab;

            _createdAssets.Add(def);
            return def;
        }

        private TilePlacementOptions Owned(int durationRounds = 0)
            => new TilePlacementOptions { Owner = _player, DurationRounds = durationRounds };

        private static GridCoord[] One(int x, int y) => new[] { new GridCoord(x, y) };

        private static GridCoord[] Block(int x, int y, int width, int height)
        {
            var coords = new List<GridCoord>(width * height);
            for (int dx = 0; dx < width; dx++)
                for (int dy = 0; dy < height; dy++)
                    coords.Add(new GridCoord(x + dx, y + dy));
            return coords.ToArray();
        }

        /// <summary>Include: los estacionados están apagados y el overload corto los saltearía.</summary>
        private static IEnumerable<GameObject> Clones()
            => Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(go => go != null
                             && go.name.StartsWith(MarkerName)
                             && go.name != MarkerName);

        private static int CloneCount() => Clones().Count();

        private static int ActiveCloneCount() => Clones().Count(go => go.activeInHierarchy);

        /// <summary>El servicio le firma la casilla al nombre: se busca por ese sufijo.</summary>
        private static GameObject CloneNamedFor(GridCoord coord)
            => Clones().FirstOrDefault(go => go.name.Contains($"(tile {coord})"));

        private static GameObject SingleActiveClone()
        {
            var active = Clones().Where(go => go.activeInHierarchy).ToList();
            Assert.AreEqual(1, active.Count, "Setup: el test espera exactamente un visual encendido.");
            return active[0];
        }

        private static string TooltipOfSingleActiveClone()
            => SingleActiveClone().GetComponent<SpecialTileTooltipInfo>().BuildTooltip();

        private static void DestroyClones()
        {
            foreach (var go in Clones().ToList()) Object.DestroyImmediate(go);
        }

        private static void DestroyPoolRoots()
        {
            foreach (var name in new[] { SpecialTileVisualPool.DefaultRootName, FixturePoolRootName })
            {
                var root = GameObject.Find(name);
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
