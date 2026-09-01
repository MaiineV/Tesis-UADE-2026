using Rollgeon.Combat.Pipelines;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Las keys de UI del tipo de ataque que la tarjeta de próximo turno suma al título.
    /// </summary>
    /// <remarks>
    /// Enumeradas por lo mismo que <c>AIIntentTextKeys</c>: el guard de localización necesita
    /// una lista que recorrer para exigir que toda key tenga entry en el seeder.
    /// </remarks>
    public static class AttackKindTextKeys
    {
        public const string ComboAttack = "attack_kind.combo_attack";
        public const string BasicAttack = "attack_kind.basic_attack";
        public const string DamageOverTime = "attack_kind.damage_over_time";
        public const string Environmental = "attack_kind.environmental";
        public const string Reaction = "attack_kind.reaction";
        public const string ScriptedAbility = "attack_kind.scripted_ability";

        /// <summary>Format del título con tipo: {0} nombre del ataque, {1} tipo.</summary>
        public const string TitleFormat = "enemy.panel.title_kind_format";

        public static readonly string[] All =
        {
            ComboAttack, BasicAttack, DamageOverTime, Environmental, Reaction, ScriptedAbility,
            TitleFormat,
        };

        public static string Key(AttackKind kind) => kind switch
        {
            AttackKind.ComboAttack => ComboAttack,
            AttackKind.BasicAttack => BasicAttack,
            AttackKind.DamageOverTime => DamageOverTime,
            AttackKind.Environmental => Environmental,
            AttackKind.Reaction => Reaction,
            AttackKind.ScriptedAbility => ScriptedAbility,
            _ => string.Empty,
        };

        /// <summary>
        /// Texto de autor de cada key. Una entry vacía es el opt-out por tipo: el título queda
        /// sin " · Tipo", igual que las reglas vacías de <c>AIIntentText</c>.
        /// </summary>
        public static string Fallback(string key) => key switch
        {
            ComboAttack => "Combo",
            BasicAttack => "Básico",
            DamageOverTime => "Daño sostenido",
            // Vacía a propósito: "Ambiental" en el título de un ataque no califica nada que el
            // jugador pueda usar. La key existe para poder llenarla sin tocar código.
            Environmental => string.Empty,
            Reaction => "Reacción",
            ScriptedAbility => "Habilidad",
            TitleFormat => "{0} · {1}",
            _ => string.Empty,
        };
    }
}
