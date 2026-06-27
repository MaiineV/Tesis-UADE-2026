namespace Rollgeon.DevConsole.Core
{
    /// <summary>Destino de salida de la consola. La capa lógica solo conoce esta interfaz.</summary>
    public interface ILogSink
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
