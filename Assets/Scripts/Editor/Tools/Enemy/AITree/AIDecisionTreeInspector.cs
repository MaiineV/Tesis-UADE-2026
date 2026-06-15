using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Side panel that shows the inline (non-topological) fields of the currently selected
    /// AI node. Crucially, the PropertyTree is rooted on the <see cref="EnemyDataSO"/>
    /// (a Unity Object) instead of on the polymorphic node directly — this is what makes
    /// Odin's polymorphic pickers (e.g. "+ Add Condition" on AINode_If) commit correctly.
    /// </summary>
    public sealed class AIDecisionTreeInspector
    {
        public VisualElement Root { get; }

        readonly Action _onChanged;
        EnemyDataSO _enemy;
        PropertyTree _soTree;
        AIDecisionNode _selected;
        string _selectedPath;
        Vector2 _bodyScroll;

        Label _header;
        Label _emptyHint;
        IMGUIContainer _body;

        public AIDecisionTreeInspector(Action onChanged)
        {
            _onChanged = onChanged;

            Root = new VisualElement
            {
                style =
                {
                    width = 320,
                    minWidth = 280,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f),
                    paddingLeft = 8, paddingRight = 8, paddingTop = 8, paddingBottom = 8,
                    borderLeftWidth = 1,
                    borderLeftColor = new Color(0.10f, 0.10f, 0.10f),
                    flexShrink = 0,
                },
            };

            _header = new Label("AI Node Inspector")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    marginBottom = 6,
                },
            };
            Root.Add(_header);

            _emptyHint = new Label("Select a node in the graph to edit its parameters.")
            {
                style =
                {
                    color = new Color(0.7f, 0.7f, 0.7f),
                    whiteSpace = WhiteSpace.Normal,
                },
            };
            Root.Add(_emptyHint);

            _body = new IMGUIContainer(DrawBody) { style = { flexGrow = 1 } };
            Root.Add(_body);
        }

        // ---- binding -----------------------------------------------------

        public void Bind(EnemyDataSO enemy)
        {
            DisposeTree();
            _enemy = enemy;
            _selected = null;
            _selectedPath = null;
            UpdateHeader();
        }

        public void SetSelection(AIDecisionNode node)
        {
            _selected = node;
            _selectedPath = null;

            if (_enemy == null || node == null)
            {
                UpdateHeader();
                _body.MarkDirtyRepaint();
                return;
            }

            EnsureTree();
            _selectedPath = FindPathTo(node);
            UpdateHeader();
            _body.MarkDirtyRepaint();
        }

        public void RefreshIfShowing(AIDecisionNode node)
        {
            if (_selected == node) _body.MarkDirtyRepaint();
        }

        public void Dispose()
        {
            DisposeTree();
        }

        // ---- IMGUI body --------------------------------------------------

        void DrawBody()
        {
            if (_selected == null || _enemy == null) return;
            EnsureTree();
            if (_soTree == null) return;

            _soTree.UpdateTree();

            // Refactor a if/else (en vez de early-return en el warning) para mantener balanceados
            // los pares Begin/EndScrollView en el mismo frame IMGUI.
            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);

            // Descripción del tipo del nodo — siempre visible, incluso cuando el nodo es
            // huérfano (los docs siguen explicando qué hace).
            var doc = AINodeDocumentation.Get(_selected.GetType());
            if (!string.IsNullOrEmpty(doc))
            {
                EditorGUILayout.HelpBox(doc, MessageType.Info);
                EditorGUILayout.Space(4);
            }

            // Path cache may go stale across topology edits — re-resolve and verify.
            if (string.IsNullOrEmpty(_selectedPath) || !PathStillPointsToSelection())
                _selectedPath = FindPathTo(_selected);
            if (string.IsNullOrEmpty(_selectedPath))
            {
                EditorGUILayout.HelpBox(
                    "Este nodo no es alcanzable desde el AIRoot — no tiene un input port conectado a " +
                    "un nodo que descienda del root, así que no se va a ejecutar en runtime.\n\n" +
                    "Causas típicas:\n" +
                    "• El nodo padre fue borrado y este quedó suelto.\n" +
                    "• Re-rooteaste el árbol (Set as Root) y este quedó fuera del subárbol del nuevo root.\n" +
                    "• Lo creaste pero todavía no lo conectaste.\n\n" +
                    "Solución: arrastrá una conexión desde un output port (de un nodo conectado al árbol) " +
                    "hacia el input port de este nodo. O borralo si no lo necesitás más.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUI.BeginChangeCheck();

                switch (_selected)
                {
                    case AINode_If ifNode:
                        DrawIfNode(ifNode);
                        break;
                    case AINode_While whileNode:
                        DrawWhileNode(whileNode);
                        break;
                    case AINode_Behavior behaviorNode:
                        DrawBehaviorNode(behaviorNode);
                        break;
                    case AINode_Move moveNode:
                        DrawMoveNode(moveNode);
                        break;
                    case AINode_KeepDistance keepDistNode:
                        DrawKeepDistanceNode(keepDistNode);
                        break;
                    default:
                        DrawDefault();
                        break;
                }

                _soTree.ApplyChanges();

                if (EditorGUI.EndChangeCheck() || GUI.changed)
                {
                    EditorUtility.SetDirty(_enemy);
                    _onChanged?.Invoke();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ---- per-subtype drawers -----------------------------------------

        void DrawDefault()
        {
            var fields = AIDecisionNodeView.InlineFieldsOf(_selected);
            if (fields.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "This node has no inline parameters — its behavior is fully determined by its children.",
                    MessageType.Info);
                return;
            }
            foreach (var fieldName in fields) DrawOdinProp(fieldName);
        }

        /// <summary>
        /// AINode_If has two polymorphic fields (TargetSelector, Conditions list) whose base
        /// classes are decorated with [HideReferenceObjectPicker] (project rule §13.6.1).
        /// Odin won't show its picker for those, so we render a custom dropdown for assigning
        /// concrete subtypes, then defer to Odin to draw each item's own fields.
        /// </summary>
        void DrawIfNode(AINode_If node)
        {
            // Target Selector
            EditorGUILayout.LabelField("Target Selector", EditorStyles.boldLabel);
            PolymorphicPicker.DrawSingle(
                "Type", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                newInstance =>
                {
                    Undo.RecordObject(_enemy, "Change Target Selector");
                    node.TargetSelector = (BaseEnemyTargetSelector)newInstance;
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
            if (node.TargetSelector != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("TargetSelector");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Conditions list (AND-evaluated)
            EditorGUILayout.LabelField("Conditions (AND)", EditorStyles.boldLabel);
            if (node.Conditions == null) node.Conditions = new List<BasePreCondition>();
            DrawPolymorphicListItems(node.Conditions, "Conditions", "Condition");
            PolymorphicPicker.DrawAddButton(
                "Condition", typeof(BasePreCondition), node.Conditions,
                () =>
                {
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
        }

        /// <summary>
        /// AINode_While mirrors AINode_If's condition+target authoring (same picker pattern),
        /// pero con un único <c>Body</c> child y un campo <c>MaxIterations</c> safeguard.
        /// </summary>
        void DrawWhileNode(AINode_While node)
        {
            // Target Selector (mismo patrón que DrawIfNode)
            EditorGUILayout.LabelField("Target Selector", EditorStyles.boldLabel);
            PolymorphicPicker.DrawSingle(
                "Type", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                newInstance =>
                {
                    Undo.RecordObject(_enemy, "Change Target Selector");
                    node.TargetSelector = (BaseEnemyTargetSelector)newInstance;
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
            if (node.TargetSelector != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("TargetSelector");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Conditions list (AND-evaluated, looped each iteration)
            EditorGUILayout.LabelField("Conditions (AND, looped)", EditorStyles.boldLabel);
            if (node.Conditions == null) node.Conditions = new List<BasePreCondition>();
            DrawPolymorphicListItems(node.Conditions, "Conditions", "Condition");
            PolymorphicPicker.DrawAddButton(
                "Condition", typeof(BasePreCondition), node.Conditions,
                () =>
                {
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });

            EditorGUILayout.Space(8);

            // MaxIterations safeguard
            EditorGUILayout.LabelField("Safeguard", EditorStyles.boldLabel);
            DrawOdinProp("MaxIterations");
        }

        /// <summary>
        /// Generic polymorphic list renderer: header row with concrete-type label + ✕ button,
        /// then Odin draws the item's inner fields. Used for AINode_If's Conditions and for
        /// EffectData's PreConditions / Effects from inside DrawEffectData.
        /// </summary>
        void DrawPolymorphicListItems(IList list, string listRelativePath, string undoLabel)
        {
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var item = list[i];
                        EditorGUILayout.LabelField(
                            item != null ? item.GetType().Name : "(null)",
                            EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (PolymorphicPicker.DrawClearButton())
                        {
                            Undo.RecordObject(_enemy, "Remove " + undoLabel);
                            list.RemoveAt(i);
                            EditorUtility.SetDirty(_enemy);
                            NotifyChanged();
                            return;
                        }
                    }
                    if (list[i] != null)
                        DrawOdinProp(listRelativePath + ".$" + i);
                }
            }
        }

        /// <summary>
        /// AINode_Behavior wraps an <see cref="EnemyActionBehavior"/>. We mirror the visual
        /// layout of <see cref="DrawIfNode"/>: assign via a custom picker (Odin can't show
        /// one because of [HideReferenceObjectPicker] §13.6.1), then expand its fields with
        /// custom pickers for nested polymorphic types and Odin draws for the rest.
        /// </summary>
        void DrawBehaviorNode(AINode_Behavior node)
        {
            EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
            PolymorphicPicker.DrawSingle(
                "Type", typeof(EnemyActionBehavior), node.Behavior,
                newInstance =>
                {
                    Undo.RecordObject(_enemy, "Change Behavior");
                    node.Behavior = (EnemyActionBehavior)newInstance;
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });

            if (node.Behavior == null) return;
            var behavior = node.Behavior;

            EditorGUILayout.Space(8);

            // Trigger / phases
            EditorGUILayout.LabelField("Trigger / Phases", EditorStyles.boldLabel);
            DrawOdinProp("Behavior.Trigger");
            DrawOdinProp("Behavior.AllowedPhases");

            EditorGUILayout.Space(6);

            // Action
            EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);
            DrawOdinProp("Behavior.ActionName");

            EditorGUILayout.Space(6);

            // Target Selector — same picker pattern as DrawIfNode
            EditorGUILayout.LabelField("Target Selector", EditorStyles.boldLabel);
            PolymorphicPicker.DrawSingle(
                "Type", typeof(BaseEnemyTargetSelector), behavior.TargetSelector,
                newInstance =>
                {
                    Undo.RecordObject(_enemy, "Change Behavior Target Selector");
                    behavior.TargetSelector = (BaseEnemyTargetSelector)newInstance;
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
            if (behavior.TargetSelector != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("Behavior.TargetSelector");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            // Effects (List<EffectData>) — EffectData is concrete + has [HideReferenceObjectPicker]
            EditorGUILayout.LabelField("Effect Pipeline", EditorStyles.boldLabel);
            if (behavior.Effects == null) behavior.Effects = new List<EffectData>();
            DrawEffectsList(behavior.Effects);
            PolymorphicPicker.DrawAddButton(
                "Effect Group", typeof(EffectData), behavior.Effects,
                () =>
                {
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
        }

        void DrawMoveNode(AINode_Move node)
        {
            // Target Selector (mismo patrón que DrawIfNode). Null = player.
            EditorGUILayout.LabelField("Target Selector", EditorStyles.boldLabel);
            PolymorphicPicker.DrawSingle(
                "Type", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                newInstance =>
                {
                    Undo.RecordObject(_enemy, "Change Target Selector");
                    node.TargetSelector = (BaseEnemyTargetSelector)newInstance;
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
            if (node.TargetSelector != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("TargetSelector");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Max Steps", EditorStyles.boldLabel);
            DrawIntReaderField("MaxSteps", node.MaxSteps,
                r => { node.MaxSteps = r; });
            if (node.MaxSteps != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("MaxSteps");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Desired Range", EditorStyles.boldLabel);
            DrawIntReaderField("DesiredRange", node.DesiredRange,
                r => { node.DesiredRange = r; });
            if (node.DesiredRange != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("DesiredRange");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
            DrawOdinProp("Retreat");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Legacy (fallback si Desired Range es null)", EditorStyles.miniBoldLabel);
            DrawOdinProp("StopAdjacent");
        }

        void DrawKeepDistanceNode(AINode_KeepDistance node)
        {
            EditorGUILayout.LabelField("Max Steps", EditorStyles.boldLabel);
            DrawIntReaderField("MaxSteps", node.MaxSteps,
                r => { node.MaxSteps = r; });
            if (node.MaxSteps != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("MaxSteps");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Ideal Distance", EditorStyles.boldLabel);
            DrawIntReaderField("IdealDistance", node.IdealDistance,
                r => { node.IdealDistance = r; });
            if (node.IdealDistance != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("IdealDistance");
                EditorGUI.indentLevel--;
            }
        }

        void DrawIntReaderField(string label, AIIntReader current, Action<AIIntReader> setter)
        {
            PolymorphicPicker.DrawSingle(
                label, typeof(AIIntReader), current,
                newInstance =>
                {
                    Undo.RecordObject(_enemy, "Change " + label);
                    setter((AIIntReader)newInstance);
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
        }

        void DrawEffectsList(List<EffectData> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var item = list[i];
                        string label = item != null && !string.IsNullOrEmpty(item.Label)
                            ? item.Label
                            : (item != null ? item.GetType().Name : "(null)");
                        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (PolymorphicPicker.DrawClearButton())
                        {
                            Undo.RecordObject(_enemy, "Remove Effect Group");
                            list.RemoveAt(i);
                            EditorUtility.SetDirty(_enemy);
                            NotifyChanged();
                            return;
                        }
                    }
                    if (list[i] != null)
                        DrawEffectData(list[i], "Behavior.Effects.$" + i);
                }
            }
        }

        /// <summary>
        /// Renders one <see cref="EffectData"/> with custom pickers for its three
        /// polymorphic fields whose base types all carry [HideReferenceObjectPicker]:
        /// <see cref="EffectData.PreConditions"/>, <see cref="EffectData.Effects"/>,
        /// <see cref="EffectData.TargetSelector"/>. Inner fields of each item are still
        /// drawn by Odin via <see cref="DrawPolymorphicListItems"/>.
        /// </summary>
        void DrawEffectData(EffectData item, string basePath)
        {
            DrawOdinProp(basePath + ".Label");

            EditorGUILayout.Space(4);

            // PreConditions
            EditorGUILayout.LabelField("PreConditions (AND)", EditorStyles.miniBoldLabel);
            if (item.PreConditions == null) item.PreConditions = new List<BasePreCondition>();
            DrawPolymorphicListItems(item.PreConditions, basePath + ".PreConditions", "PreCondition");
            PolymorphicPicker.DrawAddButton(
                "PreCondition", typeof(BasePreCondition), item.PreConditions,
                () => { EditorUtility.SetDirty(_enemy); NotifyChanged(); });

            EditorGUILayout.Space(4);

            // Effects (List<IEffect>)
            EditorGUILayout.LabelField("Effects", EditorStyles.miniBoldLabel);
            if (item.Effects == null) item.Effects = new List<IEffect>();
            DrawEffectListItems(item.Effects, basePath + ".Effects");
            PolymorphicPicker.DrawAddButton(
                "Effect", typeof(IEffect), item.Effects,
                () => { EditorUtility.SetDirty(_enemy); NotifyChanged(); });

            EditorGUILayout.Space(4);

            // Target override
            EditorGUILayout.LabelField("Target Override", EditorStyles.miniBoldLabel);
            PolymorphicPicker.DrawSingle(
                "Type", typeof(BaseEnemyTargetSelector), item.TargetSelector,
                newInstance =>
                {
                    Undo.RecordObject(_enemy, "Change Effect Target Selector");
                    item.TargetSelector = (BaseEnemyTargetSelector)newInstance;
                    EditorUtility.SetDirty(_enemy);
                    NotifyChanged();
                });
            if (item.TargetSelector != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp(basePath + ".TargetSelector");
                EditorGUI.indentLevel--;
            }
        }

        void DrawEffectListItems(IList list, string listRelativePath)
        {
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var item = list[i];
                        EditorGUILayout.LabelField(
                            item != null ? item.GetType().Name : "(null)",
                            EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (PolymorphicPicker.DrawClearButton())
                        {
                            Undo.RecordObject(_enemy, "Remove Effect");
                            list.RemoveAt(i);
                            EditorUtility.SetDirty(_enemy);
                            NotifyChanged();
                            return;
                        }
                    }

                    if (list[i] != null)
                    {
                        var effectPath = listRelativePath + ".$" + i;
                        DrawOdinProp(effectPath);
                        DrawReaderPickersForEffect(list[i], effectPath);
                    }
                }
            }
        }

        void DrawReaderPickersForEffect(object effect, string effectOdinPath)
        {
            var type = effect.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            bool hasFromReader = false;
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(DamageSource)
                    && (DamageSource)f.GetValue(effect) == DamageSource.FromReader)
                {
                    hasFromReader = true;
                    break;
                }
            }
            if (!hasFromReader) return;

            foreach (var f in fields)
            {
                if (f.FieldType != typeof(EffectIntReader)) continue;
                var current = (EffectIntReader)f.GetValue(effect);

                PolymorphicPicker.DrawSingle(
                    "Reader Type", typeof(EffectIntReader), current,
                    newInstance =>
                    {
                        Undo.RecordObject(_enemy, "Change Effect Reader");
                        f.SetValue(effect, newInstance);
                        EditorUtility.SetDirty(_enemy);
                        NotifyChanged();
                    });
            }
        }

        void DrawOdinProp(string relativePath)
        {
            var prop = _soTree.GetPropertyAtPath(_selectedPath + "." + relativePath);
            if (prop != null) prop.Draw();
            else EditorGUILayout.LabelField(relativePath, "(field not found)");
        }

        /// <summary>
        /// GenericMenu callbacks fire outside the IMGUI cycle — without an explicit repaint,
        /// the panel won't redraw to show the new state until the user moves the mouse over it.
        /// </summary>
        void NotifyChanged()
        {
            _body.MarkDirtyRepaint();
            _onChanged?.Invoke();
        }

        // ---- helpers -----------------------------------------------------

        void UpdateHeader()
        {
            if (_selected == null)
            {
                _header.text = "AI Node Inspector";
                _emptyHint.style.display = DisplayStyle.Flex;
            }
            else
            {
                _header.text = _selected.NodeName;
                _emptyHint.style.display = DisplayStyle.None;
            }
        }

        void EnsureTree()
        {
            if (_soTree != null) return;
            if (_enemy == null) return;
            _soTree = PropertyTree.Create(_enemy);
        }

        void DisposeTree()
        {
            _soTree?.Dispose();
            _soTree = null;
        }

        /// <summary>
        /// Walk the SO's PropertyTree to find the path whose value reference equals
        /// <paramref name="target"/>. Required because the AI tree topology is polymorphic
        /// and paths are not stable across edits.
        /// </summary>
        string FindPathTo(AIDecisionNode target)
        {
            if (_soTree == null || target == null) return null;
            foreach (var prop in _soTree.EnumerateTree(true))
            {
                var value = prop.ValueEntry?.WeakSmartValue;
                if (ReferenceEquals(value, target)) return prop.Path;
            }
            return null;
        }

        bool PathStillPointsToSelection()
        {
            if (_soTree == null || _selected == null || string.IsNullOrEmpty(_selectedPath)) return false;
            var prop = _soTree.GetPropertyAtPath(_selectedPath);
            return prop != null && ReferenceEquals(prop.ValueEntry?.WeakSmartValue, _selected);
        }
    }
}
