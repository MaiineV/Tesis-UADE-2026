using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Minimal sub-view for displaying a single die type in the build selection
    /// screen. Uses <c>string</c> instead of <c>DiceType</c> enum because
    /// <c>DiceBagSO</c> doesn't exist yet. UI#0013a.
    /// </summary>
    public class DiceSlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _diceLabel;

        public void Bind(string diceTypeName)
        {
            if (_diceLabel != null)
                _diceLabel.text = diceTypeName;
        }
    }
}
