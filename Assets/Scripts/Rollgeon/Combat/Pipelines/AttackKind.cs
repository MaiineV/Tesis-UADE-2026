namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Classifies the source of damage flowing through <see cref="DamagePipeline"/>.
    /// Weakness only triggers for <see cref="ComboAttack"/> by default (TECHNICAL.md §12.2.1).
    /// </summary>
    public enum AttackKind
    {
        /// <summary>Player or enemy attack with a resolved combo.</summary>
        ComboAttack,
        /// <summary>Basic attack without a combo (highest die — GD §5).</summary>
        BasicAttack,
        /// <summary>Status tick damage — no weakness by default.</summary>
        DamageOverTime,
        /// <summary>Trap, lava, arrow rain.</summary>
        Environmental,
        /// <summary>Thorns, counter-attack — see §12.2.2.</summary>
        Reaction,
        /// <summary>Passive ability that deals damage without a combo.</summary>
        ScriptedAbility,
    }
}
