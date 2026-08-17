using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Combat.Rooms;
using Rollgeon.DevConsole.UI;
using Rollgeon.Dice;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using UnityEngine;

namespace Rollgeon.EditorTools.Playtest
{
    public enum BotRunState
    {
        Booting,
        Fighting,
        Done,
        Failed,
    }

    /// <summary>
    /// Juega una pelea contra un jefe y deja un PNG por turno.
    /// </summary>
    /// <remarks>
    /// <b>Por qué existe.</b> Lo que hay que validar de los jefes sólo se ve en pantalla: que el
    /// overlay del mostrador se apague en la ronda franca, que caigan 2 monedas, que los dados
    /// nazcan pegados a La Generala, que el "-70%" aparezca y suba al romper uno. Ningún test
    /// EditMode puede afirmar eso.
    ///
    /// <b>Cómo maneja el turno.</b> No sigue un guion fijo. Los flags del turno del jugador
    /// (<c>_awaitingFirstRoll</c>, <c>ThrowBusy</c>) son privados de <c>CombatHandoffService</c>,
    /// así que el bot hace lo mismo que un jugador que no lee código: aprieta los botones que
    /// tenga sentido apretar y mira qué pasa. Los handlers del HUD ya son no-ops cuando no
    /// corresponden, y <c>OnBehaviorExecuted</c> avisa cuando la acción efectivamente ocurrió.
    /// Eso lo hace inmune al orden interno (behavior → target → roll → confirm) y a que un jefe
    /// meta un chain en el medio.
    ///
    /// <b>Lo que NO hace.</b> No toca gameplay. Sólo consume seams públicos: los <c>Action</c> de
    /// <see cref="CombatHUDView"/> (que es lo que invoca un click), <see cref="ISelectionController"/>,
    /// y los comandos de la DevConsole reales.
    /// </remarks>
    public sealed class BossBotDriver
    {
        private const string LogPrefix = "[BossBot] ";

        /// <summary>Techo por espera. Generoso: una animación de jefe encadenada puede tardar.</summary>
        private const float StepTimeoutSeconds = 25f;

        /// <summary>Techo para que arranque el combate desde que se pide el teleport.</summary>
        private const float BootTimeoutSeconds = 120f;

        /// <summary>
        /// Cuánto esperar a que aterricen feedbacks antes de capturar. En tiempo real, no de juego:
        /// con <c>timeScale</c> alto un WaitForSeconds normal capturaría a mitad de una animación.
        /// </summary>
        private const float SettleSeconds = 0.45f;

        /// <summary>
        /// Turnos sin acortar distancia antes de recurrir al <c>tp</c>. Dos y no uno: el primero
        /// puede ser el jefe reposicionándose de casualidad, y se pierde la lectura de un
        /// acercamiento real —que para el peaje del Cajero es justo lo que hay que ver.
        /// </summary>
        private const int TurnsOutOfRangeBeforeTeleport = 2;

        public static BossBotDriver Active { get; private set; }

        public BotRunState State { get; private set; } = BotRunState.Booting;
        public string Failure { get; private set; }
        public int TurnsPlayed { get; private set; }
        public string OutputDir { get; private set; }
        public int ShotsTaken { get; private set; }

        private BossBotArgs _args;
        private readonly StringBuilder _log = new StringBuilder();
        private string _logPath;

        private Guid _playerGuid;
        private Guid _bossGuid;

        private DevConsoleSession _console;
        private int _consoleLogCursor;

        private int _turnsOutOfRange;

        // Última reducción vista en un DamageResolvedPayload sobre el jefe. Es el cross-check del
        // "-70%" de la imagen: si el PNG lo muestra y el log dice 0.30, coinciden.
        private float _lastIncomingMultiplier = 1f;
        private int _lastDamageToBoss;
        private bool _behaviorExecuted;

        private EventManager.EventReceiver _onBehaviorExecuted;
        private Action<DamageResolvedPayload> _onDamageResolved;
        private Action<BossEncounterStartedPayload> _onBossEncounter;
        private Guid _announcedBossGuid;

        /// <summary>
        /// Pila de coroutines en curso. El bot no es un MonoBehaviour —Unity se niega a hacer
        /// <c>AddComponent</c> de un script que vive en una assembly de <c>Editor/</c>— así que
        /// <see cref="BossBotRunner"/> lo bombea desde <c>EditorApplication.update</c> y esta pila
        /// hace de scheduler: es lo que da sentido a los <c>yield return OtraCoroutine()</c>.
        /// </summary>
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();

        public static BossBotDriver Create(BossBotArgs args, string outputDir)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            var driver = new BossBotDriver { _args = args, OutputDir = outputDir };
            Active = driver;
            return driver;
        }

        public void Begin()
        {
            Directory.CreateDirectory(OutputDir);
            _logPath = Path.Combine(OutputDir, "turns.log");

            // Sin esto la corrida se congela cuando la ventana pierde foco, y el .ps1 espera
            // para siempre a un Unity que no avanza.
            Application.runInBackground = true;

            Line($"boss bot — {_args}");
            _stack.Push(RunAll());
        }

        /// <summary>Avanza un tick. <c>false</c> = la corrida terminó y no hay más que bombear.</summary>
        public bool Pump()
        {
            while (_stack.Count > 0)
            {
                var top = _stack.Peek();
                bool moved;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception ex)
                {
                    // Una excepción sin atrapar dejaría la corrida colgada hasta el watchdog, sin
                    // decir qué pasó. Mejor fallar acá con el stack trace en el log.
                    _stack.Clear();
                    Fail($"excepción en la corrida: {ex}");
                    return false;
                }

                if (!moved)
                {
                    _stack.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    _stack.Push(nested);
                    continue;
                }

                return true;
            }
            return false;
        }

        public void Dispose()
        {
            Unsubscribe();
            Time.timeScale = 1f;
            if (Active == this) Active = null;
        }

        // ---- Corrida ---------------------------------------------------------

        private IEnumerator RunAll()
        {
            yield return Run();

            Flush();
            if (State == BotRunState.Booting || State == BotRunState.Fighting)
                State = BotRunState.Done;

            Line($"fin — estado={State} turnos={TurnsPlayed} shots={ShotsTaken}" +
                 (string.IsNullOrEmpty(Failure) ? string.Empty : $" motivo={Failure}"));
            Flush();
        }

        private IEnumerator Run()
        {
            // 1. La run ya viene arrancada: BootstrapRunOverride hizo que 00_Bootstrap aplicara
            //    el hero y saltara directo a 02_Gameplay (mismo camino que el Scene Switcher).
            yield return WaitUntil(() =>
                    ServiceLocator.TryGetService<IPlayerService>(out var ps)
                    && ps != null && ps.PlayerGuid != Guid.Empty
                    && ServiceLocator.TryGetService<IGridManager>(out var g) && g != null,
                BootTimeoutSeconds, "que arranque la run");
            if (State == BotRunState.Failed) yield break;

            ServiceLocator.TryGetService<IPlayerService>(out var player);
            _playerGuid = player.PlayerGuid;
            Line($"run viva — player={_playerGuid} hero={player.CurrentHero?.EntityId}");

            Subscribe();
            Time.timeScale = _args.TimeScale;

            if (_args.GodMode) RunCommand("god", "on");
            if (_args.InfiniteEnergy) RunCommand("energy", "inf");

            // 2. Teleport a la sala del jefe por el comando real, así su error (id desconocido,
            //    sala sin boss) queda en el log tal cual lo vería un humano en la consola.
            if (!RunCommand("boss", _args.BossId))
            {
                Fail($"el comando 'boss {_args.BossId}' falló");
                yield break;
            }

            // 3. Esperar el combate: HUD de combate arriba y cola de turnos armada.
            yield return WaitUntil(() => CombatHud != null && TurnOrder != null && TurnOrder.ParticipantCount > 0,
                BootTimeoutSeconds, "que arranque el combate");
            if (State == BotRunState.Failed) yield break;

            if (!TryIdentifyBoss())
            {
                Fail("no pude identificar al jefe en la cola de turnos");
                yield break;
            }

            State = BotRunState.Fighting;
            Line($"combate — jefe={_bossGuid} hp={HealthOf(_bossGuid)} participantes={TurnOrder.ParticipantCount}");

            // 4. El loop.
            while (TurnsPlayed < _args.Turns)
            {
                // La muerte entra en la condición de espera, no sólo en el chequeo de abajo: si el
                // jefe mata al bot, su turno no vuelve nunca y esperarlo terminaba en un timeout
                // duro — una corrida que murió peleando se reportaba como una corrida fallida.
                yield return WaitUntil(
                    () => IsPlayerTurn() || IsDead(_playerGuid) || IsDead(_bossGuid),
                    StepTimeoutSeconds, "el turno del player");
                if (State == BotRunState.Failed) yield break;

                if (IsDead(_playerGuid)) { Line("el player murió — corte"); yield break; }
                if (IsDead(_bossGuid)) { Line("el jefe murió — corte"); yield break; }

                int turn = ++TurnsPlayed;
                yield return PlayTurn(turn);
                if (State == BotRunState.Failed) yield break;
            }

            Line($"presupuesto de turnos agotado ({_args.Turns})");
        }

        private IEnumerator PlayTurn(int turn)
        {
            int round = TurnOrder != null ? TurnOrder.RoundIndex + 1 : -1;
            yield return Capture($"turn_{turn:D2}_a_start");

            TeleportIfChasingForever();

            var decision = Decide();
            Line($"T{turn:D2} ronda={round} {Snapshot()} → {decision.Kind}: {decision.Reason}");

            switch (decision.Kind)
            {
                case BotActionKind.Attack:
                    RigRoll(turn);
                    yield return Perform(decision, BossCoord());
                    break;

                case BotActionKind.Move:
                    yield return PerformMove(decision);
                    break;

                default:
                    Line($"T{turn:D2} sin acción posible — cierra el turno");
                    break;
            }

            yield return Capture($"turn_{turn:D2}_b_action");

            // Cerrar el turno. El handler cancela selecciones colgadas antes de cerrar, así que
            // llamarlo con algo pendiente es seguro — pero puede necesitar dos pasadas (la
            // primera cancela, la segunda cierra), que es exactamente lo que hace un jugador.
            var hud = CombatHud;
            if (hud != null)
            {
                hud.OnEndTurnRequested?.Invoke();
                yield return WaitRealtime(SettleSeconds);
                if (IsPlayerTurn()) hud.OnEndTurnRequested?.Invoke();
            }

            // Los enemigos juegan. No esperamos "a que vuelva el turno del player" acá: si el
            // jefe lo mata, ese turno no vuelve nunca y el loop se colgaría hasta el timeout.
            yield return WaitUntil(() => !IsPlayerTurn() || IsDead(_playerGuid) || IsDead(_bossGuid),
                StepTimeoutSeconds, "que el turno pase a los enemigos", soft: true);

            yield return WaitUntil(() => IsPlayerTurn() || IsDead(_playerGuid) || IsDead(_bossGuid),
                StepTimeoutSeconds, "que resuelvan los enemigos", soft: true);

            yield return Capture($"turn_{turn:D2}_c_enemy");
            Line($"T{turn:D2} cierre — {Snapshot()}");
        }

        /// <summary>
        /// Aprieta los botones que un jugador apretaría hasta que la acción ocurra. Ver el
        /// remark de la clase: es a propósito que no sea un guion fijo.
        /// </summary>
        private IEnumerator Perform(BotDecision decision, GridCoord? target)
        {
            var hud = CombatHud;
            if (hud == null) yield break;

            _behaviorExecuted = false;
            hud.OnBehaviorSelected?.Invoke(decision.BehaviorIndex);

            // Esta espera es load-bearing. El targeting abre de forma asincrónica (el sub-FSM pasa
            // por PlayerSelectingSubState → BeginSelection), así que preguntar por IsSelecting en
            // el mismo frame da false y el loop metía un Confirm que CANCELABA la selección del
            // ataque justo antes de clickear al jefe. Se veía como "el ataque se ejecutó y no hizo
            // daño": doce turnos con el jefe intacto en 170.
            yield return WaitRealtime(SettleSeconds);

            bool targetSubmitted = false;
            float deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;
            while (!_behaviorExecuted && Time.realtimeSinceStartup < deadline)
            {
                if (TrySubmitTarget(target))
                {
                    targetSubmitted = true;
                    yield return WaitRealtime(SettleSeconds);
                    continue;
                }
                if (!target.HasValue && IsSelecting) break;

                // Tras clickear el target, el chain rola SOLO. Pedir Roll acá gastaría una tirada
                // extra del budget (con energía infinita siempre entra) y descarrilaría la cadena.
                if (!targetSubmitted)
                {
                    // No-op si no corresponde: sale temprano sin _awaitingFirstRoll.
                    hud.OnRollRequested?.Invoke();
                    yield return WaitRealtime(SettleSeconds);
                    if (_behaviorExecuted) break;

                    // La tirada puede abrir su propia selección (chain post-roll). Volver arriba
                    // antes de confirmar, por el mismo motivo que la espera de arriba.
                    if (IsSelecting) continue;
                }

                hud.OnConfirmRequested?.Invoke();
                yield return WaitRealtime(SettleSeconds);
            }

            if (!_behaviorExecuted)
                Line("  la acción no se ejecutó (energía, rango o combo no disponible) — sigue igual");
        }

        /// <summary>
        /// El movimiento prueba destinos de mejor a peor: el rango de movimiento puede rechazar
        /// el tile pegado al jefe, y perder el turno parado sería peor que acercarse a medias.
        /// </summary>
        private IEnumerator PerformMove(BotDecision decision)
        {
            foreach (var destination in decision.Candidates)
            {
                yield return Perform(decision, destination);
                if (_behaviorExecuted)
                {
                    Line($"  se movió a {destination}");
                    yield break;
                }

                // Dejar limpio antes de reintentar: una selección abierta haría que el próximo
                // OnBehaviorSelected la cancele en vez de arrancar de nuevo.
                var selection = Selection;
                if (selection != null && selection.IsSelecting) selection.CancelSelection();
                yield return WaitRealtime(SettleSeconds);
            }

            if (!_behaviorExecuted)
                Line("  ningún destino entró en el rango de movimiento");
        }

        /// <summary>
        /// Si caminar no acorta la distancia, teleporta al lado del jefe.
        /// </summary>
        /// <remarks>
        /// Hace falta porque varios jefes se reposicionan cada turno: el Cajero camina tanto como
        /// el player, así que perseguirlo es una carrera que nunca converge — la primera corrida
        /// gastó los 4 turnos a distancia 2-3 recibiendo disparos y no le pegó una sola vez.
        ///
        /// Es el comando <c>tp</c> de la consola, no una vía nueva. Y se **loguea**: sin la línea,
        /// la captura del turno siguiente se leería como "el bot caminó hasta ahí", que sería falso.
        ///
        /// No se toca el balance ni el kit por esto: el bot existe para que el jefe haga sus cosas
        /// delante de la cámara, no para probar que la pelea se gana caminando.
        /// </remarks>
        private void TeleportIfChasingForever()
        {
            var grid = Grid;
            if (grid == null
                || !grid.TryGetPosition(_playerGuid, out var playerCoord)
                || !grid.TryGetPosition(_bossGuid, out var bossCoord))
            {
                return;
            }

            int distance = BossBotPolicy.Distance(playerCoord, bossCoord);
            if (distance <= 1)
            {
                _turnsOutOfRange = 0;
                return;
            }

            // Turnos fuera de rango, no "turnos sin progreso". Con el jefe caminando lo mismo que
            // el player la distancia baja de a un tile cada dos turnos, así que medir progreso
            // nunca disparaba: la primera corrida gastó los 4 turnos acercándose y murió sin
            // pegar una vez.
            _turnsOutOfRange++;
            if (_turnsOutOfRange < TurnsOutOfRangeBeforeTeleport) return;

            foreach (var tile in AdjacentTiles(bossCoord))
            {
                if (!grid.InBounds(tile) || !grid.IsWalkable(tile) || !grid.IsFree(tile)) continue;

                Line($"  {_turnsOutOfRange} turnos fuera de rango (distancia {distance}) — tp a {tile}");
                if (RunCommand("tp",
                        tile.X.ToString(CultureInfo.InvariantCulture),
                        tile.Y.ToString(CultureInfo.InvariantCulture)))
                {
                    _turnsOutOfRange = 0;
                    return;
                }
            }

            Line($"  {_turnsOutOfRange} turnos fuera de rango y ningún tile libre pegado al jefe");
        }

        /// <summary>
        /// Vecinos ortogonales solamente: el ataque no llega en diagonal (ver
        /// <see cref="BossBotPolicy.Distance"/>), así que teleportar a una esquina dejaba al bot
        /// "pegado" al jefe y sin poder pegarle.
        /// </summary>
        private static IEnumerable<GridCoord> AdjacentTiles(GridCoord center) => center.Neighbors4();

        private BotDecision Decide()
        {
            var hero = Player?.CurrentHero;
            if (hero == null)
                return new BotDecision(BotActionKind.None, -1, null, "sin hero resuelto");

            var slots = new List<BotBehaviorSlot>();
            var behaviors = hero.GetBehaviorsForPhase(GamePhase.Combat);
            for (int i = 0; i < behaviors.Count; i++)
            {
                var b = behaviors[i];
                if (b == null) continue;
                slots.Add(new BotBehaviorSlot(i, b.ActionName, b.NeedsDiceRoll, b.EnergyCost));
            }

            var grid = Grid;
            if (grid == null
                || !grid.TryGetPosition(_playerGuid, out var playerCoord)
                || !grid.TryGetPosition(_bossGuid, out var bossCoord))
            {
                return new BotDecision(BotActionKind.None, -1, null, "player o jefe sin posición en la grilla");
            }

            return BossBotPolicy.Decide(
                playerCoord, bossCoord, slots,
                tile => grid.InBounds(tile) && grid.IsWalkable(tile) && grid.IsFree(tile));
        }

        private void RigRoll(int turn)
        {
            if (!ServiceLocator.TryGetService<RiggedRollState>(out var rig) || rig == null)
            {
                Line("  RiggedRollState no registrado — la tirada sale al azar y la corrida no es comparable");
                return;
            }

            int diceCount = Player?.DiceBag?.Dice?.Count ?? 5;
            var faces = BossBotRoll.FacesFor(_args.Seed, turn, diceCount);
            rig.SetNext(faces);
            Line($"  tirada fijada [{string.Join(",", faces)}]");
        }

        // ---- Identificación del jefe ----------------------------------------

        /// <summary>
        /// Prefiere el guid que anuncia <c>BossEncounterStartedPayload</c> — es el mismo con el que
        /// el juego bindea la barra de vida del jefe, así que es la fuente autoritativa y no una
        /// inferencia. El fallback por más vida queda para una sala sin ese evento.
        /// </summary>
        private bool TryIdentifyBoss()
        {
            if (_announcedBossGuid != Guid.Empty)
            {
                _bossGuid = _announcedBossGuid;
                return true;
            }

            var order = TurnOrder?.OrderForRound;
            if (order == null) return false;

            int best = int.MinValue;
            foreach (var guid in order)
            {
                if (guid == _playerGuid || guid == Guid.Empty) continue;

                int hp = HealthOf(guid);
                if (hp > best)
                {
                    best = hp;
                    _bossGuid = guid;
                }
            }
            return _bossGuid != Guid.Empty;
        }

        // ---- Captura ---------------------------------------------------------

        private IEnumerator Capture(string name)
        {
            // Esperar en tiempo real: con timeScale > 1 un WaitForSeconds cae a mitad de animación.
            yield return WaitRealtime(SettleSeconds);

            string path = Path.Combine(OutputDir, name + ".png");
            // La variante que escribe archivo captura al final del frame por su cuenta, así que no
            // hace falta un WaitForEndOfFrame — que además este scheduler propio no entendería.
            ScreenCapture.CaptureScreenshot(path, 1);
            ShotsTaken++;

            // CaptureScreenshot escribe diferido: sin esta espera el archivo puede no existir
            // todavía cuando la corrida termine y Unity se cierre.
            yield return WaitRealtime(0.25f);
        }

        // ---- Consola ---------------------------------------------------------

        /// <summary>
        /// Corre un comando de la consola de verdad y volcá su salida al log de la corrida.
        /// </summary>
        /// <remarks>
        /// Sesión propia y no la del overlay en pantalla: <c>DevConsoleUI</c> se registra como
        /// <c>IDevConsoleService</c> pero su <c>Execute(string)</c> devuelve void y su
        /// <c>BufferLogSink</c> es privado, así que un comando que falla no dejaría rastro en
        /// ningún lado. Con una sesión propia tenemos el <c>CommandResult</c> y sus líneas.
        ///
        /// Construir una segunda <c>DevConsoleSession</c> es inerte: sus controllers de cheat sólo
        /// se suscriben a eventos en <c>Enable()</c>, no en el constructor. Y que <c>god</c> y
        /// <c>energy</c> peguen sobre los controllers del bot y no sobre los del overlay es lo
        /// correcto — la corrida no debería depender del estado que dejó una sesión a mano.
        /// </remarks>
        private bool RunCommand(string name, params string[] args)
        {
            _console ??= new DevConsoleSession();
            // La línea de bienvenida de la sesión no aporta nada al log de la corrida.
            _consoleLogCursor = _console.Log.Lines.Count;

            if (!_console.Registry.TryGet(name, out var command))
            {
                Line($"  comando desconocido: '{name}'");
                return false;
            }

            var result = command.Execute(args ?? Array.Empty<string>(), _console.Ctx);

            Line($"  > {name} {string.Join(" ", args ?? Array.Empty<string>())} → " +
                 $"{(result.Success ? "ok" : "FALLÓ")} {result.Message}");
            DrainConsoleLog();
            return result.Success;
        }

        private void DrainConsoleLog()
        {
            var lines = _console.Log.Lines;
            for (int i = _consoleLogCursor; i < lines.Count; i++) Line("    | " + lines[i]);
            _consoleLogCursor = lines.Count;
        }

        // ---- Eventos ---------------------------------------------------------

        private void Subscribe()
        {
            _onBehaviorExecuted = _ => _behaviorExecuted = true;
            EventManager.Subscribe(EventName.OnBehaviorExecuted, _onBehaviorExecuted);

            // Se suscribe antes del comando 'boss', porque el evento sale al arrancar el combate.
            _onBossEncounter = payload => _announcedBossGuid = payload.BossGuid;
            TypedEvent<BossEncounterStartedPayload>.Subscribe(_onBossEncounter);

            _onDamageResolved = payload =>
            {
                if (payload.TargetGuid != _bossGuid) return;
                _lastDamageToBoss = payload.FinalDamage;
                // El payload es struct: uno armado a mano trae 0. Sólo un valor en (0,1) es una
                // reducción real; 0 significa "nadie lo seteó", no "-100%".
                if (payload.IncomingMultiplier > 0f && payload.IncomingMultiplier < 1f)
                    _lastIncomingMultiplier = payload.IncomingMultiplier;
            };
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);
        }

        private void Unsubscribe()
        {
            if (_onBehaviorExecuted != null)
            {
                EventManager.UnSubscribe(EventName.OnBehaviorExecuted, _onBehaviorExecuted);
                _onBehaviorExecuted = null;
            }
            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }
            if (_onBossEncounter != null)
            {
                TypedEvent<BossEncounterStartedPayload>.Unsubscribe(_onBossEncounter);
                _onBossEncounter = null;
            }
        }

        // ---- Servicios y estado ---------------------------------------------

        private static IPlayerService Player =>
            ServiceLocator.TryGetService<IPlayerService>(out var ps) ? ps : null;

        private static TurnOrderService TurnOrder =>
            ServiceLocator.TryGetService<TurnOrderService>(out var t) ? t : null;

        private static IGridManager Grid =>
            ServiceLocator.TryGetService<IGridManager>(out var g) ? g : null;

        private static ISelectionController Selection =>
            ServiceLocator.TryGetService<ISelectionController>(out var s) ? s : null;

        private static bool IsSelecting => Selection?.IsSelecting == true;

        /// <summary>Clickea el tile si hay una selección esperándolo. <c>false</c> = no había nada que clickear.</summary>
        private bool TrySubmitTarget(GridCoord? target)
        {
            if (!target.HasValue) return false;

            var selection = Selection;
            if (selection == null || !selection.IsSelecting) return false;

            selection.OnTargetClicked(TargetRef.At(target.Value));
            return true;
        }

        private static CombatHUDView CombatHud =>
            ServiceLocator.TryGetService<IScreenManager>(out var screens) && screens != null
                ? screens.Current as CombatHUDView
                : null;

        /// <summary>
        /// El <c>ParticipantCount</c> no es una optimización: <c>TurnOrderService.Current</c>
        /// **lanza** sin orden construido, y hay una ventana real sin participantes entre el
        /// <c>Reset</c> y el <c>BuildForCombat</c> que dispara un cambio de fase del jefe. Sin la
        /// guarda, la corrida moría con una excepción justo cuando el jefe hacía algo interesante.
        /// </summary>
        private bool IsPlayerTurn()
        {
            var turnOrder = TurnOrder;
            return turnOrder != null
                && turnOrder.ParticipantCount > 0
                && _playerGuid != Guid.Empty
                && turnOrder.Current == _playerGuid;
        }

        private static int HealthOf(Guid guid)
        {
            if (guid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return 0;
            return attrs.GetAttribute<Health>(guid)?.Value ?? 0;
        }

        private static bool IsDead(Guid guid) => guid != Guid.Empty && HealthOf(guid) <= 0;

        private GridCoord? BossCoord() =>
            Grid != null && Grid.TryGetPosition(_bossGuid, out var coord) ? coord : (GridCoord?)null;

        /// <summary>Los números que cruzan con la imagen del mismo turno.</summary>
        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append($"player_hp={HealthOf(_playerGuid)} boss_hp={HealthOf(_bossGuid)}");

            if (_lastDamageToBoss > 0)
                sb.Append($" ult_daño={_lastDamageToBoss}");
            if (_lastIncomingMultiplier < 1f)
                sb.Append($" mult={_lastIncomingMultiplier.ToString("0.00", CultureInfo.InvariantCulture)}");

            // Presente sólo en peleas con mobiliario (La Generala). Es el número que explica el
            // "-70%" de la captura.
            if (ServiceLocator.TryGetService<RoomObjectArmorService>(out var armor) && armor != null)
            {
                int intact = armor.IntactCountFor(_bossGuid);
                if (intact > 0)
                    sb.Append($" dados_intactos={intact} dr={armor.ReductionFor(_bossGuid).ToString("0.00", CultureInfo.InvariantCulture)}");
            }
            return sb.ToString();
        }

        // ---- Esperas ---------------------------------------------------------

        /// <param name="soft">
        /// Un timeout blando se loguea y sigue. Se usa donde quedarse corto degrada la captura
        /// pero no invalida la corrida; uno duro corta y hace fallar al <c>.ps1</c>.
        /// </param>
        private IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what, bool soft = false)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    if (soft) { Line($"  timeout blando esperando {what}"); yield break; }
                    Fail($"timeout ({timeoutSeconds:0}s) esperando {what}");
                    yield break;
                }
                yield return null;
            }
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        // ---- Log -------------------------------------------------------------

        private void Fail(string reason)
        {
            State = BotRunState.Failed;
            Failure = reason;
            Line($"FALLA: {reason}");
            Debug.LogError(LogPrefix + reason);
            Flush();
        }

        private void Line(string message)
        {
            _log.AppendLine(message);
            Debug.Log(LogPrefix + message);
        }

        private void Flush()
        {
            if (string.IsNullOrEmpty(_logPath)) return;
            try
            {
                File.WriteAllText(_logPath, _log.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + $"no pude escribir {_logPath}: {ex.Message}");
            }
        }
    }
}
