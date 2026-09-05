using System;
using NUnit.Framework;
using Rollgeon.Effects.Concretes;
using Sirenix.Serialization;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Los efectos con RNG propio tienen que seguir teniendo uno despues de pasar por el
    /// serializer de Odin, que es como llegan desde los assets de items en runtime.
    /// </summary>
    /// <remarks>
    /// Regresion (ronda de testers 2026-09-04): Probability Drive tiraba
    /// <c>NullReferenceException</c> al resolver porque <c>Rng</c> era un campo
    /// <c>[NonSerialized]</c> con inicializador — Odin crea la instancia deserializada
    /// sin correr constructores ni inicializadores de campo, asi que el <c>new Random()</c>
    /// nunca ocurria. Los tests unitarios de cada efecto no lo veian porque construyen el
    /// efecto con <c>new</c>.
    /// </remarks>
    [TestFixture]
    public sealed class EffectRngOdinRoundTripTests
    {
        private static readonly Type[] EffectsWithRng =
        {
            typeof(EffChainStun),
            typeof(EffGrappleClaw),
            typeof(EffJoustCharge),
            typeof(EffProbabilityChoice),
            typeof(EffProbabilityDistortion),
            typeof(EffProbabilityJump),
            typeof(EffSpawnRuntimeTile),
        };

        [TestCaseSource(nameof(EffectsWithRng))]
        public void Rng_IsAvailableAfterOdinDeserialization(Type effectType)
        {
            // Arrange — la instancia autorada (con RNG) se serializa como en el asset.
            var authored = Activator.CreateInstance(effectType);
            byte[] bytes = SerializationUtility.SerializeValue(authored, DataFormat.JSON);

            // Act — la copia deserializada es la que corre en runtime.
            var restored = SerializationUtility.DeserializeValue<object>(bytes, DataFormat.JSON);

            // Assert
            Assert.IsInstanceOf(effectType, restored);
            var rng = effectType.GetProperty("Rng").GetValue(restored);
            Assert.IsNotNull(rng, $"{effectType.Name}.Rng tiene que existir tras deserializar");
        }

        [TestCaseSource(nameof(EffectsWithRng))]
        public void Rng_CanStillBeInjectedForDeterministicTests(Type effectType)
        {
            // Arrange — los tests de cada efecto inyectan una seed fija por la propiedad.
            var effect = Activator.CreateInstance(effectType);
            var seeded = new System.Random(7);

            // Act
            effectType.GetProperty("Rng").SetValue(effect, seeded);

            // Assert
            Assert.AreSame(seeded, effectType.GetProperty("Rng").GetValue(effect));
        }
    }
}
