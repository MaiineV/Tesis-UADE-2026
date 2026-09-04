using System;
using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// API pública del Canal Dados — la consume la UI del altar (Phase 5/6),
    /// los tests, y eventualmente el damage pipeline cuando aterrice. La parte
    /// "interna" para triggers vive en <see cref="IDiceEnchantmentRuntime"/>
    /// (counters + auto-remove); la misma impl concreta provee las dos
    /// interfaces.
    /// </summary>
    public interface IDiceEnchantmentService
    {
        /// <summary>Estado runtime del bag. Null hasta que <c>OnRunStart</c> lo inicialice.</summary>
        RuntimeDiceBag Bag { get; }

        /// <summary><c>true</c> si <see cref="Bag"/> está populado y el service puede operar.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Computa el set de caras válidas del dado en <paramref name="bagIndex"/>
        /// tras aplicar los encantamientos activos. Inicializa con [1..MaxFace] y
        /// va componiendo los face filters por intersección.
        /// </summary>
        IReadOnlyCollection<int> ComputeAllowedFaces(int bagIndex);

        /// <summary>
        /// Verifica si un encantamiento podría sumarse al dado sin ejecutar
        /// el apply — compatibilidad de tipo + coherencia (las caras proyectadas
        /// componiendo TODOS los encantamientos actuales más el nuevo no pueden
        /// bajar de <c>MinFacesAfterApply</c>). Retorna
        /// <see cref="EnchantmentApplyResult.Ok"/> con el preview de caras
        /// resultantes, o <see cref="EnchantmentApplyResult.Fail"/> con la razón.
        /// </summary>
        EnchantmentApplyResult ValidateApply(int bagIndex, EnchantmentSO ench);

        /// <summary>
        /// Suma el encantamiento a la lista del dado (append — nunca reemplaza;
        /// GDD: stack ilimitado). Dispara hooks <c>OnEnchantmentApplied</c> y
        /// emite <c>EventName.OnEnchantmentApplied</c>. El índice asignado queda
        /// en <see cref="EnchantmentApplyResult.AppliedSlotIndex"/>.
        /// </summary>
        EnchantmentApplyResult Apply(int bagIndex, EnchantmentSO ench);

        /// <summary>
        /// Quita el encantamiento del slot dejando un tombstone (los índices de
        /// los demás no se mueven). Limpia counters asociados. Dispara
        /// <c>EventName.OnEnchantmentRemoved</c>. Idempotente — devuelve <c>false</c>
        /// si el slot ya estaba vacío.
        /// </summary>
        bool Remove(int bagIndex, int enchSlotIndex);

        /// <summary>
        /// Computa el scratch acumulado de los triggers <c>OnComboMatched</c>
        /// para un combo dado. La conexión con el damage pipeline (AttackResolver,
        /// pendiente) consume el scratch para sumar/multiplicar el daño final.
        /// </summary>
        EnchantmentScratch ResolveComboBonus(
            Guid sourceGuid,
            string comboId,
            IReadOnlyList<int> diceResult,
            int comboBaseDamage);

        /// <summary>
        /// Scratch resultante del último dispatch de <c>OnComboMatched</c>
        /// (poblado por el handler del TypedEvent). Permite que un consumer
        /// pasivo (UI de damage formula, debug overlay) lea sin disparar
        /// la lógica de nuevo. Null si nunca matcheó un combo en esta run.
        /// </summary>
        EnchantmentScratch LastComboScratch { get; }

        /// <summary>
        /// Hook para tests / Phase 5 — inicializa <see cref="Bag"/> a partir de
        /// un <c>DiceBagSO</c>. Normalmente lo invoca el service automáticamente
        /// en <c>OnRunStart</c> via <c>IPlayerService.DiceBag</c>.
        /// </summary>
        void InitializeFromBag(DiceBagSO bag);

        // ---- Dado de Movimiento (§6.6) — carril EnchantmentSlotRef.MovementDieSlot ----

        /// <summary>
        /// Cara máxima efectiva del dado de Movimiento: tipo base (clase / override) más las
        /// caras extra sumadas en la run. Sin bag ⇒ solo el tipo base.
        /// </summary>
        int MovementDieMaxFace { get; }

        /// <summary>
        /// Suma caras al dado de Movimiento (GDD Dice Builder: el dado no cambia de tipo, gana
        /// caras). Clamp a 0 hacia abajo. Devuelve el total de caras extra. Persiste con el bag.
        /// </summary>
        int AddMovementDieFaces(int delta);

        /// <summary>
        /// Caras válidas del dado de Movimiento: <c>1..MovementDieMaxFace</c> compuesto con los
        /// face filters de su carril. Vacío sin bag — el roller cae a su RNG plano.
        /// </summary>
        IReadOnlyCollection<int> ComputeMovementDieFaces();
    }
}
