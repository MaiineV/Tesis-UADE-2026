using UnityEngine;

namespace Rollgeon.UI.HUD.Inventory
{
    /// <summary>
    /// Math pura del grid rombo del inventario (mock "new inventory drawer"): filas de
    /// 5 rombos, las impares corridas media celda a la derecha, mínimo 20 celdas y
    /// crecimiento hacia abajo conservando el patrón. Posicionar las celdas
    /// EXCLUSIVAMENTE con esta matemática (sin LayoutGroup) — mismo criterio que
    /// <c>MinimapLayout</c> y <c>ChestReelMath</c>.
    /// </summary>
    /// <remarks>
    /// El rombo es NewUI_5/6/8/11 (30 px) a 3× point-filter = 90. El pitch vertical es
    /// menor que la celda porque las puntas de los rombos se encastran entre filas.
    /// Posiciones relativas al rect del grid (anchor/pivot top-left); cada celda con
    /// pivot centrado.
    /// </remarks>
    public static class InventoryDiamondLayout
    {
        public const int Cols = 5;
        public const int MinCells = 20;

        // Pixel art: mantener múltiplos enteros del sprite de 30 px.
        public const float DiamondSize = 90f;
        public const float ColPitch = 90f;
        public const float RowPitch = 78f;
        public const float RowStagger = ColPitch / 2f;

        public const float PanelPadding = 36f;
        public const float TitleHeight = 78f;
        public const float SectionGap = 28f;

        public static readonly float GridWidth = Cols * ColPitch + RowStagger;
        public static readonly float PanelWidth = GridWidth + 2f * PanelPadding;

        /// <summary>Celdas a dibujar: mínimo 20, y la última fila siempre se rellena.</summary>
        public static int VisibleCells(int itemCount)
        {
            int cells = Rows(itemCount) * Cols;
            return Mathf.Max(MinCells, cells);
        }

        public static int Rows(int itemCount)
        {
            int minRows = MinCells / Cols;
            if (itemCount <= 0) return minRows;
            int rows = (itemCount + Cols - 1) / Cols;
            return Mathf.Max(minRows, rows);
        }

        /// <summary>
        /// Centro de la celda <paramref name="index"/> relativo al top-left del grid.
        /// Filas impares corridas <see cref="RowStagger"/> a la derecha.
        /// </summary>
        public static Vector2 CellPosition(int index)
        {
            int row = index / Cols;
            int col = index % Cols;
            float x = DiamondSize / 2f + col * ColPitch + (row % 2 == 1 ? RowStagger : 0f);
            float y = -(DiamondSize / 2f + row * RowPitch);
            return new Vector2(x, y);
        }

        public static float GridHeight(int rows)
            => DiamondSize + (rows - 1) * RowPitch;

        public static float PanelHeight(int rows)
            => PanelPadding + TitleHeight + SectionGap + GridHeight(rows) + PanelPadding;
    }
}
