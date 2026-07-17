using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Polymorphic.Graph
{
    /// <summary>
    /// One block on the canvas: a title, its concrete type, and ports that exist only so edges have
    /// something to land on.
    /// </summary>
    /// <remarks>
    /// The body is labels and nothing else. Hosting IMGUI/Odin inside a GraphView node re-creates
    /// the focus bug recorded on <c>AIDecisionTreeGraphView</c>, where editing a polymorphic list
    /// inline made it reset. Selecting a node hands its path to the side panel; the panel edits.
    /// </remarks>
    public sealed class BlockNodeView : Node
    {
        public BlockGraphNode Model { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        public BlockNodeView(BlockGraphNode model)
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

            if (model.Kind == BlockNodeKind.Root) InputPort.style.visibility = Visibility.Hidden;
            if (model.Children.Count == 0) OutputPort.style.visibility = Visibility.Hidden;

            titleContainer.style.backgroundColor = BlockPanelStyles.AccentOf(model.Kind);

            var subtitle = new Label(model.Subtitle)
            {
                style =
                {
                    fontSize = 10,
                    color = new Color(0.72f, 0.75f, 0.80f),
                    marginLeft = 6, marginRight = 6, marginTop = 2, marginBottom = 4,
                    unityFontStyleAndWeight = FontStyle.Italic,
                },
            };
            mainContainer.Add(subtitle);

            var kindTag = new Label(model.Kind.ToString().ToUpperInvariant())
            {
                style =
                {
                    fontSize = 8,
                    color = new Color(0.55f, 0.58f, 0.64f),
                    marginLeft = 6, marginBottom = 4,
                },
            };
            mainContainer.Add(kindTag);

            style.width = BlockGraphLayout.NODE_WIDTH;
            // Pin the height to the pitch the layout assumes. Left to auto-size, a node grows past
            // its allotted row (long titles wrap) and overlaps the one below — and the layout has no
            // way to know, because it runs before anything is measured.
            style.height = BlockGraphLayout.NODE_HEIGHT;
            style.overflow = Overflow.Hidden;

            RefreshExpandedState();
            RefreshPorts();
        }

    }
}
