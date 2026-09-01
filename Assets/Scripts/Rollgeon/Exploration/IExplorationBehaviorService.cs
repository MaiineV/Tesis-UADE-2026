namespace Rollgeon.Exploration
{
    public interface IExplorationBehaviorService
    {
        bool IsActive { get; }

        /// <summary>
        /// Ejecuta el behavior asociado al <paramref name="slot"/> (el int es el
        /// valor del enum <c>HeroBehaviorSlot</c>: 0=Movement, 1=BaseAttack,
        /// 2=ClassSkill, 3=Healing, 4=ForceDoor/PassDoor).
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

        /// <summary>
        /// Cancela la caminata click-to-move en curso (hotkey X): el pawn frena al
        /// completar el step actual y la posición lógica se trunca a esa celda.
        /// Cancelar una caminata hacia una puerta NO cruza de sala ni transiciona
        /// de piso. <c>true</c> si había una caminata que cancelar.
        /// </summary>
        bool TryCancelPendingWalk();
    }
}
