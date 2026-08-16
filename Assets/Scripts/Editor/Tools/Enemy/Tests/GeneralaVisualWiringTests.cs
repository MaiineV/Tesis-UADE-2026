using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Tests de la capa visual de La Generala: los specs de wrapper, el fit del arte y la colocación
    /// de los props.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El fit y los props se testean con bounds <b>sintéticos</b>: son funciones puras, y pasarles
    /// medidas a mano deja explícito qué se espera (apoyar en el piso, tocar el casco, no pasarse de
    /// ancho) sin depender de que el artista no reexporte nada.
    /// </para>
    /// <para>
    /// Lo único que sí mira el <c>AssetDatabase</c> es que el arte fuente exista: es la falla real y
    /// silenciosa del builder — si alguien mueve <c>RangedMachine_Animated</c>, el jefe vuelve a salir
    /// sin visual y nada lo grita hasta el playtest.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class GeneralaVisualWiringTests
    {
        // ======================================================================
        // Arte fuente
        // ======================================================================

        [Test]
        public void SourceArt_ExistsForTheBossAndForTheDice()
        {
            // Assert
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneralaAssetBuilder.BossArtPrefabPath),
                $"Falta el arte del jefe en '{GeneralaAssetBuilder.BossArtPrefabPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneralaAssetBuilder.DiceArtPrefabPath),
                $"Falta el dado 3D en '{GeneralaAssetBuilder.DiceArtPrefabPath}'.");
        }

        [Test]
        public void CupProp_Exists_BecauseTheCupIsHerSecondAttack()
        {
            // Assert — el cubilete no es decorado: es el 3×3 de los turnos impares.
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(GeneralaAssetBuilder.CupPropPrefabPath),
                $"Falta la caja de dados en '{GeneralaAssetBuilder.CupPropPrefabPath}'.");
        }

        [Test]
        public void PortraitTextures_Exist_ForBothEntities()
        {
            // Assert — el import a Sprite lo hace el builder; acá alcanza con que la textura esté.
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

        // ======================================================================
        // Spec del jefe
        // ======================================================================

        [Test]
        public void BossSpec_WrapsTheTurretArt_WithABoxColliderAndItsOwnHealthBar()
        {
            // Act
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            // Assert
            Assert.AreEqual(GeneralaAssetBuilder.BossArtPrefabPath, spec.ArtPrefabPath);
            Assert.AreEqual(GeneralaAssetBuilder.BossVisualPrefabPath, spec.OutputPrefabPath);
            Assert.IsTrue(spec.OutputPrefabPath.EndsWith(".prefab"),
                "El wrapper tiene que guardarse como prefab.");

            // Box y no Capsule: la torreta es ancha y baja y un capsule deja el cursor picando aire.
            Assert.AreEqual(ColliderKind.Box, spec.Collider);
            Assert.IsTrue(spec.AddHealthBar);
        }

        [Test]
        public void BossSpec_PaintsHerNavyWithBrassEpaulettes()
        {
            // Act
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            // Assert — el cuerpo es Enemy__Base, que DiceBoss_Model.fbx remapea a Mat_Blue.
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
            // Act
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            // Assert — Mat_White es el galón sobre el navy: retintarlo sería clonar un asset para
            // dejarlo igual, y perder el único contraste claro del jefe.
            Assert.IsFalse(spec.Retints.ContainsKey("Mat_White"));
        }

        [Test]
        public void BossSpec_ClonesMaterialsIntoHerOwnFolder()
        {
            // Act
            var spec = GeneralaAssetBuilder.BuildBossSpec(SampleBossFit(), null);

            // Assert — sin carpeta propia los clones caen sobre los materiales compartidos del casino.
            Assert.AreEqual(GeneralaAssetBuilder.MaterialsFolder, spec.MaterialsFolder);
            Assert.IsTrue(spec.MaterialsFolder.StartsWith(BossVisualWrapperBuilder.DefaultMaterialsRoot));
        }

        [Test]
        public void BossSpec_PutsTheBarAboveTheFittedArt()
        {
            // Arrange
            var fit = SampleBossFit();

            // Act
            var spec = GeneralaAssetBuilder.BuildBossSpec(fit, null);

            // Assert — la barra sigue al arte medido, no a un offset fijo de humanoide.
            Assert.AreEqual(fit.HealthBarOffset, spec.HealthBarOffset);
            Assert.Greater(spec.HealthBarOffset.y, fit.Bounds.max.y,
                "La barra tiene que quedar sobre el jefe, no dentro.");
        }

        // ======================================================================
        // Spec del dado
        // ======================================================================

        [Test]
        public void DiceSpec_KeepsAHealthBar_BecauseTheDieHasFourHp()
        {
            // Act
            var spec = GeneralaAssetBuilder.BuildDiceSpec(SampleDiceFit());

            // Assert — romper el dado es la mecánica: sin barra no se sabe cuánto falta.
            Assert.AreEqual(GeneralaAssetBuilder.DiceArtPrefabPath, spec.ArtPrefabPath);
            Assert.AreEqual(GeneralaAssetBuilder.DiceVisualPrefabPath, spec.OutputPrefabPath);
            Assert.IsTrue(spec.AddHealthBar);
            Assert.AreEqual(ColliderKind.Box, spec.Collider, "Un dado es un cubo.");
        }

        [Test]
        public void DiceSpec_DoesNotRetint_BecauseItsMaterialsAreReassigned()
        {
            // Act
            var spec = GeneralaAssetBuilder.BuildDiceSpec(SampleDiceFit());

            // Assert — el prefab de la bandeja apunta a un material que no existe: el retinte por
            // nombre no tiene nada que clonar, los materiales se asignan en el post-proceso.
            Assert.IsTrue(spec.Retints == null || spec.Retints.Count == 0);
        }

        [Test]
        public void DiceMaterials_ArePartOfTheProject()
        {
            // Assert
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Material>(GeneralaAssetBuilder.DiceBodyMaterialPath),
                $"Falta '{GeneralaAssetBuilder.DiceBodyMaterialPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Material>(GeneralaAssetBuilder.DicePipMaterialPath),
                $"Falta '{GeneralaAssetBuilder.DicePipMaterialPath}'.");
        }

        // ======================================================================
        // Fit del arte
        // ======================================================================

        [Test]
        public void ArtFit_ScalesUpArtShorterThanTheBossesAlreadyInTheGame()
        {
            // Arrange — la torreta mide ~1.1 y los jefes del juego 1.8/2.
            var raw = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.8f));

            // Act
            var fit = GeneralaAssetBuilder.ArtFit.For(
                raw, GeneralaAssetBuilder.BossTargetHeight, GeneralaAssetBuilder.BossMaxWidth, 0.6f);

            // Assert
            Assert.Greater(fit.Scale, 1f, "Un jefe más chico que un enemigo común no se lee como jefe.");
        }

        [Test]
        public void ArtFit_RestsTheArtOnTheFloor()
        {
            // Arrange — pivot en el centro del volumen, como el cubo del dado.
            var raw = new Bounds(Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f));

            // Act
            var fit = GeneralaAssetBuilder.ArtFit.For(raw, 0.8f, 0.85f, 0.3f);

            // Assert — sin la levantada, medio dado queda enterrado en el piso.
            Assert.AreEqual(0f, fit.Bounds.min.y, 0.0001f);
            Assert.AreEqual(0f, raw.min.y * fit.Scale + fit.Lift, 0.0001f);
        }

        [Test]
        public void ArtFit_CapsTheWidth_EvenIfItMissesTheTargetHeight()
        {
            // Arrange — arte ancho y bajo: llegar al alto pedido lo dejaría de 2 de ancho.
            var raw = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));

            // Act
            var fit = GeneralaAssetBuilder.ArtFit.For(raw, 2f, 1.1f, 0.6f);

            // Assert — un jefe más ancho que la casilla no se lee en qué tile está parado.
            Assert.LessOrEqual(fit.Bounds.size.x, 1.1f + 0.0001f);
            Assert.Less(fit.Bounds.size.y, 2f, "Manda el ancho, no el alto.");
        }

        [Test]
        public void ArtFit_PutsTheBarOverTheHead_WithClearance()
        {
            // Arrange
            var raw = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.8f));
            const float clearance = 0.6f;

            // Act
            var fit = GeneralaAssetBuilder.ArtFit.For(raw, 2f, 1.1f, clearance);

            // Assert
            Assert.AreEqual(fit.Bounds.max.y + clearance, fit.HealthBarOffset.y, 0.0001f);
            Assert.AreEqual(0f, fit.HealthBarOffset.x, 0.0001f);
        }

        [Test]
        public void ArtFit_Unmeasured_LeavesTheWrapperUntouched()
        {
            // Act — el fallback cuando el arte no reporta bounds usables.
            var fit = GeneralaAssetBuilder.ArtFit.Unmeasured(2.6f);

            // Assert — escala 1 y sin levantar: lo que ya dejó el wrapper.
            Assert.AreEqual(1f, fit.Scale, 0.0001f);
            Assert.AreEqual(0f, fit.Lift, 0.0001f);
            Assert.AreEqual(2.6f, fit.HealthBarOffset.y, 0.0001f);
        }

        // ======================================================================
        // Props
        // ======================================================================

        [Test]
        public void CupProp_RestsOnTheFloorTouchingHerRightSide()
        {
            // Arrange — bounds con el pivot corrido, como los trae el prop de la sala.
            var fit = SampleBossFit();
            var cup = new Bounds(new Vector3(1.5f, 0.2f, -1.5f), new Vector3(0.5f, 0.4f, 0.5f));

            // Act
            var prop = GeneralaAssetBuilder.BuildCupProp(fit, cup);
            float scale = prop.LocalScale.x;

            // Assert
            Assert.AreEqual(GeneralaAssetBuilder.CupPropPrefabPath, prop.PrefabPath);
            Assert.AreEqual(scale, prop.LocalScale.y, 0.0001f, "La escala tiene que ser uniforme.");

            Assert.AreEqual(0f, prop.LocalPosition.y + cup.min.y * scale, 0.001f,
                "El cubilete tiene que apoyar en el piso, no flotar.");
            Assert.AreEqual(fit.Bounds.max.x, prop.LocalPosition.x + cup.min.x * scale, 0.001f,
                "La cara izquierda del cubilete tiene que tocar el costado del casco.");
            Assert.AreEqual(GeneralaAssetBuilder.CupHeight, cup.size.y * scale, 0.001f,
                "El cubilete tiene que quedar del alto pedido.");
        }

        [Test]
        public void BannerProp_GoesBehindHerBack_OnTheFloor()
        {
            // Arrange
            var fit = SampleBossFit();
            var banner = new Bounds(new Vector3(3.4f, 2.5f, -5.2f), new Vector3(1f, 1.5f, 0.1f));

            // Act
            bool ok = GeneralaAssetBuilder.TryBuildBannerProp(fit, banner, out var prop);
            float scale = prop != null ? prop.LocalScale.x : 0f;

            // Assert
            Assert.IsTrue(ok, "Un banner de 1.5 de alto entra de sobra en el rango de escala.");
            Assert.AreEqual(fit.Bounds.min.z, prop.LocalPosition.z + banner.max.z * scale, 0.001f,
                "El estandarte va a la espalda: el arte mira a +Z.");
            Assert.AreEqual(0f, prop.LocalPosition.y + banner.min.y * scale, 0.001f,
                "Un estandarte flotando en el aire es peor que no tener estandarte.");
            Assert.AreEqual(GeneralaAssetBuilder.BannerHeight, banner.size.y * scale, 0.001f);
        }

        [Test]
        public void BannerProp_IsSkipped_WhenItWouldNeedAnAbsurdScale()
        {
            // Arrange
            var fit = SampleBossFit();
            var tiny = new Bounds(Vector3.zero, new Vector3(0.05f, 0.05f, 0.01f));   // ×24
            var huge = new Bounds(Vector3.zero, new Vector3(6f, 10f, 0.2f));         // ×0.12

            // Act + Assert — el prop es opcional a propósito: antes que deformarlo, no va.
            Assert.IsFalse(GeneralaAssetBuilder.TryBuildBannerProp(fit, tiny, out _));
            Assert.IsFalse(GeneralaAssetBuilder.TryBuildBannerProp(fit, huge, out _));
            Assert.IsFalse(GeneralaAssetBuilder.TryBuildBannerProp(fit, default, out _),
                "Bounds degenerados tampoco pueden colgar nada.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        /// <summary>Fit de un jefe ya ajustado: 1 de ancho, 2 de alto, apoyado en el piso.</summary>
        private static GeneralaAssetBuilder.ArtFit SampleBossFit() =>
            GeneralaAssetBuilder.ArtFit.For(
                new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 0.8f)),
                GeneralaAssetBuilder.BossTargetHeight,
                GeneralaAssetBuilder.BossMaxWidth,
                0.6f);

        private static GeneralaAssetBuilder.ArtFit SampleDiceFit() =>
            GeneralaAssetBuilder.ArtFit.For(
                new Bounds(Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f)),
                GeneralaAssetBuilder.DiceTargetHeight,
                GeneralaAssetBuilder.DiceMaxWidth,
                0.3f);
    }
}
