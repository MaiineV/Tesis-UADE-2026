using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view placeholder del Combat HUD que expone los anchor points para la zona
    /// de dados (roll area + hold area + 5 dice slots). Plan §3.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No bindea eventos</b>. El render real de los dados lo hace T97c; este view
    /// solo provee los <see cref="RectTransform"/>s para que el dice-view se ancle
    /// encima.
    /// </para>
    /// <para>
    /// Se incluye en este PR porque el HUD del FP debe mostrar las zonas visibles
    /// (aunque vacias), para que el designer vea el layout.
    /// </para>
    /// </remarks>
    // [STUB T97c] — el dice rendering real se ancla aca cuando T97c aterrice.
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Zone View")]
    public class DiceZoneView : MonoBehaviour
    {
        [Title("Dice Zone — Anchors")]
        [Required("Arrastrar el RectTransform de la roll area (donde se 'tiran' los dados).")]
        [SerializeField]
        private RectTransform _rollArea;

        [Required("Arrastrar el RectTransform de la hold area (donde se holdean los dados).")]
        [SerializeField]
        private RectTransform _holdArea;

        [Title("Dice Zone — Slots")]
        [InfoBox("5 anchor children (uno por dado del combo del guerrero). Si se deja " +
                 "menos de 5, T97c usara fallback por numero disponible.")]
        [SerializeField]
        private List<RectTransform> _diceSlots = new List<RectTransform>();

        /// <summary>Anchor para la zona de tiro.</summary>
        public RectTransform GetRollArea() => _rollArea;

        /// <summary>Anchor para la zona de hold.</summary>
        public RectTransform GetHoldArea() => _holdArea;

        /// <summary>Lista readonly de los anchors de cada dado.</summary>
        public IReadOnlyList<RectTransform> GetDiceSlots() => _diceSlots;
    }
}
