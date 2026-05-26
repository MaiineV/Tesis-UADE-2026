using System;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Runtime controller del prefab de puerta entre salas (TECHNICAL.md §13.6).
    /// Se parentes bajo la sala instanciada (<see cref="RoomInstance.SpawnedPrefab"/>)
    /// por el <c>DungeonManager</c> cuando detecta que la <see cref="DoorSlotRef"/>
    /// del <see cref="RoomLayout"/> tiene vecino. Si la sala no conecta por esa
    /// dirección, se activa el <see cref="DoorSlotRef.WallPlug"/> en su lugar.
    /// </summary>
    [AddComponentMenu("Rollgeon/Dungeon/Door Controller")]
    public sealed class DoorController : MonoBehaviour
    {
        /// <summary>Owner runtime de la sala que contiene esta puerta.</summary>
        public Guid OwnerRoomInstanceId;

        /// <summary>Dirección cardinal de esta puerta en la sala dueña.</summary>
        public DoorDirection Direction;

        /// <summary>
        /// Id determinístico para el <c>ObjectStates</c> dict de
        /// <see cref="RoomInstance"/> — matcheando
        /// <c>DungeonManager.DoorStateKey(Direction)</c> ("door_N/S/E/W").
        /// </summary>
        public string SpawnPointId;

        [Header("Visual children")]
        [Tooltip("Mesh + collider activos cuando la puerta está abierta.")]
        [SerializeField] private GameObject _meshOpen;

        [Tooltip("Mesh + collider activos cuando la puerta está cerrada (lock combate o skill check).")]
        [SerializeField] private GameObject _meshClosed;

        [Tooltip("Mesh de pared tapiada — cuando no hay vecino en esa dirección.")]
        [SerializeField] private GameObject _wallPlug;

        public DoorVisualState CurrentState { get; private set; } = DoorVisualState.Open;

#if UNITY_EDITOR
        /// <summary>Editor-only: nombre del campo serializado de meshOpen (para SerializedObject).</summary>
        public const string EditorMeshOpenField  = nameof(_meshOpen);
        public const string EditorMeshClosedField = nameof(_meshClosed);
        public const string EditorWallPlugField   = nameof(_wallPlug);

        public GameObject EditorMeshOpen   => _meshOpen;
        public GameObject EditorMeshClosed => _meshClosed;
        public GameObject EditorWallPlug   => _wallPlug;
#endif

        private void Awake()
        {
            EnsureTooltipComponents();
        }

        // Auto-attach del tooltip de "Forzar Puerta": un solo WorldTooltipTrigger en el
        // root del DoorController (siempre active). El trigger usa Physics.Raycast manual
        // que acepta hits en cualquier descendant — cubre los meshes hijos sin necesidad
        // de un trigger por cada uno. El binder vive en el mismo GO.
        private void EnsureTooltipComponents()
        {
            if (GetComponent<WorldTooltipTrigger>() == null)
                gameObject.AddComponent<WorldTooltipTrigger>();

            var binder = GetComponent<HeroActionTooltipBinder>();
            if (binder == null)
            {
                binder = gameObject.AddComponent<HeroActionTooltipBinder>();
                // AddComponent disparó Awake con defaults (Healing). Configure los pisa
                // a la semántica de Forzar Puerta antes de que se invoque BuildText.
                binder.Configure(HeroBehaviorSlot.ForceDoor, GamePhase.Combat, onlyDuringCombat: true);
            }

            // El trigger del root ya quedó configurado por el Awake del binder.
            // ConfigureExternalTriggers es no-op acá pero queda como hook por si en el
            // futuro hay triggers en hijos (caso de prefabs custom).
            binder.ConfigureExternalTriggers();
        }

        public void SetState(DoorVisualState state)
        {
            CurrentState = state;

            bool open    = state == DoorVisualState.Open;
            bool locked  = state == DoorVisualState.LockedCombat
                           || state == DoorVisualState.LockedSkillCheck;
            bool tapiada = state == DoorVisualState.Tapiada;

            if (_meshOpen   != null) _meshOpen.SetActive(open);
            if (_meshClosed != null) _meshClosed.SetActive(locked);
            if (_wallPlug   != null) _wallPlug.SetActive(tapiada);
        }
    }

    /// <summary>
    /// Estados visuales de una puerta. El <c>DoorController</c> solo togglea
    /// los meshes; la lógica de locks vive en <c>DungeonManager</c> + los
    /// behaviors del InteractableComponent del prefab (§13.6 / §7.7).
    /// </summary>
    public enum DoorVisualState
    {
        /// <summary>Atravesable — sala actual Cleared o door <c>Forced</c>.</summary>
        Open = 0,

        /// <summary>Isaac-lock durante combate; se abre al <c>OnCombatEnd(Victory)</c>.</summary>
        LockedCombat = 1,

        /// <summary>Locked fuera de combate — sólo se abre con skill check exitoso.</summary>
        LockedSkillCheck = 2,

        /// <summary>No hay vecino por esta dirección — pared tapiada.</summary>
        Tapiada = 3,
    }
}
