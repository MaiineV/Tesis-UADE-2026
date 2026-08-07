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

        /// <summary>
        /// Desglosa la fórmula v2 compartida del combo del jugador (ver <see cref="PlayerComboDamage"/>):
        /// <c>dmg_base_PJ + bonos_PJ + (comboBase × multi_dmg × ability × scratch) + bono_combo</c>.
        /// <paramref name="kind"/> distingue si el resultado es daño o escudo (Spec Escudo v3).
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        public static void LogPlayerComposition(
            Guid sourceId, int dmgBasePJ, int bonosPJ, int comboBaseDamage,
            float multiDmgCombo, float abilityMultiplier, float scratchMultiplier,
            int bonoCombo, bool blocked, int finalBase,
            PlayerComboFormulaKind kind = PlayerComboFormulaKind.Damage)
        {
            if (!Enabled) return;

            bool isShield = kind == PlayerComboFormulaKind.Shield;
            var sb = new StringBuilder(640);
            sb.Append(isShield
                ? Band(ShieldCol, "SHIELD · COMPOSICIÓN — escudo base del PLAYER")
                : Band(BandCompose, "DMG · COMPOSICIÓN — daño base del PLAYER"));
            sb.Append("  ").Append(Col(Label, "src=" + Short(sourceId)));

            if (blocked)
            {
                sb.Append('\n').Append(Col(BandApply,
                    $"  ⛔ BLOQUEADO por scratch (BlockComboDamage) → {(isShield ? "escudo" : "daño")} base = 0"));
                Debug.Log(sb.ToString());
                return;
            }

            float comboTerm = comboBaseDamage * multiDmgCombo * abilityMultiplier * scratchMultiplier;

            // Términos aditivos puros (nunca se multiplican).
            sb.Append(Row("dmg_base_PJ  (ATQ.Value)", dmgBasePJ));
            sb.Append(Row("+ bonos_PJ   (ATQ.Modified − Value)", bonosPJ));

            // Único término que escala.
            sb.Append('\n').Append(Col(Label, "  término_combo = comboBase × multi_dmg × ability × scratch"));
            sb.Append('\n').Append(Col(Label, "    = "))
              .Append(Num(comboBaseDamage)).Append(Col(Label, " × "))
              .Append(Num(multiDmgCombo)).Append(Col(Label, " (multi_dmg) × "))
              .Append(Num(abilityMultiplier)).Append(Col(Label, " (ability) × "))
              .Append(Num(scratchMultiplier)).Append(Col(Label, " (scratch) = "))
              .Append(Col(Accent, "<b>" + F(comboTerm) + "</b>"));

            sb.Append(Row("+ bono_combo (passives + enchants)", bonoCombo));

            sb.Append('\n').Append(Col(isShield ? ShieldCol : BandCompose, "  ═ TOTAL BASE = "))
              .Append(Col(Label, $"{dmgBasePJ} + {bonosPJ} + {F(comboTerm)} + {bonoCombo} = "))
              .Append(Col(Accent, "<b>" + finalBase + "</b>"))
              .Append(Col(Label, isShield
                  ? "   → se suma al atributo Shield (EffAddShield)"
                  : "   → entra al DamagePipeline como BaseDamage"));

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
