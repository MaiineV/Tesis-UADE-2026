using System;
using System.Collections.Generic;
using Rollgeon.Dungeon.Components;

namespace Rollgeon.Dungeon.State
{
    /// <summary>
    /// Estado serializable por objeto de una sala (TECHNICAL.md §13.6.1).
    /// Vive dentro de <see cref="SerializableObjectStates"/> con
    /// <c>[SerializeReference]</c> para preservar el subtipo concreto al
    /// round-trip. Los stubs (<see cref="ChestState"/>, <see cref="PotionState"/>,
    /// <see cref="ShopItemState"/>) existen para cerrar la jerarquía y evitar
    /// migraciones de datos cuando se agreguen sus consumidores.
    /// </summary>
    [Serializable]
    public abstract class RoomObjectState
    {
        public string SpawnPointId;
        public bool Consumed;
    }

    /// <summary>Estado de una puerta — Isaac-lock en combate + skill-check en combat.</summary>
    [Serializable]
    public class DoorState : RoomObjectState
    {
        public DoorDirection Direction;

        /// <summary>Atravesada vía skill check en combate (§13.6, §12 DoorPrefab ejemplo).</summary>
        public bool Forced;

        /// <summary>Abierta tras clearear la sala. Persiste entre entradas.</summary>
        public bool Unlocked;
    }

    /// <summary>
    /// Estado de un spawn point de enemigo. Permite que enemigos vivos re-aparezcan
    /// con <see cref="CurrentHP"/> guardado en posición random entre spawn points
    /// libres, y que los muertos no respawneen.
    /// </summary>
    [Serializable]
    public class EnemySpawnState : RoomObjectState
    {
        /// <summary><c>BaseEntitySO.EntityId</c> del <c>EnemyDataSO</c> usado.</summary>
        public string EnemyDataSOId;

        public int CurrentHP;
        public bool IsDead;
        public int SpawnPointIndex;
    }

    /// <summary>Cofre — stub, sin consumidor hoy. Cierra la jerarquía (§13.6.1).</summary>
    [Serializable]
    public class ChestState : RoomObjectState
    {
        public bool Opened;
        public List<string> LootRolled = new List<string>();
    }

    /// <summary>Poción — stub, sin consumidor hoy.</summary>
    [Serializable]
    public class PotionState : RoomObjectState
    {
        public bool Collected;
    }

    /// <summary>Shop item — stub, sin consumidor hoy.</summary>
    [Serializable]
    public class ShopItemState : RoomObjectState
    {
        public bool Purchased;
        public string ReservedItemId;
        public int ReservedPrice;
    }
}