using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Corre el árbol REAL de La Generala (el que arma <see cref="GeneralaAssetBuilder"/>) turno a
    /// turno: la mano que tira es pública, el área que marca es la del combo que le salió, y el
    /// cubilete le cobra <b>en el acto</b> a quien esté pegado cuando tira.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El cubilete es el precio de romperle la mesa de cerca (ficha "La Generala", piso 3: melee 18
    /// a quien esté pegado). Sin él los cinco dados se rompen gratis, porque el resto de su daño se
    /// avisa una ronda antes y se esquiva caminando.
    /// </para>
    /// <para>
    /// El bug que cubre este fixture es de convivencia: el cubilete devuelve <c>Failed</c> con el
    /// jugador lejos —que es la mitad de la pelea— y un Failed suelto en el Sequence raíz le
    /// cancelaría al jefe el telegraph de la mano. Los dos ocurren en el mismo turno y ninguno
    /// puede pisar al otro.
    /// </para>
    /// </remarks>
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
        private Rollgeon.Attributes.AttributesManager _attributes;
        private ComboCatalogSO _catalog;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private SpyDamagePipeline _pipeline;
        private EnemyDataSO _dice;
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

            _dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            _created.Add(_dice);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

            // Con vida completa el gate de Fase 2 evalúa false limpio (sin él, el lookup de un
            // entity no registrado ensucia la consola con warnings).
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
            _threat.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _attributes.Dispose();
            foreach (var asset in _created) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();
        }

        // ======================================================================
        // El cubilete
        // ======================================================================

        [Test]
        public void Turn_WithThePlayerGluedToTheTable_ChargesTheCupSlamOnTheSpot()
        {
            // Arrange — el jugador arranca pegado, que es de donde le rompe los dados.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1));

            // Assert — tirar ES bajar la copa: el golpe entra en el mismo turno, sin marca previa.
            CollectionAssert.AreEqual(new[] { GeneralaAssetBuilder.CupSlamDamage }, DamageAmounts(),
                "El único daño del primer turno tiene que ser el cubilete, y por lo que pide la ficha.");
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
        }

        [Test]
        public void Turn_WithThePlayerTwoTilesFromTheTable_ChargesNothing()
        {
            // Arrange — la distancia es el único aviso que tiene el cubilete, y la elige el jugador.
            MovePlayerTo(AwayTile);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1));

            // Assert
            CollectionAssert.IsEmpty(_pipeline.Resolved,
                "El cubilete es exactamente el precio de estar pegado: a distancia no cobra nada.");
        }

        [Test]
        public void Turn_ChargesTheCupSlam_EveryRoundThePlayerStaysGlued()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act — cinco rondas seguidas del mismo árbol (mismo instance state que en combate). La
            // marca de la mano se limpia entre turnos para que lo único que llegue al pipeline sea
            // el cubilete.
            for (int round = 1; round <= 5; round++)
            {
                root.Tick(NewContext(round));
                _threat.Clear(_boss);
            }

            // Assert — no hay compás par/impar: quedarse en la mesa no tiene ronda franca.
            CollectionAssert.AreEqual(
                Enumerable.Repeat(GeneralaAssetBuilder.CupSlamDamage, 5), DamageAmounts(),
                "Cinco tiradas pegado a la mesa son cinco cubiletes.");
        }

        [Test]
        public void Turn_LandsTheCupSlam_WithoutEatingTheHandMark()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1));

            // Assert — el cubilete ya cobró y la mano quedó marcada por el canal del boss.
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
            // Arrange — lejos, el nodo del cubilete devuelve Failed. Sin su Selector de aislamiento
            // ese Failed corta el Sequence raíz y el jefe se queda sin marcar la mano.
            MovePlayerTo(AwayTile);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1));

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var hand),
                "Esquivar el cubilete no puede apagarle el ataque de la ronda.");
            Assert.AreEqual(GeneralaAssetBuilder.PairDamage, hand.Damage,
                "Y la marca sigue siendo la del combo que le salió.");
        }

        // ======================================================================
        // La mano
        // ======================================================================

        [Test]
        public void Turn_PublishesTheRolledHand_SoThePlayerCanReadItBeforeItDetonates()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1, faces: ParHand));

            // Assert
            Assert.IsTrue(BossDiceHandService.ResolveOrCreate().TryGetHand(_boss, out var hand));
            Assert.AreEqual(ParHand, hand.Values, "Los cinco números son públicos.");
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, hand.ComboId);
        }

        [Test]
        public void Turn_TheHandMark_MatchesTheComboThatCameOut()
        {
            // Arrange — [4,4,2,5,1] ⇒ Par ⇒ franja de 1 fila por PairDamage.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 2, faces: ParHand));

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var hand));
            Assert.AreEqual(GeneralaAssetBuilder.PairDamage, hand.Damage,
                "El combo que le sale ES el ataque: un Par pega lo del Par.");
        }

        [Test]
        public void Turn_ABustHand_MarksTheMinimumInsteadOfNothing()
        {
            // Arrange — [1,2,4,6,3] no forma ningún combo del catálogo.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 2, faces: new[] { 1, 2, 4, 6, 3 }));

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var hand));
            Assert.AreEqual(GeneralaAssetBuilder.BustDamage, hand.Damage);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

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
