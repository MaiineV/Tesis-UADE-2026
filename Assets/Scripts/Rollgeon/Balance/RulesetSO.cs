using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Balance
{
    /// <summary>
    /// Hub de balance del ruleset (TECHNICAL.md §14.7). <b>ESQUELETO</b> — en esta
    /// worktree (T100a) solo existe la seccion <see cref="EnergyConfig"/>.
    /// Balance#0101 (§14.7 completo) y hermanos extienden este mismo archivo
    /// agregando nuevas sub-secciones ([Title("Actions")] / [Title("Combat")] / ...).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Registro.</b> Se agrega a <c>ServiceBootstrapSO.SettingsAssets</c> — el
    /// <c>RegisterByRuntimeType</c> (Foundation#0005) lo registra en el
    /// <c>ServiceLocator</c> bajo su Type runtime (<c>RulesetSO</c>). El
    /// <c>EnergyService</c> lo resuelve via <c>ServiceLocator.GetService&lt;RulesetSO&gt;()</c>.
    /// </para>
    /// <para>
    /// <b>Odin.</b> Heredamos de <see cref="SerializedScriptableObject"/> para que
    /// los futuros merges puedan exponer tipos polimorficos / dictionaries / etc.
    /// sin re-trabajar la serializacion.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Balance/Ruleset", fileName = "Ruleset")]
    public class RulesetSO : SerializedScriptableObject
    {
        // [Merge hook] — T100c/T101/etc extend aqui. Mantener [Title("Energy")]
        // como primera seccion para estabilidad visual del Inspector.

        [Title("Energy (§12.6 — T100a)")]
        [InfoBox("Configuracion de energia del FP. Otras secciones (§14.7 completo) " +
                 "las agrega Balance#0101 / T100c / T101 en este mismo archivo.")]
        [OdinSerialize]
        private EnergyConfig _energy = new EnergyConfig();

        /// <summary>Knobs de energia (Max / AtRunStart / RegenBase). Lectura runtime.</summary>
        public EnergyConfig Energy => _energy;

        // [Merge hook] — T100c/T101/etc extend aqui (acciones, combate, turn order, ...).

        private void OnValidate()
        {
            _energy?.Validate();
        }
    }
}
