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
    /// <para>
    /// <b>Ventana ciega del primer turno:</b> el árbol arma el peaje recién en su primer tick y la
    /// cola es player-first, así que cruzar el mostrador en la apertura sale gratis. Una vez por
    /// pelea; la alternativa era que el servicio adivinara solo dónde está el mostrador.
    /// </para>
    /// <para>
    /// Lee posiciones vivas y no la foto del armado, así el peaje sigue al jefe si el kiteo lo mueve
    /// y se apaga solo cuando muere.
    /// </para>
    /// <para>
    /// <b>Tests:</b> queda suscripto a <c>EventManager</c>, que <c>ServiceLocator.Clear()</c> no
    /// desengancha. El fixture que lo cree debe llamar <see cref="Dispose"/> en el teardown o el
    /// peaje sigue cobrando en el fixture siguiente.
    /// </para>
    /// </remarks>
    public sealed class CashierCounterTollService : ICashierCounterTollService, IDisposable
    {
        /// <summary>
        /// Clasificación del daño del peaje. Es <see cref="AttackKind.Environmental"/> y no un
        /// ataque suyo porque no lo tira él: lo cobra el mostrador por quedarte de su lado, como
        /// una trampa de piso — el jefe puede estar aturdido y el peaje se cobra igual.
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

        /// <summary>
        /// Devuelve el servicio registrado o crea y registra uno nuevo (Global). Lo llama el nodo
        /// del peaje — un sistema que sólo quiera leer el estado debería usar <c>TryGetService</c>.
        /// </summary>
        public static ICashierCounterTollService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<ICashierCounterTollService>(out var existing) && existing != null)
                return existing;

            var created = new CashierCounterTollService();
            ServiceLocator.AddService<ICashierCounterTollService>(created, ServiceScope.Global);

            // El overlay nace con el peaje y no por su cuenta: es lo único que lo dibuja, y crearlo
            // acá garantiza que exista exactamente cuando hay un peaje que anunciar.
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
        /// <para>
        /// El <c>RoundIndex</c> es 0-based y el jugador abre cada ronda (CNF-006), así que su ronda
        /// N tiene índice N-1 — la misma conversión que documenta
        /// <c>ForcedRerollCapabilityService</c>. Con cadencia 2 eso deja la ronda 1 franca, que es
        /// la que el peaje ya regalaba de todos modos por la ventana ciega del primer turno: la
        /// intermitencia no agrega un caso raro al arranque.
        /// </para>
        /// <para>
        /// Sin <see cref="TurnOrderService"/> registrado cobra, no perdona. Es el mismo criterio
        /// permisivo de <c>PcRoundNumber</c> ("no sabemos la ronda ⇒ no vetamos") y falla del lado
        /// del comportamiento viejo: un peaje que se apaga solo porque falta un servicio se
        /// diagnostica mucho peor que uno que cobra siempre.
        /// </para>
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
        /// <paramref name="counterRow"/>. La fila del mostrador no es lado: parado en una abertura
        /// estás en la puerta, y la ficha cobra por comprometerte con un lado, no por asomarte.
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

            // La ronda franca se chequea ANTES de la geometría: es la ronda en la que el jugador
            // tiene permitido plantarse del lado de él, así que estar ahí no es información que el
            // peaje necesite mirar.
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

        /// <summary>
        /// Manotazo del Cajero + impacto sobre el que pagó, al cobrar. Sin esto los 10 salen como un
        /// número flotante huérfano al cerrar el turno y el peaje se lee como daño aleatorio de la
        /// sala en vez de como el precio de quedarse de su lado.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Va acá y no en <c>AINode_CashierCounterToll</c>: el nodo re-arma todos los turnos, así que
        /// animarlo ahí pondría un golpe en turnos sin cobro y quedaría mudo justo en el turno en
        /// que sí se cobra.
        /// </para>
        /// <para>
        /// <b>No bloquea el turno.</b> El cobro cae en <c>OnTurnFinished</c>, fuera de toda coroutine
        /// que pueda esperarlo, así que un <c>BeginFeedbackWait</c> acá subiría el depth sin que
        /// nadie lo baje.
        /// </para>
        /// <para>
        /// Todos los steps arrancan juntos, sin colgarse del Animation Event de impacto: el daño ya
        /// cayó, y un step esperando <c>"hit"</c> no se destrabaría hasta el watchdog.
        /// </para>
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
