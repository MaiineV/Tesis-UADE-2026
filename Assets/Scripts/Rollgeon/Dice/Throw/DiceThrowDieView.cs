using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Visual de un dado volador del throw 2D. Es un PREFAB autorado
    /// (<c>Assets/Prefabs/UI/DiceThrowDie.prefab</c>) — tamaño, fondo, fuente y
    /// cualquier decoración se editan ahí, no en código. El presenter solo lo
    /// instancia en el layer y le escribe la cara.
    /// </summary>
    [AddComponentMenu("Rollgeon/Dice/Dice Throw Die View")]
    public sealed class DiceThrowDieView : MonoBehaviour
    {
        [SerializeField, Tooltip("Cuerpo del dado: lleva la silueta del tipo.")]
        private Image _background;

        [SerializeField, Tooltip("Número de la cara.")]
        private TextMeshProUGUI _label;

        [SerializeField, Tooltip("Catálogo de siluetas. Vacío = se resuelve desde " +
                                 "Resources/Dice/DiceShapeCatalog.")]
        private DiceShapeCatalogSO _shapeCatalog;

        private DiceShapeCatalogSO _resolvedCatalog;
        private bool _catalogResolved;

        public RectTransform Rect => (RectTransform)transform;

        public Image Background => _background;

        /// <summary>Radio útil para el rebote/grab — mitad del lado mayor del rect.</summary>
        public float HalfSize => Mathf.Max(Rect.sizeDelta.x, Rect.sizeDelta.y) * 0.5f;

        public void ShowFace(int face) => _label?.SetText(face.ToString());

        public void ShowText(string text) => _label?.SetText(text);

        /// <summary>
        /// Pinta la silueta del tipo de dado; el número se dibuja encima. Mismo catálogo que
        /// <c>DiceSlotView</c> para que el 2D no se despegue del modo classic.
        /// </summary>
        public void SetDiceType(DiceType type)
        {
            if (_background == null) return;
            var catalog = ResolveCatalog();
            _background.sprite = catalog != null ? catalog.GetShape(type) : null;
        }

        private DiceShapeCatalogSO ResolveCatalog()
        {
            if (_catalogResolved) return _resolvedCatalog;
            _resolvedCatalog = DiceShapeCatalogSO.Resolve(_shapeCatalog);
            _catalogResolved = true;
            return _resolvedCatalog;
        }
    }
}
