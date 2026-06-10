using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Combos.Counters;
using Rollgeon.Dungeon;
using Rollgeon.Meta.Conditions;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using Rollgeon.Run;

namespace Rollgeon.Meta
{
    /// <summary>
    /// Implementación runtime de <see cref="IUnlockProgressService"/> (#164).
    /// <para>
    /// Clase plana registrada en <c>ServiceScope.Global</c> (mismo patrón que
    /// <c>ComboCountersService</c>): suscribe una sola vez en <see cref="Register"/>
    /// y crea un <see cref="RunUnlockState"/> fresco por run en <c>OnRunStart</c>.
    /// </para>
    /// <para>
    /// <b>Evaluación.</b> Tras cada evento relevante (combo, fin de combate, uso de
    /// item) evalúa las definiciones pendientes: las de <c>AppliesTo == Any</c>
    /// pueden desbloquear mid-run (unlock + save inmediato + toast); las que exigen
    /// outcome esperan a <c>OnRunVictory</c>/<c>OnPlayerDefeated</c>, donde primero
    /// se actualizan los contadores persistentes y después se corre la pasada final.
    /// Las condiciones de consistencia se invalidan apenas se rompen y quedan fuera
    /// de la pasada de cierre.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class UnlockProgressService : IUnlockProgressService, IPreloadableService, IDisposable
    {
        /// <summary>Después de ComboCounters (80) — su handler de OnRunStart corre primero.</summary>
        public const int DefaultPriority = 90;

        [NonSerialized] private bool _subscribed;

        // Lazy: cuando la instancia viene deserializada por Odin desde el
        // ServiceBootstrap asset, los field initializers NO corren (no hay ctor)
        // y los [NonSerialized] quedan null. Nunca acceder al campo directo.
        [NonSerialized] private List<UnlockDefinitionSO> _unlocksThisRun;

        private List<UnlockDefinitionSO> UnlocksList => _unlocksThisRun ??= new List<UnlockDefinitionSO>();

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        /// <inheritdoc />
        public IReadOnlyList<UnlockDefinitionSO> UnlocksThisRun => UnlocksList;

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            ServiceLocator.AddService<IUnlockProgressService>(this, ServiceScope.Global);
            SubscribeEvents();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            UnsubscribeEvents();
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Hook para tests — registra los listeners sin pasar por <c>Register()</c>.</summary>
        public void SubscribeEventsForTests() => SubscribeEvents();

        /// <summary>Hook para tests — desregistra los listeners.</summary>
        public void UnsubscribeEventsForTests() => UnsubscribeEvents();

        // ====================================================================
        // Event wiring
        // ====================================================================

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.Subscribe(EventName.OnRunVictory, OnRunVictoryHandler);
            EventManager.Subscribe(EventName.OnPlayerDefeated, OnPlayerDefeatedHandler);
            EventManager.Subscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.Subscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.Subscribe(EventName.OnActiveItemUsed, OnActiveItemUsedHandler);
            EventManager.Subscribe(EventName.OnFloorChanged, OnFloorChangedHandler);
            EventManager.Subscribe(EventName.OnComboCounterIncremented, OnComboCounterIncrementedHandler);
            TypedEvent<DamageResolvedPayload>.Subscribe(OnDamageResolved);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.UnSubscribe(EventName.OnRunVictory, OnRunVictoryHandler);
            EventManager.UnSubscribe(EventName.OnPlayerDefeated, OnPlayerDefeatedHandler);
            EventManager.UnSubscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.UnSubscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, OnActiveItemUsedHandler);
            EventManager.UnSubscribe(EventName.OnFloorChanged, OnFloorChangedHandler);
            EventManager.UnSubscribe(EventName.OnComboCounterIncremented, OnComboCounterIncrementedHandler);
            TypedEvent<DamageResolvedPayload>.Unsubscribe(OnDamageResolved);
            _subscribed = false;
        }

        // ====================================================================
        // Run lifecycle
        // ====================================================================

        // Schema EventName.OnRunStart: args = [Guid runId, string rulesetId].
        // RunBootstrapper dispara este evento DESPUÉS de registrar RunContext y
        // setear player + dice bag, así que acá ya se puede capturar la build.
        private void OnRunStartHandler(params object[] args)
        {
            UnlocksList.Clear();

            var state = new RunUnlockState();

            if (ServiceLocator.TryGetService<IRunContextService>(out var runCtx) && runCtx?.SelectedHero != null)
            {
                state.ClassId = runCtx.SelectedHero.EntityId;
                var combos = runCtx.SelectedHero.Sheet?.Combos;
                if (combos != null)
                {
                    foreach (var combo in combos)
                    {
                        if (combo != null && !string.IsNullOrEmpty(combo.ComboId))
                        {
                            state.ContractComboIds.Add(combo.ComboId);
                        }
                    }
                }
            }

            if (ServiceLocator.TryGetService<IPlayerService>(out var player) && player?.DiceBag?.Dice != null)
            {
                state.DiceBuild.AddRange(player.DiceBag.Dice);
            }

            ServiceLocator.AddService<RunUnlockState>(state, ServiceScope.Run);
        }

        // Schema EventName.OnRunVictory: args = [Guid runId]
        private void OnRunVictoryHandler(params object[] args) => FinalizeRun(won: true);

        // Schema EventName.OnPlayerDefeated: args = [Guid runId]
        private void OnPlayerDefeatedHandler(params object[] args) => FinalizeRun(won: false);

        // ====================================================================
        // Combat tracking
        // ====================================================================

        // Schema EventName.OnCombatTriggered: args = [Guid roomInstanceId, string roomId, RoomType roomType]
        private void OnCombatTriggeredHandler(params object[] args)
        {
            var state = GetState();
            if (state == null) return;
            state.CurrentCombatIsBoss = args != null && args.Length > 2 &&
                                        args[2] is RoomType roomType && roomType == RoomType.Boss;
        }

        // Schema EventName.OnCombatStart: args = [Guid roomInstanceId]
        private void OnCombatStartHandler(params object[] args)
        {
            var state = GetState();
            if (state == null) return;
            state.InCombat = true;
            state.TookDamageThisCombat = false;
        }

        // Schema EventName.OnCombatEnd: args = [Guid roomInstanceId, CombatOutcome outcome]
        private void OnCombatEndHandler(params object[] args)
        {
            var state = GetState();
            if (state == null) return;

            var outcome = args != null && args.Length > 1 && args[1] is CombatOutcome oc
                ? oc
                : CombatOutcome.None;

            switch (outcome)
            {
                case CombatOutcome.Victory:
                    if (!state.TookDamageThisCombat) state.FlawlessCombats++;
                    if (state.CurrentCombatIsBoss) state.BossesDefeated++;
                    break;
                case CombatOutcome.Aborted:
                    // Huida — rompe las condiciones de consistencia "sin huir".
                    state.CombatsFled++;
                    break;
            }

            state.InCombat = false;
            state.TookDamageThisCombat = false;
            state.CurrentCombatIsBoss = false;

            EvaluateMidRun();
        }

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            var state = GetState();
            if (state == null || !state.InCombat || payload.FinalDamage <= 0) return;

            if (ServiceLocator.TryGetService<IPlayerService>(out var player) &&
                payload.TargetGuid == player.PlayerGuid)
            {
                state.TookDamageThisCombat = true;
            }
        }

        // ====================================================================
        // Misc tracking
        // ====================================================================

        // Schema EventName.OnActiveItemUsed: args = [Guid sourceGuid, string itemId]
        private void OnActiveItemUsedHandler(params object[] args)
        {
            var state = GetState();
            if (state == null) return;

            if (args != null && args.Length > 1 && args[1] is string itemId && !string.IsNullOrEmpty(itemId))
            {
                state.UsedActiveItemIds.Add(itemId);
                EvaluateMidRun();
            }
        }

        // Schema EventName.OnFloorChanged: args = [Guid runId, int newFloorIndex]
        private void OnFloorChangedHandler(params object[] args)
        {
            var state = GetState();
            if (state == null) return;

            if (args != null && args.Length > 1 && args[1] is int newFloorIndex)
            {
                state.FloorsVisited = Math.Max(state.FloorsVisited, newFloorIndex + 1);
            }
        }

        // Schema EventName.OnComboCounterIncremented: args = [string comboId, int newCount]
        private void OnComboCounterIncrementedHandler(params object[] args)
        {
            EvaluateMidRun();
        }

        // ====================================================================
        // Evaluation
        // ====================================================================

        /// <summary>
        /// Pasada mid-run: marca invalidaciones de consistencia y desbloquea las
        /// definiciones <c>AppliesTo == Any</c> que ya se cumplan (#164 — el unlock
        /// se aplica y guarda inmediatamente, no requiere terminar la run).
        /// </summary>
        public void EvaluateMidRun()
        {
            var state = GetState();
            if (state == null || state.Finalized) return;
            if (!ServiceLocator.TryGetService<IMetaProgressionService>(out var meta) || meta == null) return;

            var ctx = BuildContext(state, meta, runEnded: false, runWon: false);

            var defs = meta.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def.Condition == null || meta.IsDefinitionCompleted(def)) continue;

                if (def.Condition.IsInvalidated(ctx))
                {
                    state.InvalidatedUnlockIds.Add(def.UnlockId);
                    continue;
                }

                if (def.AppliesTo == UnlockOutcomeFilter.Any && def.Condition.Evaluate(ctx))
                {
                    if (meta.TryUnlock(def, duringRun: true))
                    {
                        UnlocksList.Add(def);
                    }
                }
            }
        }

        /// <summary>
        /// Pasada de cierre: actualiza primero los contadores persistentes (racha /
        /// clases jugadas) y después evalúa todas las definiciones pendientes cuyo
        /// outcome matchee, salteando las invalidadas durante la run.
        /// </summary>
        private void FinalizeRun(bool won)
        {
            var state = GetState();
            if (state == null || state.Finalized) return;
            if (!ServiceLocator.TryGetService<IMetaProgressionService>(out var meta) || meta == null) return;

            state.Finalized = true;
            meta.RecordRunCompleted(won, state.ClassId);

            var ctx = BuildContext(state, meta, runEnded: true, runWon: won);

            var defs = meta.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def.Condition == null || meta.IsDefinitionCompleted(def)) continue;
                if (state.InvalidatedUnlockIds.Contains(def.UnlockId)) continue;
                if (!def.AppliesToOutcome(won)) continue;
                if (def.Condition.IsInvalidated(ctx)) continue;

                if (def.Condition.Evaluate(ctx) && meta.TryUnlock(def, duringRun: false))
                {
                    UnlocksList.Add(def);
                }
            }
        }

        private static RunUnlockState GetState()
        {
            return ServiceLocator.TryGetService<RunUnlockState>(out var state) ? state : null;
        }

        private static UnlockEvaluationContext BuildContext(
            RunUnlockState state, IMetaProgressionService meta, bool runEnded, bool runWon)
        {
            IReadOnlyDictionary<string, int> comboCounts = null;
            if (ServiceLocator.TryGetService<RunComboCounterState>(out var counters) && counters != null)
            {
                comboCounts = counters.Snapshot;
            }

            return new UnlockEvaluationContext
            {
                RunEnded = runEnded,
                RunWon = runWon,
                ClassId = state.ClassId,
                DiceBuild = state.DiceBuild,
                ComboCounts = comboCounts,
                ContractComboIds = state.ContractComboIds,
                UsedActiveItemIds = state.UsedActiveItemIds,
                FlawlessCombats = state.FlawlessCombats,
                CombatsFled = state.CombatsFled,
                BossesDefeated = state.BossesDefeated,
                FloorsVisited = state.FloorsVisited,
                ConsecutiveWins = meta.ConsecutiveWins,
                ClassesPlayed = meta.ClassesPlayed,
            };
        }
    }
}
