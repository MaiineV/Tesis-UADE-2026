using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI
{
    /// <summary>
    /// Clase base abstracta para todas las screens. MonoBehaviour que implementa
    /// <see cref="IBaseScreen"/> routeando los hooks internos a virtuales protegidos
    /// que las screens concretas overridean.
    /// Plan §4.3 / TECHNICAL.md §17.D.3 (con nombres <c>OnPushed/OnPopped/OnGainFocus/OnLoseFocus</c>
    /// segun brief — ver plan §10 R3 para la divergencia).
    /// </summary>
    /// <remarks>
    /// No registra automaticamente en el <see cref="IScreenManager"/>: ese trabajo es del
    /// <see cref="ScreenHost"/> de escena. No toca <c>gameObject.SetActive</c> en <c>Awake</c>
    /// — la visibilidad inicial la decide el <see cref="ScreenHost"/>.
    /// </remarks>
    public abstract class BaseScreen : MonoBehaviour, IBaseScreen
    {
        [Title("Base Screen")]
        [Tooltip("Override opcional del ScreenStringId. Vacio = usar GetType().Name. " +
                 "Uselo para tener ids estables (ej. 'MainMenu' en vez de 'MainMenuScreen').")]
        [SerializeField]
        private string _screenStringIdOverride = string.Empty;

        /// <inheritdoc/>
        public virtual string ScreenStringId =>
            string.IsNullOrEmpty(_screenStringIdOverride) ? GetType().Name : _screenStringIdOverride;

        /// <summary>
        /// Hook: se invoca cuando la screen entra al stack. Default vacio.
        /// </summary>
        protected virtual void OnPushed(IScreenPayload payload) { }

        /// <summary>
        /// Hook: se invoca cuando la screen sale del stack. Default vacio.
        /// </summary>
        protected virtual void OnPopped() { }

        /// <summary>
        /// Hook: se invoca cuando la screen pasa a ser top del stack (primera vez al pushear,
        /// o al poppear otra que estaba encima). Default vacio.
        /// </summary>
        protected virtual void OnGainFocus() { }

        /// <summary>
        /// Hook: se invoca cuando otra screen se pushea encima y esta queda oculta. Default vacio.
        /// </summary>
        protected virtual void OnLoseFocus() { }

        // --- IBaseScreen explicit forwarders (solo el ScreenManager los invoca; plan §4.2) ---

        void IBaseScreen._Internal_OnPushed(IScreenPayload payload) => OnPushed(payload);
        void IBaseScreen._Internal_OnPopped() => OnPopped();
        void IBaseScreen._Internal_OnGainFocus() => OnGainFocus();
        void IBaseScreen._Internal_OnLoseFocus() => OnLoseFocus();
        void IBaseScreen._Internal_SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
