using System;
using System.Collections.Generic;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Lógica pura del HUD de pilas de fichas: diffs de pila, regla visual del
    /// oro (tope de 4 fichas + ficha inclinada) y formatos de label. Sin
    /// dependencias de UnityEngine.Object — todo testeable en EditMode.
    /// </summary>
    public static class ChipStackMath
    {
        /// <summary>Id de sprite de las fichas de vida dentro del health stack.</summary>
        public const int HealthChipId = 0;

        /// <summary>Id de sprite de las fichas de escudo (van arriba de las de vida).</summary>
        public const int ShieldChipId = 1;

        /// <summary>Fichas planas máximas de la pila de oro; de ahí en más va la inclinada.</summary>
        public const int GoldFlatChipCap = 4;

        /// <summary>Umbral desde el cual el oro muestra la ficha inclinada y solo agita.</summary>
        public const int GoldTiltedThreshold = 5;

        /// <summary>
        /// Resultado de comparar la pila mostrada contra la deseada: se conservan
        /// las primeras <see cref="KeepCount"/>, se remueven <see cref="RemoveCount"/>
        /// del tope y se agregan <see cref="AddCount"/> nuevas encima.
        /// </summary>
        public readonly struct ChipDiff
        {
            public readonly int KeepCount;
            public readonly int RemoveCount;
            public readonly int AddCount;

            public ChipDiff(int keepCount, int removeCount, int addCount)
            {
                KeepCount = keepCount;
                RemoveCount = removeCount;
                AddCount = addCount;
            }

            public bool IsEmpty => RemoveCount == 0 && AddCount == 0;
        }

        /// <summary>
        /// Diff por prefijo común: una ficha que cambió de sprite a mitad de pila
        /// (ej. vida baja y escudo sube en el mismo frame) se resuelve removiendo
        /// hasta el prefijo compartido y re-agregando.
        /// </summary>
        public static ChipDiff ComputeDiff(IReadOnlyList<int> displayed, IReadOnlyList<int> target)
        {
            if (displayed == null) throw new ArgumentNullException(nameof(displayed));
            if (target == null) throw new ArgumentNullException(nameof(target));

            int limit = Math.Min(displayed.Count, target.Count);
            int prefix = 0;
            while (prefix < limit && displayed[prefix] == target[prefix]) prefix++;

            return new ChipDiff(prefix, displayed.Count - prefix, target.Count - prefix);
        }

        /// <summary>
        /// Llena <paramref name="buffer"/> con los ids de la pila de vida: fichas de
        /// vida abajo, fichas de escudo arriba. Valores negativos clampean a 0.
        /// </summary>
        public static void BuildHealthChipIds(int hp, int shield, List<int> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            buffer.Clear();
            for (int i = 0; i < hp; i++) buffer.Add(HealthChipId);
            for (int i = 0; i < shield; i++) buffer.Add(ShieldChipId);
        }

        /// <summary>
        /// "10/10" sin escudo; con escudo el numerador pasa a hp+escudo teñido con
        /// el color de escudo de la paleta: "&lt;color=#A3B3B1&gt;12&lt;/color&gt;/10".
        /// </summary>
        public static string FormatHealthLabel(int hp, int shield, int maxHp, string shieldHex)
        {
            if (shield <= 0) return $"{hp}/{maxHp}";
            return $"<color=#{shieldHex}>{hp + shield}</color>/{maxHp}";
        }

        public static string FormatEnergyLabel(int current, int max) => $"{current}/{max}";

        public static string FormatGoldLabel(int gold) => gold.ToString();

        /// <summary>Qué muestra la pila de oro: fichas planas + si va la inclinada.</summary>
        public readonly struct GoldVisual
        {
            public readonly int FlatChips;
            public readonly bool ShowTilted;

            public GoldVisual(int flatChips, bool showTilted)
            {
                FlatChips = flatChips;
                ShowTilted = showTilted;
            }
        }

        public static GoldVisual ComputeGoldVisual(int gold)
        {
            if (gold >= GoldTiltedThreshold) return new GoldVisual(GoldFlatChipCap, true);
            return new GoldVisual(Math.Max(0, gold), false);
        }

        public enum GoldTransition
        {
            /// <summary>Sin cambio de valor — nada que animar.</summary>
            None,

            /// <summary>Cambian las fichas visibles (algún lado del cambio está bajo 5).</summary>
            ChipsChanged,

            /// <summary>Ambos valores ≥5: la pila no cambia, solo se agita.</summary>
            ShakeOnly,
        }

        public static GoldTransition ClassifyGoldTransition(int oldGold, int newGold)
        {
            if (oldGold == newGold) return GoldTransition.None;
            if (oldGold >= GoldTiltedThreshold && newGold >= GoldTiltedThreshold)
                return GoldTransition.ShakeOnly;
            return GoldTransition.ChipsChanged;
        }
    }
}
