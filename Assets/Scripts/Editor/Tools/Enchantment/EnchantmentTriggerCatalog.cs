using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Catálogo curado del "cuándo" de un encantamiento — espejo de
    /// <c>ItemTriggerCatalog</c> para el canal dados. Acá el enemigo no es un enum
    /// gigante (<see cref="EnchantmentHookEvent"/> tiene 6 miembros y todos funcionan):
    /// es la <b>semántica</b> — <c>ComboMatched</c> es preview y re-dispara en cada
    /// toggle de hold, así que un efecto de apply directo ahí es farmeable infinito
    /// (BUG-017). El catálogo lleva esa trampa en el dato (<see cref="TriggerOption.ScratchOnly"/>)
    /// en vez de confiar en que el autor se acuerde.
    /// </summary>
    public static class EnchantmentTriggerCatalog
    {
        /// <summary>
        /// Una forma de disparo con nombre de diseño. <see cref="Id"/> es la clave
        /// estable que viaja en la spec de creación; <see cref="DisplayName"/> y
        /// <see cref="Help"/> son lo que ve el autor.
        /// </summary>
        public readonly struct TriggerOption
        {
            /// <summary>Clave estable de la opción. No se muestra.</summary>
            public readonly string Id;

            /// <summary>Frase de diseño ("Cuando jugás un combo").</summary>
            public readonly string DisplayName;

            /// <summary>Una línea con el significado exacto, trampas incluidas.</summary>
            public readonly string Help;

            /// <summary>Evento del canal dados que setea en el trigger.</summary>
            public readonly EnchantmentHookEvent Event;

            /// <summary>La UI/skill debe pedir combo ids (setea <c>Filter.Mode = ComboIds</c>).</summary>
            public readonly bool UsesComboIds;

            /// <summary>
            /// Hook de preview: solo admite efectos <c>IComboScratchWriter</c>. Un apply
            /// directo (oro, escudo, curación) acá es farmeable por toggle de hold —
            /// la auditoría lo rechaza (BUG-017).
            /// </summary>
            public readonly bool ScratchOnly;

            internal TriggerOption(
                string id, string displayName, string help, EnchantmentHookEvent evt,
                bool usesComboIds = false, bool scratchOnly = false)
            {
                Id = id;
                DisplayName = displayName;
                Help = help;
                Event = evt;
                UsesComboIds = usesComboIds;
                ScratchOnly = scratchOnly;
            }
        }

        /// <summary>
        /// Las formas de disparo que la tool ofrece. Cerrada a propósito: cada entrada
        /// nueva se agrega acá con su Help y su flag de scratch, no eligiendo el enum a mano.
        /// </summary>
        public static readonly IReadOnlyList<TriggerOption> All = new[]
        {
            // ---- combo (apply) ------------------------------------------------------
            new TriggerOption(
                "combo.played.any", "Cuando jugás cualquier combo",
                "El combo se confirmó (ventana pre-daño). Acá van los efectos de apply: oro, escudo, bonos al combo.",
                EnchantmentHookEvent.ComboPlayed),
            new TriggerOption(
                "combo.played.ids", "Cuando jugás combos específicos",
                "Igual que el anterior, pero solo con los combos elegidos. Sin ningún combo elegido no dispara nunca.",
                EnchantmentHookEvent.ComboPlayed, usesComboIds: true),

            // ---- combo (preview) ----------------------------------------------------
            new TriggerOption(
                "combo.matched.any", "Cuando un combo matchea (preview)",
                "Se detectó un combo, antes de confirmarlo. Re-dispara en cada toggle de hold: SOLO scratch-writers (EffAddComboBonus y afines) — un apply directo acá es farmeable (BUG-017).",
                EnchantmentHookEvent.ComboMatched, scratchOnly: true),
            new TriggerOption(
                "combo.matched.ids", "Cuando matchean combos específicos (preview)",
                "Preview restringido a los combos elegidos. Mismas reglas: solo scratch-writers.",
                EnchantmentHookEvent.ComboMatched, usesComboIds: true, scratchOnly: true),

            // ---- tirada -------------------------------------------------------------
            new TriggerOption(
                "roll.dice", "En cada tirada del dado",
                "Roll crudo, post-dados y pre-reroll. Dispara aunque después se rerolee.",
                EnchantmentHookEvent.DiceRolled),
            new TriggerOption(
                "roll.resolved", "Cuando la tirada queda firme",
                "El roll final lockeado después de los rerolls. El momento correcto para leer la cara definitiva.",
                EnchantmentHookEvent.RollResolved),

            // ---- ciclo --------------------------------------------------------------
            new TriggerOption(
                "applied", "Al encantar el dado",
                "Una sola vez, cuando el encantamiento entra al slot. Para efectos de setup.",
                EnchantmentHookEvent.EnchantmentApplied),
            new TriggerOption(
                "turn.finished", "Al terminar tu turno",
                "Fin del turno del jugador, con la tirada ya gastada.",
                EnchantmentHookEvent.TurnFinished),
            new TriggerOption(
                "combat.started", "Al empezar un combate",
                "Arranca una pelea. Para resetear counters por dado que duran 'hasta terminar el combate' (Racha).",
                EnchantmentHookEvent.CombatStarted),
        };

        /// <summary>
        /// Configura el "cuándo" de <paramref name="trigger"/> según la opción. No toca
        /// Undo ni Dirty — eso es del llamador, igual que en <c>ItemTriggerCatalog</c>.
        /// </summary>
        public static void Apply(ExecuteEffectsOnDiceEvent trigger, TriggerOption option)
        {
            if (trigger == null) return;

            trigger.Event = option.Event;
            trigger.Filter ??= new ComboFilter();
            trigger.Filter.Mode = option.UsesComboIds ? ComboFilterMode.ComboIds : ComboFilterMode.AnyCombo;
        }

        /// <summary>
        /// Inverso de <see cref="Apply"/>: qué opción representa el estado del trigger.
        /// <c>null</c> = configuración fuera del catálogo (ej. Filter en modo None con
        /// ids cargados a mano). Para eventos sin combo el Filter se ignora, igual que
        /// en runtime.
        /// </summary>
        public static TriggerOption? Match(ExecuteEffectsOnDiceEvent trigger)
        {
            if (trigger == null) return null;

            bool isComboHook = trigger.Event == EnchantmentHookEvent.ComboMatched
                            || trigger.Event == EnchantmentHookEvent.ComboPlayed;
            bool usesIds = isComboHook && trigger.Filter != null
                        && trigger.Filter.Mode == ComboFilterMode.ComboIds;

            foreach (var option in All)
            {
                if (option.Event != trigger.Event) continue;
                if (isComboHook && option.UsesComboIds != usesIds) continue;
                return option;
            }
            return null;
        }

        /// <summary>Frase para el panel: "Cuando jugás combos específicos: combo.trio, combo.poker".</summary>
        public static string Describe(ExecuteEffectsOnDiceEvent trigger)
        {
            if (trigger == null) return "(sin trigger)";

            var option = Match(trigger);
            if (option == null)
                return $"Disparador fuera del catálogo ({trigger.Event}, Filter={trigger.Filter?.Mode})";

            var text = option.Value.DisplayName;
            if (option.Value.UsesComboIds && trigger.Filter?.ComboIds is { Count: > 0 } ids)
                text += ": " + string.Join(", ", ids);
            if (trigger.RequireCarrierParticipates)
                text += " — solo si el dado participa del combo";
            return text;
        }
    }
}
