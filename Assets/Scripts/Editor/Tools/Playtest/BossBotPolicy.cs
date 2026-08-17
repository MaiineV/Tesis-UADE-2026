using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.EditorTools.Playtest
{
    public enum BotActionKind
    {
        /// <summary>No hay nada que hacer con este kit — cerrar el turno.</summary>
        None,
        Move,
        Attack,
    }

    /// <summary>
    /// Vista mínima de un <c>HeroActionBehavior</c>: lo único que la política necesita.
    /// Existe para que <see cref="BossBotPolicy"/> no dependa de tipos de Unity y se pueda
    /// testear en EditMode, que es la única clase de test que corre este repo.
    /// </summary>
    public readonly struct BotBehaviorSlot
    {
        /// <summary>Índice en <c>GetBehaviorsForPhase(Combat)</c> — lo que espera <c>OnBehaviorSelected</c>.</summary>
        public readonly int Index;
        public readonly string ActionName;
        public readonly bool NeedsDiceRoll;
        public readonly int EnergyCost;

        public BotBehaviorSlot(int index, string actionName, bool needsDiceRoll, int energyCost)
        {
            Index = index;
            ActionName = actionName;
            NeedsDiceRoll = needsDiceRoll;
            EnergyCost = energyCost;
        }
    }

    public readonly struct BotDecision
    {
        public readonly BotActionKind Kind;
        public readonly int BehaviorIndex;

        /// <summary>
        /// Destinos para un <see cref="BotActionKind.Move"/>, mejor primero. Es una lista y no
        /// un tile porque el bot no rehace el pathfinding del juego: tira el destino más ambicioso
        /// y, si el rango de movimiento lo rechaza, baja al siguiente.
        /// </summary>
        public readonly IReadOnlyList<GridCoord> Candidates;

        /// <summary>Por qué se decidió esto, para la línea del <c>turns.log</c>.</summary>
        public readonly string Reason;

        public BotDecision(BotActionKind kind, int behaviorIndex, IReadOnlyList<GridCoord> candidates, string reason)
        {
            Kind = kind;
            BehaviorIndex = behaviorIndex;
            Candidates = candidates ?? Array.Empty<GridCoord>();
            Reason = reason;
        }
    }

    /// <summary>
    /// La decisión de cada turno: acercarse hasta estar a rango, y ahí pegar.
    /// </summary>
    /// <remarks>
    /// Es deliberadamente tonta. El bot existe para validar a los jefes, no para jugar bien:
    /// una heurística de dos ramas es reproducible y se explica en una línea del log, mientras
    /// que algo más listo haría que dos corridas de la misma seed divergieran por razones
    /// propias del bot y no del jefe.
    ///
    /// "Acercarse y pegar" además es justo el eje que los cambios de jefes movieron — el peaje
    /// del Cajero castigaba acercarse, y la mesa de La Generala existe para que pegarle de lejos
    /// no alcance.
    /// </remarks>
    public static class BossBotPolicy
    {
        public const string MoveActionName = "Movement";
        public const string AttackActionName = "Base Attack";

        /// <summary>Radio de búsqueda de destinos alrededor del player. Acota el barrido.</summary>
        private const int SearchRadius = 6;

        /// <summary>Cuántos destinos devolver. Más allá del 6º ya no queda rango que probar.</summary>
        private const int MaxCandidates = 6;

        /// <summary>
        /// Distancia en pasos ortogonales (Manhattan), no Chebyshev.
        /// </summary>
        /// <remarks>
        /// Con Chebyshev la diagonal cuenta como rango 1, y el <c>Base Attack</c> del Warrior no
        /// llega en diagonal: el juego respondía <i>"No usable effect group: no valid targets"</i>
        /// y la corrida terminaba con el jefe intacto. Costó encontrarlo porque el bot se creía a
        /// rango y el log decía "pega" — los únicos dos turnos que funcionaban eran los que caían
        /// en ortogonal de casualidad, cuando la diagonal estaba ocupada.
        /// </remarks>
        public static int Distance(GridCoord a, GridCoord b) => a.Manhattan(b);

        public static int IndexOf(IReadOnlyList<BotBehaviorSlot> slots, string actionName)
        {
            if (slots == null || string.IsNullOrEmpty(actionName)) return -1;

            for (int i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].ActionName, actionName, StringComparison.OrdinalIgnoreCase))
                    return slots[i].Index;
            }
            return -1;
        }

        /// <param name="isWalkable">
        /// Si el bot puede pararse en ese tile. Lo provee el driver desde el grid real, así los
        /// dados de La Generala y el mobiliario cuentan como pared sin que la política sepa de ellos.
        /// </param>
        /// <param name="attackRange">
        /// Distancia máxima desde la que el ataque llega, medida en pasos ortogonales
        /// (Manhattan). Ver <see cref="Distance"/> para por qué no es Chebyshev.
        /// </param>
        public static BotDecision Decide(
            GridCoord player,
            GridCoord boss,
            IReadOnlyList<BotBehaviorSlot> slots,
            Func<GridCoord, bool> isWalkable,
            int attackRange = 1)
        {
            int distance = Distance(player, boss);

            if (distance <= attackRange)
            {
                int attackIndex = IndexOf(slots, AttackActionName);
                if (attackIndex >= 0)
                    return new BotDecision(BotActionKind.Attack, attackIndex, null, $"a rango ({distance}) — pega");

                // Sin ataque en el kit no se inventa un índice: un OnBehaviorSelected con un
                // índice cualquiera dispararía una acción distinta y la corrida mentiría.
                return new BotDecision(BotActionKind.None, -1, null,
                    $"a rango ({distance}) pero el kit no tiene '{AttackActionName}'");
            }

            int moveIndex = IndexOf(slots, MoveActionName);
            if (moveIndex < 0)
            {
                return new BotDecision(BotActionKind.None, -1, null,
                    $"lejos ({distance}) y el kit no tiene '{MoveActionName}'");
            }

            var candidates = FindApproachTiles(player, boss, isWalkable, attackRange);
            if (candidates.Count == 0)
            {
                return new BotDecision(BotActionKind.None, -1, null,
                    $"lejos ({distance}) y no hay tile libre hacia el jefe");
            }

            return new BotDecision(BotActionKind.Move, moveIndex, candidates,
                $"lejos ({distance}) — se acerca a {candidates[0]}");
        }

        /// <summary>
        /// Tiles desde donde el ataque llegaría, ordenados por cercanía al player (el más probable
        /// de entrar en el rango de movimiento primero). Si ninguno es alcanzable, cae a tiles que
        /// simplemente reducen la distancia, para no perder el turno parado.
        /// </summary>
        private static List<GridCoord> FindApproachTiles(
            GridCoord player, GridCoord boss, Func<GridCoord, bool> isWalkable, int attackRange)
        {
            var inRange = new List<GridCoord>();
            var closer = new List<GridCoord>();
            int currentDistance = Distance(player, boss);

            for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
            {
                for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
                {
                    var tile = new GridCoord(player.X + dx, player.Y + dy);
                    if (tile == player || tile == boss) continue;
                    if (isWalkable != null && !isWalkable(tile)) continue;

                    int toBoss = Distance(tile, boss);
                    if (toBoss <= attackRange) inRange.Add(tile);
                    else if (toBoss < currentDistance) closer.Add(tile);
                }
            }

            bool reachesTheBoss = inRange.Count > 0;
            var chosen = reachesTheBoss ? inRange : closer;

            // Los dos casos quieren criterios opuestos:
            //
            // - Hay tiles desde donde pegar: todos valen lo mismo para atacar, así que gana el
            //   más cercano al player — el más probable de entrar en su rango de movimiento.
            // - No hay ninguno (el anillo del jefe está tomado, ej. la mesa de La Generala):
            //   este turno no se pega igual, así que gana el que más avanza. Ordenar por
            //   cercanía al player acá desperdiciaría el rango, avanzando un tile por turno
            //   cuando el movimiento alcanzaba para tres.
            chosen.Sort((a, b) =>
            {
                int primary = reachesTheBoss
                    ? Distance(a, player).CompareTo(Distance(b, player))
                    : Distance(a, boss).CompareTo(Distance(b, boss));
                if (primary != 0) return primary;

                int secondary = reachesTheBoss
                    ? Distance(a, boss).CompareTo(Distance(b, boss))
                    : Distance(a, player).CompareTo(Distance(b, player));
                if (secondary != 0) return secondary;

                // Desempate estable: sin esto el orden depende del sort y dos corridas de la
                // misma seed podrían elegir tiles distintos.
                int byX = a.X.CompareTo(b.X);
                return byX != 0 ? byX : a.Y.CompareTo(b.Y);
            });

            if (chosen.Count > MaxCandidates) chosen.RemoveRange(MaxCandidates, chosen.Count - MaxCandidates);
            return chosen;
        }
    }
}
