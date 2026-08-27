using System;
using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Etiquetas en español de los enums de la ficha, leídas del mismo <see cref="InspectorNameAttribute"/>
    /// que dibuja Odin: un único vocabulario para el inspector, la lista y los filtros.
    /// </summary>
    public static class EnemyEditorVocab
    {
        static readonly Dictionary<Type, Dictionary<string, string>> _cache = new Dictionary<Type, Dictionary<string, string>>();

        public static string LabelOf<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            var table = TableFor(typeof(TEnum));
            string name = value.ToString();
            return table.TryGetValue(name, out var label) ? label : name;
        }

        /// <summary>Etiquetas en el orden de declaración del enum.</summary>
        public static string[] LabelsOf<TEnum>() where TEnum : struct, Enum
        {
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            var labels = new string[values.Length];
            for (int i = 0; i < values.Length; i++) labels[i] = LabelOf(values[i]);
            return labels;
        }

        /// <summary>Chip corto para la lista: M / R / S; vacío si no está definido.</summary>
        public static string Chip(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Melee:   return "M";
                case EnemyArchetype.Ranged:  return "R";
                case EnemyArchetype.Support: return "S";
                default:                     return string.Empty;
            }
        }

        static Dictionary<string, string> TableFor(Type enumType)
        {
            if (_cache.TryGetValue(enumType, out var table)) return table;
            table = new Dictionary<string, string>();
            foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = field.GetCustomAttribute<InspectorNameAttribute>();
                table[field.Name] = attr != null ? attr.displayName : field.Name;
            }
            _cache[enumType] = table;
            return table;
        }
    }
}
