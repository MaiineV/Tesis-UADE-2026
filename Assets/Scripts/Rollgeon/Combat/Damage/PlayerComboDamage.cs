using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combos.Play;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Qué está resolviendo la fórmula compartida de combo. Solo afecta al rótulo del
    /// logging de composición — la aritmética es idéntica para ambos.
    /// </summary>
    public enum PlayerComboFormulaKind
    {
        Damage,
        Shield,
        Heal,
        ForceDoor
    }

    /// <summary>
    /// Fórmula v3 del daño de combo del jugador (decisión de diseño 2026-08-09, N×M exacto):
    /// <code>
    /// N = daño_combo_base + dmg_base_PJ + bonos_PJ + Σ(caras contribuyentes) + bono_combo
    /// M = scratch_multiplier × ability_multiplier
    /// DAÑO = round(N × M)   (mitades siempre para arriba: 6.5 → 7)
    /// </code>
    /// Todo lo aditivo vive en N y TODO N se escala por M — a diferencia de v2, donde
    /// <c>dmg_base_PJ + bonos_PJ + bono_combo</c> quedaban fuera del producto. El multiplicador
    /// por tipo de dado (EV/3.5) desaparece: un d20 pesa más porque sus caras son más altas.
    /// <c>ability_multiplier</c> es la perilla por habilidad (ej. golpe rápido = 0.75 en
    /// <c>CH_Warrior.asset</c>); <c>scratch_multiplier</c> es el producto de los
    /// <c>ComboDamageMultiplier</c> de los 3 canales de scratch.
    /// </summary>
    /// <remarks>
    /// Código puro/estático para testear la fórmula aislada. Solo aplica al ataque de combo del
    /// jugador (DamageSource.ComboValue); los enemigos usan Constant/FromReader y no pasan por acá.
    /// <see cref="PlayerComboShield"/> delega acá con <see cref="PlayerComboFormulaKind.Shield"/>
    /// (misma fórmula, con <c>shieldBase</c> de la ShieldBaseTable como base de combo).
    /// Fix#0047: <c>comboBaseDamage</c> es SIEMPRE el base plano — los combos de base dinámica
    /// (Higher Number, SumaX, Fuerza Bruta) ya no traen sus caras dentro del base, así que
    /// Σcaras las cuenta una sola vez. La parte dinámica vive en
    /// <c>ComboDetectionResult.DynamicBonus</c> y solo la usa la formula B legacy.
    /// </remarks>
    public static class PlayerComboDamage
    {
        public static int Resolve(Guid sourceId, int comboBaseDamage,
            IReadOnlyList<ContributingDie> contributingDice, float abilityMultiplier = 1f,
            PlayerComboFormulaKind kind = PlayerComboFormulaKind.Damage)
            => Resolve(sourceId, comboBaseDamage, contributingDice, abilityMultiplier, kind, out _);

        /// <summary>
        /// Overload con desglose: mismos números que el overload simple, más el
        /// <see cref="DamageBreakdown"/> con cada término por separado para UI/logging.
        /// Lectura pura — llamarlo N veces devuelve el mismo breakdown sin side-effects.
        /// </summary>
        public static int Resolve(Guid sourceId, int comboBaseDamage,
            IReadOnlyList<ContributingDie> contributingDice, float abilityMultiplier,
            PlayerComboFormulaKind kind, out DamageBreakdown breakdown)
        {
            float dmgBasePJ = 0f;
            int bonosPJ = 0;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
            {
                var attack = attrs.GetAttribute<Attack>(sourceId);
                if (attack != null)
                {
                    dmgBasePJ = attack.Value;
                    bonosPJ = attack.ModifiedValue - attack.Value;
                }
            }

            // Items que REDEFINEN el daño base (Furia Contenida, Egoísta): reemplazan solo
            // dmg_base_PJ — bonos_PJ (los +Attack de otros items) queda intacto a propósito,
            // por eso no es un modifier Override sobre Attack. El breakdown refleja el valor
            // overrideado ⇒ el preview del HUD queda en paridad gratis (mismo Resolve).
            if (ServiceLocator.TryGetService<IBaseDamageOverrideService>(out var baseOverride)
                && baseOverride != null
                && baseOverride.TryGetBaseDamage(sourceId, out var overriddenBase))
            {
                dmgBasePJ = overriddenBase;
            }

            int bonoCombo = 0;
            float scratchMultiplier = 1f;
            bool block = false;
            List<ScratchContribution> sources = null;

            var sPassives = ServiceLocator.TryGetService<IComboPassiveService>(out var passives)
                ? passives?.LastComboScratch : null;
            if (sPassives != null)
            {
                bonoCombo += sPassives.BonusComboDamage;
                scratchMultiplier *= sPassives.ComboDamageMultiplier;
                block |= sPassives.BlockComboDamage;
                AppendJournal(ref sources, sPassives);
            }
            var sEnchants = ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants)
                ? enchants?.LastComboScratch : null;
            if (sEnchants != null)
            {
                bonoCombo += sEnchants.BonusComboDamage;
                scratchMultiplier *= sEnchants.ComboDamageMultiplier;
                block |= sEnchants.BlockComboDamage;
                AppendJournal(ref sources, sEnchants);
            }
            // Canal at-played: bonos inyectados por items/pasivas en la ventana de combo
            // jugado (ComboPlayedPayload). Se lee de LastPlayScratch (persiste más allá de
            // la ventana) porque el daño del ataque real está diferido al frame de impacto,
            // ya cerrada la ventana. Se limpia al inicio del turno, así el preview no ve
            // bonos jugados de un turno anterior.
            var sPlay = ServiceLocator.TryGetService<IComboPlayService>(out var play)
                ? play?.LastPlayScratch : null;
            if (sPlay != null)
            {
                bonoCombo += sPlay.BonusComboDamage;
                scratchMultiplier *= sPlay.ComboDamageMultiplier;
                block |= sPlay.BlockComboDamage;
                AppendJournal(ref sources, sPlay);
            }

            // Forzar Puerta: el bonus de items (Pico de Minero, stat ForceDoorRollBonus) entra
            // a N como un aditivo más — decisión de diseño 2026-09-02: antes era flat post-M y
            // quedaba fuera de la animación de breakdown. Cada modifier se journalea con su
            // ItemSO para que el vuelo muestre el icono del item.
            if (kind == PlayerComboFormulaKind.ForceDoor)
                bonoCombo += AppendForceDoorItemBonus(sourceId, ref sources);

            int facesSum = 0;
            if (contributingDice != null)
                for (int i = 0; i < contributingDice.Count; i++) facesSum += contributingDice[i].Face;

            float n = comboBaseDamage + dmgBasePJ + bonosPJ + facesSum + bonoCombo;
            // La palanca de playtest entra en m y no en n para que escale el golpe entero y no sólo
            // el término aditivo. Con PlayerDamageDebug apagado esto es ×1 y la fórmula es la real.
            float m = scratchMultiplier * abilityMultiplier * PlayerDamageDebug.Multiplier;
            int total = block ? 0 : RoundNxM(n, m);

            breakdown = new DamageBreakdown
            {
                Kind = kind,
                ComboBase = comboBaseDamage,
                AttackBase = dmgBasePJ,
                AttackBonus = bonosPJ,
                FacesSum = facesSum,
                AdditiveBonus = bonoCombo,
                N = n,
                ScratchMultiplier = scratchMultiplier,
                AbilityMultiplier = abilityMultiplier,
                M = m,
                Blocked = block,
                Final = total,
                Dice = contributingDice,
                Sources = sources,
            };

            DamageDebugLogger.LogPlayerComposition(sourceId, in breakdown);
            return total;
        }

        /// <summary>
        /// Redondeo canónico de la fórmula: mitades siempre para arriba en magnitud (6.5 → 7),
        /// nunca banker's rounding (<c>Mathf.RoundToInt</c> haría 6.5 → 6). Público para que el
        /// preview del HUD reproduzca exactamente el mismo número que el golpe real.
        /// N es float desde el canal de base damage override (Furia 0.25/ronda): este es el
        /// ÚNICO punto donde la fracción se redondea.
        /// </summary>
        public static int RoundNxM(float n, float m)
            => Math.Max(0, (int)Math.Round(n * (double)m, MidpointRounding.AwayFromZero));

        /// <summary>
        /// Suma el stat <see cref="ForceDoorRollBonus"/> del jugador y journalea cada modifier
        /// como fuente <see cref="ScratchSourceKind.Item"/>. La atribución al ItemSO se hace
        /// por <c>Modifier.SourceId == ItemPassiveSourceId.For(ItemId)</c> sobre el inventario;
        /// sin match (tests, modifiers de otra fuente) la entrada va sin asset y el builder
        /// la anima igual, sin icono. Devuelve el total aportado (0 sin stat / sin manager).
        /// </summary>
        private static int AppendForceDoorItemBonus(Guid sourceId, ref List<ScratchContribution> sources)
        {
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return 0;
            var stat = attrs.GetAttribute<ForceDoorRollBonus>(sourceId);
            if (stat == null) return 0;

            int total = stat.ModifiedValue;
            if (total == 0) return 0;

            ServiceLocator.TryGetService<Rollgeon.Items.IInventoryService>(out var inventory);
            var modifiers = stat.GetRawModifiers();
            int journaled = 0;
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    var mod = modifiers[i];
                    if (mod == null || mod.Amount == 0) continue;
                    var item = FindItemBySourceId(inventory, mod.SourceId);
                    sources ??= new List<ScratchContribution>(2);
                    sources.Add(new ScratchContribution(ScratchSourceKind.Item,
                        item != null ? item.ItemId : "ForceDoorRollBonus", item,
                        bagSlot: -1, bonusDelta: mod.Amount, multiplierFactor: 1f, setBlock: false));
                    journaled += mod.Amount;
                }
            }

            // El valor raw del stat (sin modifiers) o un modifier no-aditivo dejan un resto
            // sin journal: una entrada genérica mantiene el guion reconciliado.
            int remainder = total - journaled;
            if (remainder != 0)
            {
                sources ??= new List<ScratchContribution>(1);
                sources.Add(new ScratchContribution(ScratchSourceKind.Item, "ForceDoorRollBonus",
                    null, bagSlot: -1, bonusDelta: remainder, multiplierFactor: 1f, setBlock: false));
            }
            return total;
        }

        private static Rollgeon.Items.ItemSO FindItemBySourceId(
            Rollgeon.Items.IInventoryService inventory, Guid modifierSourceId)
        {
            if (inventory == null || modifierSourceId == Guid.Empty) return null;
            var found = FindItemIn(inventory.PassiveItems, modifierSourceId);
            return found ?? FindItemIn(inventory.ActiveItems, modifierSourceId);
        }

        private static Rollgeon.Items.ItemSO FindItemIn(
            IReadOnlyList<Rollgeon.Items.InventorySlot> slots, Guid modifierSourceId)
        {
            if (slots == null) return null;
            for (int i = 0; i < slots.Count; i++)
            {
                var item = slots[i]?.Item;
                if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                if (Rollgeon.Items.ItemPassiveSourceId.For(item.ItemId) == modifierSourceId)
                    return item;
            }
            return null;
        }

        // Agrega el journal de un canal al desglose. Aloca solo si alguna fuente aportó.
        private static void AppendJournal(ref List<ScratchContribution> sources, EnchantmentScratch scratch)
        {
            var journal = scratch.Journal;
            if (journal == null || journal.Count == 0) return;
            sources ??= new List<ScratchContribution>(journal.Count);
            for (int i = 0; i < journal.Count; i++) sources.Add(journal[i]);
        }
    }
}
