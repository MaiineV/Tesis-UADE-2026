namespace Rollgeon.Input
{
    /// <summary>
    /// Acciones de gameplay que exponen un hotkey (teclado hoy, gamepad a futuro).
    /// El nombre de cada valor DEBE matchear el nombre de la action en el map
    /// "Gameplay" del <c>InputSystem_Actions.inputactions</c>: el
    /// <see cref="GameplayHotkeyService"/> resuelve la action por
    /// <c>hotkey.ToString()</c>. Agregar un valor acá sin la action gemela en el
    /// asset solo loguea un warning (queda inerte), no rompe.
    /// </summary>
    public enum GameplayHotkey
    {
        Move,
        Attack,
        SpecialAttack,
        Heal,
        Roll,
        Confirm,
        EndTurn,
        ForceDoor,
        Defense,
    }
}
