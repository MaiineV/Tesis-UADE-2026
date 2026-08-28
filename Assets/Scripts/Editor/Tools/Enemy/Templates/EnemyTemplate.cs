using System;
using Rollgeon.Entities;

namespace Rollgeon.Editor.Tools.Enemy.Templates
{
    /// <summary>
    /// Punto de partida para "Nuevo enemigo desde arquetipo": ficha GDD + stats base + árbol que
    /// ya juega. <see cref="Apply"/> escribe sobre un <see cref="EnemyDataSO"/> recién creado; no
    /// toca prefab ni retrato (los elige el designer y el validador se los pide).
    /// </summary>
    public sealed class EnemyTemplate
    {
        public readonly string Id;
        public readonly string Name;
        public readonly EnemyArchetype Archetype;
        /// <summary>Qué hace el árbol y qué quedó como TBD del GDD. Va a las Notas de la ficha.</summary>
        public readonly string Description;
        public readonly Action<EnemyDataSO> Apply;

        public EnemyTemplate(string id, string name, EnemyArchetype archetype, string description, Action<EnemyDataSO> apply)
        {
            Id = id;
            Name = name;
            Archetype = archetype;
            Description = description;
            Apply = apply;
        }
    }
}
