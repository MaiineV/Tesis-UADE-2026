using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities.Bosses;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Un retrato que no resuelve no rompe nada visible en el editor: <c>Portrait</c> queda en
    /// null y recién en Play la cola de turnos muestra el visual default. Lo frágil son los nombres
    /// de sub-sprite: si el arte re-slicea la hoja compartida, se renumeran en silencio.</summary>
    [TestFixture]
    public sealed class BossPortraitLibraryTests
    {
        // Los tres pools de piso: la unicidad de caras se mide contra el pool y no contra una
        // lista fija acá, porque quién juega y quién está en banco lo decide el pool.
        private const string Floor1PoolPath = "Assets/Rollgeon/Floor/BP_Floor1.asset";
        private const string Floor2PoolPath = "Assets/Rollgeon/Floor/BP_Floor2.asset";
        private const string Floor3PoolPath = "Assets/Rollgeon/Floor/BP_Floor3.asset";

        private const string CroupierDataPath = "Assets/Rollgeon/Enemies/ED_Boss_Croupier.asset";
        private const string CajeroDataPath = "Assets/Rollgeon/Enemies/ED_Boss_Cajero.asset";
        private const string TahurDataPath = "Assets/Rollgeon/Enemies/ED_Boss_Tahur.asset";
        private const string GeneralaDataPath = "Assets/Rollgeon/Enemies/ED_Boss_Generala.asset";
        private const string BandidaDataPath = "Assets/Rollgeon/Enemies/ED_Boss_Bandida.asset";
        private const string AnotadorDataPath = "Assets/Rollgeon/Enemies/ED_Boss_Anotador.asset";

        /// <summary>Cada jefe con la ficha en disco que lo representa: la ficha es lo que permite cruzar
        /// library contra pool.</summary>
        private static readonly (string Boss, string DataPath, Func<Sprite> Portrait)[] Bosses =
        {
            ("Croupier", CroupierDataPath, BossPortraitLibrary.Croupier),
            ("Cajero", CajeroDataPath, BossPortraitLibrary.Cajero),
            ("Tahur", TahurDataPath, BossPortraitLibrary.Tahur),
            ("Generala", GeneralaDataPath, BossPortraitLibrary.Generala),
            ("Bandida", BandidaDataPath, BossPortraitLibrary.Bandida),
            ("Anotador", AnotadorDataPath, BossPortraitLibrary.Anotador),
        };

        private static IEnumerable<TestCaseData> Portraits()
        {
            yield return new TestCaseData("Croupier", BossPortraitLibrary.SheetPath,
                BossPortraitLibrary.CroupierSpriteName);
            yield return new TestCaseData("Cajero", BossPortraitLibrary.CajeroPath,
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

        /// <summary>Los que hoy pueden salir en una run: entry del pool con <c>Enabled</c> y peso
        /// mayor a cero.</summary>
        private static HashSet<string> BossesInThePool()
        {
            var inPool = new HashSet<string>();

            foreach (var poolPath in new[] { Floor1PoolPath, Floor2PoolPath, Floor3PoolPath })
            {
                var pool = AssetDatabase.LoadAssetAtPath<BossPoolSO>(poolPath);
                Assert.IsNotNull(pool,
                    $"Falta el pool '{poolPath}': sin él no se puede saber qué jefes juegan, y la " +
                    "unicidad de retratos se mediría sobre nada.");

                foreach (var entry in pool.Entries)
                {
                    if (entry?.Boss == null || !entry.Enabled || entry.Weight <= 0f) continue;
                    inPool.Add(AssetDatabase.GetAssetPath(entry.Boss));
                }
            }

            return inPool;
        }

        /// <summary>La unicidad se exige entre los que están en el pool, no entre los seis: el retrato
        /// sigue al rig, así que dos jefes con el mismo arte 3D <b>tienen</b> que compartir cara, y
        /// empatar con un jefe que no puede salir no lo ve nadie.</summary>
        [Test]
        public void EveryBossInThePool_HasItsOwnPortrait()
        {
            var inPool = BossesInThePool();
            var playing = Bosses.Where(b => inPool.Contains(b.DataPath)).ToArray();

            // Sin esto el test pasaría en verde con la lista vacía: renombrar una ficha lo
            // desarmaría en silencio en vez de ponerlo rojo.
            Assert.IsNotEmpty(playing,
                "Ningún jefe de la library matcheó una entry activa del pool — o los pools se " +
                "quedaron sin jefes o las fichas cambiaron de path.");

            var sprites = playing.Select(b => b.Portrait()).ToArray();

            CollectionAssert.AllItemsAreNotNull(sprites,
                "Un jefe del pool quedó sin retrato: " +
                string.Join(", ", playing.Where(b => b.Portrait() == null).Select(b => b.Boss)));

            CollectionAssert.AllItemsAreUnique(sprites,
                "Dos jefes que pueden salir en la misma run comparten cara. En el pool: " +
                string.Join(", ", playing.Select(b => b.Boss)));
        }

        /// <summary>Los pares que comparten cara, y la condición que lo hace aceptable: uno está en
        /// banco.</summary>
        [Test]
        public void BossesOnTheSameRig_ShareTheirFace_WithOneOfThemBenched()
        {
            var inPool = BossesInThePool();

            Assert.AreEqual(BossPortraitLibrary.Tahur(), BossPortraitLibrary.Croupier(),
                "Croupier y Tahúr visten SunkedGrand_Animated: si dejan de compartir cara, uno de " +
                "los dos muestra en la cola una silueta que no es la que el jugador tiene enfrente.");
            CollectionAssert.DoesNotContain(inPool, TahurDataPath,
                "El Tahúr entró al pool compartiendo cara con el Croupier: en la misma run el " +
                "jugador no puede distinguirlos en la cola de turnos.");

            Assert.AreEqual(BossPortraitLibrary.Bandida(), BossPortraitLibrary.Cajero(),
                "Cajero y Bandida visten MechaBoss_Animated: si dejan de compartir cara, uno de " +
                "los dos muestra en la cola una silueta que no es la que el jugador tiene enfrente.");
            CollectionAssert.DoesNotContain(inPool, BandidaDataPath,
                "La Bandida entró al pool compartiendo cara con el Cajero: en la misma run el " +
                "jugador no puede distinguirlos en la cola de turnos.");
        }

        [Test]
        public void Builders_TakeTheirPortraitFromTheLibrary()
        {
            Assert.AreEqual(BossPortraitLibrary.SheetPath, CroupierAssetBuilder.PortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.CajeroPath, CajeroAssetBuilder.PortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.SheetPath, TahurAssetBuilder.PortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.GeneralaPath,
                GeneralaAssetBuilder.BossPortraitTexturePath);
            Assert.AreEqual(BossPortraitLibrary.BandidaPath, BandidaAssetBuilder.BossPortraitPath);
            Assert.AreEqual(BossPortraitLibrary.AnotadorPath, AnotadorAssetBuilder.PortraitTexturePath);
        }

        /// <summary>El rodillo y el dado son piezas de la sala con turno propio: con la cara del jefe, la
        /// cola de turnos muestra dos entradas idénticas y no se sabe a cuál se le pega.</summary>
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
