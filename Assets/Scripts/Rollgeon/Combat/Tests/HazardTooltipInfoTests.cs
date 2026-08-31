using NUnit.Framework;
using Rollgeon.Combat.Threat;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// El contenido del panel de un hazard: header con identidad, el golpe como dato y la
    /// cadencia al pie — nunca números incrustados en el texto.
    /// </summary>
    [TestFixture]
    public sealed class HazardTooltipInfoTests
    {
        private GameObject _go;
        private HazardTooltipInfo _info;
        private HazardDefinitionSO _definition;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HazardTooltipFixture");
            _info = _go.AddComponent<HazardTooltipInfo>();

            _definition = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _definition.hideFlags = HideFlags.HideAndDontSave;
            // Keys sin mapear a propósito: los asserts leen el fallback autorado y no la tabla,
            // así el resultado no depende del locale del editor.
            _definition.DisplayName = "Escarcha de Prueba";
            _definition.NameKey = "test.hazard.unmapped";
            _definition.DescriptionKey = "test.hazard.unmapped";
            _info.Bind(_definition);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void ElHeader_TitulaConElNombreYLaFilaDeTipo()
        {
            // Act
            var content = _info.BuildContent();

            // Assert
            Assert.AreEqual("Escarcha de Prueba", content.Name,
                "El header no cae al nombre de autor cuando la key no está en tabla.");
            Assert.IsFalse(string.IsNullOrEmpty(content.Type),
                "Un hazard sin fila de tipo se lee como una casilla más — 'Peligro de sala' " +
                "es lo que lo separa del terreno.");
        }

        [Test]
        public void ElGolpe_EsUnaTarjeta_YSinDanoNoHayTarjeta()
        {
            // Arrange + Act — con daño: una tarjeta con el número como dato.
            _definition.Damage = 6;
            var withDamage = _info.CollectCards();

            // Assert
            Assert.AreEqual(1, withDamage.Count);
            Assert.AreEqual(6, withDamage[0].Damage,
                "El golpe viaja como dato, no dentro de una frase.");

            // Arrange + Act — la ficha del Cajero no pega: su valor lo cuenta la descripción.
            _definition.Damage = 0;
            var withoutDamage = _info.CollectCards();

            // Assert
            Assert.AreEqual(0, withoutDamage.Count,
                "Un hazard sin daño sacó tarjeta: un 0 en el panel es un precio inventado.");
        }

        [Test]
        public void LaCadencia_VaAlPie_SoloEnLosDeCiclo()
        {
            // Arrange — lluvia: marca y cobra cada N rondas.
            _definition.Trigger = HazardTriggerMode.CycleTelegraph;
            _definition.CycleRounds = 2;

            // Act + Assert
            StringAssert.Contains("2", _info.BuildContent().Flavor,
                "La cadencia del ciclo no está en el pie: el jugador no sabe cuándo cae.");

            // Arrange — hielo: dispara al pisar, no tiene ciclo que anunciar.
            _definition.Trigger = HazardTriggerMode.OnEnter;

            // Act + Assert
            Assert.IsNull(_info.BuildContent().Flavor,
                "Un hazard de pisada anunció cadencia de ciclo: promete un reloj que no corre.");
        }
    }
}
