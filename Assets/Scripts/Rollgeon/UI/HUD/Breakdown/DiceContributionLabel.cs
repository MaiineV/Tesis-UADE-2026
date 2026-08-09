using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// El "+N" bajo un dado: lo que ese dado realmente suma al combo (cara + bonos
    /// aditivos de sus encantamientos). Solo visible en dados contribuyentes.
    /// </summary>
    public sealed class DiceContributionLabel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;

        public RectTransform Anchor => (RectTransform)transform;

        public int Amount { get; private set; }

        public void Show(int amount)
        {
            Amount = amount;
            if (_label != null) _label.text = "+" + amount;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            Amount = 0;
            gameObject.SetActive(false);
        }
    }
}
