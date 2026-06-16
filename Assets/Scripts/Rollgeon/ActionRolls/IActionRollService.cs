using System;
using Rollgeon.Combos;
using Rollgeon.Dice;

namespace Rollgeon.ActionRolls
{
    /// <summary>
    /// Orquestra el flujo confirm → charge → roll → (maybe reroll) → resolve para
    /// las acciones que necesitan tirada (Forzar Puerta in-combat, Curarse). La UI
    /// manda eventos via los metodos <see cref="Confirm"/>/<see cref="Cancel"/>/
    /// <see cref="RequestReroll"/>/<see cref="DeclineReroll"/>; el cliente
    /// (ExplorationBehaviorService) arranca con <see cref="StartFlow"/> y recibe
    /// el resultado por callback.
    /// </summary>
    public interface IActionRollService
    {
        bool IsActive { get; }

        ActionRollPhase Phase { get; }

        ActionRollSpec CurrentSpec { get; }

        Guid CurrentPlayerGuid { get; }

        /// <summary>Ultima tirada (despues del reroll si lo hubo). Null hasta entrar a Rolling.</summary>
        int[] CurrentRoll { get; }

        /// <summary>1 = tirada inicial; 2+ = uno o más rerolls consumidos.</summary>
        int RollIndex { get; }

        /// <summary>
        /// <c>true</c> si la fase actual es <see cref="ActionRollPhase.AwaitingRerollDecision"/>
        /// y el jugador tiene energía suficiente para pagar el reroll. La UI usa esto
        /// para habilitar/deshabilitar el botón de Reroll.
        /// </summary>
        bool CanAffordReroll { get; }

        /// <summary>
        /// <c>true</c> si el user holdeó al menos un dado en la tirada actual. La UI usa
        /// esto para deshabilitar el botón "Aceptar" cuando no hay dados seleccionados
        /// — el spec exige que el user arme un combo del contrato antes de confirmar.
        /// </summary>
        bool CanConfirm { get; }

        /// <summary>Suma de CurrentRoll. 0 hasta tener una tirada.</summary>
        int CurrentSum { get; }

        /// <summary>
        /// Mascara de holds — true en posicion <c>i</c> si el user marco el dado i
        /// como "lo cuento para el combo / lo conservo en el reroll". Es el subset
        /// sobre el cual se detecta el combo + se calcula el effective total.
        /// Length == <see cref="CurrentRoll"/>.Length cuando hay tirada activa.
        /// </summary>
        System.Collections.Generic.IReadOnlyList<bool> CurrentHolds { get; }

        /// <summary>
        /// Setea los holds desde la UI. Recomputa el combo y el effective total
        /// considerando solo los dados con <c>holds[i] == true</c> (igual que el
        /// combate — el user elige que dados componen el combo).
        /// </summary>
        void SetHolds(System.Collections.Generic.IReadOnlyList<bool> holds);

        /// <summary>
        /// Total efectivo usado contra el threshold. Si la tirada matcheo combo,
        /// equivale al <c>BaseDamage</c> del combo; sino, equivale a
        /// <see cref="CurrentSum"/>. 0 hasta tener una tirada.
        /// </summary>
        int CurrentEffectiveTotal { get; }

        /// <summary>Combo de mayor prioridad detectado en la tirada actual, o null si ninguno.</summary>
        BaseComboSO CurrentCombo { get; }

        /// <summary>Notifica cada cambio de fase para que la UI actualice paneles.</summary>
        event Action<ActionRollPhase> OnPhaseChanged;

        /// <summary>
        /// Arranca el flujo. Si <c>spec.RequireConfirm</c>, entra en
        /// <see cref="ActionRollPhase.AwaitingConfirm"/> y espera a que la UI llame
        /// <see cref="Confirm"/> o <see cref="Cancel"/>. Si no requiere confirm,
        /// salta directo a <see cref="ActionRollPhase.Rolling"/>.
        /// </summary>
        void StartFlow(ActionRollSpec spec, Guid playerGuid, DiceBagSO bag,
            Action<ActionRollOutcome> onCompleted);

        // ---- UI-driven inputs ------------------------------------------------

        /// <summary>
        /// En <see cref="ActionRollPhase.AwaitingConfirm"/> aprueba el confirm dialog y
        /// arranca la tirada. En <see cref="ActionRollPhase.AwaitingRerollDecision"/>
        /// resuelve con la tirada actual (sin rerollear).
        /// </summary>
        void Confirm();

        void Cancel();

        /// <summary>Re-tira todos los dados (legacy, sin respetar holds).</summary>
        void RequestReroll();

        /// <summary>
        /// Re-tira solo los dados cuyo <paramref name="keep"/> es <c>false</c>; los
        /// <c>true</c> conservan su valor actual (igual que el reroll en combate).
        /// Cobra <c>RerollEnergyCost</c> por cada invocación; el jugador puede
        /// rerollear múltiples veces mientras tenga energía. Si <c>SpendEnergy</c>
        /// falla, el flow se resuelve con la tirada actual.
        /// </summary>
        void RequestReroll(System.Collections.Generic.IReadOnlyList<bool> keep);

        /// <summary>Equivalente a <see cref="Confirm"/> en AwaitingRerollDecision — resuelve sin rerollear.</summary>
        void DeclineReroll();
    }
}
