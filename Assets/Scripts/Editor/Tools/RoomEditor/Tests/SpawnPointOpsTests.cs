using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Editor.Tools.RoomEditor;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor.Tests
{
    [TestFixture]
    public class SpawnPointOpsTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private RoomLayout MakeLayout()
        {
            var go = new GameObject("Room");
            _created.Add(go);
            var layout = go.AddComponent<RoomLayout>();
            return layout;
        }

        private EnemyDataSO MakeEnemy(string name)
        {
            var e = ScriptableObject.CreateInstance<EnemyDataSO>();
            e.name = name;
            _created.Add(e);
            return e;
        }

        // -----------------------------------------------------------------
        // AddSpawnPoint
        // -----------------------------------------------------------------

        [Test]
        public void should_append_transform_and_attach_config_when_AddSpawnPoint_called()
        {
            // Arrange
            var layout = MakeLayout();

            // Act
            var sp = SpawnPointOps.AddSpawnPoint(layout, new Vector3(2.5f, 0.5f, 3.5f), initialSetCount: 2);

            // Assert
            Assert.IsNotNull(sp);
            Assert.AreEqual(1, layout.EnemySpawnPoints.Count);
            Assert.AreSame(sp, layout.EnemySpawnPoints[0]);
            var cfg = sp.GetComponent<SpawnPointConfig>();
            Assert.IsNotNull(cfg);
            Assert.AreEqual(2, cfg.EnemySets.Count);
            Assert.AreEqual(new Vector3(2.5f, 0.5f, 3.5f), sp.position);
            Assert.AreSame(layout.transform, sp.parent);
        }

        // -----------------------------------------------------------------
        // RemoveSpawnPoint
        // -----------------------------------------------------------------

        [Test]
        public void should_prune_list_and_destroy_gameobject_when_RemoveSpawnPoint_called()
        {
            // Arrange
            var layout = MakeLayout();
            var sp = SpawnPointOps.AddSpawnPoint(layout, Vector3.zero, initialSetCount: 0);
            var spGO = sp.gameObject;

            // Act
            bool removed = SpawnPointOps.RemoveSpawnPoint(layout, sp);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(0, layout.EnemySpawnPoints.Count);
            Assert.IsTrue(spGO == null); // Unity null-check on destroyed object
        }

        // -----------------------------------------------------------------
        // AddSetSlot
        // -----------------------------------------------------------------

        [Test]
        public void should_pad_all_configs_to_same_length_when_AddSetSlot_called()
        {
            // Arrange
            var layout = MakeLayout();
            var sp1 = SpawnPointOps.AddSpawnPoint(layout, Vector3.zero, initialSetCount: 1);
            var sp2 = SpawnPointOps.AddSpawnPoint(layout, Vector3.right, initialSetCount: 1);

            // Act
            SpawnPointOps.AddSetSlot(layout);

            // Assert
            Assert.AreEqual(2, sp1.GetComponent<SpawnPointConfig>().EnemySets.Count);
            Assert.AreEqual(2, sp2.GetComponent<SpawnPointConfig>().EnemySets.Count);
            Assert.IsNull(sp1.GetComponent<SpawnPointConfig>().EnemySets[1]);
            Assert.IsNull(sp2.GetComponent<SpawnPointConfig>().EnemySets[1]);
        }

        // -----------------------------------------------------------------
        // RemoveSetSlot
        // -----------------------------------------------------------------

        [Test]
        public void should_remove_index_across_all_configs_when_RemoveSetSlot_called()
        {
            // Arrange
            var layout = MakeLayout();
            var goblin = MakeEnemy("Goblin");
            var slime = MakeEnemy("Slime");
            var auditor = MakeEnemy("Auditor");
            var sp1 = SpawnPointOps.AddSpawnPoint(layout, Vector3.zero, initialSetCount: 0);
            var sp2 = SpawnPointOps.AddSpawnPoint(layout, Vector3.right, initialSetCount: 0);
            sp1.GetComponent<SpawnPointConfig>().EnemySets.AddRange(new[] { goblin, slime, auditor });
            sp2.GetComponent<SpawnPointConfig>().EnemySets.AddRange(new[] { slime, auditor, goblin });

            // Act
            SpawnPointOps.RemoveSetSlot(layout, 1);

            // Assert
            var sets1 = sp1.GetComponent<SpawnPointConfig>().EnemySets;
            var sets2 = sp2.GetComponent<SpawnPointConfig>().EnemySets;
            Assert.AreEqual(2, sets1.Count);
            Assert.AreEqual(2, sets2.Count);
            Assert.AreSame(goblin, sets1[0]);
            Assert.AreSame(auditor, sets1[1]);
            Assert.AreSame(slime, sets2[0]);
            Assert.AreSame(goblin, sets2[1]);
        }

        // -----------------------------------------------------------------
        // GetMaxSetCount
        // -----------------------------------------------------------------

        [Test]
        public void should_return_zero_when_GetMaxSetCount_called_on_empty_layout()
        {
            // Arrange
            var layout = MakeLayout();

            // Act
            int count = SpawnPointOps.GetMaxSetCount(layout);

            // Assert
            Assert.AreEqual(0, count);
        }

        [Test]
        public void should_return_largest_setcount_when_GetMaxSetCount_called_with_uneven_configs()
        {
            // Arrange
            var layout = MakeLayout();
            var sp1 = SpawnPointOps.AddSpawnPoint(layout, Vector3.zero, initialSetCount: 1);
            var sp2 = SpawnPointOps.AddSpawnPoint(layout, Vector3.right, initialSetCount: 3);
            var sp3 = SpawnPointOps.AddSpawnPoint(layout, Vector3.up, initialSetCount: 2);

            // Act
            int count = SpawnPointOps.GetMaxSetCount(layout);

            // Assert
            Assert.AreEqual(3, count);
        }

        // -----------------------------------------------------------------
        // NormalizeSetCount
        // -----------------------------------------------------------------

        [Test]
        public void should_pad_short_configs_with_nulls_when_NormalizeSetCount_called()
        {
            // Arrange
            var layout = MakeLayout();
            var goblin = MakeEnemy("Goblin");
            var sp1 = SpawnPointOps.AddSpawnPoint(layout, Vector3.zero, initialSetCount: 0);
            var sp2 = SpawnPointOps.AddSpawnPoint(layout, Vector3.right, initialSetCount: 0);
            sp1.GetComponent<SpawnPointConfig>().EnemySets.AddRange(new[] { goblin, goblin, goblin });
            sp2.GetComponent<SpawnPointConfig>().EnemySets.Add(goblin);

            // Act
            int padded = SpawnPointOps.NormalizeSetCount(layout);

            // Assert
            Assert.AreEqual(2, padded);
            Assert.AreEqual(3, sp2.GetComponent<SpawnPointConfig>().EnemySets.Count);
            Assert.IsNull(sp2.GetComponent<SpawnPointConfig>().EnemySets[1]);
            Assert.IsNull(sp2.GetComponent<SpawnPointConfig>().EnemySets[2]);
        }

        // -----------------------------------------------------------------
        // ResyncSpawnPointList
        // -----------------------------------------------------------------

        [Test]
        public void should_register_orphan_configs_when_ResyncSpawnPointList_called()
        {
            // Arrange
            var layout = MakeLayout();
            var orphan = new GameObject("OrphanSP");
            orphan.transform.SetParent(layout.transform);
            orphan.AddComponent<SpawnPointConfig>();
            _created.Add(orphan);

            // Act
            int added = SpawnPointOps.ResyncSpawnPointList(layout);

            // Assert
            Assert.AreEqual(1, added);
            Assert.AreEqual(1, layout.EnemySpawnPoints.Count);
            Assert.AreSame(orphan.transform, layout.EnemySpawnPoints[0]);
        }

        [Test]
        public void should_remove_null_entries_when_ResyncSpawnPointList_called()
        {
            // Arrange
            var layout = MakeLayout();
            layout.EnemySpawnPoints.Add(null);
            layout.EnemySpawnPoints.Add(null);

            // Act
            int added = SpawnPointOps.ResyncSpawnPointList(layout);

            // Assert
            Assert.AreEqual(0, added);
            Assert.AreEqual(0, layout.EnemySpawnPoints.Count);
        }

        // -----------------------------------------------------------------
        // Validate
        // -----------------------------------------------------------------

        [Test]
        public void should_report_no_issues_when_Validate_called_on_well_formed_layout()
        {
            // Arrange
            var layout = MakeLayout();
            var goblin = MakeEnemy("Goblin");
            var sp1 = SpawnPointOps.AddSpawnPoint(layout, Vector3.zero, initialSetCount: 0);
            var sp2 = SpawnPointOps.AddSpawnPoint(layout, Vector3.right, initialSetCount: 0);
            sp1.GetComponent<SpawnPointConfig>().EnemySets.Add(goblin);
            sp2.GetComponent<SpawnPointConfig>().EnemySets.Add(goblin);

            // Act
            var report = SpawnPointOps.Validate(layout);

            // Assert
            Assert.AreEqual(0, report.Issues.Count, "Expected no issues, got: " + string.Join(" | ", report.Issues));
        }

        [Test]
        public void should_report_setcount_mismatch_when_Validate_called_with_uneven_configs()
        {
            // Arrange
            var layout = MakeLayout();
            var goblin = MakeEnemy("Goblin");
            var sp1 = SpawnPointOps.AddSpawnPoint(layout, Vector3.zero, initialSetCount: 0);
            var sp2 = SpawnPointOps.AddSpawnPoint(layout, Vector3.right, initialSetCount: 0);
            sp1.GetComponent<SpawnPointConfig>().EnemySets.AddRange(new[] { goblin, goblin, goblin });
            sp2.GetComponent<SpawnPointConfig>().EnemySets.Add(goblin);

            // Act
            var report = SpawnPointOps.Validate(layout);

            // Assert
            Assert.IsTrue(report.Issues.Exists(i => i.Contains("fewer than")),
                "Expected mismatch warning, got: " + string.Join(" | ", report.Issues));
        }
    }
}
