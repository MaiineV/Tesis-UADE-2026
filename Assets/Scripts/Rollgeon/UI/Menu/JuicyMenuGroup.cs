using Rollgeon.UI.Screens;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Menu
{
    /// <summary>
    /// Coordina un stack de <see cref="JuicyMenuButton"/>: entrada staggered,
    /// dimming de los no-hovereados y el par de rombos indicadores que sigue
    /// al botón con foco (lerp + pulso, como el video de referencia).
    /// </summary>
    /// <remarks>
    /// [SETUP] Los rombos son Images planas rotadas 45° en Z, hijas de este
    /// GameObject, con raycast apagado. En el main menu la entrada espera a
    /// <see cref="MainMenuIntroAnimation.IntroFinished"/> (<see cref="_introAnimation"/>);
    /// en pausa corre en cada apertura.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Menu/Juicy Menu Group")]
    public sealed class JuicyMenuGroup : MonoBehaviour
    {
        [SerializeField, Required, Tooltip("Orden = orden del stagger de entrada.")]
        private JuicyMenuButton[] _buttons;

        [SerializeField, Optional] private RectTransform _leftDiamond;
        [SerializeField, Optional] private RectTransform _rightDiamond;

        [SerializeField, Required]
        private MenuJuiceSettingsSO _settings;

        [SerializeField, Tooltip("Reproducir la entrada staggered en cada enable (pausa). " +
                 "En el main menu también va true: el delay del intro se maneja escuchando IntroFinished.")]
        private bool _playEntranceOnEnable = true;

        [SerializeField, Optional, Tooltip("Intro de la escena (MainMenuIntroAnimation). Con esto " +
                 "cableado, la PRIMERA entrada tras cargar la escena espera a que dispare " +
                 "IntroFinished (el título termina de empujar); las siguientes (volver por " +
                 "push/pop) entran sin esperar.")]
        private MainMenuIntroAnimation _introAnimation;


        private JuicyMenuButton _current;
        private Image _leftDiamondImage;
        private Image _rightDiamondImage;
        private Vector3 _leftDiamondBaseScale = Vector3.one;
        private Vector3 _rightDiamondBaseScale = Vector3.one;

        private void Awake()
        {
            if (_leftDiamond != null)
            {
                _leftDiamondImage = _leftDiamond.GetComponent<Image>();
                _leftDiamondBaseScale = _leftDiamond.localScale;
            }
            if (_rightDiamond != null)
            {
                _rightDiamondImage = _rightDiamond.GetComponent<Image>();
                _rightDiamondBaseScale = _rightDiamond.localScale;
            }
        }

        private void OnEnable()
        {
            foreach (var button in _buttons)
            {
                if (button == null) continue;
                button.HighlightChanged += HandleHighlightChanged;
            }

            _current = null;
            SetDiamondAlpha(0f);

            if (_playEntranceOnEnable)
            {
                // Ocultar YA, sin importar si hay que esperar el evento o no —
                // PlayEntrance recién oculta+anima cuando se llama, así que si
                // sólo se la esperaba disparar al final, los botones quedaban
                // visibles durante toda la espera del intro.
                foreach (var button in _buttons)
                {
                    if (button == null) continue;
                    button.HideForEntrance();
                }

                // Chequeo directo del estado en vez de confiar solo en el evento:
                // el orden de OnEnable entre componentes hermanos (este y
                // MainMenuIntroAnimation) no está garantizado — si el intro ya
                // terminó (ej. se saltó de una al volver de gameplay) ANTES de
                // que este OnEnable llegue a suscribirse, el evento ya disparó
                // y nunca más, y nos quedaríamos esperando para siempre.
                bool introAlreadyDone = _introAnimation == null || _introAnimation.IntroHasFinished;
                if (introAlreadyDone)
                    PlayEntrance(0f);
                else
                    _introAnimation.IntroFinished += HandleIntroFinished;
            }
        }

        private void OnDisable()
        {
            foreach (var button in _buttons)
            {
                if (button == null) continue;
                button.HighlightChanged -= HandleHighlightChanged;
            }

            if (_introAnimation != null)
                _introAnimation.IntroFinished -= HandleIntroFinished;
        }

        private void HandleIntroFinished()
        {
            _introAnimation.IntroFinished -= HandleIntroFinished;
            PlayEntrance(0f);
        }

        /// <summary>Entrada staggered: delay = base + índice * step.</summary>
        public void PlayEntrance(float baseDelay = 0f)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                _buttons[i].HideForEntrance();
                _buttons[i].PlayEntrance(baseDelay + MenuJuiceMath.StaggerDelay(i, _settings.StaggerStep));
            }
        }

        private void Update()
        {
            if (_leftDiamond == null || _rightDiamond == null) return;

            float dt = Time.unscaledDeltaTime;
            bool hasTarget = _current != null && _current.isActiveAndEnabled;

            float alphaTarget = hasTarget ? 1f : 0f;
            float alphaFactor = MenuJuiceMath.SmoothLerpFactor(_settings.DiamondFadeSpeed, dt);
            FadeDiamond(_leftDiamondImage, alphaTarget, alphaFactor);
            FadeDiamond(_rightDiamondImage, alphaTarget, alphaFactor);

            if (!hasTarget) return;

            float followFactor = MenuJuiceMath.SmoothLerpFactor(_settings.DiamondFollowSpeed, dt);
            _leftDiamond.position = Vector3.Lerp(_leftDiamond.position, DiamondTarget(-1f), followFactor);
            _rightDiamond.position = Vector3.Lerp(_rightDiamond.position, DiamondTarget(1f), followFactor);

            float pulse = MenuJuiceMath.PulseScale(
                Time.unscaledTime, _settings.DiamondPulseFrequency, _settings.DiamondPulseAmplitude);
            _leftDiamond.localScale = _leftDiamondBaseScale * pulse;
            _rightDiamond.localScale = _rightDiamondBaseScale * pulse;
        }

        private void HandleHighlightChanged(JuicyMenuButton button, bool highlighted)
        {
            if (highlighted)
            {
                bool cameFromNone = _current == null;
                _current = button;
                foreach (var other in _buttons)
                {
                    if (other == null) continue;
                    other.SetDimmed(other != button);
                }

                // Sin selección previa los rombos estaban invisibles en una pos
                // vieja: snapearlos al target evita verlos "viajar" al aparecer.
                if (cameFromNone && _leftDiamond != null && _rightDiamond != null)
                {
                    _leftDiamond.position = DiamondTarget(-1f);
                    _rightDiamond.position = DiamondTarget(1f);
                }
            }
            else if (_current == button)
            {
                _current = null;
                foreach (var other in _buttons)
                {
                    if (other == null) continue;
                    other.SetDimmed(false);
                }
            }
        }

        /// <summary>
        /// Posición mundial del rombo a un costado del label (side −1 = izquierda).
        /// Se calcula en espacio del label para que acompañe el scale del hover.
        /// </summary>
        private Vector3 DiamondTarget(float side)
        {
            var labelRect = (RectTransform)_current.Label.transform;
            float half = _current.Label.preferredWidth * 0.5f;
            Vector2 center = labelRect.rect.center;
            return labelRect.TransformPoint(
                new Vector3(center.x + side * (half + _settings.DiamondMargin), center.y, 0f));
        }

        private void FadeDiamond(Image image, float targetAlpha, float factor)
        {
            if (image == null) return;
            var color = image.color;
            color.a = Mathf.Lerp(color.a, targetAlpha, factor);
            image.color = color;
        }

        private void SetDiamondAlpha(float alpha)
        {
            if (_leftDiamondImage != null)
            {
                var c = _leftDiamondImage.color; c.a = alpha; _leftDiamondImage.color = c;
            }
            if (_rightDiamondImage != null)
            {
                var c = _rightDiamondImage.color; c.a = alpha; _rightDiamondImage.color = c;
            }
        }
    }
}
