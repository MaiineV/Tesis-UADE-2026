using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// La maldición de un jefe sobre el jugador — lo que el panel anuncia en su bloque
    /// PLAYER CURSE. Data pura: el efecto real lo aplica el behavior del jefe; esto sólo dice
    /// cómo se llama y qué hace, para que el jugador lo lea desde el turno 1 en vez de
    /// descubrirlo cuando ya le trabó un dado.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Boss Curse", fileName = "BC_Boss")]
    public class BossCurseSO : ScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Key de localización: nombre y efecto salen de '<CurseId>.name' / '<CurseId>.desc' " +
                 "en la tabla Content. Puede reusar la key de un estado ya sembrado " +
                 "(ej. 'status.dice_block').")]
        public string CurseId;

        [Tooltip("Nombre de autor — fallback si la tabla no tiene la key.")]
        public string DisplayName;

        [TextArea]
        [Tooltip("Efecto en una línea — fallback si la tabla no tiene la key.")]
        public string Description;

        [Title("HUD")]
        [Tooltip("Ícono de la tarjeta. Vacío = se resuelve CurseId contra el StatusIconCatalog.")]
        public Sprite Icon;
    }
}
