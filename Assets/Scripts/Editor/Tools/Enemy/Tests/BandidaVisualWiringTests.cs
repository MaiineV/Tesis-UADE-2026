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
    /// <summary>
    /// Pase visual de <b>La Bandida</b>: que el jefe y el rodillo tengan arte propio, retinte de
    /// tragamonedas y retrato, y que los dos wrappers salgan del builder ya jugables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El bug que fijan estos tests.</b> Los dos <c>EnemyDataSO</c> apuntaban al mismo
    /// <c>SunkedGrand.prefab</c>: un humanoide gigante hacía de máquina atornillada a la pared y
    /// también de cada uno de sus tres rodillos de 3 de vida. Si alguien vuelve a apuntar cualquiera
    /// de los dos al placeholder, o los dos al mismo prefab, esto se pone rojo.
    /// </para>
    /// <para>
    /// <b>Sí tocan el <c>AssetDatabase</c></b> (mismo criterio que
    /// <c>BossVisualWrapperBuilderTests</c>): lo que se afirma es el prefab que queda escrito, y eso
    /// no se puede verificar sobre una instancia in-memory. Los builds van a una carpeta temporal bajo
    /// <c>Assets/</c> que se borra en el teardown, así que el wrapper real del proyecto no se toca al
    /// correr los tests.
    /// </para>
    /// </remarks>
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

        // ======================================================================
        // Fixture
        // ======================================================================

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

        // ======================================================================
        // Arte apuntado
        // ======================================================================

        [Test]
        public void AuthoredArt_IsNotThePlaceholderAnymore_ForBossNorReel()
        {
            // Assert — SunkedGrand es un humanoide gigante: no lee ni como máquina atornillada a la
            // pared ni como un rodillo de 3 de vida.
            Assert.AreNotEqual(PlaceholderPrefabPath, BandidaAssetBuilder.BossArtPrefabPath,
                "El arte del jefe volvió al placeholder.");
            Assert.AreNotEqual(PlaceholderPrefabPath, BandidaAssetBuilder.ReelArtPrefabPath,
                "El arte del rodillo volvió al placeholder.");
        }

        [Test]
        public void BossAndReel_DoNotShareArtPrefabNorPortrait()
        {
            // Assert — con el mismo arte para los dos, la fila de rodillos se deja de distinguir de
            // la máquina que los sostiene, que es lo que hacía ilegible la pinza.
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
            // Assert
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(BandidaAssetBuilder.BossArtPrefabPath),
                $"Falta el arte del jefe en '{BandidaAssetBuilder.BossArtPrefabPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(BandidaAssetBuilder.ReelArtPrefabPath),
                $"Falta el arte del rodillo en '{BandidaAssetBuilder.ReelArtPrefabPath}'.");

            // Se chequean como Texture2D y no como Sprite a propósito: los símbolos de casino están
            // importados como Default y recién EnsureSpriteImport (que corre en el MenuItem, no acá)
            // los pasa a Sprite. Cargarlos como Sprite daría null sin que falte el archivo.
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Texture2D>(BandidaAssetBuilder.BossPortraitPath),
                $"Falta el retrato del jefe en '{BandidaAssetBuilder.BossPortraitPath}'.");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Texture2D>(BandidaAssetBuilder.ReelPortraitPath),
                $"Falta el retrato del rodillo en '{BandidaAssetBuilder.ReelPortraitPath}'.");
        }

        // ======================================================================
        // Spec de retinte
        // ======================================================================

        [Test]
        public void BossRetintKeys_AllMatchAMaterialOfTheArt()
        {
            // Arrange
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();
            var artMaterials = MaterialNamesOf(BandidaAssetBuilder.BossArtPrefabPath);

            // Assert — el builder compartido sólo tira un warning cuando una key no matchea, y el
            // síntoma es que el jefe sale con el color de fábrica: nada que se note sin abrir la escena.
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
            // Arrange
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();

            // Assert — los labels guardados en PA_MainPalette están desalineados respecto de la tabla
            // de PaletteSlots: pedir "slot Red" no garantiza rojo, los colores directos sí.
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
            // Arrange
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();
            var chassis = spec.Retints[ChassisSourceMaterialName].MidColor.Value;
            var hardware = spec.Retints[HardwareSourceMaterialName].MidColor.Value;

            // Assert — Mat_Gold cubre torso, brazos y piernas: es la carcasa, y la carcasa de una
            // tragamonedas es roja. El dorado se reserva para los herrajes.
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
            // Arrange
            var spec = BandidaAssetBuilder.BuildBossWrapperSpec();
            var artMaterials = MaterialNamesOf(BandidaAssetBuilder.BossArtPrefabPath);

            // Assert — Mat_White es el punto de luz del torso: sobre gabinete rojo hace de "777"
            // iluminado, así que se deja compartido en vez de clonarlo para dejarlo igual.
            CollectionAssert.Contains(artMaterials, HighlightSourceMaterialName,
                "Fixture roto: el arte ya no usa el material de highlight.");
            CollectionAssert.DoesNotContain(spec.Retints.Keys, HighlightSourceMaterialName,
                "Se retintó el highlight: el torso pierde el punto de luz que lee como los 7s.");
        }

        [Test]
        public void ReelSpec_ShipsWithoutRetint_UntilSomeoneChecksTheSubmeshesInTheEditor()
        {
            // Arrange
            var spec = BandidaAssetBuilder.BuildReelWrapperSpec();

            // Assert — slotv02 ya es una tragamonedas autorada con 8 materiales por submalla; elegir
            // colores sin ver el modelo repinta la pieza equivocada (el gabinete vs la palanca).
            Assert.IsTrue(spec.Retints == null || spec.Retints.Count == 0,
                "Si se le agrega retinte al rodillo, verificar en el editor qué submalla es cuál.");
        }

        // ======================================================================
        // Wrapper del jefe
        // ======================================================================

        [Test]
        public void BossWrapper_HasTheGameplayComponentsAndTheHealthBar()
        {
            // Arrange
            var pawn = _bossWrapper.GetComponent<EntityPawn>();
            var feedback = _bossWrapper.GetComponent<PawnMaterialFeedback>();

            // Assert
            Assert.IsNotNull(pawn, "Falta EntityPawn.");
            Assert.IsNotNull(_bossWrapper.GetComponent<PawnRegistryBinding>(),
                "Sin PawnRegistryBinding el jefe no recibe hit flash.");
            Assert.IsNotNull(_bossWrapper.GetComponent<HitImpulseConsumer>(), "Falta HitImpulseConsumer.");
            Assert.Greater(GetArrayRefs(feedback, "_renderers").Count, 0,
                "PawnMaterialFeedback quedó sin renderers cableados.");
            Assert.IsNotNull(GetRef(pawn, "_healthBar"),
                "EntityPawn._healthBar sin cablear: no hay barra que inicializar al spawnear el jefe.");
        }

        [Test]
        public void BossWrapper_KeepsTheAnimatedMechArt()
        {
            // Arrange
            var art = _bossWrapper.transform.Find("Art");
            Assert.IsNotNull(art, "El arte tiene que quedar anidado en un hijo 'Art'.");

            // Assert — el set de anims (AnimCon_Mecha) es la mitad del valor de este arte: si el
            // wrapper deja de traer el Animator, el jefe se queda en T-pose toda la pelea.
            Assert.IsNotNull(art.GetComponentInChildren<Animator>(true),
                "El arte del jefe perdió el Animator.");
            Assert.Greater(art.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, 0,
                "El arte del jefe perdió los SkinnedMeshRenderers del rig.");
        }

        [Test]
        public void BossWrapper_CapsuleStaysInsideItsOwnTile()
        {
            // Arrange
            var capsule = _bossWrapper.GetComponent<CapsuleCollider>();

            // Assert — el mech está en T-pose: los bounds dan ~1.5 de radio y PawnPicker resuelve el
            // pick por collider, así que sin el clamp el jefe se come los clicks de los rodillos
            // vecinos, y romperlos es LA mecánica de esta pelea.
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
            // Arrange
            var clone = AssetDatabase.LoadAssetAtPath<Material>(ChassisCloneMaterialPath);

            // Assert — el shader ramea `_UsePalette > 0.5 ? paleta : colores directos`: sin apagar el
            // toggle el rojo queda escrito en el material pero invisible en pantalla.
            Assert.IsNotNull(clone, $"No se clonó el material de la carcasa en '{ChassisCloneMaterialPath}'.");
            Assert.AreEqual(0f, clone.GetFloat("_UsePalette"));
            AssertColorsMatch(BandidaAssetBuilder.CabinetMid, clone.GetColor("_MidColor"),
                "El clon de la carcasa no quedó con el rojo del gabinete.");
        }

        [Test]
        public void BossWrapper_DoesNotCloneTheMaterialsItLeavesAlone()
        {
            // Assert — clonar todo por si acaso llenaría el proyecto de copias idénticas al original.
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(HighlightCloneMaterialPath),
                "Mat_White no está en el retinte y terminó clonado igual.");
        }

        [Test]
        public void BossWrapper_DoesNotMutateTheSharedSourceMaterial()
        {
            // Arrange
            var source = AssetDatabase.LoadAssetAtPath<Material>(ChassisSourceMaterialPath);

            // Assert — Mat_Gold lo comparte medio casino de enemigos: retintarlo in-place los repinta
            // a todos. El original tiene que seguir resolviendo por paleta.
            Assert.IsNotNull(source, $"Fixture roto: no existe '{ChassisSourceMaterialPath}'.");
            Assert.AreEqual(1f, source.GetFloat("_UsePalette"),
                "El builder pisó el material original en vez de clonarlo.");
        }

        // ======================================================================
        // Wrapper del rodillo
        // ======================================================================

        [Test]
        public void ReelWrapper_IsAStaticObject_WithNoAnimator()
        {
            // Arrange
            var art = _reelWrapper.transform.Find("Art");
            Assert.IsNotNull(art, "Falta el hijo 'Art' del rodillo.");

            // Assert — un rodillo que respira como un enemigo se lee como un enemigo; el jugador
            // tiene que ver un objeto rompible, no otro bicho en la cola de turnos.
            Assert.IsEmpty(_reelWrapper.GetComponentsInChildren<Animator>(true),
                "El rodillo salió con Animator: se confunde con un enemigo que actúa.");
            Assert.Greater(art.GetComponentsInChildren<MeshRenderer>(true).Length, 0,
                "El arte del rodillo quedó sin MeshRenderers.");
        }

        [Test]
        public void ReelWrapper_UsesABoxColliderLiftedTogetherWithTheArt()
        {
            // Arrange
            var box = _reelWrapper.GetComponent<BoxCollider>();
            var art = _reelWrapper.transform.Find("Art");

            // Assert — Box y no capsule: la máquina es una caja y el pick de un blanco de 3 de vida
            // tiene que cubrir su silueta entera.
            Assert.IsNotNull(box, "El rodillo quedó sin BoxCollider.");
            Assert.IsNull(_reelWrapper.GetComponent<CapsuleCollider>(),
                "Quedaron dos colliders en el root del rodillo.");

            // slotv02 trae su malla en un hijo a y = -0.5 (las salas lo compensan colocando la
            // instancia a y = +1): sin el lift la máquina aparece medio tile hundida en el piso.
            Assert.AreEqual(BandidaAssetBuilder.ReelArtYLift, art.localPosition.y, 0.001f,
                "El arte del rodillo perdió el lift que cancela el offset interno de slotv02.");
            Assert.Greater(box.center.y, 0f,
                "El box quedó centrado debajo del arte: el pick no cae sobre la máquina.");
        }

        [Test]
        public void ReelWrapper_HasItsOwnHealthBar_SoChippingItReadsAsProgress()
        {
            // Arrange
            var canvas = _reelWrapper.transform.Find("Canvas");

            // Assert — AINode_SpawnReels inicializa pawn.HealthBar cada vez que repone un rodillo:
            // sin barra cableada, sus 3 de vida no se ven en ninguna parte.
            Assert.IsNotNull(canvas, "El rodillo quedó sin canvas de barra.");
            Assert.IsNotNull(canvas.GetComponent<WorldSpaceHealthBar>(),
                "El canvas del rodillo quedó sin WorldSpaceHealthBar.");
            Assert.IsNotNull(GetRef(_reelWrapper.GetComponent<EntityPawn>(), "_healthBar"),
                "EntityPawn._healthBar sin cablear: pawn.HealthBar es null en el spawn del rodillo.");
        }

        [Test]
        public void ReelWrapper_HangsItsBarLowerThanTheBossBar()
        {
            // Assert — con las cuatro barras a la misma altura la fila queda una sopa de barras y no
            // se distingue la del jefe de las de los rodillos.
            Assert.Less(BandidaAssetBuilder.ReelHealthBarOffset.y,
                BandidaAssetBuilder.BossHealthBarOffset.y,
                "La barra del rodillo dejó de estar más abajo que la del jefe.");
        }

        // ======================================================================
        // Idempotencia
        // ======================================================================

        [Test]
        public void RebuildingTheBoss_KeepsTheGuidAndReappliesTheCapsuleClamp()
        {
            // Arrange
            string guidBefore = AssetDatabase.AssetPathToGUID(BossWrapperPath);
            Assert.IsNotEmpty(guidBefore, "Fixture roto: el wrapper del jefe no está en disco.");

            // Act
            var again = BandidaAssetBuilder.BuildBossVisual(BossWrapperPath, MaterialsFolder);

            // Assert — los EnemyDataSO referencian el wrapper por GUID: si el rebuild lo cambia, el
            // jefe queda sin VisualPrefab y el spawn tira error.
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
            // Act
            var again = BandidaAssetBuilder.BuildReelVisual(ReelWrapperPath, MaterialsFolder);

            // Assert — el post-proceso setea la posición en absoluto, no suma un delta: dos corridas
            // dan el mismo prefab.
            Assert.IsNotNull(again, "El rebuild del rodillo devolvió null.");
            Assert.AreEqual(BandidaAssetBuilder.ReelArtYLift,
                again.transform.Find("Art").localPosition.y, 0.001f,
                "El lift del arte se perdió o se acumuló al reconstruir.");
            Assert.AreEqual(1, again.GetComponents<BoxCollider>().Length, "Se duplicó el box del rodillo.");
        }

        // ======================================================================
        // Populate
        // ======================================================================

        [Test]
        public void PopulateEnemyData_AssignsTheVisualPrefabAndThePortrait()
        {
            // Arrange
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var reel = ScriptableObject.CreateInstance<EnemyDataSO>();
            var portrait = CreateRuntimeSprite();
            try
            {
                // Act
                BandidaAssetBuilder.PopulateEnemyData(boss, reel, _bossWrapper, portrait);

                // Assert
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
            // Arrange
            var reel = ScriptableObject.CreateInstance<EnemyDataSO>();
            var portrait = CreateRuntimeSprite();
            try
            {
                // Act
                BandidaAssetBuilder.PopulateReelData(reel, _reelWrapper, portrait);

                // Assert
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
            // Arrange
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var reel = ScriptableObject.CreateInstance<EnemyDataSO>();
            var portrait = CreateRuntimeSprite();
            try
            {
                boss.VisualPrefab = _bossWrapper;
                boss.Portrait = portrait;

                // Act — los tests de wiring del árbol llaman al populate sin assets: no puede ser la
                // vía por la que el jefe pierde su arte.
                BandidaAssetBuilder.PopulateEnemyData(boss, reel, null, null);

                // Assert
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

        // ======================================================================
        // Helpers
        // ======================================================================

        /// <summary>
        /// Comparación por canal con tolerancia: el color viaja del <c>Color32</c> del builder al YAML
        /// del material y vuelve, así que comparar structs exactos rompería por el último bit.
        /// </summary>
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

        /// <summary>
        /// Sprite in-memory: los retratos reales son texturas importadas como Default, y forzarlas a
        /// Sprite desde un test dejaría tocado el <c>.meta</c> de un asset del proyecto.
        /// </summary>
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
