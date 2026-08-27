using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Effects;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;
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
    /// <para>
    /// The tree, undo and the EffectData drawers live in
    /// <see cref="Rollgeon.Editor.Tools.Polymorphic"/> — this class keeps only what is
    /// AI-specific: node selection, path resolution and the per-subtype layouts.
    /// </para>
    /// </summary>
    public sealed class AIDecisionTreeInspector
    {
        public VisualElement Root { get; }

        readonly Action _onChanged;
        readonly PolymorphicAuthoringContext _ctx = new PolymorphicAuthoringContext();
        EnemyDataSO _enemy;
        AIDecisionNode _selected;
        string _selectedPath;
        Vector2 _bodyScroll;

        Label _header;
        Label _emptyHint;
        IMGUIContainer _body;

        public AIDecisionTreeInspector(Action onChanged)
        {
            _onChanged = onChanged;
            _ctx.Changed += NotifyChanged;

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
            _ctx.Bind(enemy);
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

            _selectedPath = _ctx.FindPathTo(node);
            UpdateHeader();
            _body.MarkDirtyRepaint();
        }

        public void RefreshIfShowing(AIDecisionNode node)
        {
            if (_selected == node) _body.MarkDirtyRepaint();
        }

        public void Dispose()
        {
            _ctx.Dispose();
        }

        // ---- IMGUI body --------------------------------------------------

        void DrawBody()
        {
            if (_selected == null || _enemy == null) return;
            if (_ctx.Tree == null) return;

            _ctx.UpdateTree();

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
            if (string.IsNullOrEmpty(_selectedPath) || !_ctx.PathPointsTo(_selectedPath, _selected))
                _selectedPath = _ctx.FindPathTo(_selected);
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
                    case AINode_Random randomNode:
                        DrawRandomNode(randomNode);
                        break;
                    default:
                        DrawDefault();
                        break;
                }

                _ctx.ApplyChanges();

                if (EditorGUI.EndChangeCheck() || GUI.changed)
                {
                    EditorUtility.SetDirty(_enemy);
                    _onChanged?.Invoke();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ---- per-subtype drawers -----------------------------------------

        /// <summary>
        /// Dibuja todos los miembros serializados del nodo salvo la topología (hijos), que se
        /// edita por los puertos del grafo. Reemplaza las listas de campos a mano por tipo: con
        /// 60 tipos de nodo, la lista se desactualizaba (TelegraphMark listaba un campo que no
        /// existía y omitía cinco) y 50 tipos quedaban sin UI.
        /// </summary>
        /// <remarks>
        /// Un slot polimórfico nulo (ej. <c>AINode_RotateBlock.DirectedIndex : AIIntReader</c>)
        /// no muestra picker propio porque las bases llevan <c>[HideReferenceObjectPicker]</c>
        /// (§13.6.1) — se le antepone el picker del proyecto, igual que en los drawers custom.
        /// </remarks>
        void DrawDefault()
        {
            var nodeProp = _ctx.At(_selectedPath);
            if (nodeProp == null) return;

            bool drewAny = false;
            for (int i = 0; i < nodeProp.Children.Count; i++)
            {
                var child = nodeProp.Children[i];
                var entry = child.ValueEntry;
                if (entry == null)
                {
                    // Grupos / métodos: Odin decide qué mostrar.
                    child.Draw();
                    drewAny = true;
                    continue;
                }

                var declared = entry.BaseValueType;
                if (AITreeTopology.IsTopologyMember(declared)) continue;

                if (declared.IsAbstract || declared.IsInterface)
                {
                    var current = entry.WeakSmartValue;
                    PolymorphicPicker.DrawSingle(
                        child.NiceName, declared, current,
                        newInstance => _ctx.Mutate(
                            "Change " + child.NiceName,
                            () => entry.WeakSmartValue = newInstance));
                    if (entry.WeakSmartValue != null)
                    {
                        EditorGUI.indentLevel++;
                        child.Draw();
                        EditorGUI.indentLevel--;
                    }
                    drewAny = true;
                    continue;
                }

                child.Draw();
                drewAny = true;
            }

            if (!drewAny)
            {
                EditorGUILayout.HelpBox(
                    "Este nodo no tiene parámetros: su comportamiento lo definen sus hijos.",
                    MessageType.Info);
            }
        }

        /// <summary>
        /// Los hijos de Random se conectan por el puerto "Options"; acá solo se edita el peso de
        /// cada uno. <c>Options.$i.Weight</c> es la ruta Odin del elemento <c>i</c>.
        /// </summary>
        void DrawRandomNode(AINode_Random node)
        {
            EditorGUILayout.HelpBox(
                "Cada turno elige un hijo al azar. El peso es relativo (2 y 1 = 2/3 y 1/3). " +
                "Los hijos se conectan por el puerto Options; el orden es el de conexión.",
                MessageType.None);

            if (node.Options == null || node.Options.Count == 0)
            {
                EditorGUILayout.LabelField("Sin hijos conectados.", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < node.Options.Count; i++)
            {
                var child = node.Options[i].Node;
                EditorGUILayout.LabelField(child != null ? child.NodeName : "(sin conectar)",
                    EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                DrawOdinProp($"Options.${i}.Weight");
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }
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
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Type", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                Abs("TargetSelector"),
                v => node.TargetSelector = (BaseEnemyTargetSelector)v,
                "Change Target Selector");

            EditorGUILayout.Space(8);

            // Conditions list (AND-evaluated)
            EditorGUILayout.LabelField("Conditions (AND)", EditorStyles.boldLabel);
            if (node.Conditions == null) node.Conditions = new List<BasePreCondition>();
            PolymorphicBlockDrawer.DrawPolymorphicListItems(_ctx, node.Conditions, Abs("Conditions"), "Condition");
            PolymorphicBlockDrawer.DrawAddButton(_ctx, "Condition", typeof(BasePreCondition), node.Conditions);
        }

        /// <summary>
        /// AINode_While mirrors AINode_If's condition+target authoring (same picker pattern),
        /// pero con un único <c>Body</c> child y un campo <c>MaxIterations</c> safeguard.
        /// </summary>
        void DrawWhileNode(AINode_While node)
        {
            // Target Selector (mismo patrón que DrawIfNode)
            EditorGUILayout.LabelField("Target Selector", EditorStyles.boldLabel);
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Type", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                Abs("TargetSelector"),
                v => node.TargetSelector = (BaseEnemyTargetSelector)v,
                "Change Target Selector");

            EditorGUILayout.Space(8);

            // Conditions list (AND-evaluated, looped each iteration)
            EditorGUILayout.LabelField("Conditions (AND, looped)", EditorStyles.boldLabel);
            if (node.Conditions == null) node.Conditions = new List<BasePreCondition>();
            PolymorphicBlockDrawer.DrawPolymorphicListItems(_ctx, node.Conditions, Abs("Conditions"), "Condition");
            PolymorphicBlockDrawer.DrawAddButton(_ctx, "Condition", typeof(BasePreCondition), node.Conditions);

            EditorGUILayout.Space(8);

            // MaxIterations safeguard
            EditorGUILayout.LabelField("Safeguard", EditorStyles.boldLabel);
            DrawOdinProp("MaxIterations");
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
                newInstance => _ctx.Mutate(
                    "Change Behavior",
                    () => node.Behavior = (EnemyActionBehavior)newInstance));

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
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Type", typeof(BaseEnemyTargetSelector), behavior.TargetSelector,
                Abs("Behavior.TargetSelector"),
                v => behavior.TargetSelector = (BaseEnemyTargetSelector)v,
                "Change Behavior Target Selector");

            EditorGUILayout.Space(6);

            // Effects (List<EffectData>) — EffectData is concrete + has [HideReferenceObjectPicker]
            EditorGUILayout.LabelField("Effect Pipeline", EditorStyles.boldLabel);
            if (behavior.Effects == null) behavior.Effects = new List<EffectData>();
            PolymorphicBlockDrawer.DrawEffectDataList(
                _ctx, behavior.Effects, Abs("Behavior.Effects"), PolymorphicBlockDrawer.Options.Enemy);
            PolymorphicBlockDrawer.DrawAddButton(_ctx, "Effect Group", typeof(EffectData), behavior.Effects);
        }

        void DrawMoveNode(AINode_Move node)
        {
            // Target Selector (mismo patrón que DrawIfNode). Null = player.
            EditorGUILayout.LabelField("Target Selector", EditorStyles.boldLabel);
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Type", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                Abs("TargetSelector"),
                v => node.TargetSelector = (BaseEnemyTargetSelector)v,
                "Change Target Selector");

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
                newInstance => _ctx.Mutate(
                    "Change " + label,
                    () => setter((AIIntReader)newInstance)));
        }

        /// <summary>
        /// Absolute Odin path for a field of the selected node. The shared drawers take absolute
        /// paths — relativity to the selection is this inspector's concern, not theirs.
        /// </summary>
        string Abs(string relativePath) => _selectedPath + "." + relativePath;

        void DrawOdinProp(string relativePath) => _ctx.Draw(Abs(relativePath));

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

    }
}
