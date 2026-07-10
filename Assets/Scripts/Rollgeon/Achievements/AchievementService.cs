using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Achievements
{
    /// <summary>
    /// Puente eventos del juego → logros de Steam (Feature#0019). Mismo patrón que
    /// <c>UnlockProgressService</c>: clase plana Global registrada vía ExtraServices,
    /// suscribe una sola vez en <see cref="Register"/>.
    /// <para>
    /// Data-driven: el mapeo evento→API name vive en <see cref="SteamConfigSO"/>
    /// (SettingsAssets). Resuelve <see cref="ISteamService"/> lazy en cada evento —
    /// si Steam no está disponible o no hay config, no-op silencioso: el juego jamás
    /// depende de Steam para funcionar.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class AchievementService : IPreloadableService, IDisposable
    {
        /// <summary>Después de UnlockProgress (90): mismos eventos, sin dependencia entre sí.</summary>
        public const int DefaultPriority = 95;

        [NonSerialized] private bool _subscribed;
        [NonSerialized] private bool _warnedUnavailable;
        [NonSerialized] private bool _currentCombatIsBoss;

        // Lazy: cuando la instancia viene deserializada por Odin desde el
        // ServiceBootstrap asset, los field initializers NO corren (no hay ctor)
        // y los [NonSerialized] quedan null. Nunca acceder al campo directo.
        [NonSerialized] private HashSet<string> _requestedKeys;

        /// <summary>Keys ya pedidas a Steam esta sesión — evita spamear StoreStats.</summary>
        private HashSet<string> RequestedKeys => _requestedKeys ??= new HashSet<string>(StringComparer.Ordinal);

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            ServiceLocator.AddService<AchievementService>(this, ServiceScope.Global);
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
            EventManager.Subscribe(EventName.OnRunVictory, OnRunVictoryHandler);
            EventManager.Subscribe(EventName.OnPlayerDefeated, OnPlayerDefeatedHandler);
            EventManager.Subscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.Subscribe(EventName.OnFloorCleared, OnFloorClearedHandler);
            EventManager.Subscribe(EventName.OnComboCounterIncremented, OnComboCounterIncrementedHandler);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunVictory, OnRunVictoryHandler);
            EventManager.UnSubscribe(EventName.OnPlayerDefeated, OnPlayerDefeatedHandler);
            EventManager.UnSubscribe(EventName.OnCombatTriggered, OnCombatTriggeredHandler);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnFloorCleared, OnFloorClearedHandler);
            EventManager.UnSubscribe(EventName.OnComboCounterIncremented, OnComboCounterIncrementedHandler);
            _subscribed = false;
        }

        // ====================================================================
        // Handlers
        // ====================================================================

        // Schema EventName.OnRunVictory: args = [Guid runId]
        private void OnRunVictoryHandler(params object[] args) =>
            UnlockMatching(AchievementTrigger.RunVictory);

        // Schema EventName.OnPlayerDefeated: args = [Guid runId]
        private void OnPlayerDefeatedHandler(params object[] args) =>
            UnlockMatching(AchievementTrigger.RunDefeat);

        // Schema EventName.OnCombatTriggered: args = [Guid roomInstanceId, string roomId, RoomType roomType]
        private void OnCombatTriggeredHandler(params object[] args)
        {
            _currentCombatIsBoss = args != null && args.Length > 2 &&
                                   args[2] is RoomType roomType && roomType == RoomType.Boss;
        }

        // Schema EventName.OnCombatEnd: args = [Guid roomInstanceId, CombatOutcome outcome]
        private void OnCombatEndHandler(params object[] args)
        {
            var victory = args != null && args.Length > 1 &&
                          args[1] is CombatOutcome outcome && outcome == CombatOutcome.Victory;

            if (victory && _currentCombatIsBoss)
            {
                UnlockMatching(AchievementTrigger.BossDefeated);
            }

            _currentCombatIsBoss = false;
        }

        // Schema EventName.OnFloorCleared: args = [Guid runId, int floorIndex]
        private void OnFloorClearedHandler(params object[] args)
        {
            if (args != null && args.Length > 1 && args[1] is int floorIndex)
            {
                UnlockMatching(AchievementTrigger.FloorCleared, intValue: floorIndex);
            }
        }

        // Schema EventName.OnComboCounterIncremented: args = [string comboId, int newCount]
        private void OnComboCounterIncrementedHandler(params object[] args)
        {
            if (args != null && args.Length > 1 &&
                args[0] is string comboId && args[1] is int newCount)
            {
                UnlockMatching(AchievementTrigger.ComboCounterReached, comboId, newCount);
            }
        }

        // ====================================================================
        // API pública (consola / triggers Manual)
        // ====================================================================

        /// <summary>
        /// Desbloquea por key interna del config, sin importar su trigger.
        /// <c>false</c> si la key no existe, Steam no está, o ya se pidió.
        /// </summary>
        public bool TryUnlockByKey(string key)
        {
            var config = GetConfig();
            return config != null && config.TryGetByKey(key, out var def) && TryUnlock(def);
        }

        /// <summary>Revierte el logro en Steam y lo saca del de-dupe de sesión (dev/QA).</summary>
        public bool TryClearByKey(string key)
        {
            var config = GetConfig();
            if (config == null || !config.TryGetByKey(key, out var def)) return false;

            var steam = GetSteam();
            if (steam == null || !steam.Available) return false;
            if (!steam.ClearAchievement(def.SteamApiName)) return false;

            RequestedKeys.Remove(def.Key);
            return true;
        }

        /// <summary>
        /// Definiciones del config con su estado en Steam.
        /// <c>Unlocked == null</c> cuando Steam no está disponible.
        /// </summary>
        public IEnumerable<(AchievementDefinition Definition, bool? Unlocked)> ListWithStatus()
        {
            var config = GetConfig();
            if (config == null) yield break;

            var steam = GetSteam();
            var available = steam != null && steam.Available;

            foreach (var def in config.Definitions)
            {
                if (def == null) continue;

                bool? unlocked = null;
                if (available && steam.TryGetAchievementState(def.SteamApiName, out var state))
                {
                    unlocked = state;
                }

                yield return (def, unlocked);
            }
        }

        // ====================================================================
        // Unlock core
        // ====================================================================

        private void UnlockMatching(AchievementTrigger trigger, string stringValue = null, int intValue = 0)
        {
            var config = GetConfig();
            if (config == null) return;

            var defs = config.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null || def.Trigger != trigger) continue;

                switch (trigger)
                {
                    case AchievementTrigger.FloorCleared when intValue < def.IntParam:
                        continue;
                    case AchievementTrigger.ComboCounterReached
                        when (!string.IsNullOrEmpty(def.StringParam) && def.StringParam != stringValue) ||
                             intValue < def.IntParam:
                        continue;
                }

                TryUnlock(def);
            }
        }

        private bool TryUnlock(AchievementDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.SteamApiName)) return false;
            if (RequestedKeys.Contains(def.Key)) return false;

            var steam = GetSteam();
            if (steam == null || !steam.Available)
            {
                WarnUnavailableOnce();
                return false;
            }

            // Ya desbloqueado en Steam (otra sesión/dispositivo) — solo memoizar.
            if (steam.TryGetAchievementState(def.SteamApiName, out var unlocked) && unlocked)
            {
                RequestedKeys.Add(def.Key);
                return false;
            }

            if (!steam.UnlockAchievement(def.SteamApiName)) return false;

            RequestedKeys.Add(def.Key);
            return true;
        }

        private void WarnUnavailableOnce()
        {
            if (_warnedUnavailable) return;
            _warnedUnavailable = true;
            Debug.Log("[Achievements] Steam no disponible — los logros de esta sesión no se reportan.");
        }

        private static SteamConfigSO GetConfig() =>
            ServiceLocator.TryGetService<SteamConfigSO>(out var config) ? config : null;

        private static ISteamService GetSteam() =>
            ServiceLocator.TryGetService<ISteamService>(out var steam) ? steam : null;
    }
}
