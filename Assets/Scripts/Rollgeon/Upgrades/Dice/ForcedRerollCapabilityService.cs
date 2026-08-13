using System;
using System.Linq;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Handoff;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Consumidor de <see cref="CapForceRerollOnTurn"/> ("Torpe", BUG-030): en el
    /// turno configurado, al revelarse la mano del jugador, le pide al
    /// <see cref="ICombatHandoffService"/> un relanzamiento completo gratis.
    /// Una sola vez por combate, aunque haya varios dados con Torpe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué OnDiceRolled y no OnRollResolved.</b> OnRollResolved se emite en
    /// Confirm/EndTurn — la mano ya se consumió y no queda nada que relanzar. El
    /// settle real de la tirada es OnDiceRolled (el reveal), con el throw service
    /// ya liberado.
    /// </para>
    /// <para>
    /// <b>Turno.</b> Se resuelve vía <see cref="TurnOrderService.RoundIndex"/>
    /// (0-based; el jugador abre cada round por CNF-006, así que su turno N tiene
    /// RoundIndex N-1). Un contador local de OnTurnStarted se rompería en el
    /// resume mid-combate, que restaura RoundIndex por su cuenta.
    /// </para>
    /// <para>
    /// <b>Tutorial.</b> Sin gate explícito: los encantamientos solo entran por el
    /// altar y el bag del tutorial no tiene Torpe.
    /// </para>
    /// </remarks>
    public sealed class ForcedRerollCapabilityService : IPreloadableService, IDisposable
    {
        /// <summary>Después de DiceEnchantmentBootstrap — solo escucha eventos.</summary>
        public const int DefaultPriority = 90;

        private bool _combatActive;
        private bool _firedThisCombat;
        private bool _subscribed;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        /// <inheritdoc />
        public void Register()
        {
            ServiceLocator.AddService<ForcedRerollCapabilityService>(this, ServiceScope.Global);
            SubscribeEvents();
        }

        public void Dispose()
        {
            UnsubscribeEvents();
            _combatActive = false;
            _firedThisCombat = false;
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Hook para tests — fuerza la subscripcion sin pasar por <see cref="Register"/>.</summary>
        public void SubscribeEventsForTests() => SubscribeEvents();

        /// <summary>Hook para tests — desuscribe los listeners.</summary>
        public void UnsubscribeEventsForTests() => UnsubscribeEvents();

        // ====================================================================
        // Event wiring
        // ====================================================================

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.Subscribe(EventName.OnDiceRolled, OnDiceRolledHandler);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnDiceRolled, OnDiceRolledHandler);
            _subscribed = false;
        }

        // ====================================================================
        // Handlers
        // ====================================================================

        private void OnCombatStartHandler(params object[] args)
        {
            _combatActive = true;
            _firedThisCombat = false;
        }

        private void OnCombatEndHandler(params object[] args)
        {
            _combatActive = false;
            _firedThisCombat = false;
        }

        // Schema EventName.OnDiceRolled: args = [Guid sourceGuid, IReadOnlyList<int> faces]
        private void OnDiceRolledHandler(params object[] args)
        {
            if (!_combatActive || _firedThisCombat) return;
            if (args == null || args.Length < 1 || args[0] is not Guid sourceGuid) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player) || player == null
                || player.PlayerGuid == Guid.Empty || player.PlayerGuid != sourceGuid)
            {
                return;
            }
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var ench)
                || ench?.Bag == null)
            {
                return;
            }

            int turn = ResolveCurrentPlayerTurn();
            if (turn <= 0 || !AnyForceRerollMatchesTurn(ench.Bag, turn)) return;

            if (ServiceLocator.TryGetService<ICombatHandoffService>(out var handoff)
                && handoff != null
                && handoff.TryScheduleForcedFullHandReroll(sourceGuid))
            {
                // Solo si el handoff aceptó: un OnDiceRolled de ActionRoll (Heal,
                // Forzar Puerta) rebota y la chance queda viva para la mano de
                // ataque del mismo turno. El re-reveal del reroll forzado tampoco
                // re-dispara — el flag ya está consumido.
                _firedThisCombat = true;
            }
        }

        // ====================================================================
        // Consulta de la capability
        // ====================================================================

        internal static bool AnyForceRerollMatchesTurn(RuntimeDiceBag bag, int currentPlayerTurn)
        {
            if (bag == null) return false;
            for (int b = 0; b < bag.Dice.Count; b++)
            {
                var slots = bag.GetEnchantments(b);
                for (int s = 0; s < slots.Count; s++)
                {
                    var caps = slots[s]?.Capabilities;
                    if (caps == null) continue;
                    if (caps.OfType<CapForceRerollOnTurn>()
                            .Any(c => c.TriggerOnTurn == currentPlayerTurn))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Turno del jugador (1-based). -1 sin <see cref="TurnOrderService"/> registrado.</summary>
        // Público y no internal: los tests viven en otro assembly y el asmdef
        // principal no declara InternalsVisibleTo.
        public static int ResolveCurrentPlayerTurn()
        {
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return -1;
            return turnOrder.RoundIndex + 1;
        }
    }
}
