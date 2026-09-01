using System;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Entities.Traits;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// La fila de familia del panel de un enemigo. Sin escena: es una función pura.
    /// </summary>
    /// <remarks>
    /// Los asserts miran la ESTRUCTURA y no las palabras — mismo criterio que
    /// <see cref="AIIntentTextTests"/>: el locale de EditMode sale de un PlayerPref, y comparar
    /// contra el castellano pondría esto en rojo con el editor en inglés.
    /// </remarks>
    [TestFixture]
    public class EnemyArchetypeTextTests
    {
        [Test]
        public void SinFamilia_YSinSerJefe_NoHayFila()
        {
            Assert.IsEmpty(EnemyArchetypeText.Describe(EnemyArchetype.Unset, isBoss: false));
        }

        [Test]
        public void SinFamilia_PeroJefe_IgualDiceQueEsJefe()
        {
            // Cinco de los seis jefes van a vivir sin familia autorada un rato: "Jefe" solo es
            // verdad, y es mejor que una fila vacía.
            Assert.IsNotEmpty(EnemyArchetypeText.Describe(EnemyArchetype.Unset, isBoss: true));
        }

        [Test]
        public void UnJefe_LlevaSuFamiliaAdentroDelPrefijo()
        {
            string comun = EnemyArchetypeText.Describe(EnemyArchetype.Ranged, isBoss: false);
            string jefe = EnemyArchetypeText.Describe(EnemyArchetype.Ranged, isBoss: true);

            Assert.IsNotEmpty(comun);
            StringAssert.Contains(comun, jefe);
            Assert.AreNotEqual(comun, jefe, "El prefijo de jefe no agregó nada.");
        }

        [Test]
        public void LasTresFamilias_SeLeenDistinto()
        {
            var lineas = new[] { EnemyArchetype.Melee, EnemyArchetype.Ranged, EnemyArchetype.Support }
                .Select(a => EnemyArchetypeText.Describe(a, isBoss: false))
                .ToArray();

            CollectionAssert.AllItemsAreNotNull(lineas);
            CollectionAssert.AllItemsAreUnique(lineas);
        }

        /// <summary>
        /// Una familia agregada al enum sin su key no falla: sale una fila vacía. Esto lo detecta.
        /// </summary>
        [Test]
        public void CadaFamiliaDelEnum_TieneKeyYEstaEnAll()
        {
            foreach (EnemyArchetype archetype in Enum.GetValues(typeof(EnemyArchetype)))
            {
                if (archetype == EnemyArchetype.Unset) continue;

                string key = EnemyArchetypeKeys.KeyFor(archetype);
                Assert.IsNotNull(key, $"{archetype} no tiene key.");
                CollectionAssert.Contains(EnemyArchetypeKeys.All, key,
                    $"{archetype} tiene key pero no está en All, así que el guard no la mira.");
            }
        }
    }
}
