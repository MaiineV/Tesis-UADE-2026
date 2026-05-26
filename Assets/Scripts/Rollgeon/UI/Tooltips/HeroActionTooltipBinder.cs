using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Binder genérico configurable: cuelga este componente junto a un
    /// <see cref="UITooltipTrigger"/> o <see cref="WorldTooltipTrigger"/> y elegí qué
    /// slot del hero resolver. El texto se arma cada hover/click leyendo el primer
    /// <see cref="IHasTooltipInfo"/> que encuentre en los effects del behavior.
    /// </summary>
    /// <remarks>
    /// Reemplaza la necesidad de crear un binder por dominio (Heal, ForceDoor, etc).
    /// Para casos que NO viven en el hero (cofre, enemigo, item de inventario), poné
    /// un <see cref="IHasTooltipInfo"/> en el componente local y el trigger lo
    /// auto-resuelve sin binder vía <see cref="TooltipResolver"/>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Hero Action Tooltip Binder")]
    public sealed class HeroActionTooltipBinder : MonoBehaviour
    {
        [Tooltip("Slot del hero a resolver (Movement, BaseAttack, Healing, ForceDoor, etc.).")]
        [SerializeField] private HeroBehaviorSlot _slot = HeroBehaviorSlot.Healing;

        [Tooltip("Fase usada para resolver el behavior. Default Combat; usar Exploration " +
                 "si el behavior solo existe en exploración.")]
        [SerializeField] private GamePhase _resolvePhase = GamePhase.Combat;

        [Tooltip("Si true, el tooltip solo se muestra cuando IPhaseService.CurrentBase == Combat. " +
                 "Útil para acciones que solo aplican en combat (ej. Forzar Puerta).")]
        [SerializeField] private bool _onlyDuringCombat;

        private UITooltipTrigger _uiTrigger;
        private WorldTooltipTrigger _worldTrigger;

        /// <summary>
        /// Pisa los campos del binder en runtime (post-Awake). Usado por callers que
        /// hacen <c>AddComponent</c> dinámico (ej. <c>DoorController.EnsureTooltipComponents</c>):
        /// AddComponent dispara Awake inmediato con los defaults; <c>Configure</c> los corrige.
        /// BuildText lee los campos cada hover/click, así que la nueva config se respeta sin
        /// re-cablear los triggers.
        /// </summary>
        public void Configure(HeroBehaviorSlot slot, GamePhase resolvePhase, bool onlyDuringCombat)
        {
            _slot = slot;
            _resolvePhase = resolvePhase;
            _onlyDuringCombat = onlyDuringCombat;
        }

        private void Awake()
        {
            _uiTrigger = GetComponent<UITooltipTrigger>();
            _worldTrigger = GetComponent<WorldTooltipTrigger>();
            if (_uiTrigger != null) _uiTrigger.TextProvider = BuildText;
            if (_worldTrigger != null) _worldTrigger.TextProvider = BuildText;
            ConfigureExternalTriggers();
        }

        /// <summary>
        /// Para binders puestos en un GO que no es el del trigger (ej. binder en root
        /// del DoorController, triggers en cada mesh hijo). Configura todos los triggers
        /// descendientes con el mismo provider.
        /// </summary>
        public void ConfigureExternalTriggers()
        {
            var worldTriggers = GetComponentsInChildren<WorldTooltipTrigger>(includeInactive: true);
            for (int i = 0; i < worldTriggers.Length; i++)
            {
                if (worldTriggers[i] != null) worldTriggers[i].TextProvider = BuildText;
            }
            var uiTriggers = GetComponentsInChildren<UITooltipTrigger>(includeInactive: true);
            for (int i = 0; i < uiTriggers.Length; i++)
            {
                if (uiTriggers[i] != null) uiTriggers[i].TextProvider = BuildText;
            }
        }

        private string BuildText()
        {
            if (_onlyDuringCombat)
            {
                if (!ServiceLocator.TryGetService<IPhaseService>(out var phase)
                    || phase == null
                    || phase.CurrentBase != GamePhase.Combat)
                {
                    return null;
                }
            }

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)
                || playerService?.CurrentHero == null)
            {
                return null;
            }

            var phaseToResolve = _resolvePhase;
            var behavior = playerService.CurrentHero.ResolveBaseBehavior(_slot, phaseToResolve);
            if (behavior == null) return null;

            return FirstTooltipFromEffects(behavior.Effects);
        }

        private static string FirstTooltipFromEffects(List<EffectData> effects)
        {
            if (effects == null) return null;
            foreach (var group in effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is IHasTooltipInfo info)
                    {
                        var text = info.BuildTooltip();
                        if (!string.IsNullOrEmpty(text)) return text;
                    }
                }
            }
            return null;
        }
    }
}
