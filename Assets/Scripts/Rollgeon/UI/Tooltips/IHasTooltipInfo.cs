namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Implementado por effects (u otros objetos) que saben describirse a sí mismos
    /// como texto de tooltip. Los binders (<c>PotionTooltipBinder</c>, <c>DoorTooltipBinder</c>)
    /// recorren los effects de un <c>HeroActionBehavior</c> y muestran el primer texto
    /// no-vacío que encuentran.
    /// </summary>
    /// <remarks>
    /// Devolver <c>null</c> o vacío indica "no aplica ahora" — el binder lo trata
    /// como ausencia de tooltip (ej. EffForceDoor fuera de combate no expone texto).
    /// </remarks>
    public interface IHasTooltipInfo
    {
        string BuildTooltip();

        /// <summary>
        /// Overload con contexto del owner para texto dinámico por personaje (ej. daño
        /// leyendo el ATQ del hero actual). Default: delega en la versión sin contexto,
        /// así los implementadores legacy no necesitan cambios.
        /// </summary>
        string BuildTooltip(in TooltipContext context) => BuildTooltip();
    }
}
