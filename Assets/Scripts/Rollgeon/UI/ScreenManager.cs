using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.UI
{
    /// <summary>
    /// Implementacion concreta plain C# de <see cref="IScreenManager"/>. Mantiene indices
    /// <c>Type → IBaseScreen</c> y <c>string → IBaseScreen</c>, y un <c>Stack&lt;IBaseScreen&gt;</c>
    /// para el orden push/pop. Plan §4.1.
    /// </summary>
    /// <remarks>
    /// No es MonoBehaviour. El <see cref="ScreenHost"/> de cada escena instancia una nueva
    /// <see cref="ScreenManager"/> y la registra en <c>ServiceLocator</c>.
    /// </remarks>
    public class ScreenManager : IScreenManager
    {
        private const string LogPrefix = "[ScreenManager] ";

        private readonly Dictionary<Type, IBaseScreen> _byType = new Dictionary<Type, IBaseScreen>();
        private readonly Dictionary<string, IBaseScreen> _byStringId = new Dictionary<string, IBaseScreen>();
        private readonly Stack<IBaseScreen> _stack = new Stack<IBaseScreen>();

        // Paralelo a _stack: marca si cada entry se pusheo como overlay (no-destructivo).
        // Un overlay NO desactiva el screen de atras — se apila encima dejandolo vivo y
        // bindeado (ver PushInternal). Necesario para que Pause no rompa flujos que corren
        // fuera del tick de la screen tapada (ej. seleccion de target en combate).
        private readonly Stack<bool> _isOverlayStack = new Stack<bool>();

        /// <inheritdoc/>
        public IBaseScreen Current => _stack.Count > 0 ? _stack.Peek() : null;

        /// <inheritdoc/>
        public void RegisterScreen(IBaseScreen screen)
        {
            if (screen == null)
            {
                Debug.LogWarning(LogPrefix + "RegisterScreen called with null screen.");
                return;
            }

            var type = screen.GetType();
            _byType[type] = screen;

            var id = screen.ScreenStringId;
            if (!string.IsNullOrEmpty(id))
            {
                _byStringId[id] = screen;
            }
        }

        /// <inheritdoc/>
        public void UnregisterScreen(IBaseScreen screen)
        {
            if (screen == null) return;

            _byType.Remove(screen.GetType());
            var id = screen.ScreenStringId;
            if (!string.IsNullOrEmpty(id))
            {
                _byStringId.Remove(id);
            }
        }

        /// <inheritdoc/>
        public void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen
        {
            if (!_byType.TryGetValue(typeof(TScreen), out var screen))
            {
                Debug.LogWarning(
                    $"{LogPrefix}'{typeof(TScreen).Name}' no esta registrada. " +
                    "Verificar que la screen sea hija del ScreenHost en la escena.");
                return;
            }

            PushInternal(screen, payload, asOverlay: false);
        }

        /// <inheritdoc/>
        public void PushByStringId(string screenId, IScreenPayload payload = null)
        {
            if (string.IsNullOrEmpty(screenId))
            {
                Debug.LogWarning(LogPrefix + "PushByStringId called with null/empty id.");
                return;
            }

            if (!_byStringId.TryGetValue(screenId, out var screen))
            {
                Debug.LogWarning(
                    $"{LogPrefix}'{screenId}' no esta registrada. " +
                    "Fallback graceful: el stack no cambia. " +
                    "Verificar que la screen exista en la escena (T98 puede no haber mergeado todavia).");
                return;
            }

            PushInternal(screen, payload, asOverlay: false);
        }

        /// <inheritdoc/>
        public void PopCurrent()
        {
            if (_stack.Count == 0)
            {
                Debug.LogWarning(LogPrefix + "PopCurrent con stack vacio — no-op.");
                return;
            }

            var popped = _stack.Pop();
            var poppedWasOverlay = _isOverlayStack.Pop();
            popped._Internal_OnLoseFocus();
            popped._Internal_OnPopped();
            popped._Internal_SetVisible(false);

            if (_stack.Count > 0)
            {
                var newTop = _stack.Peek();
                // Si el popped era un overlay, el screen de abajo nunca se oculto: no lo
                // re-activamos para no disparar un ciclo OnDisable/OnEnable espurio (que
                // reseteaira bindings del HUD contra estado de gameplay ya en curso).
                if (!poppedWasOverlay)
                {
                    newTop._Internal_SetVisible(true);
                }
                newTop._Internal_OnGainFocus();
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Overlay no-destructivo: el screen de atras queda ACTIVO y bindeado (solo pierde
        /// foco). El bloqueo de input durante el overlay lo aporta un fondo full-screen con
        /// raycastTarget en la propia screen del overlay (ej. PauseMenuOverlay). Ver §17.D.
        /// </remarks>
        public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen
        {
            if (!_byType.TryGetValue(typeof(TScreen), out var screen))
            {
                Debug.LogWarning(
                    $"{LogPrefix}'{typeof(TScreen).Name}' no esta registrada. " +
                    "Verificar que la screen sea hija del ScreenHost en la escena.");
                return;
            }

            PushInternal(screen, payload, asOverlay: true);
        }

        /// <inheritdoc/>
        public void PopOverlay() => PopCurrent();

        // --------------- internals ---------------

        private void PushInternal(IBaseScreen screen, IScreenPayload payload, bool asOverlay)
        {
            if (_stack.Count > 0)
            {
                var previousTop = _stack.Peek();
                previousTop._Internal_OnLoseFocus();
                // Los overlays NO desactivan el screen de atras: se apilan encima dejandolo
                // vivo (sin churn de OnDisable/OnEnable). Solo los push destructivos ocultan.
                if (!asOverlay)
                {
                    previousTop._Internal_SetVisible(false);
                }
            }

            _stack.Push(screen);
            _isOverlayStack.Push(asOverlay);
            screen._Internal_SetVisible(true);
            screen._Internal_OnPushed(payload);
            screen._Internal_OnGainFocus();
        }
    }
}
