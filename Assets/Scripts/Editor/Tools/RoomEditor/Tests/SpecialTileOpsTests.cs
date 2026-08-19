using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Authoring;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor.Tests
{
    /// <summary>
    /// Tests de <see cref="SpecialTileOps"/>: el par de portal como unidad indivisible
    /// (anti-huérfano), overlap entre las 3 listas, SlotIds únicos y las validaciones.
    /// </summary>
    [TestFixture]
    public class SpecialTileOpsTests
    {
        private readonly List<Object> _created = new();
        private RoomLayout _layout;
        private SpecialTileDefinitionSO _def;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Room_OpsTest");
            _created.Add(go);
            _layout = go.AddComponent<RoomLayout>();

            _def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _def.TileId = "TILE_TEST";
            _created.Add(_def);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private static GridCoord C(int x, int y) => new GridCoord(x, y);

        // ------------------------------------------------------------------
        // PortalPair — unidad indivisible
        // ------------------------------------------------------------------

        [Test]
        public void AddPortalPair_CreatesSingleRecordWithBothCoords()
        {
            var pair = SpecialTileOps.AddPortalPair(_layout, _def, C(1, 1), C(5, 5));

            Assert.IsNotNull(pair);
            Assert.AreEqual(1, _layout.PortalPairs.Count);
            Assert.AreEqual(C(1, 1), pair.CoordA);
            Assert.AreEqual(C(5, 5), pair.CoordB);
        }

        [Test]
        public void AddPortalPair_SameCoordBothEnds_IsRejected()
        {
            var pair = SpecialTileOps.AddPortalPair(_layout, _def, C(1, 1), C(1, 1));

            Assert.IsNull(pair);
            Assert.AreEqual(0, _layout.PortalPairs.Count);
        }

        [Test]
        public void RemoveAt_EitherPortalEnd_RemovesEntirePair()
        {
            SpecialTileOps.AddPortalPair(_layout, _def, C(1, 1), C(5, 5));

            bool removed = SpecialTileOps.RemoveAt(_layout, C(5, 5));

            Assert.IsTrue(removed);
            Assert.AreEqual(0, _layout.PortalPairs.Count,
                "Borrar un extremo borra el PAR entero — el huérfano no es un estado alcanzable.");
        }

        [Test]
        public void MoveTo_PortalEnd_MovesOnlyThatEnd()
        {
            SpecialTileOps.AddPortalPair(_layout, _def, C(1, 1), C(5, 5));

            bool moved = SpecialTileOps.MoveTo(_layout, C(1, 1), C(2, 2));

            Assert.IsTrue(moved);
            Assert.AreEqual(C(2, 2), _layout.PortalPairs[0].CoordA);
            Assert.AreEqual(C(5, 5), _layout.PortalPairs[0].CoordB);
        }

        // ------------------------------------------------------------------
        // Overlaps y SlotIds
        // ------------------------------------------------------------------

        [Test]
        public void IsCellFree_ChecksAllThreeLists()
        {
            SpecialTileOps.AddPermanent(_layout, _def, C(1, 0));
            SpecialTileOps.AddSlot(_layout, C(2, 0));
            SpecialTileOps.AddPortalPair(_layout, _def, C(3, 0), C(4, 0));

            Assert.IsFalse(SpecialTileOps.IsCellFree(_layout, C(1, 0)), "Permanente ocupa.");
            Assert.IsFalse(SpecialTileOps.IsCellFree(_layout, C(2, 0)), "Slot ocupa.");
            Assert.IsFalse(SpecialTileOps.IsCellFree(_layout, C(3, 0)), "Extremo A ocupa.");
            Assert.IsFalse(SpecialTileOps.IsCellFree(_layout, C(4, 0)), "Extremo B ocupa.");
            Assert.IsTrue(SpecialTileOps.IsCellFree(_layout, C(5, 0)));
        }

        [Test]
        public void MoveTo_OccupiedDestination_IsRejected()
        {
            SpecialTileOps.AddPermanent(_layout, _def, C(1, 0));
            SpecialTileOps.AddSlot(_layout, C(2, 0));

            bool moved = SpecialTileOps.MoveTo(_layout, C(1, 0), C(2, 0));

            Assert.IsFalse(moved);
            Assert.AreEqual(C(1, 0), _layout.SpecialTilePlacements[0].Coord);
        }

        [Test]
        public void GenerateSlotId_StaysUniqueAfterDeletions()
        {
            var s1 = SpecialTileOps.AddSlot(_layout, C(0, 0));
            var s2 = SpecialTileOps.AddSlot(_layout, C(1, 0));
            Assert.AreEqual("SLOT_01", s1.SlotId);
            Assert.AreEqual("SLOT_02", s2.SlotId);

            SpecialTileOps.RemoveAt(_layout, C(0, 0)); // borra SLOT_01
            var s3 = SpecialTileOps.AddSlot(_layout, C(2, 0));

            Assert.AreEqual("SLOT_01", s3.SlotId, "El hueco liberado se reusa sin chocar con SLOT_02.");
            Assert.AreEqual(2, _layout.SpecialTileSlots.Select(s => s.SlotId).Distinct().Count());
        }

        // ------------------------------------------------------------------
        // Validate
        // ------------------------------------------------------------------

        [Test]
        public void Validate_FlagsSlotWithoutOptions_AsError()
        {
            SpecialTileOps.AddSlot(_layout, C(1, 1)); // sin opciones

            var messages = SpecialTileOps.Validate(_layout);

            Assert.IsTrue(messages.Any(m => m.StartsWith("ERROR:") && m.Contains("sin opciones")),
                string.Join("\n", messages));
        }

        [Test]
        public void Validate_FlagsDuplicatedSlotIds_AsError()
        {
            var s1 = SpecialTileOps.AddSlot(_layout, C(1, 1));
            var s2 = SpecialTileOps.AddSlot(_layout, C(2, 2));
            s1.InlineOptions.Add(_def);
            s2.InlineOptions.Add(_def);
            s2.SlotId = s1.SlotId;

            var messages = SpecialTileOps.Validate(_layout);

            Assert.IsTrue(messages.Any(m => m.StartsWith("ERROR:") && m.Contains("duplicado")),
                string.Join("\n", messages));
        }

        [Test]
        public void Validate_FlagsSharedCell_AsError()
        {
            SpecialTileOps.AddPermanent(_layout, _def, C(1, 1));
            var slot = SpecialTileOps.AddSlot(_layout, C(1, 1));
            slot.InlineOptions.Add(_def);

            var messages = SpecialTileOps.Validate(_layout);

            Assert.IsTrue(messages.Any(m => m.StartsWith("ERROR:") && m.Contains("comparten la celda")),
                string.Join("\n", messages));
        }

        [Test]
        public void Validate_FlagsDuplicatedOptions_AsWarning()
        {
            var slot = SpecialTileOps.AddSlot(_layout, C(1, 1));
            slot.InlineOptions.Add(_def);
            slot.InlineOptions.Add(_def);

            var messages = SpecialTileOps.Validate(_layout);

            Assert.IsTrue(messages.Any(m => m.StartsWith("WARN:") && m.Contains("duplicadas")),
                string.Join("\n", messages));
        }

        [Test]
        public void Validate_CleanLayout_ReturnsNoMessages()
        {
            SpecialTileOps.AddPermanent(_layout, _def, C(1, 1));
            var slot = SpecialTileOps.AddSlot(_layout, C(2, 2));
            slot.InlineOptions.Add(_def);
            SpecialTileOps.AddPortalPair(_layout, _def, C(3, 3), C(4, 4));

            var messages = SpecialTileOps.Validate(_layout);

            Assert.IsEmpty(messages, string.Join("\n", messages));
        }
    }
}
