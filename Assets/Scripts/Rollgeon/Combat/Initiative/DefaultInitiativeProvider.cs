using System;
using Rollgeon.Attributes.Stats;
using Rollgeon.Balance;
using Rollgeon.Combat.Random;

namespace Rollgeon.Combat.Initiative
{
    /// <summary>
    /// Implementación default de <see cref="IInitiativeProvider"/>
    /// (TECHNICAL.md §12.7): <c>initiative = Speed.ModifiedValue + die(Min, Max)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>DI.</b> Recibe <see cref="IEntityRegistry"/>, <see cref="IInitiativeRng"/>
    /// y <see cref="RulesetSO"/> por ctor. No toca el <c>ServiceLocator</c>.
    /// </para>
    /// <para>
    /// <b>Fallbacks.</b>
    /// <list type="bullet">
    ///   <item>Entidad no registrada en el <see cref="IEntityRegistry"/> →
    ///     devuelve <see cref="int.MinValue"/> + 1 para empujarla al fondo de
    ///     la cola (sentinel <see cref="int.MinValue"/> queda reservado).</item>
    ///   <item>Entidad sin stat <see cref="Speed"/> →
    ///     <see cref="TurnOrderConfig.FallbackInitiativeForMissingSpeed"/> como
    ///     base + die.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class DefaultInitiativeProvider : IInitiativeProvider
    {
        private readonly IEntityRegistry _registry;
        private readonly IInitiativeRng _rng;
        private readonly RulesetSO _ruleset;

        public DefaultInitiativeProvider(IEntityRegistry registry, IInitiativeRng rng, RulesetSO ruleset)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
        }

        /// <inheritdoc />
        public int RollInitiative(Guid entityGuid)
        {
            if (!_registry.TryGetAttributes(entityGuid, out var attrs) || attrs == null)
            {
                // Entidad desconocida — al fondo de la cola, sin crashear.
                return int.MinValue + 1;
            }

            int speedBase;
            if (attrs.HasAttribute<Speed>())
            {
                speedBase = attrs.GetAttributeModifiedValue<Speed, int>();
            }
            else
            {
                speedBase = _ruleset.TurnOrder.FallbackInitiativeForMissingSpeed;
            }

            // IInitiativeRng.Next es [min, max) — para un die [min..max] cerrado
            // sumamos +1 al upper bound (misma semántica que System.Random.Next).
            int min = _ruleset.TurnOrder.SpeedDieMin;
            int max = _ruleset.TurnOrder.SpeedDieMax;
            int die = _rng.Next(min, max + 1);

            return speedBase + die;
        }
    }
}
