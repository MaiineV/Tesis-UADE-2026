using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Combat.Actions;
using Rollgeon.Items;
using Rollgeon.Localization;
using Rollgeon.Timing;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Vista del reveal gacha del cofre. Vive persistente bajo <c>ScreenHost</c> en
    /// <c>Canvas_ChestReveal.prefab</c> con <see cref="_root"/> apagado (patrón
    /// <c>BossBarView</c> — NO entra al screen stack: <c>PushOverlay</c> es alias de
    /// <c>Push</c> y taparía el CombatHUD). El dim con raycast bloquea el input; cada
    /// click sobre él avanza el skip de dos etapas del <see cref="ChestRevealPlayer"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Gate de turnos.</b> Por cada <see cref="ChestOpenedPayload"/> encolado se
    /// llama <see cref="TurnManager.BeginFeedbackWait"/> una vez, y se libera con
    /// <see cref="TurnManager.OnFeedbackComplete"/> exactamente una vez al terminar
    /// (o en el watchdog / teardown) — el combate no avanza mientras el jugador mira
    /// la ruleta y nunca queda soft-lockeado.
    /// </para>
    /// <para>
    /// <b>Pacing.</b> Todas las duraciones pasan por <see cref="D"/>: divididas por
    /// <see cref="GameSpeedPrefs.Multiplier"/> (nunca <c>Time.timeScale</c>) y por el
    /// skip Fast. <c>ReducedMotion</c> salta directo al estado final.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Chest Reveal/Chest Reveal View")]
    public sealed class ChestRevealView : MonoBehaviour, IChestRevealStage
    {
        [Title("Settings")]
        [SerializeField, Required] private ChestRevealUiSettingsSO _settings;

        [Title("Refs")]
        [SerializeField, Required] private GameObject _root;
        [SerializeField, Required] private RectTransform _panelRect;
        [SerializeField, Required] private CanvasGroup _panelCanvasGroup;
        [SerializeField, Required] private Button _dimBackground;
        [SerializeField, Required] private RectTransform _viewport;
        [SerializeField, Required] private RectTransform _stripRoot;
        [SerializeField, Required] private ChestReelCellView _cellPrefab;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _rewardNameLabel;
        [SerializeField] private CanvasGroup _rewardCardGroup;
        [SerializeField] private TMP_Text _hintLabel;
        [SerializeField] private ChestRevealJuice _juice; // opcional

        private readonly Queue<ChestOpenedPayload> _queue = new Queue<ChestOpenedPayload>();
        private readonly List<ChestReelCellView> _cells = new List<ChestReelCellView>();
        private readonly ChestRevealPlayer _player = new ChestRevealPlayer();
        private readonly System.Random _rng = new System.Random();

        private Action<ChestOpenedPayload> _onChestOpened;
        private ChestOpenedPayload _current;
        private int _winnerIndex;
        private float _targetOffset;
        private float _lastSpinT;
        private int _lastTickIndex = -1;
        private Tween _spinTween;
        private Action _spinOnDone;
        private Coroutine _watchdog;
        private int _gatesHeld;

        private static bool ReducedMotion => DiceUiMotionPrefs.ReducedMotion;

        /// <summary>Duración efectiva: settings ÷ velocidad de juego ÷ skip Fast.</summary>
        private float D(float seconds)
        {
            float value = seconds / Mathf.Max(1, GameSpeedPrefs.Multiplier);
            if (_player.Skip == ChestRevealPlayer.SkipStage.Fast)
                value /= Mathf.Max(1f, _settings.SkipSpeedMultiplier);
            return value;
        }

        private void Awake()
        {
            _onChestOpened = HandleChestOpened;
            TypedEvent<ChestOpenedPayload>.Subscribe(_onChestOpened);
            if (_root != null) _root.SetActive(false);
            if (_dimBackground != null) _dimBackground.onClick.AddListener(OnDimClicked);
        }

        private void OnDestroy()
        {
            if (_onChestOpened != null)
            {
                TypedEvent<ChestOpenedPayload>.Unsubscribe(_onChestOpened);
                _onChestOpened = null;
            }
            Teardown();
        }

        private void OnDisable() => Teardown();

        // -----------------------------------------------------------------
        // Cola / gate
        // -----------------------------------------------------------------

        private void HandleChestOpened(ChestOpenedPayload payload)
        {
            _queue.Enqueue(payload);

            // Un gate por payload — el combate espera mientras la ruleta gira. El
            // cofre no pasa por el death-feedback de enemigos, así que no hay
            // animación previa que esperar: si no hay secuencia en curso, se abre ya.
            if (ServiceLocator.TryGetService<TurnManager>(out var turn) && turn != null)
            {
                turn.BeginFeedbackWait();
                _gatesHeld++;
            }

            if (!_player.IsRunning) OpenNext();
        }

        private void OpenNext()
        {
            if (_queue.Count == 0) return;
            _current = _queue.Dequeue();

            if (_root != null) _root.SetActive(true);
            BuildReel();
            ApplyStaticTexts();
            if (_rewardCardGroup != null) _rewardCardGroup.alpha = 0f;

            _player.Play(this, OnSequenceEnd);

            if (_watchdog != null) StopCoroutine(_watchdog);
            _watchdog = StartCoroutine(Watchdog());
        }

        private void OnSequenceEnd()
        {
            if (_watchdog != null)
            {
                StopCoroutine(_watchdog);
                _watchdog = null;
            }
            ReleaseOneGate();

            if (_queue.Count > 0)
            {
                OpenNext();
                return;
            }
            CloseRoot();
        }

        // Red de seguridad: si un tween muere o una ref se rompe, el estado final se
        // fuerza y el gate se libera igual — jamás un combate colgado por la ruleta.
        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, _settings.MaxSequenceSeconds));
            if (!_player.IsRunning) yield break;

            Debug.LogWarning("[ChestRevealView] Watchdog: secuencia colgada — se fuerza el cierre.");
            _player.Abort();
            ForceFinalState();
            OnSequenceEnd();
        }

        private void ReleaseOneGate()
        {
            if (_gatesHeld <= 0) return;
            _gatesHeld--;
            if (ServiceLocator.TryGetService<TurnManager>(out var turn) && turn != null)
                turn.OnFeedbackComplete();
        }

        private void Teardown()
        {
            _player.Abort();
            if (_watchdog != null)
            {
                StopCoroutine(_watchdog);
                _watchdog = null;
            }
            _spinTween.Stop();
            _queue.Clear();
            // Liberar TODOS los gates pendientes — un teardown a mitad de reveal no
            // puede dejar el TurnManager esperando.
            while (_gatesHeld > 0) ReleaseOneGate();
            if (_root != null) _root.SetActive(false);
        }

        private void CloseRoot()
        {
            if (_root == null) return;
            if (ReducedMotion || _panelCanvasGroup == null)
            {
                _root.SetActive(false);
                return;
            }
            _panelCanvasGroup.blocksRaycasts = false;
            Tween.Alpha(_panelCanvasGroup, 0f, D(_settings.CloseSeconds), useUnscaledTime: true)
                .OnComplete(this, self =>
                {
                    self._root.SetActive(false);
                    self._panelCanvasGroup.alpha = 1f;
                    self._panelCanvasGroup.blocksRaycasts = true;
                });
        }

        // -----------------------------------------------------------------
        // Construcción del reel
        // -----------------------------------------------------------------

        private void BuildReel()
        {
            float w = _settings.CellWidth;
            float s = _settings.CellSpacing;

            var winner = _current.Item != null
                ? ChestReelCellData.ForItem(_current.Item, isWinner: true)
                : ChestReelCellData.ForGold(_current.GoldAmount, _current.Tier, isWinner: true);

            _winnerIndex = ChestReelMath.PickWinnerIndex(_settings.TotalCells, _settings.MinSpinCells, _rng);
            var strip = ChestReelBuilder.BuildStrip(
                winner, _current.PoolPreview, _current.Tier,
                _settings.TotalCells, _winnerIndex,
                _settings.GoldFillerPerMille, _settings.GoldFillerMin, _settings.GoldFillerMax, _rng);

            EnsureCellCount(strip.Count);
            for (int i = 0; i < strip.Count; i++)
            {
                var cell = _cells[i];
                cell.gameObject.SetActive(true);
                cell.Bind(strip[i]);
                cell.Rect.anchoredPosition = new Vector2(ChestReelMath.CellCenterX(i, w, s), 0f);
                cell.Rect.localScale = Vector3.one;
            }
            for (int i = strip.Count; i < _cells.Count; i++)
            {
                _cells[i].gameObject.SetActive(false);
            }

            float jitter = (float)(_rng.NextDouble() * 2.0 - 1.0);
            _targetOffset = ChestReelMath.TargetOffset(_winnerIndex, w, s, jitter, _settings.MaxLandingJitter01);
            _lastTickIndex = -1;
            _lastSpinT = 0f;
            SetOffset(0f);
        }

        private void EnsureCellCount(int count)
        {
            while (_cells.Count < count)
            {
                var cell = Instantiate(_cellPrefab, _stripRoot);
                _cells.Add(cell);
            }
        }

        // La tira corre hacia la izquierda: el centro de la celda "offset" queda bajo
        // el puntero (centro del viewport).
        private void SetOffset(float offset)
        {
            float pointerX = _viewport != null ? _viewport.rect.width * 0.5f : 0f;
            if (_stripRoot != null)
                _stripRoot.anchoredPosition = new Vector2(pointerX - offset, _stripRoot.anchoredPosition.y);

            int index = ChestReelMath.CellIndexAtOffset(
                offset, _settings.CellWidth, _settings.CellSpacing, _settings.TotalCells);
            if (index != _lastTickIndex)
            {
                _lastTickIndex = index;
                _juice?.OnReelTick(index);
            }
        }

        private void ApplyStaticTexts()
        {
            if (_titleLabel != null)
            {
                string tierName = LocalizedContent.Ui(
                    ChestRevealTextKeys.RarityKey(_current.Tier), _current.Tier.ToString());
                string title = LocalizedContent.Ui(ChestRevealTextKeys.Title, "Chest opened!");
                _titleLabel.text = $"{title} — {tierName}";
            }
            if (_hintLabel != null)
                _hintLabel.text = LocalizedContent.Ui(ChestRevealTextKeys.SkipHint, "Click to skip");
        }

        private void ApplyRewardCard()
        {
            if (_rewardNameLabel == null) return;
            if (_current.Item != null)
            {
                string fallback = !string.IsNullOrEmpty(_current.Item.DisplayName)
                    ? _current.Item.DisplayName
                    : _current.Item.ItemId;
                _rewardNameLabel.text = LocalizedContent.Name(_current.Item.ItemId, fallback);
            }
            else
            {
                string format = LocalizedContent.Ui(ChestRevealTextKeys.GoldAmount, "+{0}");
                _rewardNameLabel.text = string.Format(format, _current.GoldAmount);
            }
        }

        private void OnDimClicked()
        {
            if (!_player.IsRunning) return;

            bool wasSpinNoSkip = _player.Beat == ChestRevealPlayer.RevealBeat.Spin
                                 && _player.Skip == ChestRevealPlayer.SkipStage.None;
            _player.RequestSkip();

            // Fast durante el spin: se re-tweenea el resto de la distancia acelerado.
            if (wasSpinNoSkip
                && _player.Skip == ChestRevealPlayer.SkipStage.Fast
                && _spinTween.isAlive)
            {
                _spinTween.Stop();
                StartSpinTween(_lastSpinT);
            }
        }

        // -----------------------------------------------------------------
        // IChestRevealStage
        // -----------------------------------------------------------------

        public void PlayOpen(Action onDone)
        {
            if (ReducedMotion || _panelRect == null || _panelCanvasGroup == null)
            {
                if (_panelRect != null) _panelRect.localScale = Vector3.one;
                if (_panelCanvasGroup != null) _panelCanvasGroup.alpha = 1f;
                onDone();
                return;
            }

            Tween.StopAll(onTarget: _panelRect);
            Tween.StopAll(onTarget: _panelCanvasGroup);
            _panelRect.localScale = Vector3.one * _settings.OpenScaleFrom;
            _panelCanvasGroup.alpha = 0f;
            Tween.Scale(_panelRect, Vector3.one, D(_settings.OpenSeconds), Ease.OutBack, useUnscaledTime: true);
            Tween.Alpha(_panelCanvasGroup, 1f, D(_settings.OpenSeconds), Ease.OutQuad, useUnscaledTime: true)
                .OnComplete(onDone.Invoke);
        }

        public void PlaySpin(Action onDone)
        {
            if (ReducedMotion)
            {
                SetOffset(_targetOffset);
                onDone();
                return;
            }
            _spinOnDone = onDone;
            StartSpinTween(fromT: 0f);
        }

        // El tween anima t lineal 0→1 y la curva mapea la desaceleración — así el
        // skip Fast puede re-tweenear el resto de t sin recomputar distancias.
        private void StartSpinTween(float fromT)
        {
            float remaining = Mathf.Clamp01(1f - fromT);
            float duration = D(_settings.SpinSeconds) * remaining;
            _spinTween = Tween.Custom(this, fromT, 1f, duration, (self, t) =>
                {
                    self._lastSpinT = t;
                    self.SetOffset(self.EvaluateSpin(t) * self._targetOffset);
                }, Ease.Linear, useUnscaledTime: true)
                .OnComplete(this, self =>
                {
                    var done = self._spinOnDone;
                    self._spinOnDone = null;
                    done?.Invoke();
                });
        }

        private float EvaluateSpin(float t)
        {
            var curve = _settings.DecelerationCurve;
            return curve != null && curve.length > 0 ? curve.Evaluate(t) : ChestReelMath.Decelerate01(t);
        }

        public void PlayReveal(Action onDone)
        {
            ApplyRewardCard();
            var winnerRect = _winnerIndex < _cells.Count ? _cells[_winnerIndex].Rect : null;
            _juice?.OnRewardRevealed(winnerRect, _current.Tier);

            if (ReducedMotion || _rewardCardGroup == null)
            {
                if (_rewardCardGroup != null) _rewardCardGroup.alpha = 1f;
                onDone();
                return;
            }

            Tween.StopAll(onTarget: _rewardCardGroup);
            _rewardCardGroup.alpha = 0f;
            Tween.Alpha(_rewardCardGroup, 1f, D(_settings.RevealFadeSeconds), Ease.OutQuad, useUnscaledTime: true)
                .OnComplete(onDone.Invoke);
        }

        public void WaitDismiss(Action onDone)
        {
            if (_hintLabel != null)
                _hintLabel.text = LocalizedContent.Ui(ChestRevealTextKeys.ContinueHint, "Click to continue");

            // 0 = espera el click (el player cierra vía RequestSkip); >0 = auto-cierre.
            if (_settings.AutoDismissSeconds > 0f)
                Tween.Delay(D(_settings.AutoDismissSeconds), onDone.Invoke, useUnscaledTime: true);
        }

        public void ForceFinalState()
        {
            _spinTween.Stop();
            _spinOnDone = null;
            SetOffset(_targetOffset);
            if (_panelRect != null) _panelRect.localScale = Vector3.one;
            if (_panelCanvasGroup != null) _panelCanvasGroup.alpha = 1f;
            ApplyRewardCard();
            if (_rewardCardGroup != null) _rewardCardGroup.alpha = 1f;
        }
    }
}
