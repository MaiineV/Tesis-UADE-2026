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
    /// Implementación de <see cref="ICroupierWheelService"/>. Además del estado, es dueña de los dos
    /// hooks del jefe que viven fuera del árbol de AI: la Represalia de mesa
    /// (<c>TypedEvent&lt;DamageResolvedPayload&gt;</c>) y el corrimiento de la rueda
    /// (<c>OnTurnFinished</c> + la casilla en la que el jugador cerró su turno).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Cobrar y correr son dos cosas separadas.</b> Los disparaba el mismo golpe, y eso hacía que
    /// mover el número fuera un efecto secundario gratis del único ataque que el jugador tiene:
    /// atacar era siempre, además, correr la rueda. Ahora la palanca se paga con el cuerpo (terminar
    /// el turno dentro del sector cantado) y el 8 se paga por pegar. Ninguna de las dos cosas puede
    /// hacerse "de paso" mientras se hace la otra.
    /// </para>
    /// <para>
    /// <b>Mover el hacha es pararse bajo el hacha.</b> El corrimiento pide cerrar el turno
    /// <i>adentro</i> del bloque que va a caer: el jugador que quiere redirigir el número tiene que
    /// aceptar el riesgo de que no lo consiga (aturdido, empujado, o simplemente equivocado de
    /// sector). Pedirlo desde afuera lo volvería un botón gratis.
    /// </para>
    /// <para>
    /// <b>Un corrimiento por número.</b> El candado es por slot y dura todo el windup: sin él, un
    /// jugador que cierre dos turnos dentro del mismo número lo movería dos veces, y la lectura
    /// "primero N+1, después decido" dejaría de ser verdad.
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
            // sobrevivir, o el primer cierre de turno de esta pelea correría una rueda que ya no existe.
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

            // El windup se limpia ANTES de soltar el guid: apagar los overlays de los slots necesita
            // saber de quién eran.
            ClearWindup(notify: true);

            _bossGuid = Guid.Empty;
            PhaseIndex = 1;
            NumbersPerTurn = 1;
            Rigged = false;
            RetaliationDamage = 8;
            _detonated.Clear();
        }

        // ======================================================================
        // Hooks fuera del árbol — Represalia (daño) y corrimiento (posición)
        // ======================================================================

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
        /// Pegarle cuesta 8. No mira el número, ni la fase, ni si hay windup abierto: es el precio de
        /// la casilla de melee, y para pegarle hay que ocuparla.
        /// </summary>
        /// <remarks>
        /// Se cobra por golpe y no por turno: cada impacto es una decisión aparte, y un jugador que
        /// elige pegar tres veces está eligiendo pagar tres veces. El único cobro que no ocurre es el
        /// del golpe que lo mata — un crupier muerto no manotea, y sin esa salvedad la pelea se puede
        /// ganar y perder en el mismo intercambio.
        /// </remarks>
        private void OnDamageResolvedExternal(DamageResolvedPayload payload)
        {
            if (_bossGuid == Guid.Empty || payload.TargetGuid != _bossGuid) return;

            // Un golpe que no llegó a la mesa no se cobra: sin daño ni escudo consumido no hubo golpe
            // (un evento de 0 lo publica igual el pipeline).
            if (payload.FinalDamage <= 0 && payload.ShieldAbsorbed <= 0) return;
            if (payload.WasLethal) return;

            Retaliate(payload.SourceGuid);
        }

        // ======================================================================
        // Hook de posición — el corrimiento de la rueda
        // ======================================================================

        /// <summary>
        /// El jugador que cierra su turno dentro de un sector cantado lo corre un lugar. Mismo patrón
        /// que <c>HazardService</c>: <c>OnTurnFinished</c> + la casilla que reporta el grid.
        /// </summary>
        /// <remarks>
        /// Corre <b>sólo</b> el número en cuyo sector está parado. En fase 2 la rueda está trucada y
        /// no corre ninguno, pero el criterio por slot es el que hace que la costura no mueva los dos
        /// a la vez si alguna vez se destruca.
        /// </remarks>
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

        /// <summary>
        /// Sólo el jugador corre la rueda. Mismo resolver que <c>DiceBlockService</c>: sin
        /// <see cref="IPlayerService"/> registrado no hay contra quién comparar y no se corre nada.
        /// </summary>
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
