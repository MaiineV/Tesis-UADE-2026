using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.EnergyLib;
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
    /// Mantiene el set de <see cref="ActionDefinitionSO.ActionId"/> ya usados en el
    /// turno actual, y actua como gate uniforme para:
    /// </para>
    /// <list type="number">
    ///   <item>Repetition constraint (§12.6) — opt-in via <see cref="ActionDefinitionSO.BlockOnRepeat"/>.</item>
    ///   <item>Energy cost — cobrado atomicamente por <see cref="IEnergyService.SpendEnergy"/>.</item>
    ///   <item>Ruleset override — hook para <c>RulesetSO.ForbiddenActionIds</c> (Balance#0101, stub hoy).</item>
    /// </list>
    /// <para>
    /// <b>Lifecycle.</b> <see cref="Register"/> lo invoca <c>ServiceBootstrapSO</c> en el
    /// bootstrap global; resuelve <see cref="IEnergyService"/> y se registra a si mismo
    /// en el <see cref="ServiceLocator"/>. Tambien suscribe <c>EventName.OnTurnStarted</c>
    /// para limpiar el set entre turnos.
    /// </para>
    /// <para>
    /// <b>Semantica del clear.</b> "Mismo turno" = entre dos <c>OnTurnStarted</c>
    /// consecutivos. El TurnManager es unico global y no trackea per-actor; con el
    /// ciclo player->enemy->player el set se limpia 2x por round, que es la semantica
    /// correcta de "slot individual del actor activo". Ver plan §10 R4.
    /// </para>
    /// </remarks>
    public sealed class TurnManager : IPreloadableService, IDisposable
    {
        private IEnergyService _energy;
        private ActionCatalogSO _actions;
        private RulesetSO _ruleset;

        private readonly HashSet<string> _actionsUsedThisTurn = new HashSet<string>();
        private EventManager.EventReceiver _onTurnStartedHandler;

        /// <summary>Corre despues de <see cref="EnergyService"/> (<c>Priority=50</c>).</summary>
        public int Priority => 60;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            if (!ServiceLocator.TryGetService<IEnergyService>(out _energy) || _energy == null)
            {
                Debug.LogError("[TurnManager] IEnergyService no esta registrado en ServiceLocator. " +
                               "Agregar EnergyServiceBootstrap a ServiceBootstrapSO.ExtraServices con Priority < 60.");
                return;
            }

            // Catalog y ruleset son opcionales para el runtime (el TurnManager no los requiere
            // para operar — los usa solo para el hook IsForbiddenByRuleset y referencias futuras).
            ServiceLocator.TryGetService<ActionCatalogSO>(out _actions);
            ServiceLocator.TryGetService<RulesetSO>(out _ruleset);

            _onTurnStartedHandler = OnTurnStartedExternal;
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);

            ServiceLocator.AddService<TurnManager>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onTurnStartedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
                _onTurnStartedHandler = null;
            }
            _actionsUsedThisTurn.Clear();
        }

        // ======================================================================
        // Test / dependency injection hook
        // ======================================================================

        /// <summary>
        /// Constructor-like hook para EditMode tests: inyecta dependencias sin pasar
        /// por <see cref="ServiceLocator"/>. Tambien suscribe el handler de
        /// <c>OnTurnStarted</c> (igual que <see cref="Register"/> — minus el
        /// <c>ServiceLocator.AddService</c>, que el test hace si lo necesita).
        /// </summary>
        public void ConfigureForTests(IEnergyService energy, ActionCatalogSO actions, RulesetSO ruleset)
        {
            _energy = energy;
            _actions = actions;
            _ruleset = ruleset;

            _onTurnStartedHandler = OnTurnStartedExternal;
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
        }

        // ======================================================================
        // API publica — action economy
        // ======================================================================

        /// <summary>
        /// Valida que <paramref name="action"/> se puede ejecutar ahora: ruleset permit,
        /// no es repeat bloqueado, y hay energia suficiente. No muta ningun estado.
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

            if (action.BlockOnRepeat && _actionsUsedThisTurn.Contains(action.ActionId))
            {
                reason = $"Action '{action.ActionId}' already used this turn.";
                return false;
            }

            if (_energy == null)
            {
                reason = "IEnergyService not available.";
                return false;
            }

            int available = _energy.GetCurrent(playerGuid);
            if (available < action.EnergyCost)
            {
                reason = $"Not enough energy ({available}/{action.EnergyCost}).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Camino canonico de ejecucion: valida -> cobra energia -> ejecuta effect ->
        /// marca usada. Dispatch del <see cref="ActionDefinitionSO.BackingAsset"/> es
        /// responsabilidad del caller externo (plan §10 R1).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Si <see cref="ActionDefinitionSO.Effect"/> tiene efectos, los ejecuta via
        /// <see cref="EffectData.TryExecute"/>; la accion se marca como usada solo si
        /// el effect retorno true.
        /// </para>
        /// <para>
        /// Si <see cref="ActionDefinitionSO.Effect"/> esta vacio, es un "permit no-op":
        /// se cobra energia, se marca usada, y se devuelve true — el dispatcher externo
        /// (ComboExecutor T97b / ItemSystem / AI) corre el <c>BackingAsset</c>.
        /// </para>
        /// <para>
        /// Si el effect retorna false, la energia <b>ya fue cobrada</b> (mismo patron del
        /// pseudo-code del §12.6). La accion NO se marca como usada — el jugador puede
        /// intentar otra accion pero perdio la energia.
        /// </para>
        /// </remarks>
        public bool TryExecute(ActionDefinitionSO action, Guid playerGuid, EffectContext ctx)
        {
            if (!CanExecute(action, playerGuid, out _)) return false;
            if (!_energy.SpendEnergy(playerGuid, action.EnergyCost)) return false;

            bool ok = true;
            if (action.Effect != null && action.Effect.Effects != null && action.Effect.Effects.Count > 0)
            {
                var preCtx = BuildPreCtx(ctx);
                ok = action.Effect.TryExecute(ctx, preCtx);
            }

            // Solo trackear acciones con BlockOnRepeat=true — las repetibles (movement,
            // §12.6) no entran al set para que UsedActionsCount refleje las "consumidas".
            if (ok && action.BlockOnRepeat) _actionsUsedThisTurn.Add(action.ActionId);
            return ok;
        }

        // ======================================================================
        // HeroActionBehavior overloads
        // ======================================================================

        /// <summary>
        /// Valida que <paramref name="behavior"/> se puede ejecutar ahora.
        /// Misma semantica que el overload de <see cref="ActionDefinitionSO"/>:
        /// ruleset, repetition (usa <see cref="HeroActionBehavior.ActionName"/> como key),
        /// y energy check.
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

            if (behavior.BlockOnRepeat && _actionsUsedThisTurn.Contains(behavior.ActionName))
            {
                reason = $"Behavior '{behavior.ActionName}' already used this turn.";
                return false;
            }

            if (_energy == null)
            {
                reason = "IEnergyService not available.";
                return false;
            }

            // Las preconditions del behavior se evalúan antes del energy check para no
            // cobrar energía cuando la cadena va a abortar igual (ej. Heal sin poción).
            if (!behavior.HasUsableEffectGroup(playerGuid, Guid.Empty, out var pcReason))
            {
                reason = pcReason ?? $"Behavior '{behavior.ActionName}' has no usable effect group.";
                return false;
            }

            int available = _energy.GetCurrent(playerGuid);
            if (available < behavior.EnergyCost)
            {
                reason = $"Not enough energy ({available}/{behavior.EnergyCost}).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ejecuta un <see cref="HeroActionBehavior"/>: valida, cobra energia, ejecuta
        /// via <see cref="HeroActionBehavior.Execute"/>, y trackea repeticion.
        /// </summary>
        public bool TryExecute(HeroActionBehavior behavior, Guid playerGuid, BehaviorContext ctx)
        {
            if (!CanExecute(behavior, playerGuid, out _)) return false;
            if (!_energy.SpendEnergy(playerGuid, behavior.EnergyCost)) return false;

            behavior.Execute(ctx);

            if (behavior.BlockOnRepeat)
                _actionsUsedThisTurn.Add(behavior.ActionName);

            return true;
        }

        public bool TryExecuteEnergyPrepaid(HeroActionBehavior behavior, Guid playerGuid, BehaviorContext ctx)
        {
            if (behavior == null) return false;

            behavior.Execute(ctx);

            if (behavior.BlockOnRepeat)
                _actionsUsedThisTurn.Add(behavior.ActionName);

            return true;
        }

        // ======================================================================
        // Feedback blocking hooks (§10.9)
        // ======================================================================

        private int _feedbackWaitDepth;

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
        }

        // ======================================================================
        // Introspection hooks (tests / tools)
        // ======================================================================

        /// <summary>
        /// <c>true</c> si <paramref name="actionId"/> fue ejecutada con exito en el turno
        /// actual. Expuesto para tests y para tools de debug; el runtime gameplay usa
        /// <see cref="CanExecute"/>.
        /// </summary>
        public bool WasUsedThisTurn(string actionId) => _actionsUsedThisTurn.Contains(actionId);

        /// <summary>Cantidad de acciones unicas marcadas usadas en el turno actual.</summary>
        public int UsedActionsCount => _actionsUsedThisTurn.Count;

        // ======================================================================
        // Event handlers
        // ======================================================================

        /// <summary>
        /// <c>OnTurnStarted</c> schema: <c>[Guid entityGuid]</c>. Limpia el set sin
        /// importar el payload — "turno nuevo = acciones nuevas".
        /// </summary>
        private void OnTurnStartedExternal(params object[] args)
        {
            _actionsUsedThisTurn.Clear();
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
        /// Hook point para <c>RulesetSO.ForbiddenActionIds</c> (Balance#0101). Hoy stub —
        /// siempre devuelve <c>false</c>. Balance#0101 agrega el campo al SO; en ese
        /// momento el return cambia a leer la coleccion. No-breaking para <see cref="CanExecute"/>.
        /// </summary>
        private bool IsForbiddenByRuleset(string actionId)
        {
            // [FOLLOWUP Balance#0101]: read RulesetSO.ForbiddenActionIds (not yet defined).
            // Cuando Balance#0101 agregue el campo:
            //   return _ruleset != null && _ruleset.ForbiddenActionIds != null
            //       && _ruleset.ForbiddenActionIds.Contains(actionId);
            return false;
        }
    }
}
