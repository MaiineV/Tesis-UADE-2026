using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Balance
{
    /// <summary>
    /// Config autoritativa de un "modo de juego" (TECHNICAL.md §14.7). Encarna
    /// todos los números del juego que pueden variar entre rulesets — energy,
    /// rolls, scaling, initiative, etc.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stub inicial</b> — este worktree (System#0100c) declara el SO con la
    /// sección <c>TurnOrder</c> únicamente. Otras tareas del sprint
    /// (T100a Energy, T100b Rolls, T99 Scaling) extienden el mismo archivo
    /// agregando sus sub-structs tipados.
    /// </para>
    /// <para>
    /// [Merge hook] — otros worktrees agregan sub-structs tipados acá
    /// (p.e. <c>public EnergyConfig Energy;</c>, <c>public RollConfig Rolls;</c>).
    /// Regla: un único <c>RulesetSO.cs</c> en <c>Rollgeon.Balance</c>; si algún
    /// reviewer ve dos archivos <c>RulesetSO.cs</c> distintos, es un bug de
    /// merge.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Meta/Ruleset", fileName = "Ruleset_New")]
    public class RulesetSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Identificador estable del ruleset (arcade, hardcore, relajado, etc.). " +
                 "Usado por el RulesetCatalogSO para lookup.")]
        public string RulesetId;

        [Tooltip("Nombre legible mostrado al jugador en la pantalla de selección de modo.")]
        public string DisplayName;

        [TextArea]
        [Tooltip("Descripción breve del modo de juego.")]
        public string Description;

        // ----------------------------------------------------------------
        // [Merge hook] — otros worktrees agregan sub-structs tipados acá.
        // T100a: EnergyConfig Energy.
        // T100b: RollConfig Rolls.
        // T99:   ScalingConfig Scaling (curvas de HP/damage/gold por piso).
        // Este worktree (T100c) contribuye TurnOrder.
        // ----------------------------------------------------------------

        [OdinSerialize]
        [Tooltip("Config del orden de turno: rango del speed-die + fallback de initiative " +
                 "para entidades sin stat Speed. Consumido por DefaultInitiativeProvider.")]
        public TurnOrderConfig TurnOrder;

        private void OnValidate()
        {
            TurnOrder.OnValidate();
        }
    }
}
