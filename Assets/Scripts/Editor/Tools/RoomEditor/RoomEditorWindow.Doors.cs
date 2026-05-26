using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    public sealed partial class RoomEditorWindow
    {
        // ============================ Tab — Doors ============================

        private const float DoorsR = 0.85f, DoorsG = 0.55f, DoorsB = 0.95f;

        private static readonly DoorDirection[] DoorDirections =
        {
            DoorDirection.North,
            DoorDirection.South,
            DoorDirection.East,
            DoorDirection.West,
        };

        [TabGroup(Tabs, TabDoors), BoxGroup(GDoors, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawDoorsSectionHeader() => DrawSectionHeader("Doors", new Color(DoorsR, DoorsG, DoorsB));

        [TabGroup(Tabs, TabDoors), BoxGroup(GDoors, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawDoorsList()
        {
            if (_target == null)
            {
                EditorGUILayout.HelpBox(
                    "Open or create a room prefab to manage doors.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Paint a Type=Door tile from the palette to add a door. The brush infers " +
                "N/S/E/W from the cell vs the room bounds center; you can override here.",
                MessageType.None);
            EditorGUILayout.Space(4);

            var controllersByDirection = BuildDoorControllerMap(_target);

            for (int i = 0; i < DoorDirections.Length; i++)
            {
                var dir = DoorDirections[i];
                controllersByDirection.TryGetValue(dir, out var ctrl);
                DrawDoorSlotRow(dir, ctrl);
                EditorGUILayout.Space(4);
            }
        }

        private static Dictionary<DoorDirection, DoorController> BuildDoorControllerMap(RoomLayout layout)
        {
            var map = new Dictionary<DoorDirection, DoorController>(4);
            var controllers = layout.GetComponentsInChildren<DoorController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                var c = controllers[i];
                if (!map.ContainsKey(c.Direction)) map[c.Direction] = c;
            }
            return map;
        }

        private void DrawDoorSlotRow(DoorDirection direction, DoorController controller)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                    GUILayout.Label(direction.ToString(), titleStyle, GUILayout.Width(60));
                    GUILayout.FlexibleSpace();

                    var slot = _target.GetDoorSlot(direction);
                    var statusColor = controller != null
                        ? new Color(0.45f, 0.85f, 0.45f)
                        : new Color(0.6f, 0.6f, 0.6f);
                    var statusText = controller != null ? "● assigned" : "○ empty";
                    var statusStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = statusColor } };
                    GUILayout.Label(statusText, statusStyle, GUILayout.Width(80));

                    using (new EditorGUI.DisabledScope(controller == null))
                    {
                        if (GUILayout.Button("Frame", GUILayout.Width(56)) && controller != null)
                        {
                            Selection.activeGameObject = controller.gameObject;
                            SceneView.FrameLastActiveSceneView();
                        }
                        if (GUILayout.Button("Remove", GUILayout.Width(64)) && controller != null)
                        {
                            RoomEditorDoorBinder.RemoveSlot(_target, controller.Direction);
                            Undo.DestroyObjectImmediate(controller.gameObject);
                            return;
                        }
                    }
                }

                if (controller == null)
                {
                    EditorGUILayout.LabelField("(no door placed)", EditorStyles.miniLabel);
                    return;
                }

                EditorGUI.indentLevel++;

                EditorGUILayout.ObjectField("Controller", controller, typeof(DoorController), allowSceneObjects: true);

                // Direction override — if changed, rebind.
                EditorGUI.BeginChangeCheck();
                var newDir = (DoorDirection)EditorGUILayout.EnumPopup("Direction", controller.Direction);
                if (EditorGUI.EndChangeCheck() && newDir != controller.Direction)
                {
                    RoomEditorDoorBinder.RemoveSlot(_target, controller.Direction);
                    Undo.RecordObject(controller, "Change Door Direction");
                    controller.Direction = newDir;
                    controller.SpawnPointId = newDir.DoorStateKey();
                    EditorUtility.SetDirty(controller);
                    RoomEditorDoorBinder.UpsertSlot(_target, controller, newDir);
                }

                DrawVisualOverrideField(controller, DoorController.EditorMeshOpenField,  "Mesh Open");
                DrawVisualOverrideField(controller, DoorController.EditorMeshClosedField, "Mesh Closed");
                DrawVisualOverrideField(controller, DoorController.EditorWallPlugField,   "Wall Plug");

                // Live preview state — toggles the 3 children via SetState.
                EditorGUI.BeginChangeCheck();
                var newState = (DoorVisualState)EditorGUILayout.EnumPopup("Preview State", controller.CurrentState);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Preview Door State");
                    controller.SetState(newState);
                    EditorUtility.SetDirty(controller);
                    if (controller.EditorMeshOpen   != null) EditorUtility.SetDirty(controller.EditorMeshOpen);
                    if (controller.EditorMeshClosed != null) EditorUtility.SetDirty(controller.EditorMeshClosed);
                    if (controller.EditorWallPlug   != null) EditorUtility.SetDirty(controller.EditorWallPlug);
                }

                EditorGUI.indentLevel--;
            }
        }

        private static void DrawVisualOverrideField(DoorController controller, string fieldName, string label)
        {
            var so = new SerializedObject(controller);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                EditorGUILayout.LabelField(label, $"(missing field: {fieldName})");
                return;
            }
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.ObjectField(label, prop.objectReferenceValue, typeof(GameObject), allowSceneObjects: true);
            if (EditorGUI.EndChangeCheck())
            {
                prop.objectReferenceValue = next;
                so.ApplyModifiedProperties();
            }
        }
    }
}
