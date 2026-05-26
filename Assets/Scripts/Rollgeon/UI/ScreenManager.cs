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

            PushInternal(screen, payload);
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

            PushInternal(screen, payload);
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
            popped._Internal_OnLoseFocus();
            popped._Internal_OnPopped();
            popped._Internal_SetVisible(false);

            if (_stack.Count > 0)
            {
                var newTop = _stack.Peek();
                newTop._Internal_SetVisible(true);
                newTop._Internal_OnGainFocus();
            }
        }

        /// <inheritdoc/>
        /// <remarks>MVP: alias de <see cref="Push{TScreen}"/>. Plan §10 R8.</remarks>
        public void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen
            => Push<TScreen>(payload);

        /// <inheritdoc/>
        /// <remarks>MVP: alias de <see cref="PopCurrent"/>. Plan §10 R8.</remarks>
        public void PopOverlay() => PopCurrent();

        // --------------- internals ---------------

        private void PushInternal(IBaseScreen screen, IScreenPayload payload)
        {
            if (_stack.Count > 0)
            {
                var previousTop = _stack.Peek();
                previousTop._Internal_OnLoseFocus();
                previousTop._Internal_SetVisible(false);
            }

            _stack.Push(screen);
            screen._Internal_SetVisible(true);
            screen._Internal_OnPushed(payload);
            screen._Internal_OnGainFocus();
        }
    }
}
