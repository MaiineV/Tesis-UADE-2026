using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Polymorphic.Graph
{
    /// <summary>
    /// Places every node of a <see cref="BlockGraphModel"/> — column by depth (so the flow reads
    /// left to right), row by a tidy-tree pass that centres each parent on its children.
    /// </summary>
    /// <remarks>
    /// Pure and total: same model in, same rects out, no I/O and no persistence. There is no layout
    /// sidecar on purpose. Positions here mean "where this sits in the data", so saving them would
    /// let the canvas drift out of sync with list order — and a graph that misreports execution
    /// order is worse than no graph. (The AI tree keeps its sidecar because there the positions are
    /// real authoring intent; here they are derived.)
    /// </remarks>
    public static class BlockGraphLayout
    {
        public const float NODE_WIDTH = 210f;

        /// <summary>
        /// Row pitch. Must be at least the node's real rendered height or boxes overlap on screen —
        /// <see cref="BlockNodeView"/> pins its height to this so the two can't drift apart. A
        /// GraphView node auto-sizes to title + subtitle + kind tag + port rows, which is well over
        /// the 62px this used to assume.
        /// </summary>
        public const float NODE_HEIGHT = 104f;

        public const float H_SPACING = 130f;
        public const float V_SPACING = 34f;

        public static Dictionary<BlockGraphNode, Vector2> Compute(BlockGraphModel.Result model)
        {
            var positions = new Dictionary<BlockGraphNode, Vector2>();
            if (model?.Root == null) return positions;

            float cursor = 0f;
            Place(model.Root, ref cursor, positions);
            return positions;
        }

        /// <summary>
        /// Post-order: leaves consume the next free row, parents land on the midpoint of their
        /// first and last child. Returns the row this node settled on.
        /// </summary>
        static float Place(BlockGraphNode node, ref float cursor, Dictionary<BlockGraphNode, Vector2> positions)
        {
            float x = node.Column * (NODE_WIDTH + H_SPACING);

            if (node.Children.Count == 0)
            {
                float y = cursor;
                cursor += NODE_HEIGHT + V_SPACING;
                positions[node] = new Vector2(x, y);
                return y;
            }

            float firstChildY = 0f;
            float lastChildY = 0f;
            for (int i = 0; i < node.Children.Count; i++)
            {
                float childY = Place(node.Children[i], ref cursor, positions);
                if (i == 0) firstChildY = childY;
                lastChildY = childY;
            }

            float centred = (firstChildY + lastChildY) * 0.5f;
            positions[node] = new Vector2(x, centred);
            return centred;
        }
    }
}
