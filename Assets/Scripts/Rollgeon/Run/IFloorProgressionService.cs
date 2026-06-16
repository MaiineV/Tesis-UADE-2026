using Rollgeon.Dungeon;

namespace Rollgeon.Run
{
    /// <summary>
    /// Orquesta la transición entre pisos (#158): escucha
    /// <see cref="Patterns.EventName.OnFloorExitRequested"/>, avanza
    /// <see cref="IRunContextService.FloorIndex"/>, regenera el siguiente piso via
    /// <see cref="IDungeonService.GenerateFloor"/> y muestra la pantalla de transición.
    /// Run-scoped — registrado por <c>RunController</c> al arrancar la run.
    /// </summary>
    public interface IFloorProgressionService
    {
        /// <summary>Layout del piso actualmente activo.</summary>
        FloorLayoutSO CurrentLayout { get; }
    }
}
