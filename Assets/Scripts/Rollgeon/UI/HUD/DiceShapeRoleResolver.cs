using Rollgeon.Dice;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Elige qué sprite del set de un dado corresponde al estado actual del slot.
    /// Existe como función pura para que <c>DiceSlotView</c> tenga UN solo escritor de
    /// <c>Image.sprite</c>: con un escritor por estado (roll, hold, block, hover) el sprite
    /// que queda depende del orden de las llamadas, que es justo el bug que produce
    /// "el dado quedó con la cara equivocada".
    /// </summary>
    public static class DiceShapeRoleResolver
    {
        /// <summary>
        /// Rol a dibujar. Precedencia: <b>spin &gt; blocked &gt; held &gt; hovered &gt; Front</b>.
        /// </summary>
        /// <param name="blocked">Dado bloqueado por boss.</param>
        /// <param name="held">Dado holdeado (apartado del reroll).</param>
        /// <param name="hovered">Puntero encima.</param>
        /// <param name="spin">Rol impuesto por el ciclado del giro, o <c>null</c> si no gira.</param>
        /// <remarks>
        /// <para>
        /// <b>spin gana siempre</b> — y por eso el flicker de "hover durante el giro" es
        /// imposible por construcción, no por orden de llamadas.
        /// </para>
        /// <para>
        /// <b>blocked cae en Front</b> y no en <see cref="DiceShapeRole.Selected"/> aunque el
        /// dado esté holdeado: el bloqueo ya se lee por el tint gris y el candado, y
        /// <c>DiceSlotView.SetBlocked</c> viene documentando desde siempre que el estado
        /// bloqueado pisa el visual de hold. Un dado inerte no debe verse elegido.
        /// </para>
        /// </remarks>
        public static DiceShapeRole Resolve(bool blocked, bool held, bool hovered, DiceShapeRole? spin)
        {
            if (spin.HasValue) return spin.Value;
            if (blocked) return DiceShapeRole.Front;
            if (held) return DiceShapeRole.Selected;
            if (hovered) return DiceShapeRole.Hover;
            return DiceShapeRole.Front;
        }
    }
}
