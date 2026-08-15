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
    /// <para>
    /// <b>Por qué un servicio propio y no <c>IThreatenedAreaService</c>.</b> Ese servicio se
    /// indexa por el guid de la fuente, así que una segunda marca del mismo boss pisa la
    /// primera: La Mesa (3×3, daño 0, cian) y el Castigo (la marca que sí pega) coexisten en
    /// el mismo turno. El Castigo sigue viviendo en <c>IThreatenedAreaService</c> — el único
    /// que pega — y La Mesa vive acá.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> Global vía <see cref="TahurWagerService.ResolveOrCreate"/> (lazy, sin
    /// wiring manual en <c>ServiceBootstrap.ExtraServices</c>), con reset en
    /// <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </para>
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

        /// <summary>
        /// Piso de fichas al cobrar: 0 en fase 1, 1 tras el volteo — con el rastrillo activo el
        /// pozo nunca vuelve a 0 y estancarse deja de ser posible.
        /// </summary>
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

        /// <summary>
        /// Escalón que hay que armar para cobrar: el cantado en fase 1, el inmediatamente
        /// inferior en fase 2. 0 si todavía no cantó.
        /// </summary>
        int TargetRank { get; }

        /// <summary>Publica el canto de esta ronda.</summary>
        void SetCall(int rank, string comboId);

        // -----------------------------------------------------------------
        // La fase (el volteo de la carta)
        // -----------------------------------------------------------------

        /// <summary>
        /// Fichas que el rastrillo suma por ronda, solo. Corre desde la fase 1 —lo escribe
        /// <c>AINode_TahurSettleWager</c> en cada liquidación— y el volteo puede subirlo.
        /// </summary>
        /// <remarks>
        /// Es lo que le pone reloj a "no jugar": sin rastrillo el pozo sólo se mueve cuando el
        /// jugador falla, así que renunciar al pozo dejaba el Castigo clavado en su escalón más
        /// barato y esquivable de a uno.
        /// </remarks>
        int RakeChipsPerRound { get; set; }

        /// <summary>La próxima liquidación es de gracia (el canto se armó con las reglas viejas).</summary>
        bool GraceOnNextSettle { get; }

        /// <summary>
        /// Se voltea la carta: invierte el canto, fija el rastrillo de fase 2 y levanta el piso
        /// del pozo.
        /// </summary>
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

        /// <summary>
        /// Devuelve la última mano jugada y la borra: una ronda sin jugar mano vale rank 0
        /// (armar nada es el fallo más grande), no la mano de la ronda anterior.
        /// </summary>
        string ConsumePlayedHand();

        /// <summary>Cómo terminó la última liquidación.</summary>
        TahurSettleOutcome LastOutcome { get; }

        /// <summary>
        /// <c>true</c> si esta liquidación marcó Castigo. La rama del poke es exclusiva de la de
        /// marcar: 12 + 45 rompería el techo de 45 por golpe del piso 3.
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
