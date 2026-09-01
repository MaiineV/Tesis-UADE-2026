using System;
using System.Collections.Generic;
using Rollgeon.Combos;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Heroes
{
    /// <summary>
    /// Tabla de la Habilidad de Clase "Empuje" (GDD Combat System § "Habilidad de Clase"):
    /// cuántas casillas empuja cada combo del Contrato de Generala, y el daño de choque.
    /// No escala con daño ni con ATQ — es data pura de balance, por clase.
    /// </summary>
    /// <remarks>
    /// Un combo sin entrada (o sin combo) empuja 0 ⇒ la tirada se consume sin efecto (spec:
    /// "sin fallback de ningún tipo"). <c>Fuerza Bruta</c> queda deliberadamente afuera.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Heroes/Class Skill Push Table", fileName = "ClassSkillPushTable")]
    public class ClassSkillPushTableSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [ValueDropdown("@Rollgeon.Combos.BaseComboSO.GetKnownComboIds()", AppendNextDrawer = true)]
            public string ComboId;

            [Range(0, 10)]
            public int Tiles;

            public Entry(string comboId, int tiles)
            {
                ComboId = comboId;
                Tiles = tiles;
            }
        }

        /// <summary>Valores del GDD (2026-08). Par/Trío/Generala confirmados; el resto provisional.</summary>
        public static readonly IReadOnlyList<Entry> Spec = new[]
        {
            new Entry(Combos.ComboId.Par, 1),
            new Entry(Combos.ComboId.DoublePair, 1),
            new Entry(Combos.ComboId.HigherNumber, 2),   // "Suma 4" en el GDD
            new Entry(Combos.ComboId.Triple, 2),
            new Entry(Combos.ComboId.FullHouse, 3),
            new Entry(Combos.ComboId.Straight, 3),
            new Entry(Combos.ComboId.Poker, 4),
            new Entry(Combos.ComboId.Generala, 5),
        };

        public const int DefaultCollisionDamage = 10;

        [Title("Distancia por combo")]
        [ListDrawerSettings(ShowFoldout = false)]
        public List<Entry> Entries = new List<Entry>();

        [Title("Choque")]
        [MinValue(0)]
        [Tooltip("Daño al empujado al chocar contra un obstáculo rompible u otro enemigo (y a ese " +
                 "enemigo). Contra pared no hay daño: el empujado pierde 1 turno.")]
        public int CollisionDamage = DefaultCollisionDamage;

        /// <summary>Casillas para <paramref name="comboId"/>; 0 si no hay entrada o el id es vacío.</summary>
        public int GetTiles(string comboId)
        {
            if (string.IsNullOrEmpty(comboId) || Entries == null) return 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].ComboId == comboId) return Math.Max(0, Entries[i].Tiles);
            }
            return 0;
        }

        [Button("Reset to Spec")]
        public void ResetToSpec()
        {
            Entries = new List<Entry>(Spec);
            CollisionDamage = DefaultCollisionDamage;
        }

        /// <summary>Instancia en memoria con los valores del GDD (tests, installer).</summary>
        public static ClassSkillPushTableSO CreateDefault()
        {
            var so = CreateInstance<ClassSkillPushTableSO>();
            so.ResetToSpec();
            return so;
        }
    }
}
