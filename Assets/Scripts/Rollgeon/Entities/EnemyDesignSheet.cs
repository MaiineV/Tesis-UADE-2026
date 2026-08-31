using System;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>Capa 1 del GDD "Patrones de Ataque": intención táctica del enemigo.</summary>
    public enum EnemyArchetype
    {
        [InspectorName("Sin definir")]     Unspecified = 0,
        [InspectorName("Cuerpo a cuerpo")] Melee = 1,
        [InspectorName("A distancia")]     Ranged = 2,
        [InspectorName("Apoyo")]           Support = 3,
    }

    /// <summary>Capa 3 del GDD: las 11 categorías de patrón geométrico.</summary>
    public enum AttackPatternKind
    {
        [InspectorName("Sin definir")]              Unspecified = 0,
        [InspectorName("Contacto 1×1 adyacente")]   ContactAdjacent = 1,
        [InspectorName("Contacto 1×1 diagonal")]    ContactDiagonal = 2,
        [InspectorName("Línea recta")]              StraightLine = 3,
        [InspectorName("Cono")]                     Cone = 4,
        [InspectorName("Arco / barrido")]           ArcSweep = 5,
        [InspectorName("Cruz / plus")]              Cross = 6,
        [InspectorName("Área diamond")]             DiamondArea = 7,
        [InspectorName("Anillo / donut")]           Ring = 8,
        [InspectorName("Aura")]                     Aura = 9,
        [InspectorName("Zona persistente")]         PersistentZone = 10,
        [InspectorName("Telegraph fila / columna")] TelegraphRowColumn = 11,
    }

    /// <summary>Capa 4 del GDD: cuándo impacta el ataque.</summary>
    public enum AttackTiming
    {
        [InspectorName("Sin definir")] Unspecified = 0,
        [InspectorName("Instantáneo")] Instant = 1,
        [InspectorName("Telegraph")]   Telegraph = 2,
    }

    /// <summary>
    /// Ficha declarativa del enemigo según el framework de 9 capas del GDD. Es metadata de
    /// diseño: ningún sistema de runtime la lee. El Editor de enemigos la usa para filtrar la
    /// lista y para chequear coherencia contra lo que el árbol de IA realmente hace (ej. Timing =
    /// Telegraph sin ningún nodo de telegraph).
    /// </summary>
    [Serializable]
    public sealed class EnemyDesignSheet
    {
        [Tooltip("Intención táctica (capa 1 del GDD).")]
        public EnemyArchetype Archetype = EnemyArchetype.Unspecified;

        [Tooltip("Forma del ataque en la grilla (capa 3). Fija por diseño; no cambia en runtime.")]
        public AttackPatternKind Pattern = AttackPatternKind.Unspecified;

        [Tooltip("Instantáneo = se resuelve en el mismo turno; Telegraph = se avisa un turno antes (capa 4).")]
        public AttackTiming Timing = AttackTiming.Unspecified;

        [TextArea(2, 6)]
        [Tooltip("Movimiento, selección, condición, payload, interacción espacial y fallback (capas 2, 5-9), en prosa.")]
        public string Notes;
    }
}
