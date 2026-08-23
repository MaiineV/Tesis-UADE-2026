using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Corre el árbol REAL turno a turno: el cubilete devuelve <c>Failed</c> con el jugador
    /// lejos y no puede comerse el telegraph de la mano, que se resuelve el mismo turno.</summary>
    [TestFixture]
    public class GeneralaTurnCycleTests
    {
        /// <summary>Tirada fija que resuelve a Par — el único combo del catálogo de estos tests.</summary>
        private static readonly int[] ParHand = { 4, 4, 2, 5, 1 };

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
        private HazardDefinitionSO _frost;
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

            _frost = Create<HazardDefinitionSO>();
            GeneralaAssetBuilder.ConfigureFrostHazard(_frost);

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

        [Test]
        public void Turn_WithThePlayerGluedToTheTable_ChargesTheCupSlamOnTheSpot()
        {
            // El jugador arranca pegado, que es de donde le rompe los dados.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

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
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            root.Tick(NewContext(roundIndex: 1));

            CollectionAssert.IsEmpty(_pipeline.Resolved,
                "El cubilete es exactamente el precio de estar pegado: a distancia no cobra nada.");
        }

        [Test]
        public void Turn_ChargesTheCupSlam_EveryRoundThePlayerStaysGlued()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            // Mismo árbol cinco rondas (mismo instance state que en combate); la marca se
            // limpia entre turnos para que al pipeline sólo llegue el cubilete.
            for (int round = 1; round <= 5; round++)
            {
                root.Tick(NewContext(round));
                _threat.Clear(_boss);
            }

            CollectionAssert.AreEqual(
                Enumerable.Repeat(GeneralaAssetBuilder.CupSlamDamage, 5), DamageAmounts(),
                "Cinco tiradas pegado a la mesa son cinco cubiletes.");
        }

        [Test]
        public void Turn_LandsTheCupSlam_WithoutEatingTheHandMark()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            root.Tick(NewContext(roundIndex: 1));

            CollectionAssert.Contains(DamageAmounts(), GeneralaAssetBuilder.CupSlamDamage,
                "El cubilete tiene que haber cobrado en este mismo turno.");
            Assert.IsTrue(_threat.HasPending(_boss),
                "Y la mano tiene que quedar marcada: el cubilete no puede comerse el telegraph.");
            Assert.AreEqual(1, _threat.SnapshotPending().Count,
                "Un solo aviso pendiente — el cubilete no ocupa canal propio, o la tirada valdría " +
                "dos golpes.");
        }

        [Test]
        public void Turn_WithThePlayerOutOfTheCupsReach_StillMarksTheHand()
        {
            // Lejos el cubilete devuelve Failed; sin su Selector eso corta el Sequence raíz.
            MovePlayerTo(AwayTile);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            root.Tick(NewContext(roundIndex: 1));

            Assert.IsTrue(_threat.TryConsume(_boss, out var hand),
                "Esquivar el cubilete no puede apagarle el ataque de la ronda.");
            Assert.AreEqual(GeneralaAssetBuilder.PairDamage, hand.Damage,
                "Y la marca sigue siendo la del combo que le salió.");
        }

        [Test]
        public void Turn_PublishesTheRolledHand_SoThePlayerCanReadItBeforeItDetonates()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            root.Tick(NewContext(roundIndex: 1, faces: ParHand));

            Assert.IsTrue(BossDiceHandService.ResolveOrCreate().TryGetHand(_boss, out var hand));
            Assert.AreEqual(ParHand, hand.Values, "Los cinco números son públicos.");
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, hand.ComboId);
        }

        [Test]
        public void Turn_TheHandMark_MatchesTheComboThatCameOut()
        {
            // [4,4,2,5,1] ⇒ Par ⇒ franja de 1 fila por PairDamage.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            root.Tick(NewContext(roundIndex: 2, faces: ParHand));

            Assert.IsTrue(_threat.TryConsume(_boss, out var hand));
            Assert.AreEqual(GeneralaAssetBuilder.PairDamage, hand.Damage,
                "El combo que le sale ES el ataque: un Par pega lo del Par.");
        }

        [Test]
        public void Turn_ABustHand_MarksTheMinimumInsteadOfNothing()
        {
            // [1,2,4,6,3] no forma ningún combo del catálogo.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            root.Tick(NewContext(roundIndex: 2, faces: new[] { 1, 2, 4, 6, 3 }));

            Assert.IsTrue(_threat.TryConsume(_boss, out var hand));
            Assert.AreEqual(GeneralaAssetBuilder.BustDamage, hand.Damage);
        }

        [Test]
        public void Turn_OnAFrostRound_FreezesOnlyTheTilesAroundHer_CenterIncluded()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            // Múltiplo de FrostParityDivisor (3).
            root.Tick(NewContext(roundIndex: 3));

            Assert.IsTrue(_hazards.TryGetHazardAt(GluedTile, out var frost),
                "La casilla pegada a ella es donde vive su quinto dado, y es lo que el candado cierra.");
            Assert.IsTrue(_hazards.TryGetHazardAt(TableTile, out _),
                "Su propia casilla incluida: no se congela por ser la dueña del área, no por estar afuera.");
            Assert.IsTrue(_hazards.TryGetHazardAt(new GridCoord(4, 2), out _),
                "La esquina también: es Chebyshev 1, no Manhattan.");

            Assert.IsFalse(_hazards.TryGetHazardAt(AwayTile, out _),
                "A distancia 2 ya NO hiela: el área bajó de 5×5 a 3×3 para dejar de leerse como " +
                "terreno prohibido y volver a leerse como el cerrojo de una casilla.");

            Assert.AreEqual(9, frost.Tiles.Count,
                "El 3×3 macizo son 9 casillas — la sala 11×7 las contiene todas.");
        }

        [Test]
        public void Turn_OnAFreeRound_LeavesTheTableClear_SoThereIsAWindowToBreakDice()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            // 2 no es múltiplo de FrostParityDivisor (3).
            root.Tick(NewContext(roundIndex: 2));

            Assert.IsFalse(_hazards.TryGetHazardAt(GluedTile, out _),
                "En ronda franca no cae escarcha.");
            CollectionAssert.IsEmpty(_hazards.ActiveInstances(), "Ni ninguna otra instancia.");
        }

        [Test]
        public void Turn_TheFrostCostsATurnAndNotHp_SoTheFloorCeilingHolds()
        {
            // Pegado: la escarcha le cae encima sin stunearlo, porque OnEnter se dispara
            // al pisar y él ya estaba adentro.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            // Ronda de escarcha (múltiplo de FrostParityDivisor).
            root.Tick(NewContext(roundIndex: 3));

            CollectionAssert.AreEqual(new[] { GeneralaAssetBuilder.CupSlamDamage }, DamageAmounts(),
                "La escarcha no puede cobrar daño: paga en turnos.");
        }

        [Test]
        public void Turn_TheFrostReplacesTheRingOfThePreviousCast_InsteadOfStacking()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            // Dos ciclos de escarcha (rondas 3 y 6, múltiplos de FrostParityDivisor).
            root.Tick(NewContext(roundIndex: 3));
            _threat.Clear(_boss);
            root.Tick(NewContext(roundIndex: 6));

            Assert.AreEqual(1, _hazards.ActiveInstances().Count(),
                "Una sola escarcha viva: dos superpuestas dejarían medio mapa helado.");
        }

        [Test]
        public void Turn_ForbidsTheComboThePlayerJustScored_SoItPaysZeroNextRound()
        {
            _comboLog.Record(Rollgeon.Combos.ComboId.Par);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

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
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);
            root.Tick(NewContext(roundIndex: 1));

            _comboLog.Record(Rollgeon.Combos.ComboId.Poker);
            _threat.Clear(_boss);
            root.Tick(NewContext(roundIndex: 2));

            Assert.IsTrue(_contractMods.IsForbidden(Rollgeon.Combos.ComboId.Poker));
            Assert.IsFalse(_contractMods.IsForbidden(Rollgeon.Combos.ComboId.Par),
                "El ban de la ronda pasada se levanta: sólo se prohíbe la última.");
        }

        [Test]
        public void Turn_WithAnEmptyComboLog_BansNothing_AndStillMarksTheHand()
        {
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            root.Tick(NewContext(roundIndex: 1));

            Assert.IsFalse(_contractMods.HasAnyModifier,
                "Sin nada que repetir no se prohíbe nada — ni un id vacío.");
            Assert.IsTrue(_threat.HasPending(_boss),
                "Y la regla no puede comerse la marca de la mano.");
        }

        [Test]
        public void Turn_WithoutAMovementService_StillFinishesHerTurn()
        {
            // Sin IMovementService el reposicionamiento devuelve Failed.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);

            var result = root.Tick(NewContext(roundIndex: 1));

            Assert.AreNotEqual(AIResult.Failed, result,
                "Un Failed del reposicionamiento no puede propagarse al Sequence raíz.");
            Assert.IsTrue(_threat.HasPending(_boss), "Y la mano queda marcada igual.");
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
                Rng = new ScriptedRandom(faces ?? ParHand),
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
