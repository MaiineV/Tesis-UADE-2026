namespace Rollgeon.Analytics
{
    /// <summary>
    /// Schema de eventos custom de UGS Analytics (Feature#0029) — única fuente
    /// de verdad en código, espejo 1:1 del Event Manager del dashboard.
    /// Renombrar acá = evento nuevo en el dashboard; mantener estable.
    /// Convención UGS: snake_case para eventos y parámetros.
    /// </summary>
    public static class AnalyticsEvents
    {
        // --------------------------------------------------------------------
        // Eventos — ciclo de run
        // --------------------------------------------------------------------
        public const string RunStarted = "run_started";
        public const string RunEnded = "run_ended";
        public const string FloorReached = "floor_reached";

        // --------------------------------------------------------------------
        // Eventos — combate
        // --------------------------------------------------------------------
        public const string CombatEnded = "combat_ended";
        public const string PlayerDeath = "player_death";
        public const string ComboMatched = "combo_matched";

        // --------------------------------------------------------------------
        // Eventos — economía y meta
        // --------------------------------------------------------------------
        public const string ShopPurchase = "shop_purchase";
        public const string ItemObtained = "item_obtained";
        public const string ActiveItemUsed = "active_item_used";
        public const string UnlockAchieved = "unlock_achieved";

        /// <summary>Valores de <c>run_ended.outcome</c>.</summary>
        public static class Outcomes
        {
            public const string Victory = "victory";
            public const string Defeat = "defeat";
            public const string Abandon = "abandon";
        }

        /// <summary>Nombres de parámetros. En UGS los params son globales y se asignan por evento.</summary>
        public static class Params
        {
            // Comunes a todos los eventos
            public const string RunId = "run_id";               // STRING (Guid "N")
            public const string IsEditor = "is_editor";         // BOOLEAN
            public const string AppVersion = "app_version";     // STRING

            // Run lifecycle
            public const string HeroId = "hero_id";             // STRING
            public const string RulesetId = "ruleset_id";       // STRING
            public const string IsContinue = "is_continue";     // BOOLEAN
            public const string Seed = "seed";                  // INT
            public const string FloorIndex = "floor_index";     // INT
            public const string Outcome = "outcome";            // STRING
            public const string FloorsCleared = "floors_cleared";   // INT
            public const string DurationSec = "duration_sec";   // FLOAT
            public const string CombatsWon = "combats_won";     // INT
            public const string GoldEarned = "gold_earned";     // INT
            public const string GoldSpent = "gold_spent";       // INT
            public const string CombosMatched = "combos_matched";   // INT
            public const string WasResumed = "was_resumed";     // BOOLEAN
            public const string HpAtEntry = "hp_at_entry";      // INT
            public const string GoldAtEntry = "gold_at_entry";  // INT

            // Combate
            public const string RoomType = "room_type";         // STRING
            public const string TurnCount = "turn_count";       // INT
            public const string DamageDealt = "damage_dealt";   // INT
            public const string DamageTaken = "damage_taken";   // INT
            public const string RerollsUsed = "rerolls_used";   // INT
            public const string RollsSpent = "rolls_spent";     // INT (ex energy_spent, Feature#0050)
            public const string HpRemaining = "hp_remaining";   // INT
            public const string TopCombos = "top_combos";       // STRING ("id:count,..." cap 100 chars)
            public const string BossPhaseReached = "boss_phase_reached"; // INT (0 = sin boss)
            public const string BossPhase = "boss_phase";       // INT
            public const string ComboId = "combo_id";           // STRING
            public const string BaseDamage = "base_damage";     // INT
            public const string Multiplier = "multiplier";      // FLOAT

            // Economía y meta
            public const string ItemId = "item_id";             // STRING
            public const string Price = "price";                // INT
            public const string GoldRemaining = "gold_remaining"; // INT
            public const string Source = "source";              // STRING
            public const string UnlockId = "unlock_id";         // STRING
            public const string Category = "category";          // STRING
            public const string DuringRun = "during_run";       // BOOLEAN
        }
    }
}
