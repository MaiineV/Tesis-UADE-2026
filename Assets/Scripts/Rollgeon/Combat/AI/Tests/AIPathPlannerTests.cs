using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Combat.AI.Pathing;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AIPathPlanner"/>: fórmulas exactas del GDD (HazardPenalty,
    /// TilePathCost, TerrainModifier, TacticalGain), filtro de supervivencia por personalidad,
    /// telegraph letal, daño virtual, DestinationScore y compat con el scoring legacy.
    /// </summary>
    [TestFixture]
    public class AIPathPlannerTests
    {
        private Guid _self;

        [SetUp]
        public void SetUp()
        {
            _self = Guid.NewGuid();
        }

        // ======================================================================
        // Infra
        // ======================================================================

        private sealed class FakeTileQuery : ISpecialTileAIQuery
        {
            public readonly Dictionary<GridCoord, SpecialTileAIView> Tiles =
                new Dictionary<GridCoord, SpecialTileAIView>();

            public bool DangerTelegraph;

            public bool HasAnySpecialTiles => Tiles.Count > 0;
            public bool AnyActiveDangerTelegraph => DangerTelegraph;

            public bool TryGetTileFor(GridCoord coord, Guid entity, Cardinal entryDirection,
                out SpecialTileAIView view)
                => Tiles.TryGetValue(coord, out view);
        }

        private static SpecialTileAIView DamageView(int enter, int stay = 0, int virtualDmg = 0)
            => new SpecialTileAIView(enter, stay, virtualDmg, BeneficialTileKind.None,
                false, false, default, false, 0, false);

        private static SpecialTileAIView TelegraphView(int announcedDamage, bool lethal)
            => new SpecialTileAIView(0, 0, 0, BeneficialTileKind.None,
                false, false, default, true, announcedDamage, lethal);

        private static SpecialTileAIView BenefitView(BeneficialTileKind kind)
            => new SpecialTileAIView(0, 0, 0, kind, false, false, default, false, 0, false);

        private static GridManager MakeGrid(int w, int h)
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(w, h));
            return grid;
        }

        private AIPathRequest Request(GridCoord origin, GridCoord target, int maxSteps,
            int desiredRange, MoveIntent intent = MoveIntent.Approach,
            int hp = 100, int maxHp = 100, AIPersonalityProfile? profile = null)
            => new AIPathRequest
            {
                SelfGuid = _self,
                Origin = origin,
                TargetCoord = target,
                MaxSteps = maxSteps,
                DesiredRange = desiredRange,
                Intent = intent,
                CurrentHp = hp,
                MaxHp = maxHp,
                AttackRange = 1,
                TargetHpPct = -1,
                Personality = profile ?? AIPersonalityProfile.Default,
            };

        private static readonly AIPersonalityProfile Normal = new AIPersonalityProfile(0.20f, 1.0f);
        private static readonly AIPersonalityProfile Aggressive = new AIPersonalityProfile(0.10f, 0.65f);
        private static readonly AIPersonalityProfile Kamikaze =
            new AIPersonalityProfile(0f, 0.25f, skipSurvivalFilter: false, isKamikaze: true);
        private static readonly AIPersonalityProfile KamikazeWithFlag =
            new AIPersonalityProfile(0f, 0.25f, skipSurvivalFilter: true, isKamikaze: true);

        // ======================================================================
        // Fórmulas puras — valores exactos del GDD
        // ======================================================================

        [Test]
        public void HazardPenalty_UsesCeil_ForEachCautionValue()
        {
            // dmg 15, HP proyectado 40 → (15/40)×10 = 3.75 base.
            Assert.AreEqual(6, AIPathPlanner.ComputeHazardPenalty(15, 40, 1.5f), "Support: ceil(5.625)");
            Assert.AreEqual(4, AIPathPlanner.ComputeHazardPenalty(15, 40, 1.0f), "Normal: ceil(3.75)");
            Assert.AreEqual(3, AIPathPlanner.ComputeHazardPenalty(15, 40, 0.65f), "Agresivo: ceil(2.4375)");
            Assert.AreEqual(1, AIPathPlanner.ComputeHazardPenalty(15, 40, 0.25f), "Kamikaze: ceil(0.9375)");
        }

        [Test]
        public void HazardPenalty_ZeroWhenNoDamageSource()
        {
            Assert.AreEqual(0, AIPathPlanner.ComputeHazardPenalty(0, 40, 1.5f));
        }

        [Test]
        public void TileCost_NeverBelowOne()
        {
            Assert.AreEqual(1, AIPathPlanner.ComputeTileCost(0, -1),
                "TerrainModifier negativo no puede llevar el costo a 0.");
            Assert.AreEqual(2, AIPathPlanner.ComputeTileCost(2, -1));
            Assert.AreEqual(1, AIPathPlanner.ComputeTileCost(0, 0));
        }

        [Test]
        public void TerrainModifier_PortalAndIceTables()
        {
            Assert.AreEqual(3, AIPathPlanner.ComputeTerrainModifier(true, true, false), "Portal → peligro: +3");
            Assert.AreEqual(2, AIPathPlanner.ComputeTerrainModifier(false, true, false), "Hielo → peligro: +2");
            Assert.AreEqual(-1, AIPathPlanner.ComputeTerrainModifier(true, false, true), "Acerca: −1");
            Assert.AreEqual(-1, AIPathPlanner.ComputeTerrainModifier(false, false, true));
            Assert.AreEqual(0, AIPathPlanner.ComputeTerrainModifier(true, false, false));
            Assert.AreEqual(3, AIPathPlanner.ComputeTerrainModifier(true, true, true),
                "Peligro gana sobre acercar — no se netean.");
        }

        [Test]
        public void TacticalGain_PrimaryGainTakesMax_NotSum()
        {
            // Puede atacar (4) Y corta distancia (2) Y única en banda (3) → max = 4, no 9.
            int result = AIPathPlanner.ComputeTacticalGainFinal(
                canAttackFromTile: true, isOnlyBandReacher: true, cutsDistance: true,
                selfHealthy: false, targetLow: false,
                staysOnDamage: false, staysOnTelegraph: false, lowHpAfter: false,
                tuning: null);

            Assert.AreEqual(4, result);
        }

        [Test]
        public void TacticalGain_ClampsAndFloor()
        {
            var tuning = ScriptableObject.CreateInstance<AIPathTuningSO>();
            try
            {
                tuning.GainAttackFromTile = 9;
                Assert.AreEqual(8, AIPathPlanner.ComputeTacticalGainFinal(
                        true, false, false, false, false, false, false, false, tuning),
                    "TacticalGain clampa en 8.");

                Assert.AreEqual(2, AIPathPlanner.ComputeTacticalGainFinal(
                        false, false, false, selfHealthy: true, targetLow: true,
                        false, false, false, tuning: null),
                    "ContextBonus solo (tope 2), sin PrimaryGain.");

                Assert.AreEqual(0, AIPathPlanner.ComputeTacticalGainFinal(
                        false, false, true, false, false,
                        staysOnDamage: true, staysOnTelegraph: true, lowHpAfter: true, tuning: null),
                    "TacticalGainFinal nunca baja de 0 (2 − 5).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tuning);
            }
        }

        // ======================================================================
        // Fast path — compat con el scoring legacy
        // ======================================================================

        [Test]
        public void FastPath_NoSpecialTiles_MatchesLegacyApproach()
        {
            var grid = MakeGrid(7, 7);
            var planner = new AIPathPlanner(grid, tiles: null);

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(5, 0),
                maxSteps: 3, desiredRange: 1));

            Assert.IsTrue(plan.HasMove);
            Assert.AreEqual(new GridCoord(3, 0), plan.Destination,
                "Idéntico al loop de AINode_Move: única candidata con err 1.");
            Assert.IsNull(plan.Path, "Fast path: el ejecutor usa el Move clásico.");
        }

        [Test]
        public void FastPath_EmptyTileService_AlsoUsesLegacy()
        {
            var grid = MakeGrid(7, 7);
            var planner = new AIPathPlanner(grid, new FakeTileQuery()); // sin tiles cargados

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(5, 0), 3, 1));

            Assert.IsTrue(plan.HasMove);
            Assert.IsNull(plan.Path);
        }

        [Test]
        public void FastPath_KiteMatchesKeepDistanceScoring()
        {
            var grid = MakeGrid(7, 7);
            var planner = new AIPathPlanner(grid, tiles: null);

            var plan = planner.PlanMove(Request(new GridCoord(2, 0), new GridCoord(3, 0),
                maxSteps: 2, desiredRange: 4, MoveIntent.Kite));

            Assert.IsTrue(plan.HasMove);
            Assert.AreEqual(3, plan.Destination.Manhattan(new GridCoord(3, 0)),
                "Kitea al tile que maximiza min(dist, ideal) — 3 es lo mejor en 2 pasos.");
        }

        [Test]
        public void FastPath_AlreadyOnBand_NoMove()
        {
            var grid = MakeGrid(7, 7);
            var planner = new AIPathPlanner(grid, tiles: null);

            var plan = planner.PlanMove(Request(new GridCoord(2, 0), new GridCoord(3, 0),
                maxSteps: 3, desiredRange: 1));

            Assert.IsFalse(plan.HasMove, "dist == desired: empate con quedarse ⇒ no mover.");
        }

        // ======================================================================
        // Filtro de supervivencia
        // ======================================================================

        [Test]
        public void SurvivalFilter_DiscardsRouteFromFailingTileOnward()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = DamageView(enter: 50);
            var planner = new AIPathPlanner(grid, tiles);

            // 60 − 50 = 10 ≤ 20 (20% de 100): la ruta muere en (3,0), lo de atrás sigue vivo.
            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                maxSteps: 6, desiredRange: 0, hp: 60, profile: Normal));

            Assert.IsTrue(plan.HasMove);
            Assert.AreEqual(new GridCoord(2, 0), plan.Destination);
        }

        [Test]
        public void SurvivalFilter_ExactThreshold_Blocks()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = DamageView(enter: 40);
            var planner = new AIPathPlanner(grid, tiles);

            // 60 − 40 = 20 == 20% de 100: el GDD exige '>' estricto — bloquea.
            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, hp: 60, profile: Normal));

            Assert.AreEqual(new GridCoord(2, 0), plan.Destination);
        }

        [Test]
        public void SurvivalFilter_AggressivePassesWhereNormalBlocks()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = DamageView(enter: 25);
            var planner = new AIPathPlanner(grid, tiles);

            // 40 − 25 = 15: Normal (20%) bloquea, Agresivo (10%) pasa.
            var normalPlan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, hp: 40, profile: Normal));
            var aggressivePlan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, hp: 40, profile: Aggressive));

            Assert.AreEqual(new GridCoord(2, 0), normalPlan.Destination);
            Assert.AreEqual(new GridCoord(6, 0), aggressivePlan.Destination);
        }

        [Test]
        public void SurvivalFilter_KamikazeWithNarrativeFlag_IgnoresItEntirely()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = DamageView(enter: 90);
            var planner = new AIPathPlanner(grid, tiles);

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, hp: 95, profile: KamikazeWithFlag));

            Assert.AreEqual(new GridCoord(6, 0), plan.Destination,
                "El flag narrativo del kamikaze saltea el filtro completo.");
        }

        // ======================================================================
        // Daño virtual y telegraph
        // ======================================================================

        [Test]
        public void VirtualDamage_PenalizesCostButNotSurvival()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = DamageView(enter: 0, stay: 0, virtualDmg: 25);
            var planner = new AIPathPlanner(grid, tiles);

            // hp 40, umbral 20: si el daño virtual entrara al filtro, 40−25 = 15 ≤ 20
            // bloquearía la ruta en (3,0). Como es solo costo (ceil(25/40×10)=7), la ruta
            // completa (5 + 8 = 13) sigue ganándole a frenar en (2,0) (4×3 + 2 = 14).
            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, hp: 40, profile: Normal));

            Assert.AreEqual(new GridCoord(6, 0), plan.Destination,
                "El stun virtual encarece la ruta pero no reduce HP proyectado ni filtra.");
        }

        [Test]
        public void VirtualDamage_MakesPlannerPreferDetour()
        {
            var grid = MakeGrid(5, 5);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(2, 2)] = DamageView(0, 0, virtualDmg: 25);
            var planner = new AIPathPlanner(grid, tiles);

            var plan = planner.PlanMove(Request(new GridCoord(0, 2), new GridCoord(4, 2),
                maxSteps: 6, desiredRange: 0, hp: 100, profile: Normal));

            Assert.AreEqual(new GridCoord(4, 2), plan.Destination);
            Assert.IsNotNull(plan.Path);
            CollectionAssert.DoesNotContain(plan.Path.ToList(), new GridCoord(2, 2),
                "El charco cuesta 4 (ceil(2.5)+1): el desvío de 6 pasos gana al directo de 7.");
        }

        [Test]
        public void LethalTelegraph_BlocksRoute_ExceptForKamikaze()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = TelegraphView(0, lethal: true);
            var planner = new AIPathPlanner(grid, tiles);

            var normalPlan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, profile: Normal));
            var kamikazePlan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, profile: Kamikaze));

            Assert.AreEqual(new GridCoord(2, 0), normalPlan.Destination,
                "Un Telegraph letal bloquea la ruta para IA no-kamikaze.");
            Assert.AreEqual(new GridCoord(6, 0), kamikazePlan.Destination,
                "Kamikaze lo cruza sin necesitar el flag narrativo.");
        }

        [Test]
        public void Telegraph_ReusesHazardPenaltyWithAnnouncedDamage()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            // Daño anunciado 50: 100−50 = 50 > 20 pasa el filtro, pero el costo sube.
            tiles.Tiles[new GridCoord(3, 0)] = TelegraphView(50, lethal: false);
            var planner = new AIPathPlanner(grid, tiles);

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                6, 0, hp: 100, profile: Normal));

            // Cruza igual (corredor único, err manda), pero el HP proyectado del otro lado
            // refleja el daño anunciado — quedó descontado en la ruta.
            Assert.AreEqual(new GridCoord(6, 0), plan.Destination);
        }

        // ======================================================================
        // DestinationScore — beneficios
        // ======================================================================

        [Test]
        public void Healing_ChosenOnlyWhenHpLowAndDetourSmall()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = BenefitView(BeneficialTileKind.Healing);
            var planner = new AIPathPlanner(grid, tiles);

            var lowHpPlan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                maxSteps: 4, desiredRange: 2, hp: 20, profile: Normal));
            var healthyPlan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                maxSteps: 4, desiredRange: 2, hp: 100, profile: Normal));

            Assert.AreEqual(new GridCoord(3, 0), lowHpPlan.Destination,
                "Con 20% de vida, el BenefitValue de curarse (4) paga el desvío de banda.");
            Assert.AreEqual(new GridCoord(4, 0), healthyPlan.Destination,
                "Con vida llena la condición (≤60%) no se cumple: BenefitValue = 0.");
        }

        [Test]
        public void SafeZone_ValuedOnlyWhileDangerTelegraphActive()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = BenefitView(BeneficialTileKind.SafeZone);
            var planner = new AIPathPlanner(grid, tiles);

            tiles.DangerTelegraph = true;
            var withDanger = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                4, 2, profile: Normal));
            tiles.DangerTelegraph = false;
            var withoutDanger = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                4, 2, profile: Normal));

            Assert.AreEqual(new GridCoord(3, 0), withDanger.Destination,
                "Con Telegraph peligroso activo, la zona vale 3 y paga el desvío.");
            Assert.AreEqual(new GridCoord(4, 0), withoutDanger.Destination);
        }

        [Test]
        public void Impulse_IsInert_NeverValued()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(3, 0)] = BenefitView(BeneficialTileKind.Impulse);
            var planner = new AIPathPlanner(grid, tiles);

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                4, 2, profile: Normal));

            Assert.AreEqual(new GridCoord(4, 0), plan.Destination,
                "Impulso está inerte (sin tirada real): la IA no lo valora.");
        }

        // ======================================================================
        // Pisar peligro a propósito — regla 5 + empate → opción segura
        // ======================================================================

        [Test]
        public void HazardDestination_TieBetweenGainAndPenalty_ChoosesSafeOption()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(4, 0)] = DamageView(enter: 0, stay: 12);
            var planner = new AIPathPlanner(grid, tiles);

            // (4,0) es la única en banda (err 0): gain = min(8, 3+1) − 2 = 2;
            // stayPenalty = ceil(12/100×10×1) = 2 → empate exacto ⇒ gana la opción segura.
            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(6, 0),
                maxSteps: 4, desiredRange: 2, hp: 100, profile: Normal));

            Assert.AreEqual(new GridCoord(3, 0), plan.Destination);
        }

        [Test]
        public void HazardDestination_AttackOpportunityJustifiesTheRisk()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(4, 0)] = DamageView(enter: 0, stay: 12);
            var planner = new AIPathPlanner(grid, tiles);

            // Target adyacente a la casilla: canAttackFromTile → gain = min(8, 4+1) − 2 = 3 > 2.
            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(5, 0),
                maxSteps: 4, desiredRange: 1, hp: 100, profile: Normal));

            Assert.AreEqual(new GridCoord(4, 0), plan.Destination,
                "Poder atacar desde ahí este turno justifica pisar la casilla dañina.");
        }

        [Test]
        public void HazardDestination_SurvivalOnStayDamageAlsoGates()
        {
            var grid = MakeGrid(7, 1);
            var tiles = new FakeTileQuery();
            tiles.Tiles[new GridCoord(4, 0)] = DamageView(enter: 0, stay: 90);
            var planner = new AIPathPlanner(grid, tiles);

            var plan = planner.PlanMove(Request(new GridCoord(0, 0), new GridCoord(5, 0),
                4, 1, hp: 100, profile: Normal));

            Assert.AreEqual(new GridCoord(3, 0), plan.Destination,
                "100 − 90 = 10 ≤ 20: quedarse ahí viola el filtro de supervivencia.");
        }
    }
}
