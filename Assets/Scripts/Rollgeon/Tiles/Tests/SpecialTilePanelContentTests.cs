using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Tiles.Visuals;
using Rollgeon.UI.HUD.Status;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// El panel estructurado de una casilla: header con identidad y tarjetas con los números —
    /// el mismo reparto que el panel de un enemigo, no un párrafo con los precios adentro.
    /// </summary>
    [TestFixture]
    public sealed class SpecialTilePanelContentTests
    {
        private readonly List<StatusIconState> _cards = new();

        private GameObject _go;
        private SpecialTileTooltipInfo _info;
        private SpecialTileDefinitionSO _definition;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TilePanelFixture");
            _info = _go.AddComponent<SpecialTileTooltipInfo>();
            _definition = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();

            // Key sin mapear a propósito: los asserts leen los fallbacks autorados y no la tabla,
            // así el resultado no depende del locale del editor.
            _definition.TileId = "TILE_TEST";
            _definition.NameKey = "test.tile.unmapped";
            _definition.DisplayName = "Fuego de Prueba";
            _definition.Category = TileEffectCategory.Damage;
            _cards.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void ElHeader_LlevaNombreYCategoria_NoUnParrafoConNumeros()
        {
            // Arrange
            _definition.EnterDamage = 8;
            _definition.TurnStartDamage = 12;
            _info.Bind(_definition, remainingRounds: 0);

            // Act
            var content = _info.BuildContent();

            // Assert
            Assert.AreEqual("Fuego de Prueba", content.Name,
                "El header no tituló con el nombre de la casilla.");
            Assert.IsFalse(string.IsNullOrEmpty(content.Type),
                "Una casilla de daño quedó sin fila de tipo: el 'Casilla · Daño' del header.");
            StringAssert.DoesNotContain("8", content.Text ?? string.Empty,
                "El párrafo del header lleva números incrustados: los precios van como dato en " +
                "las tarjetas, no en el texto.");
        }

        [Test]
        public void ElFuego_SonDosTarjetas_UnaPorPrecio()
        {
            // Arrange
            _definition.EnterDamage = 8;
            _definition.TurnStartDamage = 12;

            // Act
            SpecialTileCards.Append(_definition, _cards);

            // Assert
            Assert.AreEqual(2, _cards.Count, "El fuego cobra dos precios y son dos tarjetas.");
            Assert.AreEqual(8, _cards[0].Damage, "El primer precio es el de entrar.");
            Assert.AreEqual(12, _cards[1].Damage, "El segundo es el de empezar el turno encima.");
            Assert.IsFalse(string.IsNullOrEmpty(_cards[0].Eyebrow),
                "La primera tarjeta subraya el bloque con la etiqueta EFECTO.");
            Assert.IsNull(_cards[1].Eyebrow,
                "La etiqueta repetida en cada precio se lee como bloques distintos.");
        }

        [Test]
        public void ElCharcoElectrico_EsUnaTarjetaDelEstadoQueAplica()
        {
            // Arrange
            _definition.Category = TileEffectCategory.ApplyStatus;
            _definition.StatusKind = TileStatusKind.Stun;

            // Act
            SpecialTileCards.Append(_definition, _cards);

            // Assert
            Assert.AreEqual(1, _cards.Count);
            Assert.AreEqual("status.stun", _cards[0].Id,
                "La tarjeta del estado no apunta al status que la casilla aplica.");
            Assert.IsNull(_cards[0].Damage,
                "El stun no pega por sí mismo: un 0 en la tarjeta sería un precio inventado.");
        }

        [Test]
        public void UnPortal_NoAgregaTarjetas_SuDescripcionYaDiceTodo()
        {
            // Arrange
            _definition.Category = TileEffectCategory.Teleport;

            // Act
            SpecialTileCards.Append(_definition, _cards);

            // Assert
            Assert.AreEqual(0, _cards.Count,
                "Una casilla sin números sacó tarjeta: un recuadro vacío es peor que ninguno.");
        }

        [Test]
        public void LaVidaRestante_VaAlPie_NoAUnaTarjeta()
        {
            // Arrange
            _info.Bind(_definition, remainingRounds: 3);

            // Act
            var content = _info.BuildContent();

            // Assert
            StringAssert.Contains("3", content.Flavor,
                "Las rondas restantes no están en el pie: son del sistema de rondas, no un " +
                "precio de la casilla.");
        }
    }
}
