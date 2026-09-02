namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Objetos que saben describirse como texto de tooltip. Null o vacío = "no aplica
    /// ahora": el binder lo trata como ausencia de tooltip.
    /// </summary>
    public interface IHasTooltipInfo
    {
        string BuildTooltip();

        /// <summary>Texto dinámico por personaje. Default: delega en la versión sin contexto.</summary>
        string BuildTooltip(in TooltipContext context) => BuildTooltip();
    }
}
