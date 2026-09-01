using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Editor.Tools.Polymorphic.Graph;
using Rollgeon.Entities;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Canvas that renders the <see cref="EnemyDataSO.AIRoot"/> (más los subárboles sueltos de
    /// <see cref="EnemyDataSO.AIDetachedNodes"/>) as a graph and writes changes back through
    /// <see cref="AITreeSerializer.Commit"/>. Inline parameter edits happen in the side panel
    /// exposed by <see cref="Inspector"/> — keeping IMGUI/Odin out of the GraphView Node body
    /// avoids the polymorphic-picker focus issues that caused condition lists to reset when
    /// edited inline.
    /// </summary>
    /// <remarks>
    /// La validación no bloquea: solo un <see cref="IssueSeverity.Error"/> (ciclo, multi-padre)
    /// impide escribir, y los ciclos se hacen imposibles en <see cref="GetCompatiblePorts"/>.
    /// Los avisos se muestran como badge en el nodo y en el status.
    /// </remarks>
    public sealed class AIDecisionTreeGraphView : GraphView
    {
        const string UndoLabel = "Editar árbol de IA";

        public AIDecisionTreeInspector Inspector { get; }

        EnemyDataSO _enemy;
        GraphSnapshot _snap;
        readonly Dictionary<AIDecisionNode, AIDecisionNodeView> _views = new Dictionary<AIDecisionNode, AIDecisionNodeView>();
        AINodeSearchProvider _searchProvider;
        EditorWindow _hostWindow;
        Label _statusLabel;
        Label _builderBanner;
        bool _suppressChange;

        public AIDecisionTreeGraphView(EditorWindow hostWindow)
        {
            _hostWindow = hostWindow;
            Inspector = new AIDecisionTreeInspector(OnInspectorChanged)
            {
                GetChildren = (node, slot) => _snap != null ? _snap.ChildrenOf(node, slot) : new List<AIDecisionNode>(),
                MoveChild = MoveChild,
            };

            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new RightClickPanManipulator());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
            nodeCreationRequest = OnNodeCreationRequest;

            _statusLabel = new Label();
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.left = 8;
            _statusLabel.style.top = 8;
            _statusLabel.style.color = new Color(1f, 0.5f, 0.5f);
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.maxWidth = 640;
            Add(_statusLabel);

            _builderBanner = new Label();
            _builderBanner.style.position = Position.Absolute;
            _builderBanner.style.left = 8;
            _builderBanner.style.top = 30;
            _builderBanner.style.color = new Color(0.95f, 0.72f, 0.20f);
            _builderBanner.style.whiteSpace = WhiteSpace.Normal;
            _builderBanner.style.maxWidth = 640;
            _builderBanner.style.display = DisplayStyle.None;
            Add(_builderBanner);
        }

        public void Bind(EnemyDataSO enemy)
        {
            // DeleteElements fires graphViewChanged for every removed node/edge. Without
            // suppression, the teardown would mutate the previous enemy's snapshot and
            // overwrite its AIRoot with an empty tree. Same risk for AddElement during
            // BuildViewsFromSnapshot — guard the whole rebuild.
            _suppressChange = true;
            try
            {
                _snap = null;
                _enemy = null;
                ClearSelection();
                DeleteElements(graphElements.ToList());
                _views.Clear();

                _enemy = enemy;
                _statusLabel.text = string.Empty;
                Inspector.Bind(enemy);
                UpdateBuilderBanner();
                if (_enemy == null) return;

                _snap = AITreeSerializer.Load(_enemy.AIRoot, _enemy.AIDetachedNodes);
                BuildViewsFromSnapshot();
                RelabelDynamicPorts();
                ApplyIssues(AITreeValidator.Validate(_snap));
            }
            finally
            {
                _suppressChange = false;
            }
        }

        void UpdateBuilderBanner()
        {
            if (_enemy != null && BossBuilderRegistry.TryGetBuilder(_enemy, out var menuPath))
            {
                _builderBanner.text = "⚙ " + BossBuilderRegistry.BannerText(menuPath);
                _builderBanner.style.display = DisplayStyle.Flex;
            }
            else
            {
                _builderBanner.style.display = DisplayStyle.None;
            }
        }

        // ---- snapshot ↔ views --------------------------------------------

        void BuildViewsFromSnapshot()
        {
            _views.Clear();

            var saved = AITreeLayoutSidecar.Load(_enemy, _snap);
            var auto = AITreeAutoLayout.Compute(_snap);

            foreach (var n in _snap.Nodes)
            {
                var view = new AIDecisionNodeView(n);
                Vector2 pos;
                if (saved != null && saved.TryGetValue(n, out var savedPos)) pos = savedPos;
                else if (auto != null && auto.TryGetValue(n, out var autoPos)) pos = autoPos;
                else pos = Vector2.zero;
                view.SetPosition(new Rect(pos, Vector2.zero));
                AddElement(view);
                _views[n] = view;
            }

            foreach (var e in _snap.Edges)
            {
                if (!_views.TryGetValue(e.Parent, out var parentView)) continue;
                if (!_views.TryGetValue(e.Child, out var childView)) continue;

                var outPort = FindFreeOutputForSlot(parentView, e.SlotIndex);
                if (outPort == null) continue;

                var edge = outPort.ConnectTo(childView.InputPort);
                AddElement(edge);
            }

            EnsureFreeDynamicPorts();
            RefreshRootIndicators();
        }

        void EnsureFreeDynamicPorts()
        {
            foreach (var view in _views.Values)
            {
                var slots = AITreeTopology.SlotsOf(view.Data);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!slots[i].IsDynamic) continue;
                    bool hasFree = false;
                    foreach (var p in view.OutputPorts)
                    {
                        if ((int)p.userData != i) continue;
                        if (!p.connected) { hasFree = true; break; }
                    }
                    if (!hasFree) view.AddOutputPortForSlot(slots[i], i);
                }
            }
        }

        /// <summary>Deja un único puerto libre por slot dinámico (desconectar un edge del medio dejaba dos).</summary>
        void PruneFreeDynamicPorts()
        {
            foreach (var view in _views.Values)
            {
                var slots = AITreeTopology.SlotsOf(view.Data);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!slots[i].IsDynamic) continue;
                    var free = new List<Port>();
                    foreach (var p in view.OutputPorts)
                        if ((int)p.userData == i && !p.connected) free.Add(p);
                    for (int k = 0; k < free.Count - 1; k++) view.RemoveOutputPort(free[k]);
                }
            }
        }

        static AIDecisionNode ChildOfPort(Port port)
        {
            foreach (var e in port.connections)
                if (e.input?.node is AIDecisionNodeView cv) return cv.Data;
            return null;
        }

        static float? WeightOf(AIDecisionNode parent, AIDecisionNode child)
        {
            if (!(parent is AINode_Random r) || r.Options == null) return null;
            foreach (var o in r.Options) if (o.Node == child) return o.Weight;
            return null;
        }

        /// <summary>
        /// Etiqueta cada puerto dinámico con el orden de ejecución real (y el peso en Random).
        /// Se basa en el edge conectado, no en la posición del puerto en el contenedor, así que
        /// es veraz aunque el orden visual quede desfasado.
        /// </summary>
        void RelabelDynamicPorts()
        {
            if (_snap == null) return;
            foreach (var view in _views.Values)
            {
                var slots = AITreeTopology.SlotsOf(view.Data);
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!slots[i].IsDynamic) continue;
                    var ordered = _snap.ChildrenOf(view.Data, i);
                    foreach (var p in view.OutputPorts)
                    {
                        if ((int)p.userData != i) continue;
                        var child = ChildOfPort(p);
                        if (child == null) { view.SetPortLabel(p, AITreeTopology.PortLabel(slots[i], null)); continue; }
                        int idx = ordered.IndexOf(child);
                        view.SetPortLabel(p, AITreeTopology.PortLabel(slots[i], idx >= 0 ? idx + 1 : (int?)null, WeightOf(view.Data, child)));
                    }
                }
            }
        }

        /// <summary>Acomoda los puertos de un nodo al orden de ejecución: conectados en orden, libres al final.</summary>
        void ReorderOutputPorts(AIDecisionNodeView view)
        {
            var slots = AITreeTopology.SlotsOf(view.Data);
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsDynamic) continue;
                var ordered = _snap.ChildrenOf(view.Data, i);
                var byChild = new Dictionary<AIDecisionNode, Port>();
                var free = new List<Port>();
                foreach (var p in view.OutputPorts)
                {
                    if ((int)p.userData != i) continue;
                    var child = ChildOfPort(p);
                    if (child == null) free.Add(p); else byChild[child] = p;
                }
                var sequence = new List<Port>();
                foreach (var c in ordered) if (byChild.TryGetValue(c, out var p)) sequence.Add(p);
                sequence.AddRange(free);
                view.ReorderOutputPorts(sequence);
            }
        }

        static Port FindFreeOutputForSlot(AIDecisionNodeView view, int slotIndex)
        {
            foreach (var p in view.OutputPorts)
            {
                if ((int)p.userData != slotIndex) continue;
                if (!p.connected) return p;
            }
            var slots = AITreeTopology.SlotsOf(view.Data);
            for (int i = 0; i < slots.Count; i++)
            {
                if (i != slotIndex) continue;
                if (!slots[i].IsDynamic) return null;
                return view.AddOutputPortForSlot(slots[i], slotIndex);
            }
            return null;
        }

        // ---- port compatibility ------------------------------------------

        /// <summary>
        /// Además de dirección y nodo distinto, filtra las conexiones que cerrarían un ciclo:
        /// un padre no puede colgar de su propio descendiente. Multi-padre ya lo impide
        /// <c>Port.Capacity.Single</c> en el puerto de entrada.
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            var startView = startPort.node as AIDecisionNodeView;
            foreach (var p in ports.ToList())
            {
                if (p == startPort) continue;
                if (p.direction == startPort.direction) continue;
                if (p.node == startPort.node) continue;

                if (_snap != null && startView != null && p.node is AIDecisionNodeView otherView)
                {
                    bool wouldCycle = startPort.direction == Direction.Output
                        ? _snap.IsAncestor(otherView.Data, startView.Data)   // start → other: other no puede ser ancestro de start
                        : _snap.IsAncestor(startView.Data, otherView.Data);  // other → start: other no puede ser descendiente de start
                    if (wouldCycle) continue;
                }
                compatible.Add(p);
            }
            return compatible;
        }

        // ---- selection → inspector --------------------------------------

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            UpdateInspectorFromSelection();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            UpdateInspectorFromSelection();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            UpdateInspectorFromSelection();
        }

        void UpdateInspectorFromSelection()
        {
            AIDecisionNodeView only = null;
            int count = 0;
            foreach (var s in selection)
            {
                if (s is AIDecisionNodeView nv) { only = nv; count++; }
            }
            Inspector.SetSelection(count == 1 ? only.Data : null);
        }

        void OnInspectorChanged()
        {
            // El inspector editó un campo inline: refrescar summaries, pesos en puertos y avisos
            // (agregar una condición o un efecto cambia el diagnóstico).
            foreach (var view in _views.Values) view.RefreshSummary();
            RelabelDynamicPorts();
            if (_snap != null) ApplyIssues(AITreeValidator.Validate(_snap));
        }

        // ---- context menu ------------------------------------------------

        /// <summary>
        /// Right-click context menu — "Marcar como raíz" y mover entre hermanos, on top of
        /// the default Cut/Copy/Paste/Delete entries provided by the base GraphView.
        /// </summary>
        /// <remarks>
        /// Three layers of target resolution because <c>evt.target</c> can be the node itself
        /// (right-click on the frame), a child element (click on title/port/label) — and Unity's
        /// <c>GetFirstAncestorOfType&lt;T&gt;()</c> starts at <c>parent</c>, not <c>self</c>.
        /// Selection fallback covers the rare case where the click bubble misses entirely.
        /// </remarks>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var nv = ResolveTargetNodeView(evt.target as VisualElement);
            if (nv != null && _snap != null)
            {
                bool alreadyRoot = (nv.Data == _snap.Root);
                evt.menu.AppendAction(
                    "Marcar como raíz",
                    _ => SetRoot(nv.Data),
                    alreadyRoot ? DropdownMenuAction.AlwaysDisabled : DropdownMenuAction.AlwaysEnabled);

                if (_snap.TryGetParent(nv.Data, out var parent, out var slot) &&
                    AITreeTopology.SlotsOf(parent)[slot].IsDynamic)
                {
                    var siblings = _snap.ChildrenOf(parent, slot);
                    int idx = siblings.IndexOf(nv.Data);
                    evt.menu.AppendAction("Subir entre hermanos", _ => MoveSibling(nv.Data, -1),
                        idx > 0 ? DropdownMenuAction.AlwaysEnabled : DropdownMenuAction.AlwaysDisabled);
                    evt.menu.AppendAction("Bajar entre hermanos", _ => MoveSibling(nv.Data, +1),
                        idx >= 0 && idx < siblings.Count - 1 ? DropdownMenuAction.AlwaysEnabled : DropdownMenuAction.AlwaysDisabled);
                }
                evt.menu.AppendSeparator();
            }
            base.BuildContextualMenu(evt);
        }

        AIDecisionNodeView ResolveTargetNodeView(VisualElement target)
        {
            if (target is AIDecisionNodeView direct) return direct;
            var ancestor = target?.GetFirstAncestorOfType<AIDecisionNodeView>();
            if (ancestor != null) return ancestor;
            // Fallback: Unity GraphView typically auto-selects the right-clicked node.
            foreach (var s in selection)
                if (s is AIDecisionNodeView sel) return sel;
            return null;
        }

        void SetRoot(AIDecisionNode node)
        {
            if (_snap == null || node == null || _snap.Root == node) return;
            _snap.Root = node;
            RefreshRootIndicators();
            MarkDirty();
        }

        void RefreshRootIndicators()
        {
            if (_snap == null) return;
            foreach (var kv in _views) kv.Value.SetIsRoot(kv.Key == _snap.Root);
        }

        // ---- child order -------------------------------------------------

        /// <summary>
        /// Reordena hermanos sin reconstruir el canvas: se mueven los puertos (los edges los
        /// siguen) y se re-etiquetan. La selección y el inspector quedan como estaban.
        /// </summary>
        public void MoveChild(AIDecisionNode parent, int slot, int fromIndex, int toIndex)
        {
            if (_snap == null || !_snap.MoveChild(parent, slot, fromIndex, toIndex)) return;
            MarkDirty();
            if (_views.TryGetValue(parent, out var view)) ReorderOutputPorts(view);
            RelabelDynamicPorts();
            Inspector.RefreshIfShowing(parent);
        }

        void MoveSibling(AIDecisionNode node, int delta)
        {
            if (_snap == null || !_snap.TryGetParent(node, out var parent, out var slot)) return;
            int idx = _snap.ChildrenOf(parent, slot).IndexOf(node);
            if (idx < 0) return;
            MoveChild(parent, slot, idx, idx + delta);
        }

        // ---- changes -----------------------------------------------------

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_suppressChange || _snap == null) return change;
            bool topologyChanged = false;

            if (change.elementsToRemove != null)
            {
                foreach (var el in change.elementsToRemove)
                {
                    if (el is AIDecisionNodeView nv)
                    {
                        _snap.Nodes.Remove(nv.Data);
                        _snap.Edges.RemoveAll(e => e.Parent == nv.Data || e.Child == nv.Data);
                        bool wasRoot = (_snap.Root == nv.Data);
                        // Sin raíz el resto pasa a "suelto"; promover un nodo al azar escondía
                        // el problema y podía dejar como raíz cualquier hoja.
                        if (wasRoot) _snap.Root = null;
                        _views.Remove(nv.Data);
                        if (wasRoot) RefreshRootIndicators();
                        topologyChanged = true;
                    }
                    else if (el is Edge edge)
                    {
                        var parentView = edge.output.node as AIDecisionNodeView;
                        var childView = edge.input.node as AIDecisionNodeView;
                        if (parentView != null && childView != null)
                        {
                            int slot = (int)edge.output.userData;
                            _snap.Edges.RemoveAll(e =>
                                e.Parent == parentView.Data &&
                                e.Child == childView.Data &&
                                e.SlotIndex == slot);
                            topologyChanged = true;
                        }
                    }
                }
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var parentView = edge.output.node as AIDecisionNodeView;
                    var childView = edge.input.node as AIDecisionNodeView;
                    if (parentView == null || childView == null) continue;

                    int slot = (int)edge.output.userData;
                    _snap.Edges.Add(new GraphSnapshot.Edge(parentView.Data, slot, childView.Data));
                    topologyChanged = true;
                }
            }

            if (topologyChanged)
            {
                EnsureFreeDynamicPorts();
                PruneFreeDynamicPorts();
                MarkDirty();
                // Después del commit: Save ya reconstruyó Options de Random, así los pesos son reales.
                RelabelDynamicPorts();
            }
            else if (change.movedElements != null && change.movedElements.Count > 0)
            {
                SaveLayoutOnly();
            }
            return change;
        }

        void SaveLayoutOnly()
        {
            if (_enemy == null || _snap == null) return;
            var positions = new Dictionary<AIDecisionNode, Vector2>();
            foreach (var kv in _views)
                positions[kv.Key] = kv.Value.GetPosition().position;
            AITreeLayoutSidecar.Save(_enemy, _snap, positions);
        }

        // ---- node creation via SearchWindow ------------------------------

        void OnNodeCreationRequest(NodeCreationContext ctx)
        {
            if (_searchProvider == null)
            {
                _searchProvider = ScriptableObject.CreateInstance<AINodeSearchProvider>();
                _searchProvider.GraphView = this;
                _searchProvider.OnSelect = (type, screenPos) =>
                {
                    var graphPos = ScreenToGraphPosition(screenPos);
                    SpawnNode(type, graphPos);
                };
            }
            SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), _searchProvider);
        }

        /// <summary>
        /// Standard Unity GraphView pattern for screen → graph local conversion. SearchWindow
        /// hands us screen coordinates; we need them in the canvas's pan/zoom-transformed
        /// local space. Three steps:
        ///   1. screen → window-local (subtract the EditorWindow's screen position).
        ///   2. window-local → root-local via ChangeCoordinatesTo (handles any toolbar offset).
        ///   3. root-local → graph-local via the GraphView's content transform.
        /// </summary>
        Vector2 ScreenToGraphPosition(Vector2 screenPos)
        {
            if (_hostWindow == null || _hostWindow.rootVisualElement == null)
                return contentViewContainer.WorldToLocal(screenPos);

            var root = _hostWindow.rootVisualElement;
            var parent = root.parent ?? root;
            Vector2 windowMouse = root.ChangeCoordinatesTo(
                parent,
                screenPos - _hostWindow.position.position);
            return contentViewContainer.WorldToLocal(windowMouse);
        }

        void SpawnNode(Type subtype, Vector2 position)
        {
            if (_enemy == null) return;
            if (!typeof(AIDecisionNode).IsAssignableFrom(subtype) || subtype.IsAbstract) return;

            var node = (AIDecisionNode)Activator.CreateInstance(subtype);
            _snap.Nodes.Add(node);
            if (_snap.Root == null) _snap.Root = node;

            var view = new AIDecisionNodeView(node);
            view.SetPosition(new Rect(position, Vector2.zero));
            AddElement(view);
            _views[node] = view;

            RefreshRootIndicators();
            MarkDirty();
            RelabelDynamicPorts();
        }

        // ---- save back to SO ---------------------------------------------

        void MarkDirty()
        {
            if (_enemy == null || _snap == null) return;

            var positions = new Dictionary<AIDecisionNode, Vector2>();
            foreach (var kv in _views)
                positions[kv.Key] = kv.Value.GetPosition().position;

            // Commit valida, graba el undo y recién después reconstruye árbol + sueltos.
            bool ok = AITreeSerializer.Commit(_enemy, _snap, UndoLabel, out var issues);
            ApplyIssues(issues);
            if (ok) AITreeLayoutSidecar.Save(_enemy, _snap, positions);
        }

        // ---- diagnostics ---------------------------------------------------

        void ApplyIssues(List<ValidationIssue> issues)
        {
            foreach (var v in _views.Values) v.ClearIssue();
            if (issues == null) { _statusLabel.text = string.Empty; return; }

            var perNode = new Dictionary<AIDecisionNode, (IssueSeverity worst, List<string> messages)>();
            foreach (var issue in issues)
            {
                if (issue.Node == null || !_views.ContainsKey(issue.Node)) continue;
                if (!perNode.TryGetValue(issue.Node, out var acc))
                    acc = (issue.Severity, new List<string>());
                if (issue.Severity < acc.worst) acc.worst = issue.Severity; // Error=0 es la peor
                acc.messages.Add(issue.Message);
                perNode[issue.Node] = acc;
            }
            foreach (var kv in perNode)
                _views[kv.Key].SetIssue(string.Join("\n", kv.Value.messages), kv.Value.worst);

            int errors = AITreeValidator.Count(issues, IssueSeverity.Error);
            int warnings = AITreeValidator.Count(issues, IssueSeverity.Warning);
            int infos = AITreeValidator.Count(issues, IssueSeverity.Info);
            if (errors + warnings + infos == 0)
            {
                _statusLabel.text = string.Empty;
                return;
            }

            var parts = new List<string>();
            if (errors > 0) parts.Add(errors == 1 ? "1 error" : $"{errors} errores");
            if (warnings > 0) parts.Add(warnings == 1 ? "1 aviso" : $"{warnings} avisos");
            if (infos > 0) parts.Add(infos == 1 ? "1 info" : $"{infos} info");

            var firstMessages = new List<string>();
            foreach (var issue in issues)
            {
                if (firstMessages.Count == 2) break;
                firstMessages.Add(issue.Node != null ? $"{issue.Node.NodeName}: {issue.Message}" : issue.Message);
            }
            _statusLabel.text = string.Join(" · ", parts) + " — " + string.Join(" · ", firstMessages);
            _statusLabel.style.color = errors > 0 ? new Color(1f, 0.5f, 0.5f)
                                     : warnings > 0 ? new Color(0.95f, 0.72f, 0.20f)
                                     : new Color(0.65f, 0.75f, 0.90f);
        }

        public void DisposeViews()
        {
            _views.Clear();
            Inspector?.Dispose();
        }
    }
}
