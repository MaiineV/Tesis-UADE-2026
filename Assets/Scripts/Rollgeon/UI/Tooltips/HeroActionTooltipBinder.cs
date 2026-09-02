using Patterns;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Binder genérico: junto a un trigger, resuelve el slot del hero elegido y arma el
    /// texto en cada hover. Lo que no vive en el hero usa un <see cref="IHasTooltipInfo"/>
    /// local y el trigger lo auto-resuelve sin binder.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Hero Action Tooltip Binder")]
    public sealed class HeroActionTooltipBinder : MonoBehaviour
    {
        [Tooltip("Slot del hero a resolver (Movement, BaseAttack, Healing, ForceDoor, etc.).")]
        [SerializeField] private HeroBehaviorSlot _slot = HeroBehaviorSlot.Healing;

        [Tooltip("Fase usada para resolver el behavior.")]
        [SerializeField] private GamePhase _resolvePhase = GamePhase.Combat;

        [Tooltip("Solo mostrar el tooltip durante combate.")]
        [SerializeField] private bool _onlyDuringCombat;

        private UITooltipTrigger _uiTrigger;
        private WorldTooltipTrigger _worldTrigger;

        /// <summary>
        /// Para AddComponent dinámico: Awake corre con los defaults y esto los corrige;
        /// BuildText lee los campos en cada hover.
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
        /// Configura los triggers descendientes cuando el binder no vive en el GO del trigger.
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

            var context = new TooltipContext(playerService.PlayerGuid, playerService.CurrentHero,
                phaseToResolve);
            return HeroActionTooltip.BuildFor(behavior, context);
        }
    }
}
