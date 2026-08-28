using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Polymorphic.Graph
{
    /// <summary>
    /// One block on the canvas: a title that says what the block <b>is</b>, a collapsible body
    /// that says what it <b>does</b>, and ports that exist only so edges have something to land
    /// on — see <see cref="PortAddDragManipulator"/> for why dragging one never wires a node.
    /// </summary>
    /// <remarks>
    /// The body is UIToolkit controls only, never IMGUI/Odin. Hosting IMGUI inside a GraphView
    /// node re-creates the focus bug recorded on <c>AIDecisionTreeGraphView</c>, where editing a
    /// polymorphic list inline made it reset. Selecting a node hands its path to the side panel;
    /// the panel edits, this only reads.
    /// </remarks>
    public sealed class BlockNodeView : Node
    {
        /// <summary>Node box height when expanded. Matches <see cref="BlockGraphLayout.NODE_HEIGHT"/>
        /// — the layout reserves that much row pitch regardless of collapse state, so a node is
        /// only ever allowed to <i>shrink</i> below it, never grow past it (see the height comment
        /// on <see cref="BlockGraphLayout"/>: growing past the row is what overlaps the node below;
        /// shrinking just leaves blank canvas, which is safe).</summary>
        const float CollapsedHeight = 40f;

        public BlockGraphNode Model { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        /// <summary>Fires when the user toggles collapse, so the host can persist it by
        /// <see cref="BlockGraphNode.Path"/> — <c>BlockGraphView.Rebuild()</c> throws this whole
        /// instance away on every edit, so nothing kept on the view itself would survive.</summary>
        public event Action<bool> OnCollapseChanged;

        /// <summary>Fires when a drag from <see cref="OutputPort"/> is released over empty canvas.
        /// Never fires for a drop over a node — see <see cref="PortAddDragManipulator"/>.</summary>
        public event Action OnAddMenuRequested;

        bool _collapsed;
        readonly VisualElement _body;
        readonly Button _collapseButton;

        public BlockNodeView(BlockGraphNode model, bool startCollapsed)
        {
            Model = model;
            title = model.Title;

            // Deletable drives the Del key; BlockGraphView.deleteSelection turns it into a data edit.
            // The root has no owner to detach from, so it stays undeletable.
            if (!model.CanRemove) capabilities &= ~Capabilities.Deletable;

            // Not movable: position means "where this sits in the data". Letting it be dragged
            // would imply the layout is authored, and it isn't — it's recomputed every rebuild.
            capabilities &= ~Capabilities.Movable;

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
            InputPort.portName = string.Empty;
            inputContainer.Add(InputPort);

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = string.Empty;
            outputContainer.Add(OutputPort);
            // Drag-to-add lives on top of the stock port, as an extra manipulator — it never builds
            // an Edge, so a node can't end up wired to another one even by accident (spec §6.4).
            OutputPort.AddManipulator(new PortAddDragManipulator(this));

            bool hasAddTargets = BlockGraphView.HasAddableMembers(model.Value?.GetType());
            if (model.Kind == BlockNodeKind.Root) InputPort.style.visibility = Visibility.Hidden;
            if (model.Children.Count == 0 && !hasAddTargets) OutputPort.style.visibility = Visibility.Hidden;

            titleContainer.style.backgroundColor = BlockPanelStyles.AccentOf(model.Kind);

            _collapseButton = new Button(ToggleCollapsed)
            {
                text = startCollapsed ? "▸" : "▾",
                style =
                {
                    width = 18, height = 16, fontSize = 9,
                    marginLeft = 2, marginRight = 2, marginTop = 2,
                    paddingLeft = 0, paddingRight = 0, paddingTop = 0, paddingBottom = 0,
                },
            };
            titleButtonContainer.Insert(0, _collapseButton);

            _body = new VisualElement();
            BuildBody(_body, model);
            mainContainer.Add(_body);

            style.width = BlockGraphLayout.NODE_WIDTH;
            style.overflow = Overflow.Hidden;

            _collapsed = startCollapsed;
            ApplyCollapsedState(raiseEvent: false);

            RefreshExpandedState();
            RefreshPorts();
        }

        static void BuildBody(VisualElement body, BlockGraphNode model)
        {
            var icon = BlockNodeDescription.TryGetIcon(model);
            if (icon != null)
            {
                var image = new Image
                {
                    sprite = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    style = { width = 28, height = 28, marginLeft = 6, marginTop = 2, marginBottom = 2 },
                };
                body.Add(image);
            }

            var subtitle = new Label(model.Subtitle)
            {
                style =
                {
                    fontSize = 10,
                    color = new Color(0.72f, 0.75f, 0.80f),
                    marginLeft = 6, marginRight = 6, marginTop = 2, marginBottom = 2,
                    unityFontStyleAndWeight = FontStyle.Italic,
                },
            };
            body.Add(subtitle);

            var kindTag = new Label(model.Kind.ToString().ToUpperInvariant())
            {
                style =
                {
                    fontSize = 8,
                    color = new Color(0.55f, 0.58f, 0.64f),
                    marginLeft = 6, marginBottom = 2,
                },
            };
            body.Add(kindTag);

            // The line that actually says what this block does — "+30 damage", "2 conditions ·
            // 1 effect", whatever DescribeFields reflects off it. Empty for nodes with nothing
            // to add beyond the title (rare — mostly plumbing containers).
            string detail = BlockNodeDescription.Describe(model);
            if (!string.IsNullOrEmpty(detail))
            {
                var detailLabel = new Label(detail)
                {
                    style =
                    {
                        fontSize = 10,
                        color = new Color(0.88f, 0.90f, 0.93f),
                        whiteSpace = WhiteSpace.Normal,
                        marginLeft = 6, marginRight = 6, marginBottom = 4,
                    },
                };
                body.Add(detailLabel);
            }
        }

        void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            ApplyCollapsedState(raiseEvent: true);
        }

        void ApplyCollapsedState(bool raiseEvent)
        {
            _body.style.display = _collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            _collapseButton.text = _collapsed ? "▸" : "▾";
            // Shrink only — never grow past BlockGraphLayout.NODE_HEIGHT, or this node would
            // overlap the one the layout placed below it.
            style.height = _collapsed ? CollapsedHeight : BlockGraphLayout.NODE_HEIGHT;
            if (raiseEvent) OnCollapseChanged?.Invoke(_collapsed);
        }

        internal void RaiseAddMenuRequested() => OnAddMenuRequested?.Invoke();
    }
}
