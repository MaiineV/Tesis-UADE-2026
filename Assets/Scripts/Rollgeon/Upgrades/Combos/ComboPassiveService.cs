using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
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
    /// vía <c>TypedEvent&lt;ComboMatchedPayload&gt;</c>. Además dispatcha los hooks
    /// genéricos (<c>IOn*PassiveTrigger</c>) a TODAS las pasivas de la run — mismo
    /// esquema multi-evento que <c>DiceEnchantmentService</c>: scratch fresco por
    /// evento, aplicado de inmediato vía <see cref="EnchantmentScratchApplier"/>.
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

        private readonly ComboPassivePoolSO _pool;
        private bool _subscribed;
        private IReadOnlyList<int> _lastFinalRoll;

        // Guard de reentrada: aplicar un scratch puede mover oro (EnchantmentScratchApplier
        // → economy.Add → OnGoldChanged) — sin este flag, una pasiva que da oro en
        // OnGoldChanged recursa infinito. Los cambios de oro causados por pasivas no
        // re-disparan pasivas.
        private bool _applyingScratch;

        /// <param name="pool">
        /// Pool de pasivas — usado como resolver UpgradeId → SO al restaurar un save.
        /// Puede ser null (el restore descarta pasivas con warning).
        /// </param>
        public ComboPassiveService(ComboPassivePoolSO pool = null)
        {
            _pool = pool;
        }

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
            EventManager.Subscribe(EventName.OnDiceRolled, OnDiceRolledHandler);
            EventManager.Subscribe(EventName.OnTurnStarted, OnTurnStartedHandler);
            EventManager.Subscribe(EventName.OnTurnFinished, OnTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            EventManager.Subscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.Subscribe(EventName.OnGoldChanged, OnGoldChangedHandler);
            TypedEvent<ComboMatchedPayload>.Subscribe(OnComboMatched);
            TypedEvent<DamageResolvedPayload>.Subscribe(OnDamageResolvedHandler);
            TypedEvent<ComboPlayedPayload>.Subscribe(OnComboPlayed);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.UnSubscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            EventManager.UnSubscribe(EventName.OnDiceRolled, OnDiceRolledHandler);
            EventManager.UnSubscribe(EventName.OnTurnStarted, OnTurnStartedHandler);
            EventManager.UnSubscribe(EventName.OnTurnFinished, OnTurnFinishedHandler);
            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            EventManager.UnSubscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnGoldChanged, OnGoldChangedHandler);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(OnComboMatched);
            TypedEvent<DamageResolvedPayload>.Unsubscribe(OnDamageResolvedHandler);
            TypedEvent<ComboPlayedPayload>.Unsubscribe(OnComboPlayed);
            _subscribed = false;
        }

        // ====================================================================
        // Run lifecycle
        // ====================================================================

        private void OnRunStartHandler(params object[] args)
        {
            var state = new RunComboPassivesState(ResolvePassiveById);
            ServiceLocator.AddService<RunComboPassivesState>(state, ServiceScope.Run);
            global::Patterns.Save.SaveSystem.Register(state);
        }

        private ComboPassiveSO ResolvePassiveById(string upgradeId)
        {
            if (_pool?.Entries == null || string.IsNullOrEmpty(upgradeId)) return null;
            foreach (var entry in _pool.Entries)
            {
                if (entry.Passive != null && entry.Passive.UpgradeId == upgradeId)
                    return entry.Passive;
            }
            return null;
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
            // Canal único: aplica los boosts permanentes de stat (+Attack/Health/Speed/Energy) que la
            // pasiva tenga configurados, además de su efecto de combo (FlatDamageBonus / triggers).
            Rollgeon.Upgrades.PlayerStatGrants.Apply(passive.StatGrants);
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
            // Cachear ANTES de dispatchar — los readers de los triggers deben ver este roll.
            _lastFinalRoll = faces;

            var sourceGuid = args[0] is Guid guid ? guid : Guid.Empty;
            var ctx = BuildGenericContext(new EffectContext { SourceGuid = sourceGuid, DiceResult = faces });
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnRollResolvedPassiveTrigger h) h.OnRollResolved(c);
            });
        }

        private void OnDiceRolledHandler(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid sourceGuid)) return;
            if (!(args[1] is IReadOnlyList<int> faces)) return;

            var ctx = BuildGenericContext(new EffectContext { SourceGuid = sourceGuid, DiceResult = faces });
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnDiceRolledPassiveTrigger h) h.OnDiceRolled(c);
            });
        }

        private void OnTurnStartedHandler(params object[] args)
        {
            if (!TryGetPlayerTurnGuid(args, out var playerGuid)) return;

            var ctx = BuildGenericContext(new EffectContext { SourceGuid = playerGuid, DiceResult = _lastFinalRoll });
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnTurnStartedPassiveTrigger h) h.OnTurnStarted(c);
            });
        }

        private void OnTurnFinishedHandler(params object[] args)
        {
            if (!TryGetPlayerTurnGuid(args, out var playerGuid)) return;

            var ctx = BuildGenericContext(new EffectContext { SourceGuid = playerGuid, DiceResult = _lastFinalRoll });
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnTurnFinishedPassiveTrigger h) h.OnTurnFinished(c);
            });
        }

        private void OnRoomEnteredHandler(params object[] args)
        {
            // Defensivo: hay tests que disparan OnRoomEntered sin args.
            if (args == null || args.Length < 1 || !(args[0] is Guid roomInstanceId)) return;
            string roomId = args.Length >= 2 && args[1] is string s ? s : string.Empty;

            var ctx = BuildGenericContext(new EffectContext { SourceGuid = ResolvePlayerGuid() });
            ctx.RoomInstanceId = roomInstanceId;
            ctx.RoomId = roomId;
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnRoomEnteredPassiveTrigger h) h.OnRoomEntered(c);
            });
        }

        private void OnCombatStartHandler(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid roomInstanceId)) return;

            var ctx = BuildGenericContext(new EffectContext { SourceGuid = ResolvePlayerGuid() });
            ctx.RoomInstanceId = roomInstanceId;
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnCombatStartPassiveTrigger h) h.OnCombatStart(c);
            });
        }

        private void OnCombatEndHandler(params object[] args)
        {
            // Varios tests disparan OnCombatEnd con 0/1 args o outcome mal tipado —
            // se dispatcha igual con defaults (Outcome = None sentinel).
            var roomInstanceId = args != null && args.Length >= 1 && args[0] is Guid g ? g : Guid.Empty;
            var outcome = args != null && args.Length >= 2 && args[1] is CombatOutcome o ? o : CombatOutcome.None;

            var ctx = BuildGenericContext(new EffectContext { SourceGuid = ResolvePlayerGuid() });
            ctx.RoomInstanceId = roomInstanceId;
            ctx.Outcome = outcome;
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnCombatEndPassiveTrigger h) h.OnCombatEnd(c);
            });
        }

        private void OnGoldChangedHandler(params object[] args)
        {
            // Oro movido por la aplicación de un scratch de pasiva NO re-dispara pasivas.
            if (_applyingScratch) return;
            if (args == null || args.Length < 2) return;
            if (!(args[0] is int total) || !(args[1] is int delta)) return;

            var ctx = BuildGenericContext(new EffectContext { SourceGuid = ResolvePlayerGuid() });
            ctx.GoldTotal = total;
            ctx.GoldDelta = delta;
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnGoldChangedPassiveTrigger h) h.OnGoldChanged(c);
            });
        }

        private void OnDamageResolvedHandler(DamageResolvedPayload payload)
        {
            var ctx = BuildGenericContext(new EffectContext
            {
                SourceGuid = payload.SourceGuid,
                TargetGuid = payload.TargetGuid,
                DiceResult = _lastFinalRoll,
            });
            ctx.Damage = payload;
            DispatchGeneric(ctx, static (t, c) =>
            {
                if (t is IOnDamageResolvedPassiveTrigger h) h.OnDamageResolved(c);
            });
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
            // BUG-017 (canal combos): ComboMatched es preview y se re-dispara en cada
            // toggle de hold — los recursos del scratch se materializan solo at-played
            // (ComboPlayService); acá solo queda el canal de daño (LastComboScratch).
        }

        private void OnComboPlayed(ComboPlayedPayload payload)
        {
            if (string.IsNullOrEmpty(payload.ComboId)) return;
            if (!ServiceLocator.TryGetService<RunComboPassivesState>(out var state) || state == null) return;
            var all = state.GetAll();
            if (all.Count == 0) return;

            var effectCtx = new EffectContext
            {
                SourceGuid = payload.SourceGuid,
                TargetGuid = payload.TargetGuid,
                DiceResult = payload.DiceResult,
                KeptDice = payload.KeptDice,
                KeptDiceOriginalIndices = payload.KeptDiceOriginalIndices,
                ComboResult = payload.ComboResult,
            };

            // El play scratch pertenece al ComboPlayService (dueño único del apply de
            // recursos). Fallback local para tests sin el service — ahí aplicamos nosotros.
            EnchantmentScratch scratch;
            bool ownScratch = false;
            if (ServiceLocator.TryGetService<IComboPlayService>(out var play) && play?.CurrentPlayScratch != null)
            {
                scratch = play.CurrentPlayScratch;
            }
            else
            {
                scratch = new EnchantmentScratch();
                ownScratch = true;
            }

            var ctx = new ComboPassiveContext
            {
                Effect = effectCtx,
                ComboId = payload.ComboId,
                Scratch = scratch,
            };

            for (int i = 0; i < all.Count; i++)
            {
                var passive = all[i];
                if (passive == null) continue;
                // Scope por combo: TargetComboId vacío = reacciona a cualquier combo jugado.
                if (!string.IsNullOrEmpty(passive.TargetComboId) && passive.TargetComboId != payload.ComboId)
                    continue;

                var triggers = passive.ExtraTriggers;
                if (triggers == null) continue;
                for (int t = 0; t < triggers.Count; t++)
                {
                    if (triggers[t] is IOnComboPlayedPassiveTrigger played)
                        played.OnComboPlayed(ctx);
                }
            }

            // NO se toca LastComboScratch (contrato del preview / damage pipeline at-match).
            if (ownScratch) ApplyScratchSideEffects(scratch);
        }

        private void ApplyScratchSideEffects(EnchantmentScratch scratch)
        {
            _applyingScratch = true;
            try
            {
                EnchantmentScratchApplier.Apply(scratch, ResolvePlayerGuid());
            }
            finally
            {
                _applyingScratch = false;
            }
        }

        // ====================================================================
        // Generic hook dispatch
        // ====================================================================

        /// <summary>
        /// Dispatch de un hook genérico a TODAS las pasivas de la run (sin importar
        /// <c>TargetComboId</c> — ese filtro solo aplica a <see cref="OnComboMatched"/>).
        /// Aplica los side effects del scratch al final, igual que el path de combo,
        /// pero NO toca <see cref="LastComboScratch"/> (contrato del damage pipeline).
        /// </summary>
        private void DispatchGeneric(ComboPassiveContext ctx, Action<IComboPassiveTrigger, ComboPassiveContext> dispatch)
        {
            if (!ServiceLocator.TryGetService<RunComboPassivesState>(out var state) || state == null) return;
            var all = state.GetAll();
            if (all.Count == 0) return;

            for (int i = 0; i < all.Count; i++)
            {
                var triggers = all[i]?.ExtraTriggers;
                if (triggers == null) continue;
                for (int t = 0; t < triggers.Count; t++)
                {
                    if (triggers[t] != null) dispatch(triggers[t], ctx);
                }
            }

            ApplyScratchSideEffects(ctx.Scratch);
        }

        private static ComboPassiveContext BuildGenericContext(EffectContext effectCtx)
        {
            return new ComboPassiveContext
            {
                Effect = effectCtx,
                ComboId = null,
                Scratch = new EnchantmentScratch(),
            };
        }

        /// <summary>Filtro de turnos: solo dispatchamos turnos del player (no enemigos).</summary>
        private static bool TryGetPlayerTurnGuid(object[] args, out Guid playerGuid)
        {
            playerGuid = Guid.Empty;
            if (args == null || args.Length < 1) return false;
            if (!(args[0] is Guid entityGuid)) return false;

            var resolved = ResolvePlayerGuid();
            if (resolved == Guid.Empty || resolved != entityGuid) return false;

            playerGuid = entityGuid;
            return true;
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
