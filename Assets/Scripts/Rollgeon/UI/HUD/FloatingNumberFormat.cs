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
                case FloatingNumberType.GoldLost:
                    // Mismo matiz y mismo salto que el oro que entra: es el mismo recurso, y lo que
                    // distingue "te di" de "te saqué" es el signo. Se manda el valor en positivo y
                    // el signo lo pone el formato, igual que hace ForDamage con el daño recibido.
                    return new FloatingNumberStyle($"-{Mathf.Abs(rounded)} G", FloatingNumberPalette.Gold, 0.9f, FloatingMotion.Arc);
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

        /// <summary>
        /// Texto literal con el look de <paramref name="type"/> — para lo que no es una cantidad:
        /// "Soborno", "Escudo roto", el escalón que bajó. Hereda tint, escala y motion del tipo, así
        /// que un mensaje de oro sigue saltando como el oro y uno de estado sigue subiendo derecho.
        /// </summary>
        /// <remarks>
        /// Existe porque el canal legacy (<c>EventName.OnFloatingNumberRequested</c>) sólo transporta
        /// un <c>float</c>, y hay avisos que no son un número: "−1 escalón" con el formato de
        /// <see cref="FloatingNumberType.Status"/> saldría como <c>"+-1"</c>. El spawner acepta un
        /// <c>string</c> en el slot del valor y cae acá.
        /// </remarks>
        public static FloatingNumberStyle ForText(FloatingNumberType type, string text)
        {
            var style = ForType(type, 0f);
            return new FloatingNumberStyle(text, style.Tint, style.Scale, style.Motion);
        }

        /// <summary>Shield absorbió todo el hit — un "0" rojo/crema confunde (parece daño real).</summary>
        public static FloatingNumberStyle ShieldBlocked() =>
            new FloatingNumberStyle("Bloqueado", FloatingNumberPalette.Shield, 0.9f);

        /// <summary>Shield llegó a 0 en este hit y queda daño residual — se spawnea antes del número.</summary>
        public static FloatingNumberStyle ShieldBroken() =>
            new FloatingNumberStyle("Escudo roto", FloatingNumberPalette.Shield, 0.9f);

        /// <summary>
        /// El stage 3 del pipeline recortó el golpe: se spawnea <b>antes</b> del número, igual que
        /// <see cref="ShieldBroken"/>, y el número que sigue ya es el reducido.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Sin esto, la mesa de La Generala en pie es un golpe de 30 que hace 9 sin explicación en
        /// pantalla: el jugador no aprende "rompé los dados", aprende "mis golpes no sirven". El
        /// porcentaje es lo único que conecta la mesa con la barra del jefe.
        /// </para>
        /// <para>
        /// Va por acá y no por un badge en <c>BossBarView</c> a propósito: el badge de debilidad se
        /// cablea a mano prefab por prefab (<c>docs/setup/boss-weakness-badge.md</c>), y la
        /// legibilidad de una mecánica no puede depender de un paso manual de setup.
        /// </para>
        /// <para>
        /// Tint de <see cref="FloatingNumberPalette.Shield"/>: es lo mismo que ya significa "algo
        /// frenó este golpe", y darle un color propio agregaría una convención nueva para la misma
        /// idea.
        /// </para>
        /// </remarks>
        public static FloatingNumberStyle DamageReduced(float incomingMultiplier)
        {
            int percent = Mathf.RoundToInt((1f - incomingMultiplier) * 100f);
            return new FloatingNumberStyle($"-{percent}%", FloatingNumberPalette.Shield, 0.9f);
        }
    }
}
