using System;
using System.Collections.Generic;
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
        // Cota dura del restore: un save nunca debería traer menos que el piso más
        // profundo autorable (Tarjeta de Crédito = −30) — el clamp protege contra basura.
        private const int AbsoluteMinGold = -1000;

        private readonly int _startingGold;
        private int _gold;

        // Pisos registrados por fuente (item id). Gana el más bajo; vacío = 0.
        private readonly Dictionary<string, int> _floors = new Dictionary<string, int>();

        private EventManager.EventReceiver _onRunStart;

        public int CurrentGold => _gold;

        public int MinGold
        {
            get
            {
                int min = 0;
                foreach (var floor in _floors.Values)
                    if (floor < min) min = floor;
                return min;
            }
        }

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
            if (_gold - amount < MinGold) return false;
            _gold -= amount;
            EventManager.Trigger(EventName.OnGoldChanged, _gold, -amount);
            return true;
        }

        public bool CanAfford(int amount) => amount <= 0 || _gold - amount >= MinGold;

        public void ResetTo(int amount) => SetGold(Mathf.Max(0, amount));

        // ---------------------------------------------------------------- gold floor

        public void SetGoldFloor(string sourceId, int floor)
        {
            if (string.IsNullOrEmpty(sourceId) || floor >= 0) return;
            _floors[sourceId] = floor;
        }

        public void ClearGoldFloor(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            // Sin confiscar: si el jugador quedó en deuda, la deuda sigue hasta que
            // sume oro. Solo se cierra la puerta a endeudarse más.
            _floors.Remove(sourceId);
        }

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
            // Un save con oro negativo es legítimo (deuda de Tarjeta de Crédito). El piso
            // real lo re-registra el inventario al restaurar el item; acá solo se filtra basura.
            if (state is int gold) SetGold(Mathf.Max(AbsoluteMinGold, gold));
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
