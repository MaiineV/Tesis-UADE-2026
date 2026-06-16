namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Stat concreto de ataque (int). Consumido por <c>BasicEnemyAI</c> para resolver
    /// dano contra el player. Los enemies con Attack=0 son supports puros (ej: Auditor).
    /// TECHNICAL.md §7.1 / S#0012b.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Clamp.</b> El <c>BaseAttribute&lt;int&gt;</c> subyacente no clampea; la
    /// responsabilidad de no bajar de 0 vive en el caller (BasicEnemyAI skippea si
    /// attack &lt;= 0).
    /// </para>
    /// <para>
    /// <b>Duplicate.</b> Clona solo el <c>_rawValue</c>; los modificadores NO se clonan
    /// (TECHNICAL.md §2.2 — entidad "fresca" sin buffs al spawn).
    /// </para>
    /// </remarks>
    public sealed class Attack : BaseAttribute<int>
    {
        public Attack() { }
        public Attack(int initial) : base(initial) { }

        public override string GetAttributeName() => "Attack";

        protected override BaseAttribute<int> CreateDuplicate() => new Attack(_rawValue);
    }
}
