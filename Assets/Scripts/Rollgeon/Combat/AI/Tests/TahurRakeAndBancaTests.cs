using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Tahur;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Las dos líneas que le sacan al Tahúr la salida de no jugar: el rastrillo corriendo desde la
    /// fase 1 (el pozo sube solo, así que el Castigo escala 26 → 45 sin que el jugador haga nada) y
    /// La Banca (con el pozo lleno, 45 en toda la sala menos La Mesa). Sala real de la ficha —11×7
    /// con las cuatro columnas— y servicios reales salvo el pipeline de daño y el overlay.
    /// </summary>
    [TestFixture]
    public class TahurRakeAndBancaTests
    {
        private const int RoomWidth = 11;
        private const int RoomHeight = 7;

        /// <summary>Las cuatro columnas de la ficha: encarecen el eje vertical y no son casillas.</summary>
        private static readonly GridCoord[] Columns =
        {
            new GridCoord(3, 1), new GridCoord(7, 1), new GridCoord(3, 5), new GridCoord(7, 5),
        };

        private const int WalkableTiles = RoomWidth * RoomHeight - 4;
        private const int TableTiles = 9;

        private static readonly GridCoord BossStart = new GridCoord(5, 3);
        private static readonly GridCoord OnTheTable = new GridCoord(5, 2);
        private static readonly GridCoord FarFromTheTable = new GridCoord(10, 6);

        private static readonly int[] LadderPriorities = { 10, 14, 20, 26, 34, 50 };

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private TahurWagerService _wager;
        private SpyDamagePipeline _pipeline;
        private FakePlayerService _playerService;
        private ClassHeroSO _hero;
        private readonly List<LadderCombo> _combos = new List<LadderCombo>();

        private Guid _boss;
        private Guid _player;

        private AINode_TahurSettleWager _settle;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(TahurRoom());

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossStart);
            _grid.Register(_player, FarFromTheTable);

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
            // Stub del overlay: el real instancia quads en la escena y acá no se miran píxeles.
            ServiceLocator.AddService<IThreatOverlayService>(new SpyThreatOverlay());

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _wager = new TahurWagerService();
            _wager.Register();

            _settle = new AINode_TahurSettleWager();
        }

        [TearDown]
        public void TearDown()
        {
            _wager?.Dispose();
            _threat?.Dispose();

            foreach (var combo in _combos) if (combo != null) UnityEngine.Object.DestroyImmediate(combo);
            _combos.Clear();
            if (_hero != null) UnityEngine.Object.DestroyImmediate(_hero);

            TypedEvent<ComboPlayedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // =================================================================
        // El rastrillo, desde la fase 1
        // =================================================================

        [Test]
        public void Rake_StartsThePotAtOne_OnTheOpeningRound()
        {
            // Arrange — primera ronda del combate: todavía no cantó, no hay nada que liquidar.
            // Act
            Assert.AreEqual(AIResult.Succeeded, Settle());

            // Assert
            Assert.AreEqual(1, _wager.Chips, "La ficha: la carta sale y «el pozo arranca en 1».");
            Assert.AreEqual(TahurSettleOutcome.None, _wager.LastOutcome);
        }

        [Test]
        public void Rake_MovesThePotEveryRound_EvenWhenThePlayerNeverFails()
        {
            // Arrange — la postura que el rastrillo viene a romper: armar el canto exacto desde
            // lejos. Contiene el Castigo (0 fichas por resultado) y renuncia al pozo.
            for (int round = 1; round <= 4; round++)
            {
                Call(3);
                PlayHand(3);

                // Act
                Settle();

                // Assert
                Assert.AreEqual(TahurSettleOutcome.Exact, _wager.LastOutcome,
                    "El jugador no falló ninguna ronda: el pozo se movió solo.");
                Assert.AreEqual(round, _wager.Chips, $"Ronda {round}: el rastrillo empuja una ficha.");
                Assert.IsFalse(_threat.HasPending(_boss), "Armar exacto nunca marca Castigo.");
            }
        }

        [Test]
        public void Rake_WalksThePunishmentFromTwentySixToFortyFive_OnItsOwn()
        {
            // Arrange — el jugador contiene todas las rondas; el Castigo escala igual.
            var expected = new[] { 26, 32, 38, 42, 45 };

            for (int round = 0; round < expected.Length; round++)
            {
                Call(3);
                PlayHand(3);

                // Act
                Settle();

                // Assert
                Assert.AreEqual(expected[round], _settle.PunishmentDamageForChips(_wager.Chips),
                    $"Ronda {round + 1}: sin rastrillo el Castigo se quedaba clavado en 26.");
            }

            // Assert — y de ahí no pasa: el pozo tiene techo y el daño también.
            for (int extra = 0; extra < 3; extra++) { Call(3); PlayHand(3); Settle(); }
            Assert.AreEqual(5, _wager.Chips, "La banca: el pozo tope es 5 fichas.");
            Assert.AreEqual(45, _settle.PunishmentDamageForChips(_wager.Chips));
        }

        [Test]
        public void Rake_DoesNotStompThePhaseTwoRhythm_OnceTheCardIsFlipped()
        {
            // Arrange — la liquidación escribe el rastrillo de fase 1 en cada tick; después del
            // volteo el ritmo es del volteo, o subirlo en fase 2 sería imposible.
            new AINode_TahurFlipCard
            {
                RakeChipsPerRound = 2,
                ChipsFloorAfterFlip = 1,
                GraceOnFirstSettle = false,
            }.Tick(Context());

            // Act
            Settle();

            // Assert
            Assert.AreEqual(2, _wager.RakeChipsPerRound, "El volteo manda a partir de la fase 2.");
            Assert.AreEqual(2, _wager.Chips);
        }

        // =================================================================
        // La Banca
        // =================================================================

        [Test]
        public void Banca_HoldsFire_WhileThePotIsNotFull()
        {
            // Arrange
            _wager.SetChips(4);
            MarkTable();

            // Act + Assert — Failed es el caso normal: por eso va en Selector[Banca, Wait].
            Assert.AreEqual(AIResult.Failed, Banca());
            Assert.IsFalse(_threat.HasPending(_boss));
        }

        [Test]
        public void Banca_WithAFullPot_ThreatensTheWholeRoomButTheTable()
        {
            // Arrange
            _wager.SetChips(5);
            MarkTable();

            // Act
            Assert.AreEqual(AIResult.Succeeded, Banca());

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            Assert.AreEqual(WalkableTiles - TableTiles, area.Tiles.Count,
                "La sala caminable entera menos el 3×3 de La Mesa.");
            Assert.AreEqual(45, area.Damage, "El techo de daño por golpe del piso 3.");

            foreach (var tile in _wager.TableTiles)
                Assert.IsFalse(area.Contains(tile), $"La Mesa {tile} no puede estar amenazada.");
            Assert.IsFalse(area.Contains(BossStart), "La casilla del jefe es parte del hueco.");
            foreach (var column in Columns)
                Assert.IsFalse(area.Contains(column), $"La columna {column} no es una casilla.");
        }

        [Test]
        public void Banca_KeepsItsOwnHole_EvenIfTheTableWasNeverPainted()
        {
            // Arrange — sin MarkTable el hueco sale de la forma, no de las casillas guardadas.
            _wager.SetChips(5);

            // Act
            Assert.AreEqual(AIResult.Succeeded, Banca());

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            Assert.AreEqual(WalkableTiles - TableTiles, area.Tiles.Count);
        }

        [Test]
        public void Banca_NeverPaysMoreThanTheFloorCeiling()
        {
            // Arrange — un balanceo futuro no puede pasar el techo del piso por accidente.
            _wager.SetChips(5);
            MarkTable();

            // Act
            new AINode_TahurMarkBanca { Damage = 90, DamageCeiling = 45 }.Tick(Context());

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            Assert.AreEqual(45, area.Damage);
        }

        [Test]
        public void Banca_GivesOneRoundOfWarning_BeforeItCollects()
        {
            // Arrange
            _wager.SetChips(5);
            MarkTable();
            Banca();

            // Assert — la ronda del aviso no pega: el jugador todavía tiene su turno para caminar.
            Assert.IsEmpty(_pipeline.Resolved, "La Banca se marca, no se cobra en el acto.");

            // Act — el jugador se queda afuera y la cobra al abrir el turno siguiente.
            Assert.AreEqual(AIResult.Succeeded, Execute());

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(45, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(_boss, _pipeline.Resolved[0].SourceId);
        }

        [Test]
        public void Banca_LeavesWhoeverIsStandingOnTheTableUntouched()
        {
            // Arrange — la única salida es estar cobrando, o sea estar en su cara.
            MovePlayer(OnTheTable);
            _wager.SetChips(5);
            MarkTable();
            Banca();

            // Act
            Execute();

            // Assert
            Assert.IsEmpty(_pipeline.Resolved, "La Mesa es daño 0 también cuando barre la sala.");
        }

        [Test]
        public void Banca_NeverThreatensATileThePokeCanReach()
        {
            // Arrange — el poke pega 12 y La Banca 45: si pudieran caer sobre la misma casilla, el
            // jugador cobraría 57 y el techo de 45 por golpe del piso 3 sería mentira.
            _wager.SetChips(5);
            MarkTable();
            Banca();

            // Act
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));

            // Assert
            foreach (var tile in MeleeReach(BossStart))
            {
                Assert.IsFalse(area.Contains(tile),
                    $"{tile} está al alcance del poke Y amenazada por La Banca: 12 + 45.");
            }
        }

        [Test]
        public void Banca_CountsTheRoundAsMarked_SoAReorderedPokeStaysOff()
        {
            // Arrange — hoy el poke resuelve antes que La Banca, pero el gate no puede depender del
            // orden del árbol: un rewire que lo mueva no puede convertirlo en daño extra.
            MovePlayer(OnTheTable);
            _wager.SetChips(5);
            MarkTable();

            // Act
            Banca();

            // Assert
            Assert.IsTrue(_wager.MarkedPunishmentThisTurn);
            Assert.AreEqual(AIResult.Failed, new AINode_TahurPoke().Tick(Context()));
            Assert.IsEmpty(_pipeline.Resolved);
        }

        [Test]
        public void Banca_ReplacesThePunishmentOfTheSameRound()
        {
            // Arrange — con el pozo lleno el Castigo ya vale 45: detonar los dos rompería el techo.
            _wager.SetChips(4); // el rastrillo lo deja en 5 al liquidar
            Call(6);
            PlayHand(1);
            Settle();
            Assert.IsTrue(_threat.HasPending(_boss), "Fixture rota: el fallo tenía que marcar Castigo.");

            // Act
            MarkTable();
            Assert.AreEqual(AIResult.Succeeded, Banca());

            // Assert
            Assert.AreEqual(WalkableTiles - TableTiles, _threat.GetPendingTiles(_boss).Count,
                "Las dos marcas van al guid del jefe: la última manda, y es La Banca.");
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            Assert.AreEqual(45, area.Damage);
            Assert.IsFalse(_threat.HasPending(_boss),
                "Un solo área pendiente: el Castigo de 45 y La Banca de 45 no pueden caer juntos.");
        }

        [Test]
        public void Banca_StandsDown_WhenThePlayerCollectsThePot()
        {
            // Arrange — el bucle completo de la ficha: el pozo se llena solo, La Banca obliga a
            // pisar La Mesa, y cobrar desde La Mesa es lo único que la apaga.
            MovePlayer(OnTheTable);
            _wager.SetChips(4); // el rastrillo lo deja en 5 al liquidar
            MarkTable();
            Call(3);
            PlayHand(3);

            // Act
            Settle();

            // Assert
            Assert.AreEqual(TahurSettleOutcome.Exact, _wager.LastOutcome);
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(60, _pipeline.Resolved[0].BaseDamage, "12 × 5 fichas, contra el jefe.");
            Assert.AreEqual(_boss, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(0, _wager.Chips, "Cobrar vacía el pozo hasta su piso.");

            Assert.AreEqual(AIResult.Failed, Banca(), "Con el pozo vacío la banca no barre nada.");
            Assert.IsFalse(_threat.HasPending(_boss));
        }

        [Test]
        public void Banca_HoleFollowsTheTable_WhenTheTableIsWiderThanItsRadius()
        {
            // Arrange — el radio del hueco y el Size de La Mesa se autorean por separado. Si alguna
            // vez divergen, la promesa que tiene que sobrevivir es la del paño cian: es la única
            // que el jugador puede leer en pantalla.
            _wager.SetChips(5);
            new AINode_TahurMarkTable { Size = 2 }.Tick(Context());

            // Act
            Assert.AreEqual(AIResult.Succeeded, Banca());

            // Assert
            Assert.IsTrue(_threat.TryConsume(_boss, out var area));
            foreach (var tile in _wager.TableTiles)
                Assert.IsFalse(area.Contains(tile), $"La Mesa {tile} quedó amenazada.");
            Assert.AreEqual(WalkableTiles - _wager.TableTiles.Count, area.Tiles.Count);
        }

        // =================================================================
        // Helpers
        // =================================================================

        private static string IdOf(int rank) => $"combo.step_{rank}";

        /// <summary>La sala de la ficha: 11×7 con las cuatro columnas.</summary>
        private static NavGraph TahurRoom()
        {
            var walkable = new bool[RoomWidth * RoomHeight];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = true;
            foreach (var column in Columns) walkable[column.Y * RoomWidth + column.X] = false;
            return NavGraph.FromSnapshot(new GridSnapshot(RoomWidth, RoomHeight, walkable));
        }

        /// <summary>Las casillas desde las que el poke (Manhattan 1) llega al jefe.</summary>
        private static IEnumerable<GridCoord> MeleeReach(GridCoord self)
        {
            yield return self;
            yield return new GridCoord(self.X + 1, self.Y);
            yield return new GridCoord(self.X - 1, self.Y);
            yield return new GridCoord(self.X, self.Y + 1);
            yield return new GridCoord(self.X, self.Y - 1);
        }

        private AIContext Context() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = 290,
            Grid = _grid,
            DamagePipeline = _pipeline,
            PlayerService = _playerService,
            Rng = new System.Random(20260814),
        };

        private AIResult Settle() => _settle.Tick(Context());

        private AIResult Banca() => new AINode_TahurMarkBanca().Tick(Context());

        private AIResult MarkTable() => new AINode_TahurMarkTable().Tick(Context());

        private AIResult Execute() => new AINode_ExecuteTelegraph().Tick(Context());

        private void MovePlayer(GridCoord coord) => _grid.Move(_player, coord);

        private void Call(int rank) => _wager.SetCall(rank, IdOf(rank));

        private void PlayHand(int rank) => TypedEvent<ComboPlayedPayload>.Raise(new ComboPlayedPayload
        {
            SourceGuid = _player,
            ComboId = IdOf(rank),
        });

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
                _baseDamage = priority;
                _priority = priority;
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
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
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
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                Color? tint = null) { }
            public void Clear(Guid sourceGuid) { }
            public void ClearAll() { }
        }
    }
}
