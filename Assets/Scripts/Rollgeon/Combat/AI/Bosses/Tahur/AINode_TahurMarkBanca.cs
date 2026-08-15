using System;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// "La Banca" del Tahúr: con el pozo lleno se levanta y barre la mesa — marca toda la sala
    /// menos La Mesa, su 3×3, y cobra 45 al turno siguiente. Ficha de diseño "El Tahúr" (piso 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Para qué existe.</b> Es la línea que le saca al jugador la salida de no jugar. Sin ella,
    /// renunciar al pozo era gratis: el Castigo se esquiva caminando y el poke sólo entra si vas a
    /// cobrar, así que quedarse lejos era una postura estable. Con el rastrillo corriendo desde la
    /// fase 1 (<see cref="AINode_TahurSettleWager.RakeChipsPerRound"/>) el pozo se llena solo, y
    /// cuando se llena la única casilla que no cobra es justo la que exige jugar su juego.
    /// </para>
    /// <para>
    /// <b>Va último en el turno, después del movimiento y de poner la mesa.</b> El hueco se ancla
    /// en el jefe, así que marcarlo antes de moverse lo dejaría centrado donde ya no está: el paño
    /// cian diría una cosa y el hueco seguro otra. Marcado al final, el hueco y La Mesa son el
    /// mismo 3×3 y siguen siéndolo hasta que detona, porque el jefe no se mueve entre el final de
    /// su turno y el <c>AINode_ExecuteTelegraph</c> que abre el siguiente.
    /// </para>
    /// <para>
    /// <b>Reemplaza al Castigo de la ronda.</b> Marca sobre el guid del jefe, el mismo canal que
    /// usa <see cref="AINode_TahurSettleWager"/>, y <see cref="IThreatenedAreaService.Mark"/>
    /// sobrescribe: nunca detonan los dos. Es lo correcto además de lo cómodo — 45 + 45 rompería
    /// el techo de daño por golpe del piso 3, y con el pozo lleno el Castigo ya valía 45.
    /// </para>
    /// <para>
    /// <b>El poke no se le suma.</b> El poke tiene alcance 1 Manhattan desde el jefe, así que todo
    /// lo que puede alcanzar está dentro del 3×3 — o sea, dentro del hueco. Quien cobra los 45 está
    /// fuera de rango del poke por construcción, y quien recibe el poke cobró 0 de La Banca.
    /// </para>
    /// <para>
    /// Devuelve <see cref="AIResult.Failed"/> cuando el pozo todavía no está lleno (el caso normal)
    /// ⇒ va envuelto en <c>Selector[Banca, Wait]</c> como el resto de los nodos que pueden fallar.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurMarkBanca : AIActionNode
    {
        [Title("El disparador")]
        [Tooltip("Fichas que tiene que tener el pozo para que la banca barra la mesa. 5 = pozo lleno.")]
        [MinValue(1)]
        public int ChipsThreshold = 5;

        [Title("Daño")]
        [Tooltip("Daño de La Banca al detonar el turno siguiente.")]
        [MinValue(0)]
        public int Damage = 45;

        [Tooltip("Techo duro de daño por golpe del piso 3. La Banca nunca pega más que esto.")]
        [MinValue(0)]
        public int DamageCeiling = 45;

        [Tooltip("Tipo de ataque de La Banca al detonar.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Title("El hueco")]
        [Tooltip("Radio del hueco seguro (1 ⇒ el 3×3 de La Mesa). Tiene que ser el mismo Size que " +
                 "AINode_TahurMarkTable: el hueco y el paño cian son la misma promesa.")]
        [MinValue(0)]
        public int TableRadius = 1;

        public override string NodeName => $"Tahúr — La Banca ({Damage} en toda la sala menos La Mesa)";

        public override AIResult Tick(AIContext context)
        {
            if (context?.Grid == null) return AIResult.Failed;

            var wager = TahurWagerService.ResolveOrCreate();
            if (wager.Chips < EffectiveThreshold(wager)) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;

            var tiles = ThreatAreaShape.Compute(
                context.Grid, selfCoord, ThreatShape.AllExceptSquareAroundSelf,
                TableRadius, HalfRoomAxis.Vertical);

            // El hueco es La Mesa, no un cuadrado parecido: si el radio de acá y el Size del nodo
            // de la mesa alguna vez divergen, la promesa que tiene que sobrevivir es la del paño
            // cian — es la única que el jugador puede leer en pantalla.
            tiles.ExceptWith(wager.TableTiles);

            if (tiles.Count == 0)
            {
                Debug.LogWarning("[AINode_TahurMarkBanca] La Banca no cubrió ninguna casilla — " +
                                 "¿sala más chica que La Mesa, o grafo sin bounds? No se marca nada.");
                return AIResult.Failed;
            }

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_TahurMarkBanca] IThreatenedAreaService no registrado. " +
                               "Agrega ThreatenedAreaServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            threat.Mark(context.SelfGuid, tiles, Mathf.Clamp(Damage, 0, DamageCeiling), Kind);
            ThreatTelegraphOverlay.ResolveOrCreate()
                .Show(context.SelfGuid, tiles, ThreatOverlayState.Marked);

            // La ronda queda contada como ronda con marca aunque el poke ya haya pasado: si alguien
            // reordena el turno y el poke termina después, el gate de PcTahurCleanRound lo ataja.
            wager.ReportOutcome(wager.LastOutcome, markedPunishment: true);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Fichas a partir de las cuales barre la mesa, sin poder quedar por encima del techo del
        /// pozo.
        /// </summary>
        /// <remarks>
        /// El pozo está clampeado a <see cref="ITahurWagerService.MaxChips"/>: un umbral por encima
        /// de ese techo dejaría el nodo muerto sin que nada lo cante. "Pozo lleno" es la condición
        /// de la ficha, y cuánto es lleno lo decide la banca.
        /// </remarks>
        public int EffectiveThreshold(ITahurWagerService wager)
        {
            int threshold = Mathf.Max(1, ChipsThreshold);
            if (wager == null) return threshold;
            return Mathf.Min(threshold, Mathf.Max(1, wager.MaxChips));
        }
    }
}
