using System.Collections.Generic;

namespace Rollgeon.Combat.ComboLog
{
    /// <summary>
    /// Log del último (o últimos N) combo(s) que el jugador ejecutó en sus ataques
    /// (Sistemas prerequisito Bosses §3). Se escribe al resolver el ataque del jugador y se lee
    /// al inicio del turno siguiente — el Boss 2 (Jefe de Seguridad) lo usa para bloquear el
    /// combo repetido (ventana 1 en Fase 1, 2 en Fase 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tipo, no valor.</b> Se loguea el <c>ComboId</c> (Par, Trío, …), no el valor de los dados.
    /// Si el jugador no logró ningún combo (daño mínimo), se loguea <see cref="NoComboMarker"/>.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> Run-scoped vía limpieza en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </para>
    /// </remarks>
    public interface IComboLogService
    {
        /// <summary>Marcador que representa "ningún combo / daño mínimo" en el log.</summary>
        string NoComboMarker { get; }

        /// <summary>
        /// Registra el combo ejecutado en el ataque recién resuelto. <c>null</c> o vacío se
        /// normaliza a <see cref="NoComboMarker"/> ("daño mínimo / sin combo").
        /// </summary>
        void Record(string comboId);

        /// <summary>El combo más reciente; <c>null</c> si el log está vacío.</summary>
        string LastCombo { get; }

        /// <summary>
        /// Los últimos <paramref name="count"/> combos, del más reciente al más antiguo.
        /// Lista vacía si no hay historial. <paramref name="count"/> &lt;= 0 ⇒ vacío.
        /// </summary>
        IReadOnlyList<string> Last(int count);

        /// <summary>Vacía el log. Usado en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.</summary>
        void Clear();
    }
}
