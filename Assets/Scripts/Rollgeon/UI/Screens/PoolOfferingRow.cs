using System;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Una fila del pool en <see cref="BuildSelectionScreen"/>: un tipo de dado
    /// con +/- para agregarlo o sacarlo de la bolsa que el jugador esta armando.
    /// </summary>
    /// <remarks>
    /// La screen orquesta la logica (no la fila). La fila solo muestra el tipo,
    /// el contador "X / max" y emite eventos de click. <see cref="Refresh"/>
    /// recibe el conteo actual y un flag de capacidad de la bolsa para apagar
    /// el +/- cuando corresponde.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Pool Offering Row")]
    public class PoolOfferingRow : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Optional] private TextMeshProUGUI _typeLabel;
        [SerializeField, Optional] private TextMeshProUGUI _countLabel;
        [SerializeField, Optional] private Button _addButton;
        [SerializeField, Optional] private Button _removeButton;

        public DiceType Type { get; private set; }
        public int MaxInBag { get; private set; }

        public event Action<DiceType> OnAddRequested;
        public event Action<DiceType> OnRemoveRequested;

        public void Bind(DiceType type, int maxInBag)
        {
            Type = type;
            MaxInBag = maxInBag;

            if (_typeLabel != null) _typeLabel.text = type.ToString();
            if (_addButton != null) _addButton.onClick.AddListener(HandleAddClicked);
            if (_removeButton != null) _removeButton.onClick.AddListener(HandleRemoveClicked);

            Refresh(0, bagHasRoom: true);
        }

        public void Unbind()
        {
            if (_addButton != null) _addButton.onClick.RemoveListener(HandleAddClicked);
            if (_removeButton != null) _removeButton.onClick.RemoveListener(HandleRemoveClicked);
            OnAddRequested = null;
            OnRemoveRequested = null;
        }

        /// <summary>
        /// Refresca el contador y la interactividad. <paramref name="bagHasRoom"/>
        /// apaga el + cuando la bolsa ya alcanzo <c>RequiredBagSize</c>.
        /// </summary>
        public void Refresh(int currentCount, bool bagHasRoom)
        {
            if (_countLabel != null) _countLabel.text = $"{currentCount} / {MaxInBag}";
            if (_addButton != null) _addButton.interactable = bagHasRoom && currentCount < MaxInBag;
            if (_removeButton != null) _removeButton.interactable = currentCount > 0;
        }

        private void HandleAddClicked() => OnAddRequested?.Invoke(Type);
        private void HandleRemoveClicked() => OnRemoveRequested?.Invoke(Type);
    }
}
