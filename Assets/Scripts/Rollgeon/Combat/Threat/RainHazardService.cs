using System;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Amenaza ambiental "lluvia de zonas" — independiente del boss (fuente propia, nunca su
    /// GUID), inactiva hasta que algo la <see cref="Activate"/> (normalmente el árbol del boss,
    /// vía un nodo envuelto en <c>If(PcOwnerHpBelow) → Once(...)</c>, mismo patrón que el
    /// trigger de refuerzos). Una vez activa, corre en paralelo a lo que esté haciendo el boss:
    /// cada <see cref="CycleRounds"/> rondas detona lo marcado el ciclo anterior y marca de
    /// nuevo (posiciones al azar, sin perseguir — por diseño, ver plan).
    /// </summary>
    /// <remarks>
    /// Mismo patrón POCO + <see cref="IPreloadableService"/> que <c>ThreatenedAreaService</c>.
    /// Reusa <see cref="AINode_ExecuteTelegraph"/>/<see cref="AINode_TelegraphMark"/> tal cual
    /// vía una <see cref="AIContext"/> armada a mano — cero lógica de telegraph duplicada.
    /// </remarks>
    public sealed class RainHazardService : IPreloadableService, IDisposable
    {
        /// <summary>GUID fijo de esta fuente — nunca el del boss, así ambas amenazas conviven
        /// sin pisarse en <see cref="IThreatenedAreaService"/>/<see cref="ThreatTelegraphOverlay"/>.</summary>
        public static readonly Guid RainSourceId = new Guid("6c1f3a2e-7b4d-4a9e-9c3f-1a2b3c4d5e6f");

        private const int CycleRounds = 2;
        private const int SquareCount = 6;
        private const int SquareSize = 1;
        private const int Damage = 6;

        private readonly System.Random _rng = new System.Random();

        private bool _isActive;

        private EventManager.EventReceiver _onTurnQueueBuiltHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Junto al resto de servicios de combate (ver <c>ThreatenedAreaService.Priority</c> = 80).</summary>
        public int Priority => 80;

        /// <summary>True una vez que algo la activó — sigue activa el resto de la pelea aunque el HP suba.</summary>
        public bool IsActive => _isActive;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _onTurnQueueBuiltHandler = OnTurnQueueBuiltExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<RainHazardService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onTurnQueueBuiltHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuiltHandler);
                _onTurnQueueBuiltHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
            Reset();
        }

        // ======================================================================
        // API
        // ======================================================================

        /// <summary>Activa la lluvia (idempotente — llamar de nuevo mientras ya está activa no hace nada).</summary>
        public void Activate() => _isActive = true;

        // ======================================================================
        // Internals
        // ======================================================================

        private void Reset()
        {
            _isActive = false;
            if (ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) && threat != null)
                threat.Clear(RainSourceId);
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(RainSourceId);
        }

        private void OnScopeEndedExternal(params object[] args) => Reset();

        private void OnTurnQueueBuiltExternal(params object[] args)
        {
            if (!_isActive) return;
            if (args == null || args.Length < 2 || !(args[1] is int roundIndex)) return;
            if (roundIndex <= 0 || roundIndex % CycleRounds != 0) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null) return;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var damagePipeline) || damagePipeline == null) return;

            var ctx = new AIContext
            {
                SelfGuid = RainSourceId,
                PlayerGuid = playerService.PlayerGuid,
                Grid = grid,
                DamagePipeline = damagePipeline,
                Rng = _rng,
            };

            new AINode_ExecuteTelegraph().Tick(ctx);
            new AINode_TelegraphMark
            {
                Shape = ThreatShape.ScatteredSquares,
                Size = SquareSize,
                Count = SquareCount,
                Damage = Damage,
                Kind = AttackKind.Environmental,
            }.Tick(ctx);
        }
    }
}
