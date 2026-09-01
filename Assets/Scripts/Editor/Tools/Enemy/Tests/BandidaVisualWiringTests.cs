using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Toca el <c>AssetDatabase</c> —lo que se afirma es el prefab escrito— pero construye en
    /// una carpeta temporal que el teardown borra.</summary>
    [TestFixture]
    public class BandidaVisualWiringTests
    {
        private const string TestRoot = "Assets/Rollgeon/__BandidaVisualTests";
        private const string MaterialsFolder = TestRoot + "/Materials";
        private const string BossWrapperPath = TestRoot + "/PF_Boss_Bandida.prefab";
        private const string ReelWrapperPath = TestRoot + "/PF_Obj_Rodillo.prefab";

        private const string ChassisSourceMaterialName = "Mat_Gold";
        private const string HardwareSourceMaterialName = "Mat_DarkGray";
        private const string HighlightSourceMaterialName = "Mat_White";

        private const string ChassisSourceMaterialPath = "Assets/Art/3D/Materials/Mat_Gold.mat";
        private const string ChassisCloneMaterialPath = MaterialsFolder + "/Mat_Bandida_Gold.mat";
        private const string HighlightCloneMaterialPath = MaterialsFolder + "/Mat_Bandida_White.mat";

        /// <summary>El placeholder que este pase saca de encima de los dos SOs.</summary>
        private const string PlaceholderPrefabPath = "Assets/Prefabs/Enemies/SunkedGrand.prefab";

        private GameObject _bossWrapper;
        private GameObject _reelWrapper;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            BossVisualWrapperBuilder.EnsureFolder(TestRoot);

            _bossWrapper = BandidaAssetBuilder.BuildBossVisual(BossWrapperPath, MaterialsFolder);
            _reelWrapper = BandidaAssetBuilder.BuildReelVisual(ReelWrapperPath, MaterialsFolder);

            Assert.IsNotNull(_bossWrapper, "El build del wrapper del jefe devolvió null.");
            Assert.IsNotNull(_reelWrapper, "El build del wrapper del rodillo devolvió null.");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Borra prefabs y materiales clonados de una sola vez.
            if (AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void AuthoredArt_IsNotThePlaceholderAnymore_ForBossNorReel()
        {
            Assert.AreNotEqual(PlaceholderPrefabPath, BandidaAssetBuilder.BossArtPrefabPath,
                "El arte del jefe volvió al placeholder.");
            Assert.AreNotEqual(PlaceholderPrefabPath, BandidaAssetBuilder.ReelArtPrefabPath,
                "El arte del rodillo volvió al placeholder.");
        }

        [Test]
        public void BossAndReel_DoNotShareArtPrefabNorPortrait()
        {
            Assert.AreNotEqual(BandidaAssetBuilder.BossArtPrefabPath,
                BandidaAssetBuilder.ReelArtPrefabPath, "Jefe y rodillo comparten el arte.");
            Assert.AreNotEqual(BandidaAssetBuilder.BossVisualPrefabPath,
                BandidaAssetBuilder.ReelVisualPrefabPath, "Jefe y rodillo comparten el wrapper.");
            Assert.AreNotEqual(BandidaAssetBuilder.BossPortraitPath,
                BandidaAssetBuilder.ReelPortraitPath,
                "Jefe y rodillo comparten el retrato: en la cola de turnos se ven cuatro caras iguales.");
        }

        [Test]
        public void EveryAuthoredAssetPath_ExistsInTheProject()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(BandidaAssetBuilder.BossArtPrefabPath),
                $"Falta el arte del jefe en '{BandidaAssetBuilder.BossArtPrefabPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(BandidaAssetBuilder.ReelArtPrefabPath),
                $"Falta el arte del rodillo en '{BandidaAssetBuilder.ReelArtPrefabPath}'.");

            // Como Texture2D y no como Sprite: el import a Sprite lo hace el MenuItem, no este test,
            // así que cargarlos como Sprite daría null sin que falte el archivo.
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Texture2D>(BandidaAssetBuilder.BossPortraitPath),
                $"Falta el retrato del jefe en '{BandidaAssetBuilder.BossPortraitPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Texture2D>(BandidaAssetBuilder.ReelPortraitPath),
                $"Falta el retrato del rodillo en '{BandidaAssetBuilder.ReelPortraitPath}'.");
        }

        [Test]
        public void BossRetintKeys_AllMatchAMaterialOfTheArt()
        {
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();
            var artMaterials = MaterialNamesOf(BandidaAssetBuilder.BossArtPrefabPath);

            // Una key que no matchea sólo tira un warning: el jefe sale de fábrica.
            foreach (var key in spec.Retints.Keys)
            {
                CollectionAssert.Contains(artMaterials, key,
                    $"El retinte pide '{key}' y el arte no usa ningún material con ese nombre. " +
                    $"Materiales del arte: {string.Join(", ", artMaterials)}.");
            }
        }

        [Test]
        public void BossRetint_UsesDirectColors_NotPaletteSlots()
        {
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();

            // Los labels de PA_MainPalette están desalineados: "slot Red" no garantiza rojo.
            foreach (var pair in spec.Retints)
            {
                Assert.IsNull(pair.Value.PaletteSlot, $"'{pair.Key}' quedó pidiendo un slot de paleta.");
                Assert.IsTrue(pair.Value.LightColor.HasValue, $"'{pair.Key}' sin LightColor.");
                Assert.IsTrue(pair.Value.MidColor.HasValue, $"'{pair.Key}' sin MidColor.");
                Assert.IsTrue(pair.Value.ShadowColor.HasValue, $"'{pair.Key}' sin ShadowColor.");
            }
        }

        [Test]
        public void BossRetint_PaintsTheChassisRed_AndTheHardwareGold()
        {
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();
            var chassis = spec.Retints[ChassisSourceMaterialName].MidColor.Value;
            var hardware = spec.Retints[HardwareSourceMaterialName].MidColor.Value;

            // Mat_Gold cubre torso, brazos y piernas: es la carcasa, no los herrajes.
            AssertColorsMatch(BandidaAssetBuilder.CabinetMid, chassis,
                "La carcasa del mech dejó de ser el rojo del gabinete.");
            AssertColorsMatch(BandidaAssetBuilder.TrimMid, hardware,
                "Los herrajes dejaron de ser dorados.");
            Assert.Greater(chassis.r, chassis.g, "El mid de la carcasa tiene que ser rojo dominante.");
            Assert.Greater(hardware.g, hardware.b, "El mid de los herrajes tiene que ser cálido.");
        }

        [Test]
        public void BossRetint_LeavesTheHighlightMaterialOutOfTheDictionary()
        {
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();
            var artMaterials = MaterialNamesOf(BandidaAssetBuilder.BossArtPrefabPath);

            // Mat_White es el punto de luz del torso: se deja compartido a propósito.
            CollectionAssert.Contains(artMaterials, HighlightSourceMaterialName,
                "Fixture roto: el arte ya no usa el material de highlight.");
            CollectionAssert.DoesNotContain(spec.Retints.Keys, HighlightSourceMaterialName,
                "Se retintó el highlight: el torso pierde el punto de luz que lee como los 7s.");
        }

        [Test]
        public void ReelSpec_CarriesNoRetints()
        {
            var spec = BandidaAssetBuilder.BuildReelWrapperSpec();

            // Slotv02 trae 8 materiales por submalla: retintar a ciegas repinta la pieza
            // equivocada.
            Assert.IsTrue(spec.Retints == null || spec.Retints.Count == 0,
                "El spec del rodillo trae retintes: con 8 materiales por submalla, retintar a " +
                "ciegas repinta la pieza equivocada.");
        }

        [Test]
        public void BossWrapper_HasTheGameplayComponents_ButNoWorldSpaceBar()
        {
            var pawn = _bossWrapper.GetComponent<EntityPawn>();
            var feedback = _bossWrapper.GetComponent<PawnMaterialFeedback>();

            Assert.IsNotNull(pawn, "Falta EntityPawn.");
            Assert.IsNotNull(_bossWrapper.GetComponent<PawnRegistryBinding>(),
                "Sin PawnRegistryBinding el jefe no recibe hit flash.");
            Assert.IsNotNull(_bossWrapper.GetComponent<HitImpulseConsumer>(), "Falta HitImpulseConsumer.");
            Assert.Greater(GetArrayRefs(feedback, "_renderers").Count, 0,
                "PawnMaterialFeedback quedó sin renderers cableados.");
            Assert.IsNull(GetRef(pawn, "_healthBar"),
                "La jefa muestra vida en la BossBarView del HUD; la barra world-space la duplicaría.");
        }

        [Test]
        public void BossWrapper_KeepsTheAnimatedMechArt()
        {
            var art = _bossWrapper.transform.Find("Art");
            Assert.IsNotNull(art, "El arte tiene que quedar anidado en un hijo 'Art'.");

            // Sin el Animator del arte, el jefe se queda en T-pose toda la pelea.
            Assert.IsNotNull(art.GetComponentInChildren<Animator>(true),
                "El arte del jefe perdió el Animator.");
            Assert.Greater(art.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, 0,
                "El arte del jefe perdió los SkinnedMeshRenderers del rig.");
        }

        [Test]
        public void BossWrapper_CapsuleStaysInsideItsOwnTile()
        {
            var capsule = _bossWrapper.GetComponent<CapsuleCollider>();

            // El mech en T-pose da ~1.5 de radio y PawnPicker resuelve por collider: sin
            // el clamp el jefe se come los clicks de los rodillos vecinos.
            Assert.IsNotNull(capsule, "El jefe necesita collider en el root para ser targeteable.");
            Assert.LessOrEqual(capsule.radius, BandidaAssetBuilder.BossColliderRadius,
                "El capsule del jefe se pasa de su casilla y tapa la fila de rodillos.");
            Assert.Greater(capsule.radius, 0f,
                "El capsule quedó degenerado: el jefe no se puede clickear.");
            Assert.AreEqual(1, capsule.direction, "El capsule va en Y — el mech está de pie.");
        }

        [Test]
        public void BossWrapper_ClonesTheChassisMaterialWithThePaletteToggleOff()
        {
            var clone = AssetDatabase.LoadAssetAtPath<Material>(ChassisCloneMaterialPath);

            // El shader ramea `_UsePalette > 0.5 ? paleta : colores directos`.
            Assert.IsNotNull(clone, $"No se clonó el material de la carcasa en '{ChassisCloneMaterialPath}'.");
            Assert.AreEqual(0f, clone.GetFloat("_UsePalette"));
            AssertColorsMatch(BandidaAssetBuilder.CabinetMid, clone.GetColor("_MidColor"),
                "El clon de la carcasa no quedó con el rojo del gabinete.");
        }

        [Test]
        public void BossWrapper_DoesNotCloneTheMaterialsItLeavesAlone()
        {
            // Clonar todo por si acaso llenaría el proyecto de copias idénticas al original.
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(HighlightCloneMaterialPath),
                "Mat_White no está en el retinte y terminó clonado igual.");
        }

        [Test]
        public void BossWrapper_DoesNotMutateTheSharedSourceMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(ChassisSourceMaterialPath);

            // Mat_Gold lo comparte medio casino: retintarlo in-place los repinta a todos.
            Assert.IsNotNull(source, $"Fixture roto: no existe '{ChassisSourceMaterialPath}'.");
            Assert.AreEqual(1f, source.GetFloat("_UsePalette"),
                "El builder pisó el material original en vez de clonarlo.");
        }

        [Test]
        public void ReelWrapper_IsAStaticObject_WithNoAnimator()
        {
            var art = _reelWrapper.transform.Find("Art");
            Assert.IsNotNull(art, "Falta el hijo 'Art' del rodillo.");

            Assert.IsEmpty(_reelWrapper.GetComponentsInChildren<Animator>(true),
                "El rodillo salió con Animator: se confunde con un enemigo que actúa.");
            Assert.Greater(art.GetComponentsInChildren<MeshRenderer>(true).Length, 0,
                "El arte del rodillo quedó sin MeshRenderers.");
        }

        [Test]
        public void ReelWrapper_UsesABoxColliderLiftedTogetherWithTheArt()
        {
            var box = _reelWrapper.GetComponent<BoxCollider>();
            var art = _reelWrapper.transform.Find("Art");

            // Box y no capsule: la máquina es una caja y el pick cubre la silueta entera.
            Assert.IsNotNull(box, "El rodillo quedó sin BoxCollider.");
            Assert.IsNull(_reelWrapper.GetComponent<CapsuleCollider>(),
                "Quedaron dos colliders en el root del rodillo.");

            // slotv02 trae su malla en un hijo a y = -0.5: sin el lift queda medio tile hundida.
            Assert.AreEqual(BandidaAssetBuilder.ReelArtYLift, art.localPosition.y, 0.001f,
                "El arte del rodillo perdió el lift que cancela el offset interno de slotv02.");
            Assert.Greater(box.center.y, 0f,
                "El box quedó centrado debajo del arte: el pick no cae sobre la máquina.");
        }

        [Test]
        public void ReelWrapper_HasItsOwnHealthBar_SoChippingItReadsAsProgress()
        {
            var canvas = _reelWrapper.transform.Find("Canvas");

            // AINode_SpawnReels inicializa pawn.HealthBar al reponer cada rodillo.
            Assert.IsNotNull(canvas, "El rodillo quedó sin canvas de barra.");
            Assert.IsNotNull(canvas.GetComponent<WorldSpaceHealthBar>(),
                "El canvas del rodillo quedó sin WorldSpaceHealthBar.");
            Assert.IsNotNull(GetRef(_reelWrapper.GetComponent<EntityPawn>(), "_healthBar"),
                "EntityPawn._healthBar sin cablear: pawn.HealthBar es null en el spawn del rodillo.");
        }

        [Test]
        public void BossWrapper_SkipsTheWorldSpaceBar_TheHudBossBarOwnsIt()
        {
            Assert.IsFalse(BandidaAssetBuilder.BuildBossWrapperSpec().AddHealthBar,
                "La jefa muestra vida en la BossBarView del HUD; la barra world-space la duplicaría.");
        }

        [Test]
        public void RebuildingTheBoss_KeepsTheGuidAndReappliesTheCapsuleClamp()
        {
            string guidBefore = AssetDatabase.AssetPathToGUID(BossWrapperPath);
            Assert.IsNotEmpty(guidBefore, "Fixture roto: el wrapper del jefe no está en disco.");

            var again = BandidaAssetBuilder.BuildBossVisual(BossWrapperPath, MaterialsFolder);

            // Los EnemyDataSO referencian el wrapper por GUID.
            Assert.IsNotNull(again, "El rebuild del jefe devolvió null.");
            Assert.AreEqual(guidBefore, AssetDatabase.AssetPathToGUID(BossWrapperPath),
                "El rebuild cambió el GUID del wrapper.");
            Assert.AreEqual(1, again.GetComponents<EntityPawn>().Length, "Se duplicó el EntityPawn.");
            Assert.AreEqual(1, again.GetComponents<Collider>().Length, "Se duplicó el collider.");
            Assert.LessOrEqual(again.GetComponent<CapsuleCollider>().radius,
                BandidaAssetBuilder.BossColliderRadius, "El rebuild perdió el clamp del capsule.");
        }

        [Test]
        public void RebuildingTheReel_KeepsTheArtLiftWithoutStackingIt()
        {
            var again = BandidaAssetBuilder.BuildReelVisual(ReelWrapperPath, MaterialsFolder);

            // El post-proceso setea la posición en absoluto, no suma un delta.
            Assert.IsNotNull(again, "El rebuild del rodillo devolvió null.");
            Assert.AreEqual(BandidaAssetBuilder.ReelArtYLift,
                again.transform.Find("Art").localPosition.y, 0.001f,
                "El lift del arte se perdió o se acumuló al reconstruir.");
            Assert.AreEqual(1, again.GetComponents<BoxCollider>().Length, "Se duplicó el box del rodillo.");
        }

        [Test]
        public void PopulateEnemyData_AssignsTheVisualPrefabAndThePortrait()
        {
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var reel = ScriptableObject.CreateInstance<EnemyDataSO>();
            var portrait = CreateRuntimeSprite();
            try
            {
                BandidaAssetBuilder.PopulateEnemyData(boss, reel, _bossWrapper, portrait);

                Assert.AreSame(_bossWrapper, boss.VisualPrefab, "El jefe quedó sin VisualPrefab.");
                Assert.AreSame(portrait, boss.Portrait,
                    "Sin Portrait el jefe sale sin cara en la cola de turnos y en la boss bar.");
            }
            finally
            {
                Object.DestroyImmediate(boss);
                Object.DestroyImmediate(reel);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void PopulateReelData_AssignsTheVisualPrefabAndThePortrait()
        {
            var reel = ScriptableObject.CreateInstance<EnemyDataSO>();
            var portrait = CreateRuntimeSprite();
            try
            {
                BandidaAssetBuilder.PopulateReelData(reel, _reelWrapper, portrait);

                Assert.AreSame(_reelWrapper, reel.VisualPrefab, "El rodillo quedó sin VisualPrefab.");
                Assert.AreSame(portrait, reel.Portrait,
                    "AINode_SpawnReels registra ReelData.Portrait en el resolver al reponer un rodillo.");
            }
            finally
            {
                Object.DestroyImmediate(reel);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void Populate_WithNullVisuals_KeepsWhatIsAlreadyAssigned()
        {
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var reel = ScriptableObject.CreateInstance<EnemyDataSO>();
            var portrait = CreateRuntimeSprite();
            try
            {
                boss.VisualPrefab = _bossWrapper;
                boss.Portrait = portrait;

                // Los tests de wiring del árbol llaman al populate sin assets.
                BandidaAssetBuilder.PopulateEnemyData(boss, reel, null, null);

                Assert.AreSame(_bossWrapper, boss.VisualPrefab, "Un null borró el VisualPrefab.");
                Assert.AreSame(portrait, boss.Portrait, "Un null borró el Portrait.");
            }
            finally
            {
                Object.DestroyImmediate(boss);
                Object.DestroyImmediate(reel);
                DestroySprite(portrait);
            }
        }

        /// <summary>Por canal y con tolerancia: el color va y vuelve por el YAML del material, así
        /// que comparar structs exactos rompería por el último bit.</summary>
        private static void AssertColorsMatch(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, message + " (r)");
            Assert.AreEqual(expected.g, actual.g, 0.01f, message + " (g)");
            Assert.AreEqual(expected.b, actual.b, 0.01f, message + " (b)");
        }

        private static List<string> MaterialNamesOf(string prefabPath)
        {
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(art, $"Fixture roto: no existe '{prefabPath}'.");

            var names = new List<string>();
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && !names.Contains(material.name)) names.Add(material.name);
                }
            }
            return names;
        }

        /// <summary>Sprite in-memory: forzar a Sprite un retrato real dejaría tocado su
        /// <c>.meta</c>.</summary>
        private static Sprite CreateRuntimeSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;

            var texture = sprite.texture;
            Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        private static Object GetRef(Object target, string field)
        {
            var prop = new SerializedObject(target).FindProperty(field);
            Assert.IsNotNull(prop, $"'{target.GetType().Name}' no expone '{field}'.");
            return prop.objectReferenceValue;
        }

        private static List<Object> GetArrayRefs(Object target, string field)
        {
            var prop = new SerializedObject(target).FindProperty(field);
            Assert.IsNotNull(prop, $"'{target.GetType().Name}' no expone '{field}'.");

            var result = new List<Object>();
            for (int i = 0; i < prop.arraySize; i++)
                result.Add(prop.GetArrayElementAtIndex(i).objectReferenceValue);
            return result;
        }
    }
}
