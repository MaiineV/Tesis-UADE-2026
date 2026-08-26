using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Feedback;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Lee posiciones vivas y no la foto del armado, así el peaje sigue al jefe si el kiteo lo mueve.
    /// Queda suscripto a <c>EventManager</c>, que <c>ServiceLocator.Clear()</c> no desengancha: el
    /// fixture que lo cree debe llamar <see cref="Dispose"/> o sigue cobrando en el siguiente.
    /// </summary>
    public sealed class CashierCounterTollService : ICashierCounterTollService, IDisposable
    {
        /// <summary>Lo cobra el mostrador y no el jefe, así que se cobra incluso con el jefe aturdido.</summary>
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

        public int TollDamage => _tollDamage;

        public int CounterRow => _counterRow;

        public bool IsArmed => _bossGuid != Guid.Empty && _payerGuid != Guid.Empty && _tollDamage > 0;

        public int ChargesEveryNRounds => _chargesEveryNRounds;

        public bool ChargesThisRound => IsArmed && IsChargingRound();

        public Guid BossGuid => _bossGuid;

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

        public void Disarm()
        {
            _bossGuid = Guid.Empty;
            _payerGuid = Guid.Empty;
            _counterRow = 0;
            _tollDamage = 0;
            _chargesEveryNRounds = 1;
        }

        /// <summary>
        /// Se lee de <see cref="TurnOrderService.RoundIndex"/> en el momento del cobro y no de un
        /// contador propio: el resume mid-combate lo restaura por su cuenta. Es 0-based y el jugador
        /// abre cada ronda, así que su ronda N tiene índice N-1 — de ahí el <c>+1</c>.
        /// </summary>
        private bool IsChargingRound()
        {
            if (_chargesEveryNRounds <= 1) return true;
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return true;

            int round = turnOrder.RoundIndex + 1;
            return round % _chargesEveryNRounds == 0;
        }

        /// <summary>La fila del mostrador no cuenta como lado.</summary>
        public static bool IsSameSide(int row, int otherRow, int counterRow)
        {
            int side = Math.Sign(row - counterRow);
            int otherSide = Math.Sign(otherRow - counterRow);
            return side != 0 && side == otherSide;
        }

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

        /// <summary>
        /// No bloquea el turno: el cobro cae en <c>OnTurnFinished</c>, fuera de toda coroutine que
        /// pueda esperarlo, así que un <c>BeginFeedbackWait</c> acá subiría el depth sin que nadie lo
        /// baje. Los steps arrancan juntos: uno esperando el Animation Event colgaría al watchdog.
        /// </summary>
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
