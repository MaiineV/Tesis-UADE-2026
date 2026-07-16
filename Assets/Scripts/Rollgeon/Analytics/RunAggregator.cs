namespace Rollgeon.Analytics
{
    /// <summary>
    /// Acumuladores per-run del <see cref="AnalyticsTrackerService"/>
    /// (Feature#0029). Clase plana testeable — se crea lazy en el tracker
    /// (nunca deserializada por Odin) y se resetea en <c>OnRunStart</c>.
    /// </summary>
    public sealed class RunAggregator
    {
        public int CombatsWon;
        public int GoldEarned;
        public int GoldSpent;
        public int CombosMatched;
        public int FloorsCleared;
        public double RunStartTime;

        /// <summary>La run se ganó (<c>OnRunVictory</c> visto).</summary>
        public bool VictoryMarked;

        /// <summary>El player murió (<c>OnPlayerDefeated</c> visto).</summary>
        public bool DefeatMarked;

        /// <summary>
        /// Dedupe del <c>run_ended</c>: se envía eager en victory/defeat (cubre
        /// cerrar el juego en la pantalla final sin clickear) y <c>OnRunEnd</c>
        /// no debe duplicarlo.
        /// </summary>
        public bool RunEndedSent;

        /// <summary>
        /// La run es un resume de save: los acumuladores cubren solo el segmento
        /// de sesión actual (cota inferior). El análisis agrupa por run_id.
        /// </summary>
        public bool WasResumed;

        public void Reset(double now, bool wasResumed)
        {
            CombatsWon = 0;
            GoldEarned = 0;
            GoldSpent = 0;
            CombosMatched = 0;
            FloorsCleared = 0;
            RunStartTime = now;
            VictoryMarked = false;
            DefeatMarked = false;
            RunEndedSent = false;
            WasResumed = wasResumed;
        }
    }
}
