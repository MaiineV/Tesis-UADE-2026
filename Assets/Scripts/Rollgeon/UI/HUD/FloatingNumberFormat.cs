using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Estilo resuelto de un floating number: qué texto, qué tint, qué escala relativa
    /// (sobre el tamaño base del prefab) y qué curva de movimiento usar.
    /// </summary>
    public readonly struct FloatingNumberStyle
    {
        public readonly string Text;
        public readonly Color Tint;
        public readonly float Scale;
        public readonly FloatingMotion Motion;

        public FloatingNumberStyle(string text, Color tint, float scale, FloatingMotion motion = FloatingMotion.Rise)
        {
            Text = text;
            Tint = tint;
            Scale = scale;
            Motion = motion;
        }
    }

    /// <summary>
    /// Factories puras dato → <see cref="FloatingNumberStyle"/>. Separado de
    /// <see cref="FloatingDamageSpawner"/> (que resuelve posición/parenting/stagger) para
    /// poder testear el mapeo texto/color/escala sin GameObjects ni prefabs.
    /// </summary>
    public static class FloatingNumberFormat
    {
        /// <summary>
        /// Daño resuelto por <c>DamagePipeline</c> (TypedEvent&lt;DamageResolvedPayload&gt;).
        /// <paramref name="incoming"/> gana sobre <paramref name="weakness"/>: un golpe
        /// recibido siempre se pinta como incoming aunque el payload traiga weakness en "N".
        /// </summary>
        public static FloatingNumberStyle ForDamage(int amount, bool incoming, bool weakness = false)
        {
            if (incoming)
                return new FloatingNumberStyle($"-{amount}", FloatingNumberPalette.DamageTaken, 1.1f);

            if (weakness)
                return new FloatingNumberStyle($"{amount}!", FloatingNumberPalette.DamageWeakness, 1.25f);

            return new FloatingNumberStyle(amount.ToString(), FloatingNumberPalette.DamageDealt, 1f);
        }

        /// <summary>
        /// Ruta legacy (<c>EventName.OnFloatingNumberRequested</c>): heal, shield, gold,
        /// status ticks. El redondeo replica el <c>FormatByType</c> que reemplaza.
        /// </summary>
        public static FloatingNumberStyle ForType(FloatingNumberType type, float value)
        {
            int rounded = Mathf.RoundToInt(value);
            switch (type)
            {
                case FloatingNumberType.Gold:
                    // Motion.Arc: el oro "salta" en vez de subir derecho — lo distingue
                    // visualmente del resto de los floating numbers a simple vistazo.
                    return new FloatingNumberStyle($"+{rounded} G", FloatingNumberPalette.Gold, 0.9f, FloatingMotion.Arc);
                case FloatingNumberType.Shield:
                    return new FloatingNumberStyle($"+{rounded}", FloatingNumberPalette.Shield, 0.95f);
                case FloatingNumberType.Status:
                    return new FloatingNumberStyle($"+{rounded}", FloatingNumberPalette.Status, 0.9f);
                case FloatingNumberType.Heal:
                    return new FloatingNumberStyle($"+{rounded}", FloatingNumberPalette.Heal, 0.95f);
                case FloatingNumberType.Damage:
                default:
                    return ForDamage(rounded, incoming: false);
            }
        }

        /// <summary>Shield absorbió todo el hit — un "0" rojo/crema confunde (parece daño real).</summary>
        public static FloatingNumberStyle ShieldBlocked() =>
            new FloatingNumberStyle("Bloqueado", FloatingNumberPalette.Shield, 0.9f);

        /// <summary>Shield llegó a 0 en este hit y queda daño residual — se spawnea antes del número.</summary>
        public static FloatingNumberStyle ShieldBroken() =>
            new FloatingNumberStyle("Escudo roto", FloatingNumberPalette.Shield, 0.9f);
    }
}
