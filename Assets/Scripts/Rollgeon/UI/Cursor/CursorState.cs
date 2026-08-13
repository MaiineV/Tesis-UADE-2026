namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Estado del cursor custom. Los valores son el índice del sprite en el
    /// sheet (Pointer-Sheet): 0 default, 1 click-vacío, 2 hover, 3 click-hover.
    /// </summary>
    public enum CursorState
    {
        Default = 0,
        ClickEmpty = 1,
        Hover = 2,
        ClickHover = 3,
    }

    /// <summary>
    /// Núcleo puro (sin dependencias de escena) que mapea el par
    /// (botón apretado, hay algo hovereable) al estado del cursor. Separado del
    /// MonoBehaviour para poder testearlo en EditMode.
    /// </summary>
    public static class CursorStateResolver
    {
        public static CursorState Resolve(bool pressed, bool hoverable)
        {
            if (pressed) return hoverable ? CursorState.ClickHover : CursorState.ClickEmpty;
            return hoverable ? CursorState.Hover : CursorState.Default;
        }
    }
}
