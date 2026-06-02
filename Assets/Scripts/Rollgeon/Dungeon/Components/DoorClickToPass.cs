using Patterns;
using Rollgeon.Phase;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Pass-door por click directo en la puerta. Sólo activo en fase de Exploración.
    /// Reemplaza el botón "Pass door" del HUD: el usuario clickea la puerta y pasa
    /// a la sala vecina sin chequeo de adyacencia.
    /// </summary>
    /// <remarks>
    /// En Combat este componente queda inerte — la acción "Force Door" (con su
    /// tirada de dados y costo de energía) sigue siendo la única vía de cruzar
    /// puertas durante encuentros.
    /// Patrón de raycast = el mismo que <see cref="UI.Tooltips.WorldTooltipTrigger"/>:
    /// chequea IsPointerOverGameObject + Physics.RaycastAll y dispara si pega a
    /// este collider o un hijo (cubre meshOpen/meshClosed sin necesidad de un
    /// handler por mesh).
    /// </remarks>
    [RequireComponent(typeof(DoorController))]
    [AddComponentMenu("Rollgeon/Dungeon/Door Click To Pass")]
    public sealed class DoorClickToPass : MonoBehaviour
    {
        [Tooltip("Cámara usada para raycast. Null = Camera.main en runtime.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Distancia máxima del raycast al cursor en world units.")]
        [SerializeField] private float _raycastDistance = 100f;

        private DoorController _door;

        private void Awake()
        {
            _door = GetComponent<DoorController>();
        }

        private void Update()
        {
            if (_door == null) return;

            // Gate por fase: solo Exploración. En combate la acción de cruzar puertas
            // es Force Door (con tirada), no se puede pasar por click.
            if (!ServiceLocator.TryGetService<IPhaseService>(out var phase)
                || phase == null
                || phase.CurrentBase != GamePhase.Exploration)
                return;

            // La puerta tiene que estar atravesable: no tapiada, no locked.
            // El único estado válido para click-to-pass es Open.
            if (_door.CurrentState != DoorVisualState.Open) return;

            if (!MouseLeftPressedThisFrame()) return;

            // Si el click cae sobre UI (HUD, panel), no procesar el mundo.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            if (!TryGetMouseScreenPos(out var mouseScreen)) return;
            if (!RaycastHitsMe(cam, mouseScreen)) return;

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null)
            {
                Debug.LogWarning("[DoorClickToPass] IDungeonService no registrado — no-op.", this);
                return;
            }

            Debug.Log($"[DoorClickToPass] Click directo en puerta dir={_door.Direction} — EnterRoomByDoor");
            dungeon.EnterRoomByDoor(_door.Direction);
        }

        // Pixel-art pipeline: la cámara renderiza a un RT chiquito, así que
        // pixelWidth/Height ≠ Screen.width/Height. Escalamos el mouse pos al
        // viewport interno de la cámara antes del ScreenPointToRay. Mismo fix
        // que TileClickHandler / WorldTooltipTrigger usan para sus raycasts.
        private bool RaycastHitsMe(Camera cam, Vector2 mouseScreen)
        {
            var rtPos = new Vector2(
                mouseScreen.x / Screen.width  * cam.pixelWidth,
                mouseScreen.y / Screen.height * cam.pixelHeight);
            var ray = cam.ScreenPointToRay(rtPos);
            var hits = Physics.RaycastAll(ray, _raycastDistance);
            for (int i = 0; i < hits.Length; i++)
            {
                var hitGo = hits[i].collider != null ? hits[i].collider.gameObject : null;
                if (hitGo == null) continue;
                if (hitGo == gameObject) return true;
                if (hitGo.transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        private static bool TryGetMouseScreenPos(out Vector2 pos)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pos = Mouse.current.position.ReadValue();
                return true;
            }
            pos = Vector2.zero;
            return false;
#else
            pos = Input.mousePosition;
            return true;
#endif
        }

        private static bool MouseLeftPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }
}
