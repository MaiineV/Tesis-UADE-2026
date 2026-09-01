using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Casillas recorridas por el jugador en el turno actual × <see cref="PerTileAmount"/>
    /// (<see cref="IPlayerTurnStateService"/>). Para "Corredor Incansable": el ×5 vive acá
    /// porque <c>EffAddComboBonus</c> no escala su reader (mismo patrón que
    /// <c>ReadCurrentGoldSqrtScaled.Factor</c>).
    /// </summary>
    /// <remarks>
    /// Side-effect free ⇒ seguro para <c>GetComboDamageBonusPreview</c> (el preview del
    /// HUD queda "vivo" gratis: se actualiza al moverse). Sin servicio registrado → 0.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadTilesMovedThisTurn : EffectIntReader
    {
        [MinValue(0)]
        [Tooltip("Cuánto vale cada casilla recorrida. Corredor Incansable (GDD): 5.")]
        public int PerTileAmount = 1;

        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return 0;
            return state.TilesMovedThisTurn * PerTileAmount;
        }
    }
}
