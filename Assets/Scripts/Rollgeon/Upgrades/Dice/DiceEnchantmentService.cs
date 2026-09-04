using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Implementación canónica del Canal Dados — mantiene el
    /// <see cref="RuntimeDiceBag"/>, valida y aplica encantamientos, y dispatcha
    /// los triggers desde los eventos del combate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> El service vive en <c>ServiceScope.Global</c> — registra
    /// listeners de los eventos del run (<c>OnRunStart</c>, <c>OnRunEnd</c>,
    /// <c>OnRollResolved</c>, <c>OnDiceRolled</c>, <c>OnTurnFinished</c>,
    /// <c>TypedEvent&lt;ComboMatchedPayload&gt;</c>). El estado per-run
    /// (<see cref="RuntimeDiceBag"/>) se libera automáticamente via
    /// <c>ClearScope(Run)</c> al fin de la run.
    /// </para>
    /// <para>
    /// <b>Dispatch.</b> Cada handler crea un <see cref="EnchantmentScratch"/>
    /// fresh, itera todos los encantamientos del bag, dispatcha el hook que
    /// corresponda, y al final aplica los side effects (gold) via
    /// <see cref="IEconomyService"/>. Los bonus de daño (combo damage, multiplier,
    /// block) quedan en <see cref="LastComboScratch"/> para el damage pipeline.
    /// </para>
    /// <para>
    /// <b>Integración pendiente.</b> El AttackResolver / damage pipeline aún
    /// no consume <see cref="LastComboScratch"/> — Phase 4 deja esa puerta
    /// abierta. La integración la hace el ticket que aterrice el resolver.
    /// </para>
    /// </remarks>
    public sealed class DiceEnchantmentService
        : IDiceEnchantmentService, IDiceEnchantmentRuntime, IDisposable
    {
        private const string LogPrefix = "[DiceEnchantmentService] ";

        private readonly EnchantmentConfigSO _config;
        private bool _subscribed;
        private IReadOnlyList<int> _lastFinalRoll;

        public RuntimeDiceBag Bag { get; private set; }
        public bool IsReady => Bag != null;
        public EnchantmentScratch LastComboScratch { get; private set; }

        public DiceEnchantmentService(EnchantmentConfigSO config)
        {
            _config = config;
        }

        // ====================================================================
        // Lifecycle
        // ====================================================================

        public void Register()
        {
            ServiceLocator.AddService<IDiceEnchantmentService>(this, ServiceScope.Global);
            ServiceLocator.AddService<IDiceEnchantmentRuntime>(this, ServiceScope.Global);
            SubscribeEvents();
        }

        public void Dispose()
        {
            UnsubscribeEvents();
            Bag = null;
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
            EventManager.Subscribe(EventName.OnTurnFinished, OnTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnCombatStart, OnCombatStartHandler);
            TypedEvent<ComboMatchedPayload>.Subscribe(OnComboMatchedHandler);
            TypedEvent<ComboPlayedPayload>.Subscribe(OnComboPlayedHandler);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.UnSubscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            EventManager.UnSubscribe(EventName.OnDiceRolled, OnDiceRolledHandler);
            EventManager.UnSubscribe(EventName.OnTurnFinished, OnTurnFinishedHandler);
            EventManager.UnSubscribe(EventName.OnCombatStart, OnCombatStartHandler);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(OnComboMatchedHandler);
            TypedEvent<ComboPlayedPayload>.Unsubscribe(OnComboPlayedHandler);
            _subscribed = false;
        }

        // ====================================================================
        // Initialization
        // ====================================================================

        public void InitializeFromBag(DiceBagSO bag)
        {
            if (bag == null || bag.Dice == null || bag.Dice.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "InitializeFromBag con DiceBagSO null/empty — no se inicializa.");
                return;
            }
            Bag = new RuntimeDiceBag(bag.Dice, ResolveEnchantmentById);
            ServiceLocator.AddService<RuntimeDiceBag>(Bag, ServiceScope.Run);
            global::Patterns.Save.SaveSystem.Register(Bag);
        }

        private static EnchantmentSO ResolveEnchantmentById(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)) return null;
            if (!ServiceLocator.TryGetService<EnchantmentCatalogSO>(out var catalog) || catalog == null)
                return null;
            return catalog.GetById(upgradeId);
        }

        private void EnsureInitializedFromPlayer()
        {
            if (Bag != null) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps)) return;
            if (ps?.DiceBag == null) return;
            InitializeFromBag(ps.DiceBag);
        }

        // ====================================================================
        // Event handlers
        // ====================================================================

        private void OnRunStartHandler(params object[] args)
        {
            EnsureInitializedFromPlayer();
        }

        private void OnRunEndHandler(params object[] args)
        {
            // No-op — ClearScope(Run) en RunBootstrapper libera el RuntimeDiceBag.
            Bag = null;
            LastComboScratch = null;
            _lastFinalRoll = null;
        }

        private void OnRollResolvedHandler(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid sourceGuid)) return;
            if (!(args[1] is IReadOnlyList<int> faces)) return;
            _lastFinalRoll = faces;

            // BUG-060: args[2] (opcional, back-compat) es el RollActionKind que armó
            // el emisor — Attack/Defense/Heal EN COMBATE son las únicas tiradas que
            // pagan encantamientos de oro. Sin discriminante (emisores viejos/tests)
            // Unknown ⇒ no pagable, fail-safe.
            var kind = args.Length > 2 && args[2] is RollActionKind k ? k : RollActionKind.Unknown;
            if (!kind.IsCombatPayable()) return;

            // args[3] (opcional) es el ComboDetectionResult REAL de esta tirada — antes
            // este dispatch armaba el EffectContext sin ComboResult, así que
            // PcNoComboThisRoll (Ench_GoldOnRoll / Ambicioso) siempre evaluaba "sin combo"
            // y pagaba de más. Boxing de Nullable<T>: HasValue ⇒ boxed T; sin valor ⇒ null.
            ComboDetectionResult? comboResult = null;
            if (args.Length > 3 && args[3] is ComboDetectionResult crVal) comboResult = crVal;

            var effectCtx = new EffectContext
            {
                SourceGuid = sourceGuid,
                DiceResult = faces,
                ComboResult = comboResult,
            };
            DispatchRollResolved(effectCtx);
        }

        private void OnDiceRolledHandler(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid sourceGuid)) return;
            if (!(args[1] is IReadOnlyList<int> faces)) return;

            var effectCtx = new EffectContext
            {
                SourceGuid = sourceGuid,
                DiceResult = faces,
            };
            DispatchDiceRolled(effectCtx);
        }

        private void OnTurnFinishedHandler(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid entityGuid)) return;

            // Solo dispatchamos para turnos del player (no enemigos).
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps)) return;
            if (ps == null || ps.PlayerGuid != entityGuid) return;

            var effectCtx = new EffectContext
            {
                SourceGuid = entityGuid,
                DiceResult = _lastFinalRoll,
            };
            DispatchTurnFinished(effectCtx);
        }

        // Schema EventName.OnCombatStart: args = [Guid roomInstanceId] — sin entity. El
        // source es el jugador (dueño del bag); sin player service no hay a quién atribuir.
        private void OnCombatStartHandler(params object[] args)
        {
            if (Bag == null) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null) return;

            var effectCtx = new EffectContext { SourceGuid = ps.PlayerGuid };
            DispatchCombatStarted(effectCtx);
        }

        private void OnComboMatchedHandler(ComboMatchedPayload payload)
        {
            if (Bag == null) return;
            if (string.IsNullOrEmpty(payload.ComboId)) return;

            // BUG carrier-scope: antes se usaba el overload legacy de Match (sin índices),
            // así que CarrierParticipates/PcCarrierFace nunca veían contribuyentes y
            // "cuando este dado participa del combo" terminaba evaluando la tirada
            // COMPLETA. payload.ContributingDice ya viene resuelto a bag slot (lo arma
            // ContributingDiceResolver.ResolveDetailed en DiceZoneView.RunComboDetection) —
            // lo reusamos directo como ContributingIndices y dejamos KeptDiceOriginalIndices
            // en null: eff.DiceResult acá es la tirada completa indexada por bag slot (mismo
            // espacio que ctx.Slot.BagSlotIndex), así que "sin mapa ⇒ índice ya es bag slot"
            // (el fallback documentado de ResolveBagSlot) es exactamente la identidad correcta.
            var contributingBagSlots = ToBagSlots(payload.ContributingDice);

            var effectCtx = new EffectContext
            {
                SourceGuid = payload.SourceGuid,
                DiceResult = _lastFinalRoll,
                ComboResult = ComboDetectionResult.Match(payload.ComboId, payload.BaseDamage,
                    _lastFinalRoll?.Count ?? 0, contributingBagSlots, payload.DynamicBonus),
            };
            var scratch = DispatchComboMatched(effectCtx, payload.ComboId);
            LastComboScratch = scratch;
            // BUG-017: ComboMatched es preview (se re-dispara en cada toggle de hold) — los
            // recursos del scratch se materializan solo at-played (ComboPlayService); acá no.
        }

        private static IReadOnlyList<int> ToBagSlots(
            IReadOnlyList<Rollgeon.Combat.Damage.ContributingDie> contributingDice)
        {
            if (contributingDice == null || contributingDice.Count == 0) return null;
            var slots = new List<int>(contributingDice.Count);
            for (int i = 0; i < contributingDice.Count; i++) slots.Add(contributingDice[i].BagSlot);
            return slots;
        }

        private void OnComboPlayedHandler(ComboPlayedPayload payload)
        {
            if (Bag == null) return;
            if (string.IsNullOrEmpty(payload.ComboId)) return;
            // BUG-060: mismo gate que OnRollResolvedHandler — un trío tirado para MOVERSE
            // (o cualquier acción no-combate) no debe disparar "Avaro"/"Codicioso".
            if (!payload.ActionKind.IsCombatPayable()) return;

            // A diferencia de OnComboMatched (que reconstruye desde _lastFinalRoll), el
            // payload de ComboPlayed es autosuficiente: dados, kept y combo completos.
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

            var ctx = new EnchantmentTriggerContext
            {
                Effect = effectCtx,
                Scratch = scratch,
                ComboId = payload.ComboId,
            };
            ForEachEnchantment(ctx, (trigger, c) =>
            {
                if (trigger is IOnComboPlayedTrigger r) r.OnComboPlayed(c);
            });

            // NO se toca LastComboScratch (contrato del preview / damage pipeline at-match).
            if (ownScratch) ApplyScratchSideEffects(scratch);
        }

        // ====================================================================
        // Dispatch helpers
        // ====================================================================

        private void DispatchRollResolved(EffectContext effectCtx)
        {
            if (Bag == null) return;
            var scratch = new EnchantmentScratch();
            var ctx = new EnchantmentTriggerContext { Effect = effectCtx, Scratch = scratch };
            ForEachEnchantment(ctx, (trigger, c) =>
            {
                if (trigger is IOnRollResolvedTrigger r) r.OnRollResolved(c);
            });
            ApplyScratchSideEffects(scratch);
        }

        private void DispatchDiceRolled(EffectContext effectCtx)
        {
            if (Bag == null) return;
            var scratch = new EnchantmentScratch();
            var ctx = new EnchantmentTriggerContext { Effect = effectCtx, Scratch = scratch };
            ForEachEnchantment(ctx, (trigger, c) =>
            {
                if (trigger is IOnDiceRolledTrigger r) r.OnDiceRolled(c);
            });
            ApplyScratchSideEffects(scratch);
        }

        private void DispatchTurnFinished(EffectContext effectCtx)
        {
            if (Bag == null) return;
            var scratch = new EnchantmentScratch();
            var ctx = new EnchantmentTriggerContext { Effect = effectCtx, Scratch = scratch };
            ForEachEnchantment(ctx, (trigger, c) =>
            {
                if (trigger is IOnTurnFinishedTrigger r) r.OnTurnFinished(c);
            });
            ApplyScratchSideEffects(scratch);
        }

        private void DispatchCombatStarted(EffectContext effectCtx)
        {
            if (Bag == null) return;
            var scratch = new EnchantmentScratch();
            var ctx = new EnchantmentTriggerContext { Effect = effectCtx, Scratch = scratch };
            ForEachEnchantment(ctx, (trigger, c) =>
            {
                if (trigger is IOnCombatStartedTrigger r) r.OnCombatStarted(c);
            });
            ApplyScratchSideEffects(scratch);
        }

        private EnchantmentScratch DispatchComboMatched(EffectContext effectCtx, string comboId)
        {
            var scratch = new EnchantmentScratch();
            if (Bag == null) return scratch;
            var ctx = new EnchantmentTriggerContext
            {
                Effect = effectCtx,
                Scratch = scratch,
                ComboId = comboId,
            };
            ForEachEnchantment(ctx, (trigger, c) =>
            {
                if (trigger is IOnComboMatchedTrigger r) r.OnComboMatched(c);
            });
            return scratch;
        }

        private void ForEachEnchantment(
            EnchantmentTriggerContext ctx,
            Action<IEnchantmentTrigger, EnchantmentTriggerContext> dispatch)
        {
            int n = Bag.Dice.Count;
            for (int b = 0; b < n; b++)
            {
                var slots = Bag.GetEnchantments(b);
                for (int s = 0; s < slots.Count; s++)
                {
                    var ench = slots[s];
                    if (ench == null) continue;
                    ctx.Slot = new EnchantmentSlotRef(Bag.Dice[b], b, s);
                    var triggers = ench.Triggers;
                    if (triggers == null) continue;
                    // Snapshot-delta por (dado, encantamiento): atribuye al journal lo que
                    // ESTE encantamiento aportó al combo, cara del dado incluida. Neutro =
                    // cero entradas.
                    var before = ctx.Scratch != null ? ScratchSnapshot.Of(ctx.Scratch, b) : default;
                    for (int t = 0; t < triggers.Count; t++)
                    {
                        var trigger = triggers[t];
                        if (trigger != null) dispatch(trigger, ctx);
                    }
                    if (ctx.Scratch != null)
                        ScratchSnapshot.RecordDelta(ctx.Scratch, in before,
                            ScratchSourceKind.Enchantment, ench.name, ench, b);
                }
            }
        }

        private static void ApplyScratchSideEffects(EnchantmentScratch scratch)
        {
            EnchantmentScratchApplier.Apply(scratch, ResolvePlayerGuid());
        }

        // ====================================================================
        // IDiceEnchantmentService — Apply / Validate / Remove / Compute
        // ====================================================================

        public EnchantmentApplyResult ValidateApply(int bagIndex, EnchantmentSO ench)
        {
            EnsureInitializedFromPlayer();

            if (ench == null) return EnchantmentApplyResult.Fail("Enchantment is null.");
            if (Bag == null) return EnchantmentApplyResult.Fail("Runtime bag no inicializado.");
            if (bagIndex < 0 || bagIndex >= Bag.Dice.Count)
                return EnchantmentApplyResult.Fail($"Bag index {bagIndex} fuera de rango.");

            var diceType = Bag.Dice[bagIndex];
            if (!ench.IsCompatibleWith(diceType))
                return EnchantmentApplyResult.Fail(
                    $"Encantamiento '{ench.UpgradeId}' no es compatible con {diceType}.");

            var projected = ComputeProjectedFaces(bagIndex, ench);
            int minRequired = _config != null ? _config.MinFacesAfterApply : 1;
            if (projected.Count < minRequired)
            {
                return EnchantmentApplyResult.Fail(
                    $"Dejaría al dado con {projected.Count} caras (mínimo requerido: {minRequired}).",
                    projected);
            }

            return EnchantmentApplyResult.Ok(projected);
        }

        public EnchantmentApplyResult Apply(int bagIndex, EnchantmentSO ench)
        {
            var validation = ValidateApply(bagIndex, ench);
            if (!validation.Success) return validation;

            // Append — nunca pisa un encantamiento existente, así que no hay
            // counters previos que limpiar: el slot nace fresco.
            int assignedIndex = Bag.AddEnchantment(bagIndex, ench);
            if (assignedIndex < 0)
                return EnchantmentApplyResult.Fail("AddEnchantment rechazó la operación.");

            var diceType = Bag.Dice[bagIndex];
            var slot = new EnchantmentSlotRef(diceType, bagIndex, assignedIndex);

            // Dispatch IOnEnchantmentAppliedTrigger
            var scratch = new EnchantmentScratch();
            var ctx = new EnchantmentTriggerContext
            {
                Effect = new EffectContext { SourceGuid = ResolvePlayerGuid() },
                Slot = slot,
                Scratch = scratch,
            };
            if (ench.Triggers != null)
            {
                foreach (var trigger in ench.Triggers)
                {
                    if (trigger is IOnEnchantmentAppliedTrigger applied)
                        applied.OnEnchantmentApplied(ctx);
                }
            }
            ApplyScratchSideEffects(scratch);

            EventManager.Trigger(EventName.OnEnchantmentApplied,
                ResolvePlayerGuid(), ench.UpgradeId, bagIndex, assignedIndex);

            return EnchantmentApplyResult.Ok(validation.ProjectedFaces, assignedIndex);
        }

        public bool Remove(int bagIndex, int enchSlotIndex)
        {
            if (Bag == null) return false;
            var existing = Bag.GetEnchantmentAt(bagIndex, enchSlotIndex);
            if (existing == null) return false;

            var diceType = Bag.Dice[bagIndex];
            var slot = new EnchantmentSlotRef(diceType, bagIndex, enchSlotIndex);
            Bag.SetEnchantmentAt(bagIndex, enchSlotIndex, null);
            Bag.ClearCountersForSlot(slot);

            EventManager.Trigger(EventName.OnEnchantmentRemoved,
                ResolvePlayerGuid(), existing.UpgradeId, bagIndex, enchSlotIndex);

            return true;
        }

        public IReadOnlyCollection<int> ComputeAllowedFaces(int bagIndex)
        {
            if (Bag == null) return Array.Empty<int>();
            if (bagIndex < 0 || bagIndex >= Bag.Dice.Count) return Array.Empty<int>();

            var diceType = Bag.Dice[bagIndex];
            var faces = SeedFaces(diceType);
            IReadOnlyCollection<int> current = faces;
            foreach (var ench in Bag.GetEnchantments(bagIndex))
            {
                if (ench?.FaceFilter == null) continue;
                current = ench.FaceFilter.GetAllowedFaces(diceType, current);
            }
            return current;
        }

        public EnchantmentScratch ResolveComboBonus(
            Guid sourceGuid, string comboId, IReadOnlyList<int> diceResult, int comboBaseDamage)
        {
            if (Bag == null) return new EnchantmentScratch();

            var effectCtx = new EffectContext
            {
                SourceGuid = sourceGuid,
                DiceResult = diceResult,
                ComboResult = ComboDetectionResult.Match(comboBaseDamage, diceResult?.Count ?? 0),
            };
            // Re-dispatch sin side effects — query "what would happen". Triggers como
            // SpendGoldForComboBonus se ejecutarían dos veces si el caller mezclara
            // este método con el handler del TypedEvent. Convención: usar UNO u otro,
            // no ambos. El caller canónico (AttackResolver) usaría este.
            return DispatchComboMatched(effectCtx, comboId);
        }

        // ====================================================================
        // IDiceEnchantmentRuntime — triggers facing API
        // ====================================================================

        public int GetCounter(EnchantmentSlotRef slot, string key)
            => Bag != null ? Bag.GetCounter(slot, key) : 0;

        public int IncrementCounter(EnchantmentSlotRef slot, string key, int delta = 1)
            => Bag != null ? Bag.IncrementCounter(slot, key, delta) : 0;

        public void ResetCounter(EnchantmentSlotRef slot, string key)
        {
            Bag?.ResetCounter(slot, key);
        }

        public void RemoveEnchantment(EnchantmentSlotRef slot)
        {
            Remove(slot.BagSlotIndex, slot.EnchantmentSlotIndex);
        }

        // ====================================================================
        // Internals
        // ====================================================================

        private IReadOnlyCollection<int> ComputeProjectedFaces(int bagIndex, EnchantmentSO newEnch)
        {
            // Append-only: el nuevo encantamiento se compone sobre TODOS los
            // existentes — nada se reemplaza, así que nada se excluye.
            var diceType = Bag.Dice[bagIndex];
            var faces = SeedFaces(diceType);
            IReadOnlyCollection<int> current = faces;

            var slots = Bag.GetEnchantments(bagIndex);
            for (int i = 0; i < slots.Count; i++)
            {
                var existing = slots[i];
                if (existing?.FaceFilter == null) continue;
                current = existing.FaceFilter.GetAllowedFaces(diceType, current);
            }
            if (newEnch.FaceFilter != null)
            {
                current = newEnch.FaceFilter.GetAllowedFaces(diceType, current);
            }
            return current;
        }

        private static HashSet<int> SeedFaces(DiceType type)
        {
            var faces = new HashSet<int>();
            int max = type.MaxFace();
            for (int f = 1; f <= max; f++) faces.Add(f);
            return faces;
        }

        private static Guid ResolvePlayerGuid()
        {
            return ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Hook para EditMode tests — invoca <see cref="SubscribeEvents"/> sin pasar por <see cref="Register"/>.</summary>
        public void SubscribeEventsForTests() => SubscribeEvents();

        /// <summary>Hook para EditMode tests — desregistra listeners sin Dispose.</summary>
        public void UnsubscribeEventsForTests() => UnsubscribeEvents();
    }
}
