using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Chests;
using Rollgeon.Combat.Pipelines;
using Rollgeon.DevConsole.Commands;
using Rollgeon.Items;

namespace Rollgeon.DevConsole.Tests
{
    [TestFixture]
    public class ChestCommandTests
    {
        private FakeConsoleContext _ctx;
        private FakeChestService _chests;
        private SpyDamagePipeline _pipeline;
        private ChestCommand _command;

        [SetUp]
        public void SetUp()
        {
            _ctx = new FakeConsoleContext();
            _chests = new FakeChestService();
            _pipeline = new SpyDamagePipeline();
            _ctx.Register<IChestService>(_chests);
            _ctx.Register<IDamagePipeline>(_pipeline);
            _command = new ChestCommand();
        }

        [TearDown]
        public void TearDown() => EventManager.ResetEventDictionary();

        private Core.CommandResult Run(params string[] args) => _command.Execute(args, _ctx);

        [Test]
        public void chest_spawn_should_use_parsed_tier_and_mimic_flag()
        {
            // Act
            var result = Run("spawn", "legendary", "mimic");

            // Assert
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(ItemRarity.Legendary, _chests.LastSpawnTier);
            Assert.IsTrue(_chests.LastSpawnMimic);
        }

        [Test]
        public void chest_spawn_should_fail_on_unknown_tier()
        {
            // Act
            var result = Run("spawn", "banana");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, _chests.SpawnCalls);
        }

        [Test]
        public void chest_kill_should_resolve_lethal_damage_from_player_by_default()
        {
            // Arrange
            _chests.Active = new ChestRuntime { Guid = Guid.NewGuid(), Tier = ItemRarity.Rare };

            // Act
            var result = Run("kill");

            // Assert
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(_ctx.PlayerGuid, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_chests.Active.Guid, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void chest_kill_enemy_should_use_non_player_source()
        {
            // Arrange
            _chests.Active = new ChestRuntime { Guid = Guid.NewGuid(), Tier = ItemRarity.Common };

            // Act
            var result = Run("kill", "enemy");

            // Assert
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreNotEqual(_ctx.PlayerGuid, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(AttackKind.Environmental, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void chest_kill_should_fail_when_no_active_chest()
        {
            // Act
            var result = Run("kill");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, _pipeline.Resolved.Count);
        }

        [Test]
        public void chest_info_should_report_when_no_chest()
        {
            // Act
            var result = Run("info");

            // Assert
            Assert.IsTrue(result.Success);
            StringAssert.Contains("Sin cofre", result.Message);
        }

        [Test]
        public void chest_should_fail_outside_of_run()
        {
            // Arrange
            _ctx.IsRunActive = false;

            // Act
            var result = Run("info");

            // Assert
            Assert.IsFalse(result.Success);
        }

        // ----- fakes -----------------------------------------------------

        private sealed class FakeChestService : IChestService
        {
            public ChestRuntime Active;
            public int SpawnCalls;
            public ItemRarity LastSpawnTier;
            public bool LastSpawnMimic;

            public ChestRuntime ActiveChest => Active;

            public bool DebugSpawn(ItemRarity tier, bool isMimic)
            {
                SpawnCalls++;
                LastSpawnTier = tier;
                LastSpawnMimic = isMimic;
                return true;
            }

            public bool IsChest(Guid guid) => Active != null && Active.Guid == guid;

            public bool TryGetActiveChest(out Guid chestGuid)
            {
                chestGuid = Active?.Guid ?? Guid.Empty;
                return Active != null;
            }
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();
            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                return ctx;
            }
            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
