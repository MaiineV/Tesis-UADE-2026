using System;

namespace Rollgeon.Movement
{
    /// <summary>
    /// Política opcional de recorrido que <see cref="MovementService"/> consulta por
    /// <c>ServiceLocator</c>: qué entidades pueden atravesar celdas ocupadas por unidades
    /// como paso intermedio (Paso etéreo del dado de Movimiento). El destino sigue teniendo
    /// que estar libre; las celdas no caminables siguen bloqueando. Sin política registrada
    /// nadie atraviesa.
    /// </summary>
    public interface IMovementTraversalPolicy
    {
        bool CanPassThroughUnits(Guid entity);
    }
}
