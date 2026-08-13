using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Implementación de <see cref="ICroupierWheelService"/>. Además del estado, es dueña del hook de
    /// daño del jefe: el corrimiento de la rueda y la Represalia de mesa son <b>el mismo evento</b>
    /// (pegarle con el número en el aire), así que viven juntos en un único suscriptor a
    /// <c>TypedEvent&lt;DamageResolvedPayload&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un corrimiento por número.</b> El candado es por slot y dura todo el windup: sin él, el
    /// segundo golpe del turno movería la rueda dos veces y cobraría dos veces, y la lectura
    /// "primero N+1, después decido" dejaría de ser verdad.
    /// </para>
    /// <para>
    /// <b>La paridad se lee antes del corrimiento.</b> Lo que el jugador ve cuando decide pegar es el
    /// número en el aire, no el que va a quedar: si canta 3 y pegás, pagás los 8 y la rueda pasa a 4.
    /// Cobrar por el número resultante haría que el precio depende de información que todavía no
    /// existe cuando se toma la decisión.
    /// </para>
    /// <para>
    /// <b>El corrimiento mueve la marca.</b> Correr la rueda re-marca el área del slot en el sector
    /// nuevo (y repinta el overlay): si el área quedara donde estaba, la palanca no cambiaría nada de
    /// lo que va a pasar y el jugador no podría ver a dónde la mandó.
    /// </para>
    /// </remarks>
    public sealed class CroupierWheelService : ICroupierWheelService, IDisposable
    {
        private readonly List<Slot> _slots = new List<Slot>(CroupierSectorTelegraph.MaxSlots);
        private readonly List<int> _detonated = new List<int>(CroupierSectorTelegraph.MaxSlots);

        private Guid _bossGuid;
        private bool _hooked;

        private Action<DamageResolvedPayload> _onDamageResolved;
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

        /// <summary>
        /// Devuelve el servicio registrado o crea + registra uno (Global). Lazy, igual que
        /// <see cref="ThreatTelegraphOverlay.ResolveOrCreate"/>: el jefe entra por un asset de datos y
        /// no puede pedirle al usuario que agregue un bootstrap a mano para que su mecánica exista.
        /// </summary>
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

        // ======================================================================
        // ICroupierWheelService
        // ======================================================================

        public void Bind(Guid bossGuid)
        {
            if (bossGuid == Guid.Empty) return;
            if (_hooked && _bossGuid == bossGuid) return;

            // Combate nuevo (o instancia nueva del mismo jefe): el estado del anterior no puede
            // sobrevivir, o el primer golpe de esta pelea correría una rueda que ya no existe.
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
            _bossGuid = Guid.Empty;
            PhaseIndex = 1;
            NumbersPerTurn = 1;
            Rigged = false;
            RetaliationDamage = 8;
            _detonated.Clear();
            ClearWindup(notify: true);
        }

        // ======================================================================
        // Hook de daño — corrimiento + Represalia
        // ======================================================================

        private void Hook()
        {
            if (_hooked) return;

            if (_onDamageResolved == null) _onDamageResolved = OnDamageResolvedExternal;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked || _onDamageResolved == null) return;

            TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
            _hooked = false;
        }

        private void OnDamageResolvedExternal(DamageResolvedPayload payload)
        {
            if (_bossGuid == Guid.Empty || payload.TargetGuid != _bossGuid) return;
            if (Rigged || !WindupActive) return;

            // Un golpe que no llegó a la mesa no toca la palanca: sin daño ni escudo consumido no
            // hubo golpe (un evento de 0 lo publica igual el pipeline).
            if (payload.FinalDamage <= 0 && payload.ShieldAbsorbed <= 0) return;

            // La Represalia se cobra una vez por golpe, no una por número: con dos números en el aire
            // la rueda está trucada de todos modos, pero si algún día no lo estuviera, un solo golpe no
            // debería cobrar dos veces.
            bool chargeRetaliation = false;
            bool moved = false;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Nudged) continue;

                if (slot.Sector % 2 != 0) chargeRetaliation = true;

                slot.Sector = Normalize(slot.Sector + 1);
                slot.Nudged = true;
                _slots[i] = slot;
                moved = true;

                if (slot.Damage > 0)
                    CroupierSectorTelegraph.Mark(_bossGuid, slot.Index, slot.Sector, slot.Damage, slot.Kind);
            }

            if (!moved) return;

            NumbersChanged?.Invoke(SungNumbers);
            if (chargeRetaliation) Retaliate(payload.SourceGuid);
        }

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

        // ======================================================================
        // Internals
        // ======================================================================

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

        /// <summary>Estado mutable de un número en el aire. La vista pública es <see cref="CroupierWheelSlot"/>.</summary>
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
