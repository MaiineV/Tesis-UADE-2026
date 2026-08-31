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
    /// <b>Por qué Update + Physics.Raycast en lugar de OnMouseDown / OnMouseEnter</b>:
    /// los callbacks legacy del MonoBehaviour son interceptados por Unity cuando hay un
    /// Canvas con GraphicRaycaster cubriendo la pantalla (típico en juegos con HUD que
    /// ocupa toda la pantalla aunque sea con paneles transparentes). El raycast manual
    /// chequea explícitamente <see cref="EventSystem.IsPointerOverGameObject"/> y, si el
    /// cursor NO está sobre UI, hace su propio <see cref="Physics.Raycast"/> y dispara
    /// si pega a este collider. Funciona aunque el HUD ocupe toda la pantalla.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/World Tooltip Trigger")]
    public sealed class WorldTooltipTrigger : MonoBehaviour
    {
        /// <summary>
        /// Provider del texto. Si <c>null</c> al primer uso, el trigger intenta auto-resolver
        /// un <see cref="IHasTooltipInfo"/> en este GameObject o en sus padres/hijos.
        /// </summary>
        public Func<string> TextProvider;

        /// <summary>
        /// Tarjetas del panel, bajo el encabezado que da <see cref="TextProvider"/>. <c>null</c> =
        /// tooltip de texto y nada más.
        /// </summary>
        public Func<IReadOnlyList<StatusIconState>> CardsProvider;

        /// <summary>
        /// Tarjetas de la columna del costado — los estados que le aplicaron. <c>null</c> = el
        /// panel no abre segunda columna.
        /// </summary>
        public Func<IReadOnlyList<StatusIconState>> SideCardsProvider;

        /// <summary>
        /// Tarjetas colgadas debajo de la caja. <c>null</c> = nada colgando.
        /// </summary>
        public Func<IReadOnlyList<StatusIconState>> BottomCardsProvider;

        /// <summary>
        /// Un renglón extra al pie del panel, debajo del texto de sabor y con su misma letra —
        /// la debilidad del enemigo. <c>null</c> = el pie queda como lo trajo el contenido.
        /// </summary>
        public Func<string> FooterLineProvider;

        /// <summary>
        /// Provider de la banda de identidad — nombre, vitales y color al pie. Gana sobre
        /// <see cref="TextProvider"/> cuando está: quien sabe describirse entero no tiene por qué
        /// aplanarse a un párrafo.
        /// </summary>
        public Func<TooltipContent> ContentProvider;

        /// <summary>
        /// Entra (<c>true</c>) y sale (<c>false</c>) el hover. Lo consume quien además del texto
        /// tiene que pintar algo — el tooltip se resuelve solo acá adentro.
        /// </summary>
        public event Action<bool> HoverChanged;

        /// <summary>
        /// Un click sobre el objeto fija el tooltip: el panel queda abierto con el mouse libre y
        /// el contenido se re-muestra en cada turno. Solo lo prenden los enemigos. Fijar nunca
        /// consume el click que selecciona objetivo: los clicks en modo targeting se ignoran, y
        /// entrar en modo ataque suelta el fijado solo.
        /// </summary>
        public bool PinOnClick;

        /// <summary>
        /// El fijado volvió a mostrar el tooltip sin flanco de hover (cambio de turno, o el hover
        /// de otro trigger que devolvió el panel). Lo consume quien pinta el preview de amenaza,
        /// que solo escucha <see cref="HoverChanged"/> y se perdería estos re-dibujos.
        /// </summary>
        public event Action PinRefreshed;

        /// <summary>Si este trigger tiene el tooltip fijado ahora.</summary>
        public bool IsPinned => _pinned;

        private bool _pinned;

        // Un solo fijado en todo el combate: el panel es uno solo, así que fijar B suelta a A.
        private static WorldTooltipTrigger s_pinned;

        [SerializeField] private WorldTooltipMode _mode = WorldTooltipMode.Click;

        /// <summary>
        /// Modo de activación. Escribible para los triggers que se agregan por código: el default
        /// serializado es <see cref="WorldTooltipMode.Click"/> (la puerta), y un
        /// <c>AddComponent</c> no puede elegir otro desde el Inspector.
        /// </summary>
        public WorldTooltipMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;

                // El hover activo muere con el cambio de modo: si se pasa a Click quedaría un
                // tooltip abierto que ningún Update va a cerrar.
                SetHover(false, null);
            }
        }

        [SerializeField] private TooltipPlacementSettings _placement = new TooltipPlacementSettings();

        /// <summary>
        /// Dónde se dibuja el panel respecto del objeto. Escribible por lo mismo que
        /// <see cref="Mode"/>: los triggers que se cuelgan por código no pasan por el Inspector.
        /// </summary>
        public TooltipPlacementMode Placement
        {
            get => _placement.Mode;
            set => _placement.Mode = value;
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

            // Si el cursor está sobre UI (botón, panel del HUD), no procesar hits del mundo.
            bool pointerOverUI = EventSystem.current != null
                                 && EventSystem.current.IsPointerOverGameObject();

            bool hitMe = !pointerOverUI && RaycastHitsMe(cam, mouseScreen);

            if (_mode == WorldTooltipMode.Hover)
            {
                if (PinOnClick && !pointerOverUI && MouseLeftPressedThisFrame())
                    HandlePinClick(hitMe);

                SetHover(hitMe || _pinned, cam);
                return;
            }

            if (hitMe && MouseLeftPressedThisFrame())
            {
                ToggleTooltip(cam);
            }
        }

        // El click de targeting es atacar (TileClickHandler): el fijado solo puede vivir en los
        // clicks que hoy no hacen nada. Click sobre el objeto = toggle; click en el vacío con el
        // tooltip fijado = soltar.
        // Internal para los tests: simular el mouse acá probaría al raycast, no al fijado.
        internal void HandlePinClick(bool hitMe)
        {
            if (IsSelectingTarget()) return;

            // El click que confirma el objetivo resuelve la selección sincrónico, y este
            // Update puede correr después en el mismo frame: IsSelecting ya da false, pero
            // ese click fue de atacar — no puede fijar.
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
        /// Fija el tooltip de este trigger. Suelta al que estuviera fijado — el panel es uno.
        /// Público para poder fijar/soltar sin simular el mouse (tests, tutorial).
        /// </summary>
        public void Pin()
        {
            if (_pinned) return;
            if (s_pinned != null && s_pinned != this) s_pinned.Unpin();
            s_pinned = this;
            _pinned = true;

            // Suscripciones solo en el flanco del fijado, y espejadas en Unpin: un pin que
            // suscribe dos veces re-muestra el panel dos veces por turno.
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

        // Los providers recolectan fresh en cada Show: re-mostrar ES el refresh del bloque de
        // próximo turno mientras el panel está fijado, sin re-hoverear.
        private void HandlePinRefresh(params object[] args)
        {
            if (!_pinned) return;

            ShowTooltip(_camera != null ? _camera : Camera.main);
            PinRefreshed?.Invoke();
        }

        // Al agarrar la ficha de atacar el panel se cierra solo: el click vuelve a ser 100% de
        // seleccionar objetivo (decisión §6.2 del spec de tooltips).
        private void HandleTargetingStarted(params object[] args)
        {
            Unpin();
            if (!_hoverActive) return;
            SetHover(false, null);
        }

        // UN raycast por frame compartido entre todos los triggers, no uno por trigger: con la
        // sala prendida fuego hay ~112 casillas con trigger además de los enemigos, y un
        // RaycastAll (que además aloca el array) por cada uno era el nuevo costo sostenido
        // dominante. Todos comparten el mismo ray porque todos preguntan lo mismo: qué hay
        // bajo el mouse. La distancia la fija el primero del frame — todos usan el default.
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

                // Pixel-art pipeline: la cámara renderiza a un RT chiquito, así que
                // pixelWidth/Height ≠ Screen.width/Height. Escalamos el mouse pos al
                // viewport interno de la cámara antes del ScreenPointToRay. Mismo fix
                // que TileClickHandler usa para sus raycasts.
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

        // Único lugar que mueve _hoverActive: el flanco tiene que ser uno solo para que el
        // evento no se dispare dos veces ni se saltee la salida.
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

            // Con tarjetas o con banda el panel vale aunque el párrafo venga vacío: la columna
            // ES el contenido.
            if (content.IsEmpty) return;
            if (TooltipController.Instance == null) return;

            TooltipController.Instance.Show(
                content, ResolvePlacementScreenPos(cam), _ownerId, _placement.Mode);

            // Cada Show pisa el candado del panel compartido; el dueño lo re-afirma. Así el
            // hover de otro trigger sobre un panel fijado no deja el candado mintiendo.
            if (_pinned) TooltipController.Instance.SetPinned(true);
        }

        private TooltipContent BuildContent()
        {
            var cards = CardsProvider?.Invoke();
            var sideCards = SideCardsProvider?.Invoke();
            var bottomCards = BottomCardsProvider?.Invoke();
            if (ContentProvider == null) return TooltipContent.FromText(ResolveText(), cards);

            // Las tarjetas siguen viniendo del CardsProvider aunque haya banda: las arma la fila
            // que flota sobre la cabeza, y son la MISMA lista que esa fila pinta.
            var content = ContentProvider.Invoke();
            return new TooltipContent(
                text: content.Text, name: content.Name, cards: cards,
                flavor: ComposeFlavor(content.Flavor, FooterLineProvider?.Invoke()),
                health: content.Health, maxHealth: content.MaxHealth, shield: content.Shield,
                type: content.Type, sideCards: sideCards, bottomCards: bottomCards);
        }

        /// <summary>
        /// El pie con su renglón extra debajo. Estático y público porque el preview de editor
        /// arma este mismo panel sin combate y tiene que pegarlo igual.
        /// </summary>
        public static string ComposeFlavor(string flavor, string footerLine)
        {
            if (string.IsNullOrEmpty(footerLine)) return flavor;
            return string.IsNullOrEmpty(flavor) ? footerLine : flavor + "\n" + footerLine;
        }

        private void ToggleTooltip(Camera cam)
        {
            string text = ResolveText();
            if (string.IsNullOrEmpty(text) || TooltipController.Instance == null) return;
            TooltipController.Instance.Toggle(text, ResolvePlacementScreenPos(cam), _ownerId, _placement.Mode);
        }

        // Punto-pantalla final según el modo: AutoFit ancla al objeto 3D (el controller
        // suma su offset global y clampea a pantalla); Fixed ancla al RectTransform de UI
        // configurado + offset en píxeles de referencia (resolución-independiente). Fixed
        // SIN anchor cae al objeto 3D + offset en píxeles reales — para tooltips de mundo
        // conviene configurar un anchor de UI o usar AutoFit.
        private Vector2 ResolvePlacementScreenPos(Camera cam)
        {
            if (_placement.Mode == TooltipPlacementMode.Fixed && _placement.FixedAnchor != null)
                return _placement.ResolveFixedScreenPos(null);

            Vector2 objScreen = ResolveAnchorScreenPos(cam);
            return _placement.Mode == TooltipPlacementMode.Fixed
                ? objScreen + _placement.FixedOffset
                : objScreen;
        }

        // WorldToScreenPoint devuelve coords del viewport interno de la cámara — para
        // pixel-art que renderiza a un RT chiquito, eso es del RT, no del Screen real.
        // Re-escalamos al Screen para que el TooltipController (Canvas Overlay) ancle
        // donde el user ve la puerta.
        private Vector2 ResolveAnchorScreenPos(Camera cam)
        {
            // Sin cámara no hay proyección — pasa en el refresh del fijado fuera de una escena
            // completa. ScreenTopRight ignora el punto igual.
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

            // Con otro trigger fijado, salir de este hover no cierra el panel: se lo devuelve al
            // fijado, que lo re-muestra con su contenido (y su candado).
            if (s_pinned != null && s_pinned != this && s_pinned._pinned)
            {
                s_pinned.ShowTooltip(s_pinned._camera != null ? s_pinned._camera : Camera.main);
                s_pinned.PinRefreshed?.Invoke();
                return;
            }

            TooltipController.Instance.Hide(_ownerId);
        }

        // Levanta HoverChanged(false) a propósito: es lo que apaga el dibujo cuando el enemigo
        // muere con el mouse encima. El fijado también muere con el dueño.
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
        [Tooltip("Texto de ejemplo usado por el botón de preview — elegí uno del largo " +
                 "real esperado para ver cuánto espacio ocupa el panel.")]
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
