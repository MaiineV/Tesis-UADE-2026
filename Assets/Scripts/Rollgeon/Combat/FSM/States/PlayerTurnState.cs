using System;
using Patterns;
using Patterns.FSM;
using Rollgeon.Combat.FSM.States.PlayerTurn;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;

namespace Rollgeon.Combat.FSM.States
{
    public sealed class PlayerTurnState : BaseState<CombatContext, CombatInput>
    {
        internal EnemyTurnState Enemy;
        internal CombatExitState ExitRef;
        internal PlayerTurnState Self;

        private Guid _actingGuid;

        private StateMachine<PlayerTurnSubContext, PlayerTurnSubInput> _subFSM;
        private PlayerTurnSubContext _subCtx;
        private PlayerIdleSubState _idle;
        private PlayerSelectingSubState _selecting;
        private PlayerExecutingSubState _executing;

        public PlayerTurnState(CombatContext context) : base(context) { }

        public override void Enter(CombatInput input)
        {
            _actingGuid = Context.PlayerId;

            _subCtx = new PlayerTurnSubContext
            {
                CombatContext = Context,
                ActingGuid = _actingGuid,
            };

            _idle = new PlayerIdleSubState(_subCtx);
            _selecting = new PlayerSelectingSubState(_subCtx);
            _executing = new PlayerExecutingSubState(_subCtx);

            _idle.Selecting = _selecting;
            _idle.Executing = _executing;
            _selecting.Executing = _executing;
            _executing.Idle = _idle;

            _subFSM = new StateMachine<PlayerTurnSubContext, PlayerTurnSubInput>(_subCtx, _idle);

            _selecting.SetOwnerFSM(_subFSM);
            _executing.SetOwnerFSM(_subFSM);

            _subFSM.Start(PlayerTurnSubInput.None);

            EventManager.Trigger(EventName.OnTurnStarted, _actingGuid);
        }

        public override void Update()
        {
            _subFSM?.Update();
        }

        public override void Exit(CombatInput input)
        {
            _subFSM?.Stop();

            EventManager.Trigger(EventName.OnTurnFinished, _actingGuid);

            if (input != CombatInput.CombatEnded)
            {
                Context.TurnOrder.Advance();
            }
        }

        /// <param name="onComplete">
        /// Callback invocado cuando la acción terminó de ejecutarse — tras resolver la
        /// selección de target si la requería. El handoff lo usa para postergar el
        /// desbloqueo de la UI hasta que la acción realmente corre (BUG-013). Se invoca
        /// también en el path de aborto para que el caller no quede esperando para siempre.
        /// </param>
        public void RequestAction(HeroActionBehavior action, BehaviorContext behaviorContext, Action onComplete = null)
        {
            if (_subCtx == null || _subFSM == null)
            {
                UnityEngine.Debug.LogWarning($"[PlayerTurnState] RequestAction aborted — subCtx={_subCtx != null} subFSM={_subFSM != null}");
                onComplete?.Invoke();
                return;
            }

            _subCtx.PendingAction = action;
            _subCtx.PendingBehaviorContext = behaviorContext;
            _subCtx.OnActionComplete = onComplete;

            bool needsSelection = action.HasEffectsWithSelectionAt(SelectionTiming.BeforeRoll);
            UnityEngine.Debug.Log($"[PlayerTurnState] RequestAction '{action.ActionName}' needsSelection={needsSelection} currentSubState={_subFSM.Current?.GetType().Name}");

            if (needsSelection)
                _subFSM.SendInput(PlayerTurnSubInput.ActionRequiresSelection);
            else
                _subFSM.SendInput(PlayerTurnSubInput.ActionDirect);
        }

        public override bool CheckInput(CombatInput input, out BaseState<CombatContext, CombatInput> next)
        {
            switch (input)
            {
                case CombatInput.PlayerActionDone:
                    next = null;
                    return false;

                case CombatInput.PlayerEndTurn:
                    var order = Context.TurnOrder.OrderForRound;
                    if (order == null || order.Count == 0)
                    {
                        next = ExitRef;
                        return true;
                    }

                    int curIndex = IndexOf(order, Context.TurnOrder.Current);
                    if (curIndex < 0)
                    {
                        next = Self;
                        return true;
                    }
                    int nextIndex = (curIndex + 1) % order.Count;
                    Guid nextGuid = order[nextIndex];

                    next = (nextGuid == Context.PlayerId)
                        ? (BaseState<CombatContext, CombatInput>)Self
                        : Enemy;
                    return true;

                case CombatInput.CombatEnded:
                    next = ExitRef;
                    return true;

                default:
                    next = null;
                    return false;
            }
        }

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<Guid> list, Guid g)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == g) return i;
            }
            return -1;
        }
    }
}
