using Patterns;
using Rollgeon.ActionRolls;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Swappea el sprite del tablero de dados (el rectángulo de <c>DiceZoneView</c> en
    /// <c>Canvas_ActionRoll</c>) y el del <c>DiceBoardLogo</c> según el
    /// <see cref="DiceBoardType"/> de la tirada en curso. El swap se aplica sobre los
    /// <see cref="Image"/> ya existentes — nunca crea uno nuevo — para que su
    /// posición/tamaño se editen a mano en el prefab.
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

        [SerializeField]
        [Tooltip("El Image del DiceBoardLogo. Opcional: sin ref, solo se swappea el tablero.")]
        private Image _logoImage;

        private IActionRollService _actionRoll;
        private System.Action<ActionRollPhase> _onPhase;
        private bool _hasApplied;
        private DiceBoardType _currentType;

        /// <summary>
        /// Dispara cuando el tipo APLICADO cambia — nunca en la primera aplicación (el
        /// apply de OnEnable o el pull inicial son estado, no transición) ni en
        /// re-aplicaciones del mismo tipo (OnPhaseChanged refresca en cada fase). Se
        /// invoca después de aplicar los visuales: el suscriptor lee el estado final.
        /// </summary>
        public event System.Action<DiceBoardType> BoardTypeChanged;

        /// <summary>Último tipo aplicado. Lo lee <c>DiceBoardSkinJuice</c> para retomar el idle del logo.</summary>
        public DiceBoardType CurrentType => _currentType;

        private void OnEnable()
        {
            // El IActionRollService es Run-scoped: si aún no está registrado, Update lo
            // retrieva. Mismo patrón que ActionRollExplorationVisibility.
            TrySubscribeToActionRollService();
            // Re-aplicamos el último tipo conocido (Default en el primer enable): el
            // tablero conserva su skin aunque la zona se esconda y vuelva — el skin
            // solo cambia cuando una acción pide otro tipo, nunca por reset.
            ApplyBoardType(_currentType);
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
            // Re-habilitar trata el apply de OnEnable como estado inicial (sin evento):
            // restaurar el skin con el que la zona se escondió no es una transición
            // que merezca juice.
            _hasApplied = false;
        }

        private void RefreshFromActionRoll()
        {
            // Action roll activo (Heal/ForceDoor) manda su tipo. Al resolver NO
            // volvemos a Default: el tablero se queda en su tipo actual hasta que
            // otra acción pida uno distinto (pedido de playtest 2026-07-20).
            if (_actionRoll == null || !_actionRoll.IsActive) return;
            ApplyBoardType(_actionRoll.CurrentSpec.BoardType);
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

            ApplyLogo(skin);

            bool changed = _hasApplied && _currentType != type;
            _hasApplied = true;
            _currentType = type;
            if (changed) BoardTypeChanged?.Invoke(type);
        }

        private void ApplyLogo(DiceBoardSkinEntry skin)
        {
            if (_logoImage == null) return;

            // A diferencia del board (que degrada al look actual), un tipo sin logo lo
            // ESCONDE: dejar el logo del tipo anterior mentiría sobre la tirada en curso.
            if (skin.LogoSprite == null)
            {
                _logoImage.enabled = false;
                return;
            }

            _logoImage.enabled = true;
            _logoImage.sprite = skin.LogoSprite;
            _logoImage.color = skin.LogoTint;
        }
    }
}
