using System.Collections.Generic;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Máquina de estados por dado de una sesión de throw. Pura C# (sin UnityEngine)
    /// para testearla en EditMode sin escena. La consume <see cref="DiceThrowService"/>;
    /// los presenters no la tocan directo.
    /// </summary>
    /// <remarks>
    /// Transiciones válidas por dado:
    /// <c>NotThrown|Settled → Grabbed</c> (guarda el estado previo),
    /// <c>Grabbed → prev</c> (cancel), <c>Grabbed → Flying</c> (release),
    /// <c>Flying → Settled</c>. <see cref="Excluded"/> nunca transiciona.
    /// </remarks>
    public sealed class DiceThrowStateMachine
    {
        private DieThrowState[] _states = System.Array.Empty<DieThrowState>();
        private DieThrowState[] _prev = System.Array.Empty<DieThrowState>();
        private bool _active;

        public int Count => _states.Length;

        public bool IsActive => _active;

        /// <summary>Abre una sesión: cada dado arranca en NotThrown, salvo los excluidos.</summary>
        /// <param name="excludedMask">true = el dado NO participa (held/blocked). Puede ser
        /// null o más corto que <paramref name="diceCount"/> (faltantes = participan).</param>
        public void Begin(int diceCount, bool[] excludedMask = null)
        {
            _states = new DieThrowState[diceCount];
            _prev = new DieThrowState[diceCount];
            for (int i = 0; i < diceCount; i++)
            {
                bool excluded = excludedMask != null && i < excludedMask.Length && excludedMask[i];
                _states[i] = excluded ? DieThrowState.Excluded : DieThrowState.NotThrown;
            }
            _active = true;
        }

        public DieThrowState GetState(int i)
            => (i >= 0 && i < _states.Length) ? _states[i] : DieThrowState.Excluded;

        /// <summary>Agarra el dado si está agarrable (NotThrown o Settled). Guarda el
        /// estado previo para que el cancel lo devuelva exactamente a donde estaba.</summary>
        public bool TryGrab(int i)
        {
            if (!_active || i < 0 || i >= _states.Length) return false;
            var s = _states[i];
            if (s != DieThrowState.NotThrown && s != DieThrowState.Settled) return false;
            _prev[i] = s;
            _states[i] = DieThrowState.Grabbed;
            return true;
        }

        /// <summary>Cancela el agarre: cada Grabbed vuelve a su estado previo.
        /// Devuelve los índices cancelados (para que el presenter tweenee la vuelta).</summary>
        public List<int> CancelGrab()
        {
            var cancelled = new List<int>();
            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i] != DieThrowState.Grabbed) continue;
                _states[i] = _prev[i];
                cancelled.Add(i);
            }
            return cancelled;
        }

        /// <summary>Suelta los agarrados al vuelo. Devuelve los índices lanzados.</summary>
        public List<int> ReleaseGrabbed()
        {
            var released = new List<int>();
            for (int i = 0; i < _states.Length; i++)
            {
                if (_states[i] != DieThrowState.Grabbed) continue;
                _states[i] = DieThrowState.Flying;
                released.Add(i);
            }
            return released;
        }

        public bool MarkSettled(int i)
        {
            if (!_active || i < 0 || i >= _states.Length) return false;
            if (_states[i] != DieThrowState.Flying) return false;
            _states[i] = DieThrowState.Settled;
            return true;
        }

        /// <summary>true cuando todos los dados participantes están Settled.</summary>
        public bool AllSettled
        {
            get
            {
                if (!_active || _states.Length == 0) return false;
                bool any = false;
                for (int i = 0; i < _states.Length; i++)
                {
                    if (_states[i] == DieThrowState.Excluded) continue;
                    any = true;
                    if (_states[i] != DieThrowState.Settled) return false;
                }
                return any;
            }
        }

        /// <summary>Fase global derivada — prioridad: Grabbing &gt; Flying &gt; Settled &gt; Pending.</summary>
        public DiceThrowPhase Phase
        {
            get
            {
                if (!_active) return DiceThrowPhase.Idle;
                bool anyGrabbed = false, anyFlying = false;
                for (int i = 0; i < _states.Length; i++)
                {
                    if (_states[i] == DieThrowState.Grabbed) anyGrabbed = true;
                    else if (_states[i] == DieThrowState.Flying) anyFlying = true;
                }
                if (anyGrabbed) return DiceThrowPhase.Grabbing;
                if (anyFlying) return DiceThrowPhase.Flying;
                if (AllSettled) return DiceThrowPhase.Settled;
                return DiceThrowPhase.Pending;
            }
        }

        public void Reset()
        {
            _states = System.Array.Empty<DieThrowState>();
            _prev = System.Array.Empty<DieThrowState>();
            _active = false;
        }
    }
}
