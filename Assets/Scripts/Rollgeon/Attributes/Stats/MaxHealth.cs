namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Stat concreto de vida máxima (int). El raw <c>Value</c> es el máximo base del héroe
    /// (<c>ClassHeroSO.BaseMaxHp</c>, seed en <c>RunController.RegisterPlayer</c>); los
    /// aumentos in-run (reward "Max Health" del jefe, canal Character) entran como
    /// modificadores Intrinsic ⇒ el máximo efectivo es <c>ModifiedValue</c>.
    /// </summary>
    /// <remarks>
    /// BUG-022: antes el máximo era la constante <c>BaseMaxHp</c> leída directo en HUD y
    /// clamp de heal — los grants a <see cref="Health"/> eran inertes (y colgarlos del
    /// stack de Health inflaba el "HP actual" que la IA lee vía ModifiedValue). Este stat
    /// separa máximo de actual. Lectura canónica: <c>PlayerMaxHp.Resolve</c>.
    /// </remarks>
    public sealed class MaxHealth : BaseAttribute<int>
    {
        public MaxHealth() { }
        public MaxHealth(int initial) : base(initial) { }

        public override string GetAttributeName() => "MaxHealth";

        protected override BaseAttribute<int> CreateDuplicate() => new MaxHealth(_rawValue);
    }
}
