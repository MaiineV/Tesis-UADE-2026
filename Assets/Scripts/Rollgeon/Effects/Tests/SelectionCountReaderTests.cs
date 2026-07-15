using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Selection;
using Rollgeon.Effects.Selection.Readers;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Sirenix.Serialization;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Cubre la cantidad de targets dinámica (<see cref="ISelectionCountReader"/>): la
    /// semántica de <see cref="SelectionSettings.GetSelectionCount"/> (picks requeridos),
    /// los readers concretos y el round-trip de serialización del campo polimórfico.
    /// </summary>
    [TestFixture]
    public sealed class SelectionCountReaderTests
    {
        private AttributesManager _attrManager;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _attrManager = new AttributesManager();
            _owner = Guid.NewGuid();
            AttributesManager.LogMissingEntityAsWarning = true;
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            ServiceLocator.Clear();
        }

        private void RegisterOwnerWithAttack(int attack)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Attack>(new Attack(attack));
            _attrManager.Register(_owner, attrs);
            ServiceLocator.AddService<AttributesManager>(_attrManager, ServiceScope.Run);
        }

        private Guid RegisterEnemyWithHealth(int health)
        {
            var guid = Guid.NewGuid();
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(health));
            _attrManager.Register(guid, attrs);
            return guid;
        }

        // ── GetSelectionCount (semántica "picks requeridos") ─────────────

        [Test]
        public void GetSelectionCount_ConstantMode_ReturnsSelectionCount()
        {
            // Arrange
            var settings = new SelectionSettings { SelectionCount = 3 };

            // Act + Assert
            Assert.AreEqual(3, settings.GetSelectionCount(default));
        }

        [Test]
        public void GetSelectionCount_DynamicNullReader_FallsBackToOne()
        {
            // Arrange — dinámico sin reader autorado: mínimo seguro.
            var settings = new SelectionSettings
            {
                IsConstantSelectionCount = false,
                SelectionCountReader = null,
                SelectionCount = 5,
            };

            // Act + Assert
            Assert.AreEqual(1, settings.GetSelectionCount(default));
        }

        [Test]
        public void GetSelectionCount_AoeMode_AlwaysOne()
        {
            // Arrange — en AoE el count no aplica: 1 pick (el ancla), el área hace el resto.
            var settings = new SelectionSettings
            {
                TargetMode = TargetMode.Aoe,
                SelectionCount = 5,
            };

            // Act + Assert
            Assert.AreEqual(1, settings.GetSelectionCount(default));
        }

        // ── StatCountReader ───────────────────────────────────────────────

        [Test]
        public void StatCountReader_ReadsOwnerStat_WithinClamp()
        {
            // Arrange
            RegisterOwnerWithAttack(3);
            var reader = new StatCountReader { Stat = StatType.Attack, Min = 1, Max = 16 };

            // Act + Assert
            Assert.AreEqual(3, reader.Read(new ReadInfo { ownerGuid = _owner }));
        }

        [Test]
        public void StatCountReader_ValueAboveMax_ClampsToMax()
        {
            // Arrange
            RegisterOwnerWithAttack(50);
            var reader = new StatCountReader { Stat = StatType.Attack, Min = 1, Max = 4 };

            // Act + Assert
            Assert.AreEqual(4, reader.Read(new ReadInfo { ownerGuid = _owner }));
        }

        [Test]
        public void StatCountReader_NoAttributesManager_ReturnsMin()
        {
            // Arrange — sin servicio registrado: mínimo seguro, nunca excepción
            // (hay call sites con ReadInfo default, ej. ActionDragPolicy).
            var reader = new StatCountReader { Stat = StatType.Attack, Min = 2, Max = 16 };

            // Act + Assert — guid válido sin servicio, y ReadInfo default.
            Assert.AreEqual(2, reader.Read(new ReadInfo { ownerGuid = _owner }));
            Assert.AreEqual(2, reader.Read(default));
        }

        // ── AliveEnemiesCountReader ───────────────────────────────────────

        [Test]
        public void AliveEnemiesCountReader_CountsOnlyAliveEnemies()
        {
            // Arrange — 3 enemigos, uno muerto (Health 0).
            RegisterOwnerWithAttack(1);
            var query = new FakeEntityQueryService();
            query.Enemies.Add(new Entity { Guid = RegisterEnemyWithHealth(10) });
            query.Enemies.Add(new Entity { Guid = RegisterEnemyWithHealth(10) });
            query.Enemies.Add(new Entity { Guid = RegisterEnemyWithHealth(0) });
            ServiceLocator.AddService<IEntityQueryService>(query, ServiceScope.Global);
            var reader = new AliveEnemiesCountReader { MaxCount = 16 };

            // Act + Assert
            Assert.AreEqual(2, reader.Read(new ReadInfo { ownerGuid = _owner }));
        }

        [Test]
        public void AliveEnemiesCountReader_CapsAtMaxCount()
        {
            // Arrange — 3 vivos pero tope 2.
            RegisterOwnerWithAttack(1);
            var query = new FakeEntityQueryService();
            for (int i = 0; i < 3; i++)
                query.Enemies.Add(new Entity { Guid = RegisterEnemyWithHealth(10) });
            ServiceLocator.AddService<IEntityQueryService>(query, ServiceScope.Global);
            var reader = new AliveEnemiesCountReader { MaxCount = 2 };

            // Act + Assert
            Assert.AreEqual(2, reader.Read(new ReadInfo { ownerGuid = _owner }));
        }

        [Test]
        public void AliveEnemiesCountReader_NoServices_ReturnsOne()
        {
            // Arrange — sin IEntityQueryService ni guid: mínimo seguro.
            var reader = new AliveEnemiesCountReader();

            // Act + Assert
            Assert.AreEqual(1, reader.Read(default));
            Assert.AreEqual(1, reader.Read(new ReadInfo { ownerGuid = _owner }));
        }

        // ── Integración con AutoResolve ───────────────────────────────────

        [Test]
        public void AutoResolveTargets_DynamicCount_PassesOwnerGuidToReader()
        {
            // Arrange — regresión: AutoResolveTargets pasaba ReadInfo default y el count
            // dinámico veía siempre un guid vacío.
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(4, 1));
            grid.Register(_owner, new GridCoord(0, 0));
            ServiceLocator.AddService<IGridManager>(grid, ServiceScope.Global);
            var reader = new CapturingCountReader { CountToReturn = 2 };
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Empty,
                Range = 3,
                IsConstantSelectionCount = false,
                SelectionCountReader = reader,
                AutoResolve = true,
            };

            // Act
            var result = settings.AutoResolveTargets(new GridCoord(0, 0), _owner);

            // Assert
            Assert.AreEqual(_owner, reader.LastInfo.ownerGuid,
                "El auto-resolve debe pasar el ownerGuid real al reader.");
            Assert.IsTrue(result.WasCompleted);
            Assert.AreEqual(2, result.SelectedTargets.Count,
                "El count dinámico define cuántos targets elige el auto-resolve.");
        }

        // ── Serialización del campo polimórfico ───────────────────────────

        [Test]
        public void SelectionSettings_WithStatCountReader_OdinRoundTrip_PreservesReaderType()
        {
            // Arrange — el campo ISelectionCountReader viaja [OdinSerialize, SerializeReference]
            // dentro de los SerializedScriptableObject (§13.6.1).
            var settings = new SelectionSettings
            {
                IsConstantSelectionCount = false,
                SelectionCountReader = new StatCountReader
                {
                    Stat = StatType.Energy,
                    UseModified = false,
                    Min = 2,
                    Max = 6,
                },
            };

            // Act
            var bytes = SerializationUtility.SerializeValue(settings, DataFormat.JSON);
            var roundTripped = SerializationUtility.DeserializeValue<SelectionSettings>(bytes, DataFormat.JSON);

            // Assert
            Assert.IsInstanceOf<StatCountReader>(roundTripped.SelectionCountReader,
                "El tipo concreto del reader debe sobrevivir el round-trip Odin.");
            var reader = (StatCountReader)roundTripped.SelectionCountReader;
            Assert.AreEqual(StatType.Energy, reader.Stat);
            Assert.IsFalse(reader.UseModified);
            Assert.AreEqual(2, reader.Min);
            Assert.AreEqual(6, reader.Max);
        }

        // ── ValidateSelection con resultado AoE expandido ─────────────────

        [Test]
        public void ValidateSelection_AoeExpandedResultWithMultipleTargets_Passes()
        {
            // Arrange — en AoE los picks requeridos son 1 (el ancla) pero el resultado
            // llega expandido (ancla + área): count > required debe validar.
            var effect = new TestEffect();
            effect.Selection.SlotState = SlotState.Occupied;
            effect.Selection.EntityFilter = EntityFilterMask.Enemies;
            effect.Selection.TargetMode = TargetMode.Aoe;
            var result = new TargetSelectionResult
            {
                WasCompleted = true,
                SelectedTargets = new List<TargetRef>
                {
                    TargetRef.At(new GridCoord(2, 0)),
                    TargetRef.At(new GridCoord(3, 0)),
                    TargetRef.At(new GridCoord(2, 1)),
                },
            };

            // Act
            var valid = effect.ValidateSelection(result, _owner, out var error);

            // Assert
            Assert.IsTrue(valid, $"El resultado AoE expandido debe validar: {error}");
            Assert.IsNull(error);
        }

        private sealed class TestEffect : BaseEffect
        {
            public override bool ApplyEffect(EffectContext context) => true;
        }

        private sealed class CapturingCountReader : ISelectionCountReader
        {
            public ReadInfo LastInfo;
            public int CountToReturn = 2;

            public int Read(ReadInfo info)
            {
                LastInfo = info;
                return CountToReturn;
            }
        }

        private sealed class FakeEntityQueryService : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();

            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;

            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();

            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }
    }
}
