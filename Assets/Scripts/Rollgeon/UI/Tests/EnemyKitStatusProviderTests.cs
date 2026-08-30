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
        public void test_kit_a_registered_weakness_names_its_combo()
        {
            // Arrange — el registry y no el SO: es la fuente viva, la IA puede reescribirla.
            _registry.SetWeakness(_boss, "combo.poker", 1.3f);
            var provider = new EnemyKitStatusProvider(catalog: null, MakeData());

            // Act — WeaknessComboName y no Collect: la debilidad es un renglón del pie del
            // panel, no una tarjeta del costado.
            string combo = provider.WeaknessComboName(_boss);

            // Assert — sin catálogo de combos cae a la key cruda, que acá alcanza para fijar
            // que el combo que sale es el registrado.
            Assert.AreEqual("combo.poker", combo);
        }

        [Test]
        public void test_kit_no_weakness_registered_names_no_combo()
        {
            // Arrange
            var provider = new EnemyKitStatusProvider(catalog: null, MakeData());

            // Act
            string combo = provider.WeaknessComboName(_boss);

            // Assert
            Assert.IsNull(combo,
                "Un común sin debilidad no puede mostrar el renglón: un 'Debilidad:' sin combo " +
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
            Assert.AreEqual(StatusCardStyle.Trait, _states[0].Style,
                "El teleport es un rasgo del kit, no un estado transitorio: como Unit flotaría " +
                "sobre la cabeza todo el combate.");
        }

        [Test]
        public void test_kit_a_registered_weakness_becomes_a_trait_slot()
        {
            // Arrange — la piedrita rota del mockup: la debilidad vuelve al panel como slot de
            // la fila de abajo, no como renglón de texto.
            _registry.SetWeakness(_boss, "combo.poker", 1.3f);
            var provider = new EnemyKitStatusProvider(catalog: null, MakeData());

            // Act
            provider.Collect(_boss, _states);

            // Assert — sin catálogo el slot va sin sprite y la fila de abajo lo filtra sola;
            // lo que se fija acá es que el estado exista y diga el combo vigente.
            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(EnemyKitStatusProvider.WeaknessId, _states[0].Id);
            Assert.AreEqual("combo.poker", _states[0].DisplayName);
            Assert.AreEqual(StatusCardStyle.Trait, _states[0].Style,
                "La debilidad es un rasgo: como Unit flotaría sobre la cabeza todo el combate.");
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
