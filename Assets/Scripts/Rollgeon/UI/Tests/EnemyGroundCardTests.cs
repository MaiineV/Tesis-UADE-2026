using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.UI.HUD.Status;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// La caja EN EL PISO: el hover de la celda es uno solo aunque la pisen dos cosas, así que el
    /// panel del bicho suma la casilla como una tarjeta más al final de su columna.
    /// </summary>
    [TestFixture]
    public sealed class EnemyGroundCardTests
    {
        private readonly List<SpecialTileInfo> _scratch = new();
        private readonly List<StatusIconState> _cards = new();

        private FakeSpecialTileService _tiles;
        private SpecialTileDefinitionSO _fire;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _boss = Guid.NewGuid();
            _tiles = new FakeSpecialTileService();
            ServiceLocator.AddService<ISpecialTileService>(_tiles);

            _fire = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _fire.TileId = "TILE_TEST_FIRE";
            _fire.NameKey = "test.tile.unmapped";
            _fire.DisplayName = "Fuego de Prueba";
            _fire.EnterDamage = 6;
            _fire.TurnStartDamage = 10;

            _cards.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_fire);
            ServiceLocator.Clear();
        }

        [Test]
        public void ElBichoParadoEnFuego_SumaLaCajaEnElPiso_ConElPrecioDeQuedarse()
        {
            // Arrange
            _tiles.Under.Add(Info(_fire));

            // Act
            EnemyStatusIconsView.AppendGroundCards(_boss, _scratch, _cards);

            // Assert
            Assert.AreEqual(1, _cards.Count, "El panel no sumó la caja de la casilla pisada.");
            Assert.AreEqual("Fuego de Prueba", _cards[0].DisplayName,
                "La caja no titula con el nombre de la casilla.");
            Assert.AreEqual(10, _cards[0].Damage,
                "El precio que le importa a quien lee al bicho parado ahí es el de QUEDARSE " +
                "(turn start), no el de entrar.");
            Assert.IsFalse(string.IsNullOrEmpty(_cards[0].Eyebrow),
                "La caja no lleva la etiqueta EN EL PISO que la separa del resto de la columna.");
        }

        [Test]
        public void ElBichoEnUnaCasillaComun_NoSumaNada()
        {
            // Act — el fake no tiene nada bajo el bicho.
            EnemyStatusIconsView.AppendGroundCards(_boss, _scratch, _cards);

            // Assert
            Assert.AreEqual(0, _cards.Count,
                "Sin casilla especial abajo no hay caja: un EN EL PISO vacío es ruido.");
        }

        private SpecialTileInfo Info(SpecialTileDefinitionSO def)
            => new SpecialTileInfo(Guid.NewGuid(), def, new List<GridCoord> { new GridCoord(1, 1) },
                                   remainingRounds: 0, ownerGuid: Guid.Empty,
                                   linkedInstanceId: Guid.Empty);

        private sealed class FakeSpecialTileService : ISpecialTileService
        {
            public readonly List<SpecialTileInfo> Under = new();

            public Guid Place(SpecialTileDefinitionSO definition, IEnumerable<GridCoord> coords,
                              TilePlacementOptions options = default) => Guid.Empty;

            public Guid CreateRuntime(SpecialTileDefinitionSO definition, GridCoord coord,
                                      RuntimeTileRequest request, out TilePlacementError error)
            {
                error = default;
                return Guid.Empty;
            }

            public bool TryGetTileAt(GridCoord coord, out SpecialTileInfo info)
            {
                info = default;
                return false;
            }

            public IEnumerable<SpecialTileInfo> ActiveInstances() => Under;

            public void Remove(Guid instanceId) { }

            public void MoveInstance(Guid instanceId, IEnumerable<GridCoord> newCoords) { }

            public void ResolveEntries(Guid entity, IReadOnlyList<GridCoord> enteredCoords,
                                       TileMovementKind kind) { }

            public bool HasAnySpecialTiles => Under.Count > 0;

            public void CollectUnder(Guid entity, List<SpecialTileInfo> into)
            {
                into.Clear();
                into.AddRange(Under);
            }
        }
    }
}
