using System;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Resuelve el dispatch genérico string→stat (AttributesManager.SetAttributeValue es genérico
    /// en tiempo de compilación, no se puede invocar desde un string sin esta tabla).
    /// </summary>
    internal static class StatAccessor
    {
        public static readonly string[] SettableNames = { "Health", "Attack", "Speed", "Energy", "Shield" };

        public static bool TrySet(AttributesManager am, Guid id, string stat, int value, out string error)
        {
            error = null;
            switch ((stat ?? string.Empty).ToLowerInvariant())
            {
                case "health": am.SetAttributeValue<Health, int>(id, value); return true;
                case "attack": am.SetAttributeValue<Attack, int>(id, value); return true;
                case "speed": am.SetAttributeValue<Speed, int>(id, value); return true;
                case "energy": am.SetAttributeValue<Energy, int>(id, value); return true;
                case "shield": am.SetAttributeValue<Shield, int>(id, value); return true;
                default:
                    error = $"Stat desconocido: '{stat}'. Opciones: {string.Join(", ", SettableNames)}.";
                    return false;
            }
        }

        public static bool TryGet(AttributesManager am, Guid id, string stat, out int value, out string error)
        {
            error = null;
            value = 0;
            switch ((stat ?? string.Empty).ToLowerInvariant())
            {
                case "health": value = am.GetAttributeValue<Health, int>(id); return true;
                case "attack": value = am.GetAttributeValue<Attack, int>(id); return true;
                case "speed": value = am.GetAttributeValue<Speed, int>(id); return true;
                case "energy": value = am.GetAttributeValue<Energy, int>(id); return true;
                case "shield": value = am.GetAttributeValue<Shield, int>(id); return true;
                default:
                    error = $"Stat desconocido: '{stat}'.";
                    return false;
            }
        }
    }
}
