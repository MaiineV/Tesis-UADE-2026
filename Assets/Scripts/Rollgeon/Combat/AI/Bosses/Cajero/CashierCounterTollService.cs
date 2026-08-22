using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Feedback;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Implementación de <see cref="ICashierCounterTollService"/>. POCO suscripto a
    /// <see cref="EventName.OnTurnFinished"/>, sin MonoBehaviour ni bootstrap: se auto-registra vía
    /// <see cref="ResolveOrCreate"/> el primer turno del jefe, igual que
    /// <see cref="CashierLedgerService"/>.
    /// </summary>
    /// <remarks>
    /// Lee posiciones vivas y no la foto del armado, así el peaje sigue al jefe si el kiteo lo mueve
    /// y se apaga solo cuando muere. Queda suscripto a <c>EventManager</c>, que
    /// <c>ServiceLocator.Clear()</c> no desengancha: el fixture que lo cree debe llamar
    /// <see cref="Dispose"/> en el teardown o el peaje sigue cobrando en el fixture siguiente.
    /// </remarks>
    public sealed class CashierCounterTollService : ICashierCounterTollService, IDisposable
    {
        /// <summary>
        /// Clasificación del daño del peaje: <see cref="AttackKind.Environmental"/> porque lo cobra
        /// el mostrador y no el jefe, así que se cobra incluso con el jefe aturdido.
        /// </summary>
        public const AttackKind TollKind = AttackKind.Environmental;

        private Guid _bossGuid;
        private Guid _payerGuid;
        private int _counterRow;
        private int _tollDamage;
        private int _chargesEveryNRounds = 1;

        private EventManager.EventReceiver _onTurnFinished;
        private EventManager.EventReceiver _onScopeEnded;
        private bool _disposed;

        public CashierCounterTollService()
        {
            _onTurnFinished = OnTurnFinishedExternal;
            _onScopeEnded = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinished);
            EventManager.Subscribe(EventName.OnCombatEnd, _onScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, _onScopeEnded);
        }

        /// <summary>Devuelve el servicio registrado o crea y registra uno nuevo (Global).</summary>
        public static ICashierCounterTollService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<ICashierCounterTollService>(out var existing) && existing != null)
                return existing;

            var created = new CashierCounterTollService();
            ServiceLocator.AddService<ICashierCounterTollService>(created, ServiceScope.Global);

            // El overlay nace con el peaje: es lo único que lo dibuja.
            CashierCounterTollOverlay.ResolveOrCreate();
            return created;
        }

        // ======================================================================
        // ICashierCounterTollService
        // ======================================================================

        /// <inheritdoc />
        public int TollDamage => _tollDamage;

        /// <inheritdoc />
        public int CounterRow => _counterRow;

        /// <inheritdoc />
        public bool IsArmed => _bossGuid != Guid.Empty && _payerGuid != Guid.Empty && _tollDamage > 0;

        /// <inheritdoc />
        public int ChargesEveryNRounds => _chargesEveryNRounds;

        /// <inheritdoc />
        public bool ChargesThisRound => IsArmed && IsChargingRound();

        /// <inheritdoc />
        public Guid BossGuid => _bossGuid;

        /// <inheritdoc />
        public void Arm(Guid bossGuid, Guid payerGuid, int counterRow, int tollDamage,
                        int chargesEveryNRounds = 1)
        {
            if (bossGuid == Guid.Empty || payerGuid == Guid.Empty || tollDamage <= 0)
            {
                Disarm();
                return;
            }

            _bossGuid = bossGuid;
            _payerGuid = payerGuid;
            _counterRow = counterRow;
            _tollDamage = tollDamage;
            _chargesEveryNRounds = chargesEveryNRounds < 1 ? 1 : chargesEveryNRounds;
        }

        /// <inheritdoc />
        public void Disarm()
        {
            _bossGuid = Guid.Empty;
            _payerGuid = Guid.Empty;
            _counterRow = 0;
            _tollDamage = 0;
            _chargesEveryNRounds = 1;
        }

        /// <summary>
        /// Si la ronda en curso es de las que cobran. Se lee de
        /// <see cref="TurnOrderService.RoundIndex"/> en el momento del cobro y no de un contador
        /// propio: el resume mid-combate restaura el <c>RoundIndex</c> por su cuenta, y un contador
        /// local quedaría desfasado justo después de cargar una partida.
        /// </summary>
        /// <remarks>
        /// El <c>RoundIndex</c> es 0-based y el jugador abre cada ronda, así que su ronda N tiene
        /// índice N-1 — de ahí el <c>+1</c>. Sin <see cref="TurnOrderService"/> registrado cobra,
        /// no perdona.
        /// </remarks>
        private bool IsChargingRound()
        {
            if (_chargesEveryNRounds <= 1) return true;
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return true;

            int round = turnOrder.RoundIndex + 1;
            return round % _chargesEveryNRounds == 0;
        }

        /// <summary>
        /// Si <paramref name="row"/> y <paramref name="otherRow"/> caen del mismo lado de
        /// <paramref name="counterRow"/>. La fila del mostrador no cuenta como lado.
        /// </summary>
        public static bool IsSameSide(int row, int otherRow, int counterRow)
        {
            int side = Math.Sign(row - counterRow);
            int otherSide = Math.Sign(otherRow - counterRow);
            return side != 0 && side == otherSide;
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onTurnFinished != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinished);
                _onTurnFinished = null;
            }
            if (_onScopeEnded != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onScopeEnded);
                EventManager.UnSubscribe(EventName.OnRunEnd, _onScopeEnded);
                _onScopeEnded = null;
            }
            Disarm();
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnTurnFinishedExternal(params object[] args)
        {
            if (!IsArmed) return;
            if (args == null || args.Length == 0) return;
            if (!(args[0] is Guid entityGuid) || entityGuid != _payerGuid) return;

            if (!IsChargingRound()) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!grid.TryGetPosition(_bossGuid, out var bossCoord)) return;
            if (!grid.TryGetPosition(entityGuid, out var payerCoord)) return;
            if (!IsSameSide(payerCoord.Y, bossCoord.Y, _counterRow)) return;

            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null) return;

            pipeline.Resolve(new DamageContext
            {
                SourceId = _bossGuid,
                TargetId = entityGuid,
                BaseDamage = _tollDamage,
                Kind = TollKind,
            });

            PlayTollFeedback(entityGuid);
        }

        private void OnScopeEndedExternal(params object[] args) => Disarm();

        // ======================================================================
        // Presentación
        // ======================================================================

        /// <summary>Manotazo del Cajero + impacto sobre el que pagó, en el momento del cobro.</summary>
        /// <remarks>
        /// No bloquea el turno: el cobro cae en <c>OnTurnFinished</c>, fuera de toda coroutine que
        /// pueda esperarlo, así que un <c>BeginFeedbackWait</c> acá subiría el depth sin que nadie lo
        /// baje. Todos los steps arrancan juntos: un step esperando el Animation Event <c>"hit"</c>
        /// no se destrabaría hasta el watchdog.
        /// </remarks>
        private void PlayTollFeedback(Guid payerGuid)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) return;

            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep>
                {
                    Step(BossFeedbackIds.CajeroMeleeAnim),
                    Step(BossFeedbackIds.CajeroImpactVfx),
                    Step(BossFeedbackIds.CajeroImpactFeel),
                },
                SourceGuid = _bossGuid,
                TargetGuid = payerGuid,
            }, null);
        }

        private static FeedbackSequenceStep Step(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.Immediate,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };
    }
}
