using System.Collections.Generic;

namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — reemplazado por Foundation de Behaviors downstream (TECHNICAL.md §7.2 / §9.3).
    /// Expone solamente la cara que los effects de esta foundation consumen:
    /// el API de <c>StoredValues</c> (§9.3) y acceso al trigger context que se pasó al
    /// <c>Execute</c>. El lifecycle completo (finally-clear post resolve, clear defensivo
    /// al fin del turno, deep-clone al spawn) se implementa en la foundation real.
    /// </summary>
    public abstract class BaseBehavior
    {
        private readonly Dictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>> _storedValues
            = new Dictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>>();

        /// <summary>Append semántico — cada call agrega un valor a la lista bajo la key.</summary>
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
        /// Lectura tipada. Devuelve <c>false</c> si no hay valores para la key o si ninguno
        /// es del subtipo <typeparamref name="T"/>. Los valores devueltos se filtran por cast.
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
