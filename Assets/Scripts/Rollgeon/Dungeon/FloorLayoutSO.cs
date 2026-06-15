using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    public enum RoomCountMode { Fixed, Random }

    [Serializable]
    public class RoomCountSpec
    {
        public RoomCountMode Mode = RoomCountMode.Fixed;

        [MinValue(0)] public int Fixed = 1;
        [MinValue(0)] public int Min = 0;
        [MinValue(0)] public int Max = 1;

        public int Resolve(System.Random rng)
        {
            if (Mode == RoomCountMode.Fixed) return Mathf.Max(0, Fixed);
            int lo = Mathf.Max(0, Min);
            int hi = Mathf.Max(lo, Max);
            return rng.Next(lo, hi + 1);
        }

        public string Describe()
        {
            return Mode == RoomCountMode.Fixed
                ? Fixed.ToString()
                : Min == Max ? Min.ToString() : $"{Min}..{Max}";
        }

        public bool IsZero()
        {
            return Mode == RoomCountMode.Fixed ? Fixed == 0 : (Min == 0 && Max == 0);
        }
    }

    [Serializable]
    public class RoomTypeSlot
    {
        public RoomType Type;
        public RoomCountSpec Count = new RoomCountSpec();
        public List<RoomSO> Pool = new List<RoomSO>();
    }

    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Floor Layout", fileName = "FloorLayout")]
    public class FloorLayoutSO : SerializedScriptableObject
    {
        [Title("Identity")]
        public string FloorId;
        public string DisplayName;

        [Title("Progression (#158)")]
        [InfoBox("Piso siguiente de la ruta principal. Null = piso terminal (al tomar la salida " +
                 "se gana la run → Victory).")]
        public FloorLayoutSO NextFloor;

        [Title("Room Slots")]
        [InfoBox("Use Tools ▸ Floor Editor for a richer authoring experience.")]
        public List<RoomTypeSlot> Slots = new List<RoomTypeSlot>();
    }
}
