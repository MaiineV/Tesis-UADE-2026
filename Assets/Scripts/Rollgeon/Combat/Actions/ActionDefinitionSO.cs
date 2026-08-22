using Rollgeon.Effects;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.Actions
{
    /// <summary>
    /// Definicion autoreable de una accion del action economy — UNA fuente de verdad por
    /// ActionId. Replica literal de TECHNICAL.md §12.6.0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los campos son ABI (los <c>.asset</c> deserializan por nombre). Adicional = no-breaking;
    /// rename/delete = breaking coordinado con todos los <c>ActionDefinition.asset</c> ya
    /// autoreados.
    /// </para>
    /// <para>
    /// <b>Compat / BackingAsset.</b> El campo es opcional y sirve para que un catalogo
    /// especifico (ComboCatalogSO, ItemCatalogSO) encuentre el SO tipado desde el
    /// <see cref="ActionId"/>. Si <see cref="BackingAsset"/> es no-nulo y <see cref="Effect"/>
    /// esta vacio, el caller del <c>TurnManager</c> es responsable de despachar al sistema
    /// especifico (T97b combos, ItemSystem, etc.). Ver plan §10 R1.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Actions/Action Definition", fileName = "ActionDefinition")]
    public class ActionDefinitionSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Naming convention <tipo>.<subtipo>.<nombre> — ver TECHNICAL.md §12.6.0. " +
                 "Case-sensitive. Ej: 'combo.full_house', 'attack.basic', 'move', 'skill.heal'.")]
        public string ActionId;

        [Tooltip("Clasificacion del enum §12.6.0. Filtra el dropdown del HUD y el lookup " +
                 "por tipo en ActionCatalogSO.")]
        public ActionType Type;

        [Tooltip("Texto UI mostrado al jugador. NO es el id — el id es estable, este cambia con localizacion.")]
        public string DisplayName;

        [Title("Compat — referencia del SO especifico (opcional)")]
        [OdinSerialize]
        [InfoBox("Para Type = Combo -> referenciar el BaseComboSO. Para Type = UseItem -> el ItemSO. " +
                 "Para Attack/Ability/SkillCheck puro, vive el Effect abajo y este campo queda null. " +
                 "Si BackingAsset != null y Effect.Effects esta vacio, TurnManager cobra 1 roll " +
                 "y el caller externo despacha al sistema especifico. Plan §10 R1.")]
        public ScriptableObject BackingAsset;

        [Title("Effect (si no hay BackingAsset)")]
        [OdinSerialize]
        [Tooltip("Pipeline §8: PreConditions + Effects. Default = new EffectData() (listas vacias). " +
                 "Si esta vacio y hay BackingAsset, el despacho real lo hace el sistema externo.")]
        public EffectData Effect = new EffectData();

    }
}
