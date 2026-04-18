using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Balance
{
    /// <summary>
    /// Hub de balance del ruleset (TECHNICAL.md §14.7). Config autoritativa de un
    /// "modo de juego" — todos los números del juego que pueden variar entre
    /// rulesets (energy, initiative, rolls, scaling, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Registro.</b> Se agrega a <c>ServiceBootstrapSO.SettingsAssets</c> — el
    /// <c>RegisterByRuntimeType</c> (Foundation#0005) lo registra en el
    /// <c>ServiceLocator</c> bajo su Type runtime (<c>RulesetSO</c>). Los servicios
    /// (EnergyService, TurnOrderService, etc.) lo resuelven via
    /// <c>ServiceLocator.GetService&lt;RulesetSO&gt;()</c>.
    /// </para>
    /// <para>
    /// <b>Odin.</b> Heredamos de <see cref="SerializedScriptableObject"/> para que
    /// los futuros merges puedan exponer tipos polimorficos / dictionaries / etc.
    /// sin re-trabajar la serializacion.
    /// </para>
    /// <para>
    /// <b>Merge hook.</b> Regla: un único <c>RulesetSO.cs</c> en
    /// <c>Rollgeon.Balance</c>. Cada worktree agrega su sub-struct tipado aquí
    /// siguiendo el patrón `[Title("...")]` + `[OdinSerialize]`. Si algún reviewer
    /// ve dos archivos <c>RulesetSO.cs</c> distintos, es un bug de merge.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Balance/Ruleset", fileName = "Ruleset")]
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
        // T100a: EnergyConfig Energy.           ✔ implementado abajo
        // T100c: TurnOrderConfig TurnOrder.     ✔ implementado abajo
        // T100b: RollConfig Rolls.              pendiente
        // T99:   ScalingConfig Scaling.         pendiente
        // Balance#0101 (§14.7 completo) extiende con el resto (CritConfig,
        // LootConfig, ShopConfig, CrapsConfig, ...).
        // ----------------------------------------------------------------

        [Title("Energy (§12.6 — T100a)")]
        [InfoBox("Configuracion de energia del FP. Consumido por EnergyService.")]
        [OdinSerialize]
        private EnergyConfig _energy = new EnergyConfig();

        /// <summary>Knobs de energia (Max / AtRunStart / RegenBase). Lectura runtime.</summary>
        public EnergyConfig Energy => _energy;

        [Title("Initiative / Turn Order (§12.7 — T100c)")]
        [OdinSerialize]
        [Tooltip("Config del orden de turno: rango del speed-die + fallback de initiative " +
                 "para entidades sin stat Speed. Consumido por DefaultInitiativeProvider.")]
        public TurnOrderConfig TurnOrder;

        private void OnValidate()
        {
            _energy?.Validate();
            TurnOrder.OnValidate();
        }
    }
}
