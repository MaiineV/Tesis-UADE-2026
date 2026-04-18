using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Heroes
{
    /// <summary>
    /// <b>Minimal skeleton</b> del SO de clase heroe (TECHNICAL.md §4.1 / §5.3). Solo incluye la
    /// identidad minima + el <see cref="ContractSheet"/> — el resto de campos (Passive,
    /// StartingDiceBag, Portrait full, Stats base) son stubs marcados para que la tarea futura
    /// de Hero Template los extienda sin romper ninguna referencia existente.
    /// <para>
    /// <b>Hereda <see cref="SerializedScriptableObject"/></b> (Odin) para round-trip polimorfico
    /// de la lista <see cref="ContractSheet.Combos"/> (<see cref="Rollgeon.Combos.BaseComboSO"/>
    /// es polimorfico).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Heroes/Class Hero", fileName = "ClassHero")]
    public class ClassHeroSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Id canonico de la clase (ej. 'hero.warrior'). Usado por screens + catalogos.")]
        public string EntityId;

        [Tooltip("Nombre legible para UI (ej. 'Guerrero').")]
        public string DisplayName;

        [TextArea]
        [Tooltip("Descripcion breve mostrada en la pantalla de seleccion de clase (#98).")]
        public string Description;

        [Title("Contract (§5.3)")]
        [InfoBox("Lista de 8 combos en orden de prioridad ascendente. Para Warrior: " +
                 "[Par, DoblePar, SumaX, Trio, Escalera, FullHouse, Poker, Generala]. " +
                 "Ver docs/setup/Content#0097b_WarriorContractAndWeakness.md.")]
        [OdinSerialize]
        [Required]
        public ContractSheet Sheet = new ContractSheet();

        // ------------------------------------------------------------------
        // [STUB] — elevated by Hero Template task.
        // Estos campos existen para que la tarea futura los extienda sin
        // breaking changes de asset. NO los consume este worktree.
        // ------------------------------------------------------------------

        [Title("[STUB] — elevated by Hero Template task")]
        [InfoBox("Campos placeholder para la futura tarea de Hero Template. Sprint 03 solo usa " +
                 "EntityId, DisplayName, Description, Sheet. NO referenciar estos en gameplay hasta " +
                 "que la tarea de Hero Template los eleve.", InfoMessageType.Warning)]
        [Tooltip("[STUB] — elevated by Hero Template task. HP base de la clase.")]
        public int BaseMaxHp;

        [Tooltip("[STUB] — elevated by Hero Template task. Speed base (initiative).")]
        public int BaseSpeed;

        [Tooltip("[STUB] — elevated by Hero Template task. Portrait para UI de seleccion.")]
        public Sprite Portrait;

        [Tooltip("[STUB] — elevated by Hero Template task. Opaque ref al DiceBagSO inicial " +
                 "de la clase (DiceBagSO aun no existe).")]
        public ScriptableObject StartingDiceBagRef;

        [Tooltip("[STUB] — elevated by Hero Template task. Opaque ref al PassiveAbilitySO de la clase.")]
        public ScriptableObject PassiveRef;
    }
}
