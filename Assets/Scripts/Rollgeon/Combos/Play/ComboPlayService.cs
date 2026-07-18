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
            CurrentComboId = null;
        }

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnRunEnd, OnRunEndHandler);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEndHandler);
            _subscribed = false;
        }

        private void OnRunEndHandler(params object[] args)
        {
            // Defensivo: la ventana vive dentro de un Execute sincrónico, pero si la run
            // termina a mitad de una ejecución no queremos scratch colgado en la próxima.
            _depth = 0;
            CurrentPlayScratch = null;
            CurrentComboId = null;
        }

        public void BeginPlay(EffectContext effCtx)
        {
            _depth++;
            if (_depth > 1) return;

            CurrentPlayScratch = null;
            CurrentComboId = null;

            var combo = effCtx?.ComboResult;
            if (combo == null || !combo.Value.IsMatch || string.IsNullOrEmpty(combo.Value.ComboId))
                return;

            var scratch = new EnchantmentScratch();
            CurrentPlayScratch = scratch;
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
            });

            // Recursos (oro/stats) acumulados por los suscriptores se materializan una sola
            // vez. El bono de combo NO se aplica acá: queda en el scratch para que
            // PlayerComboDamage.Resolve lo lea durante esta misma ventana. Asunción
            // documentada: EffDealDamage resuelve sincrónico dentro de la ventana — si el
            // daño se difiriera más allá del Execute, el bono se perdería.
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

            CurrentPlayScratch = null;
            CurrentComboId = null;
        }
    }
}
