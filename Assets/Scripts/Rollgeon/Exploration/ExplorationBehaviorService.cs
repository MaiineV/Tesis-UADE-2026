using System;
using System.Linq;
using Patterns;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Exploration
{
    public sealed class ExplorationBehaviorService : IExplorationBehaviorService, IDisposable
    {
        private enum State { Inactive, Idle, Selecting }

        private State _state = State.Inactive;
        private HeroActionBehavior _pendingBehavior;

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

        public void OnBehaviorSelected(int index)
        {
            Debug.Log($"[ExplorationBehaviorService] OnBehaviorSelected({index}) — _state={_state}");
            if (_state != State.Idle) return;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService))
            {
                Debug.LogWarning("[ExplorationBehaviorService] IPlayerService not registered.");
                return;
            }

            var hero = playerService.CurrentHero;
            if (hero == null) return;

            var playerGuid = playerService.PlayerGuid;
            var behaviors = hero.GetBehaviorsForPhase(GamePhase.Exploration);

            if (index < 0 || index >= behaviors.Count)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] Index {index} out of range ({behaviors.Count} behaviors).");
                return;
            }

            var behavior = behaviors[index];
            if (behavior == null) return;

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

            if (behavior.EnergyCost > 0)
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

            ExecuteBehavior(behavior, playerGuid, null);
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
            _state = State.Idle;
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
                ExecuteBehavior(behavior, playerGuid, null);
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
                ExecuteBehavior(behavior, playerGuid, selfResult);
                return;
            }

            if (targetSettings.AutoResolve)
            {
                var autoResult = targetSettings.AutoResolveTargets(ownerPos, playerGuid);
                ExecuteBehavior(behavior, playerGuid, autoResult);
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

            _pendingBehavior = behavior;
            _state = State.Selecting;

            controller.OnSelectionCompleted += OnSelectionCompleted;
            controller.BeginSelection(new SelectionRequest
            {
                Settings = targetSettings,
                ValidTargets = validTargets,
                OwnerGuid = playerGuid,
                HighlightStyle = "move",
            });
        }

        private void OnSelectionCompleted(TargetSelectionResult result)
        {
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller))
                controller.OnSelectionCompleted -= OnSelectionCompleted;

            var behavior = _pendingBehavior;
            _pendingBehavior = null;
            _state = State.Idle;

            if (behavior == null || !result.WasCompleted) return;

            if (ServiceLocator.TryGetService<IPlayerService>(out var playerService))
                ExecuteBehavior(behavior, playerService.PlayerGuid, result);
        }

        private void ExecuteBehavior(HeroActionBehavior behavior, Guid playerGuid,
            TargetSelectionResult selectionResult)
        {
            var ctx = new HeroBehaviorContext
            {
                SourceEntity = new Entity { Guid = playerGuid },
                SelectionResult = selectionResult,
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
                _state = State.Inactive;
            }
        }
    }
}
