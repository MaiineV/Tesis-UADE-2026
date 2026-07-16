using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Meta;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using Rollgeon.Run;
using UnityEngine;

namespace Rollgeon.Analytics
{
    /// <summary>
    /// Puente eventos del juego → telemetría de balance (Feature#0029). Mismo
    /// patrón que <c>AchievementService</c>: clase plana Global registrada vía
    /// ExtraServices, suscribe una sola vez en <see cref="Register"/> — cero
    /// cambios en sistemas de gameplay.
    /// <para>
    /// <b>Doble compuerta.</b> <see cref="TrySend"/> exige consentimiento
    /// (<see cref="IAnalyticsConsentService.IsGranted"/>, opt-in GDPR) y delega
    /// en <see cref="IAnalyticsSink.Ready"/> el estado del SDK. La agregación
    /// corre SIEMPRE (es barata): si el jugador acepta a mitad de combate, el
    /// <c>combat_ended</c> sale con los acumuladores completos.
    /// </para>
    /// <para>
    /// Alta frecuencia se agrega (daño, turnos, rerolls, energía → per-combat);
    /// solo eventos discretos van crudos. Schema en <see cref="AnalyticsEvents"/>.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class AnalyticsTrackerService : IPreloadableService, IDisposable
    {
        /// <summary>Después de UnlockProgress (90) y Achievements (95): mismos eventos, sin dependencia.</summary>
        public const int DefaultPriority = 96;

        /// <summary>Cap del param STRING <c>top_combos</c> (límite de tamaño de UGS).</summary>
        public const int TopCombosMaxLength = 100;

        [NonSerialized] private bool _subscribed;
        [NonSerialized] private bool _warnedNoSink;

        // Lazy: cuando la instancia viene deserializada por Odin desde el
        // ServiceBootstrap asset, los field initializers NO corren (no hay ctor)
        // y los [NonSerialized] quedan null/default. Nunca acceder al campo directo.
        [NonSerialized] private RunAggregator _run;
        [NonSerialized] private CombatAggregator _combat;
        [NonSerialized] private Func<double> _timeProvider;

        // Contexto barato mantenido siempre (aunque no haya consentimiento).
        [NonSerialized] private Guid _currentRunId;
        [NonSerialized] private string _currentHeroId;
        [NonSerialized] private bool _runActive;
        [NonSerialized] private bool _isTutorialRun;
        [NonSerialized] private int _currentFloorIndex;
        [NonSerialized] private int _lastPlayerHp;
        [NonSerialized] private int _lastGold;
        [NonSerialized] private RoomType _currentRoomType;

        private RunAggregator Run => _run ??= new RunAggregator();
        private CombatAggregator Combat => _combat ??= new CombatAggregator();

        /// <summary>Reloj inyectable (regla de tests: sin dependencia del clock real).</summary>
        public Func<double> TimeProvider
        {
            get => _timeProvider ??= DefaultTimeProvider;
            set => _timeProvider = value;
        }

        private double Now => TimeProvider();

        private static double DefaultTimeProvider() => Time.realtimeSinceStartupAsDouble;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            ServiceLocator.AddService<AnalyticsTrackerService>(this, ServiceScope.Global);

            // El consent service es una clase plana sin bootstrap propio — lo
            // registra el tracker para que la UI del menú siempre lo encuentre.
            if (!ServiceLocator.TryGetService<IAnalyticsConsentService>(out _))
            {
                ServiceLocator.AddService<IAnalyticsConsentService>(
                    new AnalyticsConsentService(), ServiceScope.Global);
            }

            SubscribeEvents();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            UnsubscribeEvents();
        }

        // ====================================================================
        // Event wiring
        // ====================================================================

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.Subscribe(EventName.OnRunVictory, OnRunVictoryHandler);
            EventManager.Subscribe(EventName.OnPlayerDefeated, OnPlayerDefeatedHandler);
            EventManager.Subscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.Subscribe(EventName.OnFloorChanged, OnFloorChangedHandler);
            EventManager.Subscribe(EventName.OnFloorCleared, OnFloorClearedHandler);
            EventManager.Subscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.Subscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.Subscribe(EventName.OnTurnStarted, OnTurnStartedHandler);
            EventManager.Subscribe(EventName.OnRerollStarted, OnRerollStartedHandler);
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, OnPlayerEnergyChangedHandler);
            EventManager.Subscribe(EventName.OnPlayerHealthChanged, OnPlayerHealthChangedHandler);
            EventManager.Subscribe(EventName.OnGoldChanged, OnGoldChangedHandler);
            EventManager.Subscribe(EventName.OnBossPhaseChanged, OnBossPhaseChangedHandler);
            EventManager.Subscribe(EventName.OnShopItemPurchased, OnShopItemPurchasedHandler);
            EventManager.Subscribe(EventName.OnItemObtained, OnItemObtainedHandler);
            EventManager.Subscribe(EventName.OnActiveItemUsed, OnActiveItemUsedHandler);
            TypedEvent<DamageResolvedPayload>.Subscribe(OnDamageResolvedHandler);
            TypedEvent<ComboMatchedPayload>.Subscribe(OnComboMatchedHandler);
            TypedEvent<UnlockAchievedPayload>.Subscribe(OnUnlockAchievedHandler);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.UnSubscribe(EventName.OnRunVictory, OnRunVictoryHandler);
            EventManager.UnSubscribe(EventName.OnPlayerDefeated, OnPlayerDefeatedHandler);
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.UnSubscribe(EventName.OnFloorChanged, OnFloorChangedHandler);
            EventManager.UnSubscribe(EventName.OnFloorCleared, OnFloorClearedHandler);
            EventManager.UnSubscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.UnSubscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnTurnStarted, OnTurnStartedHandler);
            EventManager.UnSubscribe(EventName.OnRerollStarted, OnRerollStartedHandler);
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, OnPlayerEnergyChangedHandler);
            EventManager.UnSubscribe(EventName.OnPlayerHealthChanged, OnPlayerHealthChangedHandler);
            EventManager.UnSubscribe(EventName.OnGoldChanged, OnGoldChangedHandler);
            EventManager.UnSubscribe(EventName.OnBossPhaseChanged, OnBossPhaseChangedHandler);
            EventManager.UnSubscribe(EventName.OnShopItemPurchased, OnShopItemPurchasedHandler);
            EventManager.UnSubscribe(EventName.OnItemObtained, OnItemObtainedHandler);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, OnActiveItemUsedHandler);
            TypedEvent<DamageResolvedPayload>.Unsubscribe(OnDamageResolvedHandler);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(OnComboMatchedHandler);
            TypedEvent<UnlockAchievedPayload>.Unsubscribe(OnUnlockAchievedHandler);
            _subscribed = false;
        }

        // ====================================================================
        // Handlers — run lifecycle
        // ====================================================================

        // Schema EventName.OnRunStart: args = [Guid runId, string rulesetId]
        private void OnRunStartHandler(params object[] args)
        {
            // El tutorial dispara el mismo ciclo de run — no es dato de balance.
            // PendingRunRequest sigue seteado acá (se limpia después de StartRun).
            _isTutorialRun = PendingRunRequest.IsTutorial;
            if (_isTutorialRun)
            {
                _runActive = false;
                return;
            }

            _runActive = true;
            TryGetGuid(args, 0, out _currentRunId);

            var resumed = PendingRunRequest.IsResume || RunBootstrapper.IsResuming;
            Run.Reset(Now, resumed);
            Combat.Reset(Now);

            var runContext = GetRunContext();
            _currentHeroId = runContext?.SelectedHero != null && !string.IsNullOrEmpty(runContext.SelectedHero.EntityId)
                ? runContext.SelectedHero.EntityId
                : "unknown";
            _currentFloorIndex = runContext?.FloorIndex ?? 0;
            _currentRoomType = RoomType.Start;

            var rulesetId = args != null && args.Length > 1 && args[1] is string ruleset && !string.IsNullOrEmpty(ruleset)
                ? ruleset
                : "default";

            TrySend(AnalyticsEvents.RunStarted, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.HeroId] = _currentHeroId,
                [AnalyticsEvents.Params.RulesetId] = rulesetId,
                [AnalyticsEvents.Params.IsContinue] = resumed,
                // Misma derivación que RunController: seed = hash del runId.
                [AnalyticsEvents.Params.Seed] = _currentRunId.GetHashCode(),
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
            });
        }

        // Schema EventName.OnRunVictory: args = [Guid runId]
        private void OnRunVictoryHandler(params object[] args)
        {
            if (!_runActive) return;
            Run.VictoryMarked = true;
            // Eager: si el jugador cierra el juego en la VictoryScreen, EndRun
            // nunca corre y OnRunEnd no llega.
            SendRunEnded(AnalyticsEvents.Outcomes.Victory);
        }

        // Schema EventName.OnPlayerDefeated: args = [Guid runId]
        private void OnPlayerDefeatedHandler(params object[] args)
        {
            if (!_runActive) return;
            Run.DefeatMarked = true;

            // player_death primero: lleva el contexto del combate vigente.
            TrySend(AnalyticsEvents.PlayerDeath, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
                [AnalyticsEvents.Params.RoomType] = _currentRoomType.ToString(),
                [AnalyticsEvents.Params.TurnCount] = Combat.TurnCount,
                [AnalyticsEvents.Params.BossPhase] = Combat.MaxBossPhase,
            });

            SendRunEnded(AnalyticsEvents.Outcomes.Defeat);
        }

        // Schema EventName.OnRunEnd: args = [Guid runId, null] — el outcome real
        // NO viaja acá (RunBootstrapper pasa null); se deriva de los markers.
        private void OnRunEndHandler(params object[] args)
        {
            if (_runActive && !Run.RunEndedSent)
            {
                // Sin victory/defeat previo = el jugador abandonó (quit desde pausa).
                SendRunEnded(AnalyticsEvents.Outcomes.Abandon);
            }

            _runActive = false;
            _isTutorialRun = false;
        }

        private void SendRunEnded(string outcome)
        {
            if (Run.RunEndedSent) return;
            Run.RunEndedSent = true;

            TrySend(AnalyticsEvents.RunEnded, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.Outcome] = outcome,
                [AnalyticsEvents.Params.HeroId] = _currentHeroId ?? "unknown",
                [AnalyticsEvents.Params.FloorsCleared] = Run.FloorsCleared,
                [AnalyticsEvents.Params.DurationSec] = (float)(Now - Run.RunStartTime),
                [AnalyticsEvents.Params.CombatsWon] = Run.CombatsWon,
                [AnalyticsEvents.Params.GoldEarned] = Run.GoldEarned,
                [AnalyticsEvents.Params.GoldSpent] = Run.GoldSpent,
                [AnalyticsEvents.Params.CombosMatched] = Run.CombosMatched,
                [AnalyticsEvents.Params.WasResumed] = Run.WasResumed,
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
            });

            // El quit desde pausa descarga la escena enseguida — no esperar el
            // flush periódico del SDK.
            GetSink()?.Flush();
        }

        // Schema EventName.OnFloorChanged: args = [Guid runId, int newFloorIndex]
        private void OnFloorChangedHandler(params object[] args)
        {
            if (!TryGetInt(args, 1, out var newFloorIndex)) return;
            _currentFloorIndex = newFloorIndex;
            if (!_runActive) return;

            TrySend(AnalyticsEvents.FloorReached, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.FloorIndex] = newFloorIndex,
                [AnalyticsEvents.Params.HpAtEntry] = _lastPlayerHp,
                [AnalyticsEvents.Params.GoldAtEntry] = _lastGold,
            });
        }

        // Schema EventName.OnFloorCleared: args = [Guid runId, int floorIndex]
        private void OnFloorClearedHandler(params object[] args)
        {
            if (_runActive) Run.FloorsCleared++;
        }

        // ====================================================================
        // Handlers — combate
        // ====================================================================

        // Schema EventName.OnCombatTriggered: args = [Guid roomInstanceId, string roomId, RoomType roomType]
        private void OnCombatTriggeredHandler(params object[] args)
        {
            // Llega ANTES de OnCombatStart (precedente AchievementService).
            if (args != null && args.Length > 2 && args[2] is RoomType roomType)
            {
                _currentRoomType = roomType;
            }
        }

        // Schema EventName.OnCombatStart: args = [Guid roomInstanceId]
        private void OnCombatStartHandler(params object[] args)
        {
            if (!_runActive) return;
            Combat.Reset(Now);
        }

        // Schema EventName.OnCombatEnd: args = [Guid roomInstanceId, CombatOutcome outcome]
        private void OnCombatEndHandler(params object[] args)
        {
            if (!_runActive) return;

            var outcome = args != null && args.Length > 1 && args[1] is CombatOutcome parsed
                ? parsed
                : CombatOutcome.None;

            if (outcome == CombatOutcome.Victory) Run.CombatsWon++;

            TrySend(AnalyticsEvents.CombatEnded, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
                [AnalyticsEvents.Params.RoomType] = _currentRoomType.ToString(),
                [AnalyticsEvents.Params.Outcome] = outcome.ToString(),
                [AnalyticsEvents.Params.TurnCount] = Combat.TurnCount,
                [AnalyticsEvents.Params.DurationSec] = (float)(Now - Combat.CombatStartTime),
                [AnalyticsEvents.Params.DamageDealt] = Combat.DamageDealt,
                [AnalyticsEvents.Params.DamageTaken] = Combat.DamageTaken,
                [AnalyticsEvents.Params.RerollsUsed] = Combat.RerollsUsed,
                [AnalyticsEvents.Params.EnergySpent] = Combat.EnergySpent,
                [AnalyticsEvents.Params.HpRemaining] = _lastPlayerHp,
                [AnalyticsEvents.Params.TopCombos] = Combat.BuildTopCombos(TopCombosMaxLength),
                [AnalyticsEvents.Params.BossPhaseReached] = Combat.MaxBossPhase,
            });
            // NO resetear Combat acá: player_death (post-combate) lo sigue leyendo.
        }

        // Schema EventName.OnTurnStarted: args = [Guid entityGuid]
        private void OnTurnStartedHandler(params object[] args)
        {
            if (_runActive && TryGetGuid(args, 0, out var guid) && IsPlayer(guid))
            {
                Combat.TurnCount++;
            }
        }

        // Schema EventName.OnRerollStarted: args = [Guid sourceGuid, int rerollIndex]
        private void OnRerollStartedHandler(params object[] args)
        {
            if (_runActive && TryGetGuid(args, 0, out var guid) && IsPlayer(guid))
            {
                Combat.RerollsUsed++;
            }
        }

        // Schema EventName.OnPlayerEnergyChanged: args = [Guid entityGuid, int current, int max]
        private void OnPlayerEnergyChangedHandler(params object[] args)
        {
            if (_runActive && TryGetInt(args, 1, out var current))
            {
                Combat.TrackEnergy(current);
            }
        }

        // Schema EventName.OnPlayerHealthChanged: args = [Guid entityGuid, int current, int max]
        private void OnPlayerHealthChangedHandler(params object[] args)
        {
            if (TryGetInt(args, 1, out var current))
            {
                _lastPlayerHp = current;
            }
        }

        // Schema EventName.OnBossPhaseChanged: args = [Guid bossGuid, int phaseIndex]
        private void OnBossPhaseChangedHandler(params object[] args)
        {
            if (_runActive && TryGetInt(args, 1, out var phase) && phase > Combat.MaxBossPhase)
            {
                Combat.MaxBossPhase = phase;
            }
        }

        // TypedEvent<DamageResolvedPayload> — daño final post-mitigación.
        private void OnDamageResolvedHandler(DamageResolvedPayload payload)
        {
            if (!_runActive) return;

            // ShieldAbsorbed cuenta como daño producido/recibido: para balance
            // interesa la presión total, no solo lo que llegó a Health.
            var total = payload.FinalDamage + payload.ShieldAbsorbed;
            if (IsPlayer(payload.SourceGuid))
            {
                Combat.DamageDealt += total;
            }
            else if (IsPlayer(payload.TargetGuid))
            {
                Combat.DamageTaken += total;
            }
        }

        // TypedEvent<ComboMatchedPayload> — crudo (~1 por turno, volumen aceptable).
        private void OnComboMatchedHandler(ComboMatchedPayload payload)
        {
            if (!_runActive || !IsPlayer(payload.SourceGuid)) return;
            if (string.IsNullOrEmpty(payload.ComboId)) return;

            Combat.ComboCounts.TryGetValue(payload.ComboId, out var count);
            Combat.ComboCounts[payload.ComboId] = count + 1;
            Run.CombosMatched++;

            TrySend(AnalyticsEvents.ComboMatched, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.ComboId] = payload.ComboId,
                [AnalyticsEvents.Params.BaseDamage] = payload.BaseDamage,
                // MultiDmgCombo == 0 significa "no calculado" → tratar como 1.0.
                [AnalyticsEvents.Params.Multiplier] = payload.MultiDmgCombo <= 0f ? 1f : payload.MultiDmgCombo,
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
            });
        }

        // ====================================================================
        // Handlers — economía y meta
        // ====================================================================

        // Schema EventName.OnGoldChanged: args = [int current, int delta]
        private void OnGoldChangedHandler(params object[] args)
        {
            if (!TryGetInt(args, 0, out var current)) return;
            _lastGold = current;

            if (!_runActive || !TryGetInt(args, 1, out var delta)) return;
            if (delta > 0) Run.GoldEarned += delta;
            else Run.GoldSpent += -delta;
        }

        // Schema EventName.OnShopItemPurchased: args = [string spawnPointId, string rewardId, int pricePaid]
        private void OnShopItemPurchasedHandler(params object[] args)
        {
            if (!_runActive) return;
            var rewardId = args != null && args.Length > 1 ? args[1] as string : null;
            TryGetInt(args, 2, out var price);

            TrySend(AnalyticsEvents.ShopPurchase, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.ItemId] = rewardId ?? "unknown",
                [AnalyticsEvents.Params.Price] = price,
                [AnalyticsEvents.Params.GoldRemaining] = _lastGold,
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
            });
        }

        // Schema EventName.OnItemObtained: args = [Guid ownerGuid, string itemId]
        private void OnItemObtainedHandler(params object[] args)
        {
            if (!_runActive || !TryGetGuid(args, 0, out var owner) || !IsPlayer(owner)) return;
            var itemId = args.Length > 1 ? args[1] as string : null;

            TrySend(AnalyticsEvents.ItemObtained, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.ItemId] = itemId ?? "unknown",
                // Proxy razonable del origen: el tipo de la sala en curso.
                [AnalyticsEvents.Params.Source] = _currentRoomType.ToString(),
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
            });
        }

        // Schema EventName.OnActiveItemUsed: args = [Guid sourceGuid, string itemId]
        private void OnActiveItemUsedHandler(params object[] args)
        {
            if (!_runActive) return;
            var itemId = args != null && args.Length > 1 ? args[1] as string : null;

            TrySend(AnalyticsEvents.ActiveItemUsed, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.ItemId] = itemId ?? "unknown",
                [AnalyticsEvents.Params.FloorIndex] = _currentFloorIndex,
            });
        }

        // TypedEvent<UnlockAchievedPayload> — también fuera de una run activa
        // (los unlocks al cierre de run llegan después de OnRunEnd).
        private void OnUnlockAchievedHandler(UnlockAchievedPayload payload)
        {
            TrySend(AnalyticsEvents.UnlockAchieved, new Dictionary<string, object>
            {
                [AnalyticsEvents.Params.UnlockId] = payload.UnlockId ?? "unknown",
                [AnalyticsEvents.Params.Category] = payload.Category.ToString(),
                [AnalyticsEvents.Params.DuringRun] = payload.DuringRun,
            });
        }

        // ====================================================================
        // Envío
        // ====================================================================

        private void TrySend(string eventName, Dictionary<string, object> parameters)
        {
            // Compuerta 1 — consentimiento (opt-in: sin servicio o sin grant, nada).
            var consent = GetConsent();
            if (consent == null || !consent.IsGranted) return;

            // Compuerta 2 — el sink (Ready la evalúa internamente y dropea).
            var sink = GetSink();
            if (sink == null)
            {
                WarnNoSinkOnce();
                return;
            }

            parameters[AnalyticsEvents.Params.RunId] =
                _currentRunId == Guid.Empty ? string.Empty : _currentRunId.ToString("N");
            parameters[AnalyticsEvents.Params.IsEditor] = Application.isEditor;
            parameters[AnalyticsEvents.Params.AppVersion] = Application.version;

            sink.Send(eventName, parameters);
        }

        private void WarnNoSinkOnce()
        {
            if (_warnedNoSink) return;
            _warnedNoSink = true;
            Debug.Log("[Analytics] No hay IAnalyticsSink registrado — la telemetría de esta sesión no se reporta.");
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private bool IsPlayer(Guid guid)
        {
            if (guid == Guid.Empty) return false;
            var player = GetPlayer();
            return player != null && player.PlayerGuid == guid;
        }

        private static bool TryGetGuid(object[] args, int index, out Guid value)
        {
            if (args != null && args.Length > index && args[index] is Guid guid)
            {
                value = guid;
                return true;
            }
            value = Guid.Empty;
            return false;
        }

        private static bool TryGetInt(object[] args, int index, out int value)
        {
            if (args != null && args.Length > index && args[index] is int parsed)
            {
                value = parsed;
                return true;
            }
            value = 0;
            return false;
        }

        private static IAnalyticsSink GetSink() =>
            ServiceLocator.TryGetService<IAnalyticsSink>(out var sink) ? sink : null;

        private static IAnalyticsConsentService GetConsent() =>
            ServiceLocator.TryGetService<IAnalyticsConsentService>(out var consent) ? consent : null;

        private static IRunContextService GetRunContext() =>
            ServiceLocator.TryGetService<IRunContextService>(out var context) ? context : null;

        private static IPlayerService GetPlayer() =>
            ServiceLocator.TryGetService<IPlayerService>(out var player) ? player : null;
    }
}
