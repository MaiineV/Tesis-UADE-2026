using System.Reflection;
using Rollgeon.Combos;
using UnityEngine;

namespace Rollgeon.Combos.Tests
{
    /// <summary>
    /// Helpers para configurar <see cref="BaseComboSO"/> subclasses desde tests sin exponer
    /// setters publicos. Setea los campos protegidos via reflection (privados al tipo, pero
    /// la visibilidad la da <see cref="BindingFlags.Instance"/>).
    /// </summary>
    public static class ComboTestUtils
    {
        /// <summary>
        /// Crea la instancia del combo concreto <typeparamref name="T"/> con <c>_comboId</c>,
        /// <c>_baseDamage</c> y <c>_priority</c> seteados por reflection. Usa
        /// <see cref="ScriptableObject.CreateInstance{T}"/>. Desde Fix#0047 parte 2 la
        /// prioridad es un campo propio: por default se setea igual a
        /// <paramref name="baseDamage"/> (el acople viejo, que los tests de MatchBest asumen);
        /// pasar <paramref name="priority"/> para desacoplarlos explícitamente.
        /// </summary>
        public static T CreateCombo<T>(string comboId, int baseDamage, int? priority = null)
            where T : BaseComboSO
        {
            var instance = ScriptableObject.CreateInstance<T>();
            SetField(instance, "_comboId", comboId);
            SetField(instance, "_baseDamage", baseDamage);
            SetField(instance, "_priority", priority ?? baseDamage);
            return instance;
        }

        /// <summary>Setea un campo protected/private por nombre via reflection.</summary>
        public static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            if (field == null)
            {
                throw new System.InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}");
            }
            field.SetValue(target, value);
        }
    }
}
