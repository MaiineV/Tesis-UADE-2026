using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Rollgeon.Economy;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Pila de oro: hasta 4 fichas planas animadas como las demás pilas; desde
    /// 5 se suma una ficha inclinada "apoyada" (sprite ya girado) que no se
    /// vuelve a animar — los cambios estando ≥5 solo agitan la pila y
    /// actualizan el número. Al volver bajo 5 las fichas desaparecen con el
    /// fade a la izquierda estándar.
    /// </summary>
    /// <remarks>
    /// <c>OnGoldChanged</c> no trae Guid (el oro es global del run), así que la
    /// vista es autónoma: NO la bindean los orquestadores del HUD (a diferencia
    /// de vida/energía). Vive en Canvas_PlayerStatus, siempre activa.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Gold Chip Stack View")]
    public class GoldChipStackView : MonoBehaviour
    {
        private const string LogPrefix = "[GoldChipStackView] ";

        [Title("Gold Chips — Widget refs")]
        [Required("Arrastrar el ChipStackView de la pila de oro.")]
        [SerializeField]
        private ChipStackView _stack;

        [Required("Arrastrar la Image de la ficha inclinada (hija aparte, arranca inactiva).")]
        [SerializeField]
        private Image _tiltedChip;

        [Required("Arrastrar el TextMeshProUGUI debajo de la pila.")]
        [SerializeField]
        private TextMeshProUGUI _label;

        [Required("Arrastrar el ChipStackSettings asset.")]
        [SerializeField]
        private ChipStackSettingsSO _settings;

        [Title("Gold Chips — Deuda")]
        [Tooltip("Ficha que aparece con oro negativo (deuda de Tarjeta de Crédito). Hija aparte, " +
                 "arranca inactiva. Opcional: sin ella solo se tiñe el número. Sin sprite toma " +
                 "GoldChipFlat del settings.")]
        [SerializeField]
        private Image _debtChip;

        [Tooltip("Tinte del número y de la ficha de deuda mientras el oro es negativo.")]
        [SerializeField]
        private Color _debtTint = new Color(0.86f, 0.25f, 0.25f);

        [ShowInInspector, ReadOnly]
        private int _displayedGold;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private bool _hasValue;
        private bool _tiltedShown;
        private CanvasGroup _tiltedGroup;
        private Vector2 _tiltedRestPos;
        private bool _tiltedRestCaptured;
        private bool _debtShown;
        private bool _labelRestColorCaptured;
        private Color _labelRestColor;
        private readonly List<int> _chipBuffer = new List<int>();

        private void Awake()
        {
            ConfigureStack();
            CacheTilted();
            CacheDebt();
        }

        private void ConfigureStack()
        {
            if (_stack == null || _settings == null) return;
            _stack.Configure(_settings, new[] { _settings.GoldChipFlat }, _settings.GoldChipSpacingY);
        }

        private void CacheTilted()
        {
            if (_tiltedChip == null || _tiltedRestCaptured) return;

            _tiltedGroup = _tiltedChip.GetComponent<CanvasGroup>();
            if (_tiltedGroup == null) _tiltedGroup = _tiltedChip.gameObject.AddComponent<CanvasGroup>();
            _tiltedRestPos = ((RectTransform)_tiltedChip.transform).anchoredPosition;
            _tiltedRestCaptured = true;

            if (_settings != null && _settings.GoldChipTilted != null)
            {
                _tiltedChip.sprite = _settings.GoldChipTilted;
                ((RectTransform)_tiltedChip.transform).sizeDelta =
                    _settings.GoldChipTilted.rect.size * Mathf.Max(1f, _settings.ChipScale);
            }
            _tiltedChip.raycastTarget = false;
        }

        // La ficha de deuda es una Image plana (no pasa por ChipStackView): no se anima con la
        // pila, solo aparece/desaparece. Sin sprite en el prefab toma la ficha plana del settings
        // para que la deuda se lea como "una ficha de oro en rojo".
        private void CacheDebt()
        {
            if (_debtChip == null) return;

            if (_debtChip.sprite == null && _settings != null && _settings.GoldChipFlat != null)
            {
                _debtChip.sprite = _settings.GoldChipFlat;
                ((RectTransform)_debtChip.transform).sizeDelta =
                    _settings.GoldChipFlat.rect.size * Mathf.Max(1f, _settings.ChipScale);
            }
            _debtChip.color = _debtTint;
            _debtChip.raycastTarget = false;
            _debtChip.gameObject.SetActive(_debtShown);
        }

        private void OnEnable()
        {
            ConfigureStack();
            CacheTilted();
            CacheDebt();
            Subscribe();
            FetchInitialState();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_tiltedChip != null && _tiltedRestCaptured)
            {
                var rect = (RectTransform)_tiltedChip.transform;
                Tween.StopAll(onTarget: rect);
                if (_tiltedGroup != null)
                {
                    Tween.StopAll(onTarget: _tiltedGroup);
                    _tiltedGroup.alpha = 1f;
                }
                rect.anchoredPosition = _tiltedRestPos;
                _tiltedChip.gameObject.SetActive(_tiltedShown);
            }
        }

        private void Subscribe()
        {
            if (_bound) return;
            EventManager.Subscribe(EventName.OnGoldChanged, HandleGoldChanged);
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnGoldChanged, HandleGoldChanged);
            _bound = false;
        }

        private void HandleGoldChanged(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is int current))
            {
                Debug.LogWarning(LogPrefix + "OnGoldChanged args malformed.", this);
                return;
            }

            Apply(current, animate: true);
        }

        private void FetchInitialState()
        {
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy) && economy != null)
            {
                Apply(economy.CurrentGold, animate: false);
            }
        }

        private void Apply(int gold, bool animate)
        {
            if (_settings == null) return;

            var transition = _hasValue
                ? ChipStackMath.ClassifyGoldTransition(_displayedGold, gold)
                : ChipStackMath.GoldTransition.ChipsChanged;

            switch (transition)
            {
                case ChipStackMath.GoldTransition.ChipsChanged:
                    var visual = ChipStackMath.ComputeGoldVisual(gold);
                    _chipBuffer.Clear();
                    for (int i = 0; i < visual.FlatChips; i++) _chipBuffer.Add(0);
                    if (_stack != null) _stack.SetChips(_chipBuffer, animate);
                    SetTiltedShown(visual.ShowTilted, animate);
                    break;

                case ChipStackMath.GoldTransition.ShakeOnly:
                    if (animate && _stack != null)
                    {
                        _stack.Shake();
                        PlayShakeSfx();
                    }
                    break;

                case ChipStackMath.GoldTransition.None:
                    break;
            }

            if (_label != null) _label.text = ChipStackMath.FormatGoldLabel(gold);
            ApplyDebtState(ChipStackMath.IsDebt(gold));
            _displayedGold = gold;
            _hasValue = true;
        }

        /// <summary>
        /// Con oro negativo la pila queda vacía (no hay fichas negativas) y el "-12" solo se
        /// leía como un número raro. Tiñe el número y prende la ficha de deuda; al volver a
        /// ≥0 restaura el color original del label (capturado la primera vez).
        /// </summary>
        private void ApplyDebtState(bool debt)
        {
            if (_label != null)
            {
                if (!_labelRestColorCaptured)
                {
                    _labelRestColor = _label.color;
                    _labelRestColorCaptured = true;
                }
                _label.color = debt ? _debtTint : _labelRestColor;
            }

            if (_debtChip != null && debt != _debtShown)
            {
                if (debt) CacheDebt();
                _debtChip.gameObject.SetActive(debt);
            }
            _debtShown = debt;
        }

        private void SetTiltedShown(bool shown, bool animate)
        {
            if (_tiltedChip == null || shown == _tiltedShown)
            {
                _tiltedShown = shown;
                return;
            }

            _tiltedShown = shown;
            CacheTilted();

            var rect = (RectTransform)_tiltedChip.transform;
            bool instant = !animate
                           || DiceAnim.DiceUiMotionPrefs.ReducedMotion
                           || !gameObject.activeInHierarchy;

            Tween.StopAll(onTarget: rect);
            if (_tiltedGroup != null) Tween.StopAll(onTarget: _tiltedGroup);

            if (shown)
            {
                // Un solo drop-in al aparecer; después queda quieta para siempre
                // (los cambios ≥5 solo agitan la pila, no la ficha apoyada).
                _tiltedChip.gameObject.SetActive(true);
                if (_tiltedGroup != null) _tiltedGroup.alpha = 1f;

                if (instant)
                {
                    rect.anchoredPosition = _tiltedRestPos;
                    return;
                }

                rect.anchoredPosition = _tiltedRestPos + new Vector2(0f, _settings.DropHeight);
                Tween.UIAnchoredPositionY(rect, _tiltedRestPos.y, _settings.DropDuration,
                    _settings.DropEase, useUnscaledTime: true);
            }
            else
            {
                if (instant)
                {
                    rect.anchoredPosition = _tiltedRestPos;
                    _tiltedChip.gameObject.SetActive(false);
                    return;
                }

                Tween.UIAnchoredPositionX(rect, _tiltedRestPos.x - _settings.LoseSlideDistance,
                    _settings.LoseDuration, _settings.LoseEase, useUnscaledTime: true);
                if (_tiltedGroup != null)
                {
                    Tween.Alpha(_tiltedGroup, 0f, _settings.LoseDuration, _settings.LoseEase,
                            useUnscaledTime: true)
                        .OnComplete(this, self => self.ResetTiltedHidden());
                }
            }
        }

        private void ResetTiltedHidden()
        {
            if (_tiltedChip == null) return;
            var rect = (RectTransform)_tiltedChip.transform;
            rect.anchoredPosition = _tiltedRestPos;
            if (_tiltedGroup != null) _tiltedGroup.alpha = 1f;
            _tiltedChip.gameObject.SetActive(false);
        }

        private void PlayShakeSfx()
        {
            if (_settings == null || _settings.GoldShakeClip == null) return;
            if (!ServiceLocator.TryGetService<IAudioService>(out var audio) || audio == null) return;
            audio.PlaySfx2D(_settings.GoldShakeClip, _settings.SfxVolume);
        }
    }
}
