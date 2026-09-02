using System;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Lo que el reproductor le pide a la capa visual. El director real lo implementa con
    /// tweens; los tests usan un fake sincrónico. Cada método DEBE invocar su
    /// <c>onDone</c> exactamente una vez.
    /// </summary>
    public interface IBreakdownStage
    {
        void ShowCounters(int initialN, float initialM);
        void PlayPlayerBase(BreakdownStep step, Action onDone);
        void PlayDie(BreakdownStep step, Action onDone);
        void PlayDieProc(BreakdownStep step, Action onDone);
        void PlayGlobalMod(BreakdownStep step, Action onDone);
        void PlayFinalClash(int finalTotal, Action onDone);
        /// <summary>Mitigación visible post-choque (solo si difiere del total crudo).</summary>
        void PlayMitigation(int mitigatedTotal, Action onDone);
        /// <summary>
        /// Choque del total contra un umbral sobre el candado (Forzar Puerta). Reemplaza a
        /// la mitigación cuando hay umbral; el stage decide abierto/cerrado con
        /// <c>total &gt;= threshold</c>.
        /// </summary>
        void PlayThresholdClash(int finalTotal, int threshold, Action onDone);
        /// <summary>Skip total: pinta los contadores en su estado final, sin animar.
        /// N es float — el base damage override puede aportar fracción (Furia).</summary>
        void ForceFinalState(float finalN, float finalM);
    }

    /// <summary>
    /// Reproductor del guion de breakdown: avanza paso a paso vía callbacks, maneja el
    /// skip en dos etapas (1º acelera, 2º salta al choque con valores finales) y garantiza
    /// <c>onFinished</c> exactamente una vez. POCO sin UnityEngine — testeable en EditMode.
    /// </summary>
    public sealed class BreakdownSequencePlayer
    {
        public enum SkipStage { None, Fast, Jump }

        private BreakdownScript _script;
        private IBreakdownStage _stage;
        private Action _onFinished;
        private int? _mitigatedTotal;
        private int? _threshold;
        private int _index;
        private bool _running;
        private bool _clashPlayed;

        public SkipStage Skip { get; private set; }
        public bool Done { get; private set; }
        public bool IsRunning => _running;

        public void Play(BreakdownScript script, IBreakdownStage stage,
            int? mitigatedTotal, Action onFinished)
            => Play(script, stage, mitigatedTotal, null, onFinished);

        /// <param name="threshold">Umbral de Forzar Puerta: con valor, tras el choque N×M
        /// corre <see cref="IBreakdownStage.PlayThresholdClash"/> en vez de la mitigación.
        /// El salto (skip Jump) también pasa por él: el resultado del candado siempre se ve.</param>
        public void Play(BreakdownScript script, IBreakdownStage stage,
            int? mitigatedTotal, int? threshold, Action onFinished)
        {
            _script = script;
            _stage = stage;
            _onFinished = onFinished;
            _mitigatedTotal = mitigatedTotal;
            _threshold = threshold;
            _index = 0;
            _clashPlayed = false;
            Done = false;
            Skip = SkipStage.None;
            _running = true;

            _stage.ShowCounters(script.InitialN, script.InitialM);
            Advance();
        }

        /// <summary>1º click: acelera (el stage consulta <see cref="Skip"/>). 2º: salta al choque.</summary>
        public void RequestSkip()
        {
            if (!_running) return;
            if (Skip == SkipStage.None) Skip = SkipStage.Fast;
            else if (Skip == SkipStage.Fast) Skip = SkipStage.Jump;
        }

        /// <summary>Corta sin invocar <c>onFinished</c> (teardown externo — el director limpia).</summary>
        public void Abort() => _running = false;

        private void Advance()
        {
            if (!_running) return;

            // El salto siempre pasa por el choque, así el valor final se lee.
            if (Skip == SkipStage.Jump && !_clashPlayed)
            {
                _stage.ForceFinalState(_script.FinalN, _script.FinalM);
                PlayClash();
                return;
            }

            if (_index < _script.Steps.Count)
            {
                var step = _script.Steps[_index++];
                switch (step.Kind)
                {
                    case BreakdownStepKind.PlayerBase: _stage.PlayPlayerBase(step, Advance); break;
                    case BreakdownStepKind.Die: _stage.PlayDie(step, Advance); break;
                    case BreakdownStepKind.DieProc: _stage.PlayDieProc(step, Advance); break;
                    case BreakdownStepKind.GlobalMod: _stage.PlayGlobalMod(step, Advance); break;
                    default: Advance(); break;
                }
                return;
            }

            if (!_clashPlayed)
            {
                PlayClash();
                return;
            }

            Finish();
        }

        private void PlayClash()
        {
            _clashPlayed = true;
            _stage.PlayFinalClash(_script.FinalTotal, () =>
            {
                if (!_running) return;
                if (_threshold.HasValue)
                    _stage.PlayThresholdClash(_script.FinalTotal, _threshold.Value, Finish);
                else if (_mitigatedTotal.HasValue && _mitigatedTotal.Value != _script.FinalTotal)
                    _stage.PlayMitigation(_mitigatedTotal.Value, Finish);
                else
                    Finish();
            });
        }

        private void Finish()
        {
            if (!_running || Done) return;
            Done = true;
            _running = false;
            _onFinished?.Invoke();
        }
    }
}
