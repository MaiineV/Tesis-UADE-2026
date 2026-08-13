using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests del VFX opcional de <see cref="HazardDefinitionSO.TriggerVfxPrefab"/>: hasta ahora el
    /// único visual de un hazard era el quad tinteado, y pisar una estela de hielo no tenía payoff.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El seam es el <see cref="GameObject"/> spawneado: <see cref="HazardService"/> es un POCO sin
    /// escena propia, así que el efecto observable de "hay VFX" es que aparece un clon en la escena
    /// abierta. Los tests le pasan un marker en vez de un prefab de partículas real —
    /// <c>Object.Instantiate</c> clona igual un objeto de escena, y así el suite no depende de que
    /// <c>VFX_IceBurst.prefab</c> exista ni de cómo esté autorado.
    /// </para>
    /// <para>
    /// <b>Contrato central: sin prefab, nada cambia.</b> Un hazard viejo (todos los <c>.asset</c>
    /// autorados antes de que el campo existiera) tiene que seguir cobrando y disparando
    /// <c>OnHazardTriggered</c> exactamente igual.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class HazardTriggerVfxTests
    {
        private const string MarkerName = "VFX_TestMarker";

        /// <summary>El nombre que <see cref="HazardService"/> le pone al clon.</summary>
        private const string SpawnedName = MarkerName + " (hazard)";

        private GridManager _grid;
        private HazardService _hazard;
        private StubMovementService _movement;
        private GameObject _markerPrefab;
        private Guid _walkerGuid;
        private List<Guid> _triggeredEvents;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid);

            _walkerGuid = Guid.NewGuid();
            _grid.Register(_walkerGuid, new GridCoord(4, 4));

            // Registrado antes del service: la suscripción a OnEntityMoved se resuelve en el primer
            // Activate con tiles.
            _movement = new StubMovementService();
            ServiceLocator.AddService<IMovementService>(_movement);

            _hazard = new HazardService();
            _hazard.Register();

            _markerPrefab = new GameObject(MarkerName);

            _triggeredEvents = new List<Guid>();
            EventManager.Subscribe(EventName.OnHazardTriggered, args => _triggeredEvents.Add((Guid)args[0]));
        }

        [TearDown]
        public void TearDown()
        {
            DrainSpawned();

            if (_markerPrefab != null) Object.DestroyImmediate(_markerPrefab);
            _markerPrefab = null;

            // Mismo cleanup que HazardServiceTests: el overlay crea un GameObject y un Material por
            // tint que, si sobreviven, contaminan los tests siguientes.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // Null path — el comportamiento histórico
        // ======================================================================

        [Test]
        public void NoTriggerVfxPrefab_StillTriggers_AndSpawnsNothing()
        {
            var def = CreateDefinition(HazardTriggerMode.OnTurnEndInTile);
            Assert.IsNull(def.TriggerVfxPrefab, "El default del campo tiene que ser 'sin VFX'.");
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 2) });
            _grid.Move(_walkerGuid, new GridCoord(2, 2));

            EventManager.Trigger(EventName.OnTurnFinished, _walkerGuid);

            CollectionAssert.Contains(_triggeredEvents, instanceId,
                "Sin prefab de VFX el hazard tiene que cobrar igual — el evento es el hook del stun.");
            Assert.AreEqual(0, DrainSpawned(), "Sin prefab no debería spawnearse nada.");
        }

        // ======================================================================
        // Con prefab
        // ======================================================================

        [Test]
        public void TriggerVfxPrefab_OnTurnEndInTile_SpawnsOneOverTheTile()
        {
            var def = CreateDefinition(HazardTriggerMode.OnTurnEndInTile, vfx: _markerPrefab);
            _hazard.Activate(def, new[] { new GridCoord(2, 2) });
            _grid.Move(_walkerGuid, new GridCoord(2, 2));

            EventManager.Trigger(EventName.OnTurnFinished, _walkerGuid);

            var spawned = GameObject.Find(SpawnedName);
            Assert.IsNotNull(spawned, "Con prefab asignado, disparar el hazard debería instanciarlo.");

            var expected = _grid.GridToWorld(new GridCoord(2, 2)) + Vector3.up * def.TriggerVfxYOffset;
            Assert.AreEqual(expected.x, spawned.transform.position.x, 0.001f);
            Assert.AreEqual(expected.y, spawned.transform.position.y, 0.001f,
                "El burst se levanta del piso lo mismo que el quad del overlay.");
            Assert.AreEqual(expected.z, spawned.transform.position.z, 0.001f);
        }

        [Test]
        public void TriggerVfxPrefab_OnEnter_SpawnsOnThePathTileThatFired_NotOnTheDestination()
        {
            // La trampa está en el medio del camino: el VFX tiene que salir de la casilla pisada.
            var def = CreateDefinition(HazardTriggerMode.OnEnter, vfx: _markerPrefab);
            _hazard.Activate(def, new[] { new GridCoord(2, 4) });

            _movement.RaiseMoved(_walkerGuid, new GridCoord(4, 4), new GridCoord(0, 4), new[]
            {
                new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4),
                new GridCoord(1, 4), new GridCoord(0, 4),
            });

            var spawned = GameObject.Find(SpawnedName);
            Assert.IsNotNull(spawned, "Cruzar la estela debería spawnear el burst.");
            var expected = _grid.GridToWorld(new GridCoord(2, 4)) + Vector3.up * def.TriggerVfxYOffset;
            Assert.AreEqual(expected.x, spawned.transform.position.x, 0.001f,
                "El VFX va sobre la casilla que disparó, no sobre el destino del movimiento.");
        }

        [Test]
        public void TriggerVfxPrefab_OneSpawnPerTrigger_NotOnePerTileInTheArea()
        {
            // Dos casillas de estela, una sola pisada: un burst. Una estela de 3 casillas no puede
            // encender el efecto tres veces por pisar una.
            var def = CreateDefinition(HazardTriggerMode.OnEnter, vfx: _markerPrefab, consumeOnTrigger: true);
            _hazard.Activate(def, new[] { new GridCoord(3, 4), new GridCoord(0, 0) });

            _movement.RaiseMoved(_walkerGuid, new GridCoord(4, 4), new GridCoord(3, 4), new[]
            {
                new GridCoord(4, 4), new GridCoord(3, 4),
            });

            Assert.AreEqual(1, DrainSpawned(), "Un trigger = un VFX.");
        }

        [Test]
        public void ConsumedTile_DoesNotSpawnAgain()
        {
            var def = CreateDefinition(HazardTriggerMode.OnEnter, vfx: _markerPrefab, consumeOnTrigger: true);
            _hazard.Activate(def, new[] { new GridCoord(3, 4), new GridCoord(0, 0) });

            _movement.RaiseMoved(_walkerGuid, new GridCoord(4, 4), new GridCoord(3, 4), new[]
            {
                new GridCoord(4, 4), new GridCoord(3, 4),
            });
            Assert.AreEqual(1, DrainSpawned());

            // Volver a pisar la casilla derretida.
            _movement.RaiseMoved(_walkerGuid, new GridCoord(4, 4), new GridCoord(3, 4), new[]
            {
                new GridCoord(4, 4), new GridCoord(3, 4),
            });

            Assert.AreEqual(0, DrainSpawned(),
                "La casilla ya consumida no vuelve a disparar, así que tampoco vuelve a spawnear VFX.");
        }

        [Test]
        public void NoGridManager_SkipsTheVfx_ButStillTriggers()
        {
            // El hielo se activa y se pisa igual sin IGridManager (el path lo trae el evento de
            // movimiento): el hazard tiene que cobrar y solo perderse el visual.
            var def = CreateDefinition(HazardTriggerMode.OnEnter, vfx: _markerPrefab);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(3, 4) });

            ServiceLocator.RemoveService<IGridManager>();
            LogAssert.Expect(LogType.Warning, new Regex("IGridManager no registrado"));

            _movement.RaiseMoved(_walkerGuid, new GridCoord(4, 4), new GridCoord(3, 4), new[]
            {
                new GridCoord(4, 4), new GridCoord(3, 4),
            });

            CollectionAssert.Contains(_triggeredEvents, instanceId,
                "Sin grilla el hazard sigue cobrando: el VFX es decoración, no el efecto.");
            Assert.AreEqual(0, DrainSpawned(), "Sin posición de mundo no hay dónde poner el burst.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        /// <summary>
        /// Cuenta los clones vivos y los destruye. Se cuenta destruyendo (en vez de con una búsqueda
        /// por tipo) porque <see cref="GameObject.Find"/> devolvería siempre el primero: sacarlo del
        /// camino es lo que hace avanzar el conteo, y de paso deja la escena limpia para el test que
        /// sigue.
        /// </summary>
        private static int DrainSpawned()
        {
            int count = 0;
            while (count < 16)
            {
                var found = GameObject.Find(SpawnedName);
                if (found == null) break;
                Object.DestroyImmediate(found);
                count++;
            }
            return count;
        }

        private static HazardDefinitionSO CreateDefinition(
            HazardTriggerMode trigger,
            GameObject vfx = null,
            bool consumeOnTrigger = false)
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Trigger = trigger;
            def.Damage = 0; // El hielo no hace daño: así el fixture no necesita IDamagePipeline.
            def.Kind = AttackKind.Environmental;
            def.ConsumeOnTrigger = consumeOnTrigger;
            def.TriggerVfxPrefab = vfx;
            def.SourceId = Guid.NewGuid().ToString();
            return def;
        }

        private sealed class StubMovementService : IMovementService
        {
            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => new List<GridCoord>();

            public bool Move(Guid entity, GridCoord destination) => false;

            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

            public void RaiseMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
                => OnEntityMoved?.Invoke(entity, from, to, path);
        }
    }
}
