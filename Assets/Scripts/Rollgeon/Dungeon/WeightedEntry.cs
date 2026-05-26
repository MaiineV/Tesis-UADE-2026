using System;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    [Serializable]
    public struct WeightedEntry<T>
    {
        public T Item;
        public float Weight;

        public WeightedEntry(T item, float weight)
        {
            Item = item;
            Weight = Mathf.Max(0f, weight);
        }
    }
}
