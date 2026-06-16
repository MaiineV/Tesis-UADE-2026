using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Regresión del planner de topología. Cubre dos modos de falla observados en
    /// el preview del Floor Editor (seed 12345, "Piso 1"):
    /// 1. Un slot Enchantment con count>0 no se colocaba (estaba fuera de
    ///    <c>specialOrder</c>): su count inflaba el target pero la cell caía al
    ///    fallback de Combat, así que las salas de encantamiento nunca aparecían.
    /// 2. Caracterización: si el pool de un slot contiene una sala de OTRO tipo, la
    ///    cell hereda el tipo de la sala pooled (el planner confía en el pool tal
    ///    cual). Esto producía "dos starts" cuando el slot Shop quedó cableado a la
    ///    sala Start por error de datos.
    /// </summary>
    [TestFixture]
    public class FloorTopologyPlannerTests
    {
        private readonly List<Object> _created = new();

        private RoomSO Room(string id, RoomType type)
        {
            var room = ScriptableObject.CreateInstance<RoomSO>();
            room.RoomId = id;
            room.DisplayName = id;
            room.Type = type;
            _created.Add(room);
            return room;
        }

        private RoomTypeSlot Slot(RoomType type, int fixedCount, RoomSO pooled)
        {
            return new RoomTypeSlot
            {
                Type = type,
                Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = fixedCount },
                Pool = new List<RoomSO> { pooled },
            };
        }

        private FloorLayoutSO Layout(params RoomTypeSlot[] slots)
        {
            var layout = ScriptableObject.CreateInstance<FloorLayoutSO>();
            layout.Slots = new List<RoomTypeSlot>(slots);
            _created.Add(layout);
            return layout;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void Generate_EnchantmentSlotWithCount_PlacesEnchantmentRoom()
        {
            // Arrange — Start + Boss + Combat + Enchantment, todos pooled con su tipo.
            var layout = Layout(
                Slot(RoomType.Start, 1, Room("start", RoomType.Start)),
                Slot(RoomType.Boss, 1, Room("boss", RoomType.Boss)),
                Slot(RoomType.Combat, 1, Room("combat", RoomType.Combat)),
                Slot(RoomType.Enchantment, 1, Room("ench", RoomType.Enchantment)));

            // Act
            var plan = FloorTopologyPlanner.Generate(layout, seed: 12345);

            // Assert — exactamente una cell de tipo Enchantment (antes del fix: 0).
            Assert.AreEqual(1, plan.Types.Values.Count(t => t == RoomType.Enchantment),
                "Un slot Enchantment con count=1 debe colocar exactamente una sala Enchantment.");
            Assert.IsFalse(plan.Warnings.Any(w => w.Contains("Enchantment")),
                "No debería quedar cupo de Enchantment sin colocar.");
        }

        [Test]
        public void Generate_SlotPoolHasWrongType_CellInheritsPooledRoomType()
        {
            // Arrange — el slot Shop quedó (por error de datos) pooled con una sala Start.
            var layout = Layout(
                Slot(RoomType.Start, 1, Room("start", RoomType.Start)),
                Slot(RoomType.Boss, 1, Room("boss", RoomType.Boss)),
                Slot(RoomType.Shop, 1, Room("mislabeled", RoomType.Start)));

            // Act
            var plan = FloorTopologyPlanner.Generate(layout, seed: 12345);

            // Assert — ninguna Shop; la cell hereda Start → "dos starts".
            Assert.AreEqual(0, plan.Types.Values.Count(t => t == RoomType.Shop),
                "Con el pool mal cableado, ninguna cell sale como Shop.");
            Assert.AreEqual(2, plan.Types.Values.Count(t => t == RoomType.Start),
                "La cell Shop hereda el tipo de la sala pooled (Start), dando dos cells Start.");
        }
    }
}
