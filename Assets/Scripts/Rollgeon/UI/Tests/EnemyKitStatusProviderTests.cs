using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Weakness;
using Rollgeon.Entities;
using Rollgeon.UI.HUD.Status;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="EnemyKitStatusProvider"/>: la debilidad y el teleport de un enemigo,
    /// en su columna del costado.
    /// </summary>
    [TestFixture]
    public class EnemyKitStatusProviderTests
    {
        private WeaknessRegistry _registry;
        private List<StatusIconState> _states;
        private Guid _boss;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _registry = new WeaknessRegistry();
            ServiceLocator.AddService<IWeaknessRegistry>(_registry, ServiceScope.Global);

            _boss = Guid.NewGuid();
            _states = new List<StatusIconState>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();

            ServiceLocator.Clear();
        }

        private EnemyDataSO MakeData(AIDecisionNode root = null)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.AIRoot = root;
            _created.Add(data);
            return data;
        }

        [Test]
        public void test_kit_a_registered_weakness_becomes_the_top_card()
        {
            // Arrange — el registry y no el SO: es la fuente viva, la IA puede reescribirla.
            _registry.SetWeakness(_boss, "combo.poker", 1.3f);
            var provider = new EnemyKitStatusProvider(catalog: null, MakeData());

            // Act — CollectWeakness y no Collect: la debilidad va a la columna PRINCIPAL del
            // panel, así que la fila la pide por separado del resto del kit.
            provider.CollectWeakness(_boss, _states);

            // Assert
            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(EnemyKitStatusProvider.WeaknessId, _states[0].Id);
            StringAssert.Contains("1.3", _states[0].Description,
                "El multiplicador vigente va en la regla: sin él, 'débil al póker' no dice " +
                "cuánto vale tirarlo.");
        }

        [Test]
        public void test_kit_no_weakness_registered_publishes_no_weakness_card()
        {
            // Arrange
            var provider = new EnemyKitStatusProvider(catalog: null, MakeData());

            // Act
            provider.CollectWeakness(_boss, _states);

            // Assert
            Assert.IsEmpty(_states,
                "Un común sin debilidad no puede mostrar la fila: una tarjeta 'Débil' sin combo " +
                "promete un multiplicador que no existe.");
        }

        [Test]
        public void test_kit_a_teleport_node_anywhere_in_the_tree_becomes_a_card()
        {
            // Arrange — el nodo enterrado en una rama, como en el árbol real del Croupier.
            var root = new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_Selector
                    {
                        Children = new List<AIDecisionNode> { new AINode_TeleportNearTarget() },
                    },
                },
            };
            var provider = new EnemyKitStatusProvider(catalog: null, MakeData(root));

            // Act
            provider.Collect(_boss, _states);

            // Assert
            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(EnemyKitStatusProvider.TeleportId, _states[0].Id);
        }

        [Test]
        public void test_kit_a_tree_without_teleports_publishes_no_teleport_card()
        {
            // Arrange
            var root = new AINode_Sequence
            {
                Children = new List<AIDecisionNode> { new AINode_Wait() },
            };
            var provider = new EnemyKitStatusProvider(catalog: null, MakeData(root));

            // Act
            provider.Collect(_boss, _states);

            // Assert
            Assert.IsEmpty(_states,
                "La tarjeta sale del árbol: si un rediseño le saca los saltos, prometer la fuga " +
                "es describir una pelea que ya no existe.");
        }
    }
}
