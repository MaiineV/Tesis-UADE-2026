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
