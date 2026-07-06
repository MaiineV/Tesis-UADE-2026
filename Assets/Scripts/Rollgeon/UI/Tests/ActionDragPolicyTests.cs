using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.DragDrop;

namespace Rollgeon.UI.Tests
{
    public class ActionDragPolicyTests
    {
        [Test]
        public void test_canBeginDrag_available_returnsTrue()
        {
            Assert.IsTrue(ActionDragPolicy.CanBeginDrag(ActionButtonState.Available));
        }

        [Test]
        public void test_canBeginDrag_lockedSelectedUsed_returnsFalse()
        {
            Assert.IsFalse(ActionDragPolicy.CanBeginDrag(ActionButtonState.Locked));
            Assert.IsFalse(ActionDragPolicy.CanBeginDrag(ActionButtonState.Selected));
            Assert.IsFalse(ActionDragPolicy.CanBeginDrag(ActionButtonState.Used));
        }

        [Test]
        public void test_isValidDrop_coordInSet_returnsTrue()
        {
            // Arrange
            var set = new HashSet<GridCoord> { new GridCoord(1, 2), new GridCoord(3, 4) };

            // Act + Assert
            Assert.IsTrue(ActionDragPolicy.IsValidDrop(new GridCoord(3, 4), set));
        }

        [Test]
        public void test_isValidDrop_coordNotInSet_returnsFalse()
        {
            var set = new HashSet<GridCoord> { new GridCoord(1, 2) };
            Assert.IsFalse(ActionDragPolicy.IsValidDrop(new GridCoord(9, 9), set));
        }

        [Test]
        public void test_isValidDrop_nullSet_returnsFalse()
        {
            Assert.IsFalse(ActionDragPolicy.IsValidDrop(new GridCoord(0, 0), null));
        }

        [Test]
        public void test_requiresTileDrop_movementLike_returnsTrue()
        {
            // Arrange — Empty + AutoAccept + 1 target = Movimiento.
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Empty,
                AutoResolve = false,
                AutoAccept = true,
                IsConstantSelectionCount = true,
                SelectionCount = 1,
            };

            // Act + Assert
            Assert.IsTrue(ActionDragPolicy.RequiresTileDrop(settings));
        }

        [Test]
        public void test_requiresTileDrop_selfState_returnsFalse()
        {
            // Self no necesita interacción del jugador.
            var settings = new SelectionSettings { SlotState = SlotState.Self };
            Assert.IsFalse(ActionDragPolicy.RequiresTileDrop(settings));
        }

        [Test]
        public void test_requiresTileDrop_autoResolve_returnsFalse()
        {
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                AutoResolve = true,
                AutoAccept = true,
                SelectionCount = 1,
            };
            Assert.IsFalse(ActionDragPolicy.RequiresTileDrop(settings));
        }

        [Test]
        public void test_requiresTileDrop_multiTarget_returnsFalse()
        {
            // Un solo drop no puede satisfacer SelectionCount > 1.
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                AutoResolve = false,
                AutoAccept = true,
                SelectionCount = 3,
            };
            Assert.IsFalse(ActionDragPolicy.RequiresTileDrop(settings));
        }

        [Test]
        public void test_requiresTileDrop_null_returnsFalse()
        {
            Assert.IsFalse(ActionDragPolicy.RequiresTileDrop(null));
        }
    }
}
