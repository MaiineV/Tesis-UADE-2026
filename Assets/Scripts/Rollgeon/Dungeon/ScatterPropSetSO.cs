using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Lista ponderada de props decorativos (fichas, botellas, cartas, huesos...) que
    /// <see cref="Components.RoomPropScatter"/> desparrama por el piso de una sala. Puramente
    /// visual — a diferencia de <c>RoomObjectDefinitionSO</c> (mobiliario de combate con HP y
    /// cola de turnos), acá no hay nada de eso: es un dato-bolsa igual de liviano que ese SO,
    /// pero sin ningún campo de combate.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Scatter Prop Set", fileName = "ScatterPropSet")]
    public sealed class ScatterPropSetSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public GameObject Prefab;

            [Tooltip("Peso relativo del sorteo — no una probabilidad absoluta.")]
            [Min(0.01f)]
            public float Weight = 1f;

            [Tooltip("Escala uniforme random dentro de este rango, por instancia.")]
            public Vector2 UniformScaleRange = new Vector2(0.9f, 1.1f);

            [Tooltip("Habilita que RoomPropScatter, con la chance de TipOverChance, lo apoye de " +
                     "costado en vez de parado — solo para props que tienen sentido tumbados " +
                     "(botella, vaso, cráneo). Objetos que no se sostienen de costado (fichas, " +
                     "cartas, huesos sueltos) dejar en false.")]
            public bool CanTipOver;
        }

        public List<Entry> Entries = new List<Entry>();

        /// <summary>Suma de <see cref="Entry.Weight"/> de todas las entries con Prefab seteado.</summary>
        private float TotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e?.Prefab == null) continue;
                total += Mathf.Max(0f, e.Weight);
            }
            return total;
        }

        /// <summary>
        /// Sortea una entry ponderada por <see cref="Entry.Weight"/>. Null si no hay ninguna
        /// entry con <see cref="Entry.Prefab"/> seteado.
        /// </summary>
        public Entry PickWeighted(System.Random rng)
        {
            if (rng == null || Entries == null || Entries.Count == 0) return null;

            float total = TotalWeight();
            if (total <= 0f) return null;

            double roll = rng.NextDouble() * total;
            double accum = 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e?.Prefab == null) continue;
                accum += Mathf.Max(0f, e.Weight);
                if (roll <= accum) return e;
            }

            // Redondeo de floats: si el roll cayó justo en el borde superior, devolver la
            // última entry válida en vez de null.
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i]?.Prefab != null) return Entries[i];
            }
            return null;
        }
    }
}
