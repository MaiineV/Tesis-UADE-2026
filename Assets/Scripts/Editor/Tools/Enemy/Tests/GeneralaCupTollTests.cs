using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
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
    /// Corre el árbol REAL de La Generala (el que construye <see cref="GeneralaAssetBuilder"/>) y
    /// verifica el compás del cubilete: <b>baja en los turnos impares y no en los pares</b>, sin
    /// llevarse puesta la marca de la mano de dados.
    /// </summary>
    /// <remarks>
    /// Es el test que cubre el exploit que cerró el cubilete (pegarle a la mesa gratis) y, de paso,
    /// el bug que existiría si los dos avisos compartieran fuente en <see cref="IThreatenedAreaService"/>.
    /// </remarks>
    [TestFixture]
    public class GeneralaCupTollTests
    {
        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private Rollgeon.Attributes.AttributesManager _attributes;
        private ComboCatalogSO _catalog;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();
        private EnemyDataSO _dice;
        private Guid _boss;
        private Guid _player;
        private Guid _cupChannel;

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

            _grid.Register(_boss, new GridCoord(5, 3));
            _grid.Register(_player, new GridCoord(6, 3)); // Pegado a la mesa: dentro del anillo.
            _cupChannel = AINode_AuxTelegraph.ChannelGuid(_boss, GeneralaAssetBuilder.CupChannelId);
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

        [Test]
        public void Turn_OnAnOddRound_DropsTheCupTollAroundHerself()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1));

            // Assert
            Assert.IsTrue(_threat.HasPending(_cupChannel),
                "Turno impar: el cubilete tiene que quedar marcado.");
            Assert.IsTrue(_threat.TryConsume(_cupChannel, out var cup));
            Assert.AreEqual(GeneralaAssetBuilder.CupTollDamage, cup.Damage);
            Assert.AreEqual(9, cup.Tiles.Count, "3×3 alrededor suyo.");
        }

        [Test]
        public void Turn_OnAnEvenRound_DropsNoCupToll()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 2));

            // Assert
            Assert.IsFalse(_threat.HasPending(_cupChannel),
                "Turno par: la mesa no cobra peaje — es la ventana para romperle dados.");
        }

        [Test]
        public void Turn_AlternatesTheCupToll_RoundAfterRound()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);
            var marked = new List<bool>();

            // Act — cinco rondas seguidas del mismo árbol (mismo instance state que en combate).
            for (int round = 1; round <= 5; round++)
            {
                root.Tick(NewContext(round));
                marked.Add(_threat.HasPending(_cupChannel));
                _threat.Clear(_cupChannel); // El aviso se cobra al turno siguiente; acá solo medimos.
            }

            // Assert
            Assert.AreEqual(new[] { true, false, true, false, true }, marked,
                "El cubilete tiene que seguir el compás impar/par (el mismo que el lápiz del Anotador).");
        }

        [Test]
        public void Turn_OnAnOddRound_KeepsBothTheHandMarkAndTheCupToll()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1));

            // Assert — dos avisos, dos fuentes: la mano por la del boss, el cubilete por su canal.
            Assert.IsTrue(_threat.HasPending(_boss),
                "La mano de dados tiene que quedar marcada por el canal principal del boss.");
            Assert.IsTrue(_threat.HasPending(_cupChannel),
                "Y el cubilete por el suyo — uno no puede sobrescribir al otro.");
        }

        [Test]
        public void Turn_PublishesTheRolledHand_SoThePlayerCanReadItBeforeItDetonates()
        {
            // Arrange
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 1, faces: new[] { 4, 4, 2, 5, 1 }));

            // Assert
            Assert.IsTrue(BossDiceHandService.ResolveOrCreate().TryGetHand(_boss, out var hand));
            Assert.AreEqual(new[] { 4, 4, 2, 5, 1 }, hand.Values, "Los cinco números son públicos.");
            Assert.AreEqual(Rollgeon.Combos.ComboId.Par, hand.ComboId);
        }

        [Test]
        public void Turn_TheHandMark_MatchesTheComboThatCameOut()
        {
            // Arrange — [4,4,2,5,1] ⇒ Par ⇒ franja de 1 fila por PairDamage.
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);

            // Act
            root.Tick(NewContext(roundIndex: 2, faces: new[] { 4, 4, 2, 5, 1 }));

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

        [Test]
        public void Turn_ChargesThePendingCupToll_OnTheFollowingTurn()
        {
            // Arrange — el jugador se queda en la mesa con el cubilete cantado.
            var pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(pipeline);
            var root = GeneralaAssetBuilder.BuildAIRoot(_dice);
            root.Tick(NewContext(roundIndex: 1, pipeline: pipeline));
            Assert.IsTrue(_threat.HasPending(_cupChannel), "Precondición: el cubilete quedó marcado.");

            // Act — turno siguiente del jefe.
            root.Tick(NewContext(roundIndex: 2, pipeline: pipeline));

            // Assert
            CollectionAssert.Contains(DamageAmounts(pipeline), GeneralaAssetBuilder.CupTollDamage,
                "Quedarse un turno de más en la mesa tiene que costar el peaje del cubilete.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private AIContext NewContext(int roundIndex, int[] faces = null, IDamagePipeline pipeline = null)
            => new AIContext
            {
                SelfGuid = _boss,
                PlayerGuid = _player,
                SelfMaxHp = GeneralaAssetBuilder.BossHp,
                Grid = _grid,
                Attributes = _attributes,
                DamagePipeline = pipeline,
                RoundIndex = roundIndex,
                Rng = new ScriptedRandom(faces ?? new[] { 4, 4, 2, 5, 1 }),
            };

        private static List<int> DamageAmounts(SpyDamagePipeline pipeline)
        {
            var amounts = new List<int>(pipeline.Resolved.Count);
            foreach (var ctx in pipeline.Resolved) amounts.Add(ctx.BaseDamage);
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
