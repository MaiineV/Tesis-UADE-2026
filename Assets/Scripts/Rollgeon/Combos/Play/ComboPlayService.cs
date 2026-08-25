using System;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Combos.Play
{
    /// <summary>
    /// Implementación de <see cref="IComboPlayService"/>. Dueño único del
    /// <see cref="EnchantmentScratchApplier"/> sobre el play scratch: los suscriptores de
    /// <c>ComboPlayedPayload</c> SOLO escriben al scratch; aplicar acá una única vez evita
    /// el doble-apply de oro/escudo que ocurriría si cada canal aplicara por su cuenta.
    /// </summary>
    public sealed class ComboPlayService : IComboPlayService, IDisposable
    {
        private const string LogPrefix = "[ComboPlayService] ";

        // Depth counter y no bool: un efecto puede ejecutar otro behavior (chain, items)
        // y re-entrar a BeginPlay — el Begin anidado no re-emite ni resetea la ventana.
        private int _depth;
        private bool _subscribed;

        public EnchantmentScratch CurrentPlayScratch { get; private set; }

        // Persiste más allá de EndPlay para que el daño diferido al frame de impacto lo lea.
        // Se reemplaza en cada BeginPlay con combo y se limpia en OnRollResolved / run-end.
        public EnchantmentScratch LastPlayScratch { get; private set; }

        public bool IsPlayWindowOpen => _depth > 0;

        public string CurrentComboId { get; private set; }

        public void Register()
        {
            ServiceLocator.AddService<IComboPlayService>(this, ServiceScope.Global);
            SubscribeEvents();
        }

        public void Dispose()
        {
            UnsubscribeEvents();
            _depth = 0;
            CurrentPlayScratch = null;
            LastPlayScratch = null;
            CurrentComboId = null;
        }

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.Subscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEndHandler);
            EventManager.UnSubscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            _subscribed = false;
        }

        private void OnRunEndHandler(params object[] args)
        {
            // Defensivo: la ventana vive dentro de un Execute sincrónico, pero si la run
            // termina a mitad de una ejecución no queremos scratch colgado en la próxima.
            _depth = 0;
            CurrentPlayScratch = null;
            LastPlayScratch = null;
            CurrentComboId = null;
        }

        private void OnRollResolvedHandler(params object[] args)
        {
            // Inicio de turno (nuevo roll): el bono at-played del turno anterior ya fue
            // consumido por el daño diferido de su impacto. Limpiarlo acá evita que el
            // preview de este turno lo vea (contrato: el preview nunca ve bonos jugados
            // viejos) y que se filtre a un combo distinto.
            LastPlayScratch = null;
        }

        public void BeginPlay(EffectContext effCtx)
        {
            _depth++;
            if (_depth > 1) return;

            CurrentPlayScratch = null;
            CurrentComboId = null;

            var combo = effCtx?.ComboResult;
            if (combo == null || !combo.Value.IsMatch || string.IsNullOrEmpty(combo.Value.ComboId))
            {
                // Acción sin combo: no hay bono at-played para el daño diferido.
                LastPlayScratch = null;
                return;
            }

            var scratch = new EnchantmentScratch();
            CurrentPlayScratch = scratch;
            LastPlayScratch = scratch;
            CurrentComboId = combo.Value.ComboId;

            TypedEvent<ComboPlayedPayload>.Raise(new ComboPlayedPayload
            {
                SourceGuid = effCtx.SourceGuid,
                TargetGuid = effCtx.TargetGuid,
                ComboId = combo.Value.ComboId,
                ComboResult = combo.Value,
                DiceResult = effCtx.DiceResult,
                KeptDice = effCtx.KeptDice,
                KeptDiceOriginalIndices = effCtx.KeptDiceOriginalIndices,
                // BUG-060: viaja el discriminante de la acción que abrió esta ventana — sin
                // esto un trío tirado para MOVERSE (mismo bag, misma detección de combo que
                // un ataque) disparaba los hooks de oro/daño "at combo played" igual que un
                // golpe real.
                ActionKind = effCtx.ActionKind,
            });

            // Recursos (oro/stats) acumulados por los suscriptores se materializan una sola
            // vez. El bono de combo NO se aplica acá: queda en el scratch para que
            // PlayerComboDamage.Resolve lo lea. El daño del ataque real está diferido al
            // frame de impacto (fuera de esta ventana), por eso el scratch persiste en
            // LastPlayScratch hasta el próximo roll/play — si solo viviera en
            // CurrentPlayScratch el bono se perdería al cerrar la ventana.
            EnchantmentScratchApplier.Apply(scratch, effCtx.SourceGuid);
        }

        public void EndPlay()
        {
            if (_depth == 0)
            {
                Debug.LogWarning(LogPrefix + "EndPlay sin BeginPlay previo — wiring desbalanceado.");
                return;
            }

            _depth--;
            if (_depth > 0) return;

            // Cierra la ventana. LastPlayScratch NO se toca: sobrevive para que el daño
            // diferido al frame de impacto lea el bono at-played (lo limpia OnRollResolved).
            CurrentPlayScratch = null;
            CurrentComboId = null;
        }
    }
}
