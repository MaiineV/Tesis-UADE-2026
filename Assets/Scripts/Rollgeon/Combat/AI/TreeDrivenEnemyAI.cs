using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Player;
using Rollgeon.Patterns;
using UnityEngine;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Handler de turno enemigo que evalúa el <c>AIRoot</c> registrado para cada enemigo.
    /// TECHNICAL.md §7.5. Si no hay árbol registrado, cae a <see cref="BasicEnemyAI"/>.
    /// </summary>
    /// <remarks>
    /// Se suscribe a <see cref="EventName.OnTurnQueueBuilt"/> para mantener un contador
    /// de rondas que se exporta al <c>PreConditionContext</c> via
    /// <see cref="AIContextPcExtensions.BuildPcContext"/>. Al llamar
    /// <see cref="HandleEnemyTurn"/> construye un <see cref="AIContext"/> fresco.
    /// </remarks>
    public sealed class TreeDrivenEnemyAI : IEnemyAIHandler, IDisposable
    {
        private readonly IEnemyAIRegistry _registry;
        private readonly BasicEnemyAI _fallback;
        private readonly Action _onTurnComplete;
        private readonly AttributesManager _attributes;
        private readonly IPlayerService _playerService;
        private readonly IDamagePipeline _damagePipeline;

        private int _roundIndex;
        private bool _subscribed;

        // Coroutines de turno en vuelo, por enemigo. CoroutineHost es un singleton
        // persistente: sin este tracking, una coroutine que sobrevive a su turno (o al
        // combate entero, ej. el golpe que mata al player) sigue tickeando el árbol y
        // puede aplicar daño extra fuera de su turno.
        private readonly Dictionary<Guid, Coroutine> _running = new Dictionary<Guid, Coroutine>();

        // Refuerzos spawneados mid-combate (AINode_SpawnReinforcements) que todavía no
        // consumieron su turno de aparición. Se los appendea a la ronda en curso, así que
        // actuarían antes de que el jugador vuelva a jugar: sin diferir, pegan de una (daño
        // gratis). El primer HandleEnemyTurn de un guid en este set NO tickea el árbol — el
        // refuerzo "aparece" sin actuar y recién pega en su siguiente turno, cuando el jugador
        // ya tuvo una ronda para reaccionar.
        private readonly HashSet<Guid> _deferredFirstTurn = new HashSet<Guid>();

        // La apertura corre una vez por pelea. OnTurnQueueBuilt vuelve a disparar en cada ronda y un
        // restore de save puede re-armar la cola: sin esta guarda, la mesa se volvería a instalar.
        private bool _openingDone;

        public TreeDrivenEnemyAI(
            IEnemyAIRegistry registry,
            AttributesManager attributes,
            IPlayerService playerService,
            IDamagePipeline damagePipeline,
            BasicEnemyAI fallback,
            Action onTurnComplete)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _damagePipeline = damagePipeline ?? throw new ArgumentNullException(nameof(damagePipeline));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _onTurnComplete = onTurnComplete ?? throw new ArgumentNullException(nameof(onTurnComplete));

            EventManager.Subscribe(EventName.OnTurnQueueBuilt, OnTurnQueueBuilt);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEnd);
            EventManager.Subscribe(EventName.OnReinforcementSpawned, OnReinforcementSpawned);
            _subscribed = true;
        }

        public int CurrentRoundIndex => _roundIndex;

        public void Dispose()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnTurnQueueBuilt, OnTurnQueueBuilt);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEnd);
            EventManager.UnSubscribe(EventName.OnReinforcementSpawned, OnReinforcementSpawned);
            StopAllRunning();
            _deferredFirstTurn.Clear();
            _subscribed = false;
        }

        public void HandleEnemyTurn(Guid enemyId)
        {
            // Un refuerzo recién spawneado difiere su PRIMERA activación: aparece en la ronda
            // en curso pero no actúa (no hace daño gratis). El turno se cierra de inmediato para
            // que la cola avance; en su siguiente turno el refuerzo ya tickea su árbol normal.
            if (_deferredFirstTurn.Remove(enemyId))
            {
                _onTurnComplete();
                return;
            }

            if (!_registry.TryGet(enemyId, out var root, out var maxHp) || root == null)
            {
                _fallback.HandleEnemyTurn(enemyId);
                return;
            }

            var ctx = BuildContext(enemyId, maxHp);

            if (Application.isPlaying)
            {
                // Un turno nuevo del mismo enemigo invalida cualquier coroutine previa
                // suya que haya quedado en vuelo — nunca dos árboles del mismo guid.
                if (_running.TryGetValue(enemyId, out var stale))
                    CoroutineHost.Stop(stale);
                _running[enemyId] = CoroutineHost.Run(HandleEnemyTurnCoroutine(root, ctx, enemyId));
            }
            else
            {
                try { root.Tick(ctx); }
                catch (Exception ex) { Debug.LogError($"[TreeDrivenEnemyAI] Exception ticking AIRoot for {enemyId}: {ex}"); }
                finally { _onTurnComplete(); }
            }
        }

        private IEnumerator HandleEnemyTurnCoroutine(Decisions.AIDecisionNode root, AIContext ctx, Guid enemyId)
        {
            AIResult result = AIResult.Failed;
            IEnumerator co = null;
            try
            {
                co = root.TickCoroutine(ctx, r => result = r);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TreeDrivenEnemyAI] Exception creating tick coroutine for {ctx.SelfGuid}: {ex}");
                _running.Remove(enemyId);
                _onTurnComplete();
                yield break;
            }

            bool hasMore = true;
            while (hasMore)
            {
                try { hasMore = co.MoveNext(); }
                catch (Exception ex)
                {
                    Debug.LogError($"[TreeDrivenEnemyAI] Exception during tick coroutine for {ctx.SelfGuid}: {ex}");
                    break;
                }
                if (hasMore) yield return co.Current;
            }

            _running.Remove(enemyId);
            _onTurnComplete();
        }

        private void OnCombatEnd(params object[] args)
        {
            // El combate puede cerrarse en mitad de un turno enemigo (ej. el golpe que
            // mata al player, o Victory instantánea): las coroutines de AI en vuelo no
            // deben seguir tickeando contra un combate que ya no existe.
            StopAllRunning();
            _openingDone = false;
        }

        private void StopAllRunning()
        {
            foreach (var co in _running.Values)
                CoroutineHost.Stop(co);
            _running.Clear();
        }

        /// <summary>
        /// El MISMO contexto con el que este enemigo va a tickear su árbol, para poder leerlo sin
        /// tickearlo. <c>null</c> si no tiene árbol registrado.
        /// </summary>
        /// <remarks>
        /// Reusa <see cref="BuildContext"/> en vez de armar uno de lectura: a un contexto al que
        /// le falte el <c>DamagePipeline</c>, <c>AINode_RangedShot.CanFire</c> le contesta que no
        /// va a disparar cuando sí va a disparar.
        /// </remarks>
        public AIContext TryBuildReadContext(Guid enemyId)
            => _registry.TryGet(enemyId, out var root, out var maxHp) && root != null
                ? BuildContext(enemyId, maxHp)
                : null;

        private AIContext BuildContext(Guid enemyId, int maxHp)
        {
            ServiceLocator.TryGetService<IGridManager>(out var grid);
            ServiceLocator.TryGetService<IMovementService>(out var movement);
            ServiceLocator.TryGetService<IEntityVisualService>(out var visuals);
            ServiceLocator.TryGetService<Pathing.IAIPathPlanner>(out var pathPlanner);

            // Personalidad de pathing: de los traits del spawn (default Normal). La tabla de
            // tuning es opcional — sin ella rigen los umbrales del GDD.
            var personality = Pathing.AIPersonalityProfile.Default;
            if (ServiceLocator.TryGetService<Rollgeon.Entities.Traits.IUnitTraitService>(out var traitService)
                && traitService != null)
            {
                var traits = traitService.Get(enemyId);
                ServiceLocator.TryGetService<Pathing.AIPathTuningSO>(out var tuning);
                personality = Pathing.AIPersonalityProfile.Resolve(
                    traits.Personality, traits.KamikazeIgnoresSurvival, tuning);
            }

            return new AIContext
            {
                PathPlanner = pathPlanner,
                Personality = personality,
                SelfGuid = enemyId,
                PlayerGuid = _playerService.PlayerGuid,
                SelfMaxHp = maxHp,
                Attributes = _attributes,
                DamagePipeline = _damagePipeline,
                Grid = grid,
                Movement = movement,
                PlayerService = _playerService,
                RoundIndex = _roundIndex,
                Rng = null,
                VisualService = visuals,
            };
        }

        private void OnTurnQueueBuilt(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[1] is int idx)) return;

            _roundIndex = idx + 1; // 1-based for condition UX

            // La cola se arma en CombatEnterState antes de pasar a PlayerTurnState, así que esta es la
            // última rendija que queda antes de que el jugador juegue. Ronda 0 = la cola inicial; las
            // rondas siguientes re-disparan el mismo evento y no vuelven a abrir.
            if (idx == 0) RunCombatOpening(args[0] as IReadOnlyList<Guid>);
        }

        /// <summary>
        /// Le da a cada enemigo la oportunidad de dejar instalado su estado de sala antes del primer
        /// turno del jugador. Ver <see cref="Decisions.IAIOpeningNode"/>.
        /// </summary>
        private void RunCombatOpening(IReadOnlyList<Guid> queue)
        {
            if (_openingDone || queue == null) return;
            _openingDone = true;

            var nodes = new List<Decisions.IAIOpeningNode>();
            foreach (var guid in queue)
            {
                if (guid == Guid.Empty || guid == _playerService.PlayerGuid) continue;
                if (!_registry.TryGet(guid, out var root, out var maxHp) || root == null) continue;

                nodes.Clear();
                CollectOpeningNodes(root, nodes);
                if (nodes.Count == 0) continue;

                var ctx = BuildContext(guid, maxHp);
                foreach (var node in nodes)
                {
                    // Un nodo que explota en la apertura no puede dejar la pelea sin arrancar: se
                    // reporta y el resto igual instala lo suyo.
                    try { node.Opening(ctx); }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TreeDrivenEnemyAI] Exception opening {node.GetType().Name} for {guid}: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Los <see cref="Decisions.IAIOpeningNode"/> del árbol, en orden de autorado.
        /// </summary>
        /// <remarks>
        /// Baja sólo por los compuestos incondicionales. Un <c>AINode_If</c> o un <c>AINode_Once</c>
        /// son la puerta de una fase o de una cadencia: abrirlos acá pondría en la mesa cosas que el
        /// jefe todavía no se ganó.
        /// </remarks>
        private static void CollectOpeningNodes(Decisions.AIDecisionNode node, List<Decisions.IAIOpeningNode> into)
        {
            if (node == null) return;
            if (node is Decisions.IAIOpeningNode opening) into.Add(opening);

            switch (node)
            {
                case Decisions.AINode_Sequence seq: CollectChildren(seq.Children, into); break;
                case Decisions.AINode_Selector sel: CollectChildren(sel.Children, into); break;
                case Decisions.AINode_Alternate alt: CollectChildren(alt.Children, into); break;
            }
        }

        private static void CollectChildren(List<Decisions.AIDecisionNode> children, List<Decisions.IAIOpeningNode> into)
        {
            if (children == null) return;
            foreach (var child in children) CollectOpeningNodes(child, into);
        }

        private void OnReinforcementSpawned(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (args[0] is Guid guid && guid != Guid.Empty)
                _deferredFirstTurn.Add(guid);
        }
    }
}
