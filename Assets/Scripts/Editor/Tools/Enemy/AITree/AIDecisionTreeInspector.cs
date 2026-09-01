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

        /// <summary>Hijos de (nodo, slot) en orden de ejecución. Lo cablea el GraphView.</summary>
        public Func<AIDecisionNode, int, List<AIDecisionNode>> GetChildren { get; set; }

        /// <summary>(padre, slot, desde, hasta). Lo cablea el GraphView; se invoca fuera del draw IMGUI.</summary>
        public Action<AIDecisionNode, int, int, int> MoveChild { get; set; }

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

            _header = new Label("Inspector de nodo")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    marginBottom = 6,
                },
            };
            Root.Add(_header);

            _emptyHint = new Label("Seleccioná un nodo del grafo para editar sus parámetros.")
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
                    "No se encontró este nodo en el asset (¿quedó de una edición deshecha?). " +
                    "Cerrá y volvé a abrir el árbol; si persiste, borrá el nodo y crealo de nuevo.",
                    MessageType.Warning);
            }
            else
            {
                if (_selectedPath.StartsWith(nameof(EnemyDataSO.AIDetachedNodes), StringComparison.Ordinal))
                {
                    EditorGUILayout.HelpBox(
                        "Nodo suelto: no se ejecuta hasta conectarlo a la raíz. Se guarda igual para no perder el trabajo.",
                        MessageType.Info);
                    EditorGUILayout.Space(4);
                }

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

            bool drewAny = DrawChildOrder(_selected, null);
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
                            "Cambiar " + child.NiceName,
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
        /// Lista de hijos de un slot dinámico con ▲/▼. El movimiento se difiere a
        /// <c>EditorApplication.delayCall</c>: <c>Save</c> reemplaza las listas de hijos y no hay
        /// que hacerlo en medio del draw de Odin. Devuelve <c>true</c> si dibujó algo.
        /// </summary>
        bool DrawChildOrder(AIDecisionNode node, Action<int> drawExtraForChild)
        {
            var slots = AITreeTopology.SlotsOf(node);
            int slot = -1;
            for (int i = 0; i < slots.Count; i++) if (slots[i].IsDynamic) { slot = i; break; }
            if (slot < 0 || GetChildren == null) return false;

            var children = GetChildren(node, slot);
            EditorGUILayout.LabelField("Hijos (orden de ejecución)", EditorStyles.boldLabel);
            if (children == null || children.Count == 0)
            {
                EditorGUILayout.LabelField("Sin hijos conectados: usá el puerto \"+\" del nodo.", EditorStyles.miniLabel);
                EditorGUILayout.Space(6);
                return true;
            }

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1}. {(child != null ? child.NodeName : "(vacío)")}", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(24))) ScheduleMove(node, slot, i, i - 1);
                }
                using (new EditorGUI.DisabledScope(i == children.Count - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(24))) ScheduleMove(node, slot, i, i + 1);
                }
                EditorGUILayout.EndHorizontal();

                if (drawExtraForChild != null)
                {
                    EditorGUI.indentLevel++;
                    drawExtraForChild(i);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.Space(6);
            return true;
        }

        void ScheduleMove(AIDecisionNode node, int slot, int from, int to)
        {
            var move = MoveChild;
            if (move == null) return;
            EditorApplication.delayCall += () => move(node, slot, from, to);
        }

        /// <summary>
        /// Los hijos de Random se conectan por el puerto "Opciones"; acá se ordenan y se edita el
        /// peso de cada uno. <c>Options.$i.Weight</c> es la ruta Odin del elemento <c>i</c>, que
        /// tras cada commit coincide con el orden de los edges.
        /// </summary>
        void DrawRandomNode(AINode_Random node)
        {
            EditorGUILayout.HelpBox(
                "Cada turno elige un hijo al azar. El peso es relativo (2 y 1 = 2/3 y 1/3).",
                MessageType.None);

            int count = node.Options != null ? node.Options.Count : 0;
            DrawChildOrder(node, i =>
            {
                if (i < count) DrawOdinProp($"Options.${i}.Weight");
            });
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
            EditorGUILayout.LabelField("Selector de objetivo", EditorStyles.boldLabel);
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Tipo", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                Abs("TargetSelector"),
                v => node.TargetSelector = (BaseEnemyTargetSelector)v,
                "Cambiar selector de objetivo");

            EditorGUILayout.Space(8);

            // Conditions list (AND-evaluated)
            EditorGUILayout.LabelField("Condiciones (AND)", EditorStyles.boldLabel);
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
            EditorGUILayout.LabelField("Selector de objetivo", EditorStyles.boldLabel);
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Tipo", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                Abs("TargetSelector"),
                v => node.TargetSelector = (BaseEnemyTargetSelector)v,
                "Cambiar selector de objetivo");

            EditorGUILayout.Space(8);

            // Conditions list (AND-evaluated, looped each iteration)
            EditorGUILayout.LabelField("Condiciones (AND, por iteración)", EditorStyles.boldLabel);
            if (node.Conditions == null) node.Conditions = new List<BasePreCondition>();
            PolymorphicBlockDrawer.DrawPolymorphicListItems(_ctx, node.Conditions, Abs("Conditions"), "Condition");
            PolymorphicBlockDrawer.DrawAddButton(_ctx, "Condition", typeof(BasePreCondition), node.Conditions);

            EditorGUILayout.Space(8);

            // MaxIterations safeguard
            EditorGUILayout.LabelField("Tope de iteraciones", EditorStyles.boldLabel);
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
                "Tipo", typeof(EnemyActionBehavior), node.Behavior,
                newInstance => _ctx.Mutate(
                    "Cambiar behavior",
                    () => node.Behavior = (EnemyActionBehavior)newInstance));

            if (node.Behavior == null) return;
            var behavior = node.Behavior;

            EditorGUILayout.Space(8);

            // Trigger / phases
            EditorGUILayout.LabelField("Disparador / fases", EditorStyles.boldLabel);
            DrawOdinProp("Behavior.Trigger");
            DrawOdinProp("Behavior.AllowedPhases");

            EditorGUILayout.Space(6);

            // Action
            EditorGUILayout.LabelField("Acción", EditorStyles.boldLabel);
            DrawOdinProp("Behavior.ActionName");

            EditorGUILayout.Space(6);

            // Target Selector — same picker pattern as DrawIfNode
            EditorGUILayout.LabelField("Selector de objetivo", EditorStyles.boldLabel);
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Tipo", typeof(BaseEnemyTargetSelector), behavior.TargetSelector,
                Abs("Behavior.TargetSelector"),
                v => behavior.TargetSelector = (BaseEnemyTargetSelector)v,
                "Cambiar selector de objetivo del behavior");

            EditorGUILayout.Space(6);

            // Effects (List<EffectData>) — EffectData is concrete + has [HideReferenceObjectPicker]
            EditorGUILayout.LabelField("Efectos (grupos con precondiciones)", EditorStyles.boldLabel);
            if (behavior.Effects == null) behavior.Effects = new List<EffectData>();
            PolymorphicBlockDrawer.DrawEffectDataList(
                _ctx, behavior.Effects, Abs("Behavior.Effects"), PolymorphicBlockDrawer.Options.Enemy);
            PolymorphicBlockDrawer.DrawAddButton(_ctx, "Effect Group", typeof(EffectData), behavior.Effects);
        }

        void DrawMoveNode(AINode_Move node)
        {
            // Target Selector (mismo patrón que DrawIfNode). Null = player.
            EditorGUILayout.LabelField("Selector de objetivo", EditorStyles.boldLabel);
            PolymorphicBlockDrawer.DrawSingleSlot(
                _ctx, "Tipo", typeof(BaseEnemyTargetSelector), node.TargetSelector,
                Abs("TargetSelector"),
                v => node.TargetSelector = (BaseEnemyTargetSelector)v,
                "Cambiar selector de objetivo");

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Pasos máximos", EditorStyles.boldLabel);
            DrawIntReaderField("MaxSteps", node.MaxSteps,
                r => { node.MaxSteps = r; });
            if (node.MaxSteps != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("MaxSteps");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Distancia deseada", EditorStyles.boldLabel);
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
            EditorGUILayout.LabelField("Legado (fallback si Distancia deseada está vacía)", EditorStyles.miniBoldLabel);
            DrawOdinProp("StopAdjacent");
        }

        void DrawKeepDistanceNode(AINode_KeepDistance node)
        {
            EditorGUILayout.LabelField("Pasos máximos", EditorStyles.boldLabel);
            DrawIntReaderField("MaxSteps", node.MaxSteps,
                r => { node.MaxSteps = r; });
            if (node.MaxSteps != null)
            {
                EditorGUI.indentLevel++;
                DrawOdinProp("MaxSteps");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Distancia ideal", EditorStyles.boldLabel);
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
                    "Cambiar " + label,
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
                _header.text = "Inspector de nodo";
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
