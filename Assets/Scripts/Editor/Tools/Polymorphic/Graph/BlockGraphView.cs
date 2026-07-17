using System;
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
        BlockGraphModel.Result _model;
        bool _suppressSelectionEvents;

        /// <summary>Fires with the selected node's absolute Odin path, or null when cleared.</summary>
        public event Action<BlockGraphNode> OnNodeSelected;

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

        public void Bind(UnityEngine.Object asset)
        {
            _asset = asset;
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
