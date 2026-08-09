using System;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>Qué clase de fuente escribió al scratch.</summary>
    public enum ScratchSourceKind
    {
        Enchantment,
        ComboPassive,
        Item,
    }

    /// <summary>
    /// Una entrada del journal de atribución: qué fuente aportó cuánto al combo del scratch.
    /// La UI de breakdown la consume para animar "de dónde salió cada número"; sin consumidor
    /// el costo es una lista lazy que ni se aloca.
    /// </summary>
    public readonly struct ScratchContribution
    {
        public readonly ScratchSourceKind Kind;

        /// <summary>Id estable de la fuente (ItemId / nombre del SO).</summary>
        public readonly string SourceId;

        /// <summary>
        /// El SO fuente (ItemSO / EnchantmentSO / ComboPassiveSO) para que la UI resuelva
        /// icono y nombre. Tipado como Object para no acoplar el scratch a esos tipos.
        /// </summary>
        public readonly UnityEngine.Object SourceAsset;

        /// <summary>Bag slot del dado portador (encantamientos); -1 para fuentes globales.</summary>
        public readonly int BagSlot;

        /// <summary>Aporte aditivo a N (BonusComboDamage). 0 = no aportó por este canal.</summary>
        public readonly int BonusDelta;

        /// <summary>Factor multiplicativo aportado a M. 1 = neutro.</summary>
        public readonly float MultiplierFactor;

        /// <summary>La fuente activó el bloqueo total del combo.</summary>
        public readonly bool SetBlock;

        public ScratchContribution(ScratchSourceKind kind, string sourceId,
            UnityEngine.Object sourceAsset, int bagSlot, int bonusDelta,
            float multiplierFactor, bool setBlock)
        {
            Kind = kind;
            SourceId = sourceId;
            SourceAsset = sourceAsset;
            BagSlot = bagSlot;
            BonusDelta = bonusDelta;
            MultiplierFactor = multiplierFactor;
            SetBlock = setBlock;
        }

        public override string ToString()
            => $"{Kind}:{SourceId} (+{BonusDelta}, ×{MultiplierFactor}{(SetBlock ? ", BLOCK" : "")})";
    }

    /// <summary>
    /// Foto de los campos de combo del scratch para medir el delta que dejó una fuente:
    /// <c>Of</c> antes de despachar sus efectos, <c>RecordDelta</c> después. Registra solo
    /// si algo cambió — fuentes neutras no generan entradas ni alocaciones.
    /// </summary>
    public readonly struct ScratchSnapshot
    {
        public readonly int Bonus;
        public readonly float Multiplier;
        public readonly bool Block;

        private ScratchSnapshot(int bonus, float multiplier, bool block)
        {
            Bonus = bonus;
            Multiplier = multiplier;
            Block = block;
        }

        public static ScratchSnapshot Of(EnchantmentScratch s)
            => new ScratchSnapshot(s.BonusComboDamage, s.ComboDamageMultiplier, s.BlockComboDamage);

        public static void RecordDelta(EnchantmentScratch scratch, in ScratchSnapshot before,
            ScratchSourceKind kind, string sourceId, UnityEngine.Object sourceAsset, int bagSlot)
        {
            int bonusDelta = scratch.BonusComboDamage - before.Bonus;
            // El multiplicador compone multiplicativamente: el factor de ESTA fuente es el
            // cociente contra el estado previo (guard contra un previo 0 autorado a mano).
            float factor = Math.Abs(before.Multiplier) < 1e-6f
                ? scratch.ComboDamageMultiplier
                : scratch.ComboDamageMultiplier / before.Multiplier;
            bool setBlock = scratch.BlockComboDamage && !before.Block;

            if (bonusDelta == 0 && Math.Abs(factor - 1f) < 1e-4f && !setBlock) return;

            scratch.RecordContribution(new ScratchContribution(
                kind, sourceId, sourceAsset, bagSlot, bonusDelta, factor, setBlock));
        }
    }
}
