namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Bonus de rango de movimiento del personaje (BUG-85, reward "Movimiento+"):
    /// se SUMA a la cara del dado de Movimiento en combate
    /// (<c>SelectionSettings.ResolveEffectiveRange</c>). En exploración no aplica —
    /// el click-to-move es libre. Base 0; los rewards agregan modifiers
    /// Run/Intrinsic/Add igual que los demás stats de personaje.
    /// </summary>
    /// <remarks>
    /// <b>Oculto en UI</b> como <see cref="Speed"/>: el jugador ve el efecto en el
    /// rango pintado al tirar el dado, no un número suelto en el HUD.
    /// </remarks>
    [HiddenFromUI]
    public sealed class MoveRange : BaseAttribute<int>
    {
        public MoveRange() { }
        public MoveRange(int initial) : base(initial) { }

        protected override BaseAttribute<int> CreateDuplicate()
        {
            return new MoveRange(_rawValue);
        }
    }
}
