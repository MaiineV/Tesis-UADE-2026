using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Arte y manos de ejemplo del drawer de contrato. Las manos son de AUTORÍA, no se
    /// derivan del combo: son el ejemplo que mejor comunica la regla, y eso lo decide
    /// diseño. Cambiarlas es tocar este asset — no hace falta recompilar.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Contract Sheet UI Settings", fileName = "ContractSheetUiSettings")]
    public class ContractSheetUiSettingsSO : ScriptableObject
    {
        [Title("Dados")]
        [InfoBox("Índice 0 = cara 1 … índice 5 = cara 6 (sprites d6_0..d6_5).")]
        [SerializeField] private Sprite[] _faces = new Sprite[6];

        [SerializeField, Tooltip("Marco que llevan los dados que FORMAN el combo (d6_6).")]
        private Sprite _highlightFrame;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Opacidad de los dados que no forman el combo.")]
        private float _dimmedAlpha = 0.45f;

        [Title("Manos de ejemplo")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [SerializeField] private List<ComboExampleHand> _hands = new List<ComboExampleHand>();

        public float DimmedAlpha => _dimmedAlpha;
        public Sprite HighlightFrame => _highlightFrame;
        public IReadOnlyList<ComboExampleHand> Hands => _hands;

        /// <summary>Sprite de una cara (1..6). <c>null</c> fuera de rango o sin autorar.</summary>
        public Sprite FaceSprite(int face)
        {
            int index = face - 1;
            if (_faces == null || index < 0 || index >= _faces.Length) return null;
            return _faces[index];
        }

        /// <summary>Mano de ejemplo de un combo, o <c>null</c> si no está autorada.</summary>
        public ComboExampleHand FindHand(string comboId)
        {
            if (string.IsNullOrEmpty(comboId) || _hands == null) return null;
            foreach (var hand in _hands)
                if (hand != null && hand.ComboId == comboId) return hand;
            return null;
        }

        /// <summary>Reemplaza las manos — lo usa el installer al sembrar los defaults.</summary>
        public void SetHands(List<ComboExampleHand> hands) => _hands = hands;

        /// <summary>Reemplaza el arte — lo usa el installer.</summary>
        public void SetArt(Sprite[] faces, Sprite highlightFrame)
        {
            _faces = faces;
            _highlightFrame = highlightFrame;
        }
    }

    /// <summary>
    /// Los 5 dados que ilustran un combo y cuáles de ellos lo forman.
    /// </summary>
    [System.Serializable]
    public class ComboExampleHand
    {
        [Tooltip("Id del combo (ej. 'combo.pair').")]
        public string ComboId;

        [Tooltip("Caras de los 5 dados, de izquierda a derecha.")]
        public int[] Faces = new int[5];

        [Tooltip("Cuáles llevan el marco. Los demás se dibujan opacos.")]
        public bool[] Highlighted = new bool[5];

        /// <summary>
        /// True si el dado en <paramref name="index"/> forma el combo. Tolera arrays de
        /// distinto largo: una mano a medio autorar no debe tirar excepción en pantalla.
        /// </summary>
        public bool IsHighlighted(int index)
            => Highlighted != null && index >= 0 && index < Highlighted.Length && Highlighted[index];

        /// <summary>Cara del dado en <paramref name="index"/>, o 0 si no está autorada.</summary>
        public int FaceAt(int index)
            => Faces != null && index >= 0 && index < Faces.Length ? Faces[index] : 0;

        /// <summary>Cantidad de dados de la mano — 5 en las autoradas.</summary>
        public int Count => Faces?.Length ?? 0;
    }
}
