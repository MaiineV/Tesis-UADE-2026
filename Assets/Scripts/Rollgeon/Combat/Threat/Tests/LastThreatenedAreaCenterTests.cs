using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat.Tests
{
    /// <summary>
    /// Regresión del bug de playtest: el proyectil/giro de impacto de un jefe telegrafiado
    /// apuntaba a la posición VIVA del jugador en vez del área congelada — un whiff (el jugador
    /// esquivó) se veía como que igual conectó.
    /// </summary>
    [TestFixture]
    public class LastThreatenedAreaCenterTests
    {
        [Test]
        public void ComputeCenter_ShouldReturnExactAnchor_ForSymmetricDiamond()
        {
            // Arrange — diamante Manhattan<=1 centrado en (5,5): (5,5),(4,5),(6,5),(5,4),(5,6).
            var tiles = new List<GridCoord>
            {
                new GridCoord(5, 5), new GridCoord(4, 5), new GridCoord(6, 5),
                new GridCoord(5, 4), new GridCoord(5, 6),
            };

            // Act
            var center = LastThreatenedAreaCenter.ComputeCenter(tiles);

            // Assert — el promedio de una forma simétrica cae exacto en el ancla original.
            Assert.AreEqual(new GridCoord(5, 5), center);
        }

        [Test]
        public void ComputeCenter_ShouldReturnDefault_WhenTilesIsEmptyOrNull()
        {
            Assert.AreEqual(default(GridCoord), LastThreatenedAreaCenter.ComputeCenter(new List<GridCoord>()));
            Assert.AreEqual(default(GridCoord), LastThreatenedAreaCenter.ComputeCenter(null));
        }

        [Test]
        public void TryGet_ShouldReturnFalse_WhenNothingWasSet()
        {
            Assert.IsFalse(LastThreatenedAreaCenter.TryGet(Guid.NewGuid(), out _));
        }

        [Test]
        public void SetThenTryGet_ShouldRoundTripTheCenter()
        {
            // Arrange
            var owner = Guid.NewGuid();
            var expected = new GridCoord(3, -2);

            // Act
            LastThreatenedAreaCenter.Set(owner, expected);
            bool found = LastThreatenedAreaCenter.TryGet(owner, out var actual);

            // Assert
            Assert.IsTrue(found);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void TryGet_ShouldNotConsume_SoASecondReaderInTheSameTurnStillSeesIt()
        {
            // Arrange — dos lectores en el mismo ciclo: el FaceTarget del nodo de ejecución Y,
            // para el Artillery, el VFX de impacto que dispara el mismo windup.
            var owner = Guid.NewGuid();
            LastThreatenedAreaCenter.Set(owner, new GridCoord(1, 1));

            // Act
            bool firstRead = LastThreatenedAreaCenter.TryGet(owner, out var first);
            bool secondRead = LastThreatenedAreaCenter.TryGet(owner, out var second);

            // Assert
            Assert.IsTrue(firstRead);
            Assert.IsTrue(secondRead, "Un segundo lector en el mismo turno no debe encontrar la entrada vacía.");
            Assert.AreEqual(first, second);
        }

        [Test]
        public void Set_ShouldOverwritePreviousValue_ForTheSameOwner()
        {
            // Arrange — el próximo ciclo de telegraph de la misma fuente pisa el centro viejo,
            // así que un lector tardío nunca ve un dato de un ciclo anterior.
            var owner = Guid.NewGuid();
            LastThreatenedAreaCenter.Set(owner, new GridCoord(0, 0));

            // Act
            LastThreatenedAreaCenter.Set(owner, new GridCoord(9, 9));
            LastThreatenedAreaCenter.TryGet(owner, out var center);

            // Assert
            Assert.AreEqual(new GridCoord(9, 9), center);
        }

        [Test]
        public void Set_ShouldBeNoOp_ForEmptyGuid()
        {
            // Act
            LastThreatenedAreaCenter.Set(Guid.Empty, new GridCoord(1, 1));

            // Assert
            Assert.IsFalse(LastThreatenedAreaCenter.TryGet(Guid.Empty, out _));
        }
    }
}
