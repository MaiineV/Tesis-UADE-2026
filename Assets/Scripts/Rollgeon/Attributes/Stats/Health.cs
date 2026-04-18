namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Stat concreto de puntos de vida (int). Consumido por combat / HUD. El Support
    /// (Content#0099) lo cura via <c>AttributesManager.Modify&lt;Health,int&gt;</c>;
    /// el HUD (T95b downstream) se subscribe a <c>OnAttributeChanged</c> para refrescar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NO marcado <see cref="HiddenFromUIAttribute"/></b> — HP SI se muestra al jugador
    /// (a diferencia de <see cref="Speed"/>). TECHNICAL.md §4.2 / §D.
    /// </para>
    /// <para>
    /// <b>Clamp.</b> El <c>BaseAttribute&lt;int&gt;</c> subyacente no clampea; la
    /// responsabilidad de no bajar de 0 / no subir de <c>Max</c> vive en el caller
    /// (SupportHealBehavior, DamagePipeline). El heal del Support clampea contra el
    /// <c>BaseHP</c> del <c>EnemyDataSO</c>.
    /// </para>
    /// <para>
    /// <b>Duplicate.</b> Clona solo el <c>_rawValue</c>; los modificadores NO se clonan
    /// (TECHNICAL.md §2.2 — entidad "fresca" sin buffs al spawn).
    /// </para>
    /// </remarks>
    public sealed class Health : BaseAttribute<int>
    {
        public Health() { }
        public Health(int initial) : base(initial) { }

        public override string GetAttributeName() => "Health";

        protected override BaseAttribute<int> CreateDuplicate() => new Health(_rawValue);
    }
}
