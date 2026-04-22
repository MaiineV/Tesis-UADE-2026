using System;
using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Nodo runtime del grafo de piso — una sala concreta instanciada (o por
    /// instanciar) con su posición world, conexiones a vecinas y estado
    /// persistente. TECHNICAL.md §13.6.
    /// <para>
    /// Vive en <c>DungeonManager._instances</c> (scope Run, in-memory).
    /// <see cref="ObjectStates"/> preserva HP de enemigos vivos + flags de
    /// puertas (forzada/unlocked) + cofres/shop items (stubs §13.6.1). El
    /// contenedor es <see cref="SerializableObjectStates"/> con
    /// <c>[SerializeReference]</c> — preparado para futuro SaveService a disco
    /// sin migración.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class RoomInstance
    {
        public Guid InstanceId;

        public RoomSO Template;

        /// <summary>Null hasta la primera entrada a la sala; destruido al salir.</summary>
        public GameObject SpawnedPrefab;

        public Vector3 WorldPosition;

        public Vector2Int GridCell;

        /// <summary>Vecina 4-adjacent por dirección. Solo existen entries para conexiones reales.</summary>
        [NonSerialized]
        public Dictionary<DoorDirection, Guid> Connections = new Dictionary<DoorDirection, Guid>();

        public RoomState State = RoomState.Uncleared;

        /// <summary>GUIDs de enemigos spawneados en la visita actual; vaciado en ExitCurrentRoom.</summary>
        [NonSerialized]
        public List<Guid> SpawnedEnemies = new List<Guid>();

        public SerializableObjectStates ObjectStates = new SerializableObjectStates();
    }
}