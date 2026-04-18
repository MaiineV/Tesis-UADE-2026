using Rollgeon.Attributes;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Stat que representa la potencia de curacion de una entidad (int). Se suma al
    /// <c>BaseHealAmount</c> del <see cref="SupportHealBehavior"/> cuando el Support
    /// ejecuta su heal. TECHNICAL.md §7.1 (Support archetype).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Modifiers.</b> Consumido via <c>attrs.GetAttributeModifiedValue&lt;HealStrength,int&gt;()</c>;
    /// los modifiers <c>Intrinsic</c> aplicados (buffs de aliados, items) alteran el valor
    /// que el behavior lee al construir el heal amount.
    /// </para>
    /// <para>
    /// <b>Duplicate.</b> Clona solo el <c>_rawValue</c>; los modificadores NO se clonan
    /// (TECHNICAL.md §2.2).
    /// </para>
    /// </remarks>
    public sealed class HealStrength : BaseAttribute<int>
    {
        public HealStrength() { }
        public HealStrength(int initial) : base(initial) { }

        public override string GetAttributeName() => "HealStrength";

        protected override BaseAttribute<int> CreateDuplicate() => new HealStrength(_rawValue);
    }
}
