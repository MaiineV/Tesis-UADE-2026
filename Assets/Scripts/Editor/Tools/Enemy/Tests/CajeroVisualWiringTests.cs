using System.Collections.Generic;
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
    /// Vestuario de El Cajero: la ficha de wrapper, el prefab que sale de construirla contra el arte
    /// real, y la regla de <see cref="CajeroAssetBuilder.ResolveVisualPrefab"/>. Construye en una
    /// carpeta temporal que el teardown borra, y no llama a <c>EnsurePortrait</c> — reimportar
    /// <c>Casino_0070.png</c> ensuciaría el <c>.meta</c> de un asset compartido.
    /// </summary>
    [TestFixture]
    public class CajeroVisualWiringTests
    {
        private const string TestRoot = "Assets/Rollgeon/__CajeroVisualTests";
        private const string MaterialsFolder = TestRoot + "/Materials";
        private const string WrapperPath = TestRoot + "/PF_Boss_Cajero.prefab";

        private const string SourceMaterialsFolder = "Assets/Art/3D/Materials";
        private const string ClonePrefix = "Mat_Cajero_";

        /// <summary>Los cinco materiales del arte, en el orden en que los pinta el retinte.</summary>
        private static readonly string[] SourceMaterialNames =
        {
            CajeroAssetBuilder.ShellMaterial,
            CajeroAssetBuilder.TrimMaterial,
            CajeroAssetBuilder.HighlightMaterial,
            CajeroAssetBuilder.BodyMaterial,
            CajeroAssetBuilder.AccentMaterial,
        };

        private GameObject _wrapper;

        // ======================================================================
        // Fixture
        // ======================================================================

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(CajeroAssetBuilder.ArtPrefabPath),
                $"Fixture roto: no existe el arte '{CajeroAssetBuilder.ArtPrefabPath}'.");

            BossVisualWrapperBuilder.EnsureFolder(TestRoot);

            // Por el builder y no por BuildWrapper pelado: el recorte del collider es una segunda
            // pasada sobre el prefab guardado, y saltearla dejaría ese paso sin cobertura.
            _wrapper = CajeroAssetBuilder.EnsureVisualPrefab(WrapperPath, MaterialsFolder);
            Assert.IsNotNull(_wrapper, "El build del wrapper del Cajero devolvió null.");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        // ======================================================================
        // Ficha de wrapper
        // ======================================================================

        [Test]
        public void Spec_DressesTheCashierWithItsOwnArt_NotThePlaceholder()
        {
            var spec = CajeroAssetBuilder.BuildWrapperSpec();

            Assert.AreEqual(CajeroAssetBuilder.ArtPrefabPath, spec.ArtPrefabPath);
            Assert.AreEqual("Assets/Prefabs/Enemies/MechaBoss_Animated.prefab",
                CajeroAssetBuilder.ArtPrefabPath,
                "El Cajero viste el mech. Es el path literal a propósito: si el builder se mueve a " +
                "otro rig, esto se tiene que ver en el diff del test y no sólo en el del prefab.");
            Assert.AreEqual(CajeroAssetBuilder.VisualPrefabPath, spec.OutputPrefabPath,
                "El default del builder tiene que ser el wrapper propio del jefe.");
            Assert.AreNotEqual(CajeroAssetBuilder.PlaceholderVisualPrefabPath, spec.ArtPrefabPath,
                "El Cajero ya no viste el prefab del Security Boss.");
        }

        [Test]
        public void CritterSpec_DoesNotWalkOnTheSameRigAsItsBoss()
        {
            var boss = CajeroAssetBuilder.BuildWrapperSpec();
            var critter = CajeroAssetBuilder.BuildCritterWrapperSpec();

            Assert.AreEqual("Assets/Prefabs/Enemies/GeneralDirector_Animated.prefab",
                critter.ArtPrefabPath,
                "La Comisión tiene rig propio: compartir el mech del jefe la volvía \"el jefe en " +
                "chico\", y lo único que los separaba era la escala y el tinte.");
            Assert.AreNotEqual(boss.ArtPrefabPath, critter.ArtPrefabPath,
                "El jefe y su refuerzo no pueden anidar el mismo arte: la Comisión se separó del " +
                "mech justamente para dejar de ser una copia en miniatura del Cajero.");
            // La carpeta de clones sale del BossName cuando MaterialsFolder viene vacío.
            Assert.AreNotEqual(boss.BossName, critter.BossName,
                "Comparten malla, no paleta: con el mismo nombre los clones del bicho pisan los " +
                "del jefe y los dos salen del mismo color.");
        }

        [Test]
        public void Spec_RetintsEveryMaterialTheArtUses_AndNothingElse()
        {
            // Una key que no matchea sólo loguea un warning; un material sin retintar deja el
            // cuerpo negro o los discos grises.
            var spec = CajeroAssetBuilder.BuildWrapperSpec();
            var artMaterials = CollectArtMaterialNames();

            CollectionAssert.AreEquivalent(artMaterials, spec.Retints.Keys,
                "El retinte y los materiales del arte tienen que coincidir exactamente. " +
                $"Arte: {string.Join(", ", artMaterials)}. " +
                $"Retinte: {string.Join(", ", spec.Retints.Keys)}.");
        }

        [Test]
        public void Spec_UsesDirectColors_NotPaletteSlots()
        {
            // Los slots de PA_MainPalette están desalineados: un FromSlot daría un color al azar.
            var spec = CajeroAssetBuilder.BuildWrapperSpec();

            foreach (var pair in spec.Retints)
            {
                Assert.IsNull(pair.Value.PaletteSlot, $"'{pair.Key}' pide un slot de paleta.");
                Assert.IsTrue(pair.Value.LightColor.HasValue, $"'{pair.Key}' sin LightColor.");
                Assert.IsTrue(pair.Value.MidColor.HasValue, $"'{pair.Key}' sin MidColor.");
                Assert.IsTrue(pair.Value.ShadowColor.HasValue, $"'{pair.Key}' sin ShadowColor.");
            }
        }

        [Test]
        public void Spec_PaintsTheShellGold_SoTheGoldScalingReadsAtAGlance()
        {
            var spec = CajeroAssetBuilder.BuildWrapperSpec();

            foreach (var name in new[]
                     {
                         CajeroAssetBuilder.ShellMaterial,
                         CajeroAssetBuilder.TrimMaterial,
                         CajeroAssetBuilder.HighlightMaterial,
                     })
            {
                var mid = spec.Retints[name].MidColor.Value;
                Assert.Greater(mid.r, mid.b, $"'{name}' no es dorado: el azul no puede ganarle al rojo.");
                Assert.Greater(mid.g, mid.b, $"'{name}' no es dorado: le falta verde sobre el azul.");
            }
        }

        [Test]
        public void Spec_KeepsTheBodyDarkerThanTheShell_SoTheGoldPops()
        {
            var spec = CajeroAssetBuilder.BuildWrapperSpec();

            var body = spec.Retints[CajeroAssetBuilder.BodyMaterial].MidColor.Value;
            var shell = spec.Retints[CajeroAssetBuilder.ShellMaterial].MidColor.Value;

            Assert.Greater(Luminance(shell), Luminance(body),
                "El oro tiene que leerse más claro que las placas: es la mecánica del jefe.");
            Assert.Greater(body.g, body.r, "Las placas van verde fieltro de mesa.");
            Assert.Greater(body.g, body.b);
        }

        [Test]
        public void Spec_CarriesTheChipTrayProp_ScaledDownToStayOnItsTile()
        {
            var spec = CajeroAssetBuilder.BuildWrapperSpec();

            Assert.AreEqual(1, spec.Props.Count, "Un solo prop: la caja de fichas.");
            var prop = spec.Props[0];

            Assert.AreEqual(CajeroAssetBuilder.ChipsBoxPropPath, prop.PrefabPath);
            Assert.AreEqual(CajeroAssetBuilder.ChipsBoxPropName, prop.Name);
            Assert.AreEqual(0f, prop.LocalPosition.y, 0.001f,
                "La caja apoya en el mismo plano que los pies del jefe.");
            Assert.AreNotEqual(0f, prop.LocalPosition.x,
                "La caja va a un costado, no dentro de la silueta.");
            Assert.Less(prop.LocalScale.x, 1f,
                "A escala 1 la caja ocupa un tile entero y se mete en la casilla vecina.");
        }

        [Test]
        public void Spec_HangsTheHealthBarWhereTheReferenceBossHasIt()
        {
            var spec = CajeroAssetBuilder.BuildWrapperSpec();

            Assert.IsTrue(spec.AddHealthBar, "El jefe necesita barra world-space.");
            Assert.AreEqual(3f, spec.HealthBarOffset.y, 0.001f,
                "Misma altura que GeneralDirector.prefab, que anida este mismo personaje.");
        }

        [Test]
        public void Spec_CanBeRetargeted_SoTheRealPrefabIsNotTouchedByTests()
        {
            var spec = CajeroAssetBuilder.BuildWrapperSpec(WrapperPath, MaterialsFolder);

            Assert.AreEqual(WrapperPath, spec.OutputPrefabPath);
            Assert.AreEqual(MaterialsFolder, spec.MaterialsFolder);
        }

        [Test]
        public void Spec_LeavesTheMaterialsFolderToTheWrapperBuilder_ByDefault()
        {
            // Null ⇒ Assets/Rollgeon/Enemies/Materials/Cajero, la convención del wrapper builder.
            var spec = CajeroAssetBuilder.BuildWrapperSpec();

            Assert.IsNull(spec.MaterialsFolder);
            Assert.AreEqual("Cajero", spec.BossName, "El prefijo de los materiales sale de acá.");
        }

        // ======================================================================
        // Prefab construido
        // ======================================================================

        [Test]
        public void Wrapper_IsAPickableAnimatedPawn()
        {
            Assert.IsNotNull(_wrapper.GetComponent<EntityPawn>(), "Falta EntityPawn.");
            Assert.IsNotNull(_wrapper.GetComponent<PawnRegistryBinding>(), "Falta PawnRegistryBinding.");
            Assert.IsNotNull(_wrapper.GetComponent<HitImpulseConsumer>(), "Falta HitImpulseConsumer.");
            Assert.IsNotNull(_wrapper.GetComponent<PawnMaterialFeedback>(), "Falta PawnMaterialFeedback.");
            Assert.IsNotNull(_wrapper.GetComponent<Collider>(),
                "Sin collider en el root, PawnPicker no puede targetear al jefe.");
        }

        [Test]
        public void Wrapper_KeepsTheSteppedAnimatorOfTheArt()
        {
            // Si el prefab de arte perdiera su Animator el jefe quedaría en T-pose sin avisar.
            Assert.IsNotNull(_wrapper.GetComponentInChildren<Animator>(true),
                "El arte tiene que aportar su Animator (AnimCon_Mecha).");
            Assert.IsNotNull(_wrapper.GetComponentInChildren<global::SteppedAnimation>(true),
                "El look stepped a 8 FPS lo da SteppedAnimation en la raíz del arte.");
        }

        [Test]
        public void Wrapper_ParentsTheChipTrayBesideTheBoss()
        {
            var tray = _wrapper.transform.Find(CajeroAssetBuilder.ChipsBoxPropName);

            Assert.IsNotNull(tray, $"Falta el prop '{CajeroAssetBuilder.ChipsBoxPropName}'.");
            Assert.Greater(tray.GetComponentsInChildren<Renderer>(true).Length, 0,
                "La caja quedó sin renderers: ¿se instanció el prefab equivocado?");
            Assert.AreEqual(CajeroAssetBuilder.ChipsBoxLocalPosition.x, tray.localPosition.x, 0.001f);
            Assert.AreEqual(CajeroAssetBuilder.ChipsBoxLocalScale.x, tray.localScale.x, 0.001f);
        }

        [Test]
        public void Wrapper_PaintsEveryPieceOfTheMechWithItsOwnClone()
        {
            // El mech reparte sus cinco materiales entre 12 renderers, varios con más de un submesh:
            // un solo slot con el material original deja un pedazo del jefe con el gris de fábrica.
            var art = _wrapper.transform.Find("Art");
            Assert.IsNotNull(art, "Fixture roto: no hay hijo 'Art'.");

            int slots = 0;
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                foreach (var material in renderer.sharedMaterials)
                {
                    slots++;
                    Assert.IsNotNull(material, $"Slot vacío en '{renderer.name}'.");
                    StringAssert.StartsWith(ClonePrefix, material.name,
                        $"'{renderer.name}' quedó con el material original '{material.name}'.");
                }
            }

            Assert.Greater(slots, 1, "Fixture roto: el arte no reporta slots de material.");
        }

        [Test]
        public void Wrapper_KeepsItsColliderInsideItsOwnTile()
        {
            // El mech está en T-pose: sus bounds dan ~1.5 de radio y el capsule saldría tapando las
            // cuatro casillas vecinas, que con un jefe melee son justo las que el jugador necesita
            // poder clickear (las monedas del piso, los pinchos de la sala).
            var capsule = _wrapper.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(capsule, "El wrapper del jefe nace con capsule.");

            Assert.LessOrEqual(capsule.radius, CajeroAssetBuilder.ColliderRadiusCap + 0.001f,
                "El collider se pasa de su casilla: ¿corrió el recorte de EnsureVisualPrefab?");
        }

        [Test]
        public void Wrapper_ClonesEveryRetintWithThePaletteOff()
        {
            // Sin igualdad exacta de color: el ida y vuelta por el .mat puede pasar por conversión
            // de color space. Lo que importa es que el retinte llegó y la paleta quedó apagada.
            foreach (var name in SourceMaterialNames)
            {
                var clone = LoadClone(name);

                Assert.IsNotNull(clone, $"No se creó el clon de '{name}' en '{MaterialsFolder}'.");
                Assert.AreEqual(0f, clone.GetFloat("_UsePalette"),
                    $"'{clone.name}' quedó leyendo la paleta: los colores directos no se verían.");
            }
        }

        [Test]
        public void Wrapper_TurnsTheBodyGreen_InsteadOfTheGrayOfTheOriginal()
        {
            // Mat_Gray viene gris parejo (r ≥ g ≥ b): si el clon sale verde, el retinte se aplicó.
            var body = LoadClone(CajeroAssetBuilder.BodyMaterial).GetColor("_MidColor");

            Assert.Greater(body.g, body.r, "Las placas tienen que quedar verde fieltro, no grises.");
            Assert.Greater(body.g, body.b);
        }

        [Test]
        public void Wrapper_DoesNotRepaintTheSharedSourceMaterials()
        {
            // Los originales los usa medio casino: retintarlos in-place repintaría a todos.
            foreach (var name in SourceMaterialNames)
            {
                var source = AssetDatabase.LoadAssetAtPath<Material>($"{SourceMaterialsFolder}/{name}.mat");
                Assert.IsNotNull(source, $"Fixture roto: no existe '{name}.mat'.");
                Assert.AreEqual(1f, source.GetFloat("_UsePalette"),
                    $"'{name}' quedó con colores directos — el builder lo pisó en vez de clonarlo.");
            }
        }

        [Test]
        public void Wrapper_RebuildPreservesItsGuid_SoTheEnemyDataKeepsPointingAtIt()
        {
            string guidBefore = AssetDatabase.AssetPathToGUID(WrapperPath);
            Assert.IsNotEmpty(guidBefore);

            var second = CajeroAssetBuilder.EnsureVisualPrefab(WrapperPath, MaterialsFolder);

            Assert.IsNotNull(second, "El rebuild devolvió null.");
            Assert.AreEqual(guidBefore, AssetDatabase.AssetPathToGUID(WrapperPath),
                "El rebuild cambió el GUID: ED_Boss_Cajero.VisualPrefab quedaría en null.");
            Assert.AreEqual(1, second.GetComponents<EntityPawn>().Length,
                "El rebuild duplicó componentes.");
            Assert.AreEqual(1, CountNamed(second.transform, CajeroAssetBuilder.ChipsBoxPropName),
                "El rebuild duplicó la caja de fichas.");
        }

        // ======================================================================
        // Retrato
        // ======================================================================

        [Test]
        public void Portrait_PointsAtAnImportableTexture()
        {
            // Lo único que puede estar mal acá es el path, y su síntoma no rompe nada.
            var importer = AssetImporter.GetAtPath(CajeroAssetBuilder.PortraitTexturePath) as TextureImporter;

            Assert.IsNotNull(importer,
                $"'{CajeroAssetBuilder.PortraitTexturePath}' no es una textura importable.");
        }

        // ======================================================================
        // Regla de asignación
        // ======================================================================

        [Test]
        public void ResolveVisualPrefab_TakesTheWrapper_WhenTheDataHasNothing()
        {
            var wrapper = NewTemp("wrapper");
            var placeholder = NewTemp("placeholder");
            try
            {
                Assert.AreSame(wrapper,
                    CajeroAssetBuilder.ResolveVisualPrefab(null, wrapper, placeholder));
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
                Object.DestroyImmediate(placeholder);
            }
        }

        [Test]
        public void ResolveVisualPrefab_MigratesThePlaceholder()
        {
            var wrapper = NewTemp("wrapper");
            var placeholder = NewTemp("placeholder");
            try
            {
                // El placeholder era el parche de "no hay arte todavía", no una decisión de arte.
                Assert.AreSame(wrapper,
                    CajeroAssetBuilder.ResolveVisualPrefab(placeholder, wrapper, placeholder));
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
                Object.DestroyImmediate(placeholder);
            }
        }

        [Test]
        public void ResolveVisualPrefab_KeepsAHandAuthoredPrefab()
        {
            var wrapper = NewTemp("wrapper");
            var placeholder = NewTemp("placeholder");
            var authored = NewTemp("authored");
            try
            {
                // Si alguien wireó otro prefab a mano, un rebuild del builder no lo tiene que revertir.
                Assert.AreSame(authored,
                    CajeroAssetBuilder.ResolveVisualPrefab(authored, wrapper, placeholder));
            }
            finally
            {
                Object.DestroyImmediate(wrapper);
                Object.DestroyImmediate(placeholder);
                Object.DestroyImmediate(authored);
            }
        }

        [Test]
        public void ResolveVisualPrefab_FallsBackToWhatWasThere_WhenTheWrapperBuildFailed()
        {
            var placeholder = NewTemp("placeholder");
            try
            {
                // Un build fallido (arte movido, prefab corrupto) no puede dejar al jefe sin cuerpo.
                Assert.AreSame(placeholder,
                    CajeroAssetBuilder.ResolveVisualPrefab(placeholder, null, placeholder));
                Assert.IsNull(CajeroAssetBuilder.ResolveVisualPrefab(null, null, placeholder));
            }
            finally
            {
                Object.DestroyImmediate(placeholder);
            }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        /// <summary>Clon de un material del arte en la carpeta temporal del fixture.</summary>
        private static Material LoadClone(string sourceMaterialName)
        {
            string core = sourceMaterialName.StartsWith("Mat_")
                ? sourceMaterialName.Substring("Mat_".Length)
                : sourceMaterialName;

            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{ClonePrefix}{core}.mat");
        }

        private static GameObject NewTemp(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            return go;
        }

        private static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private static int CountNamed(Transform parent, string childName)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == childName) count++;
            return count;
        }

        /// <summary>Nombres únicos de material que usan los Mesh/SkinnedMesh renderers del arte.</summary>
        private static List<string> CollectArtMaterialNames()
        {
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(CajeroAssetBuilder.ArtPrefabPath);
            Assert.IsNotNull(art, "Fixture roto: no se cargó el arte.");

            var names = new List<string>();
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || names.Contains(material.name)) continue;
                    names.Add(material.name);
                }
            }

            Assert.IsNotEmpty(names, "Fixture roto: el arte no reporta materiales.");
            return names;
        }
    }
}
