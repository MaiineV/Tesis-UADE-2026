using Rollgeon.Meta.Conditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Meta
{
    /// <summary>
    /// Definición autoral de un desbloqueable de meta-progresión (#164). Se edita
    /// con la <b>Unlock Condition Tool</b> (<c>Rollgeon → Unlock Condition Tool</c>):
    /// elemento a desbloquear + categoría, condición armada con bloques
    /// <see cref="IUnlockCondition"/> (combinables con AND/OR), filtro de outcome
    /// (ganada/perdida/ambas) y texto de pista visible al jugador.
    /// <para>
    /// <b>Semántica de pool base.</b> Todo contenido SIN una definición que lo
    /// apunte (<see cref="Category"/> + <see cref="TargetId"/>) se considera parte
    /// del pool base — disponible desde la primera sesión. Por eso el estado
    /// inicial (Guerrero + D3/D4/D6) no necesita seed: simplemente no se gatea.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Meta/Unlock Definition", fileName = "Unlock")]
    public class UnlockDefinitionSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Required]
        [Tooltip("Id estable del desbloqueo (ej. 'unlock.dice.d8'). Se persiste en el save file.")]
        public string UnlockId;

        [Tooltip("Nombre legible para UI (pantalla de desbloqueos + notificación).")]
        public string DisplayName;

        [Title("Target")]
        [Tooltip("Categoría del elemento a desbloquear.")]
        public UnlockableCategory Category;

        [Required]
        [Tooltip("Id del elemento según la categoría: DiceType ('D8'), ClassHeroSO.EntityId, " +
                 "IShopRewardEntry.EntryId, UpgradeSO.UpgradeId o RoomSO.RoomId.")]
        public string TargetId;

        [Title("Player-facing text")]
        [TextArea(2, 4)]
        [Tooltip("Descripción + efecto completo, visible una vez desbloqueado.")]
        public string Description;

        [TextArea(2, 4)]
        [Tooltip("Pista visible mientras está bloqueado. Orienta sin revelar la condición exacta.")]
        public string HintText;

        [Title("Condition")]
        [Tooltip("En qué desenlace de run aplica la condición. Solo 'Any' permite unlock mid-run.")]
        public UnlockOutcomeFilter AppliesTo = UnlockOutcomeFilter.Won;

        [OdinSerialize]
        [InfoBox("Condición raíz. Componer con AndCondition / OrCondition para condiciones compuestas.")]
        public IUnlockCondition Condition;

        /// <summary><c>true</c> si el filtro de outcome admite el desenlace dado.</summary>
        public bool AppliesToOutcome(bool runWon)
        {
            return AppliesTo switch
            {
                UnlockOutcomeFilter.Won => runWon,
                UnlockOutcomeFilter.Lost => !runWon,
                _ => true,
            };
        }

        /// <summary>
        /// Clave estable del <b>elemento</b> desbloqueado (categoría + target). El save
        /// persiste estas claves: dos definiciones que apuntan al mismo elemento
        /// desbloquean lo mismo.
        /// </summary>
        public string TargetKey => MakeTargetKey(Category, TargetId);

        /// <summary>Builder canónico de la clave categoría:target.</summary>
        public static string MakeTargetKey(UnlockableCategory category, string targetId)
            => $"{category}:{targetId}";
    }
}
