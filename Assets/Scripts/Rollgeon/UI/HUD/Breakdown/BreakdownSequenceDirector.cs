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
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("_cascade")]
        [SerializeField] private GlobalModifierSpinnerView _spinner;
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

        [Tooltip("Juice de la secuencia (sonidos/flash/shake/partículas). Opcional — " +
                 "fire-and-forget, nunca participa de la cadena onDone.")]
        [SerializeField] private BreakdownJuice _juice;

        private readonly BreakdownSequencePlayer _player = new BreakdownSequencePlayer();
        private int _dieIndex;
        private int _stepIndex;
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

            _dieIndex = 0;
            _stepIndex = 0;
            _juice?.OnSequenceStart(_script.FinalN, _script.FinalM, _script.FinalTotal);
            _juice?.OnComboPlayed(payload.Breakdown, _diceZone);
            CacheCounterHomes();
            PopulateSpinner(_script);
            if (_skipButton != null) _skipButton.gameObject.SetActive(true);

            int? mitigated = ComputeMitigatedTotal(payload, _script.FinalTotal);
            _player.Play(_script, this, mitigated, EndSequence);
            _timeout = StartCoroutine(TimeoutRoutine());
        }

        private void EndSequence()
        {
            _juice?.OnSequenceEnd(); // primero: apaga fuego/duck/dims en los 3 caminos
            if (_timeout != null)
            {
                StopCoroutine(_timeout);
                _timeout = null;
            }
            if (_skipButton != null) _skipButton.gameObject.SetActive(false);
            if (_spinner != null)
            {
                _spinner.ClearEntries();
                _spinner.SetVisible(false);
            }
            RestoreCounters();
            if (_clashRoll.isAlive) _clashRoll.Stop();
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
            float ramp = ConsumeStepRamp();
            if (_playerBase == null || _breakdownView?.CounterN == null) { onDone(); return; }

            // Anticipación: la espada carga (squash de la view) y recién ahí suelta.
            _juice?.OnSwordWindup(_playerBase);
            _playerBase.Punch();
            float windup = D((_settings != null ? _settings.SwordWindupSeconds : 0.1f) * ramp);
            if (windup <= 0f) LaunchPlayerBase(step, ramp, onDone);
            else Tween.Delay(this, windup, d => d.LaunchPlayerBase(step, ramp, onDone));
        }

        private void LaunchPlayerBase(BreakdownStep step, float ramp, Action onDone)
        {
            _juice?.OnFlightDeparted(_playerBase.Anchor, towardM: false, dieIndex: -1);
            Fly(_playerBase.Anchor, _breakdownView.CounterN.Anchor,
                FormatAmount(step), null,
                D((_settings != null ? _settings.FlightSeconds : 0.32f) * ramp), Arc(), () =>
                {
                    ApplyStep(step);
                    Gap(onDone, ramp);
                }, FlightTint(step));
        }

        public void PlayDie(BreakdownStep step, Action onDone)
        {
            float ramp = ConsumeStepRamp();
            var slot = _diceZone != null ? _diceZone.GetSlotView(step.BagSlot) : null;
            var from = ResolveDieAnchor(slot);
            if (from == null || _breakdownView?.CounterN == null) { ApplyStep(step); onDone(); return; }

            int idx = _dieIndex++; // local: los closures de abajo usan ESTE índice (pitch/juice)

            if (slot != null)
                Tween.PunchScale(slot.transform, Vector3.one * 0.12f, D(0.12f * ramp), frequency: 1);
            slot?.SetContribution(null); // el label se "despega": desde acá vuela el valor
            if (slot != null) _juice?.OnDieLaunch(slot, idx);
            _juice?.OnFlightDeparted(from, towardM: false, dieIndex: idx);

            Fly(from, _breakdownView.CounterN.Anchor,
                FormatAmount(step), null,
                D((_settings != null ? _settings.FlightSeconds : 0.32f) * ramp), Arc(), () =>
                {
                    ApplyStep(step, idx);
                    Gap(onDone, ramp);
                }, FlightTint(step), startScale: 1.3f); // el "+N" despega desde su label
        }

        public void PlayDieProc(BreakdownStep step, Action onDone)
        {
            float ramp = ConsumeStepRamp();
            var slot = _diceZone != null ? _diceZone.GetSlotView(step.BagSlot) : null;
            var from = ResolveDieAnchor(slot);
            var target = TargetCounter(step);
            if (from == null || target == null) { ApplyStep(step); onDone(); return; }

            // Popup con presencia: el glow/sonido telegrafiá el proc y recién ahí vuela.
            _juice?.OnProcPopup(from, BreakdownIconResolver.Resolve(step.SourceAsset),
                step.SourceAsset, step.Target == BreakdownTarget.MultM);
            float popup = D((_settings != null ? _settings.ProcPopupSeconds : 0.12f) * ramp);
            if (popup <= 0f) LaunchProc(step, from, target, ramp, onDone);
            else Tween.Delay(this, popup, d => d.LaunchProc(step, from, target, ramp, onDone));
        }

        private void LaunchProc(BreakdownStep step, RectTransform from,
            BreakdownCounterView target, float ramp, Action onDone)
        {
            _juice?.OnFlightDeparted(from, step.Target == BreakdownTarget.MultM, dieIndex: -1);
            Fly(from, target.Anchor, FormatAmount(step),
                BreakdownIconResolver.Resolve(step.SourceAsset),
                D((_settings != null ? _settings.ProcFlightSeconds : 0.38f) * ramp),
                _settings != null ? _settings.ProcFlightArc : 110f,
                () =>
                {
                    ApplyStep(step);
                    Gap(onDone, ramp);
                }, FlightTint(step));
        }

        public void PlayGlobalMod(BreakdownStep step, Action onDone)
        {
            float ramp = ConsumeStepRamp();
            var target = TargetCounter(step);
            if (_spinner == null || _spinner.Count == 0 || target == null)
            {
                ApplyStep(step);
                onDone();
                return;
            }

            // BUG-063: la entrada del tambor es lo único que le dice al jugador QUÉ
            // ítem le está pegando al combo — con game speed x4 + ramp al piso vivía
            // <0.1s. Piso de duración real, salvo skip explícito.
            bool skipping = _player.Skip != BreakdownSequencePlayer.SkipStage.None;
            float minVisible = _settings != null ? _settings.GlobalModMinVisibleSeconds : 0.35f;

            _spinner.SetVisible(true);
            var current = _spinner.Current;
            if (current != null)
            {
                Tween.PunchScale(current.transform, Vector3.one * 0.1f, D(0.1f * ramp), frequency: 1);
                _juice?.OnCascadeTelegraph(current);
            }
            _juice?.OnFlightDeparted(current != null ? current.Rect : null,
                step.Target == BreakdownTarget.MultM, dieIndex: -1);

            float flight = BreakdownFeelMath.FloorUnlessSkipping(
                D((_settings != null ? _settings.ProcFlightSeconds : 0.38f) * ramp),
                minVisible, skipping);
            float spin = BreakdownFeelMath.FloorUnlessSkipping(
                D((_settings != null ? _settings.SpinnerSpinSeconds : 0.22f) * ramp),
                minVisible * 0.6f, skipping);

            Fly(current != null ? current.Rect : null, target.Anchor,
                FormatAmount(step), BreakdownIconResolver.Resolve(step.SourceAsset),
                flight,
                -(_settings != null ? _settings.ProcFlightArc : 110f),
                () =>
                {
                    ApplyStep(step);
                    _juice?.OnCascadeFall();
                    _spinner.AdvanceToNext(spin, onDone);
                }, FlightTint(step));
        }

        public void PlayFinalClash(int finalTotal, Action onDone)
        {
            var n = _breakdownView?.CounterN;
            var m = _breakdownView?.CounterM;
            if (n == null || m == null || _clashAnchor == null)
            {
                _juice?.OnClashImpact(finalTotal, _script?.FinalM ?? 1f);
                ShowClashTotal(finalTotal);
                Gap(onDone);
                return;
            }

            // Wind-up: N y M se separan un toque hacia afuera antes de lanzarse.
            float windup = D(_settings != null ? _settings.ClashWindupSeconds : 0.08f);
            float pixels = _settings != null ? _settings.ClashWindupPixels : 12f;
            if (windup <= 0f || pixels <= 0f || DiceAnim.DiceUiMotionPrefs.ReducedMotion)
            {
                LaunchClashTravel(n, m, finalTotal, onDone);
                return;
            }

            var clashN = ProjectToSibling(n.Anchor, _clashAnchor);
            var clashM = ProjectToSibling(m.Anchor, _clashAnchor);
            var awayN = n.Anchor.anchoredPosition + (n.Anchor.anchoredPosition - clashN).normalized * pixels;
            var awayM = m.Anchor.anchoredPosition + (m.Anchor.anchoredPosition - clashM).normalized * pixels;
            Tween.UIAnchoredPosition(n.Anchor, awayN, windup, Ease.OutQuad);
            Tween.UIAnchoredPosition(m.Anchor, awayM, windup, Ease.OutQuad)
                .OnComplete(this, d => d.LaunchClashTravel(n, m, finalTotal, onDone));
        }

        private void LaunchClashTravel(BreakdownCounterView n, BreakdownCounterView m,
            int finalTotal, Action onDone)
        {
            float travel = D(_settings != null ? _settings.ClashTravelSeconds : 0.22f);

            // N ↗ y M ↖ hacia el punto de choque, acelerando.
            Tween.UIAnchoredPosition(n.Anchor, ProjectToSibling(n.Anchor, _clashAnchor), travel, Ease.InQuad);
            Tween.UIAnchoredPosition(m.Anchor, ProjectToSibling(m.Anchor, _clashAnchor), travel, Ease.InQuad)
                .OnComplete(this, d =>
                {
                    if (d._breakdownView != null) d._breakdownView.Hide();
                    d.RestoreCounters();
                    d._juice?.OnClashImpact(finalTotal, d._script?.FinalM ?? 1f);
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
            if (delta < 0)
            {
                _juice?.OnShieldClank(_clashLabel);
                if (mitigatedTotal > 0) _juice?.OnMitigationShatter(_clashAnchor);
            }
            else
            {
                _juice?.OnWeaknessFlash(_clashLabel);
            }
            var view = _pool != null ? _pool.Rent() : null;
            if (view == null)
            {
                ShowClashTotal(mitigatedTotal);
                onDone();
                return;
            }

            _activeFlight = view;
            var mitigationTint = _settings != null
                ? (delta <= 0 ? _settings.MitigationColor : _settings.WeaknessColor)
                : (Color?)null;
            view.PlayAsGhost(text, _mitigationSprite, Vector2.zero, mitigationTint); // popup corto sobre el layer
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
            if (_spinner != null)
            {
                _spinner.ClearEntries();
                _spinner.SetVisible(false);
            }
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private int LastShownTotal;
        private int _shownClashValue;
        private Tween _clashRoll;
        private bool _clashOutlineApplied;

        private void ShowClashTotal(int total)
        {
            // La semántica de LastShownTotal es inmediata (PlayMitigation calcula su delta
            // contra el valor final YA); solo el label anima el conteo.
            int from = _clashLabel != null && _clashLabel.gameObject.activeSelf ? _shownClashValue : 0;
            LastShownTotal = total;
            if (_clashLabel == null) return;
            _clashLabel.gameObject.SetActive(true);
            // Outline recién acá: el GO arranca inactivo y tocar outlineWidth antes del
            // Awake del TMP (material interno null) tira NRE. SetActive ya lo despertó.
            if (!_clashOutlineApplied)
            {
                _clashOutlineApplied = true;
                _clashLabel.outlineWidth = 0.2f;
                // Outline oscuro de la paleta de la mesa (#0A0A0C) — mismo que ValueTextOutline.mat.
                _clashLabel.outlineColor = new Color32(0x0A, 0x0A, 0x0C, 0xFF);
            }
            if (_clashRoll.isAlive) _clashRoll.Stop();

            float dur = D(_settings != null ? _settings.ClashRollupSeconds : 0.25f);
            bool slam = _player.Skip == BreakdownSequencePlayer.SkipStage.Jump
                        || DiceAnim.DiceUiMotionPrefs.ReducedMotion
                        || dur <= 0.02f || from == total;
            if (slam)
            {
                _shownClashValue = total;
                _clashLabel.text = total.ToString();
                Tween.PunchScale(_clashLabel.transform, Vector3.one * 0.35f, D(0.18f), frequency: 2);
                _juice?.OnClashSlam();
                return;
            }

            // Roll-up rápido from→total con tick acelerando; sirve también para el
            // tick-down de mitigación (from > total cuenta hacia abajo).
            _clashRoll = Tween.Custom(this, from, total, dur, (d, v) =>
            {
                int shown = Mathf.RoundToInt(v);
                if (shown == d._shownClashValue) return;
                d._shownClashValue = shown;
                d._clashLabel.text = shown.ToString();
                d._juice?.OnClashRollupTick(shown, total);
            }, Ease.OutQuad).OnComplete(this, d =>
                Tween.PunchScale(d._clashLabel.transform, Vector3.one * 0.25f, d.D(0.18f), frequency: 2));
        }

        // Impacto de un aporte en su contador: punch proporcional al peso del aporte
        // (Balatro: el score tiembla más cuanto más grande el golpe), jiggle extra si es M.
        private void ApplyStep(BreakdownStep step, int dieIndex = -1)
        {
            var counter = TargetCounter(step);
            if (counter == null) return;

            bool isM = step.Target == BreakdownTarget.MultM;
            float final = isM ? (_script?.FinalM ?? 0f) : (_script?.FinalN ?? 0f);
            float i01 = BreakdownFeelMath.PunchIntensity01(step.Amount, final);
            float intensity = 1f + i01 * ((_settings != null ? _settings.PunchIntensityMax : 2f) - 1f);
            float rot = (_settings != null ? _settings.PunchRotationMaxDegrees : 4f) * i01 * (isM ? 1.5f : 1f);

            counter.AddAndPunch(step.Amount, intensity, rot);
            _juice?.OnCounterImpact(counter, isM, dieIndex,
                BreakdownFeelMath.Accumulate01(counter.Value, final));
        }

        private BreakdownCounterView TargetCounter(BreakdownStep step)
            => step.Target == BreakdownTarget.MultM ? _breakdownView?.CounterM : _breakdownView?.CounterN;

        private static string FormatAmount(BreakdownStep step)
            => step.Target == BreakdownTarget.MultM
                ? "×" + step.Amount.ToString("0.0#")
                : step.Amount.ToString("+0;-0");

        // Todo lo que vuela hereda el color de su destino: azul hacia N, warm hacia M.
        private Color? FlightTint(BreakdownStep step)
        {
            if (_settings == null) return null;
            return step.Target == BreakdownTarget.MultM
                ? _settings.CounterMWarmColor
                : _settings.CounterNColor;
        }

        private void Fly(RectTransform from, RectTransform to, string text, Sprite icon,
            float seconds, float arc, Action onArrive, Color? tint = null, float startScale = 1f)
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
            }, tint, startScale);
        }

        private void Gap(Action onDone, float factor = 1f)
        {
            float gap = D((_settings != null ? _settings.StepGapSeconds : 0.08f) * factor);
            if (gap <= 0f) onDone();
            else Tween.Delay(this, gap, d => onDone());
        }

        // Duración efectiva: el game speed (x1..x8) comprime toda la secuencia y
        // el primer skip multiplica encima. El ramp por step multiplica ANTES de
        // llamar acá ⇒ los tres factores componen.
        private float D(float seconds)
        {
            float speed = Rollgeon.Timing.GameSpeedPrefs.Multiplier;
            if (_player.Skip != BreakdownSequencePlayer.SkipStage.None)
                speed *= _settings != null ? _settings.SkipSpeedMultiplier : 3f;
            return seconds / speed;
        }

        // Factor de tiempo del step actual (y avance del índice): cada paso resuelto
        // acelera un poco los siguientes — Balatro contando un combo. Multiplica ANTES
        // de D() ⇒ compone con skip. Capturarlo en un local antes de los closures.
        // El clash y la mitigación NO rampean: el payoff conserva su ritmo pleno.
        private float ConsumeStepRamp()
        {
            float factor = BreakdownFeelMath.SpeedRampFactor(_stepIndex,
                _settings != null ? _settings.StepSpeedRampPerStep : 0.07f,
                _settings != null ? _settings.StepSpeedFloor : 0.45f);
            _stepIndex++;
            return factor;
        }

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

        private void PopulateSpinner(BreakdownScript script)
        {
            if (_spinner == null) return;
            var entries = new List<(Sprite icon, string label)>();
            for (int i = 0; i < script.Steps.Count; i++)
            {
                var step = script.Steps[i];
                if (step.Kind != BreakdownStepKind.GlobalMod) continue;
                entries.Add((BreakdownIconResolver.Resolve(step.SourceAsset), CascadeLabel(step)));
            }
            _spinner.SetEntries(entries, animated: true);
            _spinner.SetVisible(entries.Count > 0);
        }

        // "Nombre del objeto +X", con el monto en el color de su contador destino — se lee
        // qué aporta cada fuente y a dónde va antes de que vuele.
        private string CascadeLabel(BreakdownStep step)
        {
            string amount = FormatAmount(step);
            var tint = FlightTint(step);
            if (tint.HasValue)
                amount = $"<color=#{ColorUtility.ToHtmlStringRGB(tint.Value)}>{amount}</color>";
            string name = BreakdownIconResolver.ResolveDisplayName(step.SourceAsset);
            return string.IsNullOrEmpty(name) ? amount : $"{name} {amount}";
        }

        private int? ComputeMitigatedTotal(DamageBreakdownComputedPayload payload, int finalTotal)
        {
            // Escudo y curación no pasan por el DamagePipeline — nunca hay paso de mitigación.
            if (payload.Breakdown.Kind != PlayerComboFormulaKind.Damage) return null;
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
