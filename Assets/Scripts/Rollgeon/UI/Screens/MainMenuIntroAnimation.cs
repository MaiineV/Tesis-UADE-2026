using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Animación de entrada de <c>01_MainMenu</c>: abre un telón (CurtainUI) a
    /// los costados y desvanece dos overlays más con shaders custom
    /// (DistorcionUI, BurnUI), empuja un título y hace subir un set
    /// configurable de elementos. Se dispara una sola vez, apenas la escena
    /// carga.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive en un GameObject hijo de <c>MainMenuScreen</c>
    /// (<c>IntroAnimationController</c>). Los overlays, el título y el array de
    /// scroll se cablean por Inspector — ver plan de implementación
    /// "Intro animation — MainMenu" para el detalle de creación en engine.
    /// No requiere tocar <c>MainMenuScreen</c>, <c>BaseScreen</c> ni
    /// <c>ScreenHost</c>: se auto-dispara en <see cref="OnEnable"/> y se
    /// desactiva a sí mismo para no repetirse si el jugador vuelve al menú.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Main Menu Intro Animation")]
    public class MainMenuIntroAnimation : MonoBehaviour
    {
        [Title("Scroll (elementos que suben)")]
        [Tooltip("Array configurable — poblar en el Inspector cuando exista el arte final.")]
        [SerializeField]
        private RectTransform[] _scrollElements;

        [SerializeField] private float _slideDistance = 150f;
        [SerializeField] private float _slideDuration = 0.8f;
        [SerializeField] private float _slideDelay = 0f;
        [SerializeField] private Ease _slideEase = Ease.InOutSine;

        [Title("Título (push)")]
        [Tooltip("GameObject nuevo, distinto de TitleLabel — poblar con arte final después.")]
        [SerializeField]
        private RectTransform _tituloTransform;

        [SerializeField] private float _tituloPushDistance = 100f;
        [SerializeField] private float _tituloPushDuration = 0.6f;
        [SerializeField] private float _tituloPushDelay = 0f;
        [SerializeField] private Ease _tituloPushEase = Ease.InOutSine;

        [Title("Telón (CurtainUI)")]
        [SerializeField] private RectTransform _curtainLeft;
        [SerializeField] private RectTransform _curtainRight;
        [SerializeField] private float _curtainSlideDuration = 1.2f;
        [SerializeField] private float _curtainSlideDelay = 0f;
        [SerializeField] private Ease _curtainEase = Ease.InOutSine;

        [Title("DistorcionUI")]
        [SerializeField] private Image _distorcionOverlay;
        [SerializeField] private float _distOpacidadDuration = 1f;
        [SerializeField] private float _distOpacidadDelay = 0f;
        [SerializeField] private float _distFloat1Duration = 1f;
        [SerializeField] private float _distFloat1Delay = 0f;
        [SerializeField] private Ease _distEase = Ease.Linear;

        [Title("BurnUI")]
        [SerializeField] private Image _burnOverlay;
        [SerializeField] private float _burnDuration = 1f;
        [SerializeField] private float _burnDelay = 0f;
        [SerializeField] private Ease _burnEase = Ease.Linear;

        [Title("Botones (fade in sincronizado)")]
        [Tooltip("Botones del menú que aparecen juntos modificando su alpha (CanvasGroup).")]
        [SerializeField]
        private CanvasGroup[] _buttonsToFadeIn;

        [SerializeField] private float _buttonsFadeDuration = 0.6f;
        [SerializeField] private float _buttonsFadeDelay = 0f;
        [SerializeField] private Ease _buttonsFadeEase = Ease.InOutSine;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int OpacidadId = Shader.PropertyToID("_Opacidad");
        private static readonly int Float1Id = Shader.PropertyToID("_Float1");

        private void OnEnable()
        {
            PlayIntro();

            // Se desactiva apenas dispara los tweens (no en un callback): PrimeTween
            // corre en un manager central, no en este MonoBehaviour, así que la
            // animación sigue viva. Esto evita que la intro se repita si el jugador
            // vuelve al menú (push/pop reactiva MainMenuScreen, pero este child ya
            // quedó inactivo y no vuelve a disparar OnEnable).
            gameObject.SetActive(false);
        }

        [ContextMenu("Play")]
        private void PlayIntro()
        {
            PlayScroll();
            PlayTitulo();
            PlayCurtains();
            PlayDistorcionUI();
            PlayBurnUI();
            PlayButtonsFadeIn();
        }

        private void PlayScroll()
        {
            if (_scrollElements == null) return;

            foreach (var element in _scrollElements)
            {
                if (element == null) continue;

                var targetY = element.anchoredPosition.y + _slideDistance;
                Tween.UIAnchoredPositionY(element, targetY, _slideDuration, _slideEase, startDelay: _slideDelay);
            }
        }

        private void PlayTitulo()
        {
            if (_tituloTransform == null) return;

            var targetY = _tituloTransform.anchoredPosition.y + _tituloPushDistance;
            Tween.UIAnchoredPositionY(_tituloTransform, targetY, _tituloPushDuration, _tituloPushEase,
                startDelay: _tituloPushDelay);
        }

        private void PlayCurtains()
        {
            if (_curtainLeft != null)
            {
                var targetX = _curtainLeft.anchoredPosition.x - _curtainLeft.rect.width;
                var leftGO = _curtainLeft.gameObject;
                Tween.UIAnchoredPositionX(_curtainLeft, targetX, _curtainSlideDuration, _curtainEase,
                        startDelay: _curtainSlideDelay)
                    .OnComplete(() => leftGO.SetActive(false));
            }

            if (_curtainRight != null)
            {
                var targetX = _curtainRight.anchoredPosition.x + _curtainRight.rect.width;
                var rightGO = _curtainRight.gameObject;
                Tween.UIAnchoredPositionX(_curtainRight, targetX, _curtainSlideDuration, _curtainEase,
                        startDelay: _curtainSlideDelay)
                    .OnComplete(() => rightGO.SetActive(false));
            }
        }

        private void PlayDistorcionUI()
        {
            if (_distorcionOverlay == null) return;

            var material = _distorcionOverlay.material;
            material.SetFloat(OpacidadId, 0f);
            material.SetFloat(Float1Id, 2.5f);

            // A diferencia de Fade/Burn, este overlay se queda activo como efecto
            // ambiente — no se desactiva al terminar. Se apaga el raycast para que
            // no bloquee los botones del menú una vez asentado.
            _distorcionOverlay.raycastTarget = false;

            Tween.MaterialProperty(material, OpacidadId, 1f, _distOpacidadDuration, _distEase,
                startDelay: _distOpacidadDelay);
            Tween.MaterialProperty(material, Float1Id, 0f, _distFloat1Duration, _distEase,
                startDelay: _distFloat1Delay);
        }

        private void PlayBurnUI()
        {
            if (_burnOverlay == null) return;

            var material = _burnOverlay.material;
            material.SetFloat(ProgressId, -1f);
            var overlayGO = _burnOverlay.gameObject;

            Tween.MaterialProperty(material, ProgressId, 1f, _burnDuration, _burnEase, startDelay: _burnDelay)
                .OnComplete(() => overlayGO.SetActive(false));
        }

        private void PlayButtonsFadeIn()
        {
            if (_buttonsToFadeIn == null) return;

            // Mismo delay/duration para los tres — arrancan todos juntos (sincronizados).
            foreach (var group in _buttonsToFadeIn)
            {
                if (group == null) continue;

                group.alpha = 0f;
                Tween.Alpha(group, 1f, _buttonsFadeDuration, _buttonsFadeEase, startDelay: _buttonsFadeDelay);
            }
        }
    }
}
