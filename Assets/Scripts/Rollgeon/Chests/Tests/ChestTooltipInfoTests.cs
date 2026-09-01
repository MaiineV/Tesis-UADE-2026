using NUnit.Framework;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    /// <summary>
    /// El tooltip del cofre es a prueba de mímico: el real y el disfrazado comparten spawn,
    /// componente y contenido — el hover no puede delatar nada.
    /// </summary>
    [TestFixture]
    public sealed class ChestTooltipInfoTests
    {
        private GameObject _realGo;
        private GameObject _mimicGo;

        [SetUp]
        public void SetUp()
        {
            _realGo = new GameObject("ChestReal");
            _mimicGo = new GameObject("ChestMimicDisguised");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_realGo);
            Object.DestroyImmediate(_mimicGo);
        }

        [Test]
        public void ElCofreRealYElMimicoCamuflado_DicenExactamenteLoMismo()
        {
            // Arrange — mismo tier, que es el ÚNICO dato que el tooltip puede leer. IsMimic y el
            // EnemyDataSO del mímico (cuyas keys ChestMimic01.* son el disfraz al descubierto)
            // no entran acá: cualquier diferencia convierte el hover en detector gratis.
            var real = _realGo.AddComponent<ChestTooltipInfo>();
            var mimic = _mimicGo.AddComponent<ChestTooltipInfo>();
            real.Bind(ItemRarity.Rare);
            mimic.Bind(ItemRarity.Rare);

            // Act
            var realContent = real.BuildContent();
            var mimicContent = mimic.BuildContent();

            // Assert
            Assert.AreEqual(realContent.Name, mimicContent.Name,
                "El nombre difiere entre cofre y mímico camuflado: hover = detector de mímicos.");
            Assert.AreEqual(realContent.Type, mimicContent.Type,
                "La rareza difiere: mismo tier tiene que leer idéntico en los dos.");
            Assert.AreEqual(realContent.Text, mimicContent.Text,
                "La descripción difiere: el disfraz se cae con un mouse.");
        }

        [Test]
        public void ElPanel_NoLlevaVitales()
        {
            // Arrange — un mímico golpeado por otro enemigo queda clavado en 1 HP mientras sigue
            // disfrazado; un cofre real con ese golpe se rompe. Mostrar HP acá cantaría el truco.
            var info = _realGo.AddComponent<ChestTooltipInfo>();
            info.Bind(ItemRarity.Common);

            // Act
            var content = info.BuildContent();

            // Assert
            Assert.IsFalse(content.HasVitals,
                "El tooltip del cofre muestra vida: un cofre en 1 HP canta el mímico igual que " +
                "decir 'Mímico'. La health bar de arriba ya es problema de otro.");
        }
    }
}
