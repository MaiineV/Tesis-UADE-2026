using System;
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
        }

        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnTurnStarted, OnTurnStarted);
        }

        private void OnTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid entityGuid))
                return;

            var shieldAttr = _attributes.GetAttribute<Shield>(entityGuid);
            if (shieldAttr == null || shieldAttr.Value <= 0) return;

            _attributes.SetAttributeValue<Shield, int>(entityGuid, 0);
            EventManager.Trigger(EventName.OnShieldChanged, entityGuid, 0);
        }
    }
}
