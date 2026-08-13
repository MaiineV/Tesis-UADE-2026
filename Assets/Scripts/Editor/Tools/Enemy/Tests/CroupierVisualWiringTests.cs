using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Valida cómo queda <b>vestido</b> el Croupier: la ficha del wrapper que produce
    /// <see cref="CroupierAssetBuilder.BuildWrapperSpec"/>, y que el <c>ED_</c> se lleve prefab visual
    /// y retrato.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Casi todo es in-memory.</b> La ficha visual es un objeto puro, así que se le puede afirmar
    /// arte, retintes y transform del prop sin construir el prefab ni depender de que Unity haya
    /// reimportado nada (el accidente que ya hizo fallar suites verdes acá).
    /// </para>
    /// <para>
    /// <b>Las dos excepciones leen el AssetDatabase pero no lo escriben.</b> Que el arte siga usando
    /// los materiales que el retinte apunta, y que el PNG del retrato exista, son justo los datos que
    /// un rename silencioso rompe: el builder loguea un warning y el jefe sale con los colores de
    /// fábrica o sin retrato. Ninguna de las dos reimporta nada — convertir el PNG a Sprite es efecto
    /// del menú, y un test no tiene por qué dejarle un <c>.meta</c> cambiado a quien lo corra.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CroupierVisualWiringTests
    {
        /// <summary>Alto aproximado del arte del Healer en unidades (bastón incluido).</summary>
        private const float ArtHeight = 1.95f;

        private BossWrapperSpec _spec;

        [SetUp]
        public void SetUp()
        {
            _spec = CroupierAssetBuilder.BuildWrapperSpec();
            Assert.IsNotNull(_spec, "BuildWrapperSpec devolvió null.");
        }

        // =====================================================================
        // Arte y destino
        // =====================================================================

        [Test]
        public void Spec_DressesTheHealerArtIntoTheBossPrefab()
        {
            Assert.AreEqual("Assets/Prefabs/Enemies/Healer_Animated.prefab", _spec.ArtPrefabPath,
                "El arte del Croupier es el Healer: copa, moño, capa y bastón.");
            Assert.AreEqual("Assets/Prefabs/Enemies/Bosses/PF_Boss_Croupier.prefab", _spec.OutputPrefabPath,
                "El wrapper tiene que salir en PF_Boss_Croupier — el placeholder del Sunken Grand ya no va.");
        }

        [Test]
        public void Spec_KeepsTheHealthBarWithinReachOfTheHat()
        {
            Assert.IsTrue(_spec.AddHealthBar, "Sin barra el jefe no muestra vida en el mundo.");
            Assert.Greater(_spec.HealthBarOffset.y, ArtHeight * 0.8f,
                "La barra no puede quedar dentro del modelo.");
            Assert.Less(_spec.HealthBarOffset.y, ArtHeight * 1.6f,
                "Más arriba que esto y la barra flota despegada del sombrero.");
        }

        [Test]
        public void Spec_UsesACapsuleCollider_SoThePawnIsPickable()
        {
            // PawnPicker resuelve el pick con GetComponentInParent desde el collider: sin collider en el
            // root, el cursor no puede targetear al jefe.
            Assert.AreEqual(ColliderKind.Capsule, _spec.Collider);
        }

        // =====================================================================
        // Retinte
        // =====================================================================

        [Test]
        public void Spec_RetintsOnlyTheFabricsThatCarryTheSilhouette()
        {
            var keys = _spec.Retints.Keys.OrderBy(k => k).ToArray();

            CollectionAssert.AreEqual(
                new[] { "Mat_DarkGray", "Mat_Gold", "Mat_Red" }, keys,
                "Se retintan capa/moño (Mat_Red), traje y copa (Mat_DarkGray) y vivos (Mat_Gold). " +
                "Mat_White, Mat_Bone y Mat_Black quedan compartidos a propósito: los guantes blancos son " +
                "media lectura de 'crupier', y retintar el blanco también le cambiaría el ojo.");
        }

        [Test]
        public void Spec_GivesExplicitColors_BecauseThePaletteLabelsAreMisaligned()
        {
            // Los labels guardados en PA_MainPalette no coinciden con la tabla de PaletteSlots (Mat_Red
            // apunta hoy al slot 7, que en la tabla es "Green"): un slot no dice qué color sale.
            foreach (var pair in _spec.Retints)
            {
                Assert.IsNull(pair.Value.PaletteSlot,
                    $"'{pair.Key}' pide un PaletteSlot: los slots están desalineados, van colores directos.");
                Assert.IsTrue(pair.Value.LightColor.HasValue, $"'{pair.Key}' sin LightColor.");
                Assert.IsTrue(pair.Value.MidColor.HasValue, $"'{pair.Key}' sin MidColor.");
                Assert.IsTrue(pair.Value.ShadowColor.HasValue, $"'{pair.Key}' sin ShadowColor.");
            }
        }

        [Test]
        public void Spec_RampsGoFromLightToShadow_OnEveryRetintedMaterial()
        {
            // El shader elige entre los tres colores por iluminación: si la sombra es más clara que la
            // luz, el cel shading se ve invertido y el jefe queda plano.
            foreach (var pair in _spec.Retints)
            {
                float light = Luminance(pair.Value.LightColor.Value);
                float mid = Luminance(pair.Value.MidColor.Value);
                float shadow = Luminance(pair.Value.ShadowColor.Value);

                Assert.Greater(light, mid, $"'{pair.Key}': la luz no es más clara que el medio.");
                Assert.Greater(mid, shadow, $"'{pair.Key}': el medio no es más claro que la sombra.");
            }
        }

        [Test]
        public void Spec_KeepsTheTuxDarkerThanTheWine_AndTheBrassBrighterThanBoth()
        {
            // La legibilidad del jefe depende de estos tres escalones: traje casi negro, capa borravino,
            // vivos de latón. Sin la separación se convierte en una mancha oscura.
            float tux = Luminance(_spec.Retints["Mat_DarkGray"].MidColor.Value);
            float wine = Luminance(_spec.Retints["Mat_Red"].MidColor.Value);
            float brass = Luminance(_spec.Retints["Mat_Gold"].MidColor.Value);

            Assert.Less(tux, wine, "El traje tiene que ser más oscuro que la capa.");
            Assert.Less(wine, brass, "Los vivos dorados tienen que saltar de la capa.");
        }

        [Test]
        public void Spec_KeepsTheWineAndTheTuxOnTheRedSideOfTheWheel()
        {
            // Carmesí/borravino, no un rojo cualquiera: el canal rojo tiene que dominar en los dos.
            foreach (var key in new[] { "Mat_Red", "Mat_DarkGray" })
            {
                var mid = _spec.Retints[key].MidColor.Value;
                Assert.Greater(mid.r, mid.g, $"'{key}' no tira a rojo.");
                Assert.GreaterOrEqual(mid.r, mid.b, $"'{key}' tira más a azul que a rojo.");
            }
        }

        // =====================================================================
        // La ruleta
        // =====================================================================

        [Test]
        public void Spec_ParentsTheWheelWithTheNameTheSpinVisualLooksFor()
        {
            Assert.AreEqual(1, _spec.Props.Count, "El Croupier trae un solo prop: la ruleta.");

            var wheel = _spec.Props[0];
            Assert.AreEqual("Assets/Prefabs/Props/Ruletav03.prefab", wheel.PrefabPath);
            Assert.AreEqual(CroupierWheelSpinVisual.DefaultWheelChildName, wheel.Name,
                "El nombre del hijo es el contrato con CroupierWheelSpinVisual: si cambia, el builder no " +
                "encuentra la rueda que cablear y el fallback por nombre tampoco.");
        }

        [Test]
        public void Spec_LeavesTheWheelFacingTheCamera()
        {
            // El disco del prop mira a ±Z y el jefe encara -Z (ojos y moño están en -Z): sin rotarlo la
            // cara de la rueda queda hacia la cámara y el giro se ve. Rotarlo la pondría de perfil.
            Assert.AreEqual(Vector3.zero, _spec.Props[0].LocalEuler);
        }

        [Test]
        public void Spec_PutsTheWheelBesideTheBoss_ClearOfTheStaffAndOffTheFloor()
        {
            var wheel = _spec.Props[0];

            Assert.Less(wheel.LocalPosition.x, 0f,
                "El bastón lo lleva en +X: la rueda va del otro lado o lo atraviesa.");
            Assert.Greater(Mathf.Abs(wheel.LocalPosition.x), 0.7f,
                "Más cerca que esto y la rueda entra en el cuerpo del jefe (ancho ~±0.62).");
            Assert.Greater(wheel.LocalPosition.y, 0.5f, "La rueda no puede quedar hundida en el piso.");
            Assert.Less(wheel.LocalPosition.y, ArtHeight, "Ni flotando arriba del sombrero.");
        }

        [Test]
        public void Spec_ScalesTheWheelUniformly_AndNoBiggerThanTheAuthoredSize()
        {
            var scale = _spec.Props[0].LocalScale;

            Assert.AreEqual(scale.x, scale.y, 0.0001f, "Escala no uniforme: la rueda saldría ovalada.");
            Assert.AreEqual(scale.x, scale.z, 0.0001f);
            Assert.Greater(scale.x, 0f);
            Assert.LessOrEqual(scale.x, 1f, "La rueda acompaña al jefe, no lo tapa.");
        }

        [Test]
        public void Spec_KeepsThePropOutOfTheHitFlash()
        {
            // La rueda es mobiliario, no cuerpo: si flasheara con cada golpe, el hit feedback dejaría de
            // señalar dónde pegó el jugador.
            Assert.IsFalse(_spec.IncludePropRenderersInFeedback);
        }

        // =====================================================================
        // Ficha del jefe
        // =====================================================================

        [Test]
        public void PopulateEnemyData_TakesTheVisualPrefabAndThePortrait()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;

            var visual = new GameObject("PF_Boss_Croupier") { hideFlags = HideFlags.HideAndDontSave };
            var texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            var portrait = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            portrait.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                CroupierAssetBuilder.PopulateEnemyData(data, null, null, visual, portrait);

                Assert.AreSame(visual, data.VisualPrefab,
                    "El jefe tiene que llevarse el wrapper, no el placeholder del Sunken Grand.");
                Assert.AreSame(portrait, data.Portrait,
                    "Sin retrato la cola de turnos y la barra de jefe caen al visual default.");
            }
            finally
            {
                Object.DestroyImmediate(portrait);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(data);
            }
        }

        // =====================================================================
        // Fixtures de arte — lectura del AssetDatabase, sin escritura
        // =====================================================================

        [Test]
        public void Art_AndTheWheelProp_ExistWhereTheSpecSaysTheyDo()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(_spec.ArtPrefabPath),
                $"No existe el arte '{_spec.ArtPrefabPath}': el wrapper devolvería null y el jefe " +
                "quedaría sin VisualPrefab.");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(_spec.Props[0].PrefabPath),
                $"No existe la ruleta '{_spec.Props[0].PrefabPath}': el jefe sale sin rueda (el wrapper " +
                "saltea el prop faltante).");
        }

        [Test]
        public void Art_StillUsesEveryMaterialTheRetintTargets()
        {
            // Un rename del material fuente no rompe el build: sólo loguea un warning y el jefe sale con
            // los colores de fábrica. Este test es el único lugar donde eso grita.
            var art = AssetDatabase.LoadAssetAtPath<GameObject>(_spec.ArtPrefabPath);
            Assert.IsNotNull(art, $"Fixture roto: no existe '{_spec.ArtPrefabPath}'.");

            var present = new HashSet<string>();
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null) present.Add(material.name);
                }
            }

            foreach (var key in _spec.Retints.Keys)
            {
                Assert.Contains(key, present.ToArray(),
                    $"'{_spec.ArtPrefabPath}' ya no usa '{key}'. Materiales del arte: " +
                    $"{string.Join(", ", present.OrderBy(n => n))}.");
            }
        }

        [Test]
        public void Portrait_TextureExistsAndIsImportable()
        {
            // A propósito NO se llama a EnsureSpriteImport: convertir el PNG a Sprite es efecto del menú,
            // y un test no debería dejarle un .meta cambiado a quien lo corra.
            var importer = AssetImporter.GetAtPath(CroupierAssetBuilder.PortraitTexturePath)
                as TextureImporter;

            Assert.IsNotNull(importer,
                $"No hay textura importable en '{CroupierAssetBuilder.PortraitTexturePath}' — el retrato " +
                "del Croupier quedaría en null.");
        }

        [Test]
        public void BuiltPrefab_CarriesTheWheelAndItsSpin()
        {
            var built = AssetDatabase.LoadAssetAtPath<GameObject>(_spec.OutputPrefabPath);
            if (built == null)
            {
                Assert.Ignore($"'{_spec.OutputPrefabPath}' todavía no está construido — " +
                              "corré Tools → Rollgeon → Bosses → Build Croupier.");
            }

            var wheel = built.transform.Find(CroupierWheelSpinVisual.DefaultWheelChildName);
            Assert.IsNotNull(wheel, "El prefab construido no tiene el hijo 'Wheel'.");
            Assert.IsNotNull(built.GetComponent<CroupierWheelSpinVisual>(),
                "El prefab construido no tiene el componente que gira la rueda.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Luma Rec. 601 — alcanza para ordenar los escalones de un ramp de cel shading.</summary>
        private static float Luminance(Color color) =>
            0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
    }
}
