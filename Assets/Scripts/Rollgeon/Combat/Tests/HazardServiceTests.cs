using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="HazardService"/>: el runner genérico que reemplazó el loop hardcoded de
    /// <see cref="RainHazardService"/>. Cualquier cantidad de <see cref="HazardDefinitionSO"/>
    /// puede estar activa a la vez, cada una con su propia cadencia y su propio source id — este
    /// suite cubre esa coexistencia (ver <see cref="RainHazardServiceTests"/> para el shim de rain
    /// en sí, que debe seguir comportándose idéntico).
    /// </summary>
    [TestFixture]
    public class HazardServiceTests
    {
        private GridManager _grid;
        private TurnOrderService _turnOrder;
        private ThreatenedAreaService _threat;
        private StubPlayerService _playerService;
        private StubMovementService _movement;
        private SpyDamagePipeline _pipeline;
        private HazardService _hazard;
        private Guid _playerGuid;

        private List<Guid> _activatedEvents;
        private List<KeyValuePair<Guid, Guid>> _triggeredEvents;
        private List<Guid> _expiredEvents;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _playerGuid = Guid.NewGuid();
            _grid.Register(_playerGuid, new GridCoord(4, 4)); // SquareAroundPlayer necesita una posición real.
            _playerService = new StubPlayerService { PlayerGuid = _playerGuid };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            // Registrado antes del service: la suscripción a OnEntityMoved es lazy pero se resuelve
            // en el primer Activate con tiles, así que el stub tiene que existir ya.
            _movement = new StubMovementService();
            ServiceLocator.AddService<IMovementService>(_movement);

            _turnOrder = new TurnOrderService();

            _hazard = new HazardService();
            _hazard.Register();

            _activatedEvents = new List<Guid>();
            _triggeredEvents = new List<KeyValuePair<Guid, Guid>>();
            _expiredEvents = new List<Guid>();
            EventManager.Subscribe(EventName.OnHazardActivated,
                args => _activatedEvents.Add((Guid)args[0]));
            EventManager.Subscribe(EventName.OnHazardTriggered,
                args => _triggeredEvents.Add(new KeyValuePair<Guid, Guid>((Guid)args[0], (Guid)args[1])));
            EventManager.Subscribe(EventName.OnHazardExpired,
                args => _expiredEvents.Add((Guid)args[0]));
        }

        [TearDown]
        public void TearDown()
        {
            // AINode_TelegraphMark dispara ThreatTelegraphOverlay.ResolveOrCreate() al marcar,
            // que crea un GameObject "ThreatTelegraphOverlay" en la escena — limpiarlo, si no
            // queda huérfano y contamina tests posteriores que lo buscan por nombre. El Dispose
            // además destruye los materiales cacheados por tint (uno por color usado en el test):
            // ahora son N y no uno, y cada uno que sobreviva es un Material leakeado.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void FireRound(int roundIndex)
        {
            _turnOrder.RestoreState(new[] { _playerGuid }, cursor: 0, roundIndex: roundIndex);
        }

        private static HazardDefinitionSO CreateDefinition(
            ThreatShape shape, int size, int count, int cycleRounds, int damage, AttackKind kind, Guid sourceId)
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Shape = shape;
            def.Size = size;
            def.Count = count;
            def.CycleRounds = cycleRounds;
            def.Damage = damage;
            def.Kind = kind;
            def.SourceId = sourceId.ToString();
            return def;
        }

        [Test]
        public void Activate_TwoHazardsWithDifferentCadences_EachTelegraphsOnItsOwnCycle()
        {
            // Arrange
            var rain = CreateDefinition(ThreatShape.ScatteredSquares, size: 1, count: 2, cycleRounds: 2,
                damage: 6, kind: AttackKind.Environmental, sourceId: Guid.NewGuid());
            var fire = CreateDefinition(ThreatShape.SquareAroundPlayer, size: 1, count: 1, cycleRounds: 3,
                damage: 8, kind: AttackKind.DamageOverTime, sourceId: Guid.NewGuid());
            _hazard.Activate(rain);
            _hazard.Activate(fire);

            // Act — ronda 2: solo la cadencia de rain (cada 2) cae.
            FireRound(2);

            // Assert
            Assert.IsTrue(_threat.HasPending(rain.SourceGuid), "Rain debería marcar en su cadencia (cada 2 rondas).");
            Assert.IsFalse(_threat.HasPending(fire.SourceGuid), "Fire no debería marcar todavía (cadencia cada 3 rondas).");

            // Act — ronda 3: cae la cadencia de fire; la de rain no vuelve a caer (3 % 2 != 0).
            FireRound(3);

            // Assert
            Assert.IsTrue(_threat.HasPending(fire.SourceGuid), "Fire debería marcar en la ronda 3 (su cadencia).");
            Assert.IsTrue(_threat.HasPending(rain.SourceGuid), "La marca de rain no debería tocarse en una ronda que no es múltiplo de su cadencia.");
        }

        [Test]
        public void Reset_ClearsAllActiveHazards_RegardlessOfCount()
        {
            // Arrange
            var rain = CreateDefinition(ThreatShape.ScatteredSquares, size: 1, count: 2, cycleRounds: 2,
                damage: 6, kind: AttackKind.Environmental, sourceId: Guid.NewGuid());
            var fire = CreateDefinition(ThreatShape.SquareAroundPlayer, size: 1, count: 1, cycleRounds: 2,
                damage: 8, kind: AttackKind.DamageOverTime, sourceId: Guid.NewGuid());
            _hazard.Activate(rain);
            _hazard.Activate(fire);
            FireRound(2);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());

            // Assert
            Assert.IsFalse(_hazard.IsActive(rain), "OnCombatEnd debería desactivar todos los hazards activos, no solo uno.");
            Assert.IsFalse(_hazard.IsActive(fire), "OnCombatEnd debería desactivar todos los hazards activos, no solo uno.");
            Assert.IsFalse(_threat.HasPending(rain.SourceGuid));
            Assert.IsFalse(_threat.HasPending(fire.SourceGuid));
        }

        [Test]
        public void Activate_SameDefinitionTwice_IsIdempotent()
        {
            // Arrange
            var fire = CreateDefinition(ThreatShape.SquareAroundPlayer, size: 1, count: 1, cycleRounds: 2,
                damage: 8, kind: AttackKind.DamageOverTime, sourceId: Guid.NewGuid());

            // Act
            _hazard.Activate(fire);
            _hazard.Activate(fire);

            // Assert
            Assert.IsTrue(_hazard.IsActive(fire));
        }

        [Test]
        public void IsActive_UnknownSourceId_ReturnsFalse()
        {
            // Arrange
            var unknownId = Guid.NewGuid();

            // Act
            var result = _hazard.IsActive(unknownId);

            // Assert
            Assert.IsFalse(result);
        }

        // ======================================================================
        // Dynamic-area instances: duration
        // ======================================================================

        [Test]
        public void ActivateWithTiles_DurationRounds_ExpiresAndClearsItself()
        {
            // Arrange
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7, durationRounds: 2);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 2) });
            Assert.AreNotEqual(Guid.Empty, instanceId, "Activate con tiles debería devolver un instanceId válido.");
            CollectionAssert.Contains(_activatedEvents, instanceId, "Debería haber disparado OnHazardActivated.");

            // Act — primera ronda: envejece pero sigue viva.
            FireRound(1);

            // Assert
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(2, 2), out var info),
                "Con DurationRounds=2 la instancia no debería morir en la primera ronda.");
            Assert.AreEqual(1, info.RemainingRounds, "Debería quedar exactamente una ronda.");

            // Act — segunda ronda: se agota.
            FireRound(2);

            // Assert
            Assert.IsFalse(_hazard.TryGetHazardAt(new GridCoord(2, 2), out _),
                "Al agotarse DurationRounds la instancia debería desaparecer.");
            CollectionAssert.Contains(_expiredEvents, instanceId, "Expirar debería disparar OnHazardExpired.");
            CollectionAssert.IsEmpty(_hazard.ActiveInstances(), "No debería quedar ninguna instancia viva.");
        }

        [Test]
        public void ActivateWithTiles_DurationZero_NeverExpiresOnItsOwn()
        {
            // Arrange — 0 = infinito, el comportamiento histórico de la lluvia.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7, durationRounds: 0);
            _hazard.Activate(def, new[] { new GridCoord(2, 2) });

            // Act
            FireRound(1);
            FireRound(2);
            FireRound(3);

            // Assert
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(2, 2), out _),
                "DurationRounds=0 debería significar 'no expira solo'.");
            CollectionAssert.IsEmpty(_expiredEvents);
        }

        [Test]
        public void ActivateWithTiles_SameDefinitionTwice_InstancesAreIndependent()
        {
            // Arrange — misma definición, dos sectores detonados distintos.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7, durationRounds: 2);
            var first = _hazard.Activate(def, new[] { new GridCoord(1, 1) });

            // La segunda nace una ronda después, así su duración corre por separado.
            FireRound(1);
            var second = _hazard.Activate(def, new[] { new GridCoord(6, 6) });

            // Assert — dos instancias vivas del mismo SO, con identidad propia.
            Assert.AreNotEqual(first, second, "Cada Activate con tiles debería crear una instancia nueva.");
            Assert.AreEqual(2, CountInstances(), "Las dos instancias del mismo SO deberían convivir.");

            // Act — la ronda que mata a la primera no debería tocar a la segunda.
            FireRound(2);

            // Assert
            Assert.IsFalse(_hazard.TryGetHazardAt(new GridCoord(1, 1), out _), "La primera instancia debería haber expirado.");
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(6, 6), out var alive), "La segunda instancia debería seguir viva.");
            Assert.AreEqual(second, alive.InstanceId);
            Assert.AreEqual(1, alive.RemainingRounds, "Cada instancia lleva su propio RemainingRounds.");
            CollectionAssert.Contains(_expiredEvents, first);
            CollectionAssert.DoesNotContain(_expiredEvents, second);
        }

        [Test]
        public void ActivateWithTiles_IgnoresDefinitionShape_AndUsesGivenTiles()
        {
            // Arrange — Shape dice ScatteredSquares/Count 5, pero el área explícita manda.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7);
            def.Shape = ThreatShape.ScatteredSquares;
            def.Count = 5;

            // Act
            _hazard.Activate(def, new[] { new GridCoord(3, 3) });

            // Assert
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(3, 3), out var info));
            Assert.AreEqual(1, info.Tiles.Count, "El área dinámica debería ser exactamente la pasada por parámetro.");
            Assert.IsFalse(_threat.HasPending(def.SourceGuid),
                "Las instancias de área dinámica no deberían pasar por IThreatenedAreaService.");
        }

        [Test]
        public void ActivateWithTiles_NullOrEmptyTiles_DoesNothing()
        {
            // Arrange
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7);

            // Act + Assert
            Assert.AreEqual(Guid.Empty, _hazard.Activate(def, null));
            Assert.AreEqual(Guid.Empty, _hazard.Activate(def, new GridCoord[0]));
            Assert.AreEqual(Guid.Empty, _hazard.Activate(null, new[] { new GridCoord(1, 1) }));
            Assert.AreEqual(0, CountInstances());
        }

        [Test]
        public void Deactivate_RemovesInstance_AndRaisesExpired()
        {
            // Arrange
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 2) });

            // Act
            _hazard.Deactivate(instanceId);

            // Assert
            Assert.IsFalse(_hazard.TryGetHazardAt(new GridCoord(2, 2), out _));
            CollectionAssert.Contains(_expiredEvents, instanceId);
        }

        // ======================================================================
        // OnTurnEndInTile (fuego)
        // ======================================================================

        [Test]
        public void OnTurnEndInTile_EntityEndsTurnOutsideArea_DoesNotDamage()
        {
            // Arrange — el player arranca en (4,4); la llama está en (2,2).
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7);
            _hazard.Activate(def, new[] { new GridCoord(2, 2) });

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved, "Terminar el turno fuera del área no debería hacer daño.");
            CollectionAssert.IsEmpty(_triggeredEvents);
        }

        [Test]
        public void OnTurnEndInTile_EntityEndsTurnInsideArea_DamagesThroughPipeline()
        {
            // Arrange
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7,
                kind: AttackKind.DamageOverTime);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 2) });
            _grid.Move(_playerGuid, new GridCoord(2, 2));

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Debería resolver daño exactamente una vez.");
            var ctx = _pipeline.Resolved[0];
            Assert.AreEqual(instanceId, ctx.SourceId, "El SourceId del daño debería ser la instancia del hazard.");
            Assert.AreEqual(_playerGuid, ctx.TargetId);
            Assert.AreEqual(7, ctx.BaseDamage);
            Assert.AreEqual(AttackKind.DamageOverTime, ctx.Kind);
            Assert.AreEqual(1, _triggeredEvents.Count);
            Assert.AreEqual(instanceId, _triggeredEvents[0].Key);
            Assert.AreEqual(_playerGuid, _triggeredEvents[0].Value);
        }

        [Test]
        public void OnTurnEndInTile_OnEnterHazard_DoesNotTickOnTurnEnd()
        {
            // Arrange — el hielo no debería cobrar por quedarse parado, solo al pisar.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 5);
            _hazard.Activate(def, new[] { new GridCoord(2, 2) });
            _grid.Move(_playerGuid, new GridCoord(2, 2));

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void SkipNextTick_SuppressesExactlyOneTurnEndTick()
        {
            // Arrange — regla de diseño: "la detonación consume la llama", pero solo ese turno.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 2) });
            _grid.Move(_playerGuid, new GridCoord(2, 2));

            // Act — el nodo del boss avisa que la detonación ya cobró este turno.
            _hazard.SkipNextTick(instanceId);
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved, "El tick suprimido no debería hacer daño.");

            // Act — el turno siguiente vuelve a tickear normal.
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "SkipNextTick debería suprimir un tick, no todos.");
        }

        // ======================================================================
        // OnEnter (hielo)
        // ======================================================================

        [Test]
        public void OnEnter_TriggersOnIntermediatePathTile_NotOnlyDestination()
        {
            // Arrange — la trampa está en el medio del camino, no en el destino.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 5);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 4) });

            // Act — (4,4) → (0,4) pasando por (2,4). El path incluye el origen, como el real.
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(0, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4),
                new GridCoord(1, 4), new GridCoord(0, 4)));

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Cruzar la trampa debería dispararla, no solo aterrizar en ella.");
            Assert.AreEqual(instanceId, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(1, _triggeredEvents.Count);
            Assert.AreEqual(instanceId, _triggeredEvents[0].Key);
        }

        [Test]
        public void OnEnter_OriginTile_DoesNotTrigger()
        {
            // Arrange — salir de una tile con hazard no es "pisarla".
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 5);
            _hazard.Activate(def, new[] { new GridCoord(4, 4) });

            // Act
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(2, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4)));

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved, "El origen del movimiento debería excluirse del scan.");
        }

        [Test]
        public void OnEnter_ZeroDamage_StillRaisesTriggeredEvent()
        {
            // Arrange — el hielo puede no hacer daño: el stun lo aplica StunService escuchando el evento.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 0);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(3, 4) });

            // Act
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(3, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4)));

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved, "Damage=0 no debería tocar el pipeline.");
            Assert.AreEqual(1, _triggeredEvents.Count, "Sin daño el evento sigue siendo obligatorio — es el hook del stun.");
            Assert.AreEqual(instanceId, _triggeredEvents[0].Key);
            Assert.AreEqual(_playerGuid, _triggeredEvents[0].Value);
        }

        [Test]
        public void OnEnter_ConsumeOnTrigger_RemovesTileAndDoesNotRetrigger()
        {
            // Arrange — dos tiles para que consumir una no mate la instancia.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 5, consumeOnTrigger: true);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(2, 4), new GridCoord(0, 0) });

            // Act — primera pisada.
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(2, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4)));

            // Assert — la tile pisada se fue, la instancia sigue viva por la otra.
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.IsFalse(_hazard.TryGetHazardAt(new GridCoord(2, 4), out _),
                "ConsumeOnTrigger debería remover del área la tile que disparó.");
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(0, 0), out var info),
                "La instancia debería seguir viva mientras le queden tiles.");
            Assert.AreEqual(instanceId, info.InstanceId);
            CollectionAssert.DoesNotContain(_expiredEvents, instanceId);

            // Act — volver a pisar la tile consumida.
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(2, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4), new GridCoord(2, 4)));

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Una tile ya consumida no debería volver a disparar.");
        }

        [Test]
        public void OnEnter_ConsumeLastTile_ExpiresInstance()
        {
            // Arrange
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 5, consumeOnTrigger: true);
            var instanceId = _hazard.Activate(def, new[] { new GridCoord(3, 4) });

            // Act
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(3, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4)));

            // Assert
            CollectionAssert.Contains(_expiredEvents, instanceId,
                "Consumir la última tile debería matar la instancia entera.");
            Assert.AreEqual(0, CountInstances());
        }

        [Test]
        public void OnEnter_NoPath_FallsBackToDestination()
        {
            // Arrange — un reposicionamiento instantáneo no reporta path, pero el destino sí se pisa.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 5);
            _hazard.Activate(def, new[] { new GridCoord(1, 1) });

            // Act
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(1, 1), null);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
        }

        [Test]
        public void OnEnter_AfterCombatEnd_NoLongerTriggers()
        {
            // Arrange — el cleanup de scope tiene que soltar también la suscripción a movimiento.
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 5);
            _hazard.Activate(def, new[] { new GridCoord(3, 4) });

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(3, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4)));

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved);
            Assert.AreEqual(0, CountInstances());
            CollectionAssert.IsEmpty(_expiredEvents,
                "El cleanup de fin de combate no debería disparar OnHazardExpired (mismo criterio que OnComboUnblocked).");
        }

        // ======================================================================
        // Coexistencia con el camino histórico
        // ======================================================================

        [Test]
        public void CycleTelegraphAndInstances_CoexistWithoutInterfering()
        {
            // Arrange — lluvia por ciclo + una llama de área dinámica.
            var rain = CreateDefinition(ThreatShape.ScatteredSquares, size: 1, count: 2, cycleRounds: 2,
                damage: 6, kind: AttackKind.Environmental, sourceId: Guid.NewGuid());
            _hazard.Activate(rain);

            var fire = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 7);
            var instanceId = _hazard.Activate(fire, new[] { new GridCoord(2, 2) });

            // Act
            FireRound(2);

            // Assert
            Assert.IsTrue(_threat.HasPending(rain.SourceGuid), "La lluvia debería seguir marcando por su cadencia.");
            Assert.IsTrue(_hazard.IsActive(rain), "IsActive sigue siendo source-keyed para el camino de ciclo.");
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(2, 2), out _), "La instancia dinámica debería seguir viva.");
            Assert.IsFalse(_hazard.IsActive(instanceId),
                "IsActive nunca debería responder por un instanceId — es source-keyed por diseño.");
        }

        // ======================================================================
        // Affects — a quién le cobra el hazard
        //
        // Regresión del Croupier quemándose con su propio fuego: los sectores que él enciende
        // incluyen su propia fila, cerraba el turno adentro y el hazard le cobraba 6 por turno.
        // ======================================================================

        [Test]
        public void OnTurnEndInTile_PlayerOnly_BossEndingTurnInTheFire_PaysNothing()
        {
            // Arrange — el jefe parado sobre su propio sector encendido.
            var boss = Guid.NewGuid();
            _grid.Register(boss, new GridCoord(2, 2));
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 6);
            _hazard.Activate(def, new[] { new GridCoord(2, 2) });

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, boss);

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved, "El jefe no debería quemarse con su propio fuego.");
            CollectionAssert.IsEmpty(_triggeredEvents,
                "Tampoco debería publicar el evento: es el hook del que cuelga el stun del hielo.");
        }

        [Test]
        public void OnTurnEndInTile_PlayerOnly_PlayerInTheSameFire_StillPays()
        {
            // Arrange — el mismo fuego cubriendo dos casillas: el jefe en una, el jugador en la otra.
            // Dos entidades no pueden compartir casilla, así que el contraste es "misma instancia de
            // fuego", no "misma tile".
            var boss = Guid.NewGuid();
            _grid.Register(boss, new GridCoord(2, 2));
            _grid.Move(_playerGuid, new GridCoord(2, 3));
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 6);
            _hazard.Activate(def, new[] { new GridCoord(2, 2), new GridCoord(2, 3) });

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, boss);
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "El filtro no debería tocar el camino del jugador.");
            Assert.AreEqual(_playerGuid, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(6, _pipeline.Resolved[0].BaseDamage);
        }

        [Test]
        public void OnEnter_PlayerOnly_BossWalkingItsOwnIce_DoesNotTriggerNorConsumeTheTile()
        {
            // Arrange — el Anotador deja la estela caminando, así que pisa su propio hielo siempre.
            var boss = Guid.NewGuid();
            _grid.Register(boss, new GridCoord(4, 4));
            var def = CreateInstanceDefinition(HazardTriggerMode.OnEnter, damage: 0, consumeOnTrigger: true);
            _hazard.Activate(def, new[] { new GridCoord(3, 4) });

            // Act — el jefe cruza la casilla…
            _movement.RaiseMoved(boss, new GridCoord(4, 4), new GridCoord(3, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4)));

            // Assert — …y la deja intacta para el jugador.
            CollectionAssert.IsEmpty(_triggeredEvents, "El hielo del jefe no debería dispararle al jefe.");
            Assert.IsTrue(_hazard.TryGetHazardAt(new GridCoord(3, 4), out _),
                "Un disparo que no ocurrió no debería consumir la casilla — si no, el jefe le gasta " +
                "la trampa al jugador con solo caminarla.");

            // Act — ahora sí el jugador.
            _movement.RaiseMoved(_playerGuid, new GridCoord(4, 4), new GridCoord(3, 4), Path(
                new GridCoord(4, 4), new GridCoord(3, 4)));

            // Assert
            Assert.AreEqual(1, _triggeredEvents.Count, "La casilla intacta debería cobrarle al jugador.");
            Assert.AreEqual(_playerGuid, _triggeredEvents[0].Value);
        }

        [Test]
        public void OnTurnEndInTile_AffectsEveryone_BillsTheBossToo()
        {
            // Arrange — el opt-in explícito: el campo tiene que poder abrirse, no ser decorativo.
            var boss = Guid.NewGuid();
            _grid.Register(boss, new GridCoord(2, 2));
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 6,
                affects: HazardAffects.Everyone);
            _hazard.Activate(def, new[] { new GridCoord(2, 2) });

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, boss);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "Everyone debería cobrarle a cualquiera.");
            Assert.AreEqual(boss, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void OnTurnEndInTile_PlayerOnlyWithoutPlayerService_BillsNobody()
        {
            // Arrange — fail-closed: sin poder nombrar al jugador, cobrarle a todos sería justamente
            // el bug que este filtro mata, y caería sobre el jefe.
            var boss = Guid.NewGuid();
            _grid.Register(boss, new GridCoord(2, 2));
            _grid.Move(_playerGuid, new GridCoord(2, 3));
            var def = CreateInstanceDefinition(HazardTriggerMode.OnTurnEndInTile, damage: 6);
            _hazard.Activate(def, new[] { new GridCoord(2, 2), new GridCoord(2, 3) });

            ServiceLocator.RemoveService<IPlayerService>();
            LogAssert.Expect(LogType.Warning, new Regex("IPlayerService no registrado"));

            // Act
            EventManager.Trigger(EventName.OnTurnFinished, boss);
            EventManager.Trigger(EventName.OnTurnFinished, _playerGuid);

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved, "Sin IPlayerService un hazard PlayerOnly no cobra a nadie.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static IReadOnlyList<GridCoord> Path(params GridCoord[] coords) => coords;

        private int CountInstances()
        {
            int count = 0;
            foreach (var _ in _hazard.ActiveInstances()) count++;
            return count;
        }

        private static HazardDefinitionSO CreateInstanceDefinition(
            HazardTriggerMode trigger,
            int damage,
            AttackKind kind = AttackKind.Environmental,
            int durationRounds = 0,
            bool consumeOnTrigger = false,
            HazardAffects affects = HazardAffects.PlayerOnly)
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Trigger = trigger;
            def.Damage = damage;
            def.Kind = kind;
            def.DurationRounds = durationRounds;
            def.ConsumeOnTrigger = consumeOnTrigger;
            def.Affects = affects;
            def.SourceId = Guid.NewGuid().ToString();
            return def;
        }

        private sealed class StubMovementService : IMovementService
        {
            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => new List<GridCoord>();

            public bool Move(Guid entity, GridCoord destination) => false;

            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

            /// <summary>Dispara OnEntityMoved como lo haría el service real.</summary>
            public void RaiseMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
                => OnEntityMoved?.Invoke(entity, from, to, path);
        }

        private class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }

        private class SpyDamagePipeline : IDamagePipeline
        {
            /// <summary>Todo lo que pasó por Resolve, en orden — los tests de hazard afirman sobre
            /// SourceId/TargetId/BaseDamage/Kind, no solo sobre "hubo daño".</summary>
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) { ctx.FinalDamage = ctx.BaseDamage; return ctx; }
        }
    }
}
