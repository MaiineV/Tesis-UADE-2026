using System;
using System.Collections.Generic;
using Rollgeon.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Programmatic factory that creates a full <see cref="FloorLayoutSO"/> graph
    /// for testing and prototyping — no .asset files needed.
    /// </summary>
    public static class SampleFloorFactory
    {
        public sealed class Result : IDisposable
        {
            public FloorLayoutSO Layout { get; }
            public IReadOnlyList<Object> AllCreated { get; }

            internal Result(FloorLayoutSO layout, List<Object> allCreated)
            {
                Layout = layout;
                AllCreated = allCreated;
            }

            public void Dispose()
            {
                for (int i = AllCreated.Count - 1; i >= 0; i--)
                {
                    if (AllCreated[i] != null)
                        Object.DestroyImmediate(AllCreated[i]);
                }
            }
        }

        public static Result Create()
        {
            var all = new List<Object>();

            // --- Enemies ---------------------------------------------------

            var slime    = CreateEnemy(all, "enemy.slime",        "Slime",    10, 3,  2);
            var goblin   = CreateEnemy(all, "enemy.goblin",       "Goblin",   15, 5,  4);
            var skeleton = CreateEnemy(all, "enemy.skeleton",     "Skeleton", 20, 4,  3);
            var bat      = CreateEnemy(all, "enemy.bat",          "Bat",       8, 2,  6);
            var rat      = CreateEnemy(all, "enemy.rat",          "Rat",       6, 2,  5);
            var spider   = CreateEnemy(all, "enemy.spider",       "Spider",   12, 4,  4);
            var dragon   = CreateEnemy(all, "enemy.boss.dragon",  "Dragon",   80, 12, 3);
            var lich     = CreateEnemy(all, "enemy.boss.lich",    "Lich",     60, 8,  5);

            // --- Enemy Pools -----------------------------------------------

            var poolCellar = CreatePool(all, "Pool_Cellar",
                new WeightedEntry<EnemyDataSO>(slime, 60f),
                new WeightedEntry<EnemyDataSO>(rat,   40f));

            var poolCrypt = CreatePool(all, "Pool_Crypt",
                new WeightedEntry<EnemyDataSO>(skeleton, 50f),
                new WeightedEntry<EnemyDataSO>(bat,      30f),
                new WeightedEntry<EnemyDataSO>(spider,   20f));

            var poolCave = CreatePool(all, "Pool_Cave",
                new WeightedEntry<EnemyDataSO>(goblin, 50f),
                new WeightedEntry<EnemyDataSO>(spider, 30f),
                new WeightedEntry<EnemyDataSO>(slime,  20f));

            // --- Rooms -----------------------------------------------------

            var cellar   = CreateRoom(all, "room.combat.cellar",    "Damp Cellar",        RoomType.Combat, poolCellar);
            var crypt    = CreateRoom(all, "room.combat.crypt",     "Forgotten Crypt",    RoomType.Combat, poolCrypt);
            var cave     = CreateRoom(all, "room.combat.cave",      "Dark Cave",          RoomType.Combat, poolCave);
            var merchant = CreateRoom(all, "room.shop.merchant",    "Wandering Merchant", RoomType.Shop,   null);
            var fountain = CreateRoom(all, "room.potion.fountain",  "Healing Fountain",   RoomType.Potion, null);

            // --- Floor Layout ----------------------------------------------

            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            layout.name = "SampleFloor";
            layout.RoomCountMin   = 8;
            layout.RoomCountMax   = 12;
            layout.CombatRooms    = new List<RoomSO> { cellar, crypt, cave };
            layout.ShopRooms      = new List<RoomSO> { merchant };
            layout.PotionRooms    = new List<RoomSO> { fountain };
            layout.BossCandidates = new List<EnemyDataSO> { dragon, lich };
            all.Add(layout);

            return new Result(layout, all);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static EnemyDataSO CreateEnemy(
            List<Object> tracker, string entityId, string displayName,
            int hp, int atk, int spd)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.name        = displayName;
            enemy.EntityId    = entityId;
            enemy.DisplayName = displayName;
            enemy.BaseHP      = hp;
            enemy.BaseAttack  = atk;
            enemy.BaseSpeed   = spd;
            tracker.Add(enemy);
            return enemy;
        }

        private static EnemyPoolSO CreatePool(
            List<Object> tracker, string poolName,
            params WeightedEntry<EnemyDataSO>[] entries)
        {
            var pool = ScriptableObject.CreateInstance<EnemyPoolSO>();
            pool.name    = poolName;
            pool.Entries = new List<WeightedEntry<EnemyDataSO>>(entries);
            tracker.Add(pool);
            return pool;
        }

        private static RoomSO CreateRoom(
            List<Object> tracker, string roomId, string displayName,
            RoomType type, EnemyPoolSO enemyPool)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.name        = displayName;
            room.RoomId      = roomId;
            room.DisplayName = displayName;
            room.Type        = type;
            room.EnemyPool   = enemyPool;
            tracker.Add(room);
            return room;
        }
    }
}
