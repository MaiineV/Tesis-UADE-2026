using Rollgeon.Attributes;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Contrato comun parent de todos los datos de entidad (enemies, props, npcs).
    /// TECHNICAL.md §7.0. Minimal — identity + starting stats abstract builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hereda <see cref="SerializedScriptableObject"/></b> (Odin) para que subclases
    /// puedan agregar campos polimorficos (behaviors, AI trees, loot tables) sin
    /// re-trabajar la serializacion nativa.
    /// </para>
    /// <para>
    /// <b>Invariante.</b> <see cref="CreateRuntimeStats"/> duplica cada stat
    /// (no muta el SO origen) — el runtime trabaja sobre <c>ModifiableAttributes</c>
    /// fresh por entidad spawneada. Ver TECHNICAL.md §2.2.
    /// </para>
    /// </remarks>
    public abstract class BaseEntitySO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Delayed]
        [Tooltip("Id canonico de la entidad (ej. 'enemy.support.auditor').")]
        public string EntityId;

        [Delayed]
        [Tooltip("Nombre legible para UI.")]
        public string DisplayName;

        [TextArea]
        [Tooltip("Descripcion corta para tooltips / codex.")]
        public string Description;

        /// <summary>
        /// Construye el <see cref="ModifiableAttributes"/> inicial del runtime.
        /// Cada subtipo decide que stats incluir y con que valores base
        /// (TECHNICAL.md §7.0 / §2.2).
        /// </summary>
        public abstract ModifiableAttributes CreateRuntimeStats();
    }
}
