using System;
using System.Collections.Generic;
using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Panel que entra deslizando desde la izquierda y se cierra por tres caminos: el
    /// mismo ícono que lo abrió, Esc, o click afuera. NO pausa el juego — los paneles que
    /// lo usan son de consulta y tienen que poder quedarse abiertos mientras se juega.
    /// </summary>
    /// <remarks>
    /// Solo maneja el gesto. El CONTENIDO lo pone otro componente, que se engancha a
    /// <see cref="Opened"/> para repoblarse — así el contenido no necesita escuchar cambios
    /// del mundo mientras está cerrado. Lo comparten el contrato y la bolsa de dados.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Sliding Drawer")]
    public class SlidingDrawer : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _panel;

        [SerializeField, Required]
        [Tooltip("Captura el click afuera para cerrar. Se apaga junto con el panel.")]
        private Button _backdrop;

        [Title("Animación")]
        [SerializeField, Tooltip("X del panel cerrado — fuera de pantalla por la izquierda.")]
        private float _closedX = -1000f;

        [SerializeField, Tooltip("X del panel abierto.")]
        private float _openX = 0f;

        [SerializeField] private float _slideSeconds = 0.28f;
        [SerializeField] private Ease _slideEase = Ease.OutCubic;

        [ShowInInspector, ReadOnly] private bool _isOpen;

        private Tween _slide;

        // Registro de los drawers vivos, para que abrir uno cierre el resto. Es estático
        // porque cada panel lo instala su propio tool, sin saber de los demás: cablear
        // "cerrá a estos otros" obligaría a que cada installer conociera a todos.
        private static readonly List<SlidingDrawer> Live = new();

        /// <summary>Se dispara al abrir, ANTES de animar — el contenido se repuebla acá.</summary>
        public event Action Opened;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            // El registro va en Awake y no en OnEnable porque OnEnable no corre en EditMode
            // y dejaba la exclusión sin cubrir por tests. Las entradas muertas las poda
            // CloseOthers, así que un OnDestroy que no llegue a correr tampoco rompe.
            if (!Live.Contains(this)) Live.Add(this);

            if (_backdrop != null) _backdrop.onClick.AddListener(Close);
            ApplyClosedInstantly();
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_backdrop != null) _backdrop.onClick.RemoveListener(Close);
            Opened = null;
            if (_slide.isAlive) _slide.Stop();
        }

        private void OnDisable()
        {
            // Un slide en vuelo cuando el HUD se apaga dejaría el panel a mitad de camino
            // y a PrimeTween tweeneando un target destruido en el teardown de escena.
            if (_slide.isAlive) _slide.Complete();
        }

        private void Update()
        {
            if (!_isOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        }

        /// <summary>Lo llama el ícono que abre este panel.</summary>
        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            CloseOthers();
            Opened?.Invoke();

            if (_backdrop != null) _backdrop.gameObject.SetActive(true);
            Slide(_openX);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            // El backdrop se va YA, no al terminar el tween: mientras el panel sale, el
            // jugador ya tiene que poder clickear el tablero.
            if (_backdrop != null) _backdrop.gameObject.SetActive(false);
            Slide(_closedX);
        }

        /// <summary>
        /// Cierra los demás paneles. Se llama DESPUÉS de marcarse abierto, así que el
        /// <see cref="Close"/> del otro no puede rebotar contra este.
        /// </summary>
        private void CloseOthers()
        {
            // Hacia atrás para poder podar los destruidos en la misma pasada: un panel que
            // se fue con su escena queda como fake-null y nadie más lo saca de la lista.
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var other = Live[i];
                if (other == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }
                if (other == this || !other._isOpen) continue;
                other.Close();
            }
        }

        private void Slide(float targetX)
        {
            if (_panel == null) return;
            if (_slide.isAlive) _slide.Stop();

            if (!Application.isPlaying || DiceUiMotionPrefs.ReducedMotion || _slideSeconds <= 0f)
            {
                SetPanelX(targetX);
                return;
            }

            _slide = Tween.UIAnchoredPositionX(_panel, targetX, _slideSeconds, _slideEase,
                useUnscaledTime: true);
        }

        private void ApplyClosedInstantly()
        {
            _isOpen = false;
            if (_backdrop != null) _backdrop.gameObject.SetActive(false);
            SetPanelX(_closedX);
        }

        private void SetPanelX(float x)
        {
            if (_panel == null) return;
            var pos = _panel.anchoredPosition;
            pos.x = x;
            _panel.anchoredPosition = pos;
        }
    }
}
