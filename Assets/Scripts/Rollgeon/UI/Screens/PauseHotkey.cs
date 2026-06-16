using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Abre/cierra el <see cref="PauseMenuOverlay"/> con una tecla (Escape por
    /// defecto). Complementa el botón de pausa del HUD
    /// (<see cref="Rollgeon.UI.HUD.RoomNavigationView"/>): la tecla abre el overlay
    /// cuando no está al top del stack, y resume (lo popea) cuando sí lo está.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive en un GameObject siempre-activo de <c>02_Gameplay</c> (el
    /// <see cref="ScreenHost"/> de la escena). NO puede vivir en el HUD ni en el
    /// propio <see cref="PauseMenuOverlay"/>: el <see cref="ScreenManager"/> hace
    /// <c>SetActive(false)</c> sobre la screen tapada al pushear, así que esos
    /// objetos quedan desactivados justo cuando hay que cerrar el pause y su
    /// <c>Update</c> no correría. Scope por escena a propósito — al existir solo en
    /// la escena de gameplay, la tecla queda inerte en <c>01_MainMenu</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Pause Hotkey")]
    public class PauseHotkey : MonoBehaviour
    {
        private const string LogPrefix = "[PauseHotkey] ";

        [Title("Pause Hotkey")]
        [Tooltip("Tecla que abre/cierra el pause menu. Default Escape.")]
        [SerializeField] private Key _toggleKey = Key.Escape;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[_toggleKey].wasPressedThisFrame) return;

            Toggle();
        }

        private void Toggle()
        {
            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens))
            {
                Debug.LogWarning(LogPrefix + "IScreenManager no registrado — no se puede togglear el pause.", this);
                return;
            }

            if (screens.Current is PauseMenuOverlay)
            {
                // Pause al top → resume. PauseMenuOverlay.OnPopped popea el phase overlay.
                screens.PopOverlay();
            }
            else
            {
                // Sin pausar → abrir. Mismo path que RoomNavigationView.OnPauseClicked.
                screens.PushOverlay<PauseMenuOverlay>();
            }
        }
    }
}
