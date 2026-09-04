using System;
using Rollgeon.Combat.AI.Decisions;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Choice;
using Rollgeon.Movement;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Probability Drive (Feature#0084 §5, D4 Bandas cortes 1/3): distorsión (teleport radio 1 +
    /// swap), salto probabilístico (teleport ring 2-3) y control improbable (elección de 3
    /// opciones, o teleport directo con una sola).
    /// </summary>
    [TestFixture]
    public sealed class EffProbabilityDriveTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private sealed class FakeChoiceHost : IActiveItemChoiceHost
        {
            public ActiveItemChoiceRequest LastRequest;
            public bool RequestChoice(ActiveItemChoiceRequest request) { LastRequest = request; return true; }
        }

        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private AttributesManager _attributes;
        private FakeEntityQuery _query;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(25, 25));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes, ServiceScope.Global);

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            _traits?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private Guid SpawnEnemy(GridCoord coord)
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, coord);
            _traits.Register(guid, UnitTraits.DefaultGround);

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(30));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attributes.Register(guid, attrs);

            _query.Enemies.Add(new Entity { Guid = guid });
            return guid;
        }

        private static EffectContext BuildContext(Guid player, GridCoord center, IActiveItemChoiceHost choices = null)
        {
            return new EffectContext
            {
                SourceGuid = player,
                TargetGuid = player,
                lastResult = true,
                SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(center) },
                },
                TriggerContext = new ActiveItemRollTriggerContext
                {
                    Face = 1,
                    RawFace = 1,
                    Faces = 4,
                    Structure = ActiveItemResolution.Bands,
                    Choices = choices,
                },
            };
        }

        [Test]
        public void Distortion_TeleportsPlayerWithinRadiusOneOfCenter()
        {
            // Arrange — centro lejos del jugador, sala abierta: hay sobra de destinos en radio 1.
            var center = new GridCoord(10, 10);
            var effect = new EffProbabilityDistortion { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, center);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(_player, out var coord));
            Assert.LessOrEqual(center.Manhattan(coord), 1, "distorsión aterriza en radio 1");
        }

        [Test]
        public void Distortion_SwapsTheTwoEligibleEnemiesWithinSwapRadius()
        {
            // Arrange — dos enemigos movibles a distancia 2 del centro (dentro de SwapRadius=4).
            var center = new GridCoord(10, 10);
            var a = SpawnEnemy(new GridCoord(8, 10));
            var b = SpawnEnemy(new GridCoord(10, 8));
            var effect = new EffProbabilityDistortion { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, center);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(a, out var aCoord));
            Assert.IsTrue(_grid.TryGetPosition(b, out var bCoord));
            Assert.AreEqual(new GridCoord(10, 8), aCoord, "A terminó donde estaba B");
            Assert.AreEqual(new GridCoord(8, 10), bCoord, "B terminó donde estaba A");
        }

        [Test]
        public void Jump_TeleportsPlayerIntoRingTwoToThreeOfCenter()
        {
            // Arrange — sala abierta grande: el anillo 2-3 siempre tiene candidatos.
            var center = new GridCoord(12, 12);
            var effect = new EffProbabilityJump { Rng = new System.Random(3) };
            var ctx = BuildContext(_player, center);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(_player, out var coord));
            int dist = center.Manhattan(coord);
            Assert.IsTrue(dist >= 2 && dist <= 3, $"esperaba distancia 2-3, fue {dist}");
        }

        [Test]
        public void Choice_ThreeOptions_RequestsChoice_TeleportsOnChosen()
        {
            // Arrange — sala abierta: siempre hay >= 3 casillas seguras en radio 4.
            var center = new GridCoord(12, 12);
            var host = new FakeChoiceHost();
            var effect = new EffProbabilityChoice { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, center, host);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsNotNull(host.LastRequest, "con 3 opciones, pide elección — no teletransporta directo");
            Assert.AreEqual(3, host.LastRequest.Options.Count);

            var chosen = host.LastRequest.Options[1];
            host.LastRequest.OnChosen(chosen);

            Assert.IsTrue(_grid.TryGetPosition(_player, out var coord));
            Assert.AreEqual(chosen, coord);
        }

        [Test]
        public void Choice_Abandoned_TeleportsToARandomOption()
        {
            // Arrange
            var center = new GridCoord(12, 12);
            var host = new FakeChoiceHost();
            var effect = new EffProbabilityChoice { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, center, host);
            effect.ApplyEffect(ctx);
            Assert.IsNotNull(host.LastRequest);

            // Act
            host.LastRequest.OnAbandoned();

            // Assert — el roll ya se pagó: el efecto resuelve un destino, nunca deja el estado a medias.
            Assert.IsTrue(_grid.TryGetPosition(_player, out var coord));
            CollectionAssert.Contains(host.LastRequest.Options, coord);
        }

        [Test]
        public void Choice_SingleSafeOption_TeleportsDirectlyWithoutRequestingChoice()
        {
            // Arrange — sala de 2 celdas: el jugador ocupa una, la otra (el centro elegido) es
            // la única casilla segura dentro de radio 4 → teleport directo, sin abrir elección.
            _grid.LoadRoom(NavGraph.Rect(2, 1));
            _grid.Register(_player, new GridCoord(0, 0));
            var center = new GridCoord(1, 0);

            var host = new FakeChoiceHost();
            var effect = new EffProbabilityChoice { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, center, host);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsNull(host.LastRequest, "una sola opción — nunca abre la elección");
            Assert.IsTrue(_grid.TryGetPosition(_player, out var coord));
            Assert.AreEqual(center, coord);
        }
    }
}
