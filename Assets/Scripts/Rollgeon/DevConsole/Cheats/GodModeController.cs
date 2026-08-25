using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.DevConsole.Core;
using Rollgeon.Player;

namespace Rollgeon.DevConsole.Cheats
{
    /// <summary>
    /// Vida infinita por "pin de HP": se suscribe a <c>OnAttributeChanged</c> y, cuando el HP del
    /// player baja, lo restaura al máximo (<see cref="ClassHeroSO.BaseMaxHp"/>). Cero cambios al
    /// sistema de combate. Limitación: un golpe ≥ HP máximo desde full igual marca <c>WasLethal</c>.
    /// </summary>
    public sealed class GodModeController : IDisposable
    {
        private readonly IDevConsoleContext _ctx;
        private readonly EventManager.EventReceiver _handler;
        private readonly EventManager.EventReceiver _onRunEndHandler;
        private bool _pinning;

        public bool Enabled { get; private set; }

        public GodModeController(IDevConsoleContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _handler = OnAttributeChanged;
            _onRunEndHandler = OnRunEnd;
            // BUG-062 (hardening): DevConsoleSession vive fuera del ServiceScope.Run — sin
            // esto, God Mode prendido para debuggear un piso sobrevive a EndRun y la
            // siguiente run (o el piso 2+ de la misma) arranca "inmortal" sin que nadie lo
            // haya pedido. Auto-apagar en OnRunEnd es la red de seguridad.
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        public void Enable()
        {
            if (Enabled) return;
            Enabled = true;
            EventManager.Subscribe(EventName.OnAttributeChanged, _handler);
            PinToMax();
        }

        public void Disable()
        {
            if (!Enabled) return;
            Enabled = false;
            EventManager.UnSubscribe(EventName.OnAttributeChanged, _handler);
        }

        private void OnRunEnd(params object[] args)
        {
            if (!Enabled) return;
            UnityEngine.Debug.LogWarning(
                "[GodModeController] La run terminó con God Mode activo — auto-desactivando " +
                "para que la próxima run no arranque inmortal (BUG-062).");
            Disable();
        }

        public bool Toggle()
        {
            if (Enabled) Disable(); else Enable();
            return Enabled;
        }

        private void OnAttributeChanged(params object[] args)
        {
            if (!Enabled || _pinning) return;
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid id) || id != _ctx.PlayerGuid) return;
            if (!(args[1] is Type t) || t != typeof(Health)) return;
            PinToMax();
        }

        private void PinToMax()
        {
            var id = _ctx.PlayerGuid;
            if (id == Guid.Empty) return;
            if (!_ctx.TryResolve<AttributesManager>(out var am) || am == null) return;

            // BUG-022: pin contra el max efectivo (base + grants in-run), no la constante.
            int max = Rollgeon.Player.PlayerMaxHp.Resolve(id);
            if (max <= 0) return;
            int cur = am.GetAttributeValue<Health, int>(id);
            if (cur < max)
            {
                _pinning = true; // corta la re-entrada del OnAttributeChanged que dispara el set
                am.SetAttributeValue<Health, int>(id, max);
                _pinning = false;
            }
        }

        public void Dispose()
        {
            Disable();
            EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
        }
    }
}
