using System;
using System.Collections.Generic;
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

            // --- El Tahúr (piso 3). Tipos calificados a propósito: este archivo lo comparten
            // varias ramas de jefes y agregar usings arriba multiplica los conflictos de merge.
            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurSettleWager)] =
                "Tahúr — Settle Wager: liquida la ronda. Lee la mano que el jugador jugó " +
                "(ComboPlayedPayload), la mide contra el canto sobre la escalera del contrato " +
                "(ordenada por Priority, NO por daño base) y mueve el pozo.\n\n" +
                "• Exacto ⇒ 0 dmg y, si el jugador está en La Mesa, cobra 12 × fichas contra el " +
                "propio jefe. El cobro REEMPLAZA su ataque: esa ronda no marca Castigo.\n" +
                "• Codicia (mano mejor) ⇒ +2 fichas y el Castigo más ancho (Scattered 6×2).\n" +
                "• Fallo (mano peor, o ninguna) ⇒ +1 ficha y la forma dice cuánto faltó: " +
                "Column 1 → Row 1 → Column 3 → Scattered 4×2.\n" +
                "• Fase 2 con el canto invertido, armar el canto ⇒ te leyó: el peor resultado.\n\n" +
                "El daño sale de PotDamageTable por cantidad de fichas (26/32/38/42/45) y jamás " +
                "supera DamageCeiling (45 = techo por golpe del piso 3). Marca vía " +
                "IThreatenedAreaService, así que lo detona el ExecuteTelegraph estándar el turno " +
                "siguiente. Puede fallar (sin contrato, sin grilla) ⇒ envolver en " +
                "Selector[nodo, Wait].",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurCallHand)] =
                "Tahúr — Call Hand: canta un escalón de la escalera del contrato del jugador y lo " +
                "publica como el objetivo de la próxima ronda.\n\n" +
                "No inventa mecánicas: usa las dos reglas del Contrato que ya existen — " +
                "ForbidCombo (R03) sobre la mano cantada (armarla hace 0: cobrar cuesta el ataque, " +
                "no la vida) y MultiplyCombo ×2 (R01) sobre todo lo que esté por encima del " +
                "escalón a armar (la codicia paga doble en el golpe y doble en el pozo).\n\n" +
                "La válvula nunca canta dos escalones altos seguidos (HighRankThreshold) y " +
                "UseRotationMemory evita repetir hasta agotar el conjunto — rotativo se aprende " +
                "más rápido. En fase 2 (canto invertido) nunca canta el escalón 1: no habría " +
                "escalón debajo desde el que cobrar.",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurMarkTable)] =
                "Tahúr — Mark Table: pinta La Mesa, su 3×3, daño 0, en cian. Es el único lugar " +
                "desde donde se cobra el pozo.\n\n" +
                "Va DESPUÉS del movimiento: la mesa de esta ronda no está donde estaba la anterior, " +
                "así que hasta la ronda perfecta pide un paso. No usa IThreatenedAreaService (se " +
                "indexa por guid de fuente y pisaría al Castigo): las casillas viven en " +
                "ITahurWagerService y el overlay usa una key propia.",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurFlipCard)] =
                "Tahúr — Flip Card: setup de Fase 2 (se voltea la carta). El cartel pasa de PIDE a " +
                "LEE — la mano cantada es ahora la que NO hay que armar y se cobra el escalón " +
                "inmediatamente inferior —, entra el rastrillo (+1 ficha por ronda, sola) y cobrar " +
                "deja el pozo en 1, nunca en 0.\n\n" +
                "No cambia un solo número: cambia el puzzle. La primera liquidación después del " +
                "volteo es de gracia (el canto pendiente se armó con las reglas viejas). Envolver " +
                "en If(PcOwnerHpBelow 0.40) → Once(...).",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurPoke)] =
                "Tahúr — Poke: 12 de daño melee, solo en ronda limpia. Es el precio fijo de cobrar, " +
                "porque cobrar es estar en su cara.\n\n" +
                "Se auto-gatea con RequireCleanRound: el poke y el Castigo nunca resuelven la misma " +
                "ronda porque 12 + 45 rompe el techo de 45 por golpe del piso 3. El árbol lo gatea " +
                "además con PcTahurCleanRound + PcTargetInRange 1.",
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
