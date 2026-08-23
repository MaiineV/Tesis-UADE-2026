using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// Lee la mano jugada por <c>TypedEvent&lt;ComboPlayedPayload&gt;</c>, el único canal que dispara
    /// una vez por acción confirmada y no en cada preview de hold. Registro Global con reset en
    /// <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </summary>
    public sealed class TahurWagerService : ITahurWagerService, IDisposable
    {
        /// <summary>No es el guid del boss: esa key la usa el overlay del Castigo, y compartirla haría que la mesa y el castigo se pisen.</summary>
        public static readonly Guid TableOverlayGuid = new Guid("7a4c8f10-0000-4000-8000-000000000001");

        private int _chips;
        private int _maxChips = 5;
        private int _chipsFloor;
        private int _payoutPerChip = 12;

        private int _calledRank;
        private string _calledComboId = string.Empty;
        private bool _callInverted;

        private int _rakeChipsPerRound;
        private bool _graceOnNextSettle;

        private HashSet<GridCoord> _tableTiles;
        private string _lastPlayedComboId = string.Empty;
        private Guid _lastPlayedBy;

        private TahurSettleOutcome _lastOutcome = TahurSettleOutcome.None;
        private bool _markedPunishmentThisTurn;

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;
        private Action<ComboPlayedPayload> _onComboPlayedHandler;
        private bool _subscribed;

        public static ITahurWagerService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<ITahurWagerService>(out var existing) && existing != null)
                return existing;

            var created = new TahurWagerService();
            created.Register();
            return created;
        }

        public void Register()
        {
            Subscribe();
            ServiceLocator.AddService<ITahurWagerService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            Unsubscribe();
            _tableTiles?.Clear();
        }

        /// <summary>Hook para tests EditMode — engancha/desengancha sin pasar por <see cref="Register"/>.</summary>
        public void SubscribeForTests() => Subscribe();

        public void UnsubscribeForTests() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed) return;
            _onCombatEndHandler = OnScopeEnded;
            _onRunEndHandler = OnScopeEnded;
            _onComboPlayedHandler = OnComboPlayed;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
            TypedEvent<ComboPlayedPayload>.Subscribe(_onComboPlayedHandler);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
            TypedEvent<ComboPlayedPayload>.Unsubscribe(_onComboPlayedHandler);
            _onCombatEndHandler = null;
            _onRunEndHandler = null;
            _onComboPlayedHandler = null;
            _subscribed = false;
        }

        private void OnScopeEnded(params object[] args) => ResetForNewCombat();

        private void OnComboPlayed(ComboPlayedPayload payload)
        {
            if (string.IsNullOrEmpty(payload.ComboId)) return;
            _lastPlayedComboId = payload.ComboId;
            _lastPlayedBy = payload.SourceGuid;
        }

        public int Chips => _chips;

        public int MaxChips
        {
            get => _maxChips;
            set
            {
                _maxChips = Mathf.Max(1, value);
                SetChips(_chips);
            }
        }

        public int ChipsFloor => _chipsFloor;

        public int PayoutPerChip
        {
            get => _payoutPerChip;
            set => _payoutPerChip = Mathf.Max(0, value);
        }

        public int PendingPayout => _chips * _payoutPerChip;

        public event Action<int> ChipsChanged;

        public int AddChips(int amount)
        {
            SetChips(_chips + amount);
            return _chips;
        }

        public void SetChips(int amount)
        {
            int clamped = Mathf.Clamp(amount, 0, Mathf.Max(1, _maxChips));
            if (clamped == _chips) return;
            _chips = clamped;
            ChipsChanged?.Invoke(_chips);
        }

        public int CalledRank => _calledRank;

        public string CalledComboId => _calledComboId ?? string.Empty;

        public bool CallInverted => _callInverted;

        public int TargetRank
        {
            get
            {
                if (_calledRank <= 0) return 0;
                // LEE: cobra el escalón inferior al cantado. El canto en fase 2 nunca sale rank 1,
                // así que el Max es defensivo.
                return _callInverted ? Mathf.Max(1, _calledRank - 1) : _calledRank;
            }
        }

        public void SetCall(int rank, string comboId)
        {
            _calledRank = Mathf.Max(0, rank);
            _calledComboId = comboId ?? string.Empty;
        }

        public int RakeChipsPerRound
        {
            get => _rakeChipsPerRound;
            set => _rakeChipsPerRound = Mathf.Max(0, value);
        }

        public bool GraceOnNextSettle => _graceOnNextSettle;

        public void FlipCard(int rakeChipsPerRound, int chipsFloor, bool graceNextSettle)
        {
            _callInverted = true;
            _rakeChipsPerRound = Mathf.Max(0, rakeChipsPerRound);
            _chipsFloor = Mathf.Clamp(chipsFloor, 0, Mathf.Max(1, _maxChips));
            _graceOnNextSettle = graceNextSettle;

            // El piso no se aplica acá: el volteo no regala fichas, el piso sólo entra al cobrar.
        }

        public bool ConsumeGrace()
        {
            if (!_graceOnNextSettle) return false;
            _graceOnNextSettle = false;
            return true;
        }

        private HashSet<GridCoord> Table => _tableTiles ??= new HashSet<GridCoord>();

        public IReadOnlyCollection<GridCoord> TableTiles => Table;

        public bool IsOnTable(GridCoord coord) => Table.Contains(coord);

        public void SetTable(IEnumerable<GridCoord> tiles)
        {
            Table.Clear();
            if (tiles == null) return;
            foreach (var tile in tiles) Table.Add(tile);
        }

        public void ClearTable() => Table.Clear();

        public string LastPlayedComboId => _lastPlayedComboId ?? string.Empty;

        public Guid LastPlayedBy => _lastPlayedBy;

        public string ConsumePlayedHand()
        {
            var played = _lastPlayedComboId ?? string.Empty;
            _lastPlayedComboId = string.Empty;
            _lastPlayedBy = Guid.Empty;
            return played;
        }

        public TahurSettleOutcome LastOutcome => _lastOutcome;

        public bool MarkedPunishmentThisTurn => _markedPunishmentThisTurn;

        public void BeginBossTurn()
        {
            _markedPunishmentThisTurn = false;
            _lastOutcome = TahurSettleOutcome.None;
        }

        public void ReportOutcome(TahurSettleOutcome outcome, bool markedPunishment)
        {
            _lastOutcome = outcome;
            _markedPunishmentThisTurn = markedPunishment;
        }

        public void ResetForNewCombat()
        {
            SetChips(0);
            _chipsFloor = 0;
            _calledRank = 0;
            _calledComboId = string.Empty;
            _callInverted = false;
            _rakeChipsPerRound = 0;
            _graceOnNextSettle = false;
            _lastPlayedComboId = string.Empty;
            _lastPlayedBy = Guid.Empty;
            _lastOutcome = TahurSettleOutcome.None;
            _markedPunishmentThisTurn = false;
            ClearTable();

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(TableOverlayGuid);
        }
    }
}
