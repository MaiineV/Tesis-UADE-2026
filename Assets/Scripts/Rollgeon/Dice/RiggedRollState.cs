namespace Rollgeon.Dice
{
    /// <summary>
    /// Estado opcional para "riggear" la próxima tirada (DevConsole). El <see cref="DiceRoller"/>
    /// lo consulta antes de rolar: si hay caras encoladas las usa (clamp a MaxFace), si no rola
    /// normal. One-shot — se consume en la primera tirada. No-op si nadie lo registró.
    /// </summary>
    public sealed class RiggedRollState
    {
        private int[] _next;

        public bool HasPending => _next != null;

        /// <summary>Encola las caras de la próxima tirada. Valor ≤ 0 en un índice = rolar normal ese dado.</summary>
        public void SetNext(int[] faces)
        {
            _next = (faces != null && faces.Length > 0) ? (int[])faces.Clone() : null;
        }

        public void Clear() => _next = null;

        /// <summary>Devuelve las caras encoladas (sin clamp — el roller clampea) y las consume.</summary>
        public bool TryConsumeNext(out int[] faces)
        {
            faces = _next;
            _next = null;
            return faces != null;
        }
    }
}
