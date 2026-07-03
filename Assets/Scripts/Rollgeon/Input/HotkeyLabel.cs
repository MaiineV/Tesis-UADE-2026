using Patterns;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.Input
{
    /// <summary>
    /// Pinta en un label TMP la tecla asignada a un <see cref="GameplayHotkey"/>,
    /// resuelta del binding vigente via <see cref="IGameplayHotkeyService"/>. Al
    /// derivarse del asset, si rebindeás o sumás gamepad el texto se actualiza sin
    /// tocar la escena. Poné este componente en cada botón, con el label del lado
    /// OPUESTO al costo de energía.
    /// </summary>
    [AddComponentMenu("Rollgeon/Input/Hotkey Label")]
    public sealed class HotkeyLabel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Qué acción representa este label.")]
        private GameplayHotkey _hotkey;

        [Required("Arrastrar el TMP donde se pinta la tecla.")]
        [SerializeField]
        private TextMeshProUGUI _label;

        [SerializeField]
        [Tooltip("Formato del texto. Default '{0}'. Ej '[{0}]', '({0})'.")]
        private string _format = "{0}";

        [SerializeField]
        [Tooltip("Si el hotkey no resuelve (asset sin binding), ocultar el GameObject " +
                 "del label en vez de dejarlo vacío.")]
        private bool _hideWhenEmpty = true;

        private void OnEnable() => Refresh();

        // Start corre después de todos los Awake → el GameplayHotkeyService ya se
        // registró aunque este botón se haya activado en el mismo frame.
        private void Start() => Refresh();

        /// <summary>Re-lee la tecla del service y repinta. Público para tooling / rebind UI.</summary>
        public void Refresh()
        {
            if (_label == null) return;

            string hint = ServiceLocator.TryGetService<IGameplayHotkeyService>(out var svc) && svc != null
                ? svc.GetKeyHint(_hotkey)
                : string.Empty;

            bool empty = string.IsNullOrEmpty(hint);
            _label.text = empty ? string.Empty : string.Format(_format, hint);

            if (_hideWhenEmpty && _label.gameObject.activeSelf == empty)
                _label.gameObject.SetActive(!empty);
        }
    }
}
