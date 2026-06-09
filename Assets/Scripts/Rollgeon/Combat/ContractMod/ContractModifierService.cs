using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.ContractMod
{
    /// <summary>
    /// Implementación POCO de <see cref="IContractModifierService"/> (Sistemas prerequisito Bosses §4).
    /// Overlay runtime puro: nunca muta los <c>BaseComboSO</c>. Mismo lifecycle que los demás
    /// servicios de boss (registro global, state run-scoped, clear en OnCombatEnd/OnRunEnd).
    /// </summary>
    public sealed class ContractModifierService : IContractModifierService, IPreloadableService, IDisposable
    {
        // Modificadores colapsados por comboId. Lazy por la razón de siempre (Odin / ctor bypass).
        private Dictionary<string, ComboMod> _mods;
        private Dictionary<string, ComboMod> Mods => _mods ??= new Dictionary<string, ComboMod>();

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Resolver de la <see cref="ContractSheet"/> del jugador (inyectable en tests).</summary>
        private Func<ContractSheet> _sheetResolver;

        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _sheetResolver = DefaultSheetResolver;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<IContractModifierService>(this, ServiceScope.Global);
        }

        /// <summary>Hook EditMode tests — inyecta el resolver del sheet.</summary>
        public void ConfigureForTests(Func<ContractSheet> sheetResolver)
        {
            _sheetResolver = sheetResolver ?? DefaultSheetResolver;
        }

        public void Dispose()
        {
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
            Mods.Clear();
        }

        // ======================================================================
        // IContractModifierService
        // ======================================================================

        /// <inheritdoc />
        public int GetEffectiveBaseDamage(string comboId, int baseDamage)
        {
            if (string.IsNullOrEmpty(comboId) || !Mods.TryGetValue(comboId, out var mod))
                return baseDamage;

            if (mod.Forbidden) return 0;

            int value = mod.SetValue ?? baseDamage;
            value = Mathf.RoundToInt(value * mod.Multiplier);
            return value < 0 ? 0 : value;
        }

        /// <inheritdoc />
        public bool IsForbidden(string comboId)
            => !string.IsNullOrEmpty(comboId) && Mods.TryGetValue(comboId, out var mod) && mod.Forbidden;

        /// <inheritdoc />
        public bool HasAnyModifier => _mods != null && _mods.Count > 0;

        /// <inheritdoc />
        public void MultiplyCombo(string comboId, float factor)
        {
            if (string.IsNullOrEmpty(comboId)) return;
            var mod = Get(comboId);
            mod.Multiplier *= factor;
            Mods[comboId] = mod;
            RaiseChanged();
        }

        /// <inheritdoc />
        public void ForbidCombo(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return;
            var mod = Get(comboId);
            mod.Forbidden = true;
            Mods[comboId] = mod;
            RaiseChanged();
        }

        /// <inheritdoc />
        public void SetComboToNeighbor(string comboId, int direction)
        {
            if (string.IsNullOrEmpty(comboId) || direction == 0) return;

            var sheet = _sheetResolver?.Invoke();
            if (sheet?.Combos == null)
            {
                Debug.LogWarning("[ContractModifierService] No se pudo resolver el ContractSheet para SetComboToNeighbor.");
                return;
            }

            if (!TryGetBaseDamage(sheet, comboId, out int ownBase)) return;

            // Vecino por daño base: el inmediatamente superior (dir>0) o inferior (dir<0).
            int? neighborValue = null;
            int bestDelta = int.MaxValue;
            foreach (var combo in sheet.Combos)
            {
                if (combo == null || combo.ComboId == comboId) continue;
                int other = combo.BaseDamage;
                if (direction > 0 && other > ownBase && (other - ownBase) < bestDelta)
                {
                    bestDelta = other - ownBase;
                    neighborValue = other;
                }
                else if (direction < 0 && other < ownBase && (ownBase - other) < bestDelta)
                {
                    bestDelta = ownBase - other;
                    neighborValue = other;
                }
            }

            if (neighborValue == null) return; // ya es el máximo/mínimo — no-op.

            var mod = Get(comboId);
            mod.SetValue = neighborValue;
            Mods[comboId] = mod;
            RaiseChanged();
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            if (_mods == null || _mods.Count == 0) return;
            _mods.Clear();
            RaiseChanged();
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private ComboMod Get(string comboId)
            => Mods.TryGetValue(comboId, out var mod) ? mod : ComboMod.Identity;

        private static bool TryGetBaseDamage(ContractSheet sheet, string comboId, out int baseDamage)
        {
            foreach (var combo in sheet.Combos)
            {
                if (combo != null && combo.ComboId == comboId)
                {
                    baseDamage = combo.BaseDamage;
                    return true;
                }
            }
            baseDamage = 0;
            return false;
        }

        private static void RaiseChanged() => EventManager.Trigger(EventName.OnContractModifierChanged);

        private void OnScopeEndedExternal(params object[] args) => ClearAll();

        private static ContractSheet DefaultSheetResolver()
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps?.CurrentHero != null)
                return ps.CurrentHero.Sheet;
            return null;
        }

        /// <summary>Modificadores colapsados de un combo. Struct: copiamos por valor al guardar.</summary>
        private struct ComboMod
        {
            public bool Forbidden;
            public float Multiplier;
            public int? SetValue;

            public static ComboMod Identity => new ComboMod { Forbidden = false, Multiplier = 1f, SetValue = null };
        }
    }
}
