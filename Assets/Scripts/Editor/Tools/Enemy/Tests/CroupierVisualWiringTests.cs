using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Tiles;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Cómo queda <b>vestido</b> el Croupier: la ficha del wrapper y que el <c>ED_</c> se lleve
    /// prefab visual y retrato. Casi todo in-memory; las dos excepciones (que el arte siga usando
    /// los materiales del retinte y que el PNG del retrato exista) <b>leen</b> el AssetDatabase pero
    /// no lo escriben — un rename silencioso ahí sólo deja un warning y colores de fábrica.
    /// </summary>
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
            // PawnPicker resuelve el pick con GetComponentInParent desde el collider del root.
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
            // Los labels de PA_MainPalette no coinciden con la tabla de PaletteSlots (Mat_Red apunta
            // hoy al slot 7, que en la tabla es "Green"): un slot no dice qué color sale.
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
            // El shader elige entre los tres por iluminación: invertidos, el cel shading queda plano.
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
            // Tres escalones: traje casi negro, capa borravino, vivos de latón. Sin la separación
            // el jefe es una mancha oscura.
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
        // Sin props
        // =====================================================================

        [Test]
        public void Spec_CarriesNoProps_SoNothingHangsOffHimWithoutMeaning()
        {
            // La ruleta que colgaba a su izquierda se fue con la mecanica: el jefe ya no canta
            // numeros, y una rueda girando sin representar nada se lee como si importara. Ademas
            // tapaba media sala desde ese lado, que en un jefe que hay que perseguir es peor que
            // decorativo.
            Assert.IsEmpty(_spec.Props,
                "Volvio a colgarsele un prop. Si es intencional, tiene que significar algo que el " +
                "jugador pueda leer, y no puede taparle la vista de la sala.");
        }

        [Test]
        public void Spec_KeepsThePropOutOfTheHitFlash()
        {
            // La rueda es mobiliario: si flasheara, el hit feedback dejaría de señalar el impacto.
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
                CroupierAssetBuilder.PopulateEnemyData(data, null, visual, portrait);

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
        public void Art_ExistsWhereTheSpecSaysItDoes()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(_spec.ArtPrefabPath),
                $"No existe el arte '{_spec.ArtPrefabPath}': el wrapper devolvería null y el jefe " +
                "quedaría sin VisualPrefab.");
        }

        [Test]
        public void Art_StillUsesEveryMaterialTheRetintTargets()
        {
            // Un rename del material fuente sólo loguea un warning: este test es donde grita.
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
            // No se llama a EnsureSpriteImport: dejaría un .meta cambiado a quien corra los tests.
            var importer = AssetImporter.GetAtPath(CroupierAssetBuilder.PortraitTexturePath)
                as TextureImporter;

            Assert.IsNotNull(importer,
                $"No hay textura importable en '{CroupierAssetBuilder.PortraitTexturePath}' — el retrato " +
                "del Croupier quedaría en null.");
        }

        [Test]
        public void BuiltPrefab_CarriesNoWheel_NorTheComponentThatSpunIt()
        {
            var built = AssetDatabase.LoadAssetAtPath<GameObject>(_spec.OutputPrefabPath);
            if (built == null)
            {
                Assert.Ignore($"'{_spec.OutputPrefabPath}' todavía no está construido — " +
                              "corré Tools → Rollgeon → Bosses → Build Croupier.");
            }

            // Se chequea sobre el prefab y no sobre el spec porque el prop viejo sobrevive en el
            // asset hasta que alguien rebuildea: un spec limpio con un prefab sucio es exactamente
            // el estado que se ve en juego y no en los tests.
            Assert.IsNull(built.transform.Find(CroupierWheelSpinVisual.DefaultWheelChildName),
                "El prefab construido todavía trae el hijo de la ruleta: falta un rebuild.");
            Assert.IsNull(built.GetComponent<CroupierWheelSpinVisual>(),
                "El prefab construido todavía trae el componente que hacía girar la rueda.");
        }

        // =====================================================================
        // La casilla de fuego — lectura del AssetDatabase, sin escritura
        // =====================================================================

        /// <summary>
        /// El fuego del jefe. Se lee del asset y no del builder porque el builder sólo lo
        /// <b>carga</b>: los números y el arte viven en el <c>.asset</c>, así que un test contra
        /// constantes no vería nunca lo que ve el jugador.
        /// </summary>
        private static SpecialTileDefinitionSO LoadFireTile() =>
            AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(CroupierAssetBuilder.CroupierFirePath);

        [Test]
        public void FireTile_CarriesArt_OrTheBurningFloorIsJustTintedQuads()
        {
            var fire = LoadFireTile();
            Assert.IsNotNull(fire,
                $"No existe {CroupierAssetBuilder.CroupierFirePath}: el nodo de ignición falla y los " +
                "turnos de quema del jefe no hacen nada.");

            // SpecialTileService.SpawnVisuals cae al overlay de quads cuando no hay prefab, y el
            // fallback no avisa: la casilla "funciona" (cobra el daño) y se ve como un highlight de
            // UI. Así se fue a jugar una vez — el piso entero prendido y ni una llama en pantalla.
            Assert.IsNotNull(fire.VisualPrefab,
                "La casilla de fuego se quedó sin VisualPrefab. No es un fallo visible en consola: " +
                "el servicio degrada solo a quads tintados y el fuego desaparece sin romper nada.");
            Assert.Greater(fire.VisualYOffset, 0.05f,
                "El visual quedó pegado al piso: con este mismo VFX el hazard viejo usaba 0.1 para " +
                "no pelear z-fighting con el tinte del tile.");
        }

        [Test]
        public void FireTile_IsNotTheDefaultWhite_SoItReadsAsFireAndNotAsAGlitch()
        {
            var fire = LoadFireTile();
            Assert.IsNotNull(fire, "Falta la casilla de fuego.");

            // Sin epsilon esto pasaría por un 0.999 invisible. El default sin tocar de
            // SpecialTileDefinitionSO es blanco, y trece de las casillas del catálogo lo tienen.
            bool isWhite = Mathf.Abs(fire.OverlayTint.r - 1f) < 0.01f
                           && Mathf.Abs(fire.OverlayTint.g - 1f) < 0.01f
                           && Mathf.Abs(fire.OverlayTint.b - 1f) < 0.01f;
            Assert.IsFalse(isWhite,
                "El tinte del fallback volvió al blanco de fábrica. Aunque haya prefab, el blanco es " +
                "el color del highlight del jugador: si el prefab falta o falla, el jugador ve la " +
                "sala marcada en blanco y lo lee como un bug, no como fuego.");
        }

        [Test]
        public void FireTile_KeepsItsOwnNumbers_AndNotTheOnesOfTheGenericTemplate()
        {
            var fire = LoadFireTile();
            Assert.IsNotNull(fire, "Falta la casilla de fuego.");

            // Este asset nació como clon de Tile_FireTemp (10 / 6 / 2). Los tres números son el
            // plan del jefe, no decoración. Cruzar y quedarse cuestan lo mismo (6 y 6), pero no
            // pesan igual: el 6 de cruzar se cobra POR CASILLA y sin escudo, porque la acción del
            // turno se fue en moverse. Un rebuild descuidado desde la plantilla los devuelve sin
            // que falle nada más.
            Assert.AreEqual(6, fire.TurnStartDamage,
                "Arrancar el turno adentro cambió de precio. Por debajo del escudo mediano del " +
                "jugador (~13) el fuego deja de ser una amenaza y pasa a ser decoración.");
            Assert.GreaterOrEqual(fire.EnterDamage, fire.TurnStartDamage,
                "Cruzar el fuego pasó a costar MENOS que quedarse parado en él. Así la jugada " +
                "óptima es correr por el paño encendido, que es lo contrario del plan del jefe.");
            Assert.AreEqual(6, fire.EnterDamage,
                "Se cobra por casilla cruzada — es lo que hace que atravesar una banda para llegar al " +
                "jefe tenga precio.");
            Assert.AreEqual(CroupierAssetBuilder.FireDurationRounds, fire.DefaultDurationRounds,
                "La duración del asset dejó de coincidir con la que autora el nodo de ignición: una " +
                "de las dos manda y no se sabe cuál.");
            Assert.IsTrue(fire.OwnerBossImmune,
                "Sin esto el jefe se quema en su propio fuego, y es un jefe que huye pegado a la " +
                "banda que acaba de prender.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Luma Rec. 601 — alcanza para ordenar los escalones de un ramp de cel shading.</summary>
        private static float Luminance(Color color) =>
            0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
    }
}
