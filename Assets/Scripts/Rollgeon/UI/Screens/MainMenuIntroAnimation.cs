using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Se dispara cuando termina la última animación de la secuencia (el
        /// empuje del título). Lo escucha <see cref="Rollgeon.UI.Menu.JuicyMenuGroup"/>
        /// para arrancar la entrada de los botones justo al final del intro, en
        /// vez de adivinar un delay fijo.
        /// </summary>
        public event Action IntroFinished;

        /// <summary>
        /// Estado consultable además del evento: el orden de <c>OnEnable</c> entre
        /// componentes hermanos (este y <see cref="Rollgeon.UI.Menu.JuicyMenuGroup"/>)
        /// no está garantizado. Si el intro ya terminó (typ. porque
        /// <c>_hasPlayedThisSession</c> lo saltó de una) ANTES de que
        /// <c>JuicyMenuGroup.OnEnable</c> llegue a suscribirse, el evento ya se
        /// disparó y nunca más — sin este flag se quedaría esperando para siempre.
        /// </summary>
        public bool IntroHasFinished { get; private set; }

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

        [Title("Skip")]
        [Tooltip("Botón invisible full-rect arriba de todo — click en cualquier lado durante el " +
                 "intro lo salta directo al estado final.")]
        [SerializeField]
        private Button _skipButton;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int OpacidadId = Shader.PropertyToID("_Opacidad");
        private static readonly int Float1Id = Shader.PropertyToID("_Float1");

        // Todos los tweens que dispara PlayIntro — se guardan para poder
        // completarlos de golpe si el jugador clickea el skip (o si el intro
        // ya se vio esta sesión, ver OnEnable).
        private readonly List<Tween> _activeTweens = new List<Tween>();

        // Static: sobrevive a SceneManager.LoadScene("01_MainMenu") (derrota,
        // victoria, salir de una run) — cada vuelta al menú recrea este
        // componente de cero, así que una bandera de instancia no alcanza.
        // Solo se resetea con un reinicio real del proceso/juego.
        private static bool _hasPlayedThisSession;

        private void OnEnable()
        {
            if (_skipButton != null)
            {
                _skipButton.gameObject.SetActive(true);
                _skipButton.onClick.AddListener(SkipIntro);
            }

            PlayIntro();

            if (_hasPlayedThisSession)
            {
                // Ya se vio el intro largo esta sesión (volviendo de gameplay,
                // derrota, etc.) — saltar directo al estado final en el mismo
                // frame, sin mostrarlo de nuevo. El fade-in cortito de los
                // botones (JuicyMenuGroup) sigue andando normal vía IntroFinished.
                SkipIntro();
            }
            else
            {
                _hasPlayedThisSession = true;
            }

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

        /// <summary>
        /// Completa todos los tweens en curso de golpe (misma lógica que dejarlos
        /// terminar solos, incluye <see cref="IntroFinished"/> vía el título) y
        /// esconde el catcher de click para no seguir bloqueando el menú ya revelado.
        /// </summary>
        private void SkipIntro()
        {
            foreach (var tween in _activeTweens)
                tween.Complete();
            _activeTweens.Clear();

            if (_skipButton != null)
            {
                _skipButton.onClick.RemoveListener(SkipIntro);
                _skipButton.gameObject.SetActive(false);
            }
        }

        private void PlayScroll()
        {
            if (_scrollElements == null) return;

            foreach (var element in _scrollElements)
            {
                if (element == null) continue;

                var targetY = element.anchoredPosition.y + _slideDistance;
                _activeTweens.Add(
                    Tween.UIAnchoredPositionY(element, targetY, _slideDuration, _slideEase, startDelay: _slideDelay));
            }
        }

        private void PlayTitulo()
        {
            if (_tituloTransform == null)
            {
                // Sin título no hay de qué esperar — avisar igual para no colgar
                // a quien escucha IntroFinished (ej. JuicyMenuGroup).
                MarkIntroFinished();
                return;
            }

            var targetY = _tituloTransform.anchoredPosition.y + _tituloPushDistance;
            _activeTweens.Add(
                Tween.UIAnchoredPositionY(_tituloTransform, targetY, _tituloPushDuration, _tituloPushEase,
                        startDelay: _tituloPushDelay)
                    .OnComplete(MarkIntroFinished));
        }

        private void MarkIntroFinished()
        {
            IntroHasFinished = true;
            IntroFinished?.Invoke();
        }

        private void PlayCurtains()
        {
            if (_curtainLeft != null)
            {
                var targetX = _curtainLeft.anchoredPosition.x - _curtainLeft.rect.width;
                var leftGO = _curtainLeft.gameObject;
                _activeTweens.Add(
                    Tween.UIAnchoredPositionX(_curtainLeft, targetX, _curtainSlideDuration, _curtainEase,
                            startDelay: _curtainSlideDelay)
                        .OnComplete(() => leftGO.SetActive(false)));
            }

            if (_curtainRight != null)
            {
                var targetX = _curtainRight.anchoredPosition.x + _curtainRight.rect.width;
                var rightGO = _curtainRight.gameObject;
                _activeTweens.Add(
                    Tween.UIAnchoredPositionX(_curtainRight, targetX, _curtainSlideDuration, _curtainEase,
                            startDelay: _curtainSlideDelay)
                        .OnComplete(() => rightGO.SetActive(false)));
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

            _activeTweens.Add(
                Tween.MaterialProperty(material, OpacidadId, 1f, _distOpacidadDuration, _distEase,
                    startDelay: _distOpacidadDelay));
            _activeTweens.Add(
                Tween.MaterialProperty(material, Float1Id, 0f, _distFloat1Duration, _distEase,
                    startDelay: _distFloat1Delay));
        }

        private void PlayBurnUI()
        {
            if (_burnOverlay == null) return;

            var material = _burnOverlay.material;
            material.SetFloat(ProgressId, -1f);
            var overlayGO = _burnOverlay.gameObject;

            _activeTweens.Add(
                Tween.MaterialProperty(material, ProgressId, 1f, _burnDuration, _burnEase, startDelay: _burnDelay)
                    .OnComplete(() => overlayGO.SetActive(false)));
        }

        private void PlayButtonsFadeIn()
        {
            if (_buttonsToFadeIn == null) return;

            // Mismo delay/duration para los tres — arrancan todos juntos (sincronizados).
            foreach (var group in _buttonsToFadeIn)
            {
                if (group == null) continue;

                group.alpha = 0f;
                _activeTweens.Add(
                    Tween.Alpha(group, 1f, _buttonsFadeDuration, _buttonsFadeEase, startDelay: _buttonsFadeDelay));
            }
        }
    }
}
