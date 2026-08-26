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
    /// <summary>Toca el <c>AssetDatabase</c> —lo que se afirma es el prefab escrito— pero construye en una
    /// carpeta temporal, no en el prefab del repo.</summary>
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

        [Test]
        public void Retint_KeysMatchTheMaterialsTheArtActuallyUses()
        {
            var artMaterials = MaterialNamesOf(TahurAssetBuilder.ArtPrefabPath);
            var retints = TahurAssetBuilder.BuildRetints();

            // Una key que no matchea es un typo silencioso: el jefe sale de fábrica.
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
            // Un material compartido deja al Tahúr y al Sunken Grand gemelos ahí.
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
            var coat = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialsFolder + "/Mat_Tahur_LightBrown.mat");

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
            // Mat_LightGreen es la piel del Sunken Grand: in-place repinta medio casino.
            var skin = AssetDatabase.LoadAssetAtPath<Material>(SharedSkinMaterialPath);
            Assert.AreEqual(_sharedSkinUsePaletteBeforeBuild, skin.GetFloat("_UsePalette"),
                "El builder pisó el material compartido en vez de clonarlo.");
        }

        [Test]
        public void Wrapper_KeepsTheTwelveCardFan()
        {
            int cards = BodyRenderers(_wrapper).Count(r => r.name.Contains("Card_SunkenGrand"));
            Assert.AreEqual(12, cards,
                "El abanico del Tahúr son 12 cartas (Card_SunkenGrand + 1..11).");
        }

        [Test]
        public void Wrapper_IsTargetableAndFlashesOnHit()
        {
            Assert.IsNotNull(_wrapper.GetComponent<EntityPawn>(), "Falta EntityPawn.");
            Assert.IsNotNull(_wrapper.GetComponent<Collider>(),
                "Sin collider en el root, PawnPicker no lo puede targetear.");
            Assert.IsNotNull(_wrapper.GetComponent<PawnMaterialFeedback>(), "Falta el hit flash.");
        }

        [Test]
        public void Art_StillFiresFeedbackEventsFromItsAttackClips()
        {
            // Un re-export sin estos eventos deja el puente en peso muerto.
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
            var animator = _wrapper.GetComponentInChildren<Animator>(includeInactive: true);

            // Los Animation Events se despachan al GameObject del Animator y a ningún otro.
            Assert.IsNotNull(animator, "El arte del Tahúr tiene que traer su Animator.");
            Assert.IsNotNull(animator.GetComponent<AnimationFeedbackEvent>(),
                "Sin el puente, cada ataque tira 'AnimationEvent has no receiver' y los steps " +
                "de feedback con OnEvent nunca se destraban.");
        }

        [Test]
        public void FeedbackBridge_IsIdempotent()
        {
            // La fixture ya lo corrió una vez.
            var again = TahurAssetBuilder.EnsureAnimationFeedbackBridge(WrapperPath);

            Assert.IsNotNull(again);
            var animator = again.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.AreEqual(1, animator.GetComponents<AnimationFeedbackEvent>().Length,
                "Rebuild duplicando el puente: cada Animation Event se publicaría dos veces.");
        }

        [Test]
        public void Portrait_ResolvesToASprite()
        {
            // La hoja está sliceada en Multiple: el retrato es un sub-sprite con nombre.
            var portrait = BossPortraitLibrary.Tahur();

            Assert.IsNotNull(portrait,
                $"No se resolvió '{BossPortraitLibrary.TahurSpriteName}' en " +
                $"'{BossPortraitLibrary.SheetPath}': el campo Portrait del SO quedaría en null y la " +
                "cola de turnos caería al visual default.");
        }

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

        /// <summary>Mismo filtro que usa la utility del retinte, así los dos lados de la
        /// comparación miran el mismo conjunto.</summary>
        private static IEnumerable<Renderer> BodyRenderers(GameObject root)
            => root.GetComponentsInChildren<Renderer>(includeInactive: true)
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer);
    }
}
