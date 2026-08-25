using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Handoff.Tests
{
    /// <summary>
    /// Dónde arranca el jefe en su sala. La matemática vive en
    /// <see cref="BossEntrySpawnResolver"/> y tiene sus propios tests; acá se fija que el resolver
    /// de spawn la use para los jefes y <b>sólo</b> para ellos.
    /// </summary>
    [TestFixture]
    public class DefaultEnemySpawnResolverBossPlacementTests
    {
        /// <summary>La celda autorada del layout, y el centro exacto de la sala de prueba: es la
        /// que el jefe tiene que dejar de usar.</summary>
        private static readonly GridCoord AuthoredCell = new GridCoord(5, 5);

        private readonly List<UnityEngine.Object> _created = new();
        private InMemoryEntityRegistry _registry;
        private AttributesManager _attributes;
        private GridManager _grid;
        private RoomLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _registry = new InMemoryEntityRegistry();
            _attributes = new AttributesManager();
            _grid = new GridManager();

            var go = new GameObject("TestRoom");
            _created.Add(go);

            _layout = go.AddComponent<RoomLayout>();
            _layout.TileSize = 1f;
            _layout.NavGraph = NavGraph.Rect(11, 11);
            _grid.LoadRoom(_layout.NavGraph, _layout.GetOrigin(), _layout.TileSize);

            // Sin IDungeonService registrado no hay puerta declarada, así que la entrada sale del
            // PlayerSpawnPoint — el mismo camino que el arranque directo por bootstrap.
            _layout.PlayerSpawnPoint = Point("PlayerSpawn", new GridCoord(5, 1));
            // Dos puntos y no uno: una sala Combat puede plantar más de un enemigo, y con un solo
            // punto los índices colapsan en la misma casilla — Register desaloja al anterior.
            _layout.EnemySpawnPoints = new List<Transform>
            {
                Point("EnemySpawn0", AuthoredCell),
                Point("EnemySpawn1", new GridCoord(8, 8)),
            };
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private Transform Point(string name, GridCoord coord)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.transform.SetParent(_layout.transform);
            go.transform.position = _grid.GridToWorld(coord);
            return go.transform;
        }

        private EnemyDataSO CreateEnemy(string name, int hp = 20)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            enemy.EntityId = $"enemy.{name.ToLower()}";
            enemy.BaseHP = hp;
            enemy.BaseSpeed = 4;
            enemy.MaxEnergy = 3;
            _created.Add(enemy);
            return enemy;
        }

        private EnemyPoolSO CreatePool(params EnemyDataSO[] enemies)
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            foreach (var enemy in enemies) pool.Entries.Add(new WeightedEntry<EnemyDataSO>(enemy, 1f));
            _created.Add(pool);
            return pool;
        }

        private RoomInstance CreateRoom(RoomType type, EnemyPoolSO pool, EnemyDataSO boss = null)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "test_room";
            room.DisplayName = "Test Room";
            room.Type = type;
            room.EnemyPool = pool;
            _created.Add(room);

            return new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                State = RoomState.Uncleared,
                SpawnedPrefab = _layout.gameObject,
                Boss = boss,
            };
        }

        private DefaultEnemySpawnResolver Resolver() =>
            new DefaultEnemySpawnResolver(_registry, _attributes, grid: _grid);

        [Test]
        public void ABoss_StartsAwayFromTheEntry_NotOnItsAuthoredCell()
        {
            var boss = CreateEnemy("Croupier", hp: 200);
            var instance = CreateRoom(RoomType.Boss, CreatePool(CreateEnemy("Filler")), boss);

            var result = Resolver().Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count, "La sala de jefe tiene que spawnear exactamente uno.");
            Assert.IsTrue(_grid.TryGetPosition(result[0].id, out var coord));

            Assert.AreNotEqual(AuthoredCell, coord,
                "El jefe volvió a arrancar en su celda autorada, que es el centro exacto de la sala.");
            Assert.Greater(coord.Manhattan(new GridCoord(5, 1)), 3,
                "Arrancó pegado a la entrada: la apertura a distancia es el punto del cambio.");
        }

        /// <summary>El camino nuevo está cerrado a las salas de jefe. Un enemigo común tiene que
        /// seguir saliendo de su spawn point autorado, sin una línea de diferencia.</summary>
        [Test]
        public void ARegularEnemy_StillSpawnsOnItsAuthoredCell()
        {
            var instance = CreateRoom(RoomType.Combat, CreatePool(CreateEnemy("Goblin")));

            var result = Resolver().Resolve(instance, new System.Random(42));

            Assert.IsNotEmpty(result);
            Assert.IsTrue(_grid.TryGetPosition(result[0].id, out var coord));
            Assert.AreEqual(AuthoredCell, coord,
                "Un enemigo no-jefe cambió de casilla: el spawn de todo el juego se movió con él.");
        }

        /// <summary>Sin layout no hay ni entrada ni sala que medir: el jefe se queda con el
        /// fallback de siempre en vez de quedar sin casilla.</summary>
        [Test]
        public void WithoutALayout_TheBossFallsBackToTheAuthoredPath()
        {
            var boss = CreateEnemy("Croupier", hp: 200);
            var instance = CreateRoom(RoomType.Boss, CreatePool(CreateEnemy("Filler")), boss);
            instance.SpawnedPrefab = null;

            var result = Resolver().Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count, "El jefe tiene que spawnear igual.");
            Assert.IsTrue(_grid.TryGetPosition(result[0].id, out _),
                "Quedó sin casilla en el grid.");
        }

        /// <summary>Register desaloja al ocupante previo de la casilla: si el jefe cayera encima
        /// del jugador, lo sacaría del grid antes de que empiece la pelea.</summary>
        [Test]
        public void TheBoss_NeverLandsOnThePlayersTile()
        {
            var playerGuid = Guid.NewGuid();
            var playerCoord = new GridCoord(5, 1);
            _grid.Register(playerGuid, playerCoord);

            var boss = CreateEnemy("Croupier", hp: 200);
            var instance = CreateRoom(RoomType.Boss, CreatePool(CreateEnemy("Filler")), boss);

            Resolver().Resolve(instance, new System.Random(42));

            Assert.IsTrue(_grid.TryGetPosition(playerGuid, out var stillThere),
                "El jugador quedó desalojado del grid por el spawn del jefe.");
            Assert.AreEqual(playerCoord, stillThere);
        }
    }
}
