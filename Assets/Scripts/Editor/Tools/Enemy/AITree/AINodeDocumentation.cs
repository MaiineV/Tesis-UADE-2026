using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Bosses.Croupier;
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

            [typeof(AINode_SpinWheel)] =
                "Spin Wheel (Croupier): canta 1 número del 1 al 6 (2 en fase 2) y lo deja " +
                "flotando sobre el jefe. Ese número es dos cosas a la vez — el sector del " +
                "paño que va a caer el turno que viene y el dado de la bolsa que se " +
                "confisca — así que este nodo sólo lo elige: marcar y confiscar son los dos " +
                "nodos siguientes, que leen de la rueda.\n\n" +
                "Abre el windup: desde acá y hasta que el sector detona, pegarle al jefe " +
                "corre la rueda +1 y, si el número es impar, cobra RetaliationDamage al " +
                "atacante. Con la rueda trucada (fase 2) no dispara ninguno de los dos.\n\n" +
                "AvoidRepeatingLastNumber saca el número del turno anterior del pool (no " +
                "re-sortea), así el paño se mueve todos los turnos sin sesgar el azar.",

            [typeof(AINode_MarkSungSectors)] =
                "Mark Sung Sectors (Croupier): marca como amenazado el sector de cada número " +
                "cantado, para que detone en el próximo turno del jefe. No hace daño este " +
                "turno.\n\n" +
                "Es el TelegraphMark de este jefe: existe aparte porque el área sale de un " +
                "número decidido en runtime (no de un Size autorado) y porque en fase 2 hay " +
                "DOS áreas simultáneas, marcadas bajo fuentes distintas para que se resuelvan " +
                "por separado — el jugador parado en la columna de costura, donde los dos " +
                "sectores se pisan, cobra los dos golpes (2 × SectorDamagePhase2).\n\n" +
                "El daño se congela al marcar: un sector marcado en fase 1 detona por " +
                "SectorDamage aunque el jefe cruce el umbral en el medio.",

            [typeof(AINode_DetonateSungSectors)] =
                "Detonate Sung Sectors (Croupier): detona los sectores cantados el turno " +
                "pasado y cierra el windup (pegarle ya no mueve la rueda hasta que vuelva a " +
                "cantar).\n\n" +
                "Va PRIMERO en el Sequence raíz, como ExecuteTelegraph, y siempre devuelve " +
                "Succeeded: 'no había nada marcado' (turno 1) o 'el jugador se fue del " +
                "sector' son resoluciones válidas, no fallos que deban cortarle el turno al " +
                "jefe.",

            [typeof(AINode_IgniteDetonatedSectors)] =
                "Ignite Detonated Sectors (Croupier): prende fuego el/los sector(es) que " +
                "detonaron en ESTE turno — 6 de daño a quien termine su turno adentro. Mata " +
                "la lectura de que el bloque recién explotado es el lugar más seguro del " +
                "paño.\n\n" +
                "Duración por fase = dos definiciones (Fire / FirePhase2), porque " +
                "HazardService toma DurationRounds del SO. OJO: pedí una ronda MÁS que lo que " +
                "dice la ficha — el fuego nace en el turno del jefe y el jugador juega " +
                "primero en cada ronda, así que DurationRounds=1 expira antes de poder " +
                "tickear. 'Un turno' = 2, 'dos turnos' = 3.\n\n" +
                "BlastConsumesFlame: si el jugador se comió la detonación, el fuego de ese " +
                "sector se saltea su primer tick (SkipNextTick) y el peor caso de la costura " +
                "sigue siendo 24 en vez de 30. Se arma sólo cuando el jugador estaba adentro: " +
                "el flag se consume recién con un tick que hubiera pegado, así que armarlo a " +
                "ciegas se tragaría el primer tick legítimo.",

            [typeof(AINode_SetWheelMode)] =
                "Set Wheel Mode (Croupier): el setup de 'Pleno y color'. Cambia cuántos " +
                "números canta por turno y truca la rueda (pegarle deja de correrla y de " +
                "cobrar Represalia: la fase abarata pegarle, lo que te saca es la palanca).\n\n" +
                "Va envuelto en If(PcOwnerHpBelow 0.5) → Once, al lado del ApplyStatModifier " +
                "que dispara el feedback de fase. El PhaseIndex que setea acá es el que leen " +
                "los nodos con valores por fase (daño de sector, definición de fuego), así " +
                "que hay un único lugar del árbol que decide 'estamos en fase 2'.",
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
