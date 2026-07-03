using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Cheats
{
    /// <summary>
    /// Energía infinita: se suscribe a <c>OnEnergyChanged</c> y re-llena la energía del player al
    /// máximo cada vez que baja. Re-emite el evento (con guard de re-entrada) para que el HUD se
    /// actualice. No-op si no hay player.
    /// </summary>
    public sealed class InfiniteEnergyController : IDisposable
    {
        private readonly IDevConsoleContext _ctx;
        private readonly EventManager.EventReceiver _handler;
        private bool _pinning;

        public bool Enabled { get; private set; }

        public InfiniteEnergyController(IDevConsoleContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _handler = OnEnergyChanged;
        }

        public void Enable()
        {
            if (Enabled) return;
            Enabled = true;
            EventManager.Subscribe(EventName.OnEnergyChanged, _handler);
        }

        public void Disable()
        {
            if (!Enabled) return;
            Enabled = false;
            EventManager.UnSubscribe(EventName.OnEnergyChanged, _handler);
        }

        public bool Toggle()
        {
            if (Enabled) Disable(); else Enable();
            return Enabled;
        }

        private void OnEnergyChanged(params object[] args)
        {
            if (!Enabled || _pinning) return;
            if (args == null || args.Length < 3) return;
            if (!(args[0] is Guid id) || id != _ctx.PlayerGuid) return;
            if (!(args[1] is int current) || !(args[2] is int max)) return;
            if (current >= max) return;
            if (!_ctx.TryResolve<AttributesManager>(out var am) || am == null) return;

            _pinning = true;
            am.SetAttributeValue<Energy, int>(id, max);
            EventManager.Trigger(EventName.OnEnergyChanged, id, max, max);
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, id, max, max);
            _pinning = false;
        }

        public void Dispose() => Disable();
    }
}
