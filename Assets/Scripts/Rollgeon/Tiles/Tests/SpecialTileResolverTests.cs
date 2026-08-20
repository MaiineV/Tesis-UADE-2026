using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Grid;
using Rollgeon.Tiles.Authoring;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Tests de <see cref="SpecialTileResolver"/> y <see cref="SpecialTileSeed"/>:
    /// determinismo por (floorSeed, celda, slotId), persistencia sin re-roll, elección
    /// siempre dentro de las opciones autorizadas, y pares de portal linkeados.
    /// </summary>
    [TestFixture]
    public class SpecialTileResolverTests
    {
        private GameObject _roomGo;
        private RoomLayout _layout;
        private readonly List<Object> _createdAssets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _roomGo = new GameObject("Room_Test");
            _layout = _roomGo.AddComponent<RoomLayout>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_roomGo != null) Object.DestroyImmediate(_roomGo);
            foreach (var asset in _createdAssets)
                if (asset != null) Object.DestroyImmediate(asset);
            _createdAssets.Clear();
        }

        private SpecialTileDefinitionSO MakeDef(string tileId)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileId = tileId;
            _createdAssets.Add(def);
            return def;
        }

        private SpecialTileOptionGroupSO MakeGroup(string groupId, params SpecialTileDefinitionSO[] options)
        {
            var group = ScriptableObject.CreateInstance<SpecialTileOptionGroupSO>();
            group.GroupId = groupId;
            group.Options.AddRange(options);
            _createdAssets.Add(group);
            return group;
        }

        private SpecialTileSlot AddSlot(string slotId, GridCoord coord, params SpecialTileDefinitionSO[] options)
        {
            var slot = new SpecialTileSlot { SlotId = slotId, Coord = coord };
            slot.InlineOptions.AddRange(options);
            _layout.SpecialTileSlots.Add(slot);
            return slot;
        }

        private static readonly Vector2Int Cell = new Vector2Int(2, 3);

        // ======================================================================
        // Determinismo
        // ======================================================================

        [Test]
        public void Resolve_SameSeedAndCell_ReturnsIdenticalChoices()
        {
            AddSlot("SLOT_01", new GridCoord(1, 1), MakeDef("TILE_A"), MakeDef("TILE_B"), MakeDef("TILE_C"));

            var first = SpecialTileResolver.Resolve(_layout, 1234, Cell, roomStates: null);
            var second = SpecialTileResolver.Resolve(_layout, 1234, Cell, roomStates: null);

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(first[0].Definition, second[0].Definition,
                "Mismo (floorSeed, celda, slotId) ⇒ misma elección, siempre.");
        }

        [Test]
        public void Resolve_AddingAnotherSlot_DoesNotChangeExistingChoice()
        {
            AddSlot("SLOT_01", new GridCoord(1, 1), MakeDef("TILE_A"), MakeDef("TILE_B"), MakeDef("TILE_C"));
            var before = SpecialTileResolver.Resolve(_layout, 1234, Cell, null)
                .First(t => t.SourceId == "SLOT_01").Definition;

            AddSlot("SLOT_02", new GridCoord(3, 3), MakeDef("TILE_D"), MakeDef("TILE_E"));

            var after = SpecialTileResolver.Resolve(_layout, 1234, Cell, null)
                .First(t => t.SourceId == "SLOT_01").Definition;
            Assert.AreEqual(before, after,
                "El seed es por-slot (hash del SlotId): agregar un slot no corre los rolls ajenos.");
        }

        [Test]
        public void Resolve_AlwaysPicksFromAuthorizedOptions()
        {
            var a = MakeDef("TILE_A");
            var b = MakeDef("TILE_B");
            AddSlot("SLOT_01", new GridCoord(1, 1), a, b);

            for (int seed = 0; seed < 50; seed++)
            {
                var resolved = SpecialTileResolver.Resolve(_layout, seed, Cell, null);
                Assert.AreEqual(1, resolved.Count);
                Assert.IsTrue(resolved[0].Definition == a || resolved[0].Definition == b,
                    $"Seed {seed}: la elección salió de la lista cerrada — nunca de afuera.");
                Assert.AreEqual(new GridCoord(1, 1), resolved[0].Coord,
                    "La posición del slot es FIJA: la variación solo decide qué opción lo ocupa.");
            }
        }

        [Test]
        public void Resolve_GroupWinsOverInlineOptions()
        {
            var groupOption = MakeDef("TILE_GROUP");
            var slot = AddSlot("SLOT_01", new GridCoord(1, 1), MakeDef("TILE_INLINE"));
            slot.Group = MakeGroup("HazardGroup_A", groupOption);

            var resolved = SpecialTileResolver.Resolve(_layout, 7, Cell, null);

            Assert.AreEqual(groupOption, resolved[0].Definition);
        }

        [Test]
        public void Resolve_CanResolveEmpty_ProducesNoTileForSomeSeed()
        {
            var slot = AddSlot("SLOT_01", new GridCoord(1, 1), MakeDef("TILE_A"));
            slot.CanResolveEmpty = true;

            bool sawEmpty = false, sawTile = false;
            for (int seed = 0; seed < 100 && !(sawEmpty && sawTile); seed++)
            {
                var resolved = SpecialTileResolver.Resolve(_layout, seed, Cell, null);
                if (resolved.Count == 0) sawEmpty = true;
                else sawTile = true;
            }

            Assert.IsTrue(sawEmpty, "El grupo opcional tiene que poder resolver 'nada'.");
            Assert.IsTrue(sawTile, "Y también tiene que poder resolver la opción real.");
        }

        // ======================================================================
        // Persistencia — re-entry / resume sin re-roll
        // ======================================================================

        [Test]
        public void Resolve_WritesSlotStateOnFirstResolve()
        {
            AddSlot("SLOT_01", new GridCoord(1, 1), MakeDef("TILE_A"), MakeDef("TILE_B"));
            var states = new SerializableObjectStates();

            var resolved = SpecialTileResolver.Resolve(_layout, 1234, Cell, states);

            Assert.IsTrue(states.TryGet<SpecialTileSlotState>("stile.SLOT_01", out var state));
            Assert.AreEqual(resolved[0].Definition.TileId, state.ChosenTileId);
        }

        [Test]
        public void Resolve_HydratesFromState_WithoutReRolling()
        {
            var a = MakeDef("TILE_A");
            var b = MakeDef("TILE_B");
            AddSlot("SLOT_01", new GridCoord(1, 1), a, b);

            // El roll natural para este seed elige una opción; forzamos la OTRA en el estado
            // (simula un save donde el roll salió distinto) — el estado tiene que ganar.
            var natural = SpecialTileResolver.Resolve(_layout, 1234, Cell, null)[0].Definition;
            var other = natural == a ? b : a;
            var states = new SerializableObjectStates();
            states.Set("stile.SLOT_01", new SpecialTileSlotState { ChosenTileId = other.TileId });

            var resolved = SpecialTileResolver.Resolve(_layout, 1234, Cell, states);

            Assert.AreEqual(other, resolved[0].Definition,
                "Con estado persistido no se re-rolea: re-entry y resume respetan la elección.");
        }

        [Test]
        public void Resolve_EmptyChoicePersisted_StaysEmpty()
        {
            var slot = AddSlot("SLOT_01", new GridCoord(1, 1), MakeDef("TILE_A"));
            slot.CanResolveEmpty = true;
            var states = new SerializableObjectStates();
            states.Set("stile.SLOT_01", new SpecialTileSlotState { ChosenTileId = string.Empty });

            var resolved = SpecialTileResolver.Resolve(_layout, 1234, Cell, states);

            Assert.AreEqual(0, resolved.Count, "'Nada' persistido sigue siendo nada al re-entrar.");
        }

        [Test]
        public void Resolve_SavedOptionNoLongerAuthorized_ReRollsWithWarning()
        {
            AddSlot("SLOT_01", new GridCoord(1, 1), MakeDef("TILE_A"));
            var states = new SerializableObjectStates();
            states.Set("stile.SLOT_01", new SpecialTileSlotState { ChosenTileId = "TILE_REMOVED" });
            var warnings = new List<string>();

            var resolved = SpecialTileResolver.Resolve(_layout, 1234, Cell, states, warnings);

            Assert.AreEqual(1, warnings.Count);
            Assert.LessOrEqual(resolved.Count, 1);
        }

        // ======================================================================
        // Permanentes y portales
        // ======================================================================

        [Test]
        public void Resolve_PermanentsAtExactCoords_NoRandomization()
        {
            var spikes = MakeDef("TILE_SPIKES");
            _layout.SpecialTilePlacements.Add(new SpecialTilePlacement
            {
                Definition = spikes,
                Coord = new GridCoord(4, 2),
            });

            for (int seed = 0; seed < 10; seed++)
            {
                var resolved = SpecialTileResolver.Resolve(_layout, seed, Cell, null);
                Assert.AreEqual(1, resolved.Count);
                Assert.AreEqual(new GridCoord(4, 2), resolved[0].Coord);
                Assert.AreEqual(spikes, resolved[0].Definition);
            }
        }

        [Test]
        public void Resolve_PortalPair_BothEndsShareLinkId()
        {
            var portal = MakeDef("TILE_PORTAL");
            _layout.PortalPairs.Add(new PortalPairPlacement
            {
                PortalDefinition = portal,
                CoordA = new GridCoord(1, 1),
                CoordB = new GridCoord(5, 5),
            });

            var resolved = SpecialTileResolver.Resolve(_layout, 0, Cell, null);

            Assert.AreEqual(2, resolved.Count);
            Assert.AreEqual(resolved[0].PortalLinkId, resolved[1].PortalLinkId);
            Assert.GreaterOrEqual(resolved[0].PortalLinkId, 0);
            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(1, 1), new GridCoord(5, 5) },
                new[] { resolved[0].Coord, resolved[1].Coord });
        }

        [Test]
        public void Resolve_DegeneratePortalPair_IgnoredWithWarning()
        {
            _layout.PortalPairs.Add(new PortalPairPlacement
            {
                PortalDefinition = MakeDef("TILE_PORTAL"),
                CoordA = new GridCoord(1, 1),
                CoordB = new GridCoord(1, 1),
            });
            var warnings = new List<string>();

            var resolved = SpecialTileResolver.Resolve(_layout, 0, Cell, null, warnings);

            Assert.AreEqual(0, resolved.Count);
            Assert.AreEqual(1, warnings.Count);
        }

        // ======================================================================
        // Seed
        // ======================================================================

        [Test]
        public void Seed_SensitiveToSlotIdAndCell_StableOtherwise()
        {
            int baseline = SpecialTileSeed.Derive(1234, Cell, "SLOT_01");

            Assert.AreEqual(baseline, SpecialTileSeed.Derive(1234, Cell, "SLOT_01"), "Estable.");
            Assert.AreNotEqual(baseline, SpecialTileSeed.Derive(1234, Cell, "SLOT_02"), "Cambia por slot.");
            Assert.AreNotEqual(baseline, SpecialTileSeed.Derive(1234, new Vector2Int(9, 9), "SLOT_01"),
                "Cambia por celda de sala.");
            Assert.AreNotEqual(baseline, SpecialTileSeed.Derive(99, Cell, "SLOT_01"), "Cambia por seed de piso.");
        }
    }
}
