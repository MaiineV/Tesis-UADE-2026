using System;
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

        /// <summary>Glifos que entran en una línea del cuerpo, al ancho del nodo menos sus márgenes.</summary>
        const int CHARS_PER_LINE = 32;

        /// <summary>
        /// Cuántas líneas ocupa <paramref name="text"/> al envolverse por palabras.
        /// </summary>
        /// <remarks>
        /// Antes esto era <c>ceil(largo / CHARS_PER_LINE)</c>, y por eso algunas descripciones se
        /// cortaban: esa cuenta asume que el texto llena cada línea hasta el borde, pero el wrap real
        /// corta en el último espacio que entra. Una línea que termina antes para no partir una
        /// palabra desperdicia lo que sobra, así que el texto ocupa <b>más</b> líneas que las
        /// estimadas — nunca menos. Simular el corte por palabras elimina esa subestimación.
        /// <para>
        /// Sigue siendo una función pura sobre el string: no mide fuentes ni toca el sistema de GUI,
        /// así que el layout se mantiene determinístico y testeable fuera de un <c>OnGUI</c>.
        /// Una palabra más larga que la línea se parte, que es lo que hace el motor de texto.
        /// </para>
        /// </remarks>
        internal static int WrappedLineCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int lines = 1;
            int used = 0;

            foreach (var word in text.Split(' '))
            {
                int len = word.Length;

                // Palabra sola más larga que la línea: se parte, y cae en la última.
                if (len > CHARS_PER_LINE)
                {
                    if (used > 0) { lines++; used = 0; }
                    lines += (len - 1) / CHARS_PER_LINE;
                    used = len % CHARS_PER_LINE;
                    if (used == 0) used = CHARS_PER_LINE;
                    continue;
                }

                int needed = used == 0 ? len : used + 1 + len;
                if (needed <= CHARS_PER_LINE) { used = needed; continue; }

                lines++;
                used = len;
            }
            return lines;
        }

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
                height += WrappedLineCount(detail) * DETAIL_LINE_HEIGHT;

            return Mathf.Max(NODE_HEIGHT, height);
        }

        public static Dictionary<BlockGraphNode, Vector2> Compute(BlockGraphModel.Result model) =>
            Compute(model, null);

        /// <summary>
        /// Igual que <see cref="Compute(BlockGraphModel.Result)"/>, pero midiendo cada nodo con
        /// <paramref name="measuredHeight"/> en vez de estimarlo.
        /// </summary>
        /// <remarks>
        /// <see cref="HeightOf"/> es una estimación: suma chrome, subtítulo, tag y líneas de texto a
        /// partir de constantes. Sirve para colocar los nodos <b>antes</b> de que existan, pero se
        /// queda corta por poco — márgenes e interlineado reales que las constantes no capturan — y
        /// entonces el cuerpo se recorta.
        /// <para>
        /// El canvas la usa en la primera pasada y vuelve a llamar acá con las alturas ya resueltas
        /// por UIToolkit. Medir es lo único que no puede equivocarse; estimar siempre va a estar a un
        /// margen de distancia.
        /// </para>
        /// </remarks>
        public static Dictionary<BlockGraphNode, Vector2> Compute(
            BlockGraphModel.Result model, Func<BlockGraphNode, float> measuredHeight)
        {
            var positions = new Dictionary<BlockGraphNode, Vector2>();
            if (model?.Root == null) return positions;

            float Height(BlockGraphNode n)
            {
                if (measuredHeight == null) return HeightOf(n);
                float h = measuredHeight(n);
                // Un nodo que todavía no resolvió su geometría mide 0 o NaN: ahí vale la estimación.
                return h > 1f && !float.IsNaN(h) ? h : HeightOf(n);
            }

            float cursor = 0f;
            Place(model.Root, ref cursor, positions, Height);
            ResolveColumnOverlaps(model.AllNodes, positions, Height);
            return positions;
        }

        /// <summary>
        /// Post-order: leaves consume the next free row, parents land on the midpoint of their
        /// first and last child. Returns the row this node settled on.
        /// </summary>
        static float Place(BlockGraphNode node, ref float cursor, Dictionary<BlockGraphNode, Vector2> positions, Func<BlockGraphNode, float> height)
        {
            float x = node.Column * (NODE_WIDTH + H_SPACING);

            if (node.Children.Count == 0)
            {
                float y = cursor;
                cursor += height(node) + V_SPACING;
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

                float childY = Place(node.Children[i], ref cursor, positions, height);
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
        static void ResolveColumnOverlaps(List<BlockGraphNode> nodes, Dictionary<BlockGraphNode, Vector2> positions, Func<BlockGraphNode, float> height)
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
                    minY = y + height(node) + V_SPACING;
                }
            }
        }
    }
}
