using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.PreConditions;

namespace Rollgeon.Entities
{
    public class Entity : IDisposable
    {
        public Guid InstanceId;

        public Guid Guid
        {
            get => InstanceId;
            set => InstanceId = value;
        }

        public ClassPassiveSO Passive { get; private set; }

        private readonly List<(EventName evt, EventManager.EventReceiver handler)>
            _passiveHandlers = new List<(EventName, EventManager.EventReceiver)>();

        public void BindPassive(ClassPassiveSO passive)
        {
            if (passive == null) return;

            if (Passive != null)
                UnbindPassive();

            Passive = passive;

            foreach (var hook in passive.Hooks)
            {
                if (hook?.Effect == null) continue;

                var capturedHook = hook;
                EventManager.EventReceiver handler = args =>
                {
                    if (args == null || args.Length == 0) return;
                    if (!(args[0] is Guid ownerId) || ownerId != InstanceId) return;

                    var ctx = new EffectContext
                    {
                        SourceGuid = InstanceId,
                        TargetGuid = InstanceId,
                        SourceEntity = this,
                        TriggeringEntity = this,
                        lastResult = true
                    };

                    var preCtx = new PreConditionContext
                    {
                        OwnerGuid = InstanceId,
                        Entity = this
                    };

                    capturedHook.Effect.TryExecute(ctx, preCtx);
                };

                EventManager.Subscribe(hook.TriggerEvent, handler);
                _passiveHandlers.Add((hook.TriggerEvent, handler));
            }
        }

        public void UnbindPassive()
        {
            foreach (var (evt, handler) in _passiveHandlers)
                EventManager.UnSubscribe(evt, handler);

            _passiveHandlers.Clear();
            Passive = null;
        }

        public void Dispose() => UnbindPassive();
    }
}
