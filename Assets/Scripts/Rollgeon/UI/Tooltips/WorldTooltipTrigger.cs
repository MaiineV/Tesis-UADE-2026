using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.UI.HUD.Status;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rollgeon.UI.Tooltips
{
    /// <summary>Modo de activación de un <see cref="WorldTooltipTrigger"/>.</summary>
    public enum WorldTooltipMode
    {
        /// <summary>Click toggle: click muestra, click otra vez sobre el mismo objeto oculta.</summary>
        Click,
        /// <summary>Hover: cursor sobre el collider muestra, sale oculta.</summary>
        Hover,
    }

    /// <summary>
    /// Trigger de tooltip para objetos del mundo 3D. Requiere un <see cref="Collider"/>
    /// en el GameObject. El texto se ancla al punto-pantalla del objeto.
    /// </summary>
    /// <remarks>
    /// Update + raycast manual en lugar de OnMouseEnter/Down: los callbacks legacy los
    /// intercepta un Canvas con GraphicRaycaster que cubra la pantalla, y el HUD la cubre.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/World Tooltip Trigger")]
    public sealed class WorldTooltipTrigger : MonoBehaviour
    {
        /// <summary>Texto. Null al primer uso = auto-resolve de <see cref="IHasTooltipInfo"/>.</summary>
        public Func<string> TextProvider;

        /// <summary>Tarjetas del panel. Null = tooltip de texto y nada más.</summary>
        public Func<IReadOnlyList<StatusIconState>> CardsProvider;

        /// <summary>Columna del costado: los estados que le aplicaron.</summary>
        public Func<IReadOnlyList<StatusIconState>> SideCardsProvider;

        /// <summary>Tarjetas colgadas debajo de la caja.</summary>
        public Func<IReadOnlyList<StatusIconState>> BottomCardsProvider;

        /// <summary>Renglón extra al pie (la debilidad).</summary>
        public Func<string> FooterLineProvider;

        /// <summary>Banda de identidad. Gana sobre <see cref="TextProvider"/> cuando está.</summary>
        public Func<TooltipContent> ContentProvider;

        /// <summary>Flanco de hover, para quien además del tooltip pinta algo.</summary>
        public event Action<bool> HoverChanged;

        /// <summary>
        /// Click fija el tooltip (solo enemigos). Nunca consume el click de targeting, y
        /// entrar en modo ataque suelta el fijado.
        /// </summary>
        public bool PinOnClick;

        /// <summary>
        /// El fijado re-mostró el panel sin flanco de hover; el preview de amenaza solo
        /// escucha <see cref="HoverChanged"/> y se perdería estos re-dibujos.
        /// </summary>
        public event Action PinRefreshed;

        /// <summary>Si este trigger tiene el tooltip fijado ahora.</summary>
        public bool IsPinned => _pinned;

        private bool _pinned;

        // Un solo fijado en todo el combate: el panel es uno solo, así que fijar B suelta a A.
        private static WorldTooltipTrigger s_pinned;

        [SerializeField] private WorldTooltipMode _mode = WorldTooltipMode.Click;

        /// <summary>Escribible para los triggers agregados por código.</summary>
        public WorldTooltipMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;

                // Pasar a Click dejaría un hover abierto que ningún Update cierra.
                SetHover(false, null);
            }
        }

        [SerializeField] private TooltipPlacementSettings _placement = new TooltipPlacementSettings();

        /// <summary>Escribible por lo mismo que <see cref="Mode"/>.</summary>
        public TooltipPlacementMode Placement
        {
            get => _placement.Mode;
            set => _placement.Mode = value;
        }

        [Tooltip("Below = panel debajo del anclaje (solo AutoFit), para no tapar al pawn.")]
        [SerializeField] private TooltipVerticalSide _verticalSide = TooltipVerticalSide.Above;

        /// <summary>Lado vertical del panel (solo AutoFit).</summary>
        public TooltipVerticalSide VerticalSide
        {
            get => _verticalSide;
            set => _verticalSide = value;
        }

        [Tooltip("Cámara usada para raycast + WorldToScreenPoint. Null = Camera.main en runtime.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Distancia máxima del raycast al cursor en world units. Default 100.")]
        [SerializeField] private float _raycastDistance = 100f;

        private int _ownerId;
        private bool _hoverActive;

        private void Awake()
        {
            _ownerId = GetInstanceID();
        }

        private void Update()
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;
            if (!TryGetMouseScreenPos(out var mouseScreen)) return;

            bool pointerOverUI = EventSystem.current != null
                                 && EventSystem.current.IsPointerOverGameObject();

            bool hitMe = !pointerOverUI && RaycastHitsMe(cam, mouseScreen);

            if (_mode == WorldTooltipMode.Hover)
            {
                if (PinOnClick && !pointerOverUI && MouseLeftPressedThisFrame())
                    HandlePinClick(hitMe);

                // Con una selección de target activa el panel se re-mostraba por hover
                // y su fila de slots (el único raycastable del panel) colgaba sobre el
                // enemigo: el click de atacar caía "sobre UI" y TileClickHandler lo
                // tragaba — había que clickear afuera y de vuelta. Durante targeting
                // el tooltip no se muestra y el click llega limpio.
                SetHover(ShouldShowHover(hitMe, _pinned, IsSelectingTarget()), cam);
                return;
            }

            if (hitMe && MouseLeftPressedThisFrame())
            {
                ToggleTooltip(cam);
            }
        }

        // El fijado vive solo en los clicks que hoy no hacen nada: targeting es atacar.
        // Internal para los tests: simular el mouse probaría al raycast, no al fijado.
        internal void HandlePinClick(bool hitMe)
        {
            if (IsSelectingTarget()) return;

            // El click que confirma objetivo resuelve la selección en el mismo frame:
            // IsSelecting ya da false, pero ese click fue de atacar.
            if (SelectionController.LastSelectionEndFrame == Time.frameCount) return;

            if (hitMe)
            {
                if (_pinned) Unpin();
                else Pin();
            }
            else if (_pinned)
            {
                Unpin();
            }
        }

        private static bool IsSelectingTarget()
            => ServiceLocator.TryGetService<ISelectionController>(out var selection)
               && selection != null && selection.IsSelecting;

        /// <summary>
        /// Si el hover debe mostrar el panel este frame. Estático puro — el seam de
        /// test del fix "targeting con tooltip abierto traga el click de atacar".
        /// </summary>
        internal static bool ShouldShowHover(bool hitMe, bool pinned, bool selecting)
            => !selecting && (hitMe || pinned);

        /// <summary>Fija este trigger y suelta al anterior. Público para tests/tutorial.</summary>
        public void Pin()
        {
            if (_pinned) return;
            if (s_pinned != null && s_pinned != this) s_pinned.Unpin();
            s_pinned = this;
            _pinned = true;

            // Solo en el flanco y espejado en Unpin: doble suscripción re-muestra dos veces.
            EventManager.Subscribe(EventName.OnTurnStarted, HandlePinRefresh);
            EventManager.Subscribe(EventName.OnActionSelectionStarted, HandleTargetingStarted);
            EventManager.Subscribe(EventName.OnChainTargetSelectionStarted, HandleTargetingStarted);

            if (TooltipController.Instance != null) TooltipController.Instance.SetPinned(true);
        }

        /// <summary>Suelta el fijado. El hover vigente (si lo hay) mantiene el panel abierto.</summary>
        public void Unpin()
        {
            if (!_pinned) return;
            _pinned = false;
            if (s_pinned == this) s_pinned = null;

            EventManager.UnSubscribe(EventName.OnTurnStarted, HandlePinRefresh);
            EventManager.UnSubscribe(EventName.OnActionSelectionStarted, HandleTargetingStarted);
            EventManager.UnSubscribe(EventName.OnChainTargetSelectionStarted, HandleTargetingStarted);

            if (TooltipController.Instance != null) TooltipController.Instance.SetPinned(false);
        }

        // Re-mostrar ES el refresh: los providers recolectan fresh en cada Show.
        private void HandlePinRefresh(params object[] args)
        {
            if (!_pinned) return;

            ShowTooltip(_camera != null ? _camera : Camera.main);
            PinRefreshed?.Invoke();
        }

        // Al agarrar la ficha de atacar el panel se cierra: el click vuelve a ser de targeting.
        private void HandleTargetingStarted(params object[] args)
        {
            Unpin();
            if (!_hoverActive) return;
            SetHover(false, null);
        }

        // UN raycast por frame compartido: ~112 casillas con trigger en la sala prendida
        // fuego; uno por trigger dominaba el costo sostenido.
        private static readonly RaycastHit[] s_sharedHits = new RaycastHit[64];
        private static int s_sharedHitCount;
        private static int s_sharedFrame = -1;
        private static Camera s_sharedCam;

        private bool RaycastHitsMe(Camera cam, Vector2 mouseScreen)
        {
            if (s_sharedFrame != Time.frameCount || s_sharedCam != cam)
            {
                s_sharedFrame = Time.frameCount;
                s_sharedCam = cam;

                // La cámara pixel-art renderiza a un RT chico: el mouse se escala al viewport
                // interno antes del ScreenPointToRay (mismo fix que TileClickHandler).
                var rtPos = new Vector2(
                    mouseScreen.x / Screen.width  * cam.pixelWidth,
                    mouseScreen.y / Screen.height * cam.pixelHeight);
                var ray = cam.ScreenPointToRay(rtPos);
                s_sharedHitCount = Physics.RaycastNonAlloc(ray, s_sharedHits, _raycastDistance);
            }

            for (int i = 0; i < s_sharedHitCount; i++)
            {
                var hitGo = s_sharedHits[i].collider != null ? s_sharedHits[i].collider.gameObject : null;
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

        // Único lugar que mueve _hoverActive: un solo flanco, sin dobles disparos.
        private void SetHover(bool on, Camera cam)
        {
            if (_hoverActive == on) return;
            _hoverActive = on;

            if (on) ShowTooltip(cam);
            else HideTooltip();

            HoverChanged?.Invoke(on);
        }

        private void ShowTooltip(Camera cam)
        {
            var content = BuildContent();

            // Con tarjetas o banda el panel vale aunque el párrafo venga vacío.
            if (content.IsEmpty) return;
            if (TooltipController.Instance == null) return;

            TooltipController.Instance.Show(
                content, ResolvePlacementScreenPos(cam), _ownerId, _placement.Mode, _verticalSide);

            // Cada Show pisa el candado del panel compartido; el dueño lo re-afirma.
            if (_pinned) TooltipController.Instance.SetPinned(true);
        }

        private TooltipContent BuildContent()
        {
            var cards = CardsProvider?.Invoke();
            var sideCards = SideCardsProvider?.Invoke();
            var bottomCards = BottomCardsProvider?.Invoke();
            if (ContentProvider == null) return TooltipContent.FromText(ResolveText(), cards);

            // Las tarjetas son la MISMA lista que pinta la fila sobre la cabeza.
            var content = ContentProvider.Invoke();
            return new TooltipContent(
                text: content.Text, name: content.Name, cards: cards,
                flavor: ComposeFlavor(content.Flavor, FooterLineProvider?.Invoke()),
                health: content.Health, maxHealth: content.MaxHealth, shield: content.Shield,
                type: content.Type, sideCards: sideCards, bottomCards: bottomCards);
        }

        /// <summary>Estático y público: el preview de editor arma el mismo pie sin combate.</summary>
        public static string ComposeFlavor(string flavor, string footerLine)
        {
            if (string.IsNullOrEmpty(footerLine)) return flavor;
            return string.IsNullOrEmpty(flavor) ? footerLine : flavor + "\n" + footerLine;
        }

        private void ToggleTooltip(Camera cam)
        {
            string text = ResolveText();
            if (string.IsNullOrEmpty(text) || TooltipController.Instance == null) return;
            TooltipController.Instance.Toggle(text, ResolvePlacementScreenPos(cam), _ownerId,
                _placement.Mode, _verticalSide);
        }

        // AutoFit ancla al objeto 3D; Fixed al RectTransform configurado + offset en píxeles
        // de referencia. Fixed SIN anchor cae al objeto 3D + offset en píxeles reales.
        private Vector2 ResolvePlacementScreenPos(Camera cam)
        {
            if (_placement.Mode == TooltipPlacementMode.Fixed && _placement.FixedAnchor != null)
                return _placement.ResolveFixedScreenPos(null);

            Vector2 objScreen = ResolveAnchorScreenPos(cam);
            return _placement.Mode == TooltipPlacementMode.Fixed
                ? objScreen + _placement.FixedOffset
                : objScreen;
        }

        // WorldToScreenPoint devuelve coords del RT interno de la cámara pixel-art, no del
        // Screen real — se re-escala para que el canvas ancle donde el user ve el objeto.
        private Vector2 ResolveAnchorScreenPos(Camera cam)
        {
            // Sin cámara no hay proyección (refresh del fijado fuera de una escena completa).
            if (cam == null) return Vector2.zero;
            Vector3 rtPos = cam.WorldToScreenPoint(transform.position);
            if (cam.pixelWidth <= 0 || cam.pixelHeight <= 0) return rtPos;
            return new Vector2(
                rtPos.x / cam.pixelWidth  * Screen.width,
                rtPos.y / cam.pixelHeight * Screen.height);
        }

        private void HideTooltip()
        {
            if (TooltipController.Instance == null) return;

            // Con otro trigger fijado, salir del hover le devuelve el panel al fijado.
            if (s_pinned != null && s_pinned != this && s_pinned._pinned)
            {
                s_pinned.ShowTooltip(s_pinned._camera != null ? s_pinned._camera : Camera.main);
                s_pinned.PinRefreshed?.Invoke();
                return;
            }

            TooltipController.Instance.Hide(_ownerId);
        }

        // HoverChanged(false) a propósito: apaga el dibujo cuando el dueño muere con hover.
        private void OnDisable()
        {
            Unpin();
            SetHover(false, null);
        }

        private string ResolveText()
        {
            if (TextProvider == null) TextProvider = TooltipResolver.AutoResolve(this);
            return TextProvider?.Invoke();
        }

#if UNITY_EDITOR
        [Title("Preview (solo editor)")]
        [TextArea(2, 5)]
        [Tooltip("Texto de ejemplo del botón de preview.")]
        [SerializeField] private string _previewText =
            "<b>Forzar Puerta</b>\nCosto: 2 de energía\nPuntaje a superar: 25";

        [Button("Mostrar preview en Game view")]
        private void ShowEditorPreview()
        {
            var controller = FindFirstObjectByType<TooltipController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogWarning("[WorldTooltipTrigger] No hay TooltipController en la escena.", this);
                return;
            }
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null && _placement.FixedAnchor == null)
            {
                Debug.LogWarning("[WorldTooltipTrigger] Sin cámara para proyectar el objeto — " +
                                 "asigná _camera o usá modo Fixed con anchor de UI.", this);
                return;
            }
            controller.EditorPreview(_previewText, ResolvePlacementScreenPos(cam), _placement.Mode);
        }

        [Button("Ocultar preview")]
        private void HideEditorPreview()
        {
            var controller = FindFirstObjectByType<TooltipController>(FindObjectsInactive.Include);
            if (controller != null) controller.EditorPreviewHide();
        }
#endif
    }
}
