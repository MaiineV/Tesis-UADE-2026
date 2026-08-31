namespace Rollgeon.Attributes.Stats
{
    /// <summary>
    /// Rango de ataque en casillas (int). Materializa <c>EnemyDataSO.BaseAttackRange</c>
    /// (resuelto por tier) al spawnear; lo consumen el planner de movimiento
    /// (<c>AIPathMoveExecutor</c>) y <c>PcTargetInRange.UseOwnerAttackRange</c>.
    /// El jugador no lo tiene: los lectores caen a su fallback (1 / campo Range).
    /// </summary>
    /// <remarks>
    /// Primera clase a propósito: al ser un atributo con modificadores, "reducir el rango
    /// del sniper" es un debuff autorable sin código, igual que Attack o Speed.
    /// <b>Duplicate.</b> Clona solo el <c>_rawValue</c>; los modificadores NO se clonan
    /// (TECHNICAL.md §2.2).
    /// </remarks>
    public sealed class AttackRange : BaseAttribute<int>
    {
        public AttackRange() { }
        public AttackRange(int initial) : base(initial) { }

        public override string GetAttributeName() => "AttackRange";

        protected override BaseAttribute<int> CreateDuplicate() => new AttackRange(_rawValue);
    }
}
