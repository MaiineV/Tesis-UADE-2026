using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Dice;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Items;
using Rollgeon.Phase;
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

        [Title("Visual")]
        [Tooltip("Prefab que se instancia como pawn visual del hero. " +
                 "Debe tener un EntityPawn (se agrega en runtime si falta).")]
        public GameObject VisualPrefab;

        [Title("Phase Behaviors")]
        [InfoBox("Behaviors por fase. IsBaseBehavior + Slot para mapear a un slot base. " +
                 "IsBaseBehavior=false define una accion custom. " +
                 "AllowedPhases determina la fase. ShowConditions controla visibilidad.")]
        [OdinSerialize]
        [ListDrawerSettings(DraggableItems = true)]
        public List<HeroActionBehavior> PhaseBehaviors = new List<HeroActionBehavior>();

        public HeroActionBehavior ResolveBaseBehavior(HeroBehaviorSlot slot, GamePhase phase)
        {
            return PhaseBehaviors?
                       .FirstOrDefault(b => b != null
                                            && b.IsBaseBehavior
                                            && b.Slot == slot
                                            && b.AllowedPhases.Allows(phase));
        }

        public List<HeroActionBehavior> GetBehaviorsForPhase(GamePhase phase)
        {
            var baseBehaviors = Enum.GetValues(typeof(HeroBehaviorSlot))
                .Cast<HeroBehaviorSlot>()
                .Select(slot => ResolveBaseBehavior(slot, phase))
                .Where(b => b != null && b.AllowedPhases.Allows(phase));

            var customBehaviors = (PhaseBehaviors ?? Enumerable.Empty<HeroActionBehavior>())
                .Where(b => b != null && !b.IsBaseBehavior && b.AllowedPhases.Allows(phase));

            return baseBehaviors.Concat(customBehaviors).ToList();
        }

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

        [Title("Dice Bag Pool (Fase 2)")]
        [Tooltip("Pool de dados disponibles para la clase. El jugador arma su bolsa de 5 " +
                 "eligiendo de aca en BuildSelectionScreen. Si es null, el flujo cae al " +
                 "fallback de StartingDiceBagRef / Resources (Fase 1).")]
        public DiceBagPoolSO DiceBagPool;

        [Tooltip("Pasiva de la clase (§4.4). Null = sin pasiva.")]
        public ClassPassiveSO Passive;

        [Title("Starting Inventory")]
        [InfoBox("Items que el jugador recibe automáticamente al arrancar la run. Ej: 1 poción de healing.")]
        [SerializeField]
        public List<ItemSO> StartingItems = new List<ItemSO>();
    }
}
