using System;
using System.Collections.Generic;
using Patterns;
using Patterns.Save;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using UnityEngine;

namespace Rollgeon.Run
{
    /// <summary>
    /// <see cref="ISaveable"/> de los atributos del player (§15): HP/Energía/Attack/
    /// Speed actuales + modifiers con lifetime <c>Run</c>/<c>Permanent</c> — ahí viven
    /// los stat grants de upgrades de personaje y pasivas, así que el resume los
    /// recupera por este único snapshot.
    /// <para>
    /// <b>Exclusiones.</b> <c>Shield</c> y los modifiers <c>Turns</c>/<c>Encounter</c>
    /// son combat-scoped: el resume arranca fuera de combate y no deben revivir.
    /// </para>
    /// <para>
    /// <b>Ordering.</b> Se registra al final de <c>RunController.RegisterPlayer</c>,
    /// después de que Energy fue inicializada — el auto-restore de
    /// <c>SaveSystem.Register</c> pisa los valores base con los guardados. Si Energy
    /// aún no existe al restaurar (bag sin EnergyService), se crea defensivamente.
    /// </para>
    /// </summary>
    public sealed class PlayerAttributesSaveable : ISaveable, IDisposable
    {
        public const string SaveKeyConst = "run.player_attributes";
        private const string LogPrefix = "[PlayerAttributesSaveable] ";

        private readonly ModifiableAttributes _attrs;

        public PlayerAttributesSaveable(ModifiableAttributes attrs)
        {
            _attrs = attrs ?? throw new ArgumentNullException(nameof(attrs));
        }

        // ---------------------------------------------------------------- ISaveable

        public string SaveKey => SaveKeyConst;

        public object CaptureState()
        {
            var snapshot = new PlayerAttributesSnapshot();
            CaptureStat<Health>(snapshot, nameof(Health));
            CaptureStat<Energy>(snapshot, nameof(Energy));
            CaptureStat<Attack>(snapshot, nameof(Attack));
            CaptureStat<Speed>(snapshot, nameof(Speed));
            return snapshot;
        }

        public void RestoreState(object state)
        {
            if (state is not PlayerAttributesSnapshot snapshot) return;

            foreach (var entry in snapshot.Stats)
            {
                var attr = ResolveOrCreate(entry.Stat);
                if (attr == null)
                {
                    Debug.LogWarning(LogPrefix + $"Stat '{entry.Stat}' del save no existe " +
                                     "en el player actual — se descarta.");
                    continue;
                }

                attr.Value = entry.Value;

                foreach (var m in entry.Modifiers)
                {
                    var mod = new Modifier<int>
                    {
                        Amount = m.Amount,
                        Operation = m.Operation,
                        Duration = m.Duration,
                        ModifierId = ParseGuid(m.ModifierId),
                        CarrierId = ParseGuid(m.CarrierId),
                        SourceId = ParseGuid(m.SourceId),
                        Direction = m.Direction,
                        Lifetime = m.Lifetime,
                        TickEvent = m.TickEvent,
                    };
                    attr.AddModifier<int>(mod);
                    // Re-hook de eventos post-deserialize (§3.5) — idempotente.
                    mod.OnLoad();
                }
            }
        }

        // ---------------------------------------------------------------- IDisposable

        /// <summary>Invocado por <c>ClearScope(Run)</c> — evita duplicados de SaveKey entre runs.</summary>
        public void Dispose()
        {
            SaveSystem.Unregister(this);
        }

        // ---------------------------------------------------------------- helpers

        private void CaptureStat<T>(PlayerAttributesSnapshot snapshot, string name)
            where T : BaseAttribute<int>
        {
            if (!_attrs.HasAttribute<T>()) return;
            var attr = _attrs.GetAttribute<T>() as BaseAttribute<int>;
            if (attr == null) return;

            var entry = new PlayerStatEntry { Stat = name, Value = attr.Value };
            foreach (var mod in attr.GetRawModifiers())
            {
                if (mod.Lifetime != ModifierLifetime.Run &&
                    mod.Lifetime != ModifierLifetime.Permanent)
                {
                    continue;
                }
                entry.Modifiers.Add(new PlayerStatModifierEntry
                {
                    Amount = mod.Amount,
                    Operation = mod.Operation,
                    Duration = mod.Duration,
                    ModifierId = mod.ModifierId.ToString(),
                    CarrierId = mod.CarrierId.ToString(),
                    SourceId = mod.SourceId.ToString(),
                    Direction = mod.Direction,
                    Lifetime = mod.Lifetime,
                    TickEvent = mod.TickEvent,
                });
            }
            snapshot.Stats.Add(entry);
        }

        private BaseAttribute<int> ResolveOrCreate(string statName)
        {
            switch (statName)
            {
                case nameof(Health): return _attrs.HasAttribute<Health>() ? _attrs.GetAttribute<Health>() : null;
                case nameof(Attack): return _attrs.HasAttribute<Attack>() ? _attrs.GetAttribute<Attack>() : null;
                case nameof(Speed): return _attrs.HasAttribute<Speed>() ? _attrs.GetAttribute<Speed>() : null;
                case nameof(Energy):
                    if (!_attrs.HasAttribute<Energy>())
                    {
                        // EnergyService puede no haber corrido todavía — crearla acá
                        // es seguro: InitializeForEntity respeta la existente o el
                        // restore posterior del value la pisa igual.
                        _attrs.SetAttribute<Energy>(new Energy(0));
                    }
                    return _attrs.GetAttribute<Energy>();
                default: return null;
            }
        }

        private static Guid ParseGuid(string value)
        {
            return Guid.TryParse(value, out var guid) ? guid : Guid.Empty;
        }
    }

    /// <summary>DTO serializable de <see cref="PlayerAttributesSaveable"/>.</summary>
    [Serializable]
    public class PlayerAttributesSnapshot
    {
        public List<PlayerStatEntry> Stats = new List<PlayerStatEntry>();
    }

    [Serializable]
    public class PlayerStatEntry
    {
        public string Stat;
        public int Value;
        public List<PlayerStatModifierEntry> Modifiers = new List<PlayerStatModifierEntry>();
    }

    [Serializable]
    public class PlayerStatModifierEntry
    {
        public int Amount;
        public ModifierOperation Operation;
        public int Duration;
        public string ModifierId;
        public string CarrierId;
        public string SourceId;
        public ModifierDirection Direction;
        public ModifierLifetime Lifetime;
        public EventName TickEvent;
    }
}
