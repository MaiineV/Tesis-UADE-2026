using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Player;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Además del estado, es dueña de los dos hooks del jefe que viven fuera del árbol de AI: la
    /// Represalia de mesa y el corrimiento de la rueda (<c>OnTurnFinished</c> + la casilla en la que
    /// el jugador cerró su turno). El candado de corrimiento es por slot y dura todo el windup: sin
    /// él, cerrar dos turnos dentro del mismo número lo movería dos veces.
    /// </summary>
    public sealed class CroupierWheelService : ICroupierWheelService, IDisposable
    {
        private readonly List<Slot> _slots = new List<Slot>(CroupierSectorTelegraph.MaxSlots);
        private readonly List<int> _detonated = new List<int>(CroupierSectorTelegraph.MaxSlots);

        private Guid _bossGuid;
        private bool _hooked;

        private Action<DamageResolvedPayload> _onDamageResolved;
        private EventManager.EventReceiver _onTurnFinishedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        public int PhaseIndex { get; private set; } = 1;
        public int NumbersPerTurn { get; private set; } = 1;
        public bool Rigged { get; private set; }
        public int RetaliationDamage { get; set; } = 8;

        public bool WindupActive => _slots.Count > 0;

        public IReadOnlyList<int> SungNumbers
        {
            get
            {
                var numbers = new List<int>(_slots.Count);
                foreach (var slot in _slots) numbers.Add(slot.Sector);
                return numbers;
            }
        }

        public IReadOnlyList<int> DetonatedSectors => _detonated;

        public event Action<IReadOnlyList<int>> NumbersChanged;

        public static ICroupierWheelService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<ICroupierWheelService>(out var existing) && existing != null)
                return existing;

            var created = new CroupierWheelService();
            created.RegisterGlobal();
            return created;
        }

        private void RegisterGlobal()
        {
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<ICroupierWheelService>(this, ServiceScope.Global);
            ServiceLocator.AddService<CroupierWheelService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            Unhook();

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

            _slots.Clear();
            _detonated.Clear();
            NumbersChanged = null;
        }

        public void Bind(Guid bossGuid)
        {
            if (bossGuid == Guid.Empty) return;
            if (_hooked && _bossGuid == bossGuid) return;

            // Combate nuevo: con el estado del anterior vivo, el primer cierre de turno correría una
            // rueda que ya no existe.
            if (_hooked && _bossGuid != bossGuid) ClearWindup(notify: true);

            _bossGuid = bossGuid;
            Hook();
        }

        public void SetMode(int numbersPerTurn, bool rigged, int phaseIndex)
        {
            NumbersPerTurn = numbersPerTurn < 1 ? 1 : Math.Min(numbersPerTurn, CroupierSectorTelegraph.MaxSlots);
            Rigged = rigged;
            PhaseIndex = phaseIndex < 1 ? 1 : phaseIndex;
        }

        public void Sing(IReadOnlyList<int> numbers)
        {
            _slots.Clear();
            if (numbers != null)
            {
                for (int i = 0; i < numbers.Count && i < CroupierSectorTelegraph.MaxSlots; i++)
                {
                    int sector = Normalize(numbers[i]);
                    _slots.Add(new Slot { Index = i, Sector = sector, Damage = 0, Kind = AttackKind.BasicAttack });
                }
            }
            NumbersChanged?.Invoke(SungNumbers);
        }

        public void RecordMark(int slot, int damage, AttackKind kind)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Index != slot) continue;

                var entry = _slots[i];
                entry.Damage = damage;
                entry.Kind = kind;
                _slots[i] = entry;
                return;
            }
        }

        public IReadOnlyList<CroupierWheelSlot> ConsumeWindup()
        {
            var snapshot = new List<CroupierWheelSlot>(_slots.Count);
            _detonated.Clear();
            foreach (var slot in _slots)
            {
                snapshot.Add(new CroupierWheelSlot(slot.Index, slot.Sector, slot.Damage, slot.Kind, slot.Nudged));
                _detonated.Add(slot.Sector);
            }

            ClearWindup(notify: true);
            return snapshot;
        }

        public void ClearDetonated() => _detonated.Clear();

        public void Reset()
        {
            Unhook();

            // El windup se limpia ANTES de soltar el guid: apagar los overlays necesita saber de quién eran.
            ClearWindup(notify: true);

            _bossGuid = Guid.Empty;
            PhaseIndex = 1;
            NumbersPerTurn = 1;
            Rigged = false;
            RetaliationDamage = 8;
            _detonated.Clear();
        }

        private void Hook()
        {
            if (_hooked) return;

            if (_onDamageResolved == null) _onDamageResolved = OnDamageResolvedExternal;
            if (_onTurnFinishedHandler == null) _onTurnFinishedHandler = OnTurnFinishedExternal;

            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked) return;

            if (_onDamageResolved != null) TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
            if (_onTurnFinishedHandler != null)
                EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);

            _hooked = false;
        }

        /// <summary>
        /// Cobra <see cref="RetaliationDamage"/> por golpe recibido: no mira el número, ni la fase,
        /// ni el windup. El golpe letal no cobra — la pelea se ganaría y perdería en el mismo cruce.
        /// </summary>
        private void OnDamageResolvedExternal(DamageResolvedPayload payload)
        {
            if (_bossGuid == Guid.Empty || payload.TargetGuid != _bossGuid) return;

            // Sin daño ni escudo consumido no hubo golpe (el pipeline publica el evento de 0 igual).
            if (payload.FinalDamage <= 0 && payload.ShieldAbsorbed <= 0) return;
            if (payload.WasLethal) return;

            Retaliate(payload.SourceGuid);
        }

        /// <summary>Corre sólo el número en cuyo sector está parado el jugador, nunca los dos a la vez.</summary>
        private void OnTurnFinishedExternal(params object[] args)
        {
            if (_bossGuid == Guid.Empty || !WindupActive) return;
            if (Rigged) return;

            if (args == null || args.Length == 0 || !(args[0] is Guid entityGuid)) return;
            if (entityGuid == Guid.Empty || entityGuid != ResolvePlayerGuid()) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!grid.TryGetPosition(entityGuid, out var coord)) return;

            bool moved = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Nudged) continue;
                if (!ThreatAreaShape.ComputeRoomSector(grid, slot.Sector).Contains(coord)) continue;

                slot.Sector = Normalize(slot.Sector + 1);
                slot.Nudged = true;
                _slots[i] = slot;
                moved = true;

                if (slot.Damage > 0)
                    CroupierSectorTelegraph.Mark(_bossGuid, slot.Index, slot.Sector, slot.Damage, slot.Kind);
            }

            if (moved) NumbersChanged?.Invoke(SungNumbers);
        }

        /// <summary>Sólo el jugador corre la rueda: sin <see cref="IPlayerService"/> registrado no hay contra quién comparar.</summary>
        private static Guid ResolvePlayerGuid()
            => ServiceLocator.TryGetService<IPlayerService>(out var player) && player != null
                ? player.PlayerGuid
                : Guid.Empty;

        private void Retaliate(Guid attackerGuid)
        {
            if (attackerGuid == Guid.Empty || RetaliationDamage <= 0) return;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null) return;

            pipeline.Resolve(new DamageContext
            {
                SourceId = _bossGuid,
                TargetId = attackerGuid,
                BaseDamage = RetaliationDamage,
                Kind = AttackKind.Reaction,
            });
        }

        private void ClearWindup(bool notify)
        {
            if (_slots.Count == 0)
            {
                if (notify) NumbersChanged?.Invoke(SungNumbers);
                return;
            }

            foreach (var slot in _slots)
                CroupierSectorTelegraph.ClearOverlay(_bossGuid, slot.Index);

            _slots.Clear();
            if (notify) NumbersChanged?.Invoke(SungNumbers);
        }

        /// <summary>Rueda de 6: el corrimiento de 6 vuelve a 1 (es una rueda, no una escalera).</summary>
        private static int Normalize(int number)
        {
            int count = ThreatAreaShape.RoomSectorCount;
            int wrapped = ((number - 1) % count + count) % count;
            return wrapped + 1;
        }

        private void OnScopeEndedExternal(params object[] args) => Reset();

        private struct Slot
        {
            public int Index;
            public int Sector;
            public int Damage;
            public AttackKind Kind;
            public bool Nudged;
        }
    }
}
