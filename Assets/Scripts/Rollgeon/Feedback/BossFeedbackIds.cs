namespace Rollgeon.Feedback
{
    /// <summary>
    /// Ids de feedback de los seis jefes de casino. Viven en runtime —y no en el instalador que los
    /// autora— porque los que los consumen son los nodos de IA, que no pueden ver el assembly de
    /// Editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Una sola fuente.</b> <c>BossFeedbackInstaller</c> escribe las entradas del
    /// <c>FeedbackDB</c> leyendo de acá, así que un id sólo puede estar mal en un lugar. Cambiar el
    /// string sin re-correr <c>Tools → Rollgeon → Bosses → Build Boss Feedback</c> deja la entrada
    /// vieja huérfana en el asset y el nodo pidiendo una que no existe — que degrada con un warning,
    /// no cuelga, pero tampoco se ve.
    /// </para>
    /// <para>
    /// Convención heredada de las 27 entradas hechas a mano: <c>&lt;canal&gt;.&lt;actor&gt;.&lt;acción&gt;</c>,
    /// con <c>anim.</c> / <c>vfx.</c> / <c>feel.</c> / <c>sfx.</c> de prefijo.
    /// </para>
    /// <para>
    /// <b>Sin <c>sfx.</c> todavía</b>: el proyecto no tiene un solo clip de jefe — los 23 wavs son de
    /// dados y del breakdown de UI. Cuando existan, van acá con el mismo patrón.
    /// </para>
    /// </remarks>
    public static class BossFeedbackIds
    {
        // ---- El Croupier ----

        /// <summary>
        /// El único ataque directo que le queda: el tiro del abanico de cartas
        /// (<c>AINode_RangedShot</c> del beat "Reparte"). Id propio y no reusar
        /// <see cref="CroupierCantoAnim"/> —que ya apunta al mismo <c>Attack_Range</c>— porque
        /// "canto" nombra la ruleta que se retiró: un id que miente sobre lo que hace el nodo es
        /// justo lo que hizo que este ataque quedara mudo un rediseño entero. Además es la
        /// convención de los otros cuatro jefes con ataque a distancia (<c>.range</c> /
        /// <c>.range_impact</c>).
        /// </summary>
        public const string CroupierRangeAnim       = "anim.boss.croupier.range";
        public const string CroupierRangeImpactVfx  = "vfx.boss.croupier.range_impact";
        public const string CroupierRangeImpactFeel = "feel.boss.croupier.range_impact";

        /// <summary>
        /// El salto de <c>AINode_TeleportAwayToEdge</c>. Sin gesto, la reubicación es un cambio de
        /// posición seco que se lee como un bug y no como un teletransporte.
        /// </summary>
        public const string CroupierTeleportAnim = "anim.boss.croupier.teleport";

        /// <summary>
        /// El gesto con el que prende lo que marcó (<c>AINode_IgniteArea</c>). Es el clip de melee y
        /// no <see cref="CroupierRangeAnim"/> a propósito: el disparo ya usa ése, y repetirlo dejaría
        /// dos tiempos distintos del ciclo viéndose igual.
        /// </summary>
        public const string CroupierMeleeAnim  = "anim.boss.croupier.melee";
        // Sin llamador desde que se retiro la ruleta: el unico que lo pide es AINode_SpinWheel, que
        // ya no esta en el arbol. Se deja porque el nodo sigue compilando y borrar la entry lo
        // dejaria pidiendo una que no existe.
        public const string CroupierCantoAnim  = "anim.boss.croupier.canto";
        public const string CroupierImpactVfx  = "vfx.boss.croupier.impact";
        public const string CroupierImpactFeel = "feel.boss.croupier.impact";

        // La confiscación del dado del número que cayó. Van sobre el JUGADOR, no sobre el jefe: el
        // gesto es del paño llevándose el dado, igual que la detonación, y no hay animación de
        // Croupier que lo acompañe.
        public const string CroupierConfiscaVfx  = "vfx.boss.croupier.confisca";
        public const string CroupierConfiscaFeel = "feel.boss.croupier.confisca";

        // ---- La Bandida ----
        public const string BandidaMeleeAnim       = "anim.boss.bandida.melee";
        public const string BandidaRangeAnim       = "anim.boss.bandida.range";
        public const string BandidaArmAnim         = "anim.boss.bandida.arm";
        public const string BandidaImpactVfx       = "vfx.boss.bandida.impact";
        public const string BandidaRangeImpactVfx  = "vfx.boss.bandida.range_impact";
        public const string BandidaImpactFeel      = "feel.boss.bandida.impact";
        public const string BandidaRangeImpactFeel = "feel.boss.bandida.range_impact";

        // ---- El Cajero ----
        public const string CajeroMeleeAnim      = "anim.boss.cajero.melee";
        public const string CajeroShotAnim       = "anim.boss.cajero.shot";

        /// <summary>Gesto propio: con el <c>Attack_Melee</c> del mandoble los dos tiempos del ciclo se veían iguales.</summary>
        public const string CajeroShoveAnim      = "anim.boss.cajero.shove";

        /// <summary>
        /// El turno en que apunta el cañonazo, antes de dispararlo. Comparte el <c>Idle_Var</c> del
        /// empujón porque el rig declara tres triggers y el jefe tiene cuatro tiempos; nunca
        /// conviven (el empujón es pegado y el aviso es de lejos). Id propio igual: cuando arte le
        /// autore un clip se cambia acá y nada más.
        /// </summary>
        public const string CajeroAimAnim        = "anim.boss.cajero.aim";
        public const string CajeroImpactVfx      = "vfx.boss.cajero.impact";
        public const string CajeroShotImpactVfx  = "vfx.boss.cajero.shot_impact";
        public const string CajeroImpactFeel     = "feel.boss.cajero.impact";
        public const string CajeroShotImpactFeel = "feel.boss.cajero.shot_impact";

        /// <summary>
        /// El mordisco de la Comisión, el minion del Cajero. Va fuera del namespace
        /// <c>anim.boss.*</c> a propósito: el bicho viste <c>GeneralDirector_Animated</c>, cuyo
        /// animator declara un solo trigger, <c>Attack</c>. Sin este id el nodo cae al fallback del
        /// disparo del Cajero, que pide <c>Attack_Range</c>: un trigger que su animator no tiene, o
        /// sea silencio.
        /// </summary>
        public const string ComisionBiteAnim = "anim.enemy.comision.bite";

        // ---- El Anotador ----
        public const string AnotadorMeleeAnim  = "anim.boss.anotador.melee";
        public const string AnotadorPencilAnim = "anim.boss.anotador.pencil";
        public const string AnotadorImpactVfx  = "vfx.boss.anotador.impact";
        public const string AnotadorImpactFeel = "feel.boss.anotador.impact";

        // ---- La Generala ----
        public const string GeneralaMeleeAnim       = "anim.boss.generala.melee";
        public const string GeneralaRangeAnim       = "anim.boss.generala.range";
        public const string GeneralaRollAnim        = "anim.boss.generala.roll";
        public const string GeneralaCupSlamAnim     = "anim.boss.generala.cup_slam";

        /// <summary>
        /// Reponer la mesa de dados. Usa <c>Heal</c>, el gesto de brazos en alto del rig: era el
        /// único clip del DiceBoss sin usar y es lo más parecido a "invocar" que tiene.
        /// </summary>
        public const string GeneralaSummonAnim      = "anim.boss.generala.summon";

        /// <summary>
        /// La escarcha de la mesa (<c>AINode_GeneralaFrostRing</c>). Reusa <c>Attack_Range</c>: el
        /// hielo cae en un anillo lejos de sus manos, y es el único gesto del rig que empuja algo
        /// hacia afuera. Los cuatro triggers del DiceBoss ya están tomados, así que compartir es la
        /// única opción hasta que arte le autore un clip propio.
        /// </summary>
        public const string GeneralaFrostAnim       = "anim.boss.generala.frost";
        public const string GeneralaImpactVfx       = "vfx.boss.generala.impact";
        public const string GeneralaRangeImpactVfx  = "vfx.boss.generala.range_impact";
        public const string GeneralaImpactFeel      = "feel.boss.generala.impact";
        public const string GeneralaRangeImpactFeel = "feel.boss.generala.range_impact";

        // ---- El Tahúr ----
        public const string TahurMeleeAnim       = "anim.boss.tahur.melee";
        public const string TahurRangeAnim       = "anim.boss.tahur.range";
        public const string TahurPokeAnim        = "anim.boss.tahur.poke";
        public const string TahurBancaAnim       = "anim.boss.tahur.banca";
        public const string TahurImpactVfx       = "vfx.boss.tahur.impact";
        public const string TahurRangeImpactVfx  = "vfx.boss.tahur.range_impact";
        public const string TahurImpactFeel      = "feel.boss.tahur.impact";
        public const string TahurRangeImpactFeel = "feel.boss.tahur.range_impact";
    }
}
