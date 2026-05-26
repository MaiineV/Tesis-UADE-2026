namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Stat concreto que determina el orden de turno dentro de un round
    /// (TECHNICAL.md §4.2, §12.7). Mayor Speed = antes en la cola, antes de
    /// aplicar el speed-die del <c>DefaultInitiativeProvider</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Oculto en UI.</b> Marcado con <see cref="HiddenFromUIAttribute"/> —
    /// el HUD (§D) skippea cualquier stat con este atributo al iterar atributos
    /// para render. El jugador nunca ve el valor numérico: sólo ve el orden
    /// resultante en la turn queue.
    /// </para>
    /// <para>
    /// <b>Modifiers.</b> Consumido vía <c>attrs.GetAttributeModifiedValue&lt;Speed, int&gt;()</c>
    /// por <c>DefaultInitiativeProvider</c>. Los modifiers <c>Intrinsic</c>
    /// aplicados a <c>Speed</c> cambian el valor base — el próximo
    /// <c>BuildForCombat</c> usa el nuevo valor (TECHNICAL.md §12.7).
    /// </para>
    /// </remarks>
    [HiddenFromUI]
    public sealed class Speed : BaseAttribute<int>
    {
        public Speed() { }
        public Speed(int initial) : base(initial) { }

        protected override BaseAttribute<int> CreateDuplicate()
        {
            return new Speed(_rawValue);
        }
    }
}
