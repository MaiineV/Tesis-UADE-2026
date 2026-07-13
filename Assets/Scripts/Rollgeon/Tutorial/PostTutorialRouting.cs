namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Carrier estático entre el teardown del tutorial (escena <c>02_Gameplay</c>)
    /// y el <c>MainMenuScreen</c> (escena <c>01_MainMenu</c>): al completar el
    /// tutorial el jugador no vuelve al menú raíz sino directo a la selección de
    /// clase para su primera run real. Mismo rol cross-escena que
    /// <c>PendingRunRequest</c> — no pasa por ServiceLocator.
    /// </summary>
    public static class PostTutorialRouting
    {
        /// <summary>
        /// Al cargar el main menu, saltar directo a <c>ClassSelectionScreen</c>.
        /// Lo setea el teardown del tutorial; lo consume (y limpia) <c>MainMenuScreen</c>.
        /// </summary>
        public static bool AutoOpenClassSelection { get; set; }
    }
}
