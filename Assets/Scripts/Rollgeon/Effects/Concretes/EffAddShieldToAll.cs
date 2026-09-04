using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Banda impar de Coin Shield (Feature#0084): otorga escudo al jugador y/o a todos los
    /// enemigos vivos de la sala. El monto se computa UNA sola vez (con el escudo del
    /// jugador al momento de activar, antes de sumarle nada) y se aplica a cada target con
    /// el mismo patrón de escritura que <see cref="EffAddShield"/> (sin cap: no existe).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffAddShieldToAll : BaseEffect
    {
        [Title("Add Shield To All")]
        [OdinSerialize, SerializeReference]
        [Tooltip("Reader que resuelve el monto de escudo. Se evalúa UNA sola vez para todos los targets.")]
        private EffectIntReader _amount;

        public bool IncludeOwner = true;
        public bool IncludeEnemies = true;

        /// <summary>Setter de autoría (editor tools/tests): asigna el reader polimórfico del monto.</summary>
        public void EditorSetAmount(EffectIntReader reader) => _amount = reader;

        public override string GetEffectName() => "Add Shield To All";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            if (_amount == null)
            {
                Debug.LogWarning("[EffAddShieldToAll] Sin reader de monto configurado — no-op.");
                return true;
            }

            int amount = _amount.Read(context);
            if (amount <= 0) return true;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning("[EffAddShieldToAll] AttributesManager no registrado — no-op.");
                return true;
            }

            var targets = new List<Guid>();
            if (IncludeOwner && context.SourceGuid != Guid.Empty) targets.Add(context.SourceGuid);
            if (IncludeEnemies) targets.AddRange(CombatantQuery.LiveEnemiesOf(context.SourceGuid));

            foreach (var target in targets)
            {
                var shieldAttr = attrs.GetAttribute<Shield>(target);
                if (shieldAttr == null)
                {
                    Debug.Log($"[EffAddShieldToAll] {target} sin atributo Shield — skip.");
                    continue;
                }

                int newShield = shieldAttr.Value + amount;
                attrs.SetAttributeValue<Shield, int>(target, newShield);
                EventManager.Trigger(EventName.OnShieldChanged, target, newShield);
                EventManager.Trigger(EventName.OnFloatingNumberRequested, target,
                    Rollgeon.UI.HUD.FloatingNumberType.Shield, (float)amount, Vector3.zero);
            }

            return true;
        }
    }
}
