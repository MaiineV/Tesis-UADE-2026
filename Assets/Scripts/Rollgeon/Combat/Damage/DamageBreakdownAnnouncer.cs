using System;
using Patterns;
using Rollgeon.Combos.Play;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;

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
        /// Busca el <see cref="EffDealDamage"/> dentro de UN grupo de efectos (fase de chain),
        /// bajando por <see cref="EffectTree"/> — mismo criterio que
        /// <c>HeroActionBehavior.FindFirstDealDamageEffect</c> pero acotado a la fase que se
        /// va a ejecutar (la fase de escudo no tiene daño ⇒ null ⇒ no se anuncia).
        /// </summary>
        public static EffDealDamage FindDealDamage(EffectData group)
        {
            if (group?.Effects == null) return null;
            foreach (var eff in group.Effects)
            {
                var found = FindDealDamageIn(eff);
                if (found != null) return found;
            }
            return null;
        }

        private static EffDealDamage FindDealDamageIn(IEffect eff)
        {
            if (eff is EffDealDamage dealDmg) return dealDmg;
            foreach (var child in EffectTree.DirectChildren(eff))
            {
                var found = FindDealDamageIn(child);
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
