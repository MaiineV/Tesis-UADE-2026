using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities;
using Rollgeon.Entities.Portraits;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Run;
using UnityEngine;

namespace Rollgeon.Combat.Handoff.Tests
{
    [TestFixture]
    public class DefaultEnemySpawnResolverTests
    {
        private InMemoryEntityRegistry _registry;
        private AttributesManager _attributes;
        private DefaultEnemySpawnResolver _resolver;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            _registry = new InMemoryEntityRegistry();
            _attributes = new AttributesManager();
            _resolver = new DefaultEnemySpawnResolver(_registry, _attributes);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            foreach (var obj in _createdObjects)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private RoomInstance CreateInstance(EnemyPoolSO pool, RoomType type = RoomType.Combat,
            RoomState state = RoomState.Uncleared)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "test_room";
            room.DisplayName = "Test Room";
            room.Type = type;
            room.EnemyPool = pool;
            _createdObjects.Add(room);

            return new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                State = state
            };
        }

        private EnemyPoolSO CreatePool(params EnemyDataSO[] enemies)
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            foreach (var enemy in enemies)
            {
                pool.Entries.Add(new WeightedEntry<EnemyDataSO>(enemy, 1f));
            }
            _createdObjects.Add(pool);
            return pool;
        }

        private EnemyDataSO CreateEnemy(string name, int hp = 20)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name = name;
            enemy.EntityId = $"enemy.{name.ToLower()}";
            enemy.BaseHP = hp;
            enemy.BaseSpeed = 4;
            enemy.MaxEnergy = 3;
            _createdObjects.Add(enemy);
            return enemy;
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void Resolve_NullInstance_ReturnsEmptyList()
        {
            var result = _resolver.Resolve(null, new System.Random(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Resolve_ClearedInstance_ReturnsEmptyList()
        {
            var pool = CreatePool(CreateEnemy("Goblin"));
            var instance = CreateInstance(pool, state: RoomState.Cleared);

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(0, result.Count,
                "Salas Cleared no deben re-spawnear enemigos.");
        }

        [Test]
        public void Resolve_NullPool_ReturnsEmptyList()
        {
            var instance = CreateInstance(null);

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Resolve_EmptyPool_ReturnsEmptyList()
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>();
            _createdObjects.Add(pool);
            var instance = CreateInstance(pool);

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Resolve_CombatRoom_SpawnsTwoByDefault()
        {
            var e1 = CreateEnemy("Goblin");
            var e2 = CreateEnemy("Orc");
            var pool = CreatePool(e1, e2);
            var instance = CreateInstance(pool, RoomType.Combat);

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(2, result.Count,
                "Combat rooms default = 2 enemies.");
        }

        [Test]
        public void Resolve_BossRoom_SpawnsOneByDefault()
        {
            var boss = CreateEnemy("Dragon", hp: 80);
            var pool = CreatePool(boss);
            var instance = CreateInstance(pool, RoomType.Boss);

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count,
                "Boss rooms default = 1 enemy.");
        }

        [Test]
        public void Resolve_RegistersEachEnemyInRegistry()
        {
            var pool = CreatePool(CreateEnemy("Goblin"), CreateEnemy("Orc"));
            var instance = CreateInstance(pool);

            var result = _resolver.Resolve(instance, new System.Random(42));

            foreach (var (id, _) in result)
            {
                Assert.IsTrue(_registry.TryGetAttributes(id, out _),
                    $"Enemy {id} debe registrarse en entity registry");
            }
        }

        [Test]
        public void Resolve_RegistersEachEnemyInAttributesManager()
        {
            var pool = CreatePool(CreateEnemy("Goblin"), CreateEnemy("Orc"));
            var instance = CreateInstance(pool);

            var result = _resolver.Resolve(instance, new System.Random(42));

            foreach (var (id, _) in result)
            {
                Assert.IsTrue(_attributes.IsRegistered(id),
                    $"Enemy {id} debe registrarse en AttributesManager");
            }
        }

        [Test]
        public void Resolve_GeneratesUniqueGuids()
        {
            var pool = CreatePool(CreateEnemy("Goblin"));
            var instance = CreateInstance(pool);

            var result = _resolver.Resolve(instance, new System.Random(42));

            var uniqueIds = result.Select(r => r.id).Distinct().ToList();
            Assert.AreEqual(result.Count, uniqueIds.Count);
        }

        [Test]
        public void Resolve_TracksSpawnedEnemiesOnInstance()
        {
            var pool = CreatePool(CreateEnemy("Goblin"), CreateEnemy("Orc"));
            var instance = CreateInstance(pool);

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(result.Count, instance.SpawnedEnemies.Count,
                "Cada spawn debe aparecer en RoomInstance.SpawnedEnemies.");
            foreach (var (id, _) in result)
            {
                Assert.IsTrue(instance.SpawnedEnemies.Contains(id));
            }
        }

        [Test]
        public void Resolve_SeedsEnemySpawnStateInObjectStates()
        {
            var pool = CreatePool(CreateEnemy("Goblin", hp: 25));
            var instance = CreateInstance(pool, RoomType.Boss);

            _resolver.Resolve(instance, new System.Random(42));

            Assert.IsTrue(instance.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var state));
            Assert.IsFalse(state.IsDead);
            Assert.AreEqual(25, state.CurrentHP);
            Assert.AreEqual(0, state.SpawnPointIndex);
        }

        [Test]
        public void Resolve_WithPortraitResolver_RegistersPortraitPerSpawnedEnemy()
        {
            // Arrange
            var texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _createdObjects.Add(sprite);

            var enemy = CreateEnemy("Goblin");
            enemy.Portrait = sprite;
            var pool = CreatePool(enemy);
            var instance = CreateInstance(pool);

            var portraits = new RecordingPortraitResolver();
            var resolver = new DefaultEnemySpawnResolver(
                _registry, _attributes, portraits: portraits);

            // Act
            var result = resolver.Resolve(instance, new System.Random(42));

            // Assert
            Assert.AreEqual(result.Count, portraits.Registered.Count,
                "Cada spawn debe registrar su portrait en el resolver.");
            foreach (var (id, _) in result)
            {
                Assert.IsTrue(portraits.Registered.TryGetValue(id, out var registered));
                Assert.AreSame(sprite, registered,
                    $"El portrait registrado para {id} debe ser el del EnemyDataSO.");
            }
        }

        /// <summary>Fake que captura las llamadas a Register para asserts.</summary>
        private sealed class RecordingPortraitResolver : IEntityPortraitResolver
        {
            public readonly Dictionary<Guid, Sprite> Registered = new();
            public void Register(Guid entityId, Sprite portrait) => Registered[entityId] = portrait;
            public void Unregister(Guid entityId) => Registered.Remove(entityId);
            public bool TryGetPortrait(Guid entityId, out Sprite portrait)
                => Registered.TryGetValue(entityId, out portrait);
            public void Clear() => Registered.Clear();
        }

        // -------------------------------------------------------------------
        // Tier determinístico por piso (Feature#0023)
        // -------------------------------------------------------------------

        /// <summary>Fake mínimo del run context — solo FloorIndex importa acá.</summary>
        private sealed class FakeRunContext : IRunContextService
        {
            public Guid RunId { get; } = Guid.NewGuid();
            public int FloorIndex { get; set; }
            public ClassHeroSO SelectedHero => null;
            public bool IsRunActive => true;
            public void AdvanceFloor() => FloorIndex++;
        }

        /// <summary>Goblin 20 HP con T2 (HP ×2) autorado desde el piso 2.</summary>
        private EnemyDataSO CreateTieredEnemy()
        {
            var enemy = CreateEnemy("Goblin", hp: 20);
            enemy.ExtraTiers.Add(new EnemyTier
            {
                Label = "T2",
                MinFloor = 2,
                HP = new TierStat { Mode = StatMode.Multiplier, Multiplier = 2f },
            });
            return enemy;
        }

        [Test]
        public void Resolve_Floor1_SpawnsTier1WithBaseHP()
        {
            // Arrange — pool de 1 enemigo (Boss room = 1 spawn) para determinismo.
            var pool = CreatePool(CreateTieredEnemy());
            var instance = CreateInstance(pool, RoomType.Boss);
            var resolver = new DefaultEnemySpawnResolver(
                _registry, _attributes, runContext: new FakeRunContext { FloorIndex = 0 });

            // Act
            resolver.Resolve(instance, new System.Random(42));

            // Assert
            Assert.IsTrue(instance.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var state));
            Assert.AreEqual(1, state.Tier, "piso 1 < MinFloor 2 ⇒ Tier 1");
            Assert.AreEqual(20, state.CurrentHP);
        }

        [Test]
        public void Resolve_Floor2_SpawnsHighestEligibleTier()
        {
            var pool = CreatePool(CreateTieredEnemy());
            var instance = CreateInstance(pool, RoomType.Boss);
            var resolver = new DefaultEnemySpawnResolver(
                _registry, _attributes, runContext: new FakeRunContext { FloorIndex = 1 });

            resolver.Resolve(instance, new System.Random(42));

            Assert.IsTrue(instance.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var state));
            Assert.AreEqual(2, state.Tier, "piso 2 ⇒ T2 determinístico");
            Assert.AreEqual(40, state.CurrentHP, "HP ×2 del T2");
        }

        [Test]
        public void Resolve_NoRunContext_DefaultsToFloor1()
        {
            var pool = CreatePool(CreateTieredEnemy());
            var instance = CreateInstance(pool, RoomType.Boss);

            // _resolver del SetUp: sin IRunContextService (tests / tutorial).
            _resolver.Resolve(instance, new System.Random(42));

            Assert.IsTrue(instance.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var state));
            Assert.AreEqual(1, state.Tier, "sin run context ⇒ piso 1 ⇒ Tier 1");
        }

        [Test]
        public void Resolve_FloorAdvancesMidRun_ReadsFloorAtSpawnTime()
        {
            // El resolver vive toda la run — el piso debe leerse por spawn, no cachearse.
            var runContext = new FakeRunContext { FloorIndex = 0 };
            var resolver = new DefaultEnemySpawnResolver(
                _registry, _attributes, runContext: runContext);
            var pool = CreatePool(CreateTieredEnemy());

            var roomFloor1 = CreateInstance(pool, RoomType.Boss);
            resolver.Resolve(roomFloor1, new System.Random(42));

            runContext.AdvanceFloor();
            var roomFloor2 = CreateInstance(pool, RoomType.Boss);
            resolver.Resolve(roomFloor2, new System.Random(43));

            Assert.IsTrue(roomFloor1.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var s1));
            Assert.IsTrue(roomFloor2.ObjectStates.TryGet<EnemySpawnState>("enemy_0", out var s2));
            Assert.AreEqual(1, s1.Tier);
            Assert.AreEqual(2, s2.Tier, "tras AdvanceFloor el mismo resolver spawnea T2");
        }

        [Test]
        public void Resolve_Reentry_KeepsPersistedTierRegardlessOfFloor()
        {
            // Re-entry restaura el tier guardado — el piso actual no lo re-resuelve.
            var pool = CreatePool(CreateTieredEnemy());
            var instance = CreateInstance(pool);
            instance.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = "enemy.goblin",
                CurrentHP = 33,
                IsDead = false,
                SpawnPointIndex = 0,
                Tier = 2,
            });
            var resolver = new DefaultEnemySpawnResolver(
                _registry, _attributes, runContext: new FakeRunContext { FloorIndex = 0 });

            var result = resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count);
            var health = _attributes.GetAttribute<Rollgeon.Attributes.Stats.Health>(result[0].id);
            Assert.IsNotNull(health);
            Assert.AreEqual(33, health.Value, "re-entry respeta el HP del state (max 40 del T2 persistido)");
        }

        [Test]
        public void Resolve_Reentry_OnlySpawnsAliveEnemies()
        {
            var pool = CreatePool(CreateEnemy("Goblin", hp: 20));
            var instance = CreateInstance(pool);

            // Pre-seed 2 enemies: uno vivo con HP modificado, otro muerto.
            instance.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = "enemy.goblin",
                CurrentHP = 7,
                IsDead = false,
                SpawnPointIndex = 0,
            });
            instance.ObjectStates.Set("enemy_1", new EnemySpawnState
            {
                SpawnPointId = "enemy_1",
                EnemyDataSOId = "enemy.goblin",
                CurrentHP = 0,
                IsDead = true,
                SpawnPointIndex = 1,
            });

            var result = _resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count,
                "Solo re-spawnea los !IsDead del ObjectStates.");
        }

        // -------------------------------------------------------------------
        // Resume desde save (#0028 Fase 2): posición + GUID preservados
        // -------------------------------------------------------------------

        private EnemySpawnState SavedState(Guid guid, GridCoord cell) => new EnemySpawnState
        {
            SpawnPointId = "enemy_0",
            EnemyDataSOId = "enemy.goblin",
            CurrentHP = 7,
            IsDead = false,
            SpawnPointIndex = 0,
            Tier = 1,
            HasLastCell = true,
            LastCell = cell,
            Guid = guid.ToString(),
        };

        [Test]
        public void Resolve_ResumeFromSaveNextSpawn_UsesSavedCoordAndGuid()
        {
            var grid = new GridManager();
            var pool = CreatePool(CreateEnemy("Goblin", hp: 20));
            var instance = CreateInstance(pool);
            var savedGuid = Guid.NewGuid();
            instance.ObjectStates.Set("enemy_0", SavedState(savedGuid, new GridCoord(5, 7)));

            var resolver = new DefaultEnemySpawnResolver(_registry, _attributes, grid: grid)
            {
                ResumeFromSaveNextSpawn = true,
            };

            var result = resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(savedGuid, result[0].id, "resume preserva el GUID guardado");
            Assert.IsTrue(grid.TryGetPosition(savedGuid, out var coord));
            Assert.AreEqual(new GridCoord(5, 7), coord, "resume spawnea en la tile guardada");

            var health = _attributes.GetAttribute<Rollgeon.Attributes.Stats.Health>(savedGuid);
            Assert.AreEqual(7, health.Value, "HP guardado restaurado");

            Assert.IsFalse(resolver.ResumeFromSaveNextSpawn, "el flag es one-shot");
        }

        [Test]
        public void Resolve_Reentry_WithoutResumeFlag_IgnoresSavedGuidAndCell()
        {
            // Sin el flag (re-entry normal dentro de la sesión): GUID nuevo, posición
            // no forzada a la guardada — preserva el diseño GD de reposición random.
            var grid = new GridManager();
            var pool = CreatePool(CreateEnemy("Goblin", hp: 20));
            var instance = CreateInstance(pool);
            var savedGuid = Guid.NewGuid();
            instance.ObjectStates.Set("enemy_0", SavedState(savedGuid, new GridCoord(5, 7)));

            var resolver = new DefaultEnemySpawnResolver(_registry, _attributes, grid: grid);
            // NO seteamos ResumeFromSaveNextSpawn.

            var result = resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count);
            Assert.AreNotEqual(savedGuid, result[0].id,
                "sin resume el GUID es nuevo (no se preserva el guardado)");
        }
    }
}
