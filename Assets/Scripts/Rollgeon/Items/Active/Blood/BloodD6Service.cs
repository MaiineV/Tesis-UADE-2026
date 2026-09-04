using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos.Play;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Items.Active.Blood
{
    /// <summary>
    /// Implementación POCO de <see cref="IBloodD6Service"/> (Feature#0085, Blood D6): arma
    /// un bonus sobre el próximo combo de Ataque válido, lo espera vía
    /// <c>TypedEvent&lt;ComboPlayedPayload&gt;</c> y lo consume vía
    /// <c>TypedEvent&lt;DamageResolvedPayload&gt;</c> — el daño base del combo NUNCA se toca,
    /// el bonus es daño extra repartido entre el objetivo primario y hasta
    /// <c>MaxReceivers - 1</c> secundarios cercanos con línea de visión.
    /// </summary>
    public sealed class BloodD6Service : IBloodD6Service, IPreloadableService, IDisposable
    {
        // Cara 1..6 → bonus% y máximo de receptores (incluye al primario).
        private static readonly int[] BonusPctByFace = { 10, 20, 30, 40, 50, 66 };

        private sealed class PendingCharge
        {
            public int BonusPct;
            public int MaxReceivers;
        }

        private sealed class AwaitingCombo
        {
            public string ComboId;
            public Guid TargetGuid;
            public GridCoord PrimaryCoord;
            public bool HasPrimaryCoord;
        }

        private readonly Dictionary<Guid, PendingCharge> _pending = new();
        private readonly Dictionary<Guid, AwaitingCombo> _awaiting = new();

        private Action<ComboPlayedPayload> _onComboPlayedHandler;
        private Action<DamageResolvedPayload> _onDamageResolvedHandler;
        private EventManager.EventReceiver _onRollResolvedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Junto al resto de servicios de items activos (61) — arma después de que exista el slot.</summary>
        public int Priority => 85;

        public void Register()
        {
            SubscribeHandlers();
            ServiceLocator.AddService<IBloodD6Service>(this, ServiceScope.Global);
            ServiceLocator.AddService<BloodD6Service>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests: arma las suscripciones sin ServiceLocator.</summary>
        public void ConfigureForTests() => SubscribeHandlers();

        private void SubscribeHandlers()
        {
            UnsubscribeHandlers();

            _onComboPlayedHandler = OnComboPlayed;
            TypedEvent<ComboPlayedPayload>.Subscribe(_onComboPlayedHandler);

            _onDamageResolvedHandler = OnDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolvedHandler);

            _onRollResolvedHandler = OnRollResolvedExternal;
            EventManager.Subscribe(EventName.OnRollResolved, _onRollResolvedHandler);

            _onCombatEndHandler = OnScopeEndedExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);

            _onRunEndHandler = OnScopeEndedExternal;
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        private void UnsubscribeHandlers()
        {
            if (_onComboPlayedHandler != null)
            {
                TypedEvent<ComboPlayedPayload>.Unsubscribe(_onComboPlayedHandler);
                _onComboPlayedHandler = null;
            }
            if (_onDamageResolvedHandler != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolvedHandler);
                _onDamageResolvedHandler = null;
            }
            if (_onRollResolvedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRollResolved, _onRollResolvedHandler);
                _onRollResolvedHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
        }

        public void Dispose()
        {
            UnsubscribeHandlers();
            ClearAll();
        }

        // ======================================================================
        // IBloodD6Service
        // ======================================================================

        public void Arm(Guid owner, int face)
        {
            if (owner == Guid.Empty) return;

            int clampedFace = Mathf.Clamp(face, 1, BonusPctByFace.Length);
            _pending[owner] = new PendingCharge
            {
                BonusPct = BonusPctByFace[clampedFace - 1],
                MaxReceivers = BonusPctByFace.Length + 1 - clampedFace,
            };
            _awaiting.Remove(owner);

            EventManager.Trigger(EventName.OnBloodD6Armed, owner, clampedFace);
        }

        public bool HasPending(Guid owner) => owner != Guid.Empty && _pending.ContainsKey(owner);

        public bool TryGetPendingBonusPct(Guid owner, out int bonusPct)
        {
            bonusPct = 0;
            if (owner == Guid.Empty || !_pending.TryGetValue(owner, out var pending)) return false;
            bonusPct = pending.BonusPct;
            return true;
        }

        public void Clear(Guid owner)
        {
            if (owner == Guid.Empty) return;
            _pending.Remove(owner);
            _awaiting.Remove(owner);
        }

        public void ClearAll()
        {
            _pending.Clear();
            _awaiting.Clear();
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        // El combo de Ataque en curso queda "en espera" — el bonus se consume recién cuando
        // el golpe real resuelva (ComboPlayedPayload dispara ANTES de que el daño se aplique).
        private void OnComboPlayed(ComboPlayedPayload payload)
        {
            if (!_pending.ContainsKey(payload.SourceGuid)) return;
            if (payload.ActionKind != RollActionKind.Attack) return;
            if (string.IsNullOrEmpty(payload.ComboId)) return; // combo inválido no consume

            bool havePrimaryCoord = CombatantQuery.TryGetCoord(payload.TargetGuid, out var primaryCoord);
            _awaiting[payload.SourceGuid] = new AwaitingCombo
            {
                ComboId = payload.ComboId,
                TargetGuid = payload.TargetGuid,
                PrimaryCoord = primaryCoord,
                HasPrimaryCoord = havePrimaryCoord,
            };
        }

        // Nuevo roll: la ventana de combo anterior ya cerró sin golpe (o el jugador tiró de
        // nuevo antes de que el daño resolviera). Solo limpia la espera — la carga sigue
        // pendiente hasta que se consuma o termine el combate.
        private void OnRollResolvedHandlerInternal() => _awaiting.Clear();

        private void OnRollResolvedExternal(params object[] args) => OnRollResolvedHandlerInternal();

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            if (!_awaiting.TryGetValue(payload.SourceGuid, out var awaiting)) return;
            if (payload.Kind != AttackKind.ComboAttack && payload.Kind != AttackKind.BasicAttack) return;
            if (string.IsNullOrEmpty(payload.ComboId) || payload.ComboId != awaiting.ComboId) return;
            if (!_pending.TryGetValue(payload.SourceGuid, out var pending))
            {
                _awaiting.Remove(payload.SourceGuid);
                return;
            }

            // Limpiar PRIMERO: el daño extra que reparte este mismo método no debe re-disparar
            // la carga (ComboId vacío en su DamageContext, pero igual —defensa en profundidad—).
            _pending.Remove(payload.SourceGuid);
            _awaiting.Remove(payload.SourceGuid);

            int bonus = Mathf.FloorToInt((pending.BonusPct / 100f) * (payload.FinalDamage + payload.ShieldAbsorbed));
            if (bonus <= 0)
            {
                EventManager.Trigger(EventName.OnBloodD6Consumed, payload.SourceGuid, 0);
                return;
            }

            var receivers = ResolveReceivers(payload, awaiting, pending.MaxReceivers);
            DistributeDamage(payload.SourceGuid, receivers, bonus);

            EventManager.Trigger(EventName.OnBloodD6Consumed, payload.SourceGuid, bonus);
        }

        private void OnScopeEndedExternal(params object[] args) => ClearAll();

        // ======================================================================
        // Reparto
        // ======================================================================

        // Primario (siempre primero) + secundarios a Manhattan <= 4 del primario con LoS
        // desde el primario, nearest-first (empate por Guid), hasta maxReceivers en total.
        private static List<Guid> ResolveReceivers(DamageResolvedPayload payload, AwaitingCombo awaiting, int maxReceivers)
        {
            var receivers = new List<Guid>();
            if (payload.TargetGuid != Guid.Empty) receivers.Add(payload.TargetGuid);

            int slotsLeft = maxReceivers - receivers.Count;
            if (slotsLeft <= 0) return receivers;
            if (!awaiting.HasPrimaryCoord) return receivers;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return receivers;

            var candidates = CombatantQuery.LiveEnemiesOf(payload.SourceGuid);
            var secondary = new List<(Guid Guid, int Dist)>();
            foreach (var candidate in candidates)
            {
                if (candidate == payload.TargetGuid) continue;
                if (!CombatantQuery.TryGetCoord(candidate, out var coord)) continue;
                int dist = awaiting.PrimaryCoord.Manhattan(coord);
                if (dist > 4) continue;
                if (!GridLineOfSight.HasClearLine(grid, awaiting.PrimaryCoord, coord, payload.TargetGuid, candidate)) continue;
                secondary.Add((candidate, dist));
            }

            secondary.Sort((a, b) =>
            {
                int cmp = a.Dist.CompareTo(b.Dist);
                return cmp != 0 ? cmp : a.Guid.CompareTo(b.Guid);
            });

            for (int i = 0; i < secondary.Count && slotsLeft > 0; i++, slotsLeft--)
                receivers.Add(secondary[i].Guid);

            return receivers;
        }

        // Reparto lo más parejo posible; el resto por redondeo va al primario (receivers[0])
        // y luego a los más cercanos (orden ya nearest-first). ComboId vacío a propósito: no
        // debe volver a armar/consumir Blood D6.
        private static void DistributeDamage(Guid owner, List<Guid> receivers, int bonus)
        {
            if (receivers.Count == 0) return;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null)
            {
                Debug.LogWarning("[BloodD6Service] IDamagePipeline no registrado — bonus de Blood D6 perdido.");
                return;
            }

            int n = receivers.Count;
            int share = bonus / n;
            int remainder = bonus - share * n;

            for (int i = 0; i < n; i++)
            {
                int amount = share;
                if (remainder > 0) { amount++; remainder--; }
                if (amount <= 0) continue;

                pipeline.Resolve(new DamageContext
                {
                    SourceId = owner,
                    TargetId = receivers[i],
                    BaseDamage = amount,
                    Kind = AttackKind.ScriptedAbility,
                    ComboId = string.Empty,
                });
            }
        }
    }
}
