using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Vestuario de El Anotador validado en memoria, incluyendo que los tres colores que pueden
    /// convivir en el piso de esta pelea se lean distinto.</summary>
    [TestFixture]
    public class AnotadorVisualWiringTests
    {
        /// <summary>Leídos del FBX para que una re-exportación que renombre o agregue partes rompa el test
        /// en vez de dejar medio jefe con el color de fábrica.</summary>
        private static readonly string[] FbxMaterials =
        {
            "Enemy_:Wood1", "Enemy_:Frame1", "Enemy_:Flesh", "Enemy_:Theet",
            "Enemy_:Tongue", "Enemy_:Eye", "Enemy_:Material",
        };

        private const float ChannelTolerance = 0.0001f;

        [Test]
        public void WrapperSpec_NestsTheModel_NotTheChestMimicPrefab()
        {
            var spec = AnotadorAssetBuilder.BuildWrapperSpec();

            Assert.AreEqual(AnotadorAssetBuilder.ArtModelPath, spec.ArtPrefabPath);
            Assert.IsTrue(spec.ArtPrefabPath.EndsWith(".fbx"),
                "El arte es el modelo importado, no un prefab.");
            Assert.IsFalse(spec.ArtPrefabPath.Contains("ChestMimic_Prefab"),
                "ChestMimic_Prefab ya trae EntityPawn/PawnRegistryBinding y su propia barra: " +
                "anidarlo duplicaría la capa de gameplay que el wrapper agrega en el root.");
        }

        [Test]
        public void WrapperSpec_WritesToTheBossPrefabFolder()
        {
            var spec = AnotadorAssetBuilder.BuildWrapperSpec();

            Assert.AreEqual(AnotadorAssetBuilder.VisualPrefabPath, spec.OutputPrefabPath);
            Assert.IsTrue(spec.OutputPrefabPath.EndsWith(".prefab"),
                "BossVisualWrapperBuilder rechaza cualquier output que no termine en .prefab.");
            Assert.IsTrue(spec.OutputPrefabPath.StartsWith("Assets/Prefabs/Enemies/Bosses/"),
                "Los wrappers de jefes viven todos en la misma carpeta.");
            Assert.IsFalse(spec.OutputPrefabPath.Contains("SecurityGuardBoss"),
                "El placeholder estático quedó atrás: este jefe se mueve y anima.");
        }

        [Test]
        public void WrapperSpec_KeepsTheHealthBar_AtTheMimicsCanvasHeight()
        {
            var spec = AnotadorAssetBuilder.BuildWrapperSpec();

            Assert.IsTrue(spec.AddHealthBar, "Un jefe sin barra de vida no se puede leer.");
            Assert.Greater(spec.HealthBarOffset.y, 0f, "La barra va arriba del pawn, no dentro.");
            Assert.AreEqual(2.5f, spec.HealthBarOffset.y, 0.001f,
                "Misma altura que el canvas de ChestMimic_Prefab: es el mismo cuerpo.");
        }

        [Test]
        public void WrapperSpec_UsesACapsuleCollider_SoTheCursorCanTargetHim()
        {
            var spec = AnotadorAssetBuilder.BuildWrapperSpec();

            Assert.AreEqual(ColliderKind.Capsule, spec.Collider,
                "Sin collider en el root, PawnPicker no lo resuelve y el jefe es inpickeable.");
            Assert.AreEqual(AnotadorAssetBuilder.ArtChildName, spec.ArtChildName,
                "El nombre del hijo de arte es el que busca la pasada de animator/paleta.");
        }

        /// <summary>El retinte del wrapper no sirve para este FBX (nombres con <c>:</c> y shader sin las
        /// properties de paleta): poblarlo le saca al builder el ser la única fuente de verdad.</summary>
        [Test]
        public void WrapperSpec_LeavesRetintsEmpty_BecauseTheBuilderPaintsTheArtItself()
        {
            var spec = AnotadorAssetBuilder.BuildWrapperSpec();

            Assert.IsNull(spec.Retints,
                "Los materiales del mímico están embebidos en el FBX con el namespace de Maya " +
                "(Enemy_:Wood1): el clon del retinte se llamaría Mat_Anotador_Enemy_:Wood1.mat y ':' " +
                "es ilegal en un path de Windows. Ver AnotadorAssetBuilder.IcePaints.");
        }

        [Test]
        public void CanonicalMaterialName_StripsTheMayaNamespace()
        {
            Assert.AreEqual("Wood1", AnotadorAssetBuilder.CanonicalMaterialName("Enemy_:Wood1"));
            Assert.AreEqual("Wood1", AnotadorAssetBuilder.CanonicalMaterialName("Wood1"),
                "Si el importer resuelve el nombre sin namespace, el mapeo tiene que seguir andando.");
        }

        [Test]
        public void CanonicalMaterialName_StripsMatPrefixAndDuplicateSuffixes()
        {
            Assert.AreEqual("Wood1", AnotadorAssetBuilder.CanonicalMaterialName("Mat_Wood1"));
            Assert.AreEqual("Black", AnotadorAssetBuilder.CanonicalMaterialName("Enemy_:Black.002"),
                "El exportador numera los duplicados con .00N.");
            Assert.AreEqual("Red", AnotadorAssetBuilder.CanonicalMaterialName("Mat_Red 1"),
                "Unity numera los duplicados de asset con ' 1'.");
        }

        [Test]
        public void CanonicalMaterialName_EmptyInput_IsEmpty_NotAnException()
        {
            Assert.AreEqual(string.Empty, AnotadorAssetBuilder.CanonicalMaterialName(null));
            Assert.AreEqual(string.Empty, AnotadorAssetBuilder.CanonicalMaterialName("  "));
            Assert.IsNull(AnotadorAssetBuilder.PaintKeyFor(null),
                "Un slot de material vacío no pide pintura, no explota.");
        }

        [Test]
        public void EveryFbxMaterial_HasAPaint()
        {
            foreach (var material in FbxMaterials)
            {
                var key = AnotadorAssetBuilder.PaintKeyFor(material);
                Assert.IsNotNull(key,
                    $"'{material}' no está mapeado: saldría con el material del FBX, sin paleta y " +
                    "sin hit flash (el _HitFlashAmount solo existe en el shader de paleta).");
            }
        }

        [Test]
        public void EveryPaintKeyInTheMapping_Exists()
        {
            foreach (var pair in AnotadorAssetBuilder.ArtMaterialPaints)
            {
                Assert.IsTrue(AnotadorAssetBuilder.IcePaints.ContainsKey(pair.Value),
                    $"El material '{pair.Key}' apunta a la pintura '{pair.Value}', que no existe " +
                    "en IcePaints — un typo acá deja esa parte sin repintar y no avisa nada.");
            }
        }

        [Test]
        public void EveryPaint_CarriesTheThreeCelColors()
        {
            foreach (var pair in AnotadorAssetBuilder.IcePaints)
            {
                Assert.IsTrue(pair.Value.LightColor.HasValue, $"'{pair.Key}' sin LightColor.");
                Assert.IsTrue(pair.Value.MidColor.HasValue, $"'{pair.Key}' sin MidColor.");
                Assert.IsTrue(pair.Value.ShadowColor.HasValue, $"'{pair.Key}' sin ShadowColor.");

                // Colores directos y no PaletteSlot: los labels de PA_MainPalette están desalineados
                // respecto de la tabla de PaletteSlots y no hay un slot de hielo.
                Assert.IsFalse(pair.Value.PaletteSlot.HasValue,
                    $"'{pair.Key}' pide slot de paleta: con colores directos y slot a la vez el " +
                    "wrapper avisa y gana el color directo — mejor no ambiguarlo.");
            }
        }

        [Test]
        public void EveryPaint_GoesDarkerFromLightToShadow()
        {
            foreach (var pair in AnotadorAssetBuilder.IcePaints)
            {
                float light = Luminance(pair.Value.LightColor.Value);
                float mid = Luminance(pair.Value.MidColor.Value);
                float shadow = Luminance(pair.Value.ShadowColor.Value);

                Assert.Greater(light, mid, $"'{pair.Key}': la luz tiene que ser más clara que el medio.");
                Assert.Greater(mid, shadow, $"'{pair.Key}': el medio tiene que ser más claro que la sombra.");
            }
        }

        [Test]
        public void FrozenPaints_ReadCold()
        {
            // Todas menos el grafito: el hielo se lee por el azul, y una parte cálida rompería la
            // silueta congelada que hace legible al jefe de piso 2.
            foreach (var pair in AnotadorAssetBuilder.IcePaints)
            {
                if (pair.Key == "Graphite") continue;

                foreach (var color in Colors(pair.Value))
                {
                    Assert.Greater(color.b, color.r,
                        $"'{pair.Key}' tiene un color más rojo que azul: no lee como hielo.");
                }
            }
        }

        [Test]
        public void GraphitePaint_IsNeutral_LikePencilLead()
        {
            var graphite = AnotadorAssetBuilder.IcePaints["Graphite"];

            foreach (var color in Colors(graphite))
            {
                float spread = Mathf.Max(color.r, Mathf.Max(color.g, color.b))
                               - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
                Assert.Less(spread, 0.12f,
                    "El grafito de los herrajes es casi neutro: saturarlo lo volvería otra parte de " +
                    "hielo y se perdería el contraste con el cuerpo.");
            }
        }

        [Test]
        public void EyePaint_MatchesTheIceOverlay_SoTheTrailReadsAsHis()
        {
            var eye = AnotadorAssetBuilder.IcePaints["Eye"].MidColor.Value;
            var overlay = AnotadorAssetBuilder.IceOverlayTint;

            Assert.AreEqual(overlay.r, eye.r, ChannelTolerance);
            Assert.AreEqual(overlay.g, eye.g, ChannelTolerance);
            Assert.AreEqual(overlay.b, eye.b, ChannelTolerance);
        }

        [Test]
        public void MaterialPaths_NeverCarryAnIllegalCharacter()
        {
            foreach (var pair in AnotadorAssetBuilder.IcePaints)
            {
                var path = AnotadorAssetBuilder.MaterialPathFor(pair.Key);

                Assert.IsFalse(path.Substring("Assets".Length).Contains(":"),
                    $"'{path}' lleva ':' — es exactamente el path que Windows no puede escribir y la " +
                    "razón por la que el builder no usa el retinte del wrapper.");
                Assert.IsTrue(path.EndsWith(".mat"));
                Assert.IsTrue(path.StartsWith(AnotadorAssetBuilder.MaterialsFolder + "/"),
                    "Los materiales del jefe van todos en su carpeta, no sueltos.");
            }
        }

        [Test]
        public void ThePencilRing_DoesNotUseTheNodeDefaultViolet()
        {
            var violet = new Color(0.55f, 0.35f, 0.95f, 0.55f);
            Assert.Greater(Distance(AnotadorAssetBuilder.PencilOverlayTint, violet), 0.2f,
                "El violeta default de AINode_AuxTelegraph no significa nada en este juego.");
        }

        [Test]
        public void TheThreeFloorMarks_AreTellableApart()
        {
            var row = ThreatTelegraphOverlay.DefaultTint;      // fila: 30 de daño
            var trail = AnotadorAssetBuilder.IceOverlayTint;   // estela: stun
            var pencil = AnotadorAssetBuilder.PencilOverlayTint; // lápiz: 12 de daño

            Assert.Greater(Distance(row, trail), 0.3f, "Fila y estela tienen que verse distinto.");
            Assert.Greater(Distance(row, pencil), 0.3f, "Fila y lápiz tienen que verse distinto.");
            Assert.Greater(Distance(trail, pencil), 0.3f,
                "Estela y lápiz cobran cosas distintas (turno vs. 12 de daño): si se parecen, el " +
                "jugador no puede decidir por dónde salir.");
        }

        [Test]
        public void EveryOverlayTint_IsVisible()
        {
            Assert.Greater(AnotadorAssetBuilder.PencilOverlayTint.a, 0f,
                "Un tint con alpha 0 pinta quads invisibles (ver HazardDefinitionSO.EffectiveOverlayTint).");
            Assert.Greater(AnotadorAssetBuilder.IceOverlayTint.a, 0f);
        }

        [Test]
        public void IceBurst_NeverOverwritesItsOwnTemplate()
        {
            Assert.AreNotEqual(AnotadorAssetBuilder.VfxTemplatePrefabPath,
                AnotadorAssetBuilder.IceVfxPrefabPath,
                "El builder clona el glow de curación: si los paths coincidieran, un build se lo " +
                "llevaría puesto y todas las curaciones del juego saldrían celestes.");
            Assert.AreNotEqual(AnotadorAssetBuilder.VfxTemplateMaterialPath,
                AnotadorAssetBuilder.IceVfxMaterialPath);
        }

        [Test]
        public void IceBurst_PathsAreWhereTheProjectKeepsVfx()
        {
            Assert.IsTrue(AnotadorAssetBuilder.IceVfxPrefabPath.StartsWith("Assets/Prefabs/VFX/"));
            Assert.IsTrue(AnotadorAssetBuilder.IceVfxPrefabPath.EndsWith(".prefab"));
            Assert.IsTrue(AnotadorAssetBuilder.IceVfxMaterialPath.StartsWith("Assets/Materials/VFX/"));
            Assert.IsTrue(AnotadorAssetBuilder.IceVfxMaterialPath.EndsWith(".mat"));
        }

        [Test]
        public void IceBurst_IsTheSameCyanAsTheTrailItComesFrom()
        {
            var burst = AnotadorAssetBuilder.IceVfxColor;
            var overlay = AnotadorAssetBuilder.IceOverlayTint;

            Assert.AreEqual(overlay.r, burst.r, ChannelTolerance);
            Assert.AreEqual(overlay.g, burst.g, ChannelTolerance);
            Assert.AreEqual(overlay.b, burst.b, ChannelTolerance);
            Assert.AreEqual(1f, burst.a, ChannelTolerance,
                "El quad late en alpha; el burst es opaco — es el golpe, no el aviso.");
        }

        [Test]
        public void Portrait_IsTheFaceOfTheRigHeWears()
        {
            Assert.AreEqual(BossPortraitLibrary.AnotadorPath, AnotadorAssetBuilder.PortraitTexturePath,
                "El retrato sigue al rig: el Anotador viste ChestMimic, así que en la cola de " +
                "turnos tiene que aparecer el mímico y no un símbolo genérico del pack de casino.");
            Assert.IsTrue(AnotadorAssetBuilder.PortraitTexturePath.EndsWith(".png"));
        }

        [Test]
        public void AnimatorController_IsTheMimicsOwn()
        {
            Assert.IsTrue(AnotadorAssetBuilder.AnimatorControllerPath.EndsWith(".controller"));
            Assert.IsTrue(AnotadorAssetBuilder.AnimatorControllerPath.Contains("ChestMimic"),
                "Los clips (Attack/Awaken/Idle*/Movement) son los del mímico: el controller tiene " +
                "que ser el suyo o el Animator queda sin estados.");
            Assert.AreEqual("Awaken", AnotadorAssetBuilder.AwakenParameter);
            Assert.AreEqual(8, AnotadorAssetBuilder.SteppedAnimationFps,
                "8 FPS es el stepping del resto del roster animado.");
        }

        [Test]
        public void ArtChild_TurnsAroundToFaceTheRoom()
        {
            Assert.AreEqual(180f, AnotadorAssetBuilder.ArtLocalEuler.y, 0.001f,
                "BossVisualWrapperBuilder fuerza identidad en el hijo de arte y el root del FBX mira " +
                "-Z: sin los 180° el mímico entra de espaldas.");
        }

        private static IEnumerable<Color> Colors(MaterialRetint paint)
        {
            yield return paint.LightColor.Value;
            yield return paint.MidColor.Value;
            yield return paint.ShadowColor.Value;
        }

        /// <summary>Luma perceptual (Rec. 709) — "más claro que" no es la suma de los canales.</summary>
        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Distancia RGB, sin alpha: el pulso del overlay pisa el alpha en runtime.</summary>
        private static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
