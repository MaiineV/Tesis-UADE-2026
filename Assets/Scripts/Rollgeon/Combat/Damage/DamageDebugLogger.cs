using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Rollgeon.Combat.Pipelines;
using Debug = UnityEngine.Debug;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Logger de diagnóstico que desglosa <b>cómo se compone</b> y <b>cómo se aplica</b> el daño,
    /// término por término, para poder auditar la fórmula en runtime. Sigue el patrón de
    /// <see cref="Patterns.FSM.FSMLogger"/>: marcado con <see cref="ConditionalAttribute"/> para
    /// que las invocaciones se eliminen del IL en builds de release (sin <c>UNITY_EDITOR</c> ni
    /// <c>DEVELOPMENT_BUILD</c>) → overhead cero fuera del editor.
    /// </summary>
    /// <remarks>
    /// La consola de Unity (IMGUI) no soporta color de fondo real — no hay etiqueta <c>&lt;mark&gt;</c>.
    /// Se emula una banda de "fondo" con bloques █ coloreados sobre <c>&lt;color&gt;</c>/<c>&lt;b&gt;</c>,
    /// que es lo más cercano a un highlight que la consola permite.
    /// </remarks>
    public static class DamageDebugLogger
    {
        /// <summary>
        /// Toggle en runtime — apagalo desde código/consola si el desglose ensucia demasiado,
        /// sin necesidad de recompilar. Las llamadas ya salen compiladas-fuera en release.
        /// </summary>
        public static bool Enabled = true;

        // Paleta de las bandas y acentos (hex sin '#', se completan al formatear).
        private const string BandCompose = "ff8c1a"; // ámbar — composición del daño base del PJ
        private const string BandApply = "e6392e";   // rojo  — aplicación (weakness/escudo/HP)
        private const string Label = "9aa0a6";       // gris  — etiquetas
        private const string Accent = "ffd166";      // amarillo — números destacados
        private const string WeakCol = "c678dd";     // violeta — weakness
        private const string ShieldCol = "56b6c2";   // cyan — escudo
        private const string HealCol = "5fd068";     // verde — curación
        private const string DoorCol = "d19a66";     // ocre — check de forzar puerta

        /// <summary>
        /// Desglosa la fórmula v3 compartida del combo del jugador desde el
        /// <see cref="DamageBreakdown"/> que produjo <see cref="PlayerComboDamage.Resolve"/> —
        /// el log muestra los MISMOS términos que la fórmula computó, nunca una copia.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogPlayerComposition(Guid sourceId, in DamageBreakdown b)
        {
            if (!Enabled) return;

            string kindCol = b.Kind switch
            {
                PlayerComboFormulaKind.Shield => ShieldCol,
                PlayerComboFormulaKind.Heal => HealCol,
                PlayerComboFormulaKind.ForceDoor => DoorCol,
                _ => BandCompose,
            };
            string kindNoun = b.Kind switch
            {
                PlayerComboFormulaKind.Shield => "escudo",
                PlayerComboFormulaKind.Heal => "curación",
                PlayerComboFormulaKind.ForceDoor => "check",
                _ => "daño",
            };
            var sb = new StringBuilder(640);
            sb.Append(b.Kind switch
            {
                PlayerComboFormulaKind.Shield => Band(ShieldCol, "SHIELD · COMPOSICIÓN — escudo base del PLAYER"),
                PlayerComboFormulaKind.Heal => Band(HealCol, "HEAL · COMPOSICIÓN — curación base del PLAYER"),
                PlayerComboFormulaKind.ForceDoor => Band(DoorCol, "FORCE DOOR · COMPOSICIÓN — check del PLAYER"),
                _ => Band(BandCompose, "DMG · COMPOSICIÓN — daño base del PLAYER"),
            });
            sb.Append("  ").Append(Col(Label, "src=" + Short(sourceId)));

            if (b.Blocked)
            {
                sb.Append('\n').Append(Col(BandApply,
                    $"  ⛔ BLOQUEADO por scratch (BlockComboDamage) → {kindNoun} base = 0"));
                Debug.Log(sb.ToString());
                return;
            }

            // Términos de N — todo lo aditivo escala junto.
            sb.Append(Row("combo_base", b.ComboBase));
            sb.Append(Row("+ dmg_base_PJ  (ATQ.Value)", b.AttackBase));
            sb.Append(Row("+ bonos_PJ     (ATQ.Modified − Value)", b.AttackBonus));
            sb.Append(Row("+ Σ caras contribuyentes", b.FacesSum));
            sb.Append(Row("+ bono_combo   (passives + enchants + items)", b.AdditiveBonus));
            sb.Append('\n').Append(Col(Label, "  N = ")).Append(Col(Accent, "<b>" + b.N + "</b>"));

            // Términos de M.
            sb.Append('\n').Append(Col(Label, "  M = "))
              .Append(Num(b.ScratchMultiplier)).Append(Col(Label, " (scratch) × "))
              .Append(Num(b.AbilityMultiplier)).Append(Col(Label, " (ability) = "))
              .Append(Col(Accent, "<b>" + F(b.M) + "</b>"));

            // Una palanca de playtest olvidada prendida convierte cada número medido de acá en
            // adelante en mentira, y no se nota mirando el resultado. Se canta en cada golpe.
            if (PlayerDamageDebug.IsOn)
            {
                sb.Append('\n').Append(Col(WeakCol,
                    $"  ⚠ PlayerDamageDebug ×{F(PlayerDamageDebug.Multiplier)} PRENDIDO — " +
                    "este daño NO es el real. PlayerDamageDebug.Off() para apagarlo."));
            }

            sb.Append('\n').Append(Col(kindCol, "  ═ TOTAL = N × M = "))
              .Append(Col(Label, $"{b.N} × {F(b.M)} = "))
              .Append(Col(Accent, "<b>" + b.Final + "</b>"))
              .Append(Col(Label, b.Kind switch
              {
                  PlayerComboFormulaKind.Shield => "   → se suma al atributo Shield (EffAddShield)",
                  PlayerComboFormulaKind.Heal => "   → entra al HealPipeline como BaseHeal",
                  PlayerComboFormulaKind.ForceDoor => "   → se compara contra el threshold de la puerta",
                  _ => "   → entra al DamagePipeline como BaseDamage",
              }));

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Desglosa la aplicación del daño en el <see cref="DamagePipeline"/>: multiplicador de
        /// weakness, absorción de escudo, write a Health y letalidad. Cubre TODO el daño (player y
        /// enemigos); <see cref="AttackKind"/> permite distinguir la fuente.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogApplication(
            DamageContext ctx, int shieldBefore, int hpBefore, int hpAfter)
        {
            if (!Enabled || ctx == null) return;

            var sb = new StringBuilder(640);
            sb.Append(Band(BandApply, "DMG · APLICACIÓN — DamagePipeline"));
            sb.Append("  ").Append(Col(Label,
                $"{ctx.Kind}  src={Short(ctx.SourceId)} → tgt={Short(ctx.TargetId)}"));

            sb.Append(Row("BaseDamage (entrante)", ctx.BaseDamage));

            // Etapa weakness.
            bool weakActive = ctx.WeaknessMultiplier > 1f;
            sb.Append('\n').Append(Col(Label, "  × weakness = "))
              .Append(Col(weakActive ? WeakCol : Label, "<b>" + F(ctx.WeaknessMultiplier) + "</b>"))
              .Append(Col(Label, weakActive ? "  (¡debilidad!)" : "  (sin debilidad)"));

            // Etapa escudo.
            if (ctx.ShieldAbsorbed > 0)
            {
                sb.Append('\n').Append(Col(Label, "  escudo: "))
                  .Append(Col(ShieldCol, Short(ctx.TargetId) + " tenía " + shieldBefore))
                  .Append(Col(Label, ", absorbe "))
                  .Append(Col(ShieldCol, "<b>" + ctx.ShieldAbsorbed + "</b>"))
                  .Append(Col(Label, ctx.BlockedByShield ? "  → golpe totalmente bloqueado" : ""));
            }

            // HP resultante.
            if (hpBefore >= 0)
            {
                sb.Append('\n').Append(Col(Label, "  HP: "))
                  .Append(Num(hpBefore)).Append(Col(Label, " → "))
                  .Append(Col(Accent, "<b>" + hpAfter + "</b>"));
            }

            sb.Append('\n').Append(Col(BandApply, "  ═ FinalDamage = "))
              .Append(Col(Accent, "<b>" + ctx.FinalDamage + "</b>"))
              .Append(Col(Label, ctx.WasLethal ? "   ☠ LETAL" : ""));

            Debug.Log(sb.ToString());
        }

        // ── Helpers de formato ────────────────────────────────────────────────
        // Banda de "fondo" emulada con bloques (la consola no soporta background real).
        private static string Band(string hex, string text)
            => $"<color=#{hex}><b>████  {text}  ████</b></color>";

        private static string Col(string hex, string text) => $"<color=#{hex}>{text}</color>";

        private static string Row(string label, int value)
            => "\n" + Col(Label, "  " + label + ": ") + "<b>" + value + "</b>";

        private static string Num(float v) => Col(Accent, F(v));

        private static string F(float v)
            => v.ToString(Math.Abs(v - Math.Truncate(v)) < 0.0001f ? "0" : "0.###",
                CultureInfo.InvariantCulture);

        private static string Short(Guid g)
            => g == Guid.Empty ? "∅" : g.ToString("N").Substring(0, 6);
    }
}
