using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combos;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Patterns;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Exploration
{
    public sealed class ExplorationBehaviorService : IExplorationBehaviorService, IDisposable
    {
        private enum State { Inactive, Idle, Selecting, Rolling }

        private State _state = State.Inactive;
        private HeroActionBehavior _pendingBehavior;

        // Casillas "frente a puerta" de la selección de movimiento en curso → dirección.
        // Si el user clickea una de estas, se cruza de sala en vez de moverse.
        private System.Collections.Generic.Dictionary<GridCoord, DoorDirection> _doorTiles;

        // Casillas frente a la puerta de SALIDA de piso (#158). Pisarlas dispara la
        // transición al siguiente piso en vez de cruzar a una sala vecina.
        private System.Collections.Generic.HashSet<GridCoord> _exitTiles;

        private EventManager.EventReceiver _onPhaseEnter;
        private EventManager.EventReceiver _onPhaseExit;

        public bool IsActive => _state != State.Inactive;

        private ExplorationBehaviorService()
        {
            _onPhaseEnter = OnPhaseEnter;
            _onPhaseExit = OnPhaseExit;

            EventManager.Subscribe(EventName.OnPhaseEnter, _onPhaseEnter);
            EventManager.Subscribe(EventName.OnPhaseExit, _onPhaseExit);
        }

        public static ExplorationBehaviorService CreateAndRegister()
        {
            var service = new ExplorationBehaviorService();
            ServiceLocator.AddService<IExplorationBehaviorService>(service, ServiceScope.Run);
            return service;
        }

        public void OnBehaviorSelected(int slot)
        {
            Debug.LogWarning($"[ExplorationBehaviorService] OnBehaviorSelected(slot={slot}) — _state={_state}");

            // Si hay un flow anterior colgado (Selecting / Rolling sin terminar), lo
            // cancelamos automaticamente antes de procesar el nuevo click. Asi el user
            // puede clickear otro boton para "abandonar" la accion en curso.
            if (_state != State.Idle)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] _state={_state} → cancelando flow anterior y procesando click nuevo.");
                ForceCancelInProgress();
            }

            if (_state != State.Idle)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] No pude resetear _state a Idle (queda {_state}) — abortando click.");
                return;
            }

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService))
            {
                Debug.LogWarning("[ExplorationBehaviorService] IPlayerService not registered.");
                return;
            }

            var hero = playerService.CurrentHero;
            if (hero == null) return;

            var playerGuid = playerService.PlayerGuid;
            var behaviors = hero.GetBehaviorsForPhase(GamePhase.Exploration);

            // Buscar por slot (no por list index) — la lista de exploration filtra
            // los slots que no aplican (ej. BaseAttack, SpecialAttack), por lo que
            // los índices no se alinean con HeroBehaviorSlot. Buscar por slot
            // garantiza que el botón de Pass Door no termine disparando Healing.
            HeroActionBehavior behavior = null;
            for (int i = 0; i < behaviors.Count; i++)
            {
                if (behaviors[i] != null && (int)behaviors[i].Slot == slot)
                {
                    behavior = behaviors[i];
                    break;
                }
            }

            if (behavior == null)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] No behavior at slot {slot} for Exploration.");
                return;
            }

            var preCtx = new PreConditionContext
            {
                OwnerGuid = playerGuid,
                Entity = new Entity { Guid = playerGuid },
            };

            if (behavior.ShowConditions != null && behavior.ShowConditions.Count > 0
                && !behavior.ShouldShow(preCtx))
            {
                Debug.LogWarning($"[ExplorationBehaviorService] '{behavior.ActionName}' ShowConditions failed — behavior no ejecutado.");
                return;
            }

            // Si algun effect del behavior implementa IActionRollEffect, este "owns" el cost
            // (lo cobra el IActionRollService al confirmar el roll, o no se cobra nada en el
            // path instantaneo). Skipeamos el static charge para evitar double-billing.
            bool ownsCostViaActionRoll = TryFindActionRollEffect(behavior, out var rollEffect);

            if (ownsCostViaActionRoll && rollEffect.TryGetRollSpec(playerGuid, out var rollSpec))
            {
                StartActionRoll(behavior, playerGuid, playerService, rollSpec);
                return;
            }

            if (!ownsCostViaActionRoll && behavior.EnergyCost > 0)
            {
                if (!ServiceLocator.TryGetService<IEnergyService>(out var energy)
                    || !energy.SpendEnergy(playerGuid, behavior.EnergyCost))
                {
                    Debug.Log($"[ExplorationBehaviorService] Not enough energy for '{behavior.ActionName}'.");
                    return;
                }
            }

            if (behavior.HasEffectsWithSelectionAt(SelectionTiming.BeforeRoll))
            {
                BeginSelection(behavior, playerGuid);
                return;
            }

            ExecuteBehavior(behavior, playerGuid, null, null);
        }

        public void CancelSelection()
        {
            if (_state != State.Selecting) return;

            if (ServiceLocator.TryGetService<ISelectionController>(out var controller))
            {
                controller.OnSelectionCompleted -= OnSelectionCompleted;
                controller.CancelSelection();
            }

            _pendingBehavior = null;
            _doorTiles = null;
            _exitTiles = null;
            _state = State.Idle;
        }

        // Cancela cualquier flow en curso y deja _state en Idle. Llamado cuando el
        // user clickea otra accion mientras hay una en progreso — comportamiento
        // tipico de UX: el ultimo click "manda" y abandona lo anterior.
        private void ForceCancelInProgress()
        {
            switch (_state)
            {
                case State.Selecting:
                    Debug.LogWarning("[ExplorationBehaviorService] ForceCancel: cancelando Selecting.");
                    CancelSelection();
                    break;
                case State.Rolling:
                    Debug.LogWarning("[ExplorationBehaviorService] ForceCancel: cancelando Rolling (action roll).");
                    if (ServiceLocator.TryGetService<IActionRollService>(out var rs)
                        && rs != null && rs.IsActive)
                    {
                        rs.Cancel(); // el callback resuelve outcome.Cancelled=true → _state=Idle
                    }
                    // Safety: si el cancel del roll service no logro resetear (callback
                    // no se invoco), forzamos Idle directamente.
                    if (_state == State.Rolling)
                    {
                        _pendingBehavior = null;
                        _state = State.Idle;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            CancelSelection();

            if (_onPhaseEnter != null)
            {
                EventManager.UnSubscribe(EventName.OnPhaseEnter, _onPhaseEnter);
                _onPhaseEnter = null;
            }
            if (_onPhaseExit != null)
            {
                EventManager.UnSubscribe(EventName.OnPhaseExit, _onPhaseExit);
                _onPhaseExit = null;
            }

            _state = State.Inactive;
        }

        private static bool TryFindActionRollEffect(HeroActionBehavior behavior,
            out IActionRollEffect rollEffect)
        {
            rollEffect = null;
            if (behavior?.Effects == null) return false;
            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is IActionRollEffect candidate)
                    {
                        rollEffect = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        private void StartActionRoll(HeroActionBehavior behavior, Guid playerGuid,
            IPlayerService playerService, ActionRollSpec spec)
        {
            if (!ServiceLocator.TryGetService<IActionRollService>(out var rollService)
                || rollService == null)
            {
                Debug.LogError("[ExplorationBehaviorService] IActionRollService no registrado — " +
                               "no se puede ejecutar la accion con tirada.");
                return;
            }

            var bag = playerService.DiceBag;
            if (bag == null || bag.Dice == null || bag.Dice.Count == 0)
            {
                Debug.LogError("[ExplorationBehaviorService] PlayerService.DiceBag null o vacio — " +
                               "no se puede tirar para la accion.");
                return;
            }

            _pendingBehavior = behavior;
            _state = State.Rolling;
            Debug.LogWarning($"[ExplorationBehaviorService] StartActionRoll → behavior='{behavior.ActionName}' _state=Rolling");

            rollService.StartFlow(spec, playerGuid, bag, outcome =>
            {
                Debug.LogWarning($"[ExplorationBehaviorService] outcome callback: cancelled={outcome.Cancelled} " +
                                 $"passed={outcome.PassedThreshold} effective={outcome.EffectiveTotal} " +
                                 $"hasCombo={outcome.HasCombo} _state→Idle");
                var resolvedBehavior = _pendingBehavior;
                _pendingBehavior = null;
                _state = State.Idle;

                if (outcome.Cancelled || resolvedBehavior == null) return;

                // Reconstruir el ComboDetectionResult del outcome para que los effects
                // (EffForceDoor, EffHeal) puedan leer EffectiveTotal via ComboResult.
                ComboDetectionResult? combo = outcome.HasCombo
                    ? ComboDetectionResult.Match(outcome.EffectiveTotal,
                        outcome.FinalRoll != null ? outcome.FinalRoll.Length : 0)
                    : (ComboDetectionResult?)null;

                ExecuteBehavior(resolvedBehavior, playerGuid, null, outcome.FinalRoll, combo,
                    outcome.EffectiveTotal);
            });
        }

        private void BeginSelection(HeroActionBehavior behavior, Guid playerGuid)
        {
            var targetSettings = behavior.Effects?
                .Where(g => g?.Effects != null)
                .SelectMany(g => g.Effects)
                .FirstOrDefault(e => e != null && e.RequiresSelectionAt(SelectionTiming.BeforeRoll))
                ?.GetSelection();

            if (targetSettings == null)
            {
                Debug.LogWarning("[ExplorationBehaviorService] No SelectionSettings found — executing directly.");
                ExecuteBehavior(behavior, playerGuid, null, null);
                return;
            }

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || !grid.TryGetPosition(playerGuid, out var ownerPos))
            {
                Debug.LogWarning("[ExplorationBehaviorService] Cannot resolve player position.");
                return;
            }

            if (targetSettings.SlotState == SlotState.Self)
            {
                var selfResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new System.Collections.Generic.List<TargetRef>
                        { TargetRef.At(ownerPos) },
                };
                ExecuteBehavior(behavior, playerGuid, selfResult, null);
                return;
            }

            if (targetSettings.AutoResolve)
            {
                var autoResult = targetSettings.AutoResolveTargets(ownerPos, playerGuid);
                ExecuteBehavior(behavior, playerGuid, autoResult, null);
                return;
            }

            var validTargets = targetSettings.ResolveValidTiles(ownerPos, playerGuid);
            if (validTargets.Count == 0)
            {
                Debug.Log("[ExplorationBehaviorService] No valid targets for selection.");
                return;
            }

            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller))
            {
                Debug.LogWarning("[ExplorationBehaviorService] ISelectionController not registered.");
                return;
            }

            // Pass-door por selección de casilla: durante el movimiento, las casillas
            // frente a puertas abiertas se ofrecen como targets extra (pintadas en rojo).
            // Seleccionarlas cruza a la sala vecina. Solo aplica al slot de Movement.
            var doorTiles = ResolveDoorTiles(behavior, grid, validTargets);

            _pendingBehavior = behavior;
            _state = State.Selecting;
            Debug.LogWarning($"[ExplorationBehaviorService] BeginSelection: behavior='{behavior.ActionName}' " +
                             $"validTargets={validTargets.Count} doorTiles={(doorTiles?.Count ?? 0)} → _state=Selecting (esperando click del user).");

            controller.OnSelectionCompleted += OnSelectionCompleted;
            controller.BeginSelection(new SelectionRequest
            {
                Settings = targetSettings,
                ValidTargets = validTargets,
                OwnerGuid = playerGuid,
                HighlightStyle = "move",
                DoorTiles = doorTiles,
            });
        }

        // Calcula las casillas frente a puerta para una selección de movimiento y las
        // agrega a validTargets (clickeables aunque caigan fuera del rango). Guarda el
        // mapa casilla→dirección en _doorTiles para resolver el cruce al completarse.
        // Devuelve null si el behavior no es Movement o no hay puertas atravesables.
        private System.Collections.Generic.HashSet<GridCoord> ResolveDoorTiles(
            HeroActionBehavior behavior, IGridManager grid, System.Collections.Generic.List<TargetRef> validTargets)
        {
            _doorTiles = null;
            _exitTiles = null;

            if (!behavior.IsBaseBehavior || behavior.Slot != HeroBehaviorSlot.Movement) return null;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return null;

            var fronts = DoorTileQuery.GetOpenDoorFrontTiles(dungeon, grid);
            var exitFronts = DoorTileQuery.GetOpenExitDoorFrontTiles(dungeon, grid);
            if (fronts.Count == 0 && exitFronts.Count == 0) return null;

            if (fronts.Count > 0) _doorTiles = fronts;
            if (exitFronts.Count > 0) _exitTiles = exitFronts;

            // Las dos clases de casilla (cruce de sala + salida de piso) se ofrecen como
            // targets extra con el mismo highlight "door"; el destino se distingue al confirmar.
            var coords = new System.Collections.Generic.HashSet<GridCoord>(fronts.Keys);
            coords.UnionWith(exitFronts);

            foreach (var coord in coords)
            {
                bool already = false;
                for (int i = 0; i < validTargets.Count; i++)
                {
                    if (validTargets[i] != null && validTargets[i].Coord == coord) { already = true; break; }
                }
                if (!already) validTargets.Add(TargetRef.At(coord));
            }

            return coords;
        }

        private void OnSelectionCompleted(TargetSelectionResult result)
        {
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller))
                controller.OnSelectionCompleted -= OnSelectionCompleted;

            var behavior = _pendingBehavior;
            var doorTiles = _doorTiles;
            var exitTiles = _exitTiles;
            _pendingBehavior = null;
            _doorTiles = null;
            _exitTiles = null;
            _state = State.Idle;

            if (behavior == null || !result.WasCompleted) return;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)) return;
            var playerGuid = playerService.PlayerGuid;

            // Ejecutar el movimiento normal: el player camina hasta la casilla elegida.
            ExecuteBehavior(behavior, playerGuid, result, null);

            // Si esa casilla es una "frente a puerta", cruzar a la sala vecina recién
            // cuando el pawn termine de caminar hasta ahí (no de forma instantánea).
            var picked = result.FirstSelectedCoord;
            if (picked.HasValue && doorTiles != null
                && doorTiles.TryGetValue(picked.Value, out var dir))
            {
                Debug.Log($"[ExplorationBehaviorService] Casilla frente a puerta dir={dir} seleccionada — caminar y cruzar al llegar.");
                CoroutineHost.Run(CrossDoorAfterArrival(playerGuid, dir));
            }
            // Si es la casilla frente a la puerta de SALIDA, transicionar de piso al llegar (#158).
            else if (picked.HasValue && exitTiles != null && exitTiles.Contains(picked.Value))
            {
                Debug.Log("[ExplorationBehaviorService] Casilla frente a puerta de salida seleccionada — caminar y transicionar de piso.");
                CoroutineHost.Run(ExitFloorAfterArrival(playerGuid));
            }
        }

        // Espera a que el pawn del player termine de caminar y recién ahí cruza la puerta.
        // Corre en el CoroutineHost porque este servicio es una clase plana. Si el pawn
        // ya estaba en la casilla (sin animación) cruza inmediatamente.
        private static IEnumerator CrossDoorAfterArrival(Guid playerGuid, DoorDirection dir)
        {
            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
            {
                var wait = visuals.WaitForMoveComplete(playerGuid);
                if (wait != null) yield return wait;
            }

            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon) && dungeon != null)
            {
                Debug.Log($"[ExplorationBehaviorService] Player llegó a la casilla frente a puerta dir={dir} — EnterRoomByDoor.");
                dungeon.EnterRoomByDoor(dir);
            }
        }

        // Espera a que el pawn llegue a la casilla de salida y dispara la transición de
        // piso (#158). FloorProgressionService consume OnFloorExitRequested.
        private static IEnumerator ExitFloorAfterArrival(Guid playerGuid)
        {
            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
            {
                var wait = visuals.WaitForMoveComplete(playerGuid);
                if (wait != null) yield return wait;
            }

            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon) && dungeon != null
                && dungeon.CurrentRoomInstance != null)
            {
                var roomId = dungeon.CurrentRoomInstance.InstanceId;
                Debug.Log("[ExplorationBehaviorService] Player llegó a la puerta de salida — OnFloorExitRequested.");
                EventManager.Trigger(EventName.OnFloorExitRequested, roomId);
            }
        }

        private void ExecuteBehavior(HeroActionBehavior behavior, Guid playerGuid,
            TargetSelectionResult selectionResult, IReadOnlyList<int> diceResult,
            ComboDetectionResult? matchedCombo = null,
            int? actionRollEffectiveTotal = null)
        {
            var ctx = new HeroBehaviorContext
            {
                SourceEntity = new Entity { Guid = playerGuid },
                SelectionResult = selectionResult,
                DiceResult = diceResult,
                MatchedComboResult = matchedCombo,
                ActionRollEffectiveTotal = actionRollEffectiveTotal,
            };

            behavior.Execute(ctx);
        }

        private void OnPhaseEnter(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if ((GamePhase)args[0] == GamePhase.Exploration)
            {
                Debug.Log("[ExplorationBehaviorService] OnPhaseEnter(Exploration) — _state cambia a Idle.");
                _state = State.Idle;
            }
        }

        private void OnPhaseExit(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if ((GamePhase)args[0] == GamePhase.Exploration)
            {
                CancelSelection();
                if (_state == State.Rolling
                    && ServiceLocator.TryGetService<IActionRollService>(out var rollService)
                    && rollService != null
                    && rollService.IsActive)
                {
                    rollService.Cancel();
                }
                _pendingBehavior = null;
                _state = State.Inactive;
            }
        }
    }
}
