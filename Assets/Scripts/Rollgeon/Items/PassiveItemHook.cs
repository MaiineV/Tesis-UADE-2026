using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>Qué canal dispara el hook. APPEND-ONLY: se serializa el int del enum.</summary>
    public enum PassiveHookKind
    {
        /// <summary>Evento legacy del bus (<see cref="EventName"/>). Default — preserva assets existentes.</summary>
        EventBus = 0,

        /// <summary>
        /// Combo jugado (acción confirmada, pre-daño) vía <c>TypedEvent&lt;ComboPlayedPayload&gt;</c>,
        /// con filtro por combo. El efecto corre dentro de la ventana: un <c>EffAddComboBonus</c>
        /// acá suma al daño del golpe en curso.
        /// </summary>
        ComboPlayed = 1,
    }

    [Serializable, HideReferenceObjectPicker]
    public class PassiveItemHook
    {
        [Tooltip("EventBus = evento legacy del EventManager. ComboPlayed = combo jugado (pre-daño) con filtro por combo.")]
        [OdinSerialize]
        public PassiveHookKind Kind = PassiveHookKind.EventBus;

        [ShowIf(nameof(Kind), PassiveHookKind.EventBus)]
        [InfoBox("Evento del bus que dispara el efecto. Usables: OnTurnStarted, OnTurnFinished, " +
                 "OnRollStarted, OnDiceRolled, OnRollResolved, OnDamageIncoming, OnDamageOutgoing, " +
                 "OnComboCrossed, OnWeaknessHit, OnPlayerHealthChanged.")]
        [InfoBox("El hook filtra por args[0] == Guid del jugador (convención §18). Un evento que NO " +
                 "arranca con un Guid dispara siempre — no hay a quién comparar.",
                 InfoMessageType.Warning)]
        public EventName TriggerEvent;

        [ShowIf(nameof(Kind), PassiveHookKind.ComboPlayed)]
        [InfoBox("Qué combos escucha este hook. AnyCombo = cualquier combo jugado; " +
                 "ComboIds = solo los ids listados.")]
        [OdinSerialize]
        public ComboFilter ComboFilter = new ComboFilter();

        [ShowIf(nameof(Kind), PassiveHookKind.ComboPlayed)]
        [InfoBox("Restringe el hook a un ActionKind específico (ej. Attack para un bono de " +
                 "daño que no debe leakear a Heal/Movement — comparten el mismo play scratch, " +
                 "BUG-080). Unknown = sin restricción, dispara para cualquier acción con combo " +
                 "jugado (comportamiento previo, default).")]
        [OdinSerialize]
        public RollActionKind ActionKindFilter = RollActionKind.Unknown;

        [OdinSerialize]
        public EffectData Effect = new();

        [InfoBox("Modificadores que se aplican mientras el item este en el inventario. " +
                 "Se remueven automaticamente si el item se pierde.")]
        [OdinSerialize]
        public List<PersistentModifierDef> PersistentModifiers = new();
    }
}
