using System;
using System.Collections.Generic;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Resultado tipado de <c>IDiceEnchantmentService.ValidateApply</c> /
    /// <c>Apply</c>. <see cref="Success"/> indica si la operación procede;
    /// <see cref="ProjectedFaces"/> da el set de caras que quedarían si el apply
    /// se ejecutara — la UI lo consume para mostrar preview al jugador.
    /// </summary>
    public readonly struct EnchantmentApplyResult
    {
        /// <summary><c>true</c> si la operación es válida.</summary>
        public bool Success { get; }

        /// <summary>Razón del fallo, legible para UI / logs. Null si <see cref="Success"/>.</summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Set de caras del dado proyectado tras aplicar (o no aplicar) el
        /// encantamiento. Útil para que la UI muestre "tras este encantamiento
        /// el dado solo puede sacar 2, 4, 6, 8, 10".
        /// </summary>
        public IReadOnlyCollection<int> ProjectedFaces { get; }

        private EnchantmentApplyResult(bool success, string error, IReadOnlyCollection<int> projectedFaces)
        {
            Success = success;
            ErrorMessage = error;
            ProjectedFaces = projectedFaces ?? Array.Empty<int>();
        }

        public static EnchantmentApplyResult Ok(IReadOnlyCollection<int> projectedFaces)
            => new EnchantmentApplyResult(true, null, projectedFaces);

        public static EnchantmentApplyResult Fail(string error, IReadOnlyCollection<int> projectedFaces = null)
            => new EnchantmentApplyResult(false, error, projectedFaces);
    }
}
