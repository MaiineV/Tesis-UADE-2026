using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.Initiative;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using Rollgeon.Run;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Handoff.Tests
{
    /// <summary>
    /// BUG-078: en el resume/re-entry de una boss room, <c>LookupEnemyData</c> solo miraba
    /// <c>RoomSO.PossibleSetups</c>/<c>EnemyPool</c> — el boss del piso NUNCA vive ahí (llega
    /// por <c>RoomInstance.Boss</c> o el <c>BossPoolSO</c> del piso, precedencia de código en
    /// <c>BuildSpawnPlan</c>). El lookup devolvía <c>null</c> y el <c>continue</c> silencioso
    /// dejaba el combate sin el boss ⇒ softlock. Esta suite cubre los dos fallbacks nuevos.
    /// </summary>
    [TestFixture]
    public class DefaultEnemySpawnResolverBossFallbackTests
    {
        private InMemoryEntityRegistry _registry;
        private AttributesManager _attributes;
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            _registry = new InMemoryEntityRegistry();
            _attributes = new AttributesManager();
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
        // Helpers (espejo de DefaultEnemySpawnResolverTests, sin compartir
        // estado privado entre archivos de test).
        // -------------------------------------------------------------------

        private RoomSO CreateBossRoom(EnemyPoolSO pool)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = "test_boss_room";
            room.DisplayName = "Test Boss Room";
            room.Type = RoomType.Boss;
            room.EnemyPool = pool;
            _createdObjects.Add(room);
            return room;
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

        private BossPoolSO CreateBossPool(params EnemyDataSO[] bosses)
        {
            var pool = ScriptableObject.CreateInstance<BossPoolSO>();
            pool.name = "TestBossPool";
            pool.Entries = new List<WeightedBoss>();
            foreach (var boss in bosses)
            {
                pool.Entries.Add(new WeightedBoss { Boss = boss, Weight = 1f, Enabled = true });
            }
            _createdObjects.Add(pool);
            return pool;
        }

        private FloorLayoutSO CreateLayout(BossPoolSO bossPool)
        {
            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            layout.name = "TestFloorLayout";
            layout.FloorId = "floor.test";
            layout.BossPool = bossPool;
            _createdObjects.Add(layout);
            return layout;
        }

        private sealed class FakeFloorProgression : IFloorProgressionService
        {
            public FloorLayoutSO CurrentLayout { get; set; }
        }

        private RoomInstance CreateResumeInstance(RoomSO room, EnemyDataSO instanceBoss, EnemyDataSO savedBoss)
        {
            var instance = new RoomInstance
            {
                InstanceId = Guid.NewGuid(),
                Template = room,
                State = RoomState.Uncleared,
                Boss = instanceBoss,
            };
            instance.ObjectStates.Set("enemy_0", new EnemySpawnState
            {
                SpawnPointId = "enemy_0",
                EnemyDataSOId = savedBoss.EntityId,
                CurrentHP = 40,
                IsDead = false,
                SpawnPointIndex = 0,
                Tier = 1,
            });
            return instance;
        }

        // -------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------

        [Test]
        public void Resolve_ReentryBossRoom_FallsBackToInstanceBoss_WhenNotInPossibleSetupsOrPool()
        {
            // Arrange — la sala trae un EnemyPool que NO tiene al boss (caso real: los 3
            // prefabs de boss room comparten EP_Boss, que no lista al boss verdadero).
            var roomPoolEnemy = CreateEnemy("PoolPlaceholder");
            var boss = CreateEnemy("Croupier", hp: 200);
            var room = CreateBossRoom(CreatePool(roomPoolEnemy));
            var instance = CreateResumeInstance(room, instanceBoss: boss, savedBoss: boss);

            var resolver = new DefaultEnemySpawnResolver(_registry, _attributes);

            // Act
            var result = resolver.Resolve(instance, new System.Random(42));

            // Assert
            Assert.AreEqual(1, result.Count,
                "el boss debe re-spawnear via el fallback a RoomInstance.Boss.");
            Assert.AreSame(boss, result[0].data);
        }

        [Test]
        public void Resolve_ReentryBossRoom_FallsBackToFloorBossPool_WhenInstanceBossDoesNotMatch()
        {
            // Arrange — instance.Boss es OTRO boss (ej. override consumido en un run previo);
            // el guardado solo matchea contra el BossPool del piso vigente.
            var roomPoolEnemy = CreateEnemy("PoolPlaceholder");
            var savedBoss = CreateEnemy("Tahur", hp: 180);
            var otherInstanceBoss = CreateEnemy("Bandida", hp: 150);
            var room = CreateBossRoom(CreatePool(roomPoolEnemy));
            var instance = CreateResumeInstance(room, instanceBoss: otherInstanceBoss, savedBoss: savedBoss);

            var progression = new FakeFloorProgression
            {
                CurrentLayout = CreateLayout(CreateBossPool(savedBoss, otherInstanceBoss))
            };
            var resolver = new DefaultEnemySpawnResolver(
                _registry, _attributes, floorProgression: progression);

            // Act
            var result = resolver.Resolve(instance, new System.Random(42));

            // Assert
            Assert.AreEqual(1, result.Count,
                "el boss debe re-spawnear via el fallback al BossPool del piso.");
            Assert.AreSame(savedBoss, result[0].data);
        }

        [Test]
        public void Resolve_ReentryBossRoom_NoMatchAnywhere_FallsBackToFreshSpawn()
        {
            // Arrange — ningún lookup matchea: el EnemyDataSOId guardado no existe en
            // PossibleSetups/EnemyPool/instance.Boss/BossPool. Antes esto devolvía una
            // lista VACÍA con states vivos ⇒ la sala iba a combate sin combatientes y
            // el guard de CombatEnterState lo abortaba en fantasma (BUG-078). Ahora cae
            // al plan de primer spawn, que garantiza boss vía instance.Boss.
            var roomPoolEnemy = CreateEnemy("PoolPlaceholder");
            var instanceBoss = CreateEnemy("Anotador");
            var room = CreateBossRoom(CreatePool(roomPoolEnemy));
            var instance = CreateResumeInstance(room, instanceBoss: instanceBoss,
                savedBoss: CreateEnemy("GhostBossNotRegisteredAnywhere"));

            var resolver = new DefaultEnemySpawnResolver(_registry, _attributes);

            // Act & Assert — el LogError del lookup fallido sigue saliendo, más el
            // warning del fallback.
            LogAssert.Expect(LogType.Error,
                new Regex(".*LookupEnemyData.*ghostbossnotregisteredanywhere.*"));
            LogAssert.Expect(LogType.Warning,
                new Regex(".*Re-entry con states vivos pero 0 enemigos resueltos.*"));
            var result = resolver.Resolve(instance, new System.Random(42));

            Assert.GreaterOrEqual(result.Count, 1,
                "con states vivos irresolubles debe spawnear un combate fresco, nunca vacío.");
        }

        [Test]
        public void Resolve_ReentryBossRoom_PossibleSetupsStillWinsOverInstanceBoss()
        {
            // Regresión: si el EnemyDataSOId SÍ está en PossibleSetups/EnemyPool, ese camino
            // (más específico) sigue ganando — los fallbacks nuevos son solo el último recurso.
            var boss = CreateEnemy("Cajero", hp: 150);
            var room = CreateBossRoom(CreatePool(boss));
            var decoyInstanceBoss = CreateEnemy("Decoy");
            var instance = CreateResumeInstance(room, instanceBoss: decoyInstanceBoss, savedBoss: boss);

            var resolver = new DefaultEnemySpawnResolver(_registry, _attributes);

            var result = resolver.Resolve(instance, new System.Random(42));

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(boss, result[0].data,
                "EnemyPool matchea directo — no debería ni consultar instance.Boss.");
        }
    }
}
