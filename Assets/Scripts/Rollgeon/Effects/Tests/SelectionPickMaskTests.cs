using NUnit.Framework;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Visuals;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// El raycast de targeting se enmascara según la acción activa: un movimiento no puede
    /// ver a ninguna entidad (apuntar al modelo de un enemigo tiene que caer al piso), un
    /// ataque no puede ver al héroe (su cuerpo tapaba a los enemigos de atrás con la cámara
    /// en ángulo) pero sí a enemigos y props.
    /// </summary>
    [TestFixture]
    public sealed class SelectionPickMaskTests
    {
        private static SelectionSettings Make(SlotState slot, EntityFilterMask filter = EntityFilterMask.Enemies)
            => new SelectionSettings { SlotState = slot, EntityFilter = filter };

        [Test]
        public void For_NullSettings_ReturnsUnfiltered()
        {
            int mask = SelectionPickMask.For(null);

            Assert.AreEqual(Physics.DefaultRaycastLayers, mask,
                "Sin selección activa el pick tiene que ver lo mismo que antes de la máscara.");
        }

        [Test]
        public void For_EmptySlot_IgnoresEveryPawn()
        {
            var movement = Make(SlotState.Empty);

            int mask = SelectionPickMask.For(movement);

            Assert.AreEqual(SelectionPickMask.None, mask,
                "Un movimiento que ve pawns devuelve la celda del enemigo apuntado en vez del piso de abajo.");
        }

        [Test]
        public void For_SelfSlot_IgnoresEveryPawn()
        {
            var self = Make(SlotState.Self);

            int mask = SelectionPickMask.For(self);

            Assert.AreEqual(SelectionPickMask.None, mask);
        }

        [Test]
        public void For_OccupiedEnemies_SeesEntitiesButNotPlayer()
        {
            var attack = Make(SlotState.Occupied, EntityFilterMask.Enemies);

            int mask = SelectionPickMask.For(attack);

            Assert.AreEqual(PawnLayers.EntityMask, mask,
                "Un ataque tiene que ver a los enemigos y nada más: el héroe delante no puede capturar el rayo.");
            Assert.AreEqual(0, mask & PawnLayers.PlayerMask);
        }

        [Test]
        public void For_OccupiedEnemiesAndProps_SeesEntitiesOnly()
        {
            var attack = Make(SlotState.Occupied, EntityFilterMask.Enemies | EntityFilterMask.Props);

            int mask = SelectionPickMask.For(attack);

            Assert.AreEqual(PawnLayers.EntityMask, mask,
                "Los props (cofres, bombas) viven en Entity junto con los enemigos.");
        }

        [Test]
        public void For_OccupiedPlayer_SeesPlayerOnly()
        {
            var potion = Make(SlotState.Occupied, EntityFilterMask.Player);

            int mask = SelectionPickMask.For(potion);

            Assert.AreEqual(PawnLayers.PlayerMask, mask,
                "La poción apunta al héroe: su cuerpo es el único que puede capturar el rayo.");
        }

        [Test]
        public void For_OccupiedAlliesAndEnemies_SeesBothLayers()
        {
            var any = Make(SlotState.Occupied, EntityFilterMask.Allies | EntityFilterMask.Enemies);

            int mask = SelectionPickMask.For(any);

            Assert.AreEqual(PawnLayers.AllPawnsMask, mask);
        }

        [Test]
        public void For_BothSlot_UsesEntityFilter()
        {
            var both = Make(SlotState.Both, EntityFilterMask.Enemies);

            int mask = SelectionPickMask.For(both);

            Assert.AreEqual(PawnLayers.EntityMask, mask,
                "Both acepta piso y ocupantes: los ocupantes que puede ver los define el filtro.");
        }

        [Test]
        public void For_OccupiedNoneFilter_IgnoresEveryPawn()
        {
            var nobody = Make(SlotState.Occupied, EntityFilterMask.None);

            int mask = SelectionPickMask.For(nobody);

            Assert.AreEqual(SelectionPickMask.None, mask);
        }
    }
}
