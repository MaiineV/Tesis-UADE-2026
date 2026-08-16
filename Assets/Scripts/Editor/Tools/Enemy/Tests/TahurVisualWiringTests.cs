using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Valida el vestido visual del Tahúr contra el arte real: que el retinte matchee los materiales
    /// de <c>SunkedGrand_Animated</c>, que el wrapper no comparta paleta con el jefe del piso 1 y que
    /// el puente de Animation Events quede colgado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A diferencia de <see cref="TahurWiringTests"/>, esta fixture <b>sí</b> toca el
    /// <c>AssetDatabase</c>: lo que se verifica es el prefab que queda escrito, y eso no se puede
    /// afirmar sobre una spec en memoria.
    /// </para>
    /// <para>
    /// <b>Construye en una carpeta temporal</b>, no en <c>TahurAssetBuilder.VisualPrefabPath</c>: un
    /// test no debería reescribir el prefab que referencia el <c>ED_Boss_Tahur.asset</c> del repo
    /// (aunque el rebuild preserve el GUID, dejaría el asset tocado en cada corrida del runner).
    /// </para>
    /// </remarks>
    [TestFixture]
    public class TahurVisualWiringTests
    {
        private const string TestRoot = "Assets/Rollgeon/__TahurVisualTests";
        private const string WrapperPath = TestRoot + "/PF_Boss_Tahur_Probe.prefab";
        private const string MaterialsFolder = TestRoot + "/Materials";

        /// <summary>Clips con Animation Events — la razón del puente de feedback.</summary>
        private static readonly string[] AttackClipPaths =
        {
            "Assets/Art/3D/Animations/Enemies/SunkedGrand/Anim_SunkedGrand_Attack_Melee.anim",
            "Assets/Art/3D/Animations/Enemies/SunkedGrand/Anim_SunkedGrand_Attack_Range.anim",
        };

        private const string FeedbackFunctionName = "PushFeedbackEvent";

        /// <summary>Piel del jefe del piso 1: el material compartido que el retinte NO puede pisar.</summary>
        private const string SharedSkinMaterialPath = "Assets/Art/3D/Materials/Mat_LightGreen.mat";

        private GameObject _wrapper;
        private float _sharedSkinUsePaletteBeforeBuild;

        // ======================================================================
        // Fixture
        // ======================================================================

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(TahurAssetBuilder.ArtPrefabPath),
                $"Fixture roto: no existe el arte '{TahurAssetBuilder.ArtPrefabPath}'.");

            var skin = AssetDatabase.LoadAssetAtPath<Material>(SharedSkinMaterialPath);
            Assert.IsNotNull(skin, $"Fixture roto: no existe '{SharedSkinMaterialPath}'.");
            _sharedSkinUsePaletteBeforeBuild = skin.GetFloat("_UsePalette");

            BossVisualWrapperBuilder.EnsureFolder(TestRoot);

            var spec = TahurAssetBuilder.BuildWrapperSpec();
            spec.OutputPrefabPath = WrapperPath;
            spec.MaterialsFolder = MaterialsFolder;

            _wrapper = BossVisualWrapperBuilder.BuildWrapper(spec);
            Assert.IsNotNull(_wrapper, "El build del wrapper del Tahúr devolvió null.");

            TahurAssetBuilder.EnsureAnimationFeedbackBridge(WrapperPath);
            _wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        // ======================================================================
        // Retinte contra el arte real
        // ======================================================================

        [Test]
        public void Retint_KeysMatchTheMaterialsTheArtActuallyUses()
        {
            // Arrange
            var artMaterials = MaterialNamesOf(TahurAssetBuilder.ArtPrefabPath);
            var retints = TahurAssetBuilder.BuildRetints();

            // Assert — una key que no matchea es un typo silencioso: el jefe sale con el color de
            // fábrica y nada lo grita en el editor.
            foreach (var key in retints.Keys)
            {
                CollectionAssert.Contains(artMaterials, key,
                    $"El retinte pide '{key}' y el arte no usa ningún material con ese nombre.");
            }

            // Y al revés: un material del arte sin retintar queda idéntico al del Sunken Grand.
            foreach (var material in artMaterials)
            {
                Assert.IsTrue(retints.ContainsKey(material),
                    $"'{material}' quedó sin retinte — esa superficie sale igual en los dos jefes.");
            }
        }

        [Test]
        public void Wrapper_SwapsEveryBodyMaterialForItsOwnClone()
        {
            // Assert — si algún material siguiera apuntando al asset compartido, el Tahúr y el
            // Sunken Grand serían gemelos en esa superficie.
            foreach (var renderer in BodyRenderers(_wrapper))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;

                    var path = AssetDatabase.GetAssetPath(material);
                    StringAssert.StartsWith(MaterialsFolder, path,
                        $"'{material.name}' en '{renderer.name}' quedó compartido con el arte.");
                }
            }
        }

        [Test]
        public void Wrapper_PaintsTheCoatFeltGreen_WithThePaletteToggleOff()
        {
            // Arrange
            var coat = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialsFolder + "/Mat_Tahur_LightBrown.mat");

            // Assert
            Assert.IsNotNull(coat, "No se clonó el material de la levita.");
            Assert.AreEqual(0f, coat.GetFloat("_UsePalette"),
                "Con colores directos el toggle de paleta tiene que quedar apagado o no se ven.");

            var mid = coat.GetColor("_MidColor");
            Assert.Greater(mid.g, mid.r, "La levita es fieltro de mesa: verde dominante.");
            Assert.Greater(mid.g, mid.b, "La levita es fieltro de mesa: verde dominante.");
        }

        [Test]
        public void Wrapper_DoesNotRepaintTheFloorOneBoss()
        {
            // Assert — Mat_LightGreen es la piel del Sunken Grand y la comparten otros enemigos:
            // retintarlo in-place repintaría medio casino.
            var skin = AssetDatabase.LoadAssetAtPath<Material>(SharedSkinMaterialPath);
            Assert.AreEqual(_sharedSkinUsePaletteBeforeBuild, skin.GetFloat("_UsePalette"),
                "El builder pisó el material compartido en vez de clonarlo.");
        }

        // ======================================================================
        // Identidad del arte
        // ======================================================================

        [Test]
        public void Wrapper_KeepsTheTwelveCardFan()
        {
            // Assert — el abanico es la razón de elegir este arte para el tramposo de cartas.
            int cards = BodyRenderers(_wrapper).Count(r => r.name.Contains("Card_SunkenGrand"));
            Assert.AreEqual(12, cards,
                "El abanico del Tahúr son 12 cartas (Card_SunkenGrand + 1..11).");
        }

        [Test]
        public void Wrapper_IsTargetableAndFlashesOnHit()
        {
            // Assert — smoke del cableado que el combate espera del pawn.
            Assert.IsNotNull(_wrapper.GetComponent<EntityPawn>(), "Falta EntityPawn.");
            Assert.IsNotNull(_wrapper.GetComponent<Collider>(),
                "Sin collider en el root, PawnPicker no lo puede targetear.");
            Assert.IsNotNull(_wrapper.GetComponent<PawnMaterialFeedback>(), "Falta el hit flash.");
        }

        // ======================================================================
        // Puente de Animation Events
        // ======================================================================

        [Test]
        public void Art_StillFiresFeedbackEventsFromItsAttackClips()
        {
            // Assert — si el artista re-exporta sin estos eventos, el puente pasa a ser peso muerto
            // y este test es el que lo cuenta.
            foreach (var clipPath in AttackClipPaths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                Assert.IsNotNull(clip, $"No existe el clip '{clipPath}'.");
                Assert.IsTrue(
                    AnimationUtility.GetAnimationEvents(clip)
                        .Any(e => e.functionName == FeedbackFunctionName),
                    $"'{clipPath}' ya no dispara {FeedbackFunctionName}.");
            }
        }

        [Test]
        public void Wrapper_HangsTheFeedbackBridgeOnTheAnimator()
        {
            // Arrange
            var animator = _wrapper.GetComponentInChildren<Animator>(includeInactive: true);

            // Assert — los Animation Events se despachan al GameObject del Animator: en cualquier
            // otro hijo el componente no recibe nada.
            Assert.IsNotNull(animator, "El arte del Tahúr tiene que traer su Animator.");
            Assert.IsNotNull(animator.GetComponent<AnimationFeedbackEvent>(),
                "Sin el puente, cada ataque tira 'AnimationEvent has no receiver' y los steps " +
                "de feedback con OnEvent nunca se destraban.");
        }

        [Test]
        public void FeedbackBridge_IsIdempotent()
        {
            // Act — la fixture ya lo corrió una vez.
            var again = TahurAssetBuilder.EnsureAnimationFeedbackBridge(WrapperPath);

            // Assert
            Assert.IsNotNull(again);
            var animator = again.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.AreEqual(1, animator.GetComponents<AnimationFeedbackEvent>().Length,
                "Rebuild duplicando el puente: cada Animation Event se publicaría dos veces.");
        }

        // ======================================================================
        // Retrato
        // ======================================================================

        [Test]
        public void Portrait_ResolvesToASprite()
        {
            // Act — la hoja de personajes está sliceada en Multiple, así que el retrato es un
            // sub-sprite con nombre y no la textura entera.
            var portrait = BossPortraitLibrary.Tahur();

            // Assert
            Assert.IsNotNull(portrait,
                $"No se resolvió '{BossPortraitLibrary.TahurSpriteName}' en " +
                $"'{BossPortraitLibrary.SheetPath}': el campo Portrait del SO quedaría en null y la " +
                "cola de turnos caería al visual default.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        /// <summary>Nombres únicos de los materiales de un prefab de arte, sin partículas ni trails.</summary>
        private static List<string> MaterialNamesOf(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var probe = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            try
            {
                var names = new SortedSet<string>();
                foreach (var renderer in BodyRenderers(probe))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null) names.Add(material.name);
                    }
                }
                return names.ToList();
            }
            finally
            {
                if (probe != null) Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Mesh y SkinnedMesh renderers — el mismo filtro que usa la utility para el retinte, así los
        /// dos lados de la comparación miran el mismo conjunto.
        /// </summary>
        private static IEnumerable<Renderer> BodyRenderers(GameObject root)
            => root.GetComponentsInChildren<Renderer>(includeInactive: true)
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer);
    }
}
