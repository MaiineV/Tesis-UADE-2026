namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Pasos del tutorial, en orden de recorrido. La transición la dirigen los
    /// eventos del bus (ver tabla en <see cref="TutorialFlowController"/>).
    /// Nadie serializa ni compara ordinalmente estos valores: insertar en el
    /// medio es seguro — mantener esa propiedad si se agregan consumidores.
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>Sala A: enseñar movimiento hasta la puerta.</summary>
        Movement,

        /// <summary>Sala B: spotlight a los enemigos — click para continuar.</summary>
        EnemiesIntro,

        /// <summary>Sala B: señalar la cola de orden de turnos (esquina sup. der.).</summary>
        TurnOrderIntro,

        /// <summary>Sala B: se reveló el ícono del contrato — ver los combos.</summary>
        ContractTeach,

        /// <summary>Sala B: señalar el botón MOVER (el héroe es melee).</summary>
        MoveTeach,

        /// <summary>Sala B: señalar las casillas alcanzables — elegir a dónde moverse.</summary>
        MoveTiles,

        /// <summary>Sala B: explicar la barra de vida.</summary>
        StatsHp,

        /// <summary>Sala B: explicar la barra de energía.</summary>
        StatsEnergy,

        /// <summary>Sala B: se desbloqueó ATACAR — señalar el botón.</summary>
        AttackTeach,

        /// <summary>Sala B: la selección de objetivo se abrió (antes de tirar) — señalar al enemigo.</summary>
        TargetTeach,

        /// <summary>Sala B: objetivo marcado, aparecieron los dados — enseñar a agarrarlos y arrojarlos.</summary>
        ThrowTeach,

        /// <summary>Sala B: primera tirada revelada — combos de generala, holds, re-tirar agarrando, confirmar.</summary>
        DiceTeach,

        /// <summary>Sala B: el chain siguió a la fase de defensa — explicar el escudo.</summary>
        DefenseTeach,

        /// <summary>Sala B: la acción terminó — explicar el fin de turno.</summary>
        EndTurnTeach,

        /// <summary>Sala B: loop libre del combate 1 — repetir hasta vencer.</summary>
        Combat1,

        /// <summary>Sala B: el enemigo golpeó al jugador — se desbloqueó Heal.</summary>
        HealUnlocked,

        /// <summary>B clareada: ir a C (la puerta de la tienda sigue bloqueada).</summary>
        GoToC,

        /// <summary>Sala C: combate 2v2 — se desbloqueó ForceDoor, enseñar el escape.</summary>
        EscapeTeach,

        /// <summary>Escapó de C: qué pasa con los enemigos al escapar y al volver.</summary>
        EscapeAftermath,

        /// <summary>Escapó de C: explicar los controles de cámara (rotar/pan/zoom).</summary>
        CameraTeach,

        /// <summary>Escapó de C: zoom out muestra salas adyacentes y sus tipos.</summary>
        MapTeach,

        /// <summary>Escapó de C: la tienda se desbloqueó — comprar la mejora de Par.</summary>
        Shop,

        /// <summary>Compró: se reveló el ícono del inventario.</summary>
        BackpackTeach,

        /// <summary>Compró: C se desbloqueó de nuevo — volver a pelear.</summary>
        Combat2Prep,

        /// <summary>Sala C round 2: matar a los 2 enemigos.</summary>
        Combat2,

        /// <summary>C clareada: ir a la sala de encantamientos.</summary>
        GoToE,

        /// <summary>Sala E: encantar un dado en el altar.</summary>
        Enchant,

        /// <summary>Dado encantado: señalar el botón de cerrar la mesa.</summary>
        CloseEnchantTable,

        /// <summary>Mesa cerrada: se reveló el ícono de la bolsa de dados.</summary>
        DiceBagTeach,

        /// <summary>Mesa cerrada: cruzar la puerta de salida.</summary>
        Exit,

        /// <summary>Teardown en curso / terminado.</summary>
        Done,
    }
}
