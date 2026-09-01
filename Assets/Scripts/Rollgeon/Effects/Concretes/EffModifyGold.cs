using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>Operación de oro de <see cref="EffModifyGold"/>.</summary>
    public enum GoldOperation
    {
        /// <summary>Suma oro al balance.</summary>
        Add,

        /// <summary>Descuenta oro, all-or-nothing. Si no alcanza puede cortar el grupo.</summary>
        Spend,

        /// <summary>Setea el balance a un valor absoluto (piso 0).</summary>
        Set,
    }

    /// <summary>
    /// Modifica el oro del jugador vía <c>IEconomyService</c>, aplicado inmediato (no
    /// difiere al scratch). Con <see cref="GoldOperation.Spend"/> y
    /// <see cref="FailChainIfInsufficient"/>, el gasto insuficiente corta el grupo —
    /// permite componer "paga X y ganá Y" con atomicidad por cortocircuito.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffModifyGold : BaseEffect,
        IUsesValue, ICanBeConstantValue, ICanBeGenericValue
    {
        [Title("Gold")]
        public GoldOperation Operation = GoldOperation.Add;

        [Tooltip("Cantidad de oro. Admite readers (constante, contador de combo, cara del dado…).")]
        [OdinSerialize, SerializeReference]
        private EffectIntReader _amount = new ReadConstantInt();

        [ShowIf(nameof(Operation), GoldOperation.Spend)]
        [Tooltip("Si el oro no alcanza: true corta el grupo (los efectos siguientes no corren), " +
                 "false deja pasar sin gastar.")]
        [SerializeField]
        private bool _failChainIfInsufficient = true;

        protected override bool ShowSelection => false;

        public EffectIntReader Amount
        {
            get => _amount;
            set => _amount = value;
        }

        public bool FailChainIfInsufficient
        {
            get => _failChainIfInsufficient;
            set => _failChainIfInsufficient = value;
        }

        public override string GetEffectName() => "Modify Gold";

        public override bool ApplyEffect(EffectContext context)
        {
            int amount = _amount?.Read(context) ?? 0;

            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
            {
                // Sin economía, un Spend no puede afirmarse pagado — misma semántica que
                // el gasto insuficiente. Add/Set se descartan sin vetar (paridad con el
                // applier legacy, que salteaba silencioso).
                return Operation != GoldOperation.Spend || !_failChainIfInsufficient;
            }

            switch (Operation)
            {
                case GoldOperation.Add:
                    if (amount > 0)
                    {
                        economy.Add(amount);
                        // Floating "+XG" sobre quien lo gana (patrón EnemyGoldDropService).
                        // Antes este effect no emitía NINGÚN feedback: Tesoro de la Fortuna
                        // cobraba al fin de turno y el jugador no veía cuánto ni por qué —
                        // clave porque el HUD ya muestra el pool POST-grant cuando el item
                        // cobró sobre el leftover PRE-grant.
                        Guid floatTarget = context?.TargetGuid is { } t && t != Guid.Empty
                            ? t
                            : context?.SourceGuid ?? Guid.Empty;
                        if (floatTarget != Guid.Empty)
                            EventManager.Trigger(
                                EventName.OnFloatingNumberRequested,
                                floatTarget,
                                Rollgeon.UI.HUD.FloatingNumberType.Gold,
                                (float)amount,
                                Vector3.zero);
                    }
                    return true;

                case GoldOperation.Spend:
                    if (amount <= 0) return true;
                    if (economy.Spend(amount)) return true;
                    return !_failChainIfInsufficient;

                case GoldOperation.Set:
                    economy.ResetTo(Mathf.Max(0, amount));
                    return true;

                default:
                    return true;
            }
        }
    }
}
