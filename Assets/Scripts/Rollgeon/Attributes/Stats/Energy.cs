using Rollgeon.Attributes;

namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Stat concreto de energia del jugador (int). Consumido por acciones de combate
    /// (TECHNICAL.md §12.6). Feature#0050: es el presupuesto de acciones de la IA ENEMIGA;
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Invariantes.</b> El <c>BaseAttribute&lt;int&gt;</c> subyacente permite <c>Value &lt; 0</c>
    /// si alguien setea directo (Foundation#0003 deliberadamente no clampea — ver plan §4.1).
    /// el jugador ya no lo usa (su recurso es el Pool de Rolls, fuera de AttributesManager).
    /// </para>
    /// <para>
    /// <b>Duplicate.</b> Clona solo el <c>_rawValue</c>; los modificadores NO se clonan
    /// (TECHNICAL.md §2.2 — hero "fresco" sin buffs al empezar la run).
    /// </para>
    /// </remarks>
    public sealed class Energy : BaseAttribute<int>
    {
        public Energy() { }
        public Energy(int initial) : base(initial) { }

        public override string GetAttributeName() => "Energy";

        protected override BaseAttribute<int> CreateDuplicate() => new Energy(_rawValue);
    }
}
