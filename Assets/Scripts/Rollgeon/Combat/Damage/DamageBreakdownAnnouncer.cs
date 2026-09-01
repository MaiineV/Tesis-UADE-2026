using System;
using Patterns;
using Rollgeon.Combos.Play;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Player;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Emite <see cref="DamageBreakdownComputedPayload"/> con el
    /// desglose N×M del golpe que se está jugando. Se llama INMEDIATAMENTE después de
    /// <c>IComboPlayService.BeginPlay</c> (ventana de combo jugado ya abierta y journal
    /// at-played ya lleno) y ANTES de que ningún efecto ejecute — así el director de la
    /// animación de breakdown arma su secuencia y levanta el gate sin carreras.
    /// Sin suscriptores el Raise es un no-op: cero costo fuera de combate.
    /// </summary>
    public static class DamageBreakdownAnnouncer
    {
        public static void Announce(EffectContext effCtx, EffDealDamage dmgEff)
        {
            if (effCtx == null || dmgEff == null) return;
            if (dmgEff.Source != DamageSource.ComboValue) return;
            if (effCtx.ComboResult is not { IsMatch: true } combo
                || string.IsNullOrEmpty(combo.ComboId)) return;

            // Ventana anidada de OTRO combo (un item que dispara otro behavior): el
            // breakdown pertenece al combo top-level, no re-anunciar por el anidado.
            if (ServiceLocator.TryGetService<IComboPlayService>(out var play)
                && play != null && play.IsPlayWindowOpen && play.CurrentComboId != combo.ComboId)
                return;

            var dice = ContributingDiceResolver.ResolveFromContext(effCtx, combo.ContributingIndices);
            Guid sourceId = effCtx.SourceEntity != null ? effCtx.SourceEntity.Guid : effCtx.SourceGuid;

            PlayerComboDamage.Resolve(sourceId, combo.BaseDamage, dice, dmgEff.ComboMultiplier,
                PlayerComboFormulaKind.Damage, out var breakdown);

            TypedEvent<DamageBreakdownComputedPayload>.Raise(new DamageBreakdownComputedPayload
            {
                SourceGuid = sourceId,
                TargetGuid = effCtx.TargetGuid,
                ComboId = combo.ComboId,
                Breakdown = breakdown,
            });
        }

        /// <summary>
        /// Variante de escudo: mismo guion N×M pero con la fórmula de escudo (base de la
        /// tabla escudo_combo del sheet, NUNCA combo.BaseDamage — espejo de
        /// <c>EffAddShield.ResolveComboShield</c>). El payload va sin target: el escudo no
        /// pasa por el DamagePipeline, así el director no muestra paso de mitigación.
        /// </summary>
        public static void AnnounceShield(EffectContext effCtx, EffAddShield shieldEff)
        {
            if (effCtx == null || shieldEff == null) return;
            if (shieldEff.ShieldSource != DamageSource.ComboValue) return;
            if (effCtx.ComboResult is not { IsMatch: true } combo
                || string.IsNullOrEmpty(combo.ComboId)) return;

            if (ServiceLocator.TryGetService<IComboPlayService>(out var play)
                && play != null && play.IsPlayWindowOpen && play.CurrentComboId != combo.ComboId)
                return;

            var sheet = ServiceLocator.TryGetService<IPlayerService>(out var player)
                ? player?.CurrentHero?.Sheet
                : null;
            if (sheet == null) return;

            var dice = ContributingDiceResolver.ResolveFromContext(effCtx, combo.ContributingIndices);
            Guid sourceId = effCtx.SourceEntity != null ? effCtx.SourceEntity.Guid : effCtx.SourceGuid;

            PlayerComboShield.Resolve(sourceId, sheet.GetShieldBase(combo.ComboId), dice,
                shieldEff.ComboMultiplier, out var breakdown);
            if (breakdown.Final <= 0) return; // sin entrada en tabla o bloqueado: nada que animar

            TypedEvent<DamageBreakdownComputedPayload>.Raise(new DamageBreakdownComputedPayload
            {
                SourceGuid = sourceId,
                TargetGuid = Guid.Empty,
                ComboId = combo.ComboId,
                Breakdown = breakdown,
            });
        }

        /// <summary>
        /// Variante de curación: mismo guion N×M con la fórmula de heal (base de la
        /// HealBaseTable del sheet — espejo de <c>EffHeal.ResolveBuildDiceAmount</c>).
        /// Solo anuncia el camino con combo real; el fallback sin combo (dado más alto)
        /// no tiene desglose que animar. Payload sin target: el heal no pasa por el
        /// DamagePipeline, así el director no muestra paso de mitigación.
        /// </summary>
        public static void AnnounceHeal(EffectContext effCtx, EffHeal healEff)
        {
            if (effCtx == null || healEff == null) return;
            if (!healEff.UseBuildDice) return;
            if (effCtx.ComboResult is not { IsMatch: true } combo
                || string.IsNullOrEmpty(combo.ComboId)) return;

            if (ServiceLocator.TryGetService<IComboPlayService>(out var play)
                && play != null && play.IsPlayWindowOpen && play.CurrentComboId != combo.ComboId)
                return;

            var sheet = ServiceLocator.TryGetService<IPlayerService>(out var player)
                ? player?.CurrentHero?.Sheet
                : null;
            if (sheet == null) return;

            var dice = ContributingDiceResolver.ResolveFromContext(effCtx, combo.ContributingIndices);
            Guid sourceId = effCtx.SourceEntity != null ? effCtx.SourceEntity.Guid : effCtx.SourceGuid;

            PlayerComboHeal.Resolve(sourceId, sheet.GetHealBase(combo.ComboId), dice,
                healEff.ComboMultiplier, out var breakdown);
            if (breakdown.Final <= 0) return; // sin entrada en tabla o bloqueado: nada que animar

            TypedEvent<DamageBreakdownComputedPayload>.Raise(new DamageBreakdownComputedPayload
            {
                SourceGuid = sourceId,
                TargetGuid = Guid.Empty,
                ComboId = combo.ComboId,
                Breakdown = breakdown,
            });
        }

        /// <summary>
        /// Variante de Forzar Puerta: mismo guion N×M con la fórmula del check
        /// (<see cref="PlayerComboForceDoor"/> — base = combo.BaseDamage layered, no hay
        /// tabla propia). Solo anuncia con combo real (sin combo no hay desglose que
        /// animar), y anuncia AUNQUE el threshold falle: ver el número que no alcanzó es
        /// feedback. El <c>ForceDoorRollBonus</c> de items no entra a la animación (flat
        /// post-M, se muestra en el label del threshold). Payload sin target: no hay
        /// paso de mitigación.
        /// </summary>
        public static void AnnounceForceDoor(EffectContext effCtx, EffForceDoor doorEff)
        {
            if (effCtx == null || doorEff == null) return;
            if (effCtx.ComboResult is not { IsMatch: true } combo
                || string.IsNullOrEmpty(combo.ComboId)) return;

            if (ServiceLocator.TryGetService<IComboPlayService>(out var play)
                && play != null && play.IsPlayWindowOpen && play.CurrentComboId != combo.ComboId)
                return;

            var dice = ContributingDiceResolver.ResolveFromContext(effCtx, combo.ContributingIndices);
            Guid sourceId = effCtx.SourceEntity != null ? effCtx.SourceEntity.Guid : effCtx.SourceGuid;

            PlayerComboForceDoor.Resolve(sourceId, combo.BaseDamage, dice,
                doorEff.ComboMultiplier > 0f ? doorEff.ComboMultiplier : 1f, out var breakdown);
            if (breakdown.Final <= 0) return; // bloqueado por scratch: nada que animar

            TypedEvent<DamageBreakdownComputedPayload>.Raise(new DamageBreakdownComputedPayload
            {
                SourceGuid = sourceId,
                TargetGuid = Guid.Empty,
                ComboId = combo.ComboId,
                Breakdown = breakdown,
            });
        }

        /// <summary>El <see cref="EffForceDoor"/> de la fase (check de Forzar Puerta).</summary>
        public static EffForceDoor FindForceDoor(EffectData group)
            => FindIn<EffForceDoor>(group);

        /// <summary>
        /// Busca el <see cref="EffDealDamage"/> dentro de UN grupo de efectos (fase de chain),
        /// bajando por <see cref="EffectTree"/> — mismo criterio que
        /// <c>HeroActionBehavior.FindFirstDealDamageEffect</c> pero acotado a la fase que se
        /// va a ejecutar. Si la fase no tiene daño, el caller cae a
        /// <see cref="FindAddShield"/> para anunciar la variante de escudo.
        /// </summary>
        public static EffDealDamage FindDealDamage(EffectData group)
            => FindIn<EffDealDamage>(group);

        /// <summary>El <see cref="EffAddShield"/> de la fase (defensa del chain).</summary>
        public static EffAddShield FindAddShield(EffectData group)
            => FindIn<EffAddShield>(group);

        /// <summary>El <see cref="EffHeal"/> de la fase (curación del chain).</summary>
        public static EffHeal FindHeal(EffectData group)
            => FindIn<EffHeal>(group);

        private static T FindIn<T>(EffectData group) where T : class
        {
            if (group?.Effects == null) return null;
            foreach (var eff in group.Effects)
            {
                var found = FindInTree<T>(eff);
                if (found != null) return found;
            }
            return null;
        }

        private static T FindInTree<T>(IEffect eff) where T : class
        {
            if (eff is T match) return match;
            foreach (var child in EffectTree.DirectChildren(eff))
            {
                var found = FindInTree<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }

    /// <summary>
    /// Payload del breakdown computado al confirmar un golpe de combo del jugador.
    /// Vive acá (y no en EventPayloads) porque arrastra <see cref="DamageBreakdown"/>.
    /// </summary>
    public struct DamageBreakdownComputedPayload
    {
        public Guid SourceGuid;
        public Guid TargetGuid;
        public string ComboId;
        public DamageBreakdown Breakdown;
    }
}
