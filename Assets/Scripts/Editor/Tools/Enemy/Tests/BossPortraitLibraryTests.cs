using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Fija el mapeo retrato ↔ jefe de <see cref="BossPortraitLibrary"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un retrato que no resuelve <b>no rompe nada visible en el editor</b>: el campo
    /// <c>Portrait</c> queda en null, el builder no falla, el prefab abre igual, y recién en Play
    /// se nota que la cola de turnos muestra el visual default. Es exactamente la clase de falla
    /// que un test tiene que atrapar, porque nadie la va a ver revisando el inspector.
    /// </para>
    /// <para>
    /// Los tres nombres de sub-sprite de la hoja compartida son la parte frágil: si el arte
    /// re-slicea <c>RollGeonSprites.png</c>, los nombres <c>RollGeonSprites_N</c> se renumeran y
    /// tres jefes pierden la cara en silencio.
    /// </para>
    /// </remarks>
    [TestFixture]
    public sealed class BossPortraitLibraryTests
    {
        private static IEnumerable<TestCaseData> Portraits()
        {
            yield return new TestCaseData("Croupier", BossPortraitLibrary.SheetPath,
                BossPortraitLibrary.CroupierSpriteName);
            yield return new TestCaseData("Cajero", BossPortraitLibrary.SheetPath,
                BossPortraitLibrary.CajeroSpriteName);
            yield return new TestCaseData("Tahur", BossPortraitLibrary.SheetPath,
                BossPortraitLibrary.TahurSpriteName);
            yield return new TestCaseData("Generala", BossPortraitLibrary.GeneralaPath,
                BossPortraitLibrary.GeneralaSpriteName);
            yield return new TestCaseData("Bandida", BossPortraitLibrary.BandidaPath,
                BossPortraitLibrary.BandidaSpriteName);
            yield return new TestCaseData("Anotador", BossPortraitLibrary.AnotadorPath,
                BossPortraitLibrary.AnotadorSpriteName);
        }

        [TestCaseSource(nameof(Portraits))]
        public void EveryBossPortrait_ResolvesToItsSubSprite(string boss, string texturePath,
                                                             string spriteName)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath),
                $"Falta la textura del retrato de {boss} en '{texturePath}'.");

            Assert.IsNotNull(SpriteImportUtility.FindSubSprite(texturePath, spriteName),
                $"'{texturePath}' no expone el sub-sprite '{spriteName}': {boss} se quedaría sin " +
                "cara en la cola de turnos y en la barra de vida, sin ningún error que lo avise.");
        }

        [Test]
        public void EveryBoss_HasItsOwnPortrait()
        {
            var sprites = new[]
            {
                BossPortraitLibrary.Croupier(), BossPortraitLibrary.Cajero(),
                BossPortraitLibrary.Tahur(), BossPortraitLibrary.Generala(),
                BossPortraitLibrary.Bandida(), BossPortraitLibrary.Anotador(),
            };

            CollectionAssert.AllItemsAreNotNull(sprites);

            // Dos jefes compartiendo cara es el bug que este mapeo vino a arreglar: con los glifos
            // de casino se leían todos igual de genéricos.
            CollectionAssert.AllItemsAreUnique(sprites);
        }

        [Test]
        public void Builders_TakeTheirPortraitFromTheLibrary()
        {
            Assert.AreEqual(BossPortraitLibrary.SheetPath, CroupierAssetBuilder.PortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.SheetPath, CajeroAssetBuilder.PortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.SheetPath, TahurAssetBuilder.PortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.GeneralaPath,
                GeneralaAssetBuilder.BossPortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.BandidaPath, BandidaAssetBuilder.BossPortraitPath);
            Assert.AreEqual(BossPortraitLibrary.AnotadorPath, AnotadorAssetBuilder.PortraitTexturePath);
        }

        /// <summary>
        /// El rodillo de la Bandida y el dado de la Generala <b>no</b> son personajes: son piezas de
        /// la sala con turno propio. Si tomaran la cara del jefe, la cola de turnos mostraría dos
        /// entradas idénticas y el jugador no sabría a cuál le está pegando.
        /// </summary>
        [Test]
        public void PropEnemies_KeepTheirOwnSymbol()
        {
            Assert.AreNotEqual(BandidaAssetBuilder.BossPortraitPath,
                BandidaAssetBuilder.ReelPortraitPath);
            Assert.AreNotEqual(GeneralaAssetBuilder.BossPortraitTexturePath,
                GeneralaAssetBuilder.DicePortraitTexturePath);
        }
    }
}
