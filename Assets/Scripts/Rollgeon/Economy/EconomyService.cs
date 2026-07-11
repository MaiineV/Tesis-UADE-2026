using System;
using Patterns;
using Patterns.Save;
using UnityEngine;

namespace Rollgeon.Economy
{
    /// <summary>
    /// Implementación minimal de <see cref="IEconomyService"/> — contador de oro
    /// in-memory + trigger de <c>OnGoldChanged</c>. MVP antes del sistema de
    /// atributos real (§1.3).
    /// <para>
    /// <b>Reset por run.</b> El servicio es Global (una sola instancia), así que el
    /// oro se resetea a <c>startingGold</c> en <c>OnRunStart</c> — sin esto el oro de
    /// la run anterior se filtraba a la siguiente. En un resume, el reset corre antes
    /// del restore del save (el handler de OnRunStart de SaveSystemBootstrap captura
    /// después), y el valor guardado llega vía <see cref="RestoreState"/> en el
    /// registro/LoadFromDisk.
    /// </para>
    /// </summary>
    public sealed class EconomyService : IEconomyService, ISaveable, IDisposable
    {
        private readonly int _startingGold;
        private int _gold;

        private EventManager.EventReceiver _onRunStart;

        public int CurrentGold => _gold;

        public EconomyService(int startingGold)
        {
            _startingGold = Mathf.Max(0, startingGold);
            _gold = _startingGold;
            EventManager.Trigger(EventName.OnGoldChanged, _gold, _gold);

            _onRunStart = OnRunStart;
            EventManager.Subscribe(EventName.OnRunStart, _onRunStart);
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            _gold += amount;
            EventManager.Trigger(EventName.OnGoldChanged, _gold, amount);
        }

        public bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (_gold < amount) return false;
            _gold -= amount;
            EventManager.Trigger(EventName.OnGoldChanged, _gold, -amount);
            return true;
        }

        public bool CanAfford(int amount) => amount <= 0 || _gold >= amount;

        public void ResetTo(int amount) => SetGold(Mathf.Max(0, amount));

        // ---------------------------------------------------------------- run reset

        // Schema EventName.OnRunStart: args = [Guid runId, string rulesetId]
        private void OnRunStart(params object[] args)
        {
            // En resume el oro viene del save (restaurado en LoadFromDisk) — resetear
            // acá lo pisaría y el capture posterior de SaveSystemBootstrap escribiría
            // el valor reseteado al cache.
            if (Rollgeon.Run.RunBootstrapper.IsResuming) return;
            SetGold(_startingGold);
        }

        private void SetGold(int value)
        {
            var delta = value - _gold;
            _gold = value;
            EventManager.Trigger(EventName.OnGoldChanged, _gold, delta);
        }

        // ---------------------------------------------------------------- ISaveable

        public string SaveKey => "run.gold";

        public object CaptureState() => _gold;

        public void RestoreState(object state)
        {
            if (state is int gold) SetGold(Mathf.Max(0, gold));
        }

        // ---------------------------------------------------------------- IDisposable

        public void Dispose()
        {
            SaveSystem.Unregister(this);
            if (_onRunStart != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStart);
                _onRunStart = null;
            }
        }
    }
}
