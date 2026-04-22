using System;
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
