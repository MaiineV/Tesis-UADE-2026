using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Combat
{
    public sealed class ShieldResetHandler : IDisposable
    {
        private readonly AttributesManager _attributes;

        public ShieldResetHandler(AttributesManager attributes)
        {
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            EventManager.Subscribe(EventName.OnTurnStarted, OnTurnStarted);
            // BUG-062 (hardening): el reset por turno es la vía normal — un escudo
            // legítimamente persiste DURANTE el combate entre golpes, hasta el próximo
            // turno de su dueño. Este segundo reset es solo la red de seguridad AL
            // CERRAR el combate: si por lo que sea un escudo quedó residual (turno que
            // nunca llegó a empezar de nuevo, combate cortado por Aborted/Defeat/Victory
            // a mitad de ronda), no debe sobrevivir a la próxima pelea como un buff
            // permanente no autorado.
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEnd);
        }

        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnTurnStarted, OnTurnStarted);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEnd);
        }

        private void OnTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid entityGuid))
                return;

            ResetShield(entityGuid);
        }

        private void OnCombatEnd(params object[] args)
        {
            // Sin un entityGuid puntual en el payload (RoomInstanceId + Outcome), barremos
            // todas las entidades registradas — resetear un Shield ya en 0 es un no-op
            // barato (ver guard en ResetShield), así que iterar todo el registry no tiene
            // costo distinguible en la escala de combatientes de una sala.
            var snapshot = new List<Guid>();
            foreach (var kv in _attributes.EnumerateEntries())
            {
                snapshot.Add(kv.Key);
            }
            foreach (var entityGuid in snapshot)
            {
                ResetShield(entityGuid);
            }
        }

        private void ResetShield(Guid entityGuid)
        {
            var shieldAttr = _attributes.GetAttribute<Shield>(entityGuid);
            if (shieldAttr == null || shieldAttr.Value <= 0) return;

            _attributes.SetAttributeValue<Shield, int>(entityGuid, 0);
            EventManager.Trigger(EventName.OnShieldChanged, entityGuid, 0);
        }
    }
}
