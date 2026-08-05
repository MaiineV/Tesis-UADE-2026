using System;
using UnityEngine.InputSystem;

namespace Rollgeon.Input
{
    /// <summary>
    /// Punto único para que las views de HUD se enganchen a los hotkeys de gameplay
    /// sin conocer el <see cref="InputActionAsset"/> ni el nombre del map. Se resuelve
    /// via <see cref="Patterns.ServiceLocator"/>. La implementación
    /// (<see cref="GameplayHotkeyService"/>) mantiene el map "Gameplay" habilitado
    /// mientras vive.
    /// </summary>
    /// <remarks>
    /// El contrato es "hotkey = click del botón": las views se suscriben y, en el
    /// handler, invocan el <c>onClick</c> de su botón SOLO si está interactable. Así
    /// el teclado hereda exactamente el mismo gating que el mouse, sin duplicar reglas.
    /// </remarks>
    public interface IGameplayHotkeyService
    {
        /// <summary>True si el asset + map "Gameplay" resolvieron OK. Si es false,
        /// Subscribe/Unsubscribe son no-op y <see cref="GetKeyHint"/> devuelve "".</summary>
        bool IsReady { get; }

        /// <summary>Engancha <paramref name="handler"/> al <c>performed</c> del hotkey.
        /// No-op si el hotkey no existe en el asset o el service no está listo.</summary>
        void Subscribe(GameplayHotkey hotkey, Action<InputAction.CallbackContext> handler);

        /// <summary>Desengancha (simétrico a <see cref="Subscribe"/>).</summary>
        void Unsubscribe(GameplayHotkey hotkey, Action<InputAction.CallbackContext> handler);

        /// <summary>
        /// Marca el input del frame actual como consumido por un hotkey de mayor prioridad
        /// (ej. Confirm), para que otro hotkey en la MISMA tecla (ej. EndTurn, que comparte
        /// Space) no lo re-procese en el mismo press. Ver <see cref="WasFrameConsumed"/>.
        /// </summary>
        void ConsumeFrame();

        /// <summary>True si un hotkey ya consumió el input en el frame actual.</summary>
        bool WasFrameConsumed();

        /// <summary>Texto de la tecla asignada (ej. "Q", "Space") derivado del binding
        /// vigente. Cambia solo si se rebindea / agrega gamepad. "" si no resuelve.</summary>
        string GetKeyHint(GameplayHotkey hotkey);

        /// <summary>
        /// Suprime (o restaura) el map "Gameplay" completo. BUG-019: el overlay del
        /// tutorial lo usa durante los pasos "click para continuar" — el dim bloquea
        /// pointer pero el teclado entraba igual por InputSystem. No afecta la pausa
        /// (PauseHotkey pollea <c>Keyboard.current</c> directo, no usa el map).
        /// </summary>
        void SetSuppressed(bool suppressed);
    }
}
