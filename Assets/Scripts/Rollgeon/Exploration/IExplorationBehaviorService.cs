namespace Rollgeon.Exploration
{
    public interface IExplorationBehaviorService
    {
        bool IsActive { get; }

        /// <summary>
        /// Ejecuta el behavior asociado al <paramref name="slot"/> (el int es el
        /// valor del enum <c>HeroBehaviorSlot</c>: 0=Movement, 1=BaseAttack,
        /// 2=SpecialAttack, 3=Healing, 4=ForceDoor/PassDoor).
        /// </summary>
        /// <remarks>
        /// El parámetro es <b>slot</b>, no list-index. Antes era list-index, lo
        /// cual causaba que botones cuyo orden no coincidía con
        /// <c>HeroBehaviorSlot</c> dispararan el behavior equivocado (ej. el
        /// botón de "Pass Door" terminaba ejecutando "Healing" porque en
        /// exploración la lista filtrada quedaba [Movement, Healing, PassDoor]
        /// y el slot 1 caía en Healing).
        /// </remarks>
        void OnBehaviorSelected(int slot);

        void CancelSelection();
    }
}
