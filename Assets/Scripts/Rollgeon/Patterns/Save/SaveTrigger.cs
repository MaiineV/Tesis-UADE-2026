namespace Patterns.Save
{
    /// <summary>
    /// Momentos del ciclo de vida que pueden disparar un flush a disco (TECHNICAL.md §15.1).
    /// Cuáles escriben realmente lo decide <see cref="SaveSettingsSO.FlushOn"/> — los
    /// deshabilitados sólo capturan en memoria.
    /// </summary>
    public enum SaveTrigger
    {
        RunStart,
        RoomEnd,
        FloorEnd,
        Manual,
        RunEnd,
        Exit,
    }
}
