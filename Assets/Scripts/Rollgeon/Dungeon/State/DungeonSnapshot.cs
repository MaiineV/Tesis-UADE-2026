using System;
using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.State
{
    /// <summary>
    /// Snapshot serializable del estado espacial de un piso — lo captura/restaura
    /// <see cref="DungeonManager"/> como <c>ISaveable</c> (SaveKey <c>run.dungeon_state</c>,
    /// Feature#0028). Persiste dónde está el player (sala + tile) y el estado por sala
    /// (limpiada/visitada + <see cref="RoomObjectState"/>s: enemigos, puertas, cofres…).
    /// <para>
    /// <b>Clave estable = celda, no InstanceId.</b> <see cref="RoomInstance.InstanceId"/>
    /// se regenera (<c>Guid.NewGuid()</c>) en cada <c>GenerateFromPlan</c>; la topología,
    /// en cambio, es determinística por seed, así que <see cref="RoomInstance.GridCell"/>
    /// (<see cref="Vector2Int"/>) es el identificador estable cross-save.
    /// </para>
    /// <para>
    /// <b>Dos sistemas de coordenadas.</b> <see cref="CurrentCell"/> y
    /// <see cref="RoomSnapshot.Cell"/> son celdas de sala en la topología Isaac;
    /// <see cref="PlayerCoord"/> y <see cref="EnemySpawnState.LastCell"/> son tiles
    /// de grilla DENTRO de una sala.
    /// </para>
    /// <para>
    /// <b>ObjectStates como <see cref="Dictionary{TKey,TValue}"/>.</b> Odin
    /// <c>SerializationUtility</c> serializa contenedores de tipo base abstracto de
    /// forma polimórfica nativa (escribe el tipo concreto) — no dependemos del
    /// <c>[SerializeReference]</c> privado de <see cref="SerializableObjectStates"/>,
    /// que es del serializer de Unity. Al restaurar se reconstruye el
    /// <see cref="SerializableObjectStates"/> vía <c>Set</c>.
    /// </para>
    /// </summary>
    [Serializable]
    public class DungeonSnapshot
    {
        public Vector2Int CurrentCell;
        public DoorDirection? LastEntryDirection;

        /// <summary>Tile de grilla del player dentro de <see cref="CurrentCell"/>.</summary>
        public GridCoord PlayerCoord;

        /// <summary>GUID preservado del player — se re-inyecta en resume para que
        /// combate/posición referencien la misma entidad (los GUID se regeneran por sesión).</summary>
        public string PlayerGuid;

        public List<RoomSnapshot> Rooms = new List<RoomSnapshot>();
    }

    /// <summary>Estado persistente de una sala, indexado por su celda de topología.</summary>
    [Serializable]
    public class RoomSnapshot
    {
        public Vector2Int Cell;
        public RoomState State;
        public bool Visited;

        /// <summary>
        /// Copia de <see cref="RoomInstance.ObjectStates"/> (enemigos/puertas/cofres…).
        /// <see cref="Dictionary{TKey,TValue}"/> para que Odin preserve el subtipo
        /// concreto de cada <see cref="RoomObjectState"/> sin migración.
        /// </summary>
        public Dictionary<string, RoomObjectState> ObjectStates = new Dictionary<string, RoomObjectState>();
    }
}
