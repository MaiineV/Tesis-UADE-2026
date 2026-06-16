using System.Collections.Generic;
using Rollgeon.Dice;
using Rollgeon.Upgrades.Dice.Triggers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Encantamiento — Canal Dados del Sistema de Mejoras In-Run. Composable:
    /// un opcional <see cref="FaceFilter"/> + lista polimórfica de
    /// <see cref="Triggers"/> que reaccionan a eventos del combate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Data-driven.</b> Designers crean .asset desde <c>Create → Rollgeon →
    /// Upgrades → Dice → Enchantment</c> y configuran sin tocar código:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="AllowedDiceTypes"/> — filtro de qué dados aceptan este encantamiento (empty = todos).</description></item>
    /// <item><description><see cref="FaceFilter"/> — restricción de caras (opcional, polimórfico — <c>OnlyEvens</c>, <c>FaceRange</c>, etc.).</description></item>
    /// <item><description><see cref="Triggers"/> — hooks de comportamiento (polimórfico). Cada concrete declara qué eventos consume.</description></item>
    /// </list>
    /// <para>
    /// <b>Encantamientos malvados</b> = mismo SO, simplemente con triggers
    /// negativos en la lista (ej. <c>SpendGoldForDamage</c>, <c>NoGoldNoDamage</c>,
    /// <c>ExplodeIfUnused</c>). El sistema es agnóstico al "color" — la
    /// herramienta deja componer lo que quiera el diseñador.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Dice/Enchantment",
        fileName = "Enchantment")]
    public class EnchantmentSO : UpgradeSO
    {
        [Title("Compatibility")]
        [InfoBox("Tipos de dado a los que este encantamiento puede aplicarse. " +
                 "Vacío = aplica a todos los tipos. La validación previa al apply " +
                 "(Phase 4) usa esta lista para filtrar el pool de generación.")]
        [OdinSerialize]
        [ListDrawerSettings(ShowFoldout = false, DefaultExpandedState = true)]
        protected List<DiceType> _allowedDiceTypes = new List<DiceType>();

        [Title("Face Restriction (optional)")]
        [InfoBox("Restringe el set de caras posibles del dado. La validación previa " +
                 "rechaza el apply si la intersección con encantamientos ya activos " +
                 "queda vacía. Null = sin restricción de caras.")]
        [OdinSerialize, SerializeReference]
        protected IFaceFilter _faceFilter;

        [Title("Triggers")]
        [InfoBox("Comportamiento del encantamiento. Cada trigger implementa uno o más " +
                 "hooks (IOnDiceRolled, IOnComboMatched, IOnTurnFinished, etc.) y " +
                 "consume EffectIntReader para valores numéricos — designers pueden " +
                 "mezclar literales, current gold, combo counters, etc.")]
        [OdinSerialize, SerializeReference]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true, DefaultExpandedState = true)]
        protected List<IEnchantmentTrigger> _triggers = new List<IEnchantmentTrigger>();

        /// <inheritdoc />
        public override UpgradeChannel Channel => UpgradeChannel.Dice;

        /// <summary>Tipos de dado válidos. Empty = todos.</summary>
        public IReadOnlyList<DiceType> AllowedDiceTypes => _allowedDiceTypes;

        /// <summary>Filtro de caras opcional.</summary>
        public IFaceFilter FaceFilter => _faceFilter;

        /// <summary>Triggers polimórficos que reaccionan a eventos del combate.</summary>
        public IReadOnlyList<IEnchantmentTrigger> Triggers => _triggers;

        /// <summary>
        /// <c>true</c> si <paramref name="type"/> figura en
        /// <see cref="AllowedDiceTypes"/> o si la lista está vacía.
        /// </summary>
        public bool IsCompatibleWith(DiceType type)
        {
            if (_allowedDiceTypes == null || _allowedDiceTypes.Count == 0) return true;
            return _allowedDiceTypes.Contains(type);
        }
    }
}
