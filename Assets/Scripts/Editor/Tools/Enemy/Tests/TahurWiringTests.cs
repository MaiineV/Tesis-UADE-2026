using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Bosses.Tahur;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Valida el árbol y los stats del Tahúr <b>en memoria</b>, vía
    /// <see cref="TahurAssetBuilder"/> — sin cargar el <c>.asset</c>.
    /// </summary>
    /// <remarks>
    /// Contra el asset, un merge desprolijo o un builder no corrido dan un rojo confuso; contra el
    /// builder, el rojo dice exactamente qué número o qué cable se movió. El asset se regenera con
    /// <c>Tools/Rollgeon/Bosses/Build Tahur</c>, que usa este mismo código.
    /// </remarks>
    [TestFixture]
    public class TahurWiringTests
    {
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _root = TahurAssetBuilder.BuildAIRoot();
            Assert.IsNotNull(_root, "BuildAIRoot devolvió null.");
        }

        // -----------------------------------------------------------------
        // Stats
        // -----------------------------------------------------------------

        [Test]
        public void EnemyData_CarriesTheCalibratedStats()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                TahurAssetBuilder.PopulateEnemyData(data);

                Assert.AreEqual("boss.tahur", data.EntityId);
                Assert.AreEqual(240, data.BaseHP,
                    "Piso 3: ~8 turnos con el golpe base del piso (mediana 30). Mismo " +
                    "presupuesto que la Generala: es el otro jefe del piso, no uno más largo.");
                Assert.AreEqual(40, data.BaseAttack);
                Assert.AreEqual(60, data.MinGoldDrop, "Gold drop de jefe de piso 3: 60-80.");
                Assert.AreEqual(80, data.MaxGoldDrop);
                Assert.IsTrue(string.IsNullOrEmpty(data.WeaknessComboId),
                    "El Tahúr no tiene debilidad a propósito: un ×1,5 encima de su ×2 de codicia " +
                    "apilaría dos multiplicadores.");
                Assert.IsInstanceOf<AINode_Sequence>(data.AIRoot);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // -----------------------------------------------------------------
        // Orden del turno
        // -----------------------------------------------------------------

        [Test]
        public void Turn_ResolvesTheMarkedPunishmentFirst()
        {
            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El primer hijo debe ser ExecuteTelegraph: el Castigo de la ronda pasada se cobra " +
                "al abrir el turno, antes de marcar uno nuevo (si no, se pisarían en el mismo guid).");
        }

        [Test]
        public void Turn_SettlesThePot_BeforeAttackingAndBeforeCalling()
        {
            int settleIdx = IndexOfSubtreeWith<AINode_TahurSettleWager>();
            int pokeIdx = IndexOfSubtreeWith<AINode_TahurPoke>();
            int callIdx = IndexOfSubtreeWith<AINode_TahurCallHand>();
            int moveIdx = IndexOfSubtreeWith<AINode_Move>();
            int tableIdx = IndexOfSubtreeWith<AINode_TahurMarkTable>();

            Assert.Greater(settleIdx, 0, "No se encontró el nodo de liquidación.");
            Assert.Less(settleIdx, pokeIdx,
                "El pozo se mueve ANTES del poke: la rama del poke depende de que la liquidación " +
                "haya dejado la ronda limpia.");
            Assert.Less(settleIdx, callIdx,
                "Liquida el canto viejo antes de cantar el nuevo — al revés se mide contra el canto " +
                "equivocado.");
            Assert.Less(callIdx, moveIdx, "El canto es previo al movimiento (el orden de la ficha).");
            Assert.Less(moveIdx, tableIdx,
                "La Mesa se pinta DESPUÉS de moverse: si se pintara antes, quedaría donde el jefe " +
                "ya no está y la ronda perfecta no pediría ningún paso.");
        }

        [Test]
        public void Turn_SweepsWithTheBanca_AfterPaintingTheTable()
        {
            int tableIdx = IndexOfSubtreeWith<AINode_TahurMarkTable>();
            int bancaIdx = IndexOfSubtreeWith<AINode_TahurMarkBanca>();

            Assert.Greater(bancaIdx, -1, "No hay nodo de La Banca en el árbol.");
            Assert.Greater(bancaIdx, tableIdx,
                "La Banca marca toda la sala MENOS La Mesa, y le resta las casillas del paño cian " +
                "tal como quedaron. Marcarla antes de pintar la mesa le restaría el paño de la " +
                "ronda pasada: el hueco seguro quedaría donde el jefe ya no está.");
        }

        [Test]
        public void Turn_TicksThePhaseGate_BeforeTheSettle()
        {
            int flipIdx = IndexOfSubtreeWith<AINode_TahurFlipCard>();
            int settleIdx = IndexOfSubtreeWith<AINode_TahurSettleWager>();

            Assert.Greater(flipIdx, -1, "No hay gate de volteo de carta en el árbol.");
            Assert.Less(flipIdx, settleIdx,
                "El gate de fase va antes de la acción: en el path no-coroutine un Running/Failed " +
                "posterior lo dejaría sin tickear.");
        }

        [Test]
        public void Turn_IsolatesEveryFailableChild_WithAWaitFallback()
        {
            for (int i = 0; i < _root.Children.Count; i++)
            {
                var child = _root.Children[i];
                if (child is AINode_ExecuteTelegraph) continue; // no es un gate: siempre Succeeded.

                var selector = child as AINode_Selector;
                Assert.IsNotNull(selector,
                    $"El hijo [{i}] ({child.NodeName}) está suelto en el Sequence raíz: si devuelve " +
                    "Failed el jefe pierde el resto del turno. Envolverlo en Selector[nodo, Wait].");
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    $"El Selector del hijo [{i}] no tiene AINode_Wait de fallback — devolvería " +
                    "Failed igual y abortaría el turno.");
            }
        }

        // -----------------------------------------------------------------
        // Números del pozo
        // -----------------------------------------------------------------

        [Test]
        public void Settle_CarriesTheCalibratedPotTable()
        {
            var settle = FindFirst<AINode_TahurSettleWager>();

            Assert.AreEqual(new[] { 26, 32, 38, 42, 45 }, settle.PotDamageTable.ToArray(),
                "Tabla del pozo calibrada el 12/08 — no tocar sin re-simular.");
            Assert.AreEqual(45, settle.DamageCeiling,
                "Techo de daño por golpe del piso 3. El Castigo máximo es EXACTAMENTE 45.");
            Assert.AreEqual(12, settle.PayoutPerChip, "Cobrar paga 12 × fichas.");
            Assert.AreEqual(5, settle.MaxChips, "La banca: el pozo tope es 5 fichas.");
            Assert.AreEqual(1, settle.MissChipGain);
            Assert.AreEqual(2, settle.GreedChipGain, "La codicia mueve el pozo dos fichas.");
            Assert.AreEqual(1, settle.RakeChipsPerRound,
                "El rastrillo corre desde la fase 1: +1 ficha por ronda. En 0 el pozo sólo se " +
                "movería con los fallos del jugador y renunciar al pozo volvería a ser una postura " +
                "estable — el Castigo clavado en 26 y La Banca inalcanzable.");
        }

        [Test]
        public void Settle_NeverExceedsTheFloorCeiling()
        {
            var settle = FindFirst<AINode_TahurSettleWager>();

            for (int chips = 1; chips <= 20; chips++)
            {
                Assert.LessOrEqual(settle.PunishmentDamageForChips(chips), 45,
                    $"El Castigo con {chips} fichas pasó el techo de 45 del piso 3.");
            }
        }

        [Test]
        public void Settle_ShapeTellsHowMuchTheHandFellShort()
        {
            var settle = FindFirst<AINode_TahurSettleWager>();

            Assert.AreEqual(4, settle.MissShapes.Count,
                "Cuatro formas de fallo: Column 1 → Row 1 → Column 3 → Scattered 4×2.");
            AssertShape(settle.ShapeForShortfall(1), ThreatShape.Column, 1, null);
            AssertShape(settle.ShapeForShortfall(2), ThreatShape.Row, 1, null);
            AssertShape(settle.ShapeForShortfall(3), ThreatShape.Column, 3, null);
            AssertShape(settle.ShapeForShortfall(4), ThreatShape.ScatteredSquares, 2, 4);
            AssertShape(settle.ShapeForShortfall(9), ThreatShape.ScatteredSquares, 2, 4,
                "Faltar más de 4 escalones usa la última forma, no una quinta inexistente.");
            AssertShape(settle.GreedShape, ThreatShape.ScatteredSquares, 2, 6,
                "La codicia usa la forma más ancha: 6 cuadrados de 2×2.");
        }

        [Test]
        public void Banca_CarriesTheFloorCeiling()
        {
            var banca = FindFirst<AINode_TahurMarkBanca>();

            Assert.AreEqual(45, banca.Damage,
                "La Banca pega el techo del piso 3, igual que el Castigo con el pozo lleno.");
            Assert.AreEqual(45, banca.DamageCeiling,
                "Sin el techo, subir el Damage de La Banca pasaría los 45 por golpe del piso 3 " +
                "sin que nada lo cante.");
            Assert.AreEqual(5, banca.ChipsThreshold,
                "Barre la mesa con el pozo lleno, y lleno son las 5 fichas de la banca.");

            Assert.AreEqual(TahurAssetBuilder.TableSize, banca.TableRadius,
                "El hueco seguro y el paño cian son la misma promesa: si el radio se separa del " +
                "Size de La Mesa, el jugador lee una zona segura que no lo es.");
            Assert.AreEqual(FindFirst<AINode_TahurMarkTable>().Size, banca.TableRadius,
                "El hueco tiene que medir lo que mide el paño que el jugador ve en pantalla, no " +
                "lo que dice una constante que alguien movió de un solo lado.");
        }

        // -----------------------------------------------------------------
        // Poke, canto, mesa, fase
        // -----------------------------------------------------------------

        [Test]
        public void Poke_IsGatedByACleanRound_AndByMeleeRange()
        {
            var gate = _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g != null && Descendants(g.Then).OfType<AINode_TahurPoke>().Any());

            Assert.IsNotNull(gate, "El poke no está detrás de un If — pegaría todas las rondas.");
            Assert.IsTrue(gate.Conditions.OfType<PcTahurCleanRound>().Any(),
                "Falta PcTahurCleanRound: el poke y el Castigo no pueden resolver la misma ronda " +
                "(12 + 45 rompe el techo de 45).");
            var inRange = gate.Conditions.OfType<PcTargetInRange>().FirstOrDefault();
            Assert.IsNotNull(inRange, "Falta PcTargetInRange: el poke es melee.");
            Assert.AreEqual(1, inRange.Range);

            var poke = FindFirst<AINode_TahurPoke>();
            Assert.AreEqual(12, poke.Damage, "Poke de la ficha v2: 12.");
            Assert.IsTrue(poke.RequireCleanRound,
                "El nodo se auto-gatea aunque el If ya lo gatee — un rewire olvidadizo no puede " +
                "convertirlo en un golpe de 57.");
        }

        [Test]
        public void Call_UsesTheSixStepValve()
        {
            var call = FindFirst<AINode_TahurCallHand>();

            Assert.AreEqual(1, call.MinRank);
            Assert.AreEqual(6, call.MaxRank, "Escalones 1-6.");
            Assert.AreEqual(5, call.HighRankThreshold);
            Assert.IsTrue(call.AvoidConsecutiveHighCalls, "Nunca dos cantos ≥5 seguidos.");
            Assert.IsTrue(call.ForbidCalledHand,
                "Armar el canto tiene que hacer 0 (R03): cobrar cuesta el ataque, no la vida.");
            Assert.AreEqual(2f, call.GreedMultiplier, "El ×2 de la codicia es la R01.");
        }

        [Test]
        public void Table_IsAThreeByThreeAroundSelf_InItsOwnColour()
        {
            var table = FindFirst<AINode_TahurMarkTable>();

            Assert.AreEqual(1, table.Size, "La Mesa es SquareAroundSelf 1 ⇒ 3×3.");
            Assert.Greater(table.Tint.b, table.Tint.r,
                "La Mesa va en cian: con el naranja del Castigo el jefe es ilegible por construcción.");
        }

        [Test]
        public void PhaseGate_FlipsTheCardOnceAtFortyPercent()
        {
            var gate = _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g != null && Descendants(g.Then).OfType<AINode_TahurFlipCard>().Any());

            Assert.IsNotNull(gate, "No hay gate de HP para el volteo de la carta.");
            var hp = gate.Conditions.OfType<PcOwnerHpBelow>().FirstOrDefault();
            Assert.IsNotNull(hp, "El gate del volteo no mira el HP del jefe.");
            Assert.AreEqual(0.40f, hp.Percent, 0.0001f, "El volteo entra al 40% de HP.");
            Assert.IsInstanceOf<AINode_Once>(gate.Then,
                "El volteo es one-shot: sin Once se re-aplicaría cada turno bajo el umbral y la " +
                "gracia se regalaría en loop.");

            var flip = FindFirst<AINode_TahurFlipCard>();
            Assert.AreEqual(1, flip.RakeChipsPerRound, "El rastrillo suma 1 ficha por ronda.");
            Assert.AreEqual(1, flip.ChipsFloorAfterFlip, "Cobrar deja el pozo en 1, nunca en 0.");
            Assert.IsTrue(flip.GraceOnFirstSettle, "La primera liquidación tras el volteo es de gracia.");
        }

        [Test]
        public void Tree_NeverSpawnsReinforcements()
        {
            Assert.IsEmpty(Descendants(_root).OfType<AINode_SpawnReinforcements>().ToList(),
                "El ×2 de la codicia es un modificador global del Contrato: con refuerzos en la " +
                "sala aplicaría también contra ellos.");
        }

        [Test]
        public void Tree_ClosesDistance_AndNeverKites()
        {
            var move = FindFirst<AINode_Move>();
            Assert.IsFalse(move.Retreat, "El Tahúr nunca kitea: el que acorta es él.");
        }

        // -----------------------------------------------------------------
        // Visual y retrato
        // -----------------------------------------------------------------

        [Test]
        public void Visual_PointsAtItsOwnWrapper_NotThePlaceholderNorTheFloorOneBoss()
        {
            Assert.AreEqual("Assets/Prefabs/Enemies/Bosses/PF_Boss_Tahur.prefab",
                TahurAssetBuilder.VisualPrefabPath,
                "El Tahúr tiene wrapper propio: mientras apuntaba al GeneralDirector era un " +
                "placeholder compartido con la Generala y el Dado de la Casa.");

            Assert.AreNotEqual("Assets/Prefabs/Enemies/SunkedGrand.prefab",
                TahurAssetBuilder.VisualPrefabPath,
                "Reusar el wrapper del jefe del piso 1 los haría gemelos: mismo arte Y misma paleta.");

            Assert.AreEqual("Assets/Prefabs/Enemies/SunkedGrand_Animated.prefab",
                TahurAssetBuilder.ArtPrefabPath,
                "El arte es el humanoide con abanico de 12 cartas — el tramposo de la ficha.");
        }

        [Test]
        public void Portrait_IsTheFaceOfTheRigHeWears()
        {
            Assert.AreEqual(BossPortraitLibrary.SheetPath, TahurAssetBuilder.PortraitTexturePath,
                "El retrato sigue al rig: el Tahúr viste SunkedGrand_Animated, cuyo retrato vive " +
                "en la hoja compartida de personajes.");
        }

        [Test]
        public void EnemyData_TakesTheVisualPrefabAndThePortrait_WhenTheyResolve()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            var pawn = new GameObject("PF_Boss_Tahur_Probe");
            var portrait = MakeSprite();
            try
            {
                TahurAssetBuilder.PopulateEnemyData(data, pawn, portrait);

                Assert.AreSame(pawn, data.VisualPrefab);
                Assert.AreSame(portrait, data.Portrait);
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(pawn);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void EnemyData_KeepsWhatItHad_WhenTheArtOrTheTextureDoNotResolve()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            var pawn = new GameObject("PF_Boss_Tahur_Probe");
            var portrait = MakeSprite();
            try
            {
                data.VisualPrefab = pawn;
                data.Portrait = portrait;

                TahurAssetBuilder.PopulateEnemyData(data);

                // Un arte que falta degrada: dejar el asset sin pawn ni retrato es peor que
                // conservar el anterior, porque el jefe deja de spawnear.
                Assert.AreSame(pawn, data.VisualPrefab);
                Assert.AreSame(portrait, data.Portrait);
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(pawn);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void WrapperSpec_BuildsIntoItsOwnPathAndMaterialsFolder()
        {
            var spec = TahurAssetBuilder.BuildWrapperSpec();

            Assert.AreEqual(TahurAssetBuilder.ArtPrefabPath, spec.ArtPrefabPath);
            Assert.AreEqual(TahurAssetBuilder.VisualPrefabPath, spec.OutputPrefabPath);
            Assert.AreEqual("Tahur", spec.BossName);
            StringAssert.StartsWith("Assets/Rollgeon/Enemies/Materials/", spec.MaterialsFolder,
                "Los clones van a la carpeta de materiales de jefes, no al lado del arte compartido.");
            Assert.IsTrue(spec.Retints != null && spec.Retints.Count > 0,
                "Sin retinte el wrapper es una copia del jefe del piso 1.");
        }

        [Test]
        public void WrapperSpec_FloatsTheHealthBarAboveTheHead()
        {
            // El arte mide ~1,81 de alto (collider a mano de SunkedGrand.prefab).
            Assert.Greater(TahurAssetBuilder.BuildWrapperSpec().HealthBarOffset.y, 1.81f,
                "La barra quedaría metida dentro del cuerpo.");
            Assert.Less(TahurAssetBuilder.BuildWrapperSpec().HealthBarOffset.y, 3f,
                "El default de 3 de la utility está dimensionado para el GeneralDirector, más alto: " +
                "acá dejaría la barra flotando despegada de la cabeza.");
        }

        // -----------------------------------------------------------------
        // Paleta
        // -----------------------------------------------------------------

        [Test]
        public void Retint_RepaintsEverySurfaceOfTheSharedArt()
        {
            var retints = TahurAssetBuilder.BuildRetints();

            // Los siete materiales que usa SunkedGrand_Animated. Uno que quede afuera se comparte
            // con el jefe del piso 1 y esa superficie sale idéntica en los dos.
            foreach (var material in new[]
                     {
                         "Mat_LightBrown", "Mat_Brown", "Mat_Green",
                         "Mat_Bone", "Mat_Black", "Mat_White", "Mat_LightGreen",
                     })
            {
                Assert.IsTrue(retints.ContainsKey(material),
                    $"'{material}' quedaría con el color de fábrica del Sunken Grand.");
            }
        }

        [Test]
        public void Retint_UsesDirectColours_NotPaletteSlots()
        {
            foreach (var pair in TahurAssetBuilder.BuildRetints())
            {
                // Los slots de PA_MainPalette están desalineados respecto de los nombres de los
                // Mat_* (Mat_LightGreen → slot 3, que renderea gris): con FromColors el color que
                // se escribe es el que se ve.
                Assert.IsFalse(pair.Value.PaletteSlot.HasValue,
                    $"'{pair.Key}' pide un slot de paleta en vez de colores explícitos.");
                Assert.IsTrue(pair.Value.LightColor.HasValue
                              && pair.Value.MidColor.HasValue
                              && pair.Value.ShadowColor.HasValue,
                    $"'{pair.Key}' no define los tres tonos: el shader dejaría el resto de fábrica.");
            }
        }

        [Test]
        public void Retint_KeepsTheCoatOnFeltGreen_AndTheTrimOnGold()
        {
            var retints = TahurAssetBuilder.BuildRetints();

            var coat = retints["Mat_LightBrown"].MidColor.Value;
            Assert.Greater(coat.g, coat.r, "La levita es fieltro de mesa: verde dominante.");
            Assert.Greater(coat.g, coat.b, "La levita es fieltro de mesa: verde dominante.");

            foreach (var trim in new[] { "Mat_Green", "Mat_Bone" })
            {
                var gold = retints[trim].MidColor.Value;
                Assert.Greater(gold.r, gold.b, $"'{trim}' es el dorado de la banca.");
                Assert.Greater(gold.g, gold.b, $"'{trim}' es el dorado de la banca.");
            }
        }

        [Test]
        public void Retint_KeepsTheBodyAwayFromItsOwnTelegraphColours()
        {
            // La Mesa se pinta en cian y el Castigo en naranja. Las superficies grandes del jefe no
            // pueden compartir esos tonos o los telegraphs desaparecen sobre su propio cuerpo.
            // El dorado (cinta de la galera y canto de las cartas) SÍ pasa cerca del naranja, y por
            // eso vive sólo en detalles finos — de ahí que no entre en esta lista.
            var telegraphOrange = new Color(1f, 0.5f, 0f);

            foreach (var surface in new[] { "Mat_LightBrown", "Mat_Brown", "Mat_White", "Mat_LightGreen" })
            {
                var mid = TahurAssetBuilder.BuildRetints()[surface].MidColor.Value;

                Assert.IsFalse(mid.b >= mid.r && mid.b >= mid.g,
                    $"'{surface}' quedó dominado por el azul/cian de la Mesa.");
                Assert.Greater(Distance(mid, telegraphOrange), 0.35f,
                    $"'{surface}' quedó demasiado cerca del naranja del Castigo.");
            }
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static float Distance(Color a, Color b)
            => new Vector3(a.r - b.r, a.g - b.g, a.b - b.b).magnitude;

        private static Sprite MakeSprite()
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

        private static void AssertShape(
            TahurPunishmentShape shape, ThreatShape expected, int size, int? count, string because = null)
        {
            Assert.IsNotNull(shape, because);
            Assert.AreEqual(expected, shape.Shape, because);
            Assert.AreEqual(size, shape.Size, because);
            if (count.HasValue) Assert.AreEqual(count.Value, shape.Count, because);
        }

        private T FindFirst<T>() where T : class
        {
            var found = Descendants(_root).OfType<T>().FirstOrDefault();
            Assert.IsNotNull(found, $"No se encontró un {typeof(T).Name} en el árbol.");
            return found;
        }

        /// <summary>Índice del hijo del Sequence raíz cuyo subárbol contiene un <typeparamref name="T"/>.</summary>
        private int IndexOfSubtreeWith<T>() where T : class
            => _root.Children.FindIndex(c => Descendants(c).OfType<T>().Any());

        /// <summary>El <see cref="AINode_If"/> de un hijo, venga suelto o dentro del Selector de aislamiento.</summary>
        private static AINode_If Unwrap(AIDecisionNode child)
        {
            if (child is AINode_If direct) return direct;
            if (child is AINode_Selector sel && sel.Children != null)
                return sel.Children.OfType<AINode_If>().FirstOrDefault();
            return null;
        }

        /// <summary>Tree-walker por reflexión — mismo patrón que <c>SunkenGrandPhaseWiringTests</c>.</summary>
        private static List<object> Descendants(object root)
        {
            var all = new List<object>();
            var visited = new HashSet<object>(RefComparer.Instance);

            void Walk(object o)
            {
                if (o == null || o is string || o is Object) return;

                var type = o.GetType();
                if (type.IsPrimitive || type.IsEnum) return;
                if (!type.IsValueType && !visited.Add(o)) return;

                all.Add(o);

                if (o is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) Walk(item);
                    return;
                }

                if (!(type.Namespace ?? string.Empty).StartsWith("Rollgeon")) return;

                foreach (var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object value;
                    try { value = field.GetValue(o); }
                    catch { continue; }
                    Walk(value);
                }
            }

            Walk(root);
            return all;
        }

        private sealed class RefComparer : IEqualityComparer<object>
        {
            public static readonly RefComparer Instance = new RefComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
