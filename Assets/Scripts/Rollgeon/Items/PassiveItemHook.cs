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

    /// <summary>
    /// Cual de los Guid del evento tiene que ser el jugador para que el hook dispare.
    /// APPEND-ONLY: se serializa el int del enum.
    /// </summary>
    /// <remarks>
    /// Varios eventos del bus llevan dos entidades — <c>OnDamageIncoming</c> es
    /// <c>[sourceGuid, targetGuid, damage]</c>. El hook siempre comparaba contra
    /// <c>args[0]</c>, asi que un item colgado de <c>OnDamageIncoming</c> disparaba cuando el
    /// jugador <b>pegaba</b>, no cuando le pegaban: lo contrario de lo que dice el nombre, y en
    /// silencio. Con <see cref="Source"/> como default, todo asset ya autorado conserva
    /// exactamente ese comportamiento.
    /// </remarks>
    public enum PassiveHookSubject
    {
        /// <summary>El jugador es <c>args[0]</c> — quien origina el evento. Default.</summary>
        Source = 0,

        /// <summary>El jugador es <c>args[1]</c> — quien lo recibe. Habilita "cuando te pegan".</summary>
        Target = 1,

        /// <summary>
        /// Sin filtro de entidad: el hook dispara siempre que el evento suene. Para eventos
        /// cuyo <c>args[0]</c> NO es el jugador (ej. <c>OnCombatStart</c> lleva el roomId) —
        /// con Source/Target esos hooks no disparaban nunca.
        /// </summary>
        None = 2,
    }

    [Serializable, HideReferenceObjectPicker]
    public class PassiveItemHook
    {
        [Tooltip("EventBus = evento legacy del EventManager. ComboPlayed = combo jugado (pre-daño) con filtro por combo.")]
        [OdinSerialize]
        public PassiveHookKind Kind = PassiveHookKind.EventBus;

        [ShowIf(nameof(Kind), PassiveHookKind.EventBus)]
        [InfoBox("Evento del bus que dispara el efecto. La lista curada y verificada vive en " +
                 "ItemTriggerCatalog.All (Editor > Tools > Item) — usar esos. OnComboCrossed " +
                 "dispara con Guid.Empty: nunca matchea un hook filtrado por jugador.")]
        [InfoBox("El hook filtra por args[Subject] == Guid del jugador (convención §18). Un evento " +
                 "que NO lleva un Guid en esa posición dispara siempre — no hay a quién comparar. " +
                 "Subject=None desactiva el filtro a propósito.",
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

        [ShowIf(nameof(Kind), PassiveHookKind.EventBus)]
        [InfoBox("Quien tiene que ser el jugador en el evento. Source = el que lo origina " +
                 "(args[0]); Target = el que lo recibe (args[1]). Solo cambia algo en eventos " +
                 "con dos entidades: OnDamageIncoming/OnDamageOutgoing son [source, target, dmg], " +
                 "asi que Target es 'cuando te pegan' y Source es 'cuando pegas'. None = sin " +
                 "filtro, para eventos cuyo args[0] no es el jugador (ej. OnCombatStart).")]
        [OdinSerialize]
        public PassiveHookSubject Subject = PassiveHookSubject.Source;

        [OdinSerialize]
        public EffectData Effect = new();

        [InfoBox("Modificadores que se aplican mientras el item este en el inventario. " +
                 "Se remueven automaticamente si el item se pierde.")]
        [OdinSerialize]
        public List<PersistentModifierDef> PersistentModifiers = new();
    }
}
