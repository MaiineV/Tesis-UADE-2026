namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Payload passed to <see cref="FloorTransitionScreen"/> carrying the
    /// floor number and optional title. UI#0013b.
    /// </summary>
    public sealed class FloorTransitionPayload : IScreenPayload
    {
        /// <summary>1-based display number for the floor.</summary>
        public int FloorNumber;

        /// <summary>Optional floor title, e.g. "Catacumbas Profundas". Nullable.</summary>
        public string FloorTitle;
    }
}
