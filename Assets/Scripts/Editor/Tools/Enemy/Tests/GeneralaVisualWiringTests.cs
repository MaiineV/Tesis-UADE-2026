using System.Linq;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>El fit y los props van con bounds <b>sintéticos</b> (son funciones puras); lo único que
    /// mira el <c>AssetDatabase</c> es que el arte fuente exista, la falla silenciosa del builder.</summary>
    [TestFixture]
    public class GeneralaVisualWiringTests
    {

        [Test]
        public void SourceArt_ExistsForTheBossAndForTheDice()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneralaAssetBuilder.BossArtPrefabPath),
                $"Falta el arte del jefe en '{GeneralaAssetBuilder.BossArtPrefabPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneralaAssetBuilder.DiceArtPrefabPath),
                $"Falta el dado 3D en '{GeneralaAssetBuilder.DiceArtPrefabPath}'.");
        }

        [Test]
        public void SourceArt_FiresTheImpactEventFromItsAttackClips()
        {
            // El windup de su ataque telegrafiado ancla el daño en esta key: sin ella cobra al cerrar
            // el step en vez de en el golpe, y un nodo que la espere para arrancar se cuelga.
            foreach (var clipPath in GeneralaAssetBuilder.AttackClipPaths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                Assert.IsNotNull(clip, $"No existe el clip '{clipPath}'.");
                Assert.IsTrue(
                    AnimationUtility.GetAnimationEvents(clip)
                        .Any(e => e.functionName == "PushFeedbackEvent" && e.stringParameter == "hit"),
                    $"'{clipPath}' no publica 'hit'.");
            }
        }

        /// <summary>Los props salieron por decisión de arte. Se afirma sobre el prefab que carga el
        /// juego y no sobre el builder: el builder los reponía en cada rebuild, así que lo que hay
        /// que fijar es que el rebuild ya no los traiga.</summary>
        [Test]
        public void TheBossPrefab_HangsNoProps()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GeneralaAssetBuilder.BossVisualPrefabPath);
            Assert.IsNotNull(prefab, $"No se pudo cargar {GeneralaAssetBuilder.BossVisualPrefabPath}.");

            foreach (var prop in new[] { "Cubilete", "Estandarte" })
                Assert.IsNull(prefab.transform.Find(prop),
                    $"Volvió el prop '{prop}': un rebuild del wrapper lo repuso.");
        }

        [Test]
        public void PortraitTextures_Exist_ForBothEntities()
        {
            // El import a Sprite lo hace el builder; acá alcanza con que la textura esté.
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Texture2D>(GeneralaAssetBuilder.BossPortraitTexturePath),
                $"Falta el retrato del jefe en '{GeneralaAssetBuilder.BossPortraitTexturePath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Texture2D>(GeneralaAssetBuilder.DicePortraitTexturePath),
                $"Falta el retrato del dado en '{GeneralaAssetBuilder.DicePortraitTexturePath}'.");
            Assert.AreNotEqual(
                GeneralaAssetBuilder.BossPortraitTexturePath,
                GeneralaAssetBuilder.DicePortraitTexturePath,
                "El jefe y sus dados no pueden compartir retrato: en la cola de turnos hay cinco " +
                "dados seguidos y se leerían como cinco copias de ella.");
        }

        [Test]
        public void BossSpec_WrapsTheTurretArt_WithABoxColliderAndItsOwnHealthBar()
        {
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            Assert.AreEqual(GeneralaAssetBuilder.BossArtPrefabPath, spec.ArtPrefabPath);
            Assert.AreEqual(GeneralaAssetBuilder.BossVisualPrefabPath, spec.OutputPrefabPath);
            Assert.IsTrue(spec.OutputPrefabPath.EndsWith(".prefab"),
                "El wrapper tiene que guardarse como prefab.");

            // Box y no Capsule: la torreta es ancha y baja y un capsule deja el cursor picando aire.
            Assert.AreEqual(ColliderKind.Box, spec.Collider);
            Assert.IsFalse(spec.AddHealthBar,
                "La jefa muestra vida en la BossBarView del HUD; la barra world-space la duplicaría.");
        }

        [Test]
        public void BossSpec_PaintsHerNavyWithBrassEpaulettes()
        {
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            // El cuerpo es Enemy__Base, que DiceBoss_Model.fbx remapea a Mat_Blue.
            Assert.IsTrue(spec.Retints.ContainsKey("Mat_Blue"), "El cuerpo quedó sin retintar.");
            var body = spec.Retints["Mat_Blue"].MidColor;
            Assert.IsTrue(body.HasValue, "El navy va por colores directos, no por slot de paleta.");
            Assert.Greater(body.Value.b, body.Value.r, "El cuerpo tiene que quedar azul.");
            Assert.Greater(body.Value.b, body.Value.g, "El cuerpo tiene que quedar azul.");

            // Enemy__Trim, el filo ornamental del dado, es la charretera de esta jefa.
            var brass = spec.Retints["Mat_LightBlue"].MidColor;
            Assert.IsTrue(brass.HasValue);
            Assert.Greater(brass.Value.r, brass.Value.b, "Las charreteras tienen que quedar doradas.");
        }

        [Test]
        public void BossSpec_LeavesTheWhiteTrimShared()
        {
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            // Mat_White es el galón sobre el navy: el único contraste claro del jefe.
            Assert.IsFalse(spec.Retints.ContainsKey("Mat_White"));
        }

        [Test]
        public void BossSpec_ClonesMaterialsIntoHerOwnFolder()
        {
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            // Sin carpeta propia los clones caen sobre los materiales compartidos del casino.
            Assert.AreEqual(GeneralaAssetBuilder.MaterialsFolder, spec.MaterialsFolder);
            Assert.IsTrue(spec.MaterialsFolder.StartsWith(BossVisualWrapperBuilder.DefaultMaterialsRoot));
        }

        [Test]
        public void DiceSpec_KeepsAHealthBar_BecauseTheDieHasFourHp()
        {
            var spec = GeneralaAssetBuilder.BuildDiceSpec(SampleDiceFit());

            Assert.AreEqual(GeneralaAssetBuilder.DiceArtPrefabPath, spec.ArtPrefabPath);
            Assert.AreEqual(GeneralaAssetBuilder.DiceVisualPrefabPath, spec.OutputPrefabPath);
            Assert.IsTrue(spec.AddHealthBar);
            Assert.AreEqual(ColliderKind.Box, spec.Collider, "Un dado es un cubo.");
        }

        [Test]
        public void DiceSpec_DoesNotRetint_BecauseItsMaterialsAreReassigned()
        {
            var spec = GeneralaAssetBuilder.BuildDiceSpec(SampleDiceFit());

            // El prefab apunta a un material inexistente: no hay nada que clonar por
            // nombre, los materiales se asignan en el post-proceso.
            Assert.IsTrue(spec.Retints == null || spec.Retints.Count == 0);
        }

        [Test]
        public void DiceMaterials_ArePartOfTheProject()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Material>(GeneralaAssetBuilder.DiceBodyMaterialPath),
                $"Falta '{GeneralaAssetBuilder.DiceBodyMaterialPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Material>(GeneralaAssetBuilder.DicePipMaterialPath),
                $"Falta '{GeneralaAssetBuilder.DicePipMaterialPath}'.");
        }

        [Test]
        public void ArtFit_ScalesUpArtShorterThanTheBossesAlreadyInTheGame()
        {
            // La torreta mide ~1.1 y los jefes del juego 1.8/2.
            var raw = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.8f));

            var fit = BossArtFitter.ArtFit.For(
                raw, GeneralaAssetBuilder.BossTargetHeight, GeneralaAssetBuilder.BossMaxWidth, 0.6f);

            Assert.Greater(fit.Scale, 1f, "Un jefe más chico que un enemigo común no se lee como jefe.");
        }

        [Test]
        public void ArtFit_RestsTheArtOnTheFloor()
        {
            // Pivot en el centro del volumen, como el cubo del dado.
            var raw = new Bounds(Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f));

            var fit = BossArtFitter.ArtFit.For(raw, 0.8f, 0.85f, 0.3f);

            Assert.AreEqual(0f, fit.Bounds.min.y, 0.0001f);
            Assert.AreEqual(0f, raw.min.y * fit.Scale + fit.Lift, 0.0001f);
        }

        [Test]
        public void ArtFit_CapsTheWidth_EvenIfItMissesTheTargetHeight()
        {
            // Arte ancho y bajo: llegar al alto pedido lo dejaría de 2 de ancho.
            var raw = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));

            var fit = BossArtFitter.ArtFit.For(raw, 2f, 1.1f, 0.6f);

            Assert.LessOrEqual(fit.Bounds.size.x, 1.1f + 0.0001f);
            Assert.Less(fit.Bounds.size.y, 2f, "Manda el ancho, no el alto.");
        }

        [Test]
        public void ArtFit_PutsTheBarOverTheHead_WithClearance()
        {
            var raw = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.8f));
            const float clearance = 0.6f;

            var fit = BossArtFitter.ArtFit.For(raw, 2f, 1.1f, clearance);

            Assert.AreEqual(fit.Bounds.max.y + clearance, fit.HealthBarOffset.y, 0.0001f);
            Assert.AreEqual(0f, fit.HealthBarOffset.x, 0.0001f);
        }

        [Test]
        public void ArtFit_Unmeasured_LeavesTheWrapperUntouched()
        {
            // El fallback cuando el arte no reporta bounds usables.
            var fit = BossArtFitter.ArtFit.Unmeasured(2.6f);

            Assert.AreEqual(1f, fit.Scale, 0.0001f);
            Assert.AreEqual(0f, fit.Lift, 0.0001f);
            Assert.AreEqual(2.6f, fit.HealthBarOffset.y, 0.0001f);
        }

        /// <summary>Fit de un jefe ya ajustado: 1 de ancho, 2 de alto, apoyado en el piso.</summary>
        private static BossArtFitter.ArtFit SampleBossFit() =>
            BossArtFitter.ArtFit.For(
                new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.8f)),
                GeneralaAssetBuilder.BossTargetHeight,
                GeneralaAssetBuilder.BossMaxWidth,
                0.6f);

        private static BossArtFitter.ArtFit SampleDiceFit() =>
            BossArtFitter.ArtFit.For(
                new Bounds(Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f)),
                GeneralaAssetBuilder.DiceTargetHeight,
                GeneralaAssetBuilder.DiceMaxWidth,
                0.3f);
    }
}
