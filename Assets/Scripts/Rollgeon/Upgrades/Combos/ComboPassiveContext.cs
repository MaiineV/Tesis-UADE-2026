using System;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Wrapper que se le pasa a cada hook de un <see cref="IComboPassiveTrigger"/>.
    /// Análogo a <see cref="Rollgeon.Upgrades.Dice.EnchantmentTriggerContext"/>
    /// pero sin <c>SlotRef</c> (los passives no están atados a un dado específico).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scratch compartido.</b> Reusamos
    /// <see cref="Rollgeon.Upgrades.Dice.EnchantmentScratch"/> — semánticamente
    /// son los mismos campos (BonusComboDamage, ComboDamageMultiplier, BlockComboDamage,
    /// BonusGold, BonusShield). Cuando aterrice el AttackResolver consume ambos
    /// scratches (de DiceEnchantmentService y de ComboPassiveService) sumando.
    /// </para>
    /// </remarks>
    public sealed class ComboPassiveContext
    {
        /// <summary>Combat / roll state. Populado por el service antes de disparar el hook.</summary>
        public EffectContext Effect;

        /// <summary>
        /// ID del combo matched. Coincide con el <c>ComboPassiveSO.TargetComboId</c>.
        /// Null en los hooks genéricos (no-combo).
        /// </summary>
        public string ComboId;

        /// <summary>Buffer mutable. Los triggers escriben aquí; el service consume al final.</summary>
        public EnchantmentScratch Scratch;

        // ------------------------------------------------------------------
        // Payload por evento — cada campo lo popula SOLO el handler del evento
        // correspondiente (ver docs de cada hook en IComboPassiveTrigger.cs);
        // fuera de ese hook queda en default.
        // ------------------------------------------------------------------

        /// <summary>OnGoldChanged: total de oro luego del cambio.</summary>
        public int GoldTotal;

        /// <summary>OnGoldChanged: delta con signo (+ganó / -gastó). Ojo: en init/reset delta == total.</summary>
        public int GoldDelta;

        /// <summary>OnRoomEntered / OnCombatStart / OnCombatEnd: instancia de la sala.</summary>
        public Guid RoomInstanceId;

        /// <summary>OnRoomEntered: id de template de la sala. Puede ser vacío.</summary>
        public string RoomId;

        /// <summary>OnCombatEnd: resultado del combate. <c>None</c> = sentinel (arg ausente).</summary>
        public CombatOutcome Outcome;

        /// <summary>OnDamageResolved: payload completo del golpe. Null fuera de ese hook.
        /// Los triggers filtran por Source/Target vs player guid según les importe.</summary>
        public DamageResolvedPayload? Damage;
    }
}
