using System;
using System.Collections.Generic;
using Patterns;
using Patterns.FSM;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;

namespace Rollgeon.Combat.FSM.States.PlayerTurn
{
    public sealed class PlayerSelectingSubState : BaseState<PlayerTurnSubContext, PlayerTurnSubInput>
    {
        internal PlayerExecutingSubState Executing;

        private StateMachine<PlayerTurnSubContext, PlayerTurnSubInput> _ownerFSM;
        private ISelectionController _controller;

        public PlayerSelectingSubState(PlayerTurnSubContext context) : base(context) { }

        public void SetOwnerFSM(StateMachine<PlayerTurnSubContext, PlayerTurnSubInput> fsm)
        {
            _ownerFSM = fsm;
        }

        public override void Enter(PlayerTurnSubInput input)
        {
            UnityEngine.Debug.Log("[PlayerSelectingSubState] Enter");

            var action = Context.PendingAction;
            if (action == null || action.Effects == null)
            {
                UnityEngine.Debug.LogWarning("[PlayerSelectingSubState] PendingAction or Effects is null — skipping");
                _ownerFSM?.SendInput(PlayerTurnSubInput.SelectionCompleted);
                return;
            }

            SelectionSettings targetSettings = null;
            foreach (var group in action.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff != null && eff.RequiresSelectionAt(SelectionTiming.BeforeResolve))
                    {
                        targetSettings = eff.GetSelection();
                        break;
                    }
                }
                if (targetSettings != null) break;
            }

            if (targetSettings == null)
            {
                UnityEngine.Debug.LogWarning("[PlayerSelectingSubState] No effect with BeforeResolve selection found — skipping");
                _ownerFSM?.SendInput(PlayerTurnSubInput.SelectionCompleted);
                return;
            }

            UnityEngine.Debug.Log($"[PlayerSelectingSubState] SelectionSettings found — IsGlobal={targetSettings.IsGlobal} Range={targetSettings.Range} RequireEmpty={targetSettings.RequireEmptySlot} AutoAccept={targetSettings.AutoAccept}");

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                UnityEngine.Debug.LogWarning("[PlayerSelectingSubState] IGridManager not registered — skipping");
                _ownerFSM?.SendInput(PlayerTurnSubInput.SelectionCompleted);
                return;
            }

            if (!grid.TryGetPosition(Context.ActingGuid, out var ownerPos))
            {
                UnityEngine.Debug.LogWarning($"[PlayerSelectingSubState] TryGetPosition failed for {Context.ActingGuid} — skipping");
                _ownerFSM?.SendInput(PlayerTurnSubInput.SelectionCompleted);
                return;
            }

            UnityEngine.Debug.Log($"[PlayerSelectingSubState] Owner position = {ownerPos}");

            var validTargets = targetSettings.ResolveValidTiles(ownerPos);
            UnityEngine.Debug.Log($"[PlayerSelectingSubState] ResolveValidTiles returned {validTargets.Count} tiles");

            if (!ServiceLocator.TryGetService<ISelectionController>(out _controller))
            {
                UnityEngine.Debug.LogWarning("[PlayerSelectingSubState] ISelectionController not registered — skipping");
                _ownerFSM?.SendInput(PlayerTurnSubInput.SelectionCompleted);
                return;
            }

            _controller.OnSelectionCompleted += OnSelectionDone;

            var request = new SelectionRequest
            {
                Settings = targetSettings,
                ValidTargets = validTargets,
                OwnerGuid = Context.ActingGuid,
                HighlightStyle = "move",
            };

            UnityEngine.Debug.Log("[PlayerSelectingSubState] BeginSelection — waiting for player click");
            _controller.BeginSelection(request);
        }

        public override void Exit(PlayerTurnSubInput input)
        {
            if (_controller != null)
            {
                _controller.OnSelectionCompleted -= OnSelectionDone;
                _controller = null;
            }
        }

        private void OnSelectionDone(TargetSelectionResult result)
        {
            Context.SelectionResult = result;
            _ownerFSM?.SendInput(PlayerTurnSubInput.SelectionCompleted);
        }

        public override bool CheckInput(PlayerTurnSubInput input,
            out BaseState<PlayerTurnSubContext, PlayerTurnSubInput> next)
        {
            if (input == PlayerTurnSubInput.SelectionCompleted)
            {
                next = Executing;
                return true;
            }

            next = null;
            return false;
        }
    }
}
