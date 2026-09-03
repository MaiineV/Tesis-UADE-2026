using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Input;
using Rollgeon.Player;
using UnityEngine.InputSystem;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Toggle (ALT, <see cref="GameplayHotkey.ToggleEnemyRanges"/>) que pinta el alcance de
    /// TODOS los enemigos del combate a la vez, con el mismo amarillo del hover individual
    /// (<see cref="ThreatTelegraphOverlay.ReachTint"/>).
    /// </summary>
    /// <remarks>
    /// Hermano de <see cref="EnemyIntentPreviewOverlay"/>: mismo lector
    /// (<see cref="IEnemyIntentService.TryReadReach"/> — estático, sin contar el movimiento
    /// del enemigo) y un canal de telégrafo por enemigo, así el hover y el toggle no se
    /// pisan los quads. Mientras el toggle queda prendido, el pintado sigue el turno: se
    /// apaga durante los turnos enemigos (ahí el reach es una foto a medio revelar, mismo
    /// motivo que el preview del hover) y se repinta al volver el turno del jugador.
    /// </remarks>
    public sealed class AllEnemyRangesOverlay
    {
        /// <summary>Canal por enemigo, distinto del "hover.range" del preview individual.</summary>
        private const string Channel = "alt.range";

        private static AllEnemyRangesOverlay s_instance;

        private readonly HashSet<GridCoord> _cells = new();
        private readonly List<Guid> _painted = new();

        private IGameplayHotkeyService _hotkeys;
        private bool _on;
        private bool _bound;

        public static AllEnemyRangesOverlay ResolveOrCreate() => s_instance ??= new AllEnemyRangesOverlay();

        /// <summary>Si el toggle está prendido (aunque el pintado esté suspendido por turno enemigo).</summary>
        public bool IsOn => _on;

        public static Guid Source(Guid enemyId) => AINode_AuxTelegraph.ChannelGuid(enemyId, Channel);

        /// <summary>
        /// Lo llama <c>CombatHUDView.BindAll</c> (patrón <c>CombatRightPanelSwitcher</c>).
        /// Re-entrar a combate arranca con el toggle apagado: un overlay heredado de la sala
        /// anterior pintaría enemigos que ya no existen.
        /// </summary>
        public void Bind()
        {
            if (_bound) Unbind();

            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out var hotkeys) && hotkeys != null)
            {
                _hotkeys = hotkeys;
                _hotkeys.Subscribe(GameplayHotkey.ToggleEnemyRanges, OnToggleHotkey);
            }

            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, HandleScopeEnded);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _on = false;
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;

            if (_hotkeys != null)
            {
                _hotkeys.Unsubscribe(GameplayHotkey.ToggleEnemyRanges, OnToggleHotkey);
                _hotkeys = null;
            }

            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleScopeEnded);
            EventManager.UnSubscribe(EventName.OnRunEnd, HandleScopeEnded);
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            SetOn(false);
            _bound = false;
        }

        private void OnToggleHotkey(InputAction.CallbackContext _) => Toggle();

        public void Toggle() => SetOn(!_on);

        public void SetOn(bool on)
        {
            if (_on == on) return;
            _on = on;
            if (on) Repaint();
            else ClearPainted();
        }

        /// <summary>Repinta contra el orden de turnos actual. No-op con el toggle apagado.</summary>
        public void Repaint()
        {
            if (!_on) return;
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return;
            Repaint(turnOrder.OrderForRound);
        }

        /// <summary>
        /// Seam de EditMode: la selección de quién se pinta, sin depender de armar un
        /// <see cref="TurnOrderService"/> real. <see cref="IEnemyIntentService.TryReadReach"/>
        /// ya filtra solo (jugador y props sin árbol devuelven false; turno enemigo también).
        /// </summary>
        internal void Repaint(IReadOnlyList<Guid> order)
        {
            ClearPainted();
            if (!_on || order == null) return;

            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return;

            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();
            if (overlay == null) return;

            foreach (var id in order)
            {
                if (!intents.TryReadReach(id, _cells) || _cells.Count == 0) continue;

                overlay.Show(Source(id), _cells, ThreatOverlayState.Incoming,
                             ThreatTelegraphOverlay.ReachTint);
                _painted.Add(id);
            }
        }

        private void ClearPainted()
        {
            if (_painted.Count == 0) return;

            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();
            if (overlay != null)
            {
                foreach (var id in _painted) overlay.Clear(Source(id));
            }
            _painted.Clear();
        }

        // El toggle es una preferencia del jugador y sobrevive al turno; lo que no sobrevive
        // es el pintado: durante el turno enemigo TryReadReach devolvería false igual, pero
        // los quads ya puestos quedarían mostrando el alcance de ANTES de que se mueva.
        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid acting)) return;

            var playerGuid = ServiceLocator.TryGetService<IPlayerService>(out var players)
                ? players?.PlayerGuid ?? Guid.Empty
                : Guid.Empty;

            if (acting == playerGuid) Repaint();
            else ClearPainted();
        }

        // Matar un enemigo con el toggle prendido: su alcance se va con él, sin esperar el
        // próximo repintado de turno.
        private void HandleEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (!_painted.Remove(guid)) return;

            ThreatTelegraphOverlay.ResolveOrCreate()?.Clear(Source(guid));
        }

        private void HandleScopeEnded(params object[] args) => SetOn(false);
    }
}
