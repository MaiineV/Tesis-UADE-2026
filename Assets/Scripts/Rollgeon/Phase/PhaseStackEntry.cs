namespace Rollgeon.Phase
{
    public readonly struct PhaseStackEntry
    {
        public readonly GamePhase Base;
        public readonly PhaseOverlay Overlay;

        public PhaseStackEntry(GamePhase basePhase, PhaseOverlay overlay)
        {
            Base = basePhase;
            Overlay = overlay;
        }
    }
}
