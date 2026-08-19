using System;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Cheats
{
    /// <summary>
    /// Rolls infinitos: se suscribe a <c>OnPlayerRollsChanged</c> y re-llena el pool
    /// del player al máximo cada vez que baja. El <c>RestoreCurrent</c> del service
    /// re-emite el evento (guard de re-entrada acá) para que el HUD se actualice.
    /// No-op si no hay player o el service no está.
    /// </summary>
    public sealed class InfiniteRollsController : IDisposable
    {
        private readonly IDevConsoleContext _ctx;
        private readonly EventManager.EventReceiver _handler;
        private bool _pinning;

        public bool Enabled { get; private set; }

        public InfiniteRollsController(IDevConsoleContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _handler = OnRollsChanged;
        }

        public void Enable()
        {
            if (Enabled) return;
            Enabled = true;
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, _handler);
        }

        public void Disable()
        {
            if (!Enabled) return;
            Enabled = false;
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, _handler);
        }

        public bool Toggle()
        {
            if (Enabled) Disable(); else Enable();
            return Enabled;
        }

        private void OnRollsChanged(params object[] args)
        {
            if (!Enabled || _pinning) return;
            if (args == null || args.Length < 3) return;
            if (!(args[0] is Guid id) || id != _ctx.PlayerGuid) return;
            if (!(args[1] is int current) || !(args[2] is int max)) return;
            if (current >= max) return;
            if (!_ctx.TryResolve<IRollPoolService>(out var rolls) || rolls == null) return;

            _pinning = true;
            rolls.RestoreCurrent(id, max);
            _pinning = false;
        }

        public void Dispose() => Disable();
    }
}
