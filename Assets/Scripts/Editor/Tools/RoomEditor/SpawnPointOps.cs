using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    /// <summary>
    /// Pure editor operations on the spawn-point side of <see cref="RoomLayout"/>.
    /// Centralized so the Spawn Points tab and tests share the same semantics
    /// (e.g. "Add Set pads every <see cref="SpawnPointConfig"/> in lockstep").
    /// </summary>
    public static class SpawnPointOps
    {
        public const string UndoLabel = "Edit Spawn Points";

        public sealed class ValidationReport
        {
            public List<string> Issues = new List<string>();
        }

        // -----------------------------------------------------------------
        // Sets
        // -----------------------------------------------------------------

        public static int GetMaxSetCount(RoomLayout layout)
        {
            if (layout == null) return 0;
            int max = 0;
            var configs = layout.GetComponentsInChildren<SpawnPointConfig>(includeInactive: true);
            foreach (var c in configs)
                if (c != null && c.SetCount > max) max = c.SetCount;
            return max;
        }

        /// <summary>Appends a null entry to <c>EnemySets</c> on every <see cref="SpawnPointConfig"/> in the room.</summary>
        public static void AddSetSlot(RoomLayout layout)
        {
            if (layout == null) return;
            var configs = layout.GetComponentsInChildren<SpawnPointConfig>(includeInactive: true);
            foreach (var c in configs)
            {
                if (c == null) continue;
                Undo.RecordObject(c, UndoLabel);
                if (c.EnemySets == null) c.EnemySets = new List<Rollgeon.Entities.EnemyDataSO>();
                c.EnemySets.Add(null);
                EditorUtility.SetDirty(c);
            }
        }

        /// <summary>Removes the entry at <paramref name="index"/> from every config that has it.</summary>
        public static void RemoveSetSlot(RoomLayout layout, int index)
        {
            if (layout == null || index < 0) return;
            var configs = layout.GetComponentsInChildren<SpawnPointConfig>(includeInactive: true);
            foreach (var c in configs)
            {
                if (c == null || c.EnemySets == null) continue;
                if (index >= c.EnemySets.Count) continue;
                Undo.RecordObject(c, UndoLabel);
                c.EnemySets.RemoveAt(index);
                EditorUtility.SetDirty(c);
            }
        }

        /// <summary>Pads short <c>EnemySets</c> lists with nulls so all configs match the max length.</summary>
        public static int NormalizeSetCount(RoomLayout layout)
        {
            if (layout == null) return 0;
            int max = GetMaxSetCount(layout);
            int padded = 0;
            var configs = layout.GetComponentsInChildren<SpawnPointConfig>(includeInactive: true);
            foreach (var c in configs)
            {
                if (c == null) continue;
                if (c.EnemySets == null) c.EnemySets = new List<Rollgeon.Entities.EnemyDataSO>();
                while (c.EnemySets.Count < max)
                {
                    Undo.RecordObject(c, UndoLabel);
                    c.EnemySets.Add(null);
                    padded++;
                    EditorUtility.SetDirty(c);
                }
            }
            return padded;
        }

        // -----------------------------------------------------------------
        // Spawn points
        // -----------------------------------------------------------------

        public static Transform AddSpawnPoint(RoomLayout layout, Vector3 worldPosition, int initialSetCount)
        {
            if (layout == null) return null;

            int index = layout.EnemySpawnPoints != null ? layout.EnemySpawnPoints.Count : 0;
            var go = new GameObject($"SP_{(index + 1):00}");
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);

            Undo.SetTransformParent(go.transform, layout.transform, UndoLabel);
            go.transform.position = worldPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var config = Undo.AddComponent<SpawnPointConfig>(go);
            for (int i = 0; i < initialSetCount; i++) config.EnemySets.Add(null);
            EditorUtility.SetDirty(config);

            Undo.RecordObject(layout, UndoLabel);
            if (layout.EnemySpawnPoints == null) layout.EnemySpawnPoints = new List<Transform>();
            layout.EnemySpawnPoints.Add(go.transform);
            EditorUtility.SetDirty(layout);

            return go.transform;
        }

        public static bool RemoveSpawnPoint(RoomLayout layout, Transform sp)
        {
            if (layout == null || sp == null) return false;

            Undo.RecordObject(layout, UndoLabel);
            if (layout.EnemySpawnPoints != null)
            {
                for (int i = layout.EnemySpawnPoints.Count - 1; i >= 0; i--)
                    if (layout.EnemySpawnPoints[i] == sp) layout.EnemySpawnPoints.RemoveAt(i);
            }
            EditorUtility.SetDirty(layout);
            Undo.DestroyObjectImmediate(sp.gameObject);
            return true;
        }

        public static void RemoveNullAt(RoomLayout layout, int index)
        {
            if (layout == null || layout.EnemySpawnPoints == null) return;
            if (index < 0 || index >= layout.EnemySpawnPoints.Count) return;
            if (layout.EnemySpawnPoints[index] != null) return;
            Undo.RecordObject(layout, UndoLabel);
            layout.EnemySpawnPoints.RemoveAt(index);
            EditorUtility.SetDirty(layout);
        }

        public static void MoveSpawnPoint(Transform sp, Vector3 worldPosition)
        {
            if (sp == null) return;
            Undo.RecordObject(sp, UndoLabel);
            sp.position = worldPosition;
            EditorUtility.SetDirty(sp);
        }

        /// <summary>Rebuilds <c>layout.EnemySpawnPoints</c> from child <see cref="SpawnPointConfig"/> components.</summary>
        public static int ResyncSpawnPointList(RoomLayout layout)
        {
            if (layout == null) return 0;
            Undo.RecordObject(layout, UndoLabel);
            if (layout.EnemySpawnPoints == null) layout.EnemySpawnPoints = new List<Transform>();

            // Remove nulls and entries not under this layout.
            for (int i = layout.EnemySpawnPoints.Count - 1; i >= 0; i--)
            {
                var t = layout.EnemySpawnPoints[i];
                if (t == null || !t.IsChildOf(layout.transform))
                    layout.EnemySpawnPoints.RemoveAt(i);
            }

            int added = 0;
            var configs = layout.GetComponentsInChildren<SpawnPointConfig>(includeInactive: true);
            foreach (var c in configs)
            {
                if (c == null) continue;
                if (!layout.EnemySpawnPoints.Contains(c.transform))
                {
                    layout.EnemySpawnPoints.Add(c.transform);
                    added++;
                }
            }
            EditorUtility.SetDirty(layout);
            return added;
        }

        // -----------------------------------------------------------------
        // Validation
        // -----------------------------------------------------------------

        public static ValidationReport Validate(RoomLayout layout)
        {
            var report = new ValidationReport();
            if (layout == null) return report;

            int max = GetMaxSetCount(layout);

            // 1. Null entries in EnemySpawnPoints.
            if (layout.EnemySpawnPoints != null)
            {
                int nullCount = 0;
                foreach (var sp in layout.EnemySpawnPoints) if (sp == null) nullCount++;
                if (nullCount > 0)
                    report.Issues.Add($"{nullCount} null entry/entries in RoomLayout.EnemySpawnPoints. Use 'Re-sync' to clean up.");
            }

            // 2. Orphan configs (SpawnPointConfig in tree but not in list).
            var configs = layout.GetComponentsInChildren<SpawnPointConfig>(includeInactive: true);
            int orphans = 0;
            foreach (var c in configs)
            {
                if (c == null) continue;
                if (layout.EnemySpawnPoints == null || !layout.EnemySpawnPoints.Contains(c.transform))
                    orphans++;
            }
            if (orphans > 0)
                report.Issues.Add($"{orphans} SpawnPointConfig component(s) not registered in EnemySpawnPoints. Use 'Re-sync'.");

            // 3. SetCount mismatch.
            int mismatched = 0;
            foreach (var c in configs)
            {
                if (c == null) continue;
                if (c.SetCount != max) mismatched++;
            }
            if (mismatched > 0)
                report.Issues.Add($"{mismatched} config(s) have fewer than {max} sets. Use 'Normalize' to pad with nulls.");

            // 4. Null EnemyDataSO slots.
            int nullSlots = 0;
            foreach (var c in configs)
            {
                if (c == null || c.EnemySets == null) continue;
                foreach (var e in c.EnemySets) if (e == null) nullSlots++;
            }
            if (nullSlots > 0)
                report.Issues.Add($"{nullSlots} null EnemyDataSO slot(s). The resolver will fall back to RoomSO.EnemyPool for these.");

            return report;
        }
    }
}
