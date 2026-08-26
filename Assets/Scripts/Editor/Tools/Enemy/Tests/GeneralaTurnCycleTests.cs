using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Tiles;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Corre el árbol REAL turno a turno: el cubilete devuelve <c>Failed</c> con el jugador
    /// lejos y no puede comerse la marca del anillo, que se prende al turno siguiente.</summary>
    [TestFixture]
    public class GeneralaTurnCycleTests
    {
        /// <summary>Tirada fija: el Rng del contexto no puede quedar null o el spawn de la mesa falla.</summary>
        private static readonly int[] ScriptedFaces = { 4, 4, 2, 5, 1 };

        private static readonly GridCoord TableTile = new GridCoord(5, 3);

        /// <summary>Manhattan 1: la casilla desde la que el jugador le pega a ella, y ella a él.</summary>
        private static readonly GridCoord GluedTile = new GridCoord(6, 3);

        /// <summary>Manhattan 2: fuera del cubilete y también fuera del alcance del jugador.</summary>
        private static readonly GridCoord AwayTile = new GridCoord(7, 3);

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private HazardService _hazards;
        private StunService _stun;
        private IceStunBinder _iceBinder;
        private ContractModifierService _contractMods;
        private ComboLogService _comboLog;
        private Rollgeon.Attributes.AttributesManager _attributes;
        private ComboCatalogSO _catalog;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private SpyDamagePipeline _pipeline;
        private RoomObjectDefinitionSO _dice;
        private SpecialTileService _tiles;
        private SpecialTileDefinitionSO _electric;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 7)); // La sala del juego.
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            // Servicios reales y no stubs: lo que se protege es que el Sequence corra entero, y un
            // stub que no falla nunca escondería justo el Failed que hay que aislar.
            _hazards = new HazardService();
            _hazards.Register();

            _stun = new StunService();
            _stun.Register();

            _iceBinder = new IceStunBinder();
            _iceBinder.Register();

            _contractMods = new ContractModifierService();
            _contractMods.Register();

            _comboLog = new ComboLogService();
            _comboLog.Register();

            // Servicio real: lo que se prueba es que el anillo se plante de verdad.
            _tiles = new SpecialTileService();
            _tiles.Register();

            _electric = Create<SpecialTileDefinitionSO>();
            GeneralaAssetBuilder.ConfigureElectricTile(_electric);

            _attributes = new Rollgeon.Attributes.AttributesManager();
            ServiceLocator.AddService<Rollgeon.Attributes.AttributesManager>(_attributes);

            // Catálogo mínimo: alcanza el Par para que la mano tirada resuelva a algo concreto.
            _catalog = Create<ComboCatalogSO>();
            var par = Create<Combo_Par>();
            SetPrivateField(par, "_comboId", Rollgeon.Combos.ComboId.Par);
            SetPrivateField(par, "_baseDamage", 10);
            _catalog.EditorAdd(par);
            ServiceLocator.AddService<ComboCatalogSO>(_catalog);

            _pipeline = new SpyDamagePipeline();

            _dice = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _created.Add(_dice);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

            // Con vida completa el gate de Fase 2 evalúa false limpio, sin warnings de lookup.
            var bossStats = new Rollgeon.Attributes.ModifiableAttributes();
            bossStats.EnsureInitialized();
            bossStats.SetAttribute<Rollgeon.Attributes.Stats.Health>(
                new Rollgeon.Attributes.Stats.Health(GeneralaAssetBuilder.BossHp));
            _attributes.Register(_boss, bossStats);

            _grid.Register(_boss, TableTile);
            _grid.Register(_player, GluedTile);
        }

        [TearDown]
        public void TearDown()
        {
            _comboLog?.Dispose();
            _contractMods?.Dispose();
            _tiles?.Dispose();
            _iceBinder?.Dispose();
            _stun?.Dispose();
            _hazards?.Dispose();
            _threat.Dispose();

            // Publicar un anillo pinta overlay: crea su GameObject y cachea un material por tint.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftoverOverlay = GameObject.Find("ThreatTelegraphOverlay");
            if (leftoverOverlay != null) UnityEngine.Object.DestroyImmediate(leftoverOverlay);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _attributes.Dispose();
            foreach (var asset in _created) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();
        }

        /// <summary>
        /// La fuente del anillo NO es el guid del jefe: la marca viaja por su canal propio, y
        /// consumir el default devolveria vacio.
        /// </summary>
        private Guid RingSource()
            => AINode_TelegraphMark.SourceKey(_boss, GeneralaAssetBuilder.RingChannelId);

        [Test]
        public void Turn_WithThePlayerGluedToTheTable_ChargesTheCupSlamOnTheSpot()
        {
            // El jugador arranca pegado, que es de donde le rompe los dados.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));

            CollectionAssert.AreEqual(new[] { GeneralaAssetBuilder.CupSlamDamage }, DamageAmounts(),
                "El único daño del primer turno tiene que ser el cubilete, y por lo que pide la ficha.");
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void Turn_WithThePlayerTwoTilesFromTheTable_ChargesNothing()
        {
            MovePlayerTo(AwayTile);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));

            CollectionAssert.IsEmpty(_pipeline.Resolved,
                "El cubilete es exactamente el precio de estar pegado: a distancia no cobra nada.");
        }

        [Test]
        public void Turn_ChargesTheCupSlam_EveryRoundThePlayerStaysGlued()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            // Mismo árbol cinco rondas (mismo instance state que en combate). La marca del anillo
            // NO se limpia: es su ataque, y el jugador pegado a la mesa esta parado en el 3x3 del
            // centro, asi que el tercer tiempo del ciclo le prende encima.
            for (int round = 1; round <= 5; round++) root.Tick(NewContext(round));

            var cup = DamageAmounts().Where(d => d == GeneralaAssetBuilder.CupSlamDamage).ToList();
            Assert.AreEqual(5, cup.Count, "Cinco tiradas pegado a la mesa son cinco cubiletes.");

            // Y el anillo del centro cobra una sola vez en las cinco: el ciclo es de tres tiempos y
            // el jugador solo esta en uno de ellos.
            Assert.AreEqual(1, DamageAmounts().Count(d => d == GeneralaAssetBuilder.RingDamage),
                "El ciclo tiene que haber prendido el centro exactamente una vez en cinco rondas.");
        }

        [Test]
        public void Turn_LandsTheCupSlam_WithoutEatingTheRingMark()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));

            CollectionAssert.Contains(DamageAmounts(), GeneralaAssetBuilder.CupSlamDamage,
                "El cubilete tiene que haber cobrado en este mismo turno.");
            Assert.IsTrue(_threat.HasPending(RingSource()),
                "Y el anillo tiene que quedar marcado: el cubilete no puede comerse el telegraph.");
            Assert.AreEqual(1, _threat.SnapshotPending().Count,
                "Un solo aviso pendiente — el cubilete no ocupa canal propio, o el turno valdría " +
                "dos golpes.");
        }

        [Test]
        public void Turn_WithThePlayerOutOfTheCupsReach_StillMarksTheRing()
        {
            // Lejos el cubilete devuelve Failed; sin su Selector eso corta el Sequence raíz.
            MovePlayerTo(AwayTile);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));

            Assert.IsTrue(_threat.TryConsume(RingSource(), out var ring),
                "Esquivar el cubilete no puede apagarle el ataque de la ronda.");
            Assert.AreEqual(GeneralaAssetBuilder.RingDamage, ring.Damage);
        }

        [Test]
        public void Turn_MarksTheOuterRingFirst_AndWalksInwardOneRingPerTurn()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            var sizes = new List<int>();
            for (int round = 1; round <= 4; round++)
            {
                root.Tick(NewContext(round));
                Assert.IsTrue(_threat.TryConsume(RingSource(), out var ring),
                    $"La ronda {round} no marco ningun anillo.");
                sizes.Add(ring.Tiles.Count);
            }

            // De afuera hacia adentro, y el cuarto turno vuelve al grande: el ciclo es de tres.
            Assert.Greater(sizes[0], sizes[1], "El anillo del borde tiene que ser el mas grande.");
            Assert.Greater(sizes[1], sizes[2], "Y el del centro el mas chico.");
            Assert.AreEqual(sizes[0], sizes[3], "El cuarto turno vuelve al anillo del borde.");
        }

        [Test]
        public void Turn_LightsTheRingItMarkedTheTurnBefore_AndNotOnTheFirstTurn()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));
            CollectionAssert.IsEmpty(_tiles.ActiveInstances().ToList(),
                "En su primer turno no hay marca pendiente todavia: no puede prender nada.");

            root.Tick(NewContext(roundIndex: 2));
            Assert.AreEqual(1, _tiles.ActiveInstances().Count(),
                "El segundo turno prende el anillo que marco el primero.");
        }

        [Test]
        public void Turn_TheRingIsCenteredOnTheRoom_SoItDoesNotFollowHer()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));
            Assert.IsTrue(_threat.TryConsume(RingSource(), out var first));

            // El reposicionamiento del paso 7 ya la movio: si el anillo se anclara en ella, el
            // segundo tiempo saldria corrido.
            _grid.Register(_boss, AwayTile);
            root.Tick(NewContext(roundIndex: 2));
            Assert.IsTrue(_threat.TryConsume(RingSource(), out _));
            root.Tick(NewContext(roundIndex: 3));
            Assert.IsTrue(_threat.TryConsume(RingSource(), out _));
            root.Tick(NewContext(roundIndex: 4));
            Assert.IsTrue(_threat.TryConsume(RingSource(), out var wrapped));

            CollectionAssert.AreEquivalent(first.Tiles, wrapped.Tiles,
                "El mismo tiempo del ciclo tiene que marcar las mismas casillas aunque ella se haya " +
                "movido: el anillo se centra en la sala.");
        }

        [Test]
        public void Turn_ForbidsTheComboThePlayerJustScored_SoItPaysZeroNextRound()
        {
            _comboLog.Record(Rollgeon.Combos.ComboId.Par);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));

            // La UI del Contrato lee estas dos cosas exactas (ContractRowStateResolver).
            Assert.IsTrue(_contractMods.IsForbidden(Rollgeon.Combos.ComboId.Par),
                "La mano que acaba de anotar tiene que quedar prohibida.");
            Assert.AreEqual(0, _contractMods.GetEffectiveBaseDamage(Rollgeon.Combos.ComboId.Par, 10),
                "Repetirla tiene que resolver a 0 daño.");
        }

        [Test]
        public void Turn_TheBanIsASlidingWindow_SoLastRoundsComboComesBack()
        {
            _comboLog.Record(Rollgeon.Combos.ComboId.Par);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);
            root.Tick(NewContext(roundIndex: 1));

            _comboLog.Record(Rollgeon.Combos.ComboId.Poker);
            _threat.Clear(_boss);
            root.Tick(NewContext(roundIndex: 2));

            Assert.IsTrue(_contractMods.IsForbidden(Rollgeon.Combos.ComboId.Poker));
            Assert.IsFalse(_contractMods.IsForbidden(Rollgeon.Combos.ComboId.Par),
                "El ban de la ronda pasada se levanta: sólo se prohíbe la última.");
        }

        [Test]
        public void Turn_WithAnEmptyComboLog_BansNothing_AndStillMarksTheRing()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            root.Tick(NewContext(roundIndex: 1));

            Assert.IsFalse(_contractMods.HasAnyModifier,
                "Sin nada que repetir no se prohíbe nada — ni un id vacío.");
            Assert.IsTrue(_threat.HasPending(RingSource()),
                "Y la regla no puede comerse la marca del anillo.");
        }

        [Test]
        public void Turn_WithoutAMovementService_StillFinishesHerTurn()
        {
            // Sin IMovementService el reposicionamiento devuelve Failed.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);

            var result = root.Tick(NewContext(roundIndex: 1));

            Assert.AreNotEqual(AIResult.Failed, result,
                "Un Failed del reposicionamiento no puede propagarse al Sequence raíz.");
            Assert.IsTrue(_threat.HasPending(RingSource()), "Y el anillo queda marcado igual.");
        }

        private void MovePlayerTo(GridCoord coord) => _grid.Register(_player, coord);

        private AIContext NewContext(int roundIndex, int[] faces = null)
            => new AIContext
            {
                SelfGuid = _boss,
                PlayerGuid = _player,
                SelfMaxHp = GeneralaAssetBuilder.BossHp,
                Grid = _grid,
                Attributes = _attributes,
                DamagePipeline = _pipeline,
                RoundIndex = roundIndex,
                Rng = new ScriptedRandom(faces ?? ScriptedFaces),
            };

        private List<int> DamageAmounts()
        {
            var amounts = new List<int>(_pipeline.Resolved.Count);
            foreach (var ctx in _pipeline.Resolved) amounts.Add(ctx.BaseDamage);
            return amounts;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(field, $"No existe el campo '{fieldName}' en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        /// <summary>Caras escriteadas y cíclicas — la tirada del jefe tiene que ser determinística.</summary>
        private sealed class ScriptedRandom : System.Random
        {
            private readonly int[] _faces;
            private int _cursor;

            public ScriptedRandom(int[] faces) => _faces = faces;

            public override int Next(int minValue, int maxValue)
            {
                if (_faces == null || _faces.Length == 0) return minValue;
                return _faces[_cursor++ % _faces.Length];
            }

            public override int Next(int maxValue) => 0;

            public override double NextDouble() => 0d;
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }
        }
    }
}
