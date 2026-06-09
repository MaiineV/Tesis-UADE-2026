using System;
using System.Collections;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Phase;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using UnityEngine;
using CoroutineHost = Rollgeon.Patterns.CoroutineHost;

namespace Rollgeon.Run
{
    /// <summary>
    /// Servicio run-scoped que orquesta la transición multi-piso (#158).
    /// <para>
    /// Al recibir <see cref="EventName.OnFloorExitRequested"/> (el player caminó hasta
    /// la puerta de salida): cubre el swap con la <c>FloorTransitionScreen</c>, avanza
    /// <see cref="IRunContextService.FloorIndex"/>, regenera el siguiente piso y restaura
    /// la fase de exploración. El estado del jugador (HP/oro/dados/ítems) es global y
    /// sobrevive la regeneración — solo se recrea el grafo de salas.
    /// </para>
    /// <para>
    /// No re-corre <c>RunController.OnRunStart</c>: el héroe se recoloca solo porque su
    /// pawn sobrevive a <c>ClearState</c> y <c>PlayerRoomTransitioner</c> reacciona al
    /// <see cref="EventName.OnRoomEntered"/> que dispara <c>GenerateFloor</c>.
    /// </para>
    /// </summary>
    public sealed class FloorProgressionService : IFloorProgressionService, IDisposable
    {
        private const string LogPrefix = "[FloorProgressionService] ";

        private readonly int _baseSeed;
        private FloorLayoutSO _currentLayout;
        private bool _transitioning;

        private EventManager.EventReceiver _onExitRequested;

        public FloorLayoutSO CurrentLayout => _currentLayout;

        private FloorProgressionService(FloorLayoutSO startLayout, int baseSeed)
        {
            _currentLayout = startLayout;
            _baseSeed = baseSeed;

            _onExitRequested = OnFloorExitRequested;
            EventManager.Subscribe(EventName.OnFloorExitRequested, _onExitRequested);
        }

        /// <summary>
        /// Factory: crea el servicio con el layout/seed del piso inicial y lo registra
        /// como <see cref="IFloorProgressionService"/> en <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static FloorProgressionService CreateAndRegister(FloorLayoutSO startLayout, int baseSeed)
        {
            var service = new FloorProgressionService(startLayout, baseSeed);
            ServiceLocator.AddService<IFloorProgressionService>(service, ServiceScope.Run);
            return service;
        }

        public void Dispose()
        {
            if (_onExitRequested != null)
            {
                EventManager.UnSubscribe(EventName.OnFloorExitRequested, _onExitRequested);
                _onExitRequested = null;
            }
        }

        private void OnFloorExitRequested(params object[] args)
        {
            if (_transitioning) return;
            _transitioning = true;
            CoroutineHost.Run(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            // 1. Bloquear input de exploración mientras se hace el swap.
            ServiceLocator.TryGetService<IPhaseService>(out var phase);
            phase?.ReplacePhase(GamePhase.Loading);

            var next = _currentLayout != null ? _currentLayout.NextFloor : null;

            // 2. Piso terminal (sin NextFloor): la run se gana al tomar la salida.
            if (next == null)
            {
                Guid wonRunId = ServiceLocator.TryGetService<IRunContextService>(out var rcWin)
                    ? rcWin.RunId
                    : Guid.Empty;
                Debug.Log(LogPrefix + "Piso terminal — OnRunVictory.");
                EventManager.Trigger(EventName.OnRunVictory, wonRunId);
                _transitioning = false;
                yield break;
            }

            // 3. Avanzar floorDepth (incrementa FloorIndex + dispara OnFloorChanged).
            if (!ServiceLocator.TryGetService<IRunContextService>(out var runCtx))
            {
                Debug.LogError(LogPrefix + "IRunContextService no registrado — no se puede avanzar de piso.");
                _transitioning = false;
                yield break;
            }
            runCtx.AdvanceFloor();
            int floorIndex = runCtx.FloorIndex;

            // 4. Cubrir el swap con la pantalla de transición ANTES de regenerar.
            if (ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                screens.PushByStringId("FloorTransitionScreen", new FloorTransitionPayload
                {
                    FloorNumber = floorIndex + 1,
                    FloorTitle = next.DisplayName,
                });
            }

            // Un frame para que la screen renderice y tape el swap del piso.
            yield return null;

            // 5. Regenerar el piso. GenerateFloor dispara OnRoomEntered → RoomGridLoader +
            //    PlayerRoomTransitioner recolocan al héroe (el pawn sobrevive a ClearState).
            _currentLayout = next;
            int newSeed = DeriveSeed(_baseSeed, floorIndex);
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                dungeon.GenerateFloor(next, newSeed);
            }
            else
            {
                Debug.LogError(LogPrefix + "IDungeonService no registrado — no se puede generar el piso siguiente.");
            }

            // 6. Restaurar exploración. La start room es Start/Cleared → no dispara combate.
            phase?.ReplacePhase(GamePhase.Exploration);

            _transitioning = false;
        }

        /// <summary>Seed determinista por piso para reproducibilidad (save/restore futuro).</summary>
        private static int DeriveSeed(int baseSeed, int floorIndex)
        {
            unchecked { return baseSeed * 92821 + floorIndex; }
        }
    }
}
