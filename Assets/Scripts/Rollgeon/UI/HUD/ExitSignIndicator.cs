using System;
using PrimeTween;
using Rollgeon.Tutorial.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Tuning visual del indicador de salida — lo provee el caller (los valores
    /// viven serializados en <c>DoorExitSignView</c>, no acá) para que el overlay
    /// quede sin estado propio de configuración.
    /// </summary>
    public readonly struct ExitSignIndicatorStyle
    {
        /// <summary>Tamaño de la flecha en unidades de canvas (ref. 1920x1080).</summary>
        public readonly Vector2 Size;

        /// <summary>Separación vertical entre el punto anclado y la punta de la flecha, en píxeles.</summary>
        public readonly float GapPx;

        public readonly float BobAmplitudePx;
        public readonly float BobDuration;

        /// <summary>Píxeles desde los que cae la flecha al aparecer (0 = sin drop-in).</summary>
        public readonly float DropPixels;
        public readonly float DropDuration;
        public readonly Ease DropEase;

        public ExitSignIndicatorStyle(Vector2 size, float gapPx, float bobAmplitudePx,
            float bobDuration, float dropPixels, float dropDuration, Ease dropEase)
        {
            Size = size;
            GapPx = gapPx;
            BobAmplitudePx = bobAmplitudePx;
            BobDuration = bobDuration;
            DropPixels = dropPixels;
            DropDuration = dropDuration;
            DropEase = dropEase;
        }
    }

    /// <summary>
    /// Indicador screen-space que señala una posición de MUNDO (la casilla frente a
    /// la puerta de salida de piso) — reemplaza al cartel 3D, que no se leía bien en
    /// el mapa. El sprite es el propio cartel bakeado del modelo
    /// (<c>ExitSignSpriteBaker</c>), con el comportamiento de la flecha del
    /// <c>TutorialOverlay</c>: bob infinito y reseguido del anchor por LateUpdate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Self-built on-demand + <c>DontDestroyOnLoad</c>, mismo patrón que
    /// <see cref="InteractionPromptView"/>. Owner-guard en <see cref="Hide"/> por si
    /// a futuro hubiera más de una exit por piso (hoy <c>MarkBossExitDoor</c>
    /// garantiza una sola): el último <see cref="Show"/> gana y un Hide ajeno no
    /// pisa al dueño vigente.
    /// </para>
    /// <para>
    /// A diferencia de InteractionPromptView, acá el cleanup por cambio de escena es
    /// obligatorio: el anchor es una posición de mundo que muere con el gameplay, y
    /// sin el <c>activeSceneChanged → HideInstant</c> la flecha quedaría colgada
    /// sobre el menú al morir o salir de la run.
    /// </para>
    /// </remarks>
    public static class ExitSignIndicator
    {
        private const string OverlayName = "[ExitSignIndicator]";

        // Debajo del dim del tutorial (29000): si un paso de tutorial señala otra
        // cosa, la flecha de salida queda correctamente opacada.
        private const int SortingOrder = 24000;
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static Runtime _instance;

        /// <summary>Visible este frame (para debugging/tests PlayMode).</summary>
        public static bool IsVisible => _instance != null && _instance.Visible;

        /// <summary>
        /// Muestra (o retargetea) la flecha sobre <paramref name="worldPosProvider"/>.
        /// El provider se evalúa POR FRAME: el grid puede no estar registrado todavía
        /// cuando la puerta abre (SetState corre durante el build del dungeon) y así
        /// el anchor "se cura solo" apenas el servicio aparece.
        /// </summary>
        public static void Show(int ownerId, Func<Vector3> worldPosProvider, Sprite sprite,
            in ExitSignIndicatorStyle style)
        {
            if (worldPosProvider == null) return;
            EnsureInstance();
            _instance.ShowFor(ownerId, worldPosProvider, sprite, in style);
        }

        /// <summary>Oculta la flecha SOLO si <paramref name="ownerId"/> es el dueño actual.</summary>
        public static void Hide(int ownerId)
        {
            if (_instance == null) return;
            _instance.HideIfOwner(ownerId);
        }

        /// <summary>Oculta sin importar el owner (cleanup global).</summary>
        public static void HideForce()
        {
            if (_instance == null) return;
            _instance.HideInstant();
        }

        /// <summary>
        /// Destruye el overlay y limpia el estado estático — solo para teardown de
        /// tests (los statics persisten dentro de la sesión del Editor).
        /// </summary>
        public static void ResetForTests()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance.gameObject);
                _instance = null;
            }
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var go = new GameObject(OverlayName);
            // En EditMode/tests DontDestroyOnLoad no aplica — el GO vive y muere con
            // la escena de test (mismo guard que InteractionPromptView).
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<Runtime>();
            _instance.Build();
        }

        /// <summary>MonoBehaviour host: LateUpdate de tracking + tween de drop-in.</summary>
        private sealed class Runtime : MonoBehaviour
        {
            private const float ScreenMarginPx = 24f;

            private Canvas _canvas;
            private Image _arrow;

            private Func<Vector3> _worldPosProvider;
            private ExitSignIndicatorStyle _style;
            private int _currentOwnerId;
            private float _bobPhase;
            private float _dropOffsetPx;
            private Tween _dropTween;

            public bool Visible { get; private set; }

            public void Build()
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = SortingOrder;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                // Sin GraphicRaycaster: la flecha es puramente informativa y no debe
                // robarle ni un raycast al mundo o al HUD.

                _arrow = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                    .GetComponent<Image>();
                _arrow.transform.SetParent(transform, worldPositionStays: false);
                _arrow.raycastTarget = false;
                _arrow.preserveAspect = true;
                var rect = _arrow.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                _arrow.gameObject.SetActive(false);

                SceneManager.activeSceneChanged += OnActiveSceneChanged;
            }

            private void OnDestroy()
            {
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                if (_dropTween.isAlive) _dropTween.Stop();
                if (_instance == this) _instance = null;
            }

            private void OnActiveSceneChanged(Scene from, Scene to) => HideInstant();

            public void ShowFor(int ownerId, Func<Vector3> worldPosProvider, Sprite sprite,
                in ExitSignIndicatorStyle style)
            {
                _currentOwnerId = ownerId;
                _worldPosProvider = worldPosProvider;
                _style = style;
                _bobPhase = 0f;

                _arrow.sprite = sprite;
                _arrow.rectTransform.sizeDelta = style.Size;
                _arrow.gameObject.SetActive(true);
                Visible = true;

                if (_dropTween.isAlive) _dropTween.Stop();
                _dropOffsetPx = 0f;
                if (Application.isPlaying && style.DropPixels > 0f && style.DropDuration > 0f)
                {
                    // El tween no toca el rect (LateUpdate lo pisaría): anima un offset
                    // que el posicionado por frame compone. Unscaled: debe caer igual
                    // con el juego pausado.
                    _dropOffsetPx = style.DropPixels;
                    _dropTween = Tween.Custom(this, style.DropPixels, 0f, style.DropDuration,
                        (self, value) => self._dropOffsetPx = value,
                        style.DropEase, useUnscaledTime: true);
                }

                UpdateArrowPosition();
            }

            public void HideIfOwner(int ownerId)
            {
                if (!Visible || _currentOwnerId != ownerId) return;
                HideInstant();
            }

            public void HideInstant()
            {
                Visible = false;
                _worldPosProvider = null;
                if (_dropTween.isAlive) _dropTween.Stop();
                if (_arrow != null) _arrow.gameObject.SetActive(false);
            }

            private void LateUpdate()
            {
                if (!Visible) return;

                if (_style.BobDuration > 0f)
                    _bobPhase += Time.unscaledDeltaTime / _style.BobDuration;

                UpdateArrowPosition();
            }

            private void UpdateArrowPosition()
            {
                if (_worldPosProvider == null) return;

                if (!TutorialAnchorResolver.TryWorldToScreen(_worldPosProvider(), out var anchorPos))
                {
                    // Anchor detrás de cámara este frame — apagar el graphic, no el
                    // estado: al volver a resolver reaparece sola.
                    _arrow.enabled = false;
                    return;
                }
                _arrow.enabled = true;

                float bob = _style.BobAmplitudePx > 0f && _style.BobDuration > 0f
                    ? Mathf.PingPong(_bobPhase, 1f) * _style.BobAmplitudePx
                    : 0f;

                // El cartel flota SOBRE la casilla: gap + su medio alto en pantalla.
                // sizeDelta está en unidades de canvas → convertir por el scale factor.
                float canvasScale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
                float halfVerticalPx = _style.Size.y * 0.5f * canvasScale;
                float y = anchorPos.y + _style.GapPx + halfVerticalPx + bob + _dropOffsetPx;

                // Clamp DESPUÉS del bob (plan): al panear lejos la flecha se pega al
                // borde quieta en vez de vibrar contra él.
                var pos = new Vector2(
                    Mathf.Clamp(anchorPos.x, ScreenMarginPx, Screen.width - ScreenMarginPx),
                    Mathf.Clamp(y, ScreenMarginPx, Screen.height - ScreenMarginPx));

                // rect.position en píxeles de pantalla — válido en ScreenSpaceOverlay
                // (mismo posicionado que la flecha del TutorialOverlay).
                _arrow.rectTransform.position = new Vector3(pos.x, pos.y, 0f);
            }
        }
    }
}
