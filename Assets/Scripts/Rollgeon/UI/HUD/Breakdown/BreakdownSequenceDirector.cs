using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Combat.Damage;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Orquestador de la secuencia de breakdown: escucha
    /// <see cref="DamageBreakdownComputedPayload"/> (emitido en la ventana de BeginPlay,
    /// antes de que ningún efecto corra), levanta <see cref="BreakdownUiGate"/>, reproduce
    /// el guion del <see cref="BreakdownScriptBuilder"/> con
    /// <see cref="BreakdownSequencePlayer"/> y libera el gate al terminar — recién ahí el
    /// <c>FeedbackManager</c> despacha la secuencia real del golpe.
    /// Implementa <see cref="IBreakdownStage"/>: acá viven los tweens, el player decide.
    /// </summary>
    public sealed class BreakdownSequenceDirector : MonoBehaviour, IBreakdownStage
    {
        [SerializeField] private BreakdownAnimSettingsSO _settings;
        [SerializeField] private DamageBreakdownView _breakdownView;
        [SerializeField] private PlayerBaseDamageView _playerBase;
        [SerializeField] private DiceZoneView _diceZone;
        [SerializeField] private GlobalModifierCascadeView _cascade;
        [SerializeField] private FlyingValuePool _pool;

        [Tooltip("Punto de choque de N y M (centro-arriba del board).")]
        [SerializeField] private RectTransform _clashAnchor;

        [Tooltip("Label del total final, hijo del ClashAnchor. Desactivado por default.")]
        [SerializeField] private TextMeshProUGUI _clashLabel;

        [Tooltip("Botón full-screen invisible, activo solo durante la secuencia: " +
                 "1er click acelera, 2do salta al choque.")]
        [SerializeField] private Button _skipButton;

        [Tooltip("Sprite del popup de mitigación post-choque (escudo). Opcional.")]
        [SerializeField] private Sprite _mitigationSprite;

        private readonly BreakdownSequencePlayer _player = new BreakdownSequencePlayer();
        private Guid _playerGuid;
        private bool _bound;
        private Action<DamageBreakdownComputedPayload> _onBreakdown;
        private bool _gateHeld;
        private FlyingValueView _activeFlight;
        private Coroutine _timeout;
        private BreakdownScript _script;
        private Vector2 _counterNHome, _counterMHome;
        private bool _homesCached;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;
            _onBreakdown = HandleBreakdownComputed;
            TypedEvent<DamageBreakdownComputedPayload>.Subscribe(_onBreakdown);
            if (_skipButton != null)
            {
                _skipButton.onClick.AddListener(HandleSkipPressed);
                _skipButton.gameObject.SetActive(false);
            }
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (_onBreakdown != null)
            {
                TypedEvent<DamageBreakdownComputedPayload>.Unsubscribe(_onBreakdown);
                _onBreakdown = null;
            }
            if (_skipButton != null) _skipButton.onClick.RemoveListener(HandleSkipPressed);
            AbortSequence();
            _bound = false;
        }

        private void OnDisable() => AbortSequence();

        // ==================================================================
        // Arranque / cierre
        // ==================================================================

        private void HandleBreakdownComputed(DamageBreakdownComputedPayload payload)
        {
            if (payload.SourceGuid != _playerGuid) return;
            if (_player.IsRunning) return; // un breakdown a la vez (no debería pasar)

            _script = BreakdownScriptBuilder.Build(payload.Breakdown);
            if (!_script.Reconciled)
                Debug.LogWarning("[BreakdownSequenceDirector] El guion no reconcilia contra " +
                                 "los finales de la fórmula (fuente sin journal): los valores " +
                                 "finales mandan.", this);

            BreakdownUiGate.Begin();
            _gateHeld = true;

            CacheCounterHomes();
            PopulateCascade(_script);
            if (_skipButton != null) _skipButton.gameObject.SetActive(true);

            int? mitigated = ComputeMitigatedTotal(payload, _script.FinalTotal);
            _player.Play(_script, this, mitigated, EndSequence);
            _timeout = StartCoroutine(TimeoutRoutine());
        }

        private void EndSequence()
        {
            if (_timeout != null)
            {
                StopCoroutine(_timeout);
                _timeout = null;
            }
            if (_skipButton != null) _skipButton.gameObject.SetActive(false);
            if (_cascade != null)
            {
                _cascade.ClearEntries();
                _cascade.SetVisible(false);
            }
            RestoreCounters();
            if (_clashLabel != null) _clashLabel.gameObject.SetActive(false);
            ReleaseGate();
        }

        private void AbortSequence()
        {
            if (!_player.IsRunning && !_gateHeld) return;
            _player.Abort();
            _activeFlight = null;
            EndSequence();
        }

        private void ReleaseGate()
        {
            if (!_gateHeld) return;
            _gateHeld = false;
            BreakdownUiGate.End();
        }

        private IEnumerator TimeoutRoutine()
        {
            float max = _settings != null ? _settings.MaxSequenceSeconds : 8f;
            yield return new WaitForSeconds(max);
            if (_player.Done) yield break;
            Debug.LogWarning("[BreakdownSequenceDirector] Secuencia vencida a los " + max +
                             "s — estado final forzado y gate liberado.", this);
            _player.Abort();
            ForceFinalState(_script.FinalN, _script.FinalM);
            EndSequence();
        }

        private void HandleSkipPressed()
        {
            _player.RequestSkip();
            if (_player.Skip == BreakdownSequencePlayer.SkipStage.Jump)
                _activeFlight?.CompleteInstantly();
        }

        // ==================================================================
        // IBreakdownStage — la capa visual que el player dirige
        // ==================================================================

        public void ShowCounters(int initialN, float initialM)
        {
            if (_breakdownView == null) return;
            _breakdownView.ShowPreview(initialN, initialM);
            _breakdownView.CounterN?.Punch();
        }

        public void PlayPlayerBase(BreakdownStep step, Action onDone)
        {
            if (_playerBase == null || _breakdownView?.CounterN == null) { onDone(); return; }
            _playerBase.Punch();
            Fly(_playerBase.Anchor, _breakdownView.CounterN.Anchor,
                FormatAmount(step), null, FlightSeconds(), Arc(), () =>
                {
                    ApplyStep(step);
                    Gap(onDone);
                });
        }

        public void PlayDie(BreakdownStep step, Action onDone)
        {
            var slot = _diceZone != null ? _diceZone.GetSlotView(step.BagSlot) : null;
            var from = ResolveDieAnchor(slot);
            if (from == null || _breakdownView?.CounterN == null) { ApplyStep(step); onDone(); return; }

            if (slot != null)
                Tween.PunchScale(slot.transform, Vector3.one * 0.12f, D(0.12f), frequency: 1);
            slot?.SetContribution(null); // el label se "despega": desde acá vuela el valor

            Fly(from, _breakdownView.CounterN.Anchor,
                FormatAmount(step), null, FlightSeconds(), Arc(), () =>
                {
                    ApplyStep(step);
                    Gap(onDone);
                });
        }

        public void PlayDieProc(BreakdownStep step, Action onDone)
        {
            var slot = _diceZone != null ? _diceZone.GetSlotView(step.BagSlot) : null;
            var from = ResolveDieAnchor(slot);
            var target = TargetCounter(step);
            if (from == null || target == null) { ApplyStep(step); onDone(); return; }

            Fly(from, target.Anchor, FormatAmount(step),
                BreakdownIconResolver.Resolve(step.SourceAsset),
                D(_settings != null ? _settings.ProcFlightSeconds : 0.38f),
                _settings != null ? _settings.ProcFlightArc : 110f,
                () =>
                {
                    ApplyStep(step);
                    Gap(onDone);
                });
        }

        public void PlayGlobalMod(BreakdownStep step, Action onDone)
        {
            var target = TargetCounter(step);
            if (_cascade == null || _cascade.Count == 0 || target == null)
            {
                ApplyStep(step);
                onDone();
                return;
            }

            _cascade.SetVisible(true);
            var bottom = _cascade.Bottom;
            if (bottom != null)
                Tween.PunchScale(bottom.transform, Vector3.one * 0.1f, D(0.1f), frequency: 1);

            Fly(bottom != null ? bottom.Rect : _cascade.Bottom?.Rect, target.Anchor,
                FormatAmount(step), BreakdownIconResolver.Resolve(step.SourceAsset),
                D(_settings != null ? _settings.ProcFlightSeconds : 0.38f),
                -(_settings != null ? _settings.ProcFlightArc : 110f),
                () =>
                {
                    ApplyStep(step);
                    _cascade.RemoveBottom(D(_settings != null ? _settings.CascadeFallSeconds : 0.15f), onDone);
                });
        }

        public void PlayFinalClash(int finalTotal, Action onDone)
        {
            var n = _breakdownView?.CounterN;
            var m = _breakdownView?.CounterM;
            if (n == null || m == null || _clashAnchor == null)
            {
                ShowClashTotal(finalTotal);
                Gap(onDone);
                return;
            }

            float travel = D(_settings != null ? _settings.ClashTravelSeconds : 0.22f);
            var clashLocal = _clashAnchor;

            // N ↗ y M ↖ hacia el punto de choque, acelerando.
            Tween.UIAnchoredPosition(n.Anchor, ProjectToSibling(n.Anchor, clashLocal), travel, Ease.InQuad);
            Tween.UIAnchoredPosition(m.Anchor, ProjectToSibling(m.Anchor, clashLocal), travel, Ease.InQuad)
                .OnComplete(this, d =>
                {
                    if (d._breakdownView != null) d._breakdownView.Hide();
                    d.RestoreCounters();
                    d.ShowClashTotal(finalTotal);
                    float hold = d.D(d._settings != null ? d._settings.ClashHoldSeconds : 0.4f);
                    if (hold <= 0f) onDone();
                    else Tween.Delay(d, hold, x => onDone());
                });
        }

        public void PlayMitigation(int mitigatedTotal, Action onDone)
        {
            if (_clashLabel == null || _clashAnchor == null)
            {
                onDone();
                return;
            }

            // El "-X" (o el ajuste de debilidad) cae sobre el total y lo actualiza: la
            // reducción del golpe real nunca es "mágica" — se explica acá.
            int delta = mitigatedTotal - LastShownTotal;
            string text = delta <= 0 ? delta.ToString("+0;-0") : "×!"; // debilidad sube: marca distinta
            var view = _pool != null ? _pool.Rent() : null;
            if (view == null)
            {
                ShowClashTotal(mitigatedTotal);
                onDone();
                return;
            }

            _activeFlight = view;
            view.PlayAsGhost(text, _mitigationSprite, Vector2.zero); // popup corto sobre el layer
            Tween.Delay(this, D(_settings != null ? _settings.MitigationSeconds : 0.3f), d =>
            {
                d._activeFlight = null;
                d.ShowClashTotal(mitigatedTotal);
                d.Gap(onDone);
            });
        }

        public void ForceFinalState(int finalN, float finalM)
        {
            _breakdownView?.CounterN?.SetValue(finalN, isMultiplier: false);
            _breakdownView?.CounterM?.SetValue(finalM, isMultiplier: true);
            if (_cascade != null)
            {
                _cascade.ClearEntries();
                _cascade.SetVisible(false);
            }
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private int LastShownTotal;

        private void ShowClashTotal(int total)
        {
            LastShownTotal = total;
            if (_clashLabel == null) return;
            _clashLabel.text = total.ToString();
            _clashLabel.gameObject.SetActive(true);
            Tween.PunchScale(_clashLabel.transform, Vector3.one * 0.25f, D(0.18f), frequency: 2);
        }

        private void ApplyStep(BreakdownStep step)
        {
            var counter = TargetCounter(step);
            counter?.AddAndPunch(step.Amount);
        }

        private BreakdownCounterView TargetCounter(BreakdownStep step)
            => step.Target == BreakdownTarget.MultM ? _breakdownView?.CounterM : _breakdownView?.CounterN;

        private static string FormatAmount(BreakdownStep step)
            => step.Target == BreakdownTarget.MultM
                ? "×" + step.Amount.ToString("0.0#")
                : step.Amount.ToString("+0;-0");

        private void Fly(RectTransform from, RectTransform to, string text, Sprite icon,
            float seconds, float arc, Action onArrive)
        {
            var view = _pool != null ? _pool.Rent() : null;
            if (view == null || from == null || to == null)
            {
                onArrive();
                return;
            }
            _activeFlight = view;
            view.Fly(from, to, text, icon, seconds, arc, () =>
            {
                _activeFlight = null;
                onArrive();
            });
        }

        private void Gap(Action onDone)
        {
            float gap = D(_settings != null ? _settings.StepGapSeconds : 0.08f);
            if (gap <= 0f) onDone();
            else Tween.Delay(this, gap, d => onDone());
        }

        // Duración efectiva: el primer skip acelera todo lo que falta.
        private float D(float seconds)
            => _player.Skip == BreakdownSequencePlayer.SkipStage.None
                ? seconds
                : seconds / (_settings != null ? _settings.SkipSpeedMultiplier : 3f);

        private float FlightSeconds() => D(_settings != null ? _settings.FlightSeconds : 0.32f);
        private float Arc() => _settings != null ? _settings.FlightArc : 60f;

        private static RectTransform ResolveDieAnchor(DiceSlotView slot)
        {
            if (slot == null) return null;
            var contribution = slot.ContributionLabel;
            return contribution != null ? contribution.Anchor : (RectTransform)slot.transform;
        }

        // Posición del clash anchor expresada en el espacio del padre del contador (para
        // tweenear anchoredPosition directo).
        private static Vector2 ProjectToSibling(RectTransform mover, RectTransform target)
        {
            var parent = (RectTransform)mover.parent;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local);
            return local;
        }

        private void CacheCounterHomes()
        {
            if (_homesCached || _breakdownView?.CounterN == null || _breakdownView.CounterM == null) return;
            _counterNHome = _breakdownView.CounterN.Anchor.anchoredPosition;
            _counterMHome = _breakdownView.CounterM.Anchor.anchoredPosition;
            _homesCached = true;
        }

        private void RestoreCounters()
        {
            if (!_homesCached) return;
            if (_breakdownView?.CounterN != null)
                _breakdownView.CounterN.Anchor.anchoredPosition = _counterNHome;
            if (_breakdownView?.CounterM != null)
                _breakdownView.CounterM.Anchor.anchoredPosition = _counterMHome;
        }

        private void PopulateCascade(BreakdownScript script)
        {
            if (_cascade == null) return;
            var entries = new List<(Sprite icon, string label)>();
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var step = script.Steps[i];
                if (step.Kind != BreakdownStepKind.GlobalMod) continue;
                entries.Add((BreakdownIconResolver.Resolve(step.SourceAsset), FormatAmount(step)));
            }
            _cascade.SetEntries(entries);
            _cascade.SetVisible(entries.Count > 0);
        }

        private int? ComputeMitigatedTotal(DamageBreakdownComputedPayload payload, int finalTotal)
        {
            if (payload.TargetGuid == Guid.Empty || finalTotal <= 0) return null;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null)
                return null;

            var ctx = new DamageContext
            {
                SourceId = payload.SourceGuid,
                TargetId = payload.TargetGuid,
                BaseDamage = finalTotal,
                ComboId = payload.ComboId,
                IsWeaknessHit = !string.IsNullOrEmpty(payload.ComboId),
            };
            pipeline.Preview(ctx);
            return ctx.FinalDamage != finalTotal ? ctx.FinalDamage : (int?)null;
        }
    }
}
