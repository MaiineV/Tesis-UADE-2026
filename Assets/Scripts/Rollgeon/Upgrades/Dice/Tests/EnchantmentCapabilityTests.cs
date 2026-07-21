using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Attributes;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Tests del modelo de capabilities declarativas: lista en <see cref="EnchantmentSO"/>,
    /// consulta por tipo, y marcado <see cref="NotYetWiredAttribute"/> detectable (lo
    /// consume el warning del EnchantmentEditorWindow).
    /// </summary>
    [TestFixture]
    public class EnchantmentCapabilityTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private EnchantmentSO MakeEnchantmentWithCapabilities(params IEnchantmentCapability[] caps)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _created.Add(ench);
            typeof(EnchantmentSO).GetField("_capabilities", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, new List<IEnchantmentCapability>(caps));
            return ench;
        }

        [Test]
        public void Capabilities_QueryableByType()
        {
            // Arrange
            var ench = MakeEnchantmentWithCapabilities(
                new CapWildcard(),
                new CapForceRerollOnTurn { TriggerOnTurn = 4 });

            // Act / Assert — el patrón de consumo futuro: OfType<CapX>().
            Assert.AreEqual(2, ench.Capabilities.Count);
            Assert.IsTrue(ench.Capabilities.OfType<CapWildcard>().Any());
            Assert.AreEqual(4, ench.Capabilities.OfType<CapForceRerollOnTurn>().Single().TriggerOnTurn);
            Assert.IsFalse(ench.Capabilities.OfType<CapPreventHolding>().Any());
        }

        [Test]
        public void Capabilities_DefaultList_IsEmpty()
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _created.Add(ench);

            Assert.IsNotNull(ench.Capabilities);
            Assert.AreEqual(0, ench.Capabilities.Count);
        }

        [Test]
        public void AllSevenCapabilities_CarryNotYetWired()
        {
            // El tooling del editor detecta lo no-wireado por reflexión sobre este
            // atributo — las 7 capabilities deben declararlo hasta tener consumidor.
            var capabilityTypes = new[]
            {
                typeof(CapPreventHolding), typeof(CapWildcard), typeof(CapLadderStep),
                typeof(CapMimeticCopy), typeof(CapRerollKeepHighest),
                typeof(CapForceRerollOnTurn), typeof(CapAnchorAccumulate),
            };

            foreach (var type in capabilityTypes)
            {
                Assert.IsNotNull(type.GetCustomAttribute<NotYetWiredAttribute>(),
                    $"{type.Name} debe estar marcada [NotYetWired] hasta que exista su consumidor.");
                Assert.IsTrue(typeof(IEnchantmentCapability).IsAssignableFrom(type));
            }
        }
    }
}
