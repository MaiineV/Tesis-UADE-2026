using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Polymorphic.Graph
{
    /// <summary>
    /// Read-only canvas over an Odin-serialized asset: hooks → effect groups → conditions and
    /// effects, flowing left to right, with nested chains hanging off their parent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The canvas navigates; the panel edits.</b> Selecting a node reports its Odin path via
    /// <see cref="OnNodeSelected"/> and the host draws it. Nothing is edited here.
    /// </para>
    /// <para>
    /// <b>Nothing is connectable and nothing is draggable</b>, because there is no authored topology
    /// to author: every edge comes from a field or a list index. Ports exist so edges have anchors.
    /// Adding a block means adding a list element, not dropping a node on empty canvas — so there is
    /// no node-creation search window either.
    /// </para>
    /// </remarks>
    public sealed class BlockGraphView : GraphView
    {
        readonly Dictionary<BlockGraphNode, BlockNodeView> _views = new Dictionary<BlockGraphNode, BlockNodeView>();
        readonly Label _emptyHint;

        UnityEngine.Object _asset;
        PolymorphicAuthoringContext _ctx;
        BlockGraphModel.Result _model;
        bool _suppressSelectionEvents;

        /// <summary>Fires with the selected node's absolute Odin path, or null when cleared.</summary>
        public event Action<BlockGraphNode> OnNodeSelected;

        /// <summary>Fires after a structural edit made from the canvas, so hosts can repaint.</summary>
        public event Action OnStructureChanged;

        public BlockGraphView()
        {
            style.flexGrow = 1;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new RightClickPanManipulator());
            // Deliberately no SelectionDragger and no RectangleSelector: positions are derived, so
            // moving a node would be a lie, and box-select only exists to move things.

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            _emptyHint = new Label("Select an asset to see its effect flow.")
            {
                style =
                {
                    position = Position.Absolute,
                    left = 12, top = 10,
                    color = new Color(0.62f, 0.65f, 0.70f),
                },
            };
            Add(_emptyHint);
        }

        /// <summary>Ports are anchors, never connection candidates — nothing here is user-wired.</summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            => new List<Port>();

        public void Bind(UnityEngine.Object asset, PolymorphicAuthoringContext ctx = null)
        {
            _asset = asset;
            _ctx = ctx;
            Rebuild();
        }

        // ---- structural editing from the canvas ----------------------------

        /// <summary>
        /// Right-click menu: add to any list this block owns, or remove the block itself.
        /// </summary>
        /// <remarks>
        /// This is where structure is edited, because the panel deliberately doesn't show the lists
        /// — their contents are already nodes on the canvas, and drilling into a serialised list to
        /// add one entry is the thing this tool exists to replace.
        /// </remarks>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            var node = (evt.target as BlockNodeView)
                       ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<BlockNodeView>();
            if (node == null || _ctx == null) return;

            AppendAddItems(evt.menu, node);

            if (node.Model.CanRemove)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction($"Remove '{node.Model.Title}'", _ => Remove(node.Model));
            }
        }

        void AppendAddItems(DropdownMenu menu, BlockNodeView node)
        {
            var value = node.Model.Value;
            if (value == null) return;
            var type = value.GetType();

            foreach (var member in PolymorphicMemberScanner.Scan(type))
            {
                if (!member.IsList) continue;
                if (!(member.Field.GetValue(value) is IList list)) continue;

                foreach (var concrete in PolymorphicPicker.ConcreteSubtypesOf(member.BaseType))
                {
                    var capturedList = list;
                    var capturedType = concrete;
                    var label = member.Title;
                    menu.AppendAction(
                        $"Add {label}/{concrete.Name}",
                        _ => Add(capturedList, capturedType, label));
                }
            }

            foreach (var member in PolymorphicMemberScanner.BlockMembersOf(type))
            {
                if (!member.IsList) continue;
                if (!(member.Field.GetValue(value) is IList list)) continue;

                var capturedList = list;
                var capturedType = member.BaseType;
                var label = member.Title;
                menu.AppendAction($"Add {label}/{member.BaseType.Name}",
                    _ => Add(capturedList, capturedType, label));
            }
        }

        void Add(IList list, Type concrete, string label)
        {
            _ctx.Mutate($"Add {label}", () => list.Add(Activator.CreateInstance(concrete)));
            OnStructureChanged?.Invoke();
            Rebuild();
        }

        void Remove(BlockGraphNode node)
        {
            var member = node.SourceMember.Value;
            _ctx.Mutate($"Remove {node.Title}", () =>
            {
                if (node.SourceIndex >= 0)
                {
                    if (member.Field.GetValue(node.Owner) is IList list
                        && node.SourceIndex < list.Count)
                        list.RemoveAt(node.SourceIndex);
                }
                else
                {
                    member.Field.SetValue(node.Owner, null);
                }
            });
            OnNodeSelected?.Invoke(null);
            OnStructureChanged?.Invoke();
            Rebuild();
        }

        /// <summary>
        /// Throw the canvas away and re-project it from the data. Cheap enough at these sizes
        /// (CH_Warrior, the biggest asset in the project, is 43 nodes) and it's the only way the
        /// graph can't drift from list order.
        /// </summary>
        public void Rebuild()
        {
            _suppressSelectionEvents = true;
            try
            {
                ClearSelection();
                DeleteElements(graphElements.ToList());
                _views.Clear();

                _model = BlockGraphModel.Build(_asset);
                _emptyHint.style.display = _model?.Root == null ? DisplayStyle.Flex : DisplayStyle.None;
                if (_model?.Root == null) return;

                var positions = BlockGraphLayout.Compute(_model);

                foreach (var node in _model.AllNodes)
                {
                    var view = new BlockNodeView(node);
                    var p = positions.TryGetValue(node, out var xy) ? xy : Vector2.zero;
                    view.SetPosition(new Rect(p, Vector2.zero));
                    AddElement(view);
                    _views[node] = view;
                }

                foreach (var node in _model.AllNodes)
                {
                    if (!_views.TryGetValue(node, out var parentView)) continue;
                    foreach (var child in node.Children)
                    {
                        if (!_views.TryGetValue(child, out var childView)) continue;
                        var edge = parentView.OutputPort.ConnectTo(childView.InputPort);
                        edge.capabilities &= ~Capabilities.Deletable;
                        edge.capabilities &= ~Capabilities.Selectable;
                        AddElement(edge);
                    }
                }
            }
            finally
            {
                _suppressSelectionEvents = false;
            }
        }

        /// <summary>Frame the whole graph. Called by the host after a bind.</summary>
        public void FrameGraph()
        {
            if (_views.Count == 0) return;
            schedule.Execute(() => FrameAll()).ExecuteLater(16);
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            if (_suppressSelectionEvents) return;
            if (selectable is BlockNodeView view) OnNodeSelected?.Invoke(view.Model);
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            if (_suppressSelectionEvents) return;
            OnNodeSelected?.Invoke(null);
        }
    }
}
