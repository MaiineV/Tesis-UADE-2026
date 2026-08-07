using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AntiRepeat
{
    /// <summary>
    /// Servicio POCO que mantiene el modo vivo del pasivo anti-repetición (A/B) y, en
    /// <b>Mode Dice</b>, bloquea un dado al azar al inicio de cada turno del jugador reusando
    /// <see cref="IDiceBlockService"/> (el mismo candado que usa el Boss 1). Registrado global.
    /// <para>
    /// Siembra el <see cref="Mode"/> desde <see cref="AntiRepeatConfigSO"/> en
    /// <see cref="Register"/> (los SettingsAssets ya están registrados en el ServiceLocator
    /// para ese punto — <c>ServiceBootstrapSO.RegisterAll</c> corre Settings antes que
    /// ExtraServices). El comando de consola <c>passive</c> flipea el modo sin tocar el asset.
    /// </para>
    /// </summary>
    public sealed class AntiRepeatModeService : IAntiRepeatModeService, IPreloadableService, IDisposable
    {
        private AntiRepeatMode _mode = AntiRepeatMode.Combo;

        private EventManager.EventReceiver _onTurnStartedHandler;
        private Func<Guid> _playerGuidResolver;
        private System.Random _rng;

        public int Priority => 82; // después de DiceBlockService (80) para que el candado exista.

        public AntiRepeatMode Mode => _mode;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _playerGuidResolver ??= DefaultPlayerGuidResolver;
            _rng ??= new System.Random();

            SeedFromConfig();
            SubscribeHandlers();

            ServiceLocator.AddService<IAntiRepeatModeService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests — inyecta resolver de player guid y RNG determinista.</summary>
        public void ConfigureForTests(Func<Guid> playerGuidResolver, System.Random rng = null)
        {
            _playerGuidResolver = playerGuidResolver ?? DefaultPlayerGuidResolver;
            _rng = rng ?? new System.Random();
            SubscribeHandlers();
        }

        private void SeedFromConfig()
        {
            if (ServiceLocator.TryGetService<AntiRepeatConfigSO>(out var cfg) && cfg != null)
                _mode = cfg.Mode;
            else
                _mode = AntiRepeatMode.Combo; // fallback si el config no está en SettingsAssets.
        }

        private void SubscribeHandlers()
        {
            if (_onTurnStartedHandler != null) return; // idempotente (Register + ConfigureForTests)
            _onTurnStartedHandler = OnTurnStartedExternal;
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
        }

        public void Dispose()
        {
            if (_onTurnStartedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
                _onTurnStartedHandler = null;
            }
        }

        // ======================================================================
        // IAntiRepeatModeService
        // ======================================================================

        public void SetMode(AntiRepeatMode mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            EventManager.Trigger(EventName.OnAntiRepeatModeChanged);
        }

        // ======================================================================
        // Mode DICE — bloqueo de un dado por turno
        // ======================================================================

        // NOTE: PLAYTEST REQUIRED — este handler asume que EventName.OnTurnStarted del jugador
        // se dispara ANTES de que el jugador tire los dados. Si en runtime resulta que el roll
        // ocurre antes que OnTurnStarted (o el bag aún no está listo en este punto), el bloqueo
        // no afecta la tirada del turno actual y el hook debe moverse a un punto más temprano
        // (ej. inicio de PlayerTurnState, antes del roll). Verificar contra TurnManager /
        // PlayerTurnState en un playtest real antes de dar por bueno el Mode Dice.
        private void OnTurnStartedExternal(params object[] args)
        {
            if (_mode != AntiRepeatMode.Dice) return;
            if (args == null || args.Length == 0 || !(args[0] is Guid turnGuid)) return;

            var playerGuid = ResolvePlayerGuid();
            if (playerGuid == Guid.Empty || turnGuid != playerGuid) return;

            if (!ServiceLocator.TryGetService<IDiceBlockService>(out var dice) || dice == null) return;

            int bagSize = ResolveBagSize();
            if (bagSize <= 0) return;

            // Elegimos un slot no-bloqueado al azar (el boss podría haber bloqueado alguno ya
            // este turno; no lo pisamos). DiceBlockService auto-limpia en OnTurnFinished.
            var candidates = new List<int>(bagSize);
            for (int i = 0; i < bagSize; i++)
                if (!dice.IsBlocked(i)) candidates.Add(i);

            if (candidates.Count == 0) return;

            int pick = candidates[NextInt(candidates.Count)];
            dice.Block(pick);
        }

        private int ResolveBagSize()
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps?.DiceBag?.Dice != null)
                return ps.DiceBag.Dice.Count;
            return 0;
        }

        private int NextInt(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 1) return 0;
            return _rng != null
                ? _rng.Next(exclusiveUpperBound)
                : UnityEngine.Random.Range(0, exclusiveUpperBound);
        }

        private Guid ResolvePlayerGuid()
            => _playerGuidResolver != null ? _playerGuidResolver() : Guid.Empty;

        private static Guid DefaultPlayerGuidResolver()
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var svc) && svc != null)
                return svc.PlayerGuid;
            return Guid.Empty;
        }
    }
}
