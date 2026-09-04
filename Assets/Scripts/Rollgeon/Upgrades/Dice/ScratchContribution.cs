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

        /// <summary>Aporte ADITIVO al bono de M (ComboMultiplierBonus). 0 = neutro.</summary>
        public readonly float MultiplierBonusDelta;

        /// <summary>
        /// Delta sobre la cara del dado portador (<see cref="EnchantmentScratch.AddFaceDelta"/>).
        /// 0 = la fuente no mutó la cara. Solo tiene sentido con <see cref="BagSlot"/> ≥ 0.
        /// </summary>
        public readonly int FaceDelta;

        // multiplierBonusDelta y faceDelta van últimos y opcionales a propósito: los ctors
        // posicionales de tests y los nombrados de PlayerComboDamage siguen compilando sin tocarse.
        public ScratchContribution(ScratchSourceKind kind, string sourceId,
            UnityEngine.Object sourceAsset, int bagSlot, int bonusDelta,
            float multiplierFactor, bool setBlock, float multiplierBonusDelta = 0f,
            int faceDelta = 0)
        {
            Kind = kind;
            SourceId = sourceId;
            SourceAsset = sourceAsset;
            BagSlot = bagSlot;
            BonusDelta = bonusDelta;
            MultiplierFactor = multiplierFactor;
            SetBlock = setBlock;
            MultiplierBonusDelta = multiplierBonusDelta;
            FaceDelta = faceDelta;
        }

        public override string ToString()
            => $"{Kind}:{SourceId} (+{BonusDelta}, ×{MultiplierFactor}, +M{MultiplierBonusDelta}" +
               $"{(FaceDelta != 0 ? ", cara" + FaceDelta.ToString("+0;-0") : "")}{(SetBlock ? ", BLOCK" : "")})";
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
        public readonly float MultiplierBonus;
        public readonly bool Block;

        /// <summary>Delta de cara del slot observado antes de la fuente; 0 si la foto no mira un slot.</summary>
        public readonly int FaceDelta;

        private ScratchSnapshot(int bonus, float multiplier, float multiplierBonus, bool block, int faceDelta)
        {
            Bonus = bonus;
            Multiplier = multiplier;
            MultiplierBonus = multiplierBonus;
            Block = block;
            FaceDelta = faceDelta;
        }

        public static ScratchSnapshot Of(EnchantmentScratch s)
            => new ScratchSnapshot(s.BonusComboDamage, s.ComboDamageMultiplier,
                s.ComboMultiplierBonus, s.BlockComboDamage, faceDelta: 0);

        /// <summary>
        /// Foto que además mira la cara del dado en <paramref name="bagSlot"/>: la usa el canal
        /// dados para atribuir al journal la mutación de cara que dejó cada encantamiento.
        /// </summary>
        public static ScratchSnapshot Of(EnchantmentScratch s, int bagSlot)
            => new ScratchSnapshot(s.BonusComboDamage, s.ComboDamageMultiplier,
                s.ComboMultiplierBonus, s.BlockComboDamage, s.GetFaceDelta(bagSlot));

        public static void RecordDelta(EnchantmentScratch scratch, in ScratchSnapshot before,
            ScratchSourceKind kind, string sourceId, UnityEngine.Object sourceAsset, int bagSlot)
        {
            int bonusDelta = scratch.BonusComboDamage - before.Bonus;
            // El multiplicador compone multiplicativamente: el factor de ESTA fuente es el
            // cociente contra el estado previo (guard contra un previo 0 autorado a mano).
            float factor = Math.Abs(before.Multiplier) < 1e-6f
                ? scratch.ComboDamageMultiplier
                : scratch.ComboDamageMultiplier / before.Multiplier;
            // El bono de M compone aditivamente: delta, no absoluto (misma semántica que Bonus).
            float multBonusDelta = scratch.ComboMultiplierBonus - before.MultiplierBonus;
            bool setBlock = scratch.BlockComboDamage && !before.Block;
            // La cara solo se atribuye a fuentes por dado: una global (bagSlot -1) no la mira.
            int faceDelta = bagSlot >= 0 ? scratch.GetFaceDelta(bagSlot) - before.FaceDelta : 0;

            if (bonusDelta == 0 && Math.Abs(factor - 1f) < 1e-4f
                && Math.Abs(multBonusDelta) < 1e-4f && !setBlock && faceDelta == 0) return;

            scratch.RecordContribution(new ScratchContribution(
                kind, sourceId, sourceAsset, bagSlot, bonusDelta, factor, setBlock, multBonusDelta, faceDelta));
        }
    }
}
