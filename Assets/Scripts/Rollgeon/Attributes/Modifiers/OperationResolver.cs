using System;
using System.Collections.Generic;

namespace Rollgeon.Attributes.Modifiers
{
    /// <summary>
    /// Resolver estatico que mapea <c>(Type, ModifierOperation) -&gt; Func&lt;T, T, T&gt;</c>.
    /// Cachea los delegates por primera resolucion para evitar lookup-per-apply.
    /// Lanza <see cref="NotSupportedException"/> si la combinacion no aplica
    /// (ej: <see cref="ModifierOperation.And"/> sobre <c>int</c>).
    /// Especificado en TECHNICAL.md §3.3.
    /// </summary>
    public static class OperationResolver
    {
        private static readonly Dictionary<(Type, ModifierOperation), Delegate> Cache
            = new Dictionary<(Type, ModifierOperation), Delegate>();

        // --- int -----------------------------------------------------------
        private static readonly Dictionary<ModifierOperation, Func<int, int, int>> IntOps
            = new Dictionary<ModifierOperation, Func<int, int, int>>
            {
                { ModifierOperation.Add,      (a, b) => a + b },
                { ModifierOperation.Subtract, (a, b) => a - b },
                { ModifierOperation.Multiply, (a, b) => a * b },
                { ModifierOperation.Override, (a, b) => b },
                { ModifierOperation.Min,      (a, b) => a < b ? a : b },
                { ModifierOperation.Max,      (a, b) => a > b ? a : b },
                { ModifierOperation.Percent,  (a, b) => a + (a * b) },
                { ModifierOperation.Replace,  (a, b) => b },
            };

        // --- float ---------------------------------------------------------
        private static readonly Dictionary<ModifierOperation, Func<float, float, float>> FloatOps
            = new Dictionary<ModifierOperation, Func<float, float, float>>
            {
                { ModifierOperation.Add,      (a, b) => a + b },
                { ModifierOperation.Subtract, (a, b) => a - b },
                { ModifierOperation.Multiply, (a, b) => a * b },
                { ModifierOperation.Override, (a, b) => b },
                { ModifierOperation.Min,      (a, b) => a < b ? a : b },
                { ModifierOperation.Max,      (a, b) => a > b ? a : b },
                { ModifierOperation.Percent,  (a, b) => a + (a * b) },
                { ModifierOperation.Replace,  (a, b) => b },
            };

        // --- bool ----------------------------------------------------------
        private static readonly Dictionary<ModifierOperation, Func<bool, bool, bool>> BoolOps
            = new Dictionary<ModifierOperation, Func<bool, bool, bool>>
            {
                { ModifierOperation.Set,      (a, b) => b },
                { ModifierOperation.Override, (a, b) => b },
                { ModifierOperation.And,      (a, b) => a && b },
                { ModifierOperation.Or,       (a, b) => a || b },
                { ModifierOperation.Xor,      (a, b) => a ^ b },
                { ModifierOperation.Replace,  (a, b) => b },
            };

        /// <summary>
        /// Resuelve y cachea el delegate <c>Func&lt;T, T, T&gt;</c> correspondiente
        /// a la operacion. Lanza <see cref="NotSupportedException"/> si la
        /// combinacion tipo/op no existe.
        /// </summary>
        public static Func<T, T, T> Resolve<T>(ModifierOperation op)
        {
            var key = (typeof(T), op);
            if (Cache.TryGetValue(key, out var cached))
            {
                return (Func<T, T, T>)cached;
            }

            Func<T, T, T> resolved = BuildResolver<T>(op);
            Cache[key] = resolved;
            return resolved;
        }

        /// <summary>
        /// Vacia el cache. Uso reservado: teardown de tests que necesiten
        /// aislar estado (tests de diff generics no deberian reusar entries).
        /// </summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        private static Func<T, T, T> BuildResolver<T>(ModifierOperation op)
        {
            Type t = typeof(T);

            if (t == typeof(int) && IntOps.TryGetValue(op, out var intOp))
            {
                return (Func<T, T, T>)(Delegate)intOp;
            }

            if (t == typeof(float) && FloatOps.TryGetValue(op, out var floatOp))
            {
                return (Func<T, T, T>)(Delegate)floatOp;
            }

            if (t == typeof(bool) && BoolOps.TryGetValue(op, out var boolOp))
            {
                return (Func<T, T, T>)(Delegate)boolOp;
            }

            throw new NotSupportedException(
                $"[OperationResolver] Operation '{op}' is not supported for type '{t.Name}'. " +
                "Check ModifierOperation catalogue in TECHNICAL.md §3.3.");
        }
    }
}
