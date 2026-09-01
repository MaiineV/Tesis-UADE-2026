namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Bonus plano al total efectivo de la tirada de Forzar Puerta (int, base 0). Los items
    /// tipo "Pico de Minero" lo suben con modifiers Intrinsic (PersistentModifierDef);
    /// <c>ActionRollService</c> lo suma al total cuando <c>spec.Kind == ForceDoor</c> —
    /// con o sin combo, que es lo que pide el GDD ("se aplica a cada intento").
    /// </summary>
    /// <remarks>
    /// Mismo patrón que <see cref="MoveRange"/> (BUG-85): un stat-canal que existe solo
    /// para que el lifecycle de items/rewards sea gratis vía el sistema de modifiers.
    /// </remarks>
    [HiddenFromUI]
    public sealed class ForceDoorRollBonus : BaseAttribute<int>
    {
        public ForceDoorRollBonus() { }
        public ForceDoorRollBonus(int initial) : base(initial) { }

        public override string GetAttributeName() => "ForceDoorRollBonus";

        protected override BaseAttribute<int> CreateDuplicate() => new ForceDoorRollBonus(_rawValue);
    }
}
