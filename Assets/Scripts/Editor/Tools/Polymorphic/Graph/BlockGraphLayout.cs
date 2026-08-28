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
        /// Floor for <see cref="HeightOf"/> — a node with no icon and at most one line of body text
        /// still reserves this much, matching the old fixed box so plain nodes don't shrink and
        /// shuffle the canvas. Real rows grow past this from here on; they never used to.
        /// </summary>
        public const float NODE_HEIGHT = 104f;

        public const float H_SPACING = 130f;
        public const float V_SPACING = 34f;

        /// <summary>Header chrome common to every node: title bar + port row. Tuned so a node with
        /// no icon and one line of body text lands exactly on <see cref="NODE_HEIGHT"/>.</summary>
        const float HEADER_HEIGHT = 56f;
        const float SUBTITLE_HEIGHT = 16f;
        const float KIND_TAG_HEIGHT = 12f;
        const float BODY_PADDING = 6f;
        const float ICON_ROW_HEIGHT = 32f;
        const float DETAIL_LINE_HEIGHT = 14f;

        /// <summary>Rough glyph budget per wrapped line of body text at the node's fixed width and
        /// the body label's font size. Deliberately conservative (real text wraps a little earlier)
        /// so this only ever over-reserves, never clips.</summary>
        const float CHARS_PER_LINE = 32f;

        /// <summary>Extra breathing room where a group's children stop being an AND-gate and start
        /// being a sequence — see <see cref="IsLaneBoundary"/>.</summary>
        const float LANE_GAP = V_SPACING;

        /// <summary>
        /// The box height this node needs to show its icon (if any), subtitle, kind tag and body
        /// text without clipping. Pure function of the node's own data — titles, subtitles and the
        /// description <see cref="BlockNodeDescription"/> would render — never of live UI state, so
        /// it stays deterministic and callable from a background pass. <see cref="BlockNodeView"/>
        /// must use this exact value for its expanded height, or the two drift and boxes overlap.
        /// </summary>
        public static float HeightOf(BlockGraphNode node)
        {
            if (node == null) return NODE_HEIGHT;

            float height = HEADER_HEIGHT + SUBTITLE_HEIGHT + KIND_TAG_HEIGHT + BODY_PADDING;

            if (BlockNodeDescription.TryGetIcon(node) != null) height += ICON_ROW_HEIGHT;

            string detail = BlockNodeDescription.Describe(node);
            if (!string.IsNullOrEmpty(detail))
            {
                int lines = Mathf.Max(1, Mathf.CeilToInt(detail.Length / CHARS_PER_LINE));
                height += lines * DETAIL_LINE_HEIGHT;
            }

            return Mathf.Max(NODE_HEIGHT, height);
        }

        public static Dictionary<BlockGraphNode, Vector2> Compute(BlockGraphModel.Result model)
        {
            var positions = new Dictionary<BlockGraphNode, Vector2>();
            if (model?.Root == null) return positions;

            float cursor = 0f;
            Place(model.Root, ref cursor, positions);
            ResolveColumnOverlaps(model.AllNodes, positions);
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
                cursor += HeightOf(node) + V_SPACING;
                positions[node] = new Vector2(x, y);
                return y;
            }

            float firstChildY = 0f;
            float lastChildY = 0f;
            for (int i = 0; i < node.Children.Count; i++)
            {
                // Between the last condition and the first effect, an EffectData stops being an
                // AND-gate and starts being an ordered sequence — a wider gap makes that boundary
                // read as a boundary instead of one flat list of siblings.
                if (i > 0 && IsLaneBoundary(node.Children[i - 1], node.Children[i])) cursor += LANE_GAP;

                float childY = Place(node.Children[i], ref cursor, positions);
                if (i == 0) firstChildY = childY;
                lastChildY = childY;
            }

            float centred = (firstChildY + lastChildY) * 0.5f;
            positions[node] = new Vector2(x, centred);
            return centred;
        }

        static bool IsLaneBoundary(BlockGraphNode previous, BlockGraphNode next) =>
            previous.Kind == BlockNodeKind.Condition && next.Kind != BlockNodeKind.Condition;

        /// <summary>
        /// Pass one only reserves row space for leaves — a parent's row is the midpoint of its
        /// children, not a row of its own. That is correct as long as every box is the same height;
        /// once boxes vary (a root with an icon and identity fields, a condition whose description
        /// wraps to three lines) a tall parent can still land close enough to a neighbour from a
        /// different subtree to overlap it. This resolves that one column at a time: sort by the y
        /// pass one already chose, then only ever push a box <b>down</b> far enough to clear the one
        /// above it. Never moves a node up — that would undo pass one's centring — and a column's
        /// fix-up never touches another column's positions or any x, so it can't cascade sideways.
        /// </summary>
        static void ResolveColumnOverlaps(List<BlockGraphNode> nodes, Dictionary<BlockGraphNode, Vector2> positions)
        {
            var byColumn = new Dictionary<int, List<BlockGraphNode>>();
            foreach (var node in nodes)
            {
                if (!byColumn.TryGetValue(node.Column, out var list))
                    byColumn[node.Column] = list = new List<BlockGraphNode>();
                list.Add(node);
            }

            foreach (var column in byColumn.Values)
            {
                column.Sort((a, b) => positions[a].y.CompareTo(positions[b].y));

                float minY = float.NegativeInfinity;
                foreach (var node in column)
                {
                    var p = positions[node];
                    float y = Mathf.Max(p.y, minY);
                    positions[node] = new Vector2(p.x, y);
                    minY = y + HeightOf(node) + V_SPACING;
                }
            }
        }
    }
}
