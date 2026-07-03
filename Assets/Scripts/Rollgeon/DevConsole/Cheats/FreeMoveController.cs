namespace Rollgeon.DevConsole.Cheats
{
    /// <summary>
    /// Estado del modo "movimiento libre". El comando lo togglea; la capa UI (DevConsoleController)
    /// lo lee y, mientras esté activo, mueve al player 1 tile por tecla vía IMovementService —
    /// sin gate de turno ni de puertas. Se registra en el ServiceLocator para que la UI lo consulte.
    /// </summary>
    public sealed class FreeMoveController
    {
        public bool Enabled { get; private set; }

        public void Set(bool on) => Enabled = on;

        public bool Toggle()
        {
            Enabled = !Enabled;
            return Enabled;
        }
    }
}
