using System;
using System.Collections.Generic;
using Rollgeon.Effects.Stubs;
using Sirenix.OdinInspector;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Clase base "real" de todos los behaviors. TECHNICAL.md §7.2.
    /// Unifica el contrato: <c>Trigger</c> + filtro de fase + <c>Effects</c> + <c>Execute</c>
    /// + StoredValues API (§9.3). El stub <c>Rollgeon.Effects.Stubs.BaseBehavior</c> de
    /// Foundation#0004 ahora hereda de este tipo (adapter) para no romper EffHeal/EffDamage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>StoredValues.</b> El API <see cref="SetBehaviorValue"/> /
    /// <see cref="TryGetBehaviorValues{T}"/> / <see cref="ClearBehaviorValues"/> vive aca
    /// y lo heredan tanto los behaviors reales como el stub adapter. TECHNICAL.md §9.3.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> <c>ClearBehaviorValues</c> lo llama el dispatcher del turn manager
    /// en un <c>finally</c> post-resolve; el behavior no se auto-limpia. Downstream
    /// (T100d+) agrega lifecycle completo.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public abstract class BaseBehavior
    {
        /// <summary>
        /// Trigger que dispara la ejecucion de este behavior. TECHNICAL.md §7.2.
        /// </summary>
        [Title("Trigger")]
        [Tooltip("Evento que dispara la ejecucion de este behavior.")]
        public BehaviorTrigger Trigger = BehaviorTrigger.OnTurnStart;

        /// <summary>
        /// Fases del juego en las que este behavior esta habilitado. Si la fase actual
        /// no esta en la mascara, <see cref="Execute"/> no se invoca. TECHNICAL.md §7.2.
        /// </summary>
        [Tooltip("Fases del juego en las que este behavior puede ejecutar (AND con Trigger).")]
        public GamePhaseMask AllowedPhases = GamePhaseMask.All;

        private readonly Dictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> _storedValues
            = new Dictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>>();

        /// <summary>Nombre legible del behavior para UI / debug.</summary>
        public virtual string BehaviorName => GetType().Name;

        /// <summary>
        /// Soft-check antes del <see cref="Execute"/>. Default <c>true</c>. Los behaviors
        /// concretos pueden override para filtrar (ej. "solo si hay targets validos").
        /// </summary>
        public virtual bool CanExecute(BehaviorContext ctx) => true;

        /// <summary>
        /// Punto de extension principal: aplica el efecto del behavior. Llamado por el
        /// dispatcher (downstream) con un <paramref name="ctx"/> que incluye al owner
        /// y, si aplica, al entity que disparo el trigger.
        /// </summary>
        public abstract void Execute(BehaviorContext ctx);

        // --- StoredValues API (§9.3) --------------------------------------

        /// <summary>Append semantico — cada call agrega un valor a la lista bajo la key.</summary>
        public void SetBehaviorValue(BehaviorValueKey key, BaseBehaviorStoredValue value)
        {
            if (!_storedValues.TryGetValue(key, out var list))
            {
                list = new List<BaseBehaviorStoredValue>();
                _storedValues[key] = list;
            }
            list.Add(value);
        }

        /// <summary>
        /// Lectura tipada. Devuelve <c>false</c> si no hay valores para la key o si
        /// ninguno es del subtipo <typeparamref name="T"/>.
        /// </summary>
        public bool TryGetBehaviorValues<T>(BehaviorValueKey key, out List<T> values)
            where T : BaseBehaviorStoredValue
        {
            values = null;
            if (!_storedValues.TryGetValue(key, out var list)) return false;

            values = new List<T>(list.Count);
            foreach (var raw in list)
            {
                if (raw is T typed) values.Add(typed);
            }
            return values.Count > 0;
        }

        /// <summary>Limpia todos los valores. Idempotente. Llamado por el <c>finally</c> post resolve.</summary>
        public void ClearBehaviorValues()
        {
            _storedValues.Clear();
        }
    }
}
