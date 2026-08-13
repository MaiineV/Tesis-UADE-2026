using System;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Stage que renderiza cada beat del reveal. Cada Play* debe invocar
    /// <paramref name="onDone"/> exactamente una vez cuando su beat termina (el
    /// player tolera duplicados y callbacks tardíos tras un skip/abort).
    /// </summary>
    public interface IChestRevealStage
    {
        /// <summary>Pop del panel + anticipación del cofre.</summary>
        void PlayOpen(Action onDone);

        /// <summary>El spin largo del reel hasta el offset objetivo.</summary>
        void PlaySpin(Action onDone);

        /// <summary>Highlight del ganador + card de recompensa.</summary>
        void PlayReveal(Action onDone);

        /// <summary>Espera el click de dismiss (o un auto-hold del stage).</summary>
        void WaitDismiss(Action onDone);

        /// <summary>Estado final instantáneo: tira en el offset objetivo, card visible.</summary>
        void ForceFinalState();
    }

    /// <summary>
    /// Secuenciador puro del reveal gacha (patrón <c>BreakdownSequencePlayer</c>):
    /// Open → Spin → Reveal → WaitDismiss → Finished. Skip en dos etapas —
    /// primer click acelera (<see cref="SkipStage.Fast"/>, el stage lee
    /// <see cref="Skip"/> y multiplica sus tweens), segundo click salta al estado
    /// final (<see cref="SkipStage.Jump"/>); durante WaitDismiss cualquier click
    /// cierra. Sin corutinas ni tiempo: el tiempo vive en el stage.
    /// </summary>
    public sealed class ChestRevealPlayer
    {
        public enum SkipStage { None, Fast, Jump }

        public enum RevealBeat { Idle, Open, Spin, Reveal, WaitDismiss, Finished }

        public SkipStage Skip { get; private set; }
        public RevealBeat Beat { get; private set; } = RevealBeat.Idle;
        public bool IsRunning => Beat != RevealBeat.Idle && Beat != RevealBeat.Finished;
        public bool Done => Beat == RevealBeat.Finished;

        private IChestRevealStage _stage;
        private Action _onFinished;

        // Invalida onDone viejos tras skip-jump, abort o re-play: un callback creado
        // para un token anterior se ignora — garantía de exactly-once por beat.
        private int _token;

        public void Play(IChestRevealStage stage, Action onFinished)
        {
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _onFinished = onFinished;
            Skip = SkipStage.None;
            _token++;
            EnterBeat(RevealBeat.Open);
        }

        public void RequestSkip()
        {
            if (!IsRunning) return;

            if (Beat == RevealBeat.WaitDismiss)
            {
                _token++;
                EnterBeat(RevealBeat.Finished);
                return;
            }

            if (Skip == SkipStage.None)
            {
                Skip = SkipStage.Fast;
                return;
            }

            if (Skip == SkipStage.Fast)
            {
                Skip = SkipStage.Jump;
                _token++;
                _stage.ForceFinalState();
                EnterBeat(RevealBeat.WaitDismiss);
            }
        }

        /// <summary>Teardown externo (OnDisable/pop): corta sin invocar onFinished.</summary>
        public void Abort()
        {
            _token++;
            _stage = null;
            _onFinished = null;
            Skip = SkipStage.None;
            Beat = RevealBeat.Idle;
        }

        private void EnterBeat(RevealBeat beat)
        {
            Beat = beat;

            if (beat == RevealBeat.Finished)
            {
                var finished = _onFinished;
                _onFinished = null;
                _stage = null;
                finished?.Invoke();
                return;
            }

            int token = _token;
            void OnDone()
            {
                if (token != _token) return;
                _token++;
                EnterBeat(Next(beat));
            }

            switch (beat)
            {
                case RevealBeat.Open: _stage.PlayOpen(OnDone); break;
                case RevealBeat.Spin: _stage.PlaySpin(OnDone); break;
                case RevealBeat.Reveal: _stage.PlayReveal(OnDone); break;
                case RevealBeat.WaitDismiss: _stage.WaitDismiss(OnDone); break;
            }
        }

        private static RevealBeat Next(RevealBeat beat)
        {
            switch (beat)
            {
                case RevealBeat.Open: return RevealBeat.Spin;
                case RevealBeat.Spin: return RevealBeat.Reveal;
                case RevealBeat.Reveal: return RevealBeat.WaitDismiss;
                default: return RevealBeat.Finished;
            }
        }
    }
}
