namespace Rollgeon.DevConsole.UI
{
    /// <summary>Fachada de la consola registrada en el ServiceLocator (abrir/cerrar/ejecutar).</summary>
    public interface IDevConsoleService
    {
        bool IsOpen { get; }
        void Open();
        void Close();
        void Toggle();
        void Execute(string line);
    }
}
