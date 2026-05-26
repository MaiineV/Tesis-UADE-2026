using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    /// <summary>
    /// Editor-only glue between a painted <see cref="DoorController"/> instance and the
    /// owning <see cref="RoomLayout"/>'s <c>DoorSlots</c>. Pure math (direction inference)
    /// lives on <see cref="DoorDirectionExtensions.FromLocalPosition"/>; this class wires
    /// the controller and upserts the matching slot with Undo support.
    /// </summary>
    public static class RoomEditorDoorBinder
    {
        public const string BindUndoLabel = "Bind Door";

        /// <summary>
        /// Returns the inferred <see cref="DoorDirection"/> for a world-space point inside
        /// <paramref name="layout"/>. Uses <see cref="RoomLayout.LocalBounds"/>.center as the
        /// reference so painted rooms behave the same whether the prefab origin is centered
        /// or offset.
        /// </summary>
        public static DoorDirection InferDirection(RoomLayout layout, Vector3 worldPosition)
        {
            if (layout == null) return DoorDirection.North;
            Vector3 local = layout.transform.InverseTransformPoint(worldPosition) - layout.LocalBounds.center;
            return DoorDirectionExtensions.FromLocalPosition(local);
        }

        /// <summary>
        /// Sets <see cref="DoorController.Direction"/> + <see cref="DoorController.SpawnPointId"/>
        /// based on the cell where the controller was placed, then upserts the matching slot in
        /// <c>layout.DoorSlots</c>. If a different controller already occupies that direction it
        /// is destroyed with a warning so the room never has two doors pointing the same way.
        /// </summary>
        public static DoorDirection BindOnPlace(RoomLayout layout, DoorController controller, Vector3 worldPosition)
        {
            if (layout == null || controller == null) return DoorDirection.North;

            var direction = InferDirection(layout, worldPosition);

            Undo.RecordObject(controller, BindUndoLabel);
            controller.Direction = direction;
            controller.SpawnPointId = direction.DoorStateKey();
            EditorUtility.SetDirty(controller);

            UpsertSlot(layout, controller, direction);
            return direction;
        }

        /// <summary>
        /// Replaces the slot for <paramref name="direction"/> with one pointing at
        /// <paramref name="controller"/>, destroying any pre-existing door GameObject for that
        /// direction. Public so the Doors tab can call it after a manual direction change.
        /// </summary>
        public static void UpsertSlot(RoomLayout layout, DoorController controller, DoorDirection direction)
        {
            if (layout == null || controller == null) return;

            Undo.RecordObject(layout, BindUndoLabel);

            var existing = layout.GetDoorSlot(direction);
            if (existing != null)
            {
                if (existing.DoorRoot != null && existing.DoorRoot != controller.gameObject)
                {
                    Debug.LogWarning(
                        $"[RoomEditor] '{layout.name}': replacing door at {direction} " +
                        $"('{existing.DoorRoot.name}' → '{controller.gameObject.name}').");
                    Undo.DestroyObjectImmediate(existing.DoorRoot);
                }
                existing.Anchor = controller.transform;
                existing.WallPlug = controller.EditorWallPlug;
                existing.DoorRoot = controller.gameObject;
            }
            else
            {
                layout.DoorSlots.Add(new DoorSlotRef
                {
                    Direction = direction,
                    Anchor = controller.transform,
                    WallPlug = controller.EditorWallPlug,
                    DoorRoot = controller.gameObject,
                });
            }

            EditorUtility.SetDirty(layout);
        }

        /// <summary>
        /// Removes the slot for <paramref name="direction"/> if present. Does not destroy the
        /// door GameObject — the painter handles erase via <c>TileMarker</c>.
        /// </summary>
        public static bool RemoveSlot(RoomLayout layout, DoorDirection direction)
        {
            if (layout == null) return false;
            for (int i = 0; i < layout.DoorSlots.Count; i++)
            {
                if (layout.DoorSlots[i] != null && layout.DoorSlots[i].Direction == direction)
                {
                    Undo.RecordObject(layout, "Remove Door Slot");
                    layout.DoorSlots.RemoveAt(i);
                    EditorUtility.SetDirty(layout);
                    return true;
                }
            }
            return false;
        }
    }
}
