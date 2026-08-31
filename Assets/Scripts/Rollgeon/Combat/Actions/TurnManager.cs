using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Combat.Actions
{
    /// <summary>
    /// Servicio runtime (clase plana, NO <c>MonoBehaviour</c>) que enforcea el
    /// <b>action economy</b> de Rollgeon. TECHNICAL.md §12.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gate uniforme de ejecucion de acciones:
    /// </para>
    /// <list type="number">
    ///   <item>Roll cost — las acciones directas (sin dados) cuestan 1 roll flat,
    ///         cobrado atomicamente por <see cref="IRollPoolService.TrySpendRolls"/>;
    ///         las acciones con dados se cobran por tirada en el handoff.</item>
    ///   <item>Ruleset override — hook para <c>RulesetSO.ForbiddenActionIds</c> (Balance#0101, stub hoy).</item>
    /// </list>
    /// <para>
    /// <b>Sin limite de acciones por turno.</b> Mientras queden rolls en el pool, cualquier
    /// accion (incluido el movimiento) puede repetirse en el mismo turno. El unico
    /// presupuesto del turno es el pool de rolls.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> <see cref="Register"/> lo invoca <c>ServiceBootstrapSO</c> en el
    /// bootstrap global; resuelve <see cref="IRollPoolService"/> y se registra a si mismo
    /// en el <see cref="ServiceLocator"/>.
    /// </para>
    /// </remarks>
    public sealed class TurnManager : IPreloadableService, IDisposable
    {
        private IRollPoolService _rolls;
        private ActionCatalogSO _actions;
        private RulesetSO _ruleset;

        /// <summary>
        /// Quien tiene el turno ahora, segun <see cref="EventName.OnTurnStarted"/>.
        /// <see cref="Guid.Empty"/> fuera de turno (o fuera de combate, donde nadie lo
        /// dispara). Solo lo consultan las acciones de tipo <see cref="ActionType.UseItem"/>.
        /// </summary>
        private Guid _actingGuid;

        /// <summary>
        /// <c>ActionId</c> de items ya usados en el turno actual. Se vacia en cada
        /// <see cref="EventName.OnTurnStarted"/>. Dos items que comparten
        /// <c>ActionId</c> (ej. todas las pociones con <c>item.potion</c>) se limitan
        /// entre si a uno por turno.
        /// </summary>
        private readonly HashSet<string> _itemActionsUsedThisTurn =
            new HashSet<string>(StringComparer.Ordinal);

        private EventManager.EventReceiver _onTurnStartedHandler;
        private EventManager.EventReceiver _onTurnFinishedHandler;

        /// <summary>Corre despues de <see cref="RollPoolService"/> (<c>Priority=50</c>).</summary>
        public int Priority => 60;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            if (!ServiceLocator.TryGetService<IRollPoolService>(out _rolls) || _rolls == null)
            {
                Debug.LogError("[TurnManager] IRollPoolService no esta registrado en ServiceLocator. " +
                               "Agregar RollPoolServiceBootstrap a ServiceBootstrapSO.ExtraServices con Priority < 60.");
                return;
            }

            // Catalog y ruleset son opcionales para el runtime (el TurnManager no los requiere
            // para operar — los usa solo para el hook IsForbiddenByRuleset y referencias futuras).
            ServiceLocator.TryGetService<ActionCatalogSO>(out _actions);
            ServiceLocator.TryGetService<RulesetSO>(out _ruleset);

            SubscribeTurnTracking();
            ServiceLocator.AddService<TurnManager>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            UnsubscribeTurnTracking();
            _feedbackWaitDepth = 0;
            _feedbackContinuations.Clear();
        }

        // ======================================================================
        // Turno actual + ActionIds de item gastados (solo ActionType.UseItem)
        // ======================================================================

        /// <summary>
        /// Los items con <c>ConsumesAction</c> solo se usan en el turno propio y una vez
        /// por <c>ActionId</c>. Es una regla de items: el resto de las acciones sigue sin
        /// limite por turno (el unico presupuesto es el pool de rolls).
        /// </summary>
        private void SubscribeTurnTracking()
        {
            if (_onTurnStartedHandler != null) return;

            _onTurnStartedHandler = HandleTurnStarted;
            _onTurnFinishedHandler = HandleTurnFinished;
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
        }

        private void UnsubscribeTurnTracking()
        {
            if (_onTurnStartedHandler == null) return;

            EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
            EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            _onTurnStartedHandler = null;
            _onTurnFinishedHandler = null;
            _actingGuid = Guid.Empty;
            _itemActionsUsedThisTurn.Clear();
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            _actingGuid = guid;
            _itemActionsUsedThisTurn.Clear();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _actingGuid) return;
            _actingGuid = Guid.Empty;
        }

        /// <summary>
        /// Hook de EditMode tests: setea el actor del turno sin pasar por el bus.
        /// </summary>
        public void SetActingGuidForTests(Guid guid)
        {
            _actingGuid = guid;
            _itemActionsUsedThisTurn.Clear();
        }

        /// <summary>
        /// <c>true</c> si <paramref name="actionId"/> ya se gasto en este turno.
        /// Seam para el HUD, que necesita saberlo sin intentar la accion.
        /// </summary>
        public bool IsItemActionUsedThisTurn(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && _itemActionsUsedThisTurn.Contains(actionId);
        }

        /// <summary>
        /// <c>true</c> si <paramref name="guid"/> tiene el turno. Fuera de combate nadie
        /// dispara <c>OnTurnStarted</c>, asi que el turno no gatea nada.
        /// </summary>
        public bool IsActingTurn(Guid guid)
        {
            if (_rolls == null || !_rolls.IsCombatActive) return true;
            return _actingGuid == guid;
        }

        // ======================================================================
        // Test / dependency injection hook
        // ======================================================================

        /// <summary>
        /// Constructor-like hook para EditMode tests: inyecta dependencias sin pasar
        /// por <see cref="ServiceLocator"/> (igual que <see cref="Register"/> — minus el
        /// <c>ServiceLocator.AddService</c>, que el test hace si lo necesita).
        /// </summary>
        public void ConfigureForTests(IRollPoolService rolls, ActionCatalogSO actions, RulesetSO ruleset)
        {
            _rolls = rolls;
            _actions = actions;
            _ruleset = ruleset;
        }

        // ======================================================================
        // API publica — action economy
        // ======================================================================

        /// <summary>
        /// Valida que <paramref name="action"/> se puede ejecutar ahora: ruleset permit
        /// y hay al menos 1 roll en el pool. No muta ningun estado.
        /// </summary>
        /// <param name="action">Definicion del catalogo. Null = rechazo con reason.</param>
        /// <param name="playerGuid">Actor que intenta ejecutar la accion.</param>
        /// <param name="reason">Mensaje human-readable del rechazo (null si la funcion retorna true).</param>
        public bool CanExecute(ActionDefinitionSO action, Guid playerGuid, out string reason)
        {
            reason = null;

            if (action == null)
            {
                reason = "Action is null.";
                return false;
            }

            if (IsForbiddenByRuleset(action.ActionId))
            {
                reason = $"Action '{action.ActionId}' is forbidden by the active ruleset.";
                return false;
            }

            // Reglas exclusivas de items activos con ConsumesAction. El resto de las
            // acciones sigue sin limite por turno — el unico presupuesto son los rolls.
            if (action.Type == ActionType.UseItem)
            {
                if (!IsActingTurn(playerGuid))
                {
                    reason = "Not your turn.";
                    return false;
                }
                if (IsItemActionUsedThisTurn(action.ActionId))
                {
                    reason = $"Action '{action.ActionId}' was already used this turn.";
                    return false;
                }
            }

            if (_rolls == null)
            {
                reason = "IRollPoolService not available.";
                return false;
            }

            // El pool solo existe en combate: fuera de combate (items en exploración)
            // no se gatea ni se cobra.
            if (_rolls.IsCombatActive)
            {
                int available = _rolls.GetCurrent(playerGuid);
                if (available < 1)
                {
                    reason = $"Not enough rolls ({available}/1).";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Camino canonico de ejecucion: valida -> cobra 1 roll (en combate) -> ejecuta effect.
        /// Dispatch del <see cref="ActionDefinitionSO.BackingAsset"/> es
        /// responsabilidad del caller externo (plan §10 R1).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Si <see cref="ActionDefinitionSO.Effect"/> tiene efectos, los ejecuta via
        /// <see cref="EffectData.TryExecute"/> y devuelve su resultado.
        /// </para>
        /// <para>
        /// Si <see cref="ActionDefinitionSO.Effect"/> esta vacio, es un "permit no-op":
        /// se cobra 1 roll y se devuelve true — el dispatcher externo
        /// (ComboExecutor T97b / ItemSystem / AI) corre el <c>BackingAsset</c>.
        /// </para>
        /// <para>
        /// Si el effect retorna false, el roll <b>ya fue cobrado</b> (mismo patron del
        /// pseudo-code del §12.6) — el jugador puede intentar otra accion pero perdio el roll.
        /// </para>
        /// </remarks>
        public bool TryExecute(ActionDefinitionSO action, Guid playerGuid, EffectContext ctx)
        {
            if (!CanExecute(action, playerGuid, out _)) return false;
            if (_rolls.IsCombatActive && !_rolls.TrySpendRolls(playerGuid, 1)) return false;

            // El ActionId se marca al pasar el gate, no al terminar el efecto: el roll ya
            // se cobro y el turno ya se consumio aunque el efecto devuelva false.
            if (action.Type == ActionType.UseItem && !string.IsNullOrEmpty(action.ActionId))
                _itemActionsUsedThisTurn.Add(action.ActionId);

            if (action.Effect == null || action.Effect.Effects == null || action.Effect.Effects.Count == 0)
                return true;

            var preCtx = BuildPreCtx(ctx);
            return action.Effect.TryExecute(ctx, preCtx);
        }

        // ======================================================================
        // HeroActionBehavior overloads
        // ======================================================================

        /// <summary>
        /// Valida que <paramref name="behavior"/> se puede ejecutar ahora.
        /// Misma semantica que el overload de <see cref="ActionDefinitionSO"/>:
        /// ruleset, preconditions del behavior y roll-pool check.
        /// </summary>
        public bool CanExecute(HeroActionBehavior behavior, Guid playerGuid, out string reason)
        {
            reason = null;

            if (behavior == null)
            {
                reason = "Behavior is null.";
                return false;
            }

            if (IsForbiddenByRuleset(behavior.ActionName))
            {
                reason = $"Behavior '{behavior.ActionName}' is forbidden by the active ruleset.";
                return false;
            }

            if (_rolls == null)
            {
                reason = "IRollPoolService not available.";
                return false;
            }

            // Las preconditions del behavior se evalúan antes del roll check para no
            // cobrar un roll cuando la cadena va a abortar igual (ej. Heal sin poción).
            if (!behavior.HasUsableEffectGroup(playerGuid, Guid.Empty, out var pcReason))
            {
                reason = pcReason ?? $"Behavior '{behavior.ActionName}' has no usable effect group.";
                return false;
            }

            if (_rolls.IsCombatActive)
            {
                int available = _rolls.GetCurrent(playerGuid);
                if (available < 1)
                {
                    reason = $"Not enough rolls ({available}/1).";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Ejecuta un <see cref="HeroActionBehavior"/>: valida, cobra 1 roll y ejecuta
        /// via <see cref="HeroActionBehavior.Execute"/>.
        /// </summary>
        public bool TryExecute(HeroActionBehavior behavior, Guid playerGuid, BehaviorContext ctx)
        {
            if (!CanExecute(behavior, playerGuid, out _)) return false;
            if (_rolls.IsCombatActive && !_rolls.TrySpendRolls(playerGuid, 1)) return false;

            behavior.Execute(ctx);
            return true;
        }

        /// <summary>
        /// Ejecuta sin cobrar: el caller (handoff de dados) ya pagó el roll de la
        /// tirada.
        /// </summary>
        public bool TryExecuteRollsPrepaid(HeroActionBehavior behavior, Guid playerGuid, BehaviorContext ctx)
        {
            if (behavior == null) return false;

            behavior.Execute(ctx);
            return true;
        }

        // ======================================================================
        // Feedback blocking hooks (§10.9)
        // ======================================================================

        private int _feedbackWaitDepth;
        private readonly List<Action> _feedbackContinuations = new List<Action>();

        /// <summary>
        /// <c>true</c> mientras haya al menos un feedback bloqueante en vuelo. El resolver
        /// de effects puede chequear este flag para suspender el avance de la cadena.
        /// </summary>
        public bool IsWaitingForFeedback => _feedbackWaitDepth > 0;

        /// <summary>
        /// Marca el inicio de un feedback bloqueante. Llamado por <c>EffPlayFeedback</c>
        /// antes de <see cref="IFeedbackService.RequestFeedbackBlocking"/>. TECHNICAL.md §10.9.
        /// </summary>
        public void BeginFeedbackWait()
        {
            _feedbackWaitDepth++;
        }

        /// <summary>
        /// Callback que el <see cref="Rollgeon.Feedback.IFeedbackService"/> invoca cuando
        /// el feedback termina. Contraparte de <see cref="BeginFeedbackWait"/>.
        /// </summary>
        public void OnFeedbackComplete()
        {
            if (_feedbackWaitDepth > 0) _feedbackWaitDepth--;
            if (_feedbackWaitDepth == 0) FlushFeedbackContinuations();
        }

        /// <summary>
        /// Corre <paramref name="continuation"/> cuando no queden feedbacks bloqueantes en
        /// vuelo. Si no hay ninguno, corre <b>sincrónico</b> — el caller no debe asumir
        /// que se difiere.
        /// </summary>
        /// <remarks>
        /// Existe para los flujos que no son coroutines y no pueden usar
        /// <see cref="WaitForFeedbackCompletion"/>: el chain del héroe lo ejecuta
        /// <c>CombatHandoffService</c>, que no es MonoBehaviour. Sin esto, un efecto
        /// diferido al frame de impacto (§10.8, <c>StepSource.InlineEffect</c>) resolvería
        /// <i>después</i> de que el chain ya avanzó de fase.
        /// </remarks>
        public void RunWhenFeedbackSettles(Action continuation)
        {
            if (continuation == null) return;
            if (_feedbackWaitDepth <= 0)
            {
                continuation();
                return;
            }
            _feedbackContinuations.Add(continuation);
        }

        /// <summary>
        /// Vacía la cola invocando cada continuación. Copia y limpia <b>antes</b> de invocar:
        /// una continuación puede encolar otra (fase siguiente del chain) y mutar la lista
        /// en pleno recorrido.
        /// </summary>
        private void FlushFeedbackContinuations()
        {
            if (_feedbackContinuations.Count == 0) return;
            var pending = _feedbackContinuations.ToArray();
            _feedbackContinuations.Clear();
            for (int i = 0; i < pending.Length; i++)
            {
                try { pending[i]?.Invoke(); }
                catch (Exception e)
                {
                    // Una continuación que explota no debe dejar colgadas a las demás ni
                    // envenenar el contador de feedback.
                    Debug.LogError($"[TurnManager] Feedback continuation falló: {e}");
                }
            }
        }

        /// <summary>
        /// Coroutine helper que yieldea hasta que todos los feedbacks bloqueantes en vuelo
        /// hayan disparado su <see cref="OnFeedbackComplete"/>, con un timeout de seguridad.
        /// </summary>
        /// <remarks>
        /// Usado por <see cref="EffectData.ExecuteCoroutine"/> y por el combat driver entre
        /// turnos. El timeout degrada a fuerza bruta (resetea el counter) si se supera —
        /// evita deadlocks si una callback de feedback se pierde.
        /// </remarks>
        public static IEnumerator WaitForFeedbackCompletion(TurnManager manager, float timeoutSeconds = 10f)
        {
            if (manager == null) yield break;
            float deadline = Time.time + Mathf.Max(0.1f, timeoutSeconds);
            while (manager._feedbackWaitDepth > 0 && Time.time < deadline)
                yield return null;

            if (manager._feedbackWaitDepth > 0)
            {
                Debug.LogWarning($"[TurnManager] Feedback wait timed out after {timeoutSeconds}s — " +
                                 $"force-resetting depth from {manager._feedbackWaitDepth} to 0.");
                manager._feedbackWaitDepth = 0;
            }

            // El force-reset saltea OnFeedbackComplete, así que las continuaciones encoladas
            // quedarían huérfanas — y en el chain del héroe eso es un turno colgado.
            manager.FlushFeedbackContinuations();
        }

        // ======================================================================
        // Helpers privados
        // ======================================================================

        /// <summary>
        /// Construye un <see cref="PreConditionContext"/> a partir del
        /// <see cref="EffectContext"/> actual. <b>Inline a proposito — no modificamos
        /// F#0004.</b> El EffectContext de F#0004 no expone <c>BuildPreConditionContext()</c>,
        /// entonces lo construimos aca con los campos que si expone (plan §10 R3).
        /// </summary>
        private PreConditionContext BuildPreCtx(EffectContext ctx)
        {
            // [FOLLOWUP F#0004]: si EffectContext gana un helper publico
            // BuildPreConditionContext(), reemplazar este metodo por una delegacion.
            if (ctx == null) return new PreConditionContext();
            return new PreConditionContext
            {
                OwnerGuid = ctx.SourceGuid,
                OpponentGuid = ctx.TargetGuid,
                Entity = ctx.SourceEntity,
            };
        }

        /// <summary>
        /// Hook point para reglas que prohíben acciones por id. Hoy cubre el gate del
        /// tutorial (<see cref="Rollgeon.Tutorial.ITutorialActionGateService"/> — solo
        /// registrado durante el tutorial; ausente = nada prohibido). Backstop de
        /// ejecución: bloquea ambos paths de <see cref="CanExecute"/> aunque la UI
        /// deje pasar un click/hotkey.
        /// </summary>
        private bool IsForbiddenByRuleset(string actionId)
        {
            if (ServiceLocator.TryGetService<Rollgeon.Tutorial.ITutorialActionGateService>(out var tutorialGate)
                && tutorialGate != null && tutorialGate.IsActionLocked(actionId))
            {
                return true;
            }

            // [FOLLOWUP Balance#0101]: read RulesetSO.ForbiddenActionIds (not yet defined).
            // Cuando Balance#0101 agregue el campo:
            //   return _ruleset != null && _ruleset.ForbiddenActionIds != null
            //       && _ruleset.ForbiddenActionIds.Contains(actionId);
            return false;
        }
    }
}
