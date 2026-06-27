namespace Rollgeon.DevConsole.Core
{
    /// <summary>Resultado de ejecutar un comando: éxito/fallo + mensaje para el log.</summary>
    public readonly struct CommandResult
    {
        public bool Success { get; }
        public string Message { get; }

        private CommandResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static CommandResult Ok(string message = null) => new CommandResult(true, message);
        public static CommandResult Fail(string message) => new CommandResult(false, message);
    }
}
