namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Payload opcional para <see cref="ClassSelectionScreen"/>. MVP no lo cablea — el flujo
    /// canonico es <c>OnPushed(null)</c>. Existe como escape hatch por si un caller futuro
    /// quiere pre-seleccionar una clase (ej. "continuar con el ultimo heroe jugado").
    /// Plan §4.2.
    /// </summary>
    public sealed class ClassSelectionPayload : IScreenPayload
    {
        /// <summary>
        /// Id canonico de la clase a pre-seleccionar (ej. <c>"hero.warrior"</c>). Se deja null si
        /// el caller no tiene preferencia — el screen arranca sin seleccion.
        /// </summary>
        public string PreSelectedClassId;
    }
}
