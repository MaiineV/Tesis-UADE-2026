using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Heroes.Tests
{
    /// <summary>
    /// Resolución de "la pasiva está surtiendo efecto ahora", que es lo que decide qué
    /// marco muestra el ícono de estado del HUD.
    /// </summary>
    [TestFixture]
    public class ClassPassiveActiveStateTests
    {
        private ClassPassiveSO _passive;
        private Guid _owner;

        [SetUp]
        public void Setup()
        {
            _passive = ScriptableObject.CreateInstance<ClassPassiveSO>();
            _owner = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            if (_passive != null) UnityEngine.Object.DestroyImmediate(_passive);
        }

        [Test]
        public void should_be_active_when_marked_always_active()
        {
            // Arrange — pasivas de efecto permanente, sin condición de disparo.
            _passive.AlwaysActive = true;

            // Act + Assert
            Assert.IsTrue(_passive.IsActiveFor(_owner));
        }

        [Test]
        public void should_ignore_conditions_when_marked_always_active()
        {
            // Arrange
            _passive.AlwaysActive = true;
            _passive.ActiveConditions = new List<BasePreCondition> { new StubCondition(false) };

            // Act + Assert
            Assert.IsTrue(_passive.IsActiveFor(_owner), "AlwaysActive corta antes de evaluar.");
        }

        [Test]
        public void should_be_inactive_when_no_conditions_are_authored()
        {
            // Arrange — una pasiva que no declaró cómo se la ve prendida no puede afirmar
            // que lo está; para las permanentes existe AlwaysActive, que es explícito.
            _passive.ActiveConditions = new List<BasePreCondition>();

            // Act + Assert
            Assert.IsFalse(_passive.IsActiveFor(_owner));
        }

        [Test]
        public void should_be_active_when_every_condition_passes()
        {
            // Arrange
            _passive.ActiveConditions = new List<BasePreCondition>
            {
                new StubCondition(true), new StubCondition(true),
            };

            // Act + Assert
            Assert.IsTrue(_passive.IsActiveFor(_owner));
        }

        [Test]
        public void should_be_inactive_when_any_condition_fails()
        {
            // Arrange — semántica AND, igual que EffectData.CanBeExecuted.
            _passive.ActiveConditions = new List<BasePreCondition>
            {
                new StubCondition(true), new StubCondition(false),
            };

            // Act + Assert
            Assert.IsFalse(_passive.IsActiveFor(_owner));
        }

        [Test]
        public void should_pass_the_owner_guid_to_the_conditions()
        {
            // Arrange — sin el owner en el contexto, condiciones como PCHasModifier no
            // pueden mirar los atributos del jugador y siempre darían false.
            var spy = new StubCondition(true);
            _passive.ActiveConditions = new List<BasePreCondition> { spy };

            // Act
            _passive.IsActiveFor(_owner);

            // Assert
            Assert.AreEqual(_owner, spy.LastContext?.OwnerGuid);
            Assert.AreEqual(_owner, spy.LastContext?.Entity?.Guid);
        }

        private sealed class StubCondition : BasePreCondition
        {
            private readonly bool _result;
            public PreConditionContext LastContext { get; private set; }

            public StubCondition(bool result) => _result = result;

            public override string ConditionName => "stub";

            public override bool Evaluate(PreConditionContext context)
            {
                LastContext = context;
                return _result;
            }
        }
    }
}
