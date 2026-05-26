using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Service Global del Canal Combos. Mantiene el <see cref="RunComboPassivesState"/>
    /// run-scoped, expone <see cref="Apply"/> para tienda + <see cref="GetBonusDamage"/>
    /// para el damage pipeline, y dispatcha extras (<see cref="IOnComboPassiveMatchedTrigger"/>)
    /// vía <c>TypedEvent&lt;ComboMatchedPayload&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> Service Global. El <see cref="RunComboPassivesState"/> vive en
    /// scope Run — recreado en <c>OnRunStart</c> y liberado en <c>ClearScope(Run)</c>.
    /// </para>
    /// <para>
    /// <b>Coexistencia con DiceEnchantmentService.</b> Ambos services se suscriben
    /// a <c>TypedEvent&lt;ComboMatchedPayload&gt;</c>. Cada uno produce su propio
    /// <see cref="EnchantmentScratch"/>. El AttackResolver (futuro) lee ambos.
    /// </para>
    /// </remarks>
    public sealed class ComboPassiveService : IComboPassiveService, IDisposable
    {
        private const string LogPrefix = "[ComboPassiveService] ";

        private bool _subscribed;
        private IReadOnlyList<int> _lastFinalRoll;

        public EnchantmentScratch LastComboScratch { get; private set; }

        public bool IsReady => ServiceLocator.HasService<RunComboPassivesState>();

        // ====================================================================
        // Lifecycle
        // ====================================================================

        public void Register()
        {
            ServiceLocator.AddService<IComboPassiveService>(this, ServiceScope.Global);
            SubscribeEvents();
        }

        public void Dispose()
        {
            UnsubscribeEvents();
            LastComboScratch = null;
            _lastFinalRoll = null;
        }

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.Subscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.Subscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            TypedEvent<ComboMatchedPayload>.Subscribe(OnComboMatched);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.UnSubscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(OnComboMatched);
            _subscribed = false;
        }

        // ====================================================================
        // Run lifecycle
        // ====================================================================

        private void OnRunStartHandler(params object[] args)
        {
            var state = new RunComboPassivesState();
            ServiceLocator.AddService<RunComboPassivesState>(state, ServiceScope.Run);
        }

        private void OnRunEndHandler(params object[] args)
        {
            LastComboScratch = null;
            _lastFinalRoll = null;
            // El state se libera vía ClearScope(Run).
        }

        // ====================================================================
        // IComboPassiveService — public API
        // ====================================================================

        public IReadOnlyList<ComboPassiveSO> GetPassivesFor(string comboId)
        {
            if (!ServiceLocator.TryGetService<RunComboPassivesState>(out var state) || state == null)
                return Array.Empty<ComboPassiveSO>();
            return state.Get(comboId);
        }

        public void Apply(ComboPassiveSO passive)
        {
            if (passive == null) return;
            if (!ServiceLocator.TryGetService<RunComboPassivesState>(out var state) || state == null)
            {
                Debug.LogWarning(LogPrefix + "Apply llamado pero RunComboPassivesState no está registrado " +
                                              "(fuera de run?). Pasiva descartada.");
                return;
            }
            state.Add(passive);
            EventManager.Trigger(EventName.OnItemObtained, ResolvePlayerGuid(), passive.UpgradeId);
        }

        public int GetBonusDamage(string comboId)
        {
            var passives = GetPassivesFor(comboId);
            if (passives.Count == 0) return 0;

            // Build un EffectContext minimal para los readers — DiceResult
            // del último roll si lo tenemos, vacío si no.
            var effectCtx = new EffectContext { DiceResult = _lastFinalRoll };

            int total = 0;
            for (int i = 0; i < passives.Count; i++)
            {
                var passive = passives[i];
                if (passive?.FlatDamageBonus == null) continue;
                total += passive.FlatDamageBonus.Read(effectCtx);
            }
            return total;
        }

        // ====================================================================
        // Event handlers
        // ====================================================================

        private void OnRollResolvedHandler(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[1] is IReadOnlyList<int> faces)) return;
            _lastFinalRoll = faces;
        }

        private void OnComboMatched(ComboMatchedPayload payload)
        {
            if (string.IsNullOrEmpty(payload.ComboId)) return;
            var passives = GetPassivesFor(payload.ComboId);
            if (passives.Count == 0) return;

            var effectCtx = new EffectContext
            {
                SourceGuid = payload.SourceGuid,
                DiceResult = _lastFinalRoll,
                ComboResult = ComboDetectionResult.Match(payload.BaseDamage, _lastFinalRoll?.Count ?? 0),
            };

            var scratch = new EnchantmentScratch();
            var ctx = new ComboPassiveContext
            {
                Effect = effectCtx,
                ComboId = payload.ComboId,
                Scratch = scratch,
            };

            for (int i = 0; i < passives.Count; i++)
            {
                var passive = passives[i];
                if (passive == null) continue;

                // Flat damage bonus también suma acá para que LastComboScratch sea autocontenido.
                if (passive.FlatDamageBonus != null)
                {
                    scratch.BonusComboDamage += passive.FlatDamageBonus.Read(effectCtx);
                }

                // Extra triggers (gold-on-match, shield-on-match, etc.)
                var triggers = passive.ExtraTriggers;
                if (triggers == null) continue;
                for (int t = 0; t < triggers.Count; t++)
                {
                    if (triggers[t] is IOnComboPassiveMatchedTrigger matched)
                        matched.OnComboMatched(ctx);
                }
            }

            LastComboScratch = scratch;
            ApplyScratchSideEffects(scratch);
        }

        private static void ApplyScratchSideEffects(EnchantmentScratch scratch)
        {
            if (scratch == null) return;
            if (scratch.BonusGold == 0) return;
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null) return;
            if (scratch.BonusGold > 0) economy.Add(scratch.BonusGold);
            else economy.Spend(-scratch.BonusGold);
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static Guid ResolvePlayerGuid()
        {
            return ServiceLocator.TryGetService<Rollgeon.Player.IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        public void SubscribeEventsForTests() => SubscribeEvents();
        public void UnsubscribeEventsForTests() => UnsubscribeEvents();
    }
}
