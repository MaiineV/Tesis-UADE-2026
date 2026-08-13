using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Bosses.Tahur;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Unit tests del pozo del Tahúr: acumulación de fichas, mapa fichas → castigo
    /// (26/32/38/42/45 con techo en 45), el cobro que reemplaza el ataque, el rastrillo y el
    /// canto. Sala 11×7 real, servicios reales salvo el pipeline de daño y el overlay.
    /// </summary>
    [TestFixture]
    public class TahurWagerTests
    {
        private const int RoomWidth = 11;
        private const int RoomHeight = 7;

        private static readonly int[] LadderPriorities = { 10, 14, 20, 26, 34, 50 };

        private GridManager _grid;
        private TahurWagerService _wager;
        private ThreatenedAreaService _threat;
        private ContractModifierService _mods;
        private SpyDamagePipeline _pipeline;
        private FakePlayerService _playerService;
        private ClassHeroSO _hero;
        private readonly List<LadderCombo> _combos = new List<LadderCombo>();

        private Guid _boss;
        private Guid _player;

        private AINode_TahurSettleWager _settle;
        private AINode_TahurCallHand _call;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomWidth, RoomHeight));

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, new GridCoord(5, 3));
            _grid.Register(_player, new GridCoord(8, 3));

            _hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _hero.Sheet = new ContractSheet { Combos = new List<BaseComboSO>() };
            for (int rank = 1; rank <= LadderPriorities.Length; rank++)
            {
                var combo = ScriptableObject.CreateInstance<LadderCombo>();
                combo.Configure(IdOf(rank), LadderPriorities[rank - 1]);
                _combos.Add(combo);
                _hero.Sheet.Combos.Add(combo);
            }

            _playerService = new FakePlayerService(_player, _hero);
            _pipeline = new SpyDamagePipeline();

            ServiceLocator.AddService<IGridManager>(_grid);
            ServiceLocator.AddService<IPlayerService>(_playerService);
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);
            // Stub del overlay: el real instancia quads en la escena y estos tests no miran píxeles.
            ServiceLocator.AddService<IThreatOverlayService>(new SpyThreatOverlay());

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _mods = new ContractModifierService();
            _mods.Register();
            _mods.ConfigureForTests(() => _hero.Sheet);

            _wager = new TahurWagerService();
            _wager.Register();

            _settle = TahurAssetBuilder.BuildSettleWager();
            _call = TahurAssetBuilder.BuildCallHand();
        }

        [TearDown]
        public void TearDown()
        {
            _wager?.Dispose();
            _mods?.Dispose();
            _threat?.Dispose();

            foreach (var combo in _combos) if (combo != null) Object.DestroyImmediate(combo);
            _combos.Clear();
            if (_hero != null) Object.DestroyImmediate(_hero);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // =================================================================
        // El mapa fichas → castigo
        // =================================================================

        [TestCase(1, 26)]
        [TestCase(2, 32)]
        [TestCase(3, 38)]
        [TestCase(4, 42)]
        [TestCase(5, 45)]
        [TestCase(6, 45)]
        [TestCase(40, 45)]
        public void PunishmentDamage_MapsChipsToTheCalibratedTable(int chips, int expected)
        {
            Assert.AreEqual(expected, _settle.PunishmentDamageForChips(chips),
                "Tabla del pozo v2 (12/08): 26/32/38/42/45, con 45 como techo por golpe del piso 3.");
        }

        [Test]
        public void PunishmentDamage_NeverExceedsFortyFive_EvenWithAFatterTable()
        {
            // Un balanceo futuro que suba la tabla no puede pasar el techo del piso por accidente.
            _settle.PotDamageTable = new List<int> { 26, 32, 38, 42, 90 };

            Assert.AreEqual(45, _settle.PunishmentDamageForChips(5));
        }

        // =================================================================
        // Los cuatro resultados
        // =================================================================

        [Test]
        public void Miss_AddsOneChip_AndMarksTwentySixOnThePlayersColumn()
        {
            Call(3);
            PlayHand(2);

            Assert.AreEqual(AIResult.Succeeded, _settle.Tick(NewContext()));

            Assert.AreEqual(1, _wager.Chips, "Cada fallo suma una ficha.");
            Assert.AreEqual(TahurSettleOutcome.Miss, _wager.LastOutcome);
            Assert.IsTrue(_wager.MarkedPunishmentThisTurn);

            Assert.IsTrue(_threat.TryConsume(_boss, out var area), "El fallo tiene que marcar Castigo.");
            Assert.AreEqual(26, area.Damage, "Una ficha ⇒ 26.");
            var xs = area.Tiles.Select(t => t.X).Distinct().ToList();
            Assert.AreEqual(1, xs.Count, "Faltar un escalón marca Column 1: la columna del jugador.");
            Assert.AreEqual(8, xs[0], "El Castigo se centra en donde estaba el jugador.");
            Assert.AreEqual(RoomHeight, area.Tiles.Count);
        }

        [Test]
        public void Greed_AddsTwoChips_AndUsesTheWidestShape()
        {
            Call(2);
            PlayHand(5);

            _settle.Tick(NewContext());

            Assert.AreEqual(2, _wager.Chips, "La codicia mueve el pozo dos fichas.");
            Assert.AreEqual(TahurSettleOutcome.Greed, _wager.LastOutcome);

            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            Assert.AreEqual(32, area.Damage, "Dos fichas ⇒ 32.");
            Assert.Greater(area.Tiles.Select(t => t.X).Distinct().Count(), 1,
                "La codicia usa Scattered 6×2, no una franja.");
        }

        [Test]
        public void NoHandPlayed_SettlesAsTheBiggestShortfall()
        {
            // Armar nada es el fallo más grande: distancia = el escalón cantado entero.
            Call(4);

            _settle.Tick(NewContext());

            Assert.AreEqual(1, _wager.Chips);
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            Assert.AreEqual(26, area.Damage);
            Assert.Greater(area.Tiles.Select(t => t.X).Distinct().Count(), 1,
                "Faltar 4 escalones marca Scattered 4×2, no una franja.");
        }

        [Test]
        public void HandPlayedByAnotherEntity_DoesNotCount()
        {
            Call(3);
            RaiseComboPlayed(Guid.NewGuid(), IdOf(3));

            _settle.Tick(NewContext());

            Assert.AreEqual(TahurSettleOutcome.Miss, _wager.LastOutcome,
                "Solo la mano del jugador liquida el canto.");
        }

        [Test]
        public void ExactInsideTable_PaysThePotToTheBoss_AndReplacesTheAttack()
        {
            PutTableUnderPlayer();
            _wager.SetChips(3);
            Call(4);
            PlayHand(4);

            _settle.Tick(NewContext());

            Assert.AreEqual(TahurSettleOutcome.Exact, _wager.LastOutcome);
            Assert.IsFalse(_wager.MarkedPunishmentThisTurn,
                "Cobrar reemplaza su ataque: la ronda del cobro no marca Castigo.");
            Assert.IsFalse(_threat.HasPending(_boss));

            Assert.AreEqual(1, _pipeline.Contexts.Count, "El cobro resuelve un único daño.");
            var payout = _pipeline.Contexts[0];
            Assert.AreEqual(36, payout.BaseDamage, "12 × 3 fichas.");
            Assert.AreEqual(_boss, payout.TargetId, "El pozo le pega a él, no al jugador.");
            Assert.AreEqual(_player, payout.SourceId);

            Assert.AreEqual(0, _wager.Chips, "En fase 1 cobrar vacía el pozo.");
        }

        [Test]
        public void ExactOutsideTable_CollectsNothing_ButKeepsTheRoundClean()
        {
            PutTableUnderPlayer();
            _grid.Move(_player, new GridCoord(9, 6)); // fuera del 3×3 del jefe
            _wager.SetChips(3);
            Call(4);
            PlayHand(4);

            _settle.Tick(NewContext());

            Assert.IsEmpty(_pipeline.Contexts, "Cobrar exige estar en La Mesa.");
            Assert.AreEqual(3, _wager.Chips, "Sin cobro el pozo queda como estaba.");
            Assert.IsFalse(_wager.MarkedPunishmentThisTurn, "Armar exacto nunca marca Castigo.");
            Assert.AreEqual(TahurSettleOutcome.Exact, _wager.LastOutcome);
        }

        [Test]
        public void NoCallYet_SettlesNothing()
        {
            _settle.Tick(NewContext());

            Assert.AreEqual(TahurSettleOutcome.None, _wager.LastOutcome,
                "En la primera ronda todavía no cantó: no hay nada que liquidar.");
            Assert.AreEqual(0, _wager.Chips);
            Assert.IsFalse(_threat.HasPending(_boss));
        }

        [Test]
        public void Chips_ClampAtTheBank()
        {
            for (int round = 0; round < 8; round++)
            {
                Call(6);
                PlayHand(1);
                _settle.Tick(NewContext());
                _threat.Clear(_boss);
            }

            Assert.AreEqual(5, _wager.Chips, "La banca: el pozo tope es 5 fichas.");
            Assert.AreEqual(45, _settle.PunishmentDamageForChips(_wager.Chips));
        }

        // =================================================================
        // La fase 2: el volteo, el rastrillo y la gracia
        // =================================================================

        [Test]
        public void FlipCard_InvertsTheCall_AndTurnsOnTheRake()
        {
            TahurAssetBuilder.BuildFlipCard().Tick(NewContext());

            Assert.IsTrue(_wager.CallInverted, "El cartel pasa de PIDE a LEE.");
            Assert.AreEqual(1, _wager.RakeChipsPerRound);
            Assert.AreEqual(1, _wager.ChipsFloor, "Cobrar deja el pozo en 1, nunca en 0.");
            Assert.IsTrue(_wager.GraceOnNextSettle);
        }

        [Test]
        public void FirstSettleAfterTheFlip_IsGrace_ButTheRakeStillRuns()
        {
            TahurAssetBuilder.BuildFlipCard().Tick(NewContext());
            Call(3);
            PlayHand(1); // un fallo que, sin gracia, marcaría Castigo

            _settle.Tick(NewContext());

            Assert.AreEqual(TahurSettleOutcome.Grace, _wager.LastOutcome);
            Assert.IsFalse(_threat.HasPending(_boss),
                "El canto pendiente se armó con las reglas viejas: la primera liquidación no castiga.");
            Assert.AreEqual(1, _wager.Chips, "El rastrillo corre solo, incluso en la ronda de gracia.");
            Assert.IsFalse(_wager.GraceOnNextSettle, "La gracia es una sola.");
        }

        [Test]
        public void Inverted_CollectingLeavesThePotAtOne()
        {
            TahurAssetBuilder.BuildFlipCard().Tick(NewContext());
            _settle.Tick(NewContext()); // consume la gracia; el rastrillo deja el pozo en 1

            PutTableUnderPlayer();
            Call(4);      // LEE: el escalón a armar es el 3
            PlayHand(3);

            _settle.Tick(NewContext());

            Assert.AreEqual(TahurSettleOutcome.Exact, _wager.LastOutcome);
            Assert.AreEqual(1, _pipeline.Contexts.Count);
            Assert.AreEqual(24, _pipeline.Contexts[0].BaseDamage,
                "El rastrillo subió el pozo a 2 antes de liquidar ⇒ 12 × 2.");
            Assert.AreEqual(1, _wager.Chips, "Con el rastrillo encendido el pozo nunca vuelve a 0.");
        }

        [Test]
        public void Inverted_HittingTheCall_SettlesAsTheWorstResult()
        {
            TahurAssetBuilder.BuildFlipCard().Tick(NewContext());
            _settle.Tick(NewContext()); // gracia + rastrillo ⇒ 1 ficha

            Call(4);
            PlayHand(4); // te leyó

            _settle.Tick(NewContext());

            Assert.AreEqual(TahurSettleOutcome.Read, _wager.LastOutcome);
            Assert.AreEqual(4, _wager.Chips, "1 previa + 1 del rastrillo + 2 por dejarse leer.");
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            Assert.AreEqual(42, area.Damage, "Cuatro fichas ⇒ 42.");
            Assert.Greater(area.Tiles.Select(t => t.X).Distinct().Count(), 1,
                "Te leyó ⇒ la forma más ancha, la de la codicia.");
        }

        // =================================================================
        // El poke
        // =================================================================

        [Test]
        public void Poke_FailsOnAPunishmentRound()
        {
            _grid.Move(_player, new GridCoord(6, 3)); // pegado al jefe
            Call(3);
            PlayHand(1);
            _settle.Tick(NewContext());

            var poke = TahurAssetBuilder.BuildPoke();
            Assert.AreEqual(AIResult.Failed, poke.Tick(NewContext()),
                "El poke y el Castigo nunca resuelven la misma ronda: 12 + 45 rompe el techo de 45.");
            Assert.IsEmpty(_pipeline.Contexts);
        }

        [Test]
        public void Poke_HitsForTwelveOnACleanRound()
        {
            _grid.Move(_player, new GridCoord(6, 3));
            PutTableUnderPlayer();
            Call(4);
            PlayHand(4);
            _settle.Tick(NewContext());
            _pipeline.Contexts.Clear(); // el cobro del pozo ya resolvió su daño

            var poke = TahurAssetBuilder.BuildPoke();
            Assert.AreEqual(AIResult.Succeeded, poke.Tick(NewContext()));

            Assert.AreEqual(1, _pipeline.Contexts.Count);
            Assert.AreEqual(12, _pipeline.Contexts[0].BaseDamage, "Poke de la ficha v2: 12.");
            Assert.AreEqual(_player, _pipeline.Contexts[0].TargetId);
        }

        [Test]
        public void Poke_FailsOutOfMeleeRange()
        {
            PutTableUnderPlayer();
            _grid.Move(_player, new GridCoord(9, 6));
            Call(4);
            PlayHand(4);
            _settle.Tick(NewContext());

            Assert.AreEqual(AIResult.Failed, TahurAssetBuilder.BuildPoke().Tick(NewContext()));
        }

        // =================================================================
        // La Mesa
        // =================================================================

        [Test]
        public void MarkTable_PaintsTheThreeByThreeAroundItself_WithoutTouchingThePunishment()
        {
            Call(3);
            PlayHand(1);
            _settle.Tick(NewContext());          // deja un Castigo pendiente
            Assert.IsTrue(_threat.HasPending(_boss));

            TahurAssetBuilder.BuildMarkTable().Tick(NewContext());

            Assert.AreEqual(9, _wager.TableTiles.Count, "3×3 alrededor del jefe.");
            Assert.IsTrue(_wager.IsOnTable(new GridCoord(4, 2)));
            Assert.IsTrue(_wager.IsOnTable(new GridCoord(6, 4)));
            Assert.IsFalse(_wager.IsOnTable(new GridCoord(8, 3)));
            Assert.IsTrue(_threat.HasPending(_boss),
                "La Mesa no puede pisar el Castigo: van en canales distintos.");
        }

        // =================================================================
        // El canto
        // =================================================================

        [Test]
        public void Call_ForbidsTheCalledHand_AndDoublesEverythingAbove()
        {
            Assert.AreEqual(AIResult.Succeeded, _call.Tick(NewContext()));

            int called = _wager.CalledRank;
            Assert.That(called, Is.InRange(1, 6));
            Assert.AreEqual(IdOf(called), _wager.CalledComboId);
            Assert.IsTrue(_mods.IsForbidden(IdOf(called)),
                "Armar el canto hace 0 (R03): cobrar cuesta el ataque, no la vida.");

            for (int rank = called + 1; rank <= LadderPriorities.Length; rank++)
            {
                Assert.AreEqual(20, _mods.GetEffectiveBaseDamage(IdOf(rank), 10),
                    $"El escalón {rank} está por encima del canto: la codicia paga ×2 (R01).");
            }
            for (int rank = 1; rank < called; rank++)
            {
                Assert.AreEqual(10, _mods.GetEffectiveBaseDamage(IdOf(rank), 10),
                    $"El escalón {rank} está por debajo del canto: sin multiplicador.");
            }
        }

        [Test]
        public void Call_ClearsThePreviousRoundsRules()
        {
            _call.Tick(NewContext());
            int first = _wager.CalledRank;

            _call.Tick(NewContext());

            if (_wager.CalledRank != first)
            {
                Assert.IsFalse(_mods.IsForbidden(IdOf(first)),
                    "La regla de la ronda anterior se cancela: acumuladas dejan el Contrato ilegible.");
            }
        }

        [Test]
        public void Call_NeverCallsTwoHighStepsInARow()
        {
            for (int round = 0; round < 24; round++)
            {
                int previous = _wager.CalledRank;
                _call.Tick(NewContext());

                if (previous >= 5)
                {
                    Assert.Less(_wager.CalledRank, 5,
                        "La válvula: nunca dos cantos ≥5 seguidos.");
                }
            }
        }

        [Test]
        public void Call_RotatesWithoutRepeatingUntilTheSetIsExhausted()
        {
            // Sin la válvula la rotación se puede medir sola: 6 cantos = los 6 escalones.
            _call.AvoidConsecutiveHighCalls = false;

            var seen = new List<int>();
            for (int round = 0; round < LadderPriorities.Length; round++)
            {
                _call.Tick(NewContext());
                seen.Add(_wager.CalledRank);
            }

            CollectionAssert.AreEquivalent(Enumerable.Range(1, LadderPriorities.Length), seen,
                "Rotativo con memoria: no repite hasta agotar el conjunto.");
        }

        [Test]
        public void Call_NeverPicksTheFirstStepOnceInverted()
        {
            TahurAssetBuilder.BuildFlipCard().Tick(NewContext());

            for (int round = 0; round < 24; round++)
            {
                _call.Tick(NewContext());
                Assert.GreaterOrEqual(_wager.CalledRank, 2,
                    "LEE cobra el escalón de abajo: cantar el 1 no dejaría nada que cobrar.");
                Assert.AreEqual(_wager.CalledRank - 1, _wager.TargetRank);
            }
        }

        // =================================================================
        // Helpers
        // =================================================================

        private static string IdOf(int rank) => $"combo.step_{rank}";

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = 290,
            Grid = _grid,
            DamagePipeline = _pipeline,
            PlayerService = _playerService,
            Rng = new System.Random(20260812),
        };

        private void Call(int rank) => _wager.SetCall(rank, IdOf(rank));

        private void PlayHand(int rank) => RaiseComboPlayed(_player, IdOf(rank));

        private static void RaiseComboPlayed(Guid source, string comboId)
            => TypedEvent<ComboPlayedPayload>.Raise(new ComboPlayedPayload
            {
                SourceGuid = source,
                ComboId = comboId,
            });

        /// <summary>Pone La Mesa donde está el jugador (que arranca pegado al jefe).</summary>
        private void PutTableUnderPlayer()
        {
            _grid.Move(_player, new GridCoord(6, 3));
            TahurAssetBuilder.BuildMarkTable().Tick(NewContext());
        }

        // -----------------------------------------------------------------
        // Doubles
        // -----------------------------------------------------------------

        /// <summary>Combo de catálogo mínimo: solo aporta id y Priority para armar la escalera.</summary>
        private sealed class LadderCombo : BaseComboSO
        {
            public void Configure(string comboId, int priority)
            {
                _comboId = comboId;
                _displayName = comboId;
                _baseDamage = priority; // Priority default = BaseDamage
            }

            public override bool Matches(int[] finalDice) => false;
        }

        private sealed class FakePlayerService : IPlayerService
        {
            private readonly ClassHeroSO _hero;

            public FakePlayerService(Guid playerGuid, ClassHeroSO hero)
            {
                PlayerGuid = playerGuid;
                _hero = hero;
            }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => _hero;
            public Rollgeon.Dice.DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(Rollgeon.Dice.DiceBagSO bag) { }
            public void ClearPlayer() { }
#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Contexts = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Contexts.Add(ctx);
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }
        }

        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint) { }
            public void Clear(Guid sourceGuid) { }
            public void ClearAll() { }
        }
    }
}
