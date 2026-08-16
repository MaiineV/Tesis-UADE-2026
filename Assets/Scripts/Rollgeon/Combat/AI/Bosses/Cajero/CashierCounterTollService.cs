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
    /// <b>Ventana ciega del primer turno.</b> El árbol del jefe arma el peaje recién en su primer
    /// tick, y la cola es player-first: si el jugador cruza el mostrador en su turno de apertura,
    /// ese cierre de turno sale gratis. Es un turno y se paga una sola vez por pelea; la
    /// alternativa era que el servicio adivinara solo quién es el Cajero y dónde está su mostrador.
    /// </para>
    /// <para>
    /// <b>Lee posiciones vivas, no la foto del armado.</b> El lado se resuelve con las coordenadas
    /// que tienen los dos en el momento del cobro. Así el peaje sigue al jefe si el kiteo lo mete
    /// por una abertura, y se apaga solo cuando el jefe muere: <c>CombatDeathWatcher</c> lo saca de
    /// la grilla y sin coordenada del jefe no hay lado que compartir.
    /// </para>
    /// <para>
    /// <b>Tests.</b> Queda suscripto a <c>EventManager</c>, que <c>ServiceLocator.Clear()</c> no
    /// desengancha. Un fixture que lo cree (o que tickee el árbol del Cajero, que lo crea solo)
    /// debe llamar <see cref="Dispose"/> en el teardown o el peaje sigue cobrando en el fixture
    /// siguiente.
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
        public Guid BossGuid => _bossGuid;

        /// <inheritdoc />
        public void Arm(Guid bossGuid, Guid payerGuid, int counterRow, int tollDamage)
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
        }

        /// <inheritdoc />
        public void Disarm()
        {
            _bossGuid = Guid.Empty;
            _payerGuid = Guid.Empty;
            _counterRow = 0;
            _tollDamage = 0;
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
        /// <b>Va acá y no en <c>AINode_CashierCounterToll</c>.</b> El nodo sólo arma, y re-arma todos
        /// los turnos: animarlo pondría un golpe en pantalla en turnos donde no se cobró nada y lo
        /// dejaría mudo justo en el turno en que sí se cobra, que es al cerrar el del jugador —
        /// después de que el árbol del jefe ya tickeó.
        /// </para>
        /// <para>
        /// <b>No bloquea el turno.</b> El cobro cae en <c>OnTurnFinished</c>, fuera de toda coroutine
        /// que pueda esperarlo (el gate de <c>TurnManager</c> hoy sólo lo miran <c>EffectData</c> y
        /// <c>AINode_ExecuteTelegraph</c>), así que un <c>BeginFeedbackWait</c> acá subiría el depth
        /// sin que nadie lo espere. Y aunque hubiera quién: un peaje pasivo que frena la pelea un
        /// segundo cada vez que el jugador cierra su turno cerca del mostrador se vuelve un impuesto
        /// al ritmo, no una lectura.
        /// </para>
        /// <para>
        /// <b>Todos los steps arrancan juntos</b>, sin colgarse del Animation Event de impacto: el
        /// daño ya cayó (el número flotante está en pantalla), así que el chispazo tiene que ir ahí y
        /// no 0.4s después. Además la secuencia del turno del jefe arranca pisando
        /// <c>FeedbackSequenceRuntime.Current</c>, y un step esperando <c>"hit"</c> en el bus viejo no
        /// se destrabaría nunca — lo levantaría el watchdog, tarde y con warning.
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
