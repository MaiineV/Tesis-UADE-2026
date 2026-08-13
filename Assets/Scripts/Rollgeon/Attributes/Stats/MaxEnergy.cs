namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Stat concreto de energía máxima (int). El raw <c>Value</c> es el cap del ruleset
    /// (<c>EnergyRules.EnergyMax</c>, seed en <c>EnergyService.InitializeForEntity</c>);
    /// los aumentos in-run (reward "Energy" del jefe) entran como modificadores Intrinsic
    /// ⇒ el cap efectivo es <c>ModifiedValue</c>, expuesto vía <c>EnergyService.GetMax</c>.
    /// </summary>
    /// <remarks>
    /// BUG-022 / followup R8 de <c>EnergyService</c>: antes el cap era la constante del
    /// ruleset y los grants de Energy eran inertes.
    /// </remarks>
    public sealed class MaxEnergy : BaseAttribute<int>
    {
        public MaxEnergy() { }
        public MaxEnergy(int initial) : base(initial) { }

        public override string GetAttributeName() => "MaxEnergy";

        protected override BaseAttribute<int> CreateDuplicate() => new MaxEnergy(_rawValue);
    }
}
