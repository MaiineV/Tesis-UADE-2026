using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// Cómo terminó la liquidación (settle) de una ronda del Tahúr. Lo escribe
    /// <c>AINode_TahurSettleWager</c> y lo leen la rama del poke (vía
    /// <c>PcTahurCleanRound</c>) y el HUD.
    /// </summary>
    public enum TahurSettleOutcome
    {
        /// <summary>No había canto que liquidar (primer turno del combate).</summary>
        None,

        /// <summary>Armó la mano cantada: 0 dmg y, si estaba en La Mesa, cobra el pozo.</summary>
        Exact,

        /// <summary>Armó una mano mejor: su golpe fue ×2, el pozo se movió 2 fichas.</summary>
        Greed,

        /// <summary>Armó una peor (o ninguna): +1 ficha y castigo con forma = cuánto faltó.</summary>
        Miss,

        /// <summary>Fase 2: acertó el canto invertido — lo leyó, liquida como el peor resultado.</summary>
        Read,

        /// <summary>Primera liquidación después del volteo de la carta: de gracia.</summary>
        Grace,
    }

    /// <summary>
    /// Estado persistente del Tahúr entre turnos: el pozo (fichas), la mano cantada, La Mesa
    /// y la lectura de la mano que el jugador jugó en la ronda. Ficha de diseño "El Tahúr"
    /// (piso 3).
    /// </summary>
    /// <remarks>
    /// La Mesa vive acá y no en <c>IThreatenedAreaService</c>: ese se indexa por guid de fuente, así
    /// que la segunda marca del mismo boss pisa la primera y La Mesa (3×3, daño 0) coexiste con el
    /// Castigo en el mismo turno. Global y lazy, con reset en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </remarks>
    public interface ITahurWagerService
    {
        // -----------------------------------------------------------------
        // El pozo
        // -----------------------------------------------------------------

        /// <summary>Fichas acumuladas. Cada fallo suma 1, la codicia 2, el rastrillo 1 por ronda.</summary>
        int Chips { get; }

        /// <summary>Techo de fichas (la banca). Clampea <see cref="Chips"/>.</summary>
        int MaxChips { get; set; }

        /// <summary>Piso de fichas al cobrar: 0 en fase 1, 1 tras el volteo (el pozo nunca vuelve a 0).</summary>
        int ChipsFloor { get; }

        /// <summary>Pago por ficha al cobrar el pozo. Lo publica el nodo de liquidación para el HUD.</summary>
        int PayoutPerChip { get; set; }

        /// <summary>Lo que pagaría cobrar ahora (<see cref="Chips"/> × <see cref="PayoutPerChip"/>).</summary>
        int PendingPayout { get; }

        /// <summary>Cambió el pozo. Para el HUD — no hay <c>EventName</c> apropiado y no se agrega uno.</summary>
        event Action<int> ChipsChanged;

        /// <summary>Suma (o resta) fichas y devuelve el valor final ya clampeado.</summary>
        int AddChips(int amount);

        /// <summary>Fija las fichas al valor dado, clampeado a [0, <see cref="MaxChips"/>].</summary>
        void SetChips(int amount);

        // -----------------------------------------------------------------
        // El canto
        // -----------------------------------------------------------------

        /// <summary>Escalón cantado (1-based sobre la escalera del contrato). 0 = todavía no cantó.</summary>
        int CalledRank { get; }

        /// <summary>ComboId del escalón cantado. Vacío = todavía no cantó.</summary>
        string CalledComboId { get; }

        /// <summary>Fase 2: el cartel pasó de PIDE a LEE — la mano cantada es la que NO hay que armar.</summary>
        bool CallInverted { get; }

        /// <summary>Escalón a armar para cobrar: el cantado en fase 1, el inferior en fase 2. 0 = no cantó.</summary>
        int TargetRank { get; }

        /// <summary>Publica el canto de esta ronda.</summary>
        void SetCall(int rank, string comboId);

        // -----------------------------------------------------------------
        // La fase (el volteo de la carta)
        // -----------------------------------------------------------------

        /// <summary>
        /// Fichas que el rastrillo suma por ronda, solo, desde la fase 1. Es lo que le pone reloj a
        /// "no jugar": sin rastrillo el pozo sólo se mueve cuando el jugador falla.
        /// </summary>
        int RakeChipsPerRound { get; set; }

        /// <summary>La próxima liquidación es de gracia (el canto se armó con las reglas viejas).</summary>
        bool GraceOnNextSettle { get; }

        /// <summary>Voltea la carta: invierte el canto, fija el rastrillo de fase 2 y sube el piso del pozo.</summary>
        void FlipCard(int rakeChipsPerRound, int chipsFloor, bool graceNextSettle);

        /// <summary>Consume la gracia. <c>true</c> si esta liquidación era la de gracia.</summary>
        bool ConsumeGrace();

        // -----------------------------------------------------------------
        // La Mesa
        // -----------------------------------------------------------------

        /// <summary>Casillas de La Mesa (su 3×3, daño 0) — el único lugar desde donde se cobra.</summary>
        IReadOnlyCollection<GridCoord> TableTiles { get; }

        /// <summary><c>true</c> si <paramref name="coord"/> está en La Mesa.</summary>
        bool IsOnTable(GridCoord coord);

        /// <summary>Pinta La Mesa en su posición final del turno.</summary>
        void SetTable(IEnumerable<GridCoord> tiles);

        /// <summary>Levanta La Mesa (fin de combate).</summary>
        void ClearTable();

        // -----------------------------------------------------------------
        // La liquidación
        // -----------------------------------------------------------------

        /// <summary>ComboId de la última mano jugada. Vacío = el jugador no armó nada.</summary>
        string LastPlayedComboId { get; }

        /// <summary>Quién jugó la última mano — el settle solo lee las del jugador.</summary>
        Guid LastPlayedBy { get; }

        /// <summary>Devuelve la última mano jugada y la borra: una ronda sin jugar vale rank 0.</summary>
        string ConsumePlayedHand();

        /// <summary>Cómo terminó la última liquidación.</summary>
        TahurSettleOutcome LastOutcome { get; }

        /// <summary>
        /// <c>true</c> si esta liquidación marcó Castigo. El poke es exclusivo de la rama sin marca:
        /// 12 + 45 rompería el techo de 45 por golpe del piso 3.
        /// </summary>
        bool MarkedPunishmentThisTurn { get; }

        /// <summary>Abre el turno del boss: resetea los flags por-turno.</summary>
        void BeginBossTurn();

        /// <summary>Registra el resultado de la liquidación de este turno.</summary>
        void ReportOutcome(TahurSettleOutcome outcome, bool markedPunishment);

        /// <summary>Vuelve al estado de arranque (pozo 0, sin canto, fase 1, sin mesa).</summary>
        void ResetForNewCombat();
    }
}
