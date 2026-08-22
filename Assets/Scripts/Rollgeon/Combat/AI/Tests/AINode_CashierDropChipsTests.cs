using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_CashierDropChips"/>: la ficha cae DENTRO de la columna recién
    /// marcada y a 2-3 casillas del jugador — "te muestra la plata exactamente donde va a caer el
    /// hacha". También cubre que sólo pague cuando le pegaron y que no invente fichas sin columna.
    /// </summary>
    [TestFixture]
    public class AINode_CashierDropChipsTests
    {
        private const int RoomSize = 9;

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private HazardService _hazards;
        private FakeCashierLedgerService _ledger;
        private HazardDefinitionSO _chip;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSize, RoomSize));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _hazards = new HazardService();
            _hazards.Register();

            _player = Guid.NewGuid();
            _boss = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(4, 4));
            _grid.Register(_boss, new GridCoord(8, 4));

            _ledger = new FakeCashierLedgerService { DamageTaken = true };
            ServiceLocator.AddService<ICashierLedgerService>(_ledger);

            _chip = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _chip.hideFlags = HideFlags.HideAndDontSave;
            _chip.Trigger = HazardTriggerMode.OnEnter;
            _chip.ConsumeOnTrigger = true;
            _chip.Damage = 0;
            _chip.DurationRounds = 1;
            _chip.SourceId = Guid.NewGuid().ToString();
        }

        [TearDown]
        public void TearDown()
        {
            _hazards.Dispose();
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            UnityEngine.Object.DestroyImmediate(_chip);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ---- Helpers -----------------------------------------------------

        /// <summary>Marca la columna de 3 de ancho del jugador, como haría el nodo de la columna.</summary>
        private HashSet<GridCoord> MarkColumn(int size = 3)
        {
            Assert.IsTrue(_grid.TryGetPosition(_player, out var playerCoord));
            var tiles = ThreatAreaShape.Compute(_grid, playerCoord, ThreatShape.Column, size, HalfRoomAxis.Vertical);
            _threat.Mark(_boss, tiles, 28, AttackKind.BasicAttack);
            return tiles;
        }

        private AINode_CashierDropChips NewNode() => new AINode_CashierDropChips
        {
            Chip = _chip,
            Count = 1,
            MinValue = 6,
            MaxValue = 9,
            MinDistanceFromPlayer = 2,
            MaxDistanceFromPlayer = 3,
            RequireDamageTaken = true,
        };

        private AIContext NewContext(int seed = 3) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            SelfMaxHp = 190,
            Rng = new System.Random(seed),
        };

        private List<HazardInstanceInfo> LiveChips()
        {
            var live = new List<HazardInstanceInfo>();
            foreach (var info in _hazards.ActiveInstances()) live.Add(info);
            return live;
        }

        // ---- Caso central ------------------------------------------------

        [Test]
        public void Tick_DropsOneChip_InsideTheMarkedColumn_AtTwoOrThreeTiles()
        {
            var column = MarkColumn();

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            var chips = LiveChips();
            Assert.AreEqual(1, chips.Count, "Una ficha por golpe recibido.");

            Assert.IsTrue(_grid.TryGetPosition(_player, out var playerCoord));
            foreach (var coord in chips[0].Tiles)
            {
                Assert.IsTrue(column.Contains(coord),
                    $"La ficha {coord} cayó fuera de la columna marcada — el anzuelo pierde sentido.");
                int dist = coord.Manhattan(playerCoord);
                Assert.GreaterOrEqual(dist, 2, "La ficha nunca cae encima del jugador (gratis).");
                Assert.LessOrEqual(dist, 3, "Ni tan lejos que no valga la pena el desvío.");
            }
        }

        [Test]
        public void Tick_RegistersTheChipValue_InTheFichaRange()
        {
            MarkColumn();

            NewNode().Tick(NewContext());

            Assert.AreEqual(1, _ledger.RegisteredChips);
            Assert.GreaterOrEqual(_ledger.LastChipValue, 6);
            Assert.LessOrEqual(_ledger.LastChipValue, 9);
            Assert.AreEqual(_boss, _ledger.LastChipOwner, "La ficha recuerda quién la soltó.");
        }

        [Test]
        public void Tick_AfterTheAudit_ChipsAreWorthDouble()
        {
            MarkColumn();
            _ledger.ChipValueMultiplier = 2;

            NewNode().Tick(NewContext());

            Assert.GreaterOrEqual(_ledger.LastChipValue, 12);
            Assert.LessOrEqual(_ledger.LastChipValue, 18,
                "Post-arqueo la tentación sube: 6-9 pasa a 12-18.");
        }

        [Test]
        public void Tick_TheChipIsAOneTurnConsumableTrap_NotDamage()
        {
            MarkColumn();

            NewNode().Tick(NewContext());

            var def = LiveChips()[0].Definition;
            Assert.AreEqual(HazardTriggerMode.OnEnter, def.Trigger, "Se cobra al pisarla, escaneando el path.");
            Assert.IsTrue(def.ConsumeOnTrigger, "Se consume: una ficha, un cobro.");
            Assert.AreEqual(0, def.Damage, "La ficha no lastima — lastima la columna donde está.");
            Assert.AreEqual(1, LiveChips()[0].RemainingRounds, "Dura un turno.");
        }

        [Test]
        public void Tick_ConsumesTheDamageFlag_SoOneHitPaysOneChip()
        {
            MarkColumn();
            var node = NewNode();
            var context = NewContext();

            Assert.AreEqual(AIResult.Succeeded, node.Tick(context));

            MarkColumn(); // Turno siguiente sin recibir golpes.
            Assert.AreEqual(AIResult.Failed, node.Tick(context),
                "Sin golpe nuevo no hay ficha nueva.");
            Assert.AreEqual(1, _ledger.RegisteredChips);
        }

        // ---- El piso garantizado -------------------------------------------

        [Test]
        public void Tick_WithoutDamageTaken_StillDropsTheGuaranteedFloor()
        {
            MarkColumn();
            _ledger.DamageTaken = false;
            var node = NewNode();
            node.Count = 2;
            node.MinCount = 1;

            var result = node.Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(1, LiveChips().Count,
                "La columna siempre deja algo en el piso: es lo que hace que se vean monedas.");
        }

        [Test]
        public void Tick_WithDamageTaken_DropsTheFullCount_SoHittingHimStillPays()
        {
            MarkColumn();
            var node = NewNode();
            node.Count = 2;
            node.MinCount = 1;

            node.Tick(NewContext());

            Assert.AreEqual(2, LiveChips().Count,
                "Pegarle sigue pagando — sube de MinCount a Count. El personaje no cambia.");
        }

        [Test]
        public void Tick_TheFloorStillConsumesTheDamageFlag_SoNoHitIsPaidTwice()
        {
            // El flag es destructivo y no se puede pre-chequear. Dejarlo puesto haría que el próximo
            // turno de columna cobrara otra vez un golpe ya pagado.
            MarkColumn();
            var node = NewNode();
            node.Count = 2;
            node.MinCount = 1;
            var context = NewContext();

            node.Tick(context);        // Le pegaron: 2 fichas y consume el flag.
            MarkColumn();
            node.Tick(context);        // Sin golpe nuevo: cae al piso.

            Assert.AreEqual(3, _ledger.RegisteredChips,
                "2 del turno con golpe + 1 del piso, no 2 + 2.");
        }

        // ---- Failed benignos ----------------------------------------------

        [Test]
        public void Tick_WithoutDamageTaken_AndNoFloor_ReturnsFailed_AndDropsNothing()
        {
            MarkColumn();
            _ledger.DamageTaken = false;

            // MinCount = 0 (default del nodo): sin piso, sin golpe, no hay ficha.
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(NewContext()));
            Assert.IsEmpty(LiveChips());
        }

        [Test]
        public void Tick_WithoutAMarkedColumn_ReturnsFailed()
        {
            // El jefe recibió daño pero todavía no marcó nada (o la marca ya detonó).
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(NewContext()));
            Assert.IsEmpty(LiveChips());
        }

        [Test]
        public void Tick_WithoutChipDefinition_ReturnsFailed()
        {
            MarkColumn();
            var node = NewNode();
            node.Chip = null;

            Assert.AreEqual(AIResult.Failed, node.Tick(NewContext()));
        }

        [Test]
        public void Tick_BandFullyBlocked_FallsBackToTheClosestLegalTile()
        {
            // Columna de 1 de ancho: la banda 2-3 del jugador en (4,4) son (4,1),(4,2),(4,6),(4,7).
            // Tapadas las cuatro, la ficha tiene que caer en la más cercana que igual respete el
            // mínimo — una ficha lejos es mejor que un turno sin pagar.
            MarkColumn(size: 1);
            foreach (var blocked in new[] { new GridCoord(4, 1), new GridCoord(4, 2), new GridCoord(4, 6), new GridCoord(4, 7) })
                _grid.Register(Guid.NewGuid(), blocked);

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Succeeded, result, "Con la banda tapada igual suelta ficha.");
            foreach (var coord in LiveChips()[0].Tiles)
            {
                Assert.AreEqual(4, coord.Manhattan(new GridCoord(4, 4)),
                    "El fallback elige la casilla legal más cercana (distancia 4), no una al azar.");
            }
        }

        [Test]
        public void Tick_MinDistanceIsHard_NeverDropsNextToThePlayer()
        {
            // Toda la banda 2-3 tapada y las casillas a distancia 1 libres: la ficha igual no
            // puede caer pegada al jugador (sería oro gratis sin gastar el movimiento).
            MarkColumn(size: 1);
            foreach (var blocked in new[] { new GridCoord(4, 0), new GridCoord(4, 1), new GridCoord(4, 2),
                                            new GridCoord(4, 6), new GridCoord(4, 7), new GridCoord(4, 8) })
                _grid.Register(Guid.NewGuid(), blocked);

            var result = NewNode().Tick(NewContext());

            Assert.AreEqual(AIResult.Failed, result,
                "Sin casilla a distancia >= 2 no hay ficha: nunca cae en (4,3)/(4,5).");
            Assert.IsEmpty(LiveChips());
        }

        [Test]
        public void Tick_TwoChips_LandOnDifferentTiles()
        {
            MarkColumn();
            var node = NewNode();
            node.Count = 2;

            Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext()));

            var chips = LiveChips();
            Assert.AreEqual(2, chips.Count);

            var occupied = new HashSet<GridCoord>();
            foreach (var chip in chips)
            {
                foreach (var coord in chip.Tiles)
                    Assert.IsTrue(occupied.Add(coord), "Dos fichas no pueden compartir casilla.");
            }
        }

        [Test]
        public void Tick_DoesNotDropOnOccupiedTiles()
        {
            // Columna de 1: banda = (4,1),(4,2),(4,6),(4,7). Tapadas tres, sólo queda (4,2).
            MarkColumn(size: 1);
            foreach (var blocked in new[] { new GridCoord(4, 1), new GridCoord(4, 6), new GridCoord(4, 7) })
                _grid.Register(Guid.NewGuid(), blocked);

            NewNode().Tick(NewContext());

            var chips = LiveChips();
            Assert.AreEqual(1, chips.Count);
            foreach (var coord in chips[0].Tiles)
                Assert.AreEqual(new GridCoord(4, 2), coord, "La ficha va a la única casilla libre de la banda.");
        }

        [Test]
        public void Tick_NullContext_ReturnsFailed()
        {
            Assert.AreEqual(AIResult.Failed, NewNode().Tick(null));
        }
    }
}
