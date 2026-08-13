using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Tests de <see cref="BossVisualWrapperBuilder"/> contra arte real del proyecto
    /// (<c>Healer_Animated</c> + la ruleta), construyendo en una carpeta temporal bajo
    /// <c>Assets/</c> que se borra en el teardown.
    /// </summary>
    /// <remarks>
    /// A diferencia del resto de los tests de builders, estos <b>sí</b> tocan el
    /// <c>AssetDatabase</c>: lo que se está verificando es precisamente el prefab que queda
    /// escrito (componentes, jerarquía, materiales clonados, GUID estable), y eso no se puede
    /// afirmar sobre una instancia in-memory.
    /// </remarks>
    [TestFixture]
    public class BossVisualWrapperBuilderTests
    {
        private const string ArtPrefabPath = "Assets/Prefabs/Enemies/Healer_Animated.prefab";
        private const string PropPrefabPath = "Assets/Prefabs/Props/Ruletav03.prefab";

        private const string TestRoot = "Assets/Rollgeon/__BossWrapperTests";
        private const string MaterialsFolder = TestRoot + "/Materials";
        private const string WrapperPath = TestRoot + "/PF_Boss_TestDummy.prefab";

        private const string BossName = "TestDummy";

        /// <summary>Material que el arte del Healer reusa en varios renderers — fixture del dedupe.</summary>
        private const string SharedSourceMaterialName = "Mat_Red";

        private const string SourceMaterialAssetPath = "Assets/Art/3D/Materials/Mat_Red.mat";
        private const string CloneMaterialPath = MaterialsFolder + "/Mat_TestDummy_Red.mat";

        private const string PropInstanceName = "Wheel";

        private static readonly Vector3 PropLocalPosition = new Vector3(1f, 0f, 2f);
        private static readonly Vector3 PropLocalEuler = new Vector3(0f, 90f, 0f);
        private static readonly Vector3 PropLocalScale = new Vector3(0.5f, 0.5f, 0.5f);

        private GameObject _wrapper;
        private float _sourceSlotBeforeBuild;

        // ======================================================================
        // Fixture
        // ======================================================================

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(ArtPrefabPath),
                $"Fixture roto: no existe el arte '{ArtPrefabPath}'.");

            // Se captura antes de construir para poder afirmar que el retinte no pisó el original,
            // sin hardcodear el slot (que es dato de arte y puede cambiar).
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialAssetPath);
            Assert.IsNotNull(source, $"Fixture roto: no existe '{SourceMaterialAssetPath}'.");
            _sourceSlotBeforeBuild = source.GetFloat("_PaletteSlot");

            BossVisualWrapperBuilder.EnsureFolder(TestRoot);

            _wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildStandardSpec());
            Assert.IsNotNull(_wrapper, "El build del wrapper estándar devolvió null.");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Borra prefabs y materiales clonados de una sola vez.
            if (AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        private static BossWrapperSpec BuildStandardSpec(string outputPath = WrapperPath)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtPrefabPath,
                OutputPrefabPath = outputPath,
                BossName = BossName,
                MaterialsFolder = MaterialsFolder,
                Retints = new Dictionary<string, MaterialRetint>
                {
                    { SharedSourceMaterialName, MaterialRetint.FromSlot(PaletteSlots.Navy) },
                },
                Props = new List<BossPropSpec>
                {
                    new BossPropSpec
                    {
                        PrefabPath = PropPrefabPath,
                        Name = PropInstanceName,
                        LocalPosition = PropLocalPosition,
                        LocalEuler = PropLocalEuler,
                        LocalScale = PropLocalScale,
                    },
                },
            };
        }

        // ======================================================================
        // Jerarquía
        // ======================================================================

        [Test]
        public void BuildWrapper_NestsTheArtUnderAnArtChild()
        {
            // Assert
            var art = _wrapper.transform.Find("Art");
            Assert.IsNotNull(art, "El arte tiene que quedar anidado en un hijo 'Art'.");
            Assert.Greater(art.GetComponentsInChildren<Renderer>(true).Length, 0,
                "El hijo 'Art' tiene que traer los renderers del prefab de arte.");
        }

        [Test]
        public void BuildWrapper_KeepsTheArtChildAtIdentity()
        {
            // Assert — el collider se dimensiona asumiendo el arte en el origen del wrapper.
            var art = _wrapper.transform.Find("Art");
            Assert.AreEqual(Vector3.zero, art.localPosition);
            Assert.AreEqual(Vector3.one, art.localScale);
        }

        // ======================================================================
        // Componentes de gameplay
        // ======================================================================

        [Test]
        public void BuildWrapper_AddsTheGameplayComponentsOnTheRoot()
        {
            // Assert
            Assert.IsNotNull(_wrapper.GetComponent<EntityPawn>(), "Falta EntityPawn.");
            Assert.IsNotNull(_wrapper.GetComponent<PawnRegistryBinding>(), "Falta PawnRegistryBinding.");
            Assert.IsNotNull(_wrapper.GetComponent<HitImpulseConsumer>(), "Falta HitImpulseConsumer.");
            Assert.IsNotNull(_wrapper.GetComponent<PawnMaterialFeedback>(), "Falta PawnMaterialFeedback.");
        }

        [Test]
        public void BuildWrapper_PutsTheColliderOnTheRoot_SoPawnPickerCanTargetIt()
        {
            // Assert — PawnPicker resuelve el pick con GetComponentInParent desde el collider.
            var collider = _wrapper.GetComponent<Collider>();
            Assert.IsNotNull(collider, "El collider va en el root, junto al EntityPawn.");
        }

        [Test]
        public void BuildWrapper_SizesTheCapsuleToTheArtBounds()
        {
            // Arrange — se recalculan los bounds sobre una instancia aparte del mismo arte.
            var probe = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ArtPrefabPath)) as GameObject;
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;

            Bounds expected = default;
            bool any = false;
            foreach (var r in probe.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (any) expected.Encapsulate(r.bounds);
                else { expected = r.bounds; any = true; }
            }
            Assert.IsTrue(any, "Fixture roto: el arte no tiene renderers.");

            // Act
            var capsule = _wrapper.GetComponent<CapsuleCollider>();

            // Assert
            Assert.IsNotNull(capsule, "El default de ColliderKind es Capsule.");
            Assert.AreEqual(1, capsule.direction, "El capsule tiene que ir en Y — los pawns están de pie.");
            Assert.AreEqual(expected.size.y, capsule.height, 0.05f);
            Assert.AreEqual(Mathf.Max(expected.extents.x, expected.extents.z), capsule.radius, 0.05f);
            Assert.AreEqual(expected.center.y, capsule.center.y, 0.05f);

            Object.DestroyImmediate(probe);
        }

        [Test]
        public void BuildWrapper_WiresTheMaterialFeedbackRenderers()
        {
            // Assert — se cablean explícito para no depender del auto-populate de Awake.
            var feedback = _wrapper.GetComponent<PawnMaterialFeedback>();
            var renderers = GetArrayRefs(feedback, "_renderers");

            Assert.Greater(renderers.Count, 0, "PawnMaterialFeedback quedó sin renderers.");
            CollectionAssert.DoesNotContain(renderers, null, "Hay renderers null en el array.");
        }

        [Test]
        public void BuildWrapper_ExcludesPropRenderersFromFeedback_ByDefault()
        {
            // Arrange
            var feedback = _wrapper.GetComponent<PawnMaterialFeedback>();
            var wired = GetArrayRefs(feedback, "_renderers");
            var propRoot = _wrapper.transform.Find(PropInstanceName);
            Assert.IsNotNull(propRoot, "Fixture roto: no se instanció el prop.");

            // Assert
            foreach (var r in propRoot.GetComponentsInChildren<Renderer>(true))
            {
                CollectionAssert.DoesNotContain(wired, r,
                    "Con IncludePropRenderersInFeedback en false, los props no entran al hit flash.");
            }
        }

        // ======================================================================
        // Barra de vida
        // ======================================================================

        [Test]
        public void BuildWrapper_BuildsAWorldSpaceHealthBarCanvas()
        {
            // Assert
            var canvasTransform = _wrapper.transform.Find("Canvas");
            Assert.IsNotNull(canvasTransform, "Falta el hijo 'Canvas' de la barra.");

            var canvas = canvasTransform.GetComponent<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode,
                "La barra flota en el mundo sobre el jefe.");
        }

        [Test]
        public void BuildWrapper_LaysOutTheHealthBarPiecesInDrawOrder()
        {
            // Assert — el marco tiene que dibujarse sobre el relleno, y el texto sobre todo.
            var canvas = _wrapper.transform.Find("Canvas");
            var expected = new[] { "LifeBackground", "LifeFill", "Frame", "HealthText" };

            Assert.AreEqual(expected.Length, canvas.childCount);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], canvas.GetChild(i).name, $"Hijo {i} fuera de orden.");
        }

        [Test]
        public void BuildWrapper_MakesTheFillImageHorizontallyFilled()
        {
            // Assert — con type Simple la barra no se consumiría al recibir daño.
            var fill = _wrapper.transform.Find("Canvas/LifeFill").GetComponent<Image>();

            Assert.IsNotNull(fill);
            Assert.AreEqual(Image.Type.Filled, fill.type);
            Assert.AreEqual(Image.FillMethod.Horizontal, fill.fillMethod);
            Assert.AreEqual(1f, fill.fillAmount);
            Assert.IsNotNull(fill.sprite, "El relleno quedó sin sprite del atlas.");
        }

        [Test]
        public void BuildWrapper_WiresTheHealthBarToItsPieces()
        {
            // Assert
            var healthBar = _wrapper.transform.Find("Canvas").GetComponent<WorldSpaceHealthBar>();
            Assert.IsNotNull(healthBar, "El WorldSpaceHealthBar va en el Canvas.");

            Assert.IsNotNull(GetRef(healthBar, "_fillImage"), "_fillImage sin cablear.");
            Assert.IsNotNull(GetRef(healthBar, "_hpText"), "_hpText sin cablear.");
            Assert.IsNotNull(GetRef(healthBar, "_barRoot"), "_barRoot sin cablear.");
            Assert.AreEqual("{0}/{1}", GetString(healthBar, "_textFormat"));
        }

        [Test]
        public void BuildWrapper_PointsThePawnAtTheHealthBar()
        {
            // Assert
            var pawn = _wrapper.GetComponent<EntityPawn>();
            Assert.IsNotNull(GetRef(pawn, "_healthBar"),
                "EntityPawn._healthBar tiene que apuntar a la barra creada.");
        }

        [Test]
        public void BuildWrapper_LeavesTheHealthBarOutOfTheCursorRaycast()
        {
            // Assert — si la barra fuera raycast target, se comería el hover del targeting del pawn.
            var canvas = _wrapper.transform.Find("Canvas");
            Assert.IsNull(canvas.GetComponent<GraphicRaycaster>(),
                "La barra no lleva GraphicRaycaster.");

            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
                Assert.IsFalse(graphic.raycastTarget, $"'{graphic.name}' quedó como raycast target.");
        }

        [Test]
        public void BuildWrapper_SkipsTheHealthBar_WhenSpecSaysSo()
        {
            // Arrange
            const string path = TestRoot + "/PF_Boss_NoBar.prefab";
            var spec = BuildStandardSpec(path);
            spec.AddHealthBar = false;

            // Act
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(spec);

            // Assert
            Assert.IsNotNull(wrapper);
            Assert.IsNull(wrapper.transform.Find("Canvas"), "No debería haber canvas de barra.");
            Assert.IsNull(GetRef(wrapper.GetComponent<EntityPawn>(), "_healthBar"),
                "Sin barra, EntityPawn._healthBar queda null.");
        }

        // ======================================================================
        // Props
        // ======================================================================

        [Test]
        public void BuildWrapper_ParentsPropsWithTheGivenLocalTransform()
        {
            // Assert
            var prop = _wrapper.transform.Find(PropInstanceName);
            Assert.IsNotNull(prop, $"Falta el prop '{PropInstanceName}'.");

            Assert.AreEqual(PropLocalPosition.x, prop.localPosition.x, 0.001f);
            Assert.AreEqual(PropLocalPosition.z, prop.localPosition.z, 0.001f);
            Assert.AreEqual(PropLocalEuler.y, prop.localEulerAngles.y, 0.01f);
            Assert.AreEqual(PropLocalScale.x, prop.localScale.x, 0.001f);
        }

        [Test]
        public void BuildWrapper_SkipsMissingProps_WithoutFailingTheBuild()
        {
            // Arrange
            const string path = TestRoot + "/PF_Boss_BadProp.prefab";
            var spec = BuildStandardSpec(path);
            spec.Props = new List<BossPropSpec>
            {
                new BossPropSpec { PrefabPath = "Assets/Nope/DoesNotExist.prefab" },
            };

            LogAssert.Expect(LogType.Warning, new Regex("Prop no encontrado"));

            // Act
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(spec);

            // Assert — un prop que falta degrada, no cancela: el jefe sigue siendo jugable.
            Assert.IsNotNull(wrapper, "Un prop inexistente no debe invalidar el wrapper.");
            Assert.IsNotNull(wrapper.GetComponent<EntityPawn>());
        }

        // ======================================================================
        // Retinte
        // ======================================================================

        [Test]
        public void BuildWrapper_ClonesTheRetintedMaterialToTheBossFolder()
        {
            // Assert
            var clone = AssetDatabase.LoadAssetAtPath<Material>(CloneMaterialPath);
            Assert.IsNotNull(clone, $"No se creó el clon en '{CloneMaterialPath}'.");
            Assert.AreEqual((float)PaletteSlots.Navy, clone.GetFloat("_PaletteSlot"),
                "El clon no quedó con el slot pedido.");
            Assert.AreEqual(1f, clone.GetFloat("_UsePalette"),
                "Con PaletteSlot, _UsePalette tiene que quedar prendido.");
        }

        [Test]
        public void BuildWrapper_DoesNotMutateTheSharedSourceMaterial()
        {
            // Assert — Mat_Red lo usan medio casino de enemigos; retintarlo in-place los repinta todos.
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialAssetPath);
            Assert.AreEqual(_sourceSlotBeforeBuild, source.GetFloat("_PaletteSlot"),
                "El builder pisó el material original en vez de clonarlo.");
        }

        [Test]
        public void BuildWrapper_ReusesOneCloneForAMaterialSharedAcrossRenderers()
        {
            // Arrange
            var art = _wrapper.transform.Find("Art");
            var distinctClones = new HashSet<Material>();
            int slotsPointingAtTheClone = 0;

            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (material.name != "Mat_TestDummy_Red") continue;
                    distinctClones.Add(material);
                    slotsPointingAtTheClone++;
                }
            }

            // Assert — el arte reusa Mat_Red en varios renderers: tiene que haber UN clon, no uno
            // por slot, o el batching se rompe y los clones divergen al tunear.
            Assert.Greater(slotsPointingAtTheClone, 1,
                "Fixture roto: se esperaba el material compartido en más de un slot.");
            Assert.AreEqual(1, distinctClones.Count,
                "Se clonó el material una vez por renderer en vez de dedupear.");
        }

        [Test]
        public void BuildWrapper_LeavesNonRetintedMaterialsShared()
        {
            // Arrange
            var art = _wrapper.transform.Find("Art");
            bool foundSharedOriginal = false;

            // Assert — clonar todo llenaría el proyecto de copias idénticas al original.
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    if (material.name.StartsWith($"Mat_{BossName}_")) continue;

                    var path = AssetDatabase.GetAssetPath(material);
                    Assert.IsFalse(path.StartsWith(MaterialsFolder),
                        $"'{material.name}' no estaba en el retinte y terminó clonado.");
                    foundSharedOriginal = true;
                }
            }

            Assert.IsTrue(foundSharedOriginal,
                "Fixture roto: se esperaba al menos un material sin retintar.");
        }

        [Test]
        public void BuildWrapper_WarnsWhenARetintKeyMatchesNoMaterial()
        {
            // Arrange
            const string path = TestRoot + "/PF_Boss_BadRetint.prefab";
            var spec = BuildStandardSpec(path);
            spec.Retints = new Dictionary<string, MaterialRetint>
            {
                { "Mat_NoExiste", MaterialRetint.FromSlot(PaletteSlots.Gold) },
            };

            // El síntoma de un typo acá es que el jefe sale con el color de fábrica y nada lo grita.
            LogAssert.Expect(LogType.Warning, new Regex("Mat_NoExiste"));

            // Act
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(spec);

            // Assert
            Assert.IsNotNull(wrapper);
        }

        [Test]
        public void BuildWrapper_TurnsOffThePaletteToggle_WhenGivenDirectColors()
        {
            // Arrange
            const string path = TestRoot + "/PF_Boss_DirectColors.prefab";
            const string clonePath = MaterialsFolder + "/Mat_Direct_Red.mat";
            var spec = BuildStandardSpec(path);
            spec.BossName = "Direct";
            spec.Props = null;
            spec.Retints = new Dictionary<string, MaterialRetint>
            {
                {
                    SharedSourceMaterialName,
                    MaterialRetint.FromColors(Color.white, Color.green, Color.black)
                },
            };

            // Act
            BossVisualWrapperBuilder.BuildWrapper(spec);

            // Assert — el shader ramea `_UsePalette > 0.5 ? paleta : colores directos`: sin apagar el
            // toggle, los colores quedan escritos pero invisibles.
            var clone = AssetDatabase.LoadAssetAtPath<Material>(clonePath);
            Assert.IsNotNull(clone, $"No se creó el clon en '{clonePath}'.");
            Assert.AreEqual(0f, clone.GetFloat("_UsePalette"));
            Assert.AreEqual(Color.green, clone.GetColor("_MidColor"));
        }

        // ======================================================================
        // Idempotencia y defensa
        // ======================================================================

        [Test]
        public void BuildWrapper_RebuildingPreservesThePrefabGuid()
        {
            // Arrange
            const string path = TestRoot + "/PF_Boss_Rebuilt.prefab";
            var first = BossVisualWrapperBuilder.BuildWrapper(BuildStandardSpec(path));
            Assert.IsNotNull(first);
            string guidBefore = AssetDatabase.AssetPathToGUID(path);
            Assert.IsNotEmpty(guidBefore);

            // Act
            var second = BossVisualWrapperBuilder.BuildWrapper(BuildStandardSpec(path));

            // Assert — los EnemyDataSO referencian este prefab por GUID: si cambia, quedan en null.
            Assert.IsNotNull(second, "El rebuild devolvió null.");
            Assert.AreEqual(guidBefore, AssetDatabase.AssetPathToGUID(path),
                "El rebuild cambió el GUID del prefab.");
            Assert.IsNotNull(second.GetComponent<EntityPawn>(),
                "El rebuild dejó el prefab sin componentes.");
        }

        [Test]
        public void BuildWrapper_RebuildingDoesNotDuplicateComponents()
        {
            // Arrange
            const string path = TestRoot + "/PF_Boss_NoDupes.prefab";
            BossVisualWrapperBuilder.BuildWrapper(BuildStandardSpec(path));

            // Act
            var second = BossVisualWrapperBuilder.BuildWrapper(BuildStandardSpec(path));

            // Assert — se reconstruye desde cero, no se parchea el prefab viejo.
            Assert.AreEqual(1, second.GetComponents<EntityPawn>().Length);
            Assert.AreEqual(1, second.GetComponents<PawnMaterialFeedback>().Length);
            Assert.AreEqual(1, second.GetComponents<Collider>().Length);
            Assert.AreEqual(1, CountNamed(second.transform, "Canvas"),
                "Se duplicó el canvas de la barra en el rebuild.");
            Assert.AreEqual(1, CountNamed(second.transform, "Art"),
                "Se duplicó el arte en el rebuild.");
            Assert.AreEqual(1, CountNamed(second.transform, PropInstanceName),
                "Se duplicó el prop en el rebuild.");
        }

        [Test]
        public void BuildWrapper_ReturnsNull_WhenTheArtPrefabIsMissing()
        {
            // Arrange
            var spec = BuildStandardSpec(TestRoot + "/PF_Boss_NoArt.prefab");
            spec.ArtPrefabPath = "Assets/Nope/Missing_Animated.prefab";

            LogAssert.Expect(LogType.Error, new Regex("No hay prefab de arte"));

            // Act
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(spec);

            // Assert
            Assert.IsNull(wrapper);
        }

        [Test]
        public void BuildWrapper_ReturnsNull_WhenOutputPathIsNotAPrefab()
        {
            // Arrange
            var spec = BuildStandardSpec(TestRoot + "/PF_Boss_Wrong.asset");

            LogAssert.Expect(LogType.Error, new Regex("\\.prefab"));

            // Act + Assert
            Assert.IsNull(BossVisualWrapperBuilder.BuildWrapper(spec));
        }

        [Test]
        public void BuildWrapper_ReturnsNull_OnNullSpec()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, new Regex("spec null"));

            // Act + Assert
            Assert.IsNull(BossVisualWrapperBuilder.BuildWrapper(null));
        }

        [Test]
        public void BuildWrapper_DerivesTheBossNameFromTheOutputPath_WhenNotGiven()
        {
            // Arrange
            const string path = TestRoot + "/PF_Boss_Derived.prefab";
            var spec = BuildStandardSpec(path);
            spec.BossName = null;
            spec.MaterialsFolder = MaterialsFolder;
            spec.Props = null;

            // Act
            BossVisualWrapperBuilder.BuildWrapper(spec);

            // Assert — "PF_Boss_Derived" → "Derived": el prefijo no aporta al nombre del material.
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/Mat_Derived_Red.mat"),
                "El nombre del jefe no se derivó del output path.");
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static int CountNamed(Transform parent, string childName)
        {
            int count = 0;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == childName) count++;
            return count;
        }

        private static Object GetRef(Object target, string field)
        {
            var prop = new SerializedObject(target).FindProperty(field);
            Assert.IsNotNull(prop, $"'{target.GetType().Name}' no expone '{field}'.");
            return prop.objectReferenceValue;
        }

        private static string GetString(Object target, string field)
        {
            var prop = new SerializedObject(target).FindProperty(field);
            Assert.IsNotNull(prop, $"'{target.GetType().Name}' no expone '{field}'.");
            return prop.stringValue;
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
