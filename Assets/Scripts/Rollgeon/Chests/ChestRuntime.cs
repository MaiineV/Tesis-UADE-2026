using System;
using Rollgeon.Grid;
using Rollgeon.Items;

namespace Rollgeon.Chests
{
    /// <summary>Fase del cofre activo. Terminales: todo salvo <see cref="Idle"/>.</summary>
    public enum ChestPhase
    {
        Idle,
        Opened,
        Broken,
        MimicActive,
        Expired
    }

    /// <summary>Estado runtime del cofre activo de la sala (no persistente).</summary>
    public sealed class ChestRuntime
    {
        public Guid Guid;
        public Guid RoomInstanceId;
        public ItemRarity Tier;
        public bool IsMimic;
        public GridCoord Coord;
        public ChestPhase Phase = ChestPhase.Idle;

        /// <summary>Guid del enemigo Melee spawneado al activarse el Mimic.</summary>
        public Guid MimicEnemyGuid;
    }
}
