using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Catálogo de descripciones por tipo de nodo del AI tree, mostradas en el side panel
    /// del inspector. Cuando un nodo nuevo se agrega al runtime, sumar su entry acá para
    /// que el editor le muestre help al designer.
    /// </summary>
    public static class AINodeDocumentation
    {
        private static readonly Dictionary<Type, string> _descriptions = new Dictionary<Type, string>
        {
            [typeof(AINode_Sequence)] =
                "Secuencia (AND): ejecuta los Children en orden. Si alguno retorna Failed, " +
                "corta ahí y devuelve Failed sin tocar el resto. Si todos retornan Succeeded, " +
                "devuelve Succeeded.\n\n" +
                "Usalo cuando una serie de pasos tiene que pasar todos en orden " +
                "(ej: 'moverte → atacar → recuperar energía').",

            [typeof(AINode_Selector)] =
                "Selector (OR): prueba los Children en orden hasta que uno retorne Succeeded — " +
                "devuelve Succeeded sin tocar los demás. Si todos fallan, devuelve Failed.\n\n" +
                "Usalo para fallback chains: 'intentá X; si no podés, intentá Y; si no, hacé Z'.",

            [typeof(AINode_If)] =
                "If: ramifica según la lista AND-evaluada de Conditions (PreConditions). " +
                "Si todas pasan, ejecuta el branch Then; si alguna falla, ejecuta Else.\n\n" +
                "TargetSelector decide a quién apuntan las Conditions (default: Always Player). " +
                "Lista de Conditions vacía = AND-empty = pasa siempre (toma Then).",

            [typeof(AINode_While)] =
                "While: loop. Re-ejecuta el Body mientras todas las Conditions sean true, " +
                "hasta MaxIterations.\n\n" +
                "• Conditions vacías → permisivo (true) → MaxIterations es el único corte.\n" +
                "• Body retorna Failed → propaga Failed inmediatamente.\n" +
                "• Cap alcanzado → Failed + warning log (señal de bug — condition que nunca flips).",

            [typeof(AINode_Random)] =
                "Random (weighted): elige un hijo al azar entre los Options usando Weight " +
                "como probabilidad relativa (mayor weight = más probable). Devuelve el " +
                "resultado del child elegido.\n\n" +
                "RNG inyectado vía AIContext.Rng — los tests pueden seedearlo para determinismo.",

            [typeof(AINode_Alternate)] =
                "Alternate (round-robin): rota entre los Children en orden fijo, uno por " +
                "turno (0, 1, 2, ..., 0, 1, 2, ...). A diferencia de Random, garantiza que " +
                "nunca se repite el mismo hijo dos veces seguidas.\n\n" +
                "Usalo cuando necesitás que el enemigo alterne dos comportamientos (ej. " +
                "'ataque especial' / 'golpe normal') sin depender del azar. El índice vive " +
                "en la instancia runtime (se resetea a 0 en cada pelea nueva).",

            [typeof(AINode_Move)] =
                "Move Toward Target: mueve al enemy hasta MaxSteps casillas (AIIntReader) hacia " +
                "el target del TargetSelector (null = player), manteniendo DesiredRange casillas " +
                "de distancia Manhattan. Si DesiredRange es null usa el legacy StopAdjacent " +
                "(true → 1, false → 0).\n\n" +
                "Si Retreat es true y está más cerca que DesiredRange, retrocede (kite); si es " +
                "false, demasiado cerca = no se mueve. Devuelve Succeeded si se movió, Failed si " +
                "ya está en la banda o no hay tile mejor. Setea AIContext.PendingWait con la animación.",

            [typeof(AINode_KeepDistance)] =
                "Keep Distance (kiting): mueve al enemy hasta MaxSteps casillas para mantener " +
                "IdealDistance del player. Si ya está a la distancia ideal, no se mueve.\n\n" +
                "Usalo para enemigos ranged/casters que evitan el melee.",

            [typeof(AINode_Wait)] =
                "Wait: no-op. Siempre devuelve Succeeded sin hacer nada.\n\n" +
                "Usalo como placeholder durante prototipado, o como pad de turno cuando un " +
                "branch no debería actuar pero tampoco fallar.",

            [typeof(AINode_TelegraphMark)] =
                "Telegraph Mark: marca un área (sin dañar este turno) para que " +
                "ExecuteTelegraph la detone el turno siguiente del enemigo.\n\n" +
                "Shape=DirectionalBand: el área sale del propio enemigo (no del jugador) — " +
                "banda perpendicular de 2·Size+1 casillas, centrada en su coordenada, " +
                "extendida Depth casillas en la dirección cardinal dominante hacia el " +
                "jugador.\n\n" +
                "Shape=ScatteredSquares: Count cuadrados de Size×Size, anclados al azar " +
                "(AIContext.Rng) en el 50% central de la sala — ni el jugador ni el " +
                "enemigo son el centro, y nunca aparecen pegados a las paredes. Prioriza " +
                "que los cuadrados no se toquen ni se solapen entre sí (degrada el gap si " +
                "la sala no da lugar). Requiere una sala con bounds reales.\n\n" +
                "Shape=SquareAroundSelf: mismo cálculo que Square (2·Size+1), pero centrado " +
                "en la coordenada del propio enemigo en vez de la del jugador.\n\n" +
                "Las demás shapes (Square/Row/Column/HalfRoom) se centran en el jugador " +
                "como siempre.",

            [typeof(AINode_Behavior)] =
                "Behavior: ejecuta un EnemyActionBehavior — la unidad reusable de combate " +
                "(ataque, heal, buff, etc.).\n\n" +
                "El TargetSelector del Behavior resuelve a quién apuntan los Effects. Cada " +
                "Effect tiene su propia lista de PreConditions independiente — el behavior " +
                "ejecuta solo los Effects cuyas PreConditions pasan.",

            [typeof(AINode_ActivateRainHazard)] =
                "Activate Rain Hazard: activa RainHazardService (idempotente) — una amenaza " +
                "ambiental independiente del boss (fuente propia) que, una vez activa, marca " +
                "y detona zonas erráticas en su propio ciclo de rondas, en paralelo a lo que " +
                "esté haciendo el boss.\n\n" +
                "Pensado para envolver en If(PcOwnerHpBelow) → Once(...), igual que el " +
                "trigger de refuerzos — dispara una sola vez al cruzar el umbral de HP.",

            [typeof(AINode_SpawnReinforcements)] =
                "Spawn Reinforcements: spawnea Count copias de EnemyToSpawn en tiles del " +
                "borde de la sala (perímetro del bounding box, walkable y libres) y los " +
                "suma a la ronda de combate en curso.\n\n" +
                "Los refuerzos van al FINAL de la ronda actual — quien ya estaba en cola " +
                "(player/boss) termina su turno normal antes; de ahí en adelante rotan de " +
                "forma regular y estable, como cualquier otro participante.\n\n" +
                "Pensado para envolver en If(PcOwnerHpBelow) → Once(...) — dispara una sola " +
                "vez al cruzar el umbral de HP, igual que otros triggers de fase.",

            // --- La Bandida (piso 1) ----------------------------------------------------
            [typeof(AINode_SpawnReels)] =
                "Spawn Reels (La Bandida): mantiene la fila de rodillos. La arma alineada en " +
                "el primer turno —Count casillas consecutivas a un paso del jefe, del lado con " +
                "más tiles libres (Direction fija el lado a mano)—, detecta los rotos y los " +
                "repone en su MISMA ranura a los RespawnDelayTurns turnos del jefe.\n\n" +
                "Al devolver un rodillo rearma la cuenta del jackpot en CountdownOnRespawn: " +
                "reponer y resetear la cuenta son el mismo paso, si no el jackpot dispararía al " +
                "turno siguiente del respawn.\n\n" +
                "NO envolver en Once: se auto-gatea (arma la fila una sola vez) pero necesita " +
                "tickear cada turno para correr los relojes de reposición. Latcheado en Once, " +
                "ningún rodillo vuelve nunca.",

            [typeof(AINode_TickJackpot)] =
                "Tick Jackpot Countdown (La Bandida): baja un turno la cuenta regresiva del " +
                "jackpot (2 → 1 → 0) y la publica para el número gigante sobre la máquina. " +
                "No-op si la cuenta está cancelada.\n\n" +
                "Va suelto en el Sequence raíz, antes del pool de acción, y devuelve siempre " +
                "Succeeded. Quién marca el jackpot al llegar a 0 lo decide el pool vía la " +
                "PreCondition Jackpot Countdown — este nodo solo lleva la cuenta.",

            [typeof(AINode_ResetJackpotCountdown)] =
                "Reset Jackpot Countdown (La Bandida): rearma la cuenta en Value y la vuelve a " +
                "poner a contar.\n\n" +
                "Va inmediatamente después del TelegraphMark del jackpot, en el mismo Sequence: " +
                "la cuenta que dispara se rearma en el acto. Esa asimetría es de diseño — la " +
                "ronda muerta solo la cobra quien rompe un rodillo (la reposición). La pausa es " +
                "el premio de cancelar; tanquear el jackpot no la recibe.",

            [typeof(AINode_LockReel)] =
                "Lock Reel (La Bandida, Fase 2 · HOLD): traba una ranura de la fila. El rodillo " +
                "trabado deja de cancelar la cuenta y se repone con LockedHp de vida (pool " +
                "inagotable: el pipeline de daño no tiene canal de inmunidad), así que quedan " +
                "dos blancos válidos — los dos de la punta, los que están más lejos.\n\n" +
                "One-shot: va dentro del Once del gate de fase. Devuelve Failed si la fila " +
                "todavía no está armada, para que el Once no latchee en falso.",

            [typeof(AINode_SetReelRespawnDelay)] =
                "Set Reel Respawn Delay (La Bandida, Fase 2): pisa el delay de reposición de los " +
                "rodillos. Bajarlo a 1 hace que la cuenta arranque de nuevo cada ronda en vez de " +
                "cada dos.\n\n" +
                "Solo cambia frecuencia: ningún número de daño se mueve.",
        };

        /// <summary>
        /// Devuelve la descripción para el tipo de nodo, o <c>null</c> si no hay registrada.
        /// </summary>
        public static string Get(Type t)
        {
            if (t == null) return null;
            return _descriptions.TryGetValue(t, out var doc) ? doc : null;
        }
    }
}
