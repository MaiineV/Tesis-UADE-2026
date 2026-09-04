using Rollgeon.Entities.Visuals;
using UnityEngine;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Traduce la selección activa a la máscara de layers con la que <c>PawnPicker</c>
    /// busca pawns bajo el cursor. Es lo que hace contextual al raycast de targeting: un
    /// movimiento no ve a ninguna entidad (apuntar al modelo de un enemigo cae al piso que
    /// hay debajo del cursor), un ataque no ve al héroe pero sí a enemigos y props.
    /// </summary>
    public static class SelectionPickMask
    {
        /// <summary>Sin contexto: todo lo raycasteable, el comportamiento previo a la máscara.</summary>
        public const int Unfiltered = Physics.DefaultRaycastLayers;

        /// <summary>Ningún pawn cuenta: el pick va directo al plano del piso.</summary>
        public const int None = 0;

        public static int For(SelectionSettings settings)
        {
            if (settings == null) return Unfiltered;

            switch (settings.SlotState)
            {
                case SlotState.Occupied:
                case SlotState.Both:
                    return ForOccupants(settings.EntityFilter);
                default:
                    // Empty (movimiento) y Self: ningún cuerpo puede capturar el rayo.
                    return None;
            }
        }

        // Cada bit del filtro mapea a la layer del pawn que lo satisface. Los props
        // (cofres, bombas) viven en Entity junto con los enemigos.
        private static int ForOccupants(EntityFilterMask filter)
        {
            int mask = None;
            if ((filter & (EntityFilterMask.Enemies | EntityFilterMask.Neutrals | EntityFilterMask.Props)) != 0)
                mask |= PawnLayers.EntityMask;
            if ((filter & (EntityFilterMask.Player | EntityFilterMask.Allies)) != 0)
                mask |= PawnLayers.PlayerMask;
            return mask;
        }
    }
}
