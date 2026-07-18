using Patterns;
using Rollgeon.ActionRolls;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Swappea el sprite del tablero de dados (el rectángulo de <c>DiceZoneView</c> en
    /// <c>Canvas_ActionRoll</c>) según el <see cref="DiceBoardType"/> de la tirada en curso.
    /// El swap se aplica sobre el <see cref="Image"/> ya existente — nunca crea uno nuevo —
    /// para que su posición/tamaño se editen a mano en el prefab.
    /// </summary>
    /// <remarks>
    /// Dos fuentes alimentan el tipo, sin solaparse (solo un flujo de dados corre a la vez):
    /// <list type="bullet">
    ///   <item><b>Combate</b> (ataque/defensa): push desde
    ///   <c>CombatHUDView.SetBehaviorForFormula</c> vía <see cref="ApplyBoardType"/>.</item>
    ///   <item><b>Exploración</b> (Heal / Forzar Puerta): pull de
    ///   <see cref="IActionRollService.CurrentSpec"/> al cambiar la fase.</item>
    /// </list>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Board Skin View")]
    public sealed class DiceBoardSkinView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("El Image del rectángulo negro existente en DiceZoneView. Se le swappea el sprite.")]
        private Image _boardImage;

        [SerializeField]
        [Tooltip("Config con el sprite de cada DiceBoardType. Sin catalog, el tablero queda como está.")]
        private DiceBoardSkinCatalogSO _catalog;

        private IActionRollService _actionRoll;
        private System.Action<ActionRollPhase> _onPhase;

        private void OnEnable()
        {
            // El IActionRollService es Run-scoped: si aún no está registrado, Update lo
            // retrieva. Mismo patrón que ActionRollExplorationVisibility.
            TrySubscribeToActionRollService();
            // Arrancamos en Default para que el tablero muestre su skin base en reposo.
            ApplyBoardType(DiceBoardType.Default);
        }

        private void Update()
        {
            if (_actionRoll != null) return;
            TrySubscribeToActionRollService();
        }

        private void TrySubscribeToActionRollService()
        {
            if (_actionRoll != null) return;
            if (!ServiceLocator.TryGetService<IActionRollService>(out _actionRoll) || _actionRoll == null)
            {
                _actionRoll = null;
                return;
            }
            _onPhase = _ => RefreshFromActionRoll();
            _actionRoll.OnPhaseChanged += _onPhase;
            RefreshFromActionRoll();
        }

        private void OnDisable()
        {
            if (_actionRoll != null && _onPhase != null)
            {
                _actionRoll.OnPhaseChanged -= _onPhase;
                _onPhase = null;
            }
            _actionRoll = null;
        }

        private void RefreshFromActionRoll()
        {
            // Action roll activo (Heal/ForceDoor) manda su tipo; al resolver, volvemos a Default.
            ApplyBoardType(_actionRoll != null && _actionRoll.IsActive
                ? _actionRoll.CurrentSpec.BoardType
                : DiceBoardType.Default);
        }

        /// <summary>
        /// Aplica el skin de <paramref name="type"/> sobre el Image existente. Si no hay
        /// catalog o el tipo (ni Default) tiene skin, deja el Image como está — degradación
        /// segura al look actual, nunca lo borra.
        /// </summary>
        public void ApplyBoardType(DiceBoardType type)
        {
            if (_boardImage == null || _catalog == null) return;
            if (!_catalog.TryGet(type, out var skin)) return;

            _boardImage.sprite = skin.Sprite;
            _boardImage.color = skin.Tint;
            _boardImage.type = skin.ImageType;
        }
    }
}
