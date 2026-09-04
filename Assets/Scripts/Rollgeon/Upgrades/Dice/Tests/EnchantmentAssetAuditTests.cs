using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Combos.Triggers.Concretes;
using Rollgeon.Upgrades.Dice.PreConditions;
using UnityEditor;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Auditoría permanente de assets: tras la migración (Etapa 3-4), ningún
    /// EnchantmentSO / ComboPassiveSO puede referenciar triggers del namespace
    /// legacy — borrar las clases con assets sin migrar rompe silencioso por
    /// rid huérfano. GATE OBLIGATORIO antes del borrado.
    /// </summary>
    [TestFixture]
    public class EnchantmentAssetAuditTests
    {
        private const string LegacyNamespace = "Rollgeon.Upgrades.Dice.Triggers.Concretes";

        [Test]
        public void AllEnchantmentAssets_UseOnlyComposableTriggers()
        {
            var offenders = new StringBuilder();

            foreach (var guid in AssetDatabase.FindAssets("t:EnchantmentSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench?.Triggers == null) continue;

                foreach (var trigger in ench.Triggers)
                {
                    if (trigger == null)
                    {
                        offenders.AppendLine($"{path}: trigger null (¿rid huérfano de un tipo borrado?)");
                        continue;
                    }
                    if (trigger.GetType().Namespace == LegacyNamespace)
                        offenders.AppendLine($"{path}: trigger legacy {trigger.GetType().Name}");
                }
            }

            Assert.IsEmpty(offenders.ToString(), "Assets con triggers legacy sin migrar:\n" + offenders);
        }

        [Test]
        public void AllEnchantmentAssets_HaveACategoryAssigned()
        {
            // Arrange + Act — None = sin clasificar; el drawer mostraría el nombre pelado.
            var offenders = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:EnchantmentSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench != null && ench.Category == EnchantmentCategory.None)
                    offenders.AppendLine(path);
            }

            // Assert
            Assert.IsEmpty(offenders.ToString(),
                "Assets sin categoría — correr 'Rollgeon/Enchantments/Assign Categories' "
                + "(y sumar el id al diccionario si es nuevo):\n" + offenders);
        }

        /// <summary>
        /// Repro del reporte "encantamiento con combo asignado otorga en cualquier combo":
        /// carga el ASSET REAL (Ench_Avaro, oro solo con trío/poker/generala) y despacha
        /// su trigger con un combo fuera y uno dentro de la whitelist. Valida juntos la
        /// serialización del ComboFilter (Odin + SerializeReference) y el gate de RunIf.
        /// </summary>
        [Test]
        public void Ench_Avaro_RealAsset_GrantsGoldOnlyForWhitelistedCombos()
        {
            // Arrange — asset real + economía fake.
            var avaro = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(
                "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Avaro.asset");
            Assert.IsNotNull(avaro, "Ench_Avaro.asset no encontrado — ¿se movió el catálogo?");

            Triggers.ExecuteEffectsOnDiceEvent bridge = null;
            foreach (var trigger in avaro.Triggers)
            {
                if (trigger is Triggers.ExecuteEffectsOnDiceEvent b) { bridge = b; break; }
            }
            Assert.IsNotNull(bridge, "Ench_Avaro no tiene ExecuteEffectsOnDiceEvent.");
            Assert.AreEqual(Triggers.EnchantmentHookEvent.ComboPlayed, bridge.Event,
                "BUG-017: el trigger de recursos debe estar en ComboPlayed.");
            Assert.IsNotNull(bridge.Filter, "El ComboFilter deserializó null — con filtro null RunIf otorga en CUALQUIER combo.");
            Assert.AreEqual(ComboFilterMode.ComboIds, bridge.Filter.Mode,
                "El filtro deserializó en modo AnyCombo — otorgaría en cualquier combo.");
            CollectionAssert.Contains(bridge.Filter.ComboIds, "combo.trio");

            var economy = new AuditFakeEconomy();
            ServiceLocator.AddService<IEconomyService>(economy, ServiceScope.Global);
            try
            {
                // Act 1 — combo FUERA de la whitelist (par): no debe otorgar oro.
                bridge.OnComboPlayed(BuildPlayedCtx("combo.pair"));
                int goldAfterPair = economy.CurrentGold;

                // Act 2 — combo DENTRO de la whitelist (trío): otorga.
                bridge.OnComboPlayed(BuildPlayedCtx("combo.trio"));

                // Assert
                Assert.AreEqual(0, goldAfterPair,
                    "Un par NO está en la whitelist de Avaro — el oro no debe otorgarse.");
                Assert.Greater(economy.CurrentGold, 0,
                    "Un trío SÍ está en la whitelist de Avaro — el oro debe otorgarse.");
            }
            finally
            {
                ServiceLocator.Clear();
            }
        }

        /// <summary>
        /// BUG-030b: Afilado ("el mínimo es siempre la mitad del máximo redondeado
        /// arriba") tenía <c>_faceFilter</c> null y solo compensaba con un bonus de
        /// daño post-roll (<see cref="Readers.ReadCarrierRollDelta"/>) — no restringía
        /// caras. Carga el ASSET REAL y valida que ahora tenga <see cref="MinHalfMaxFilter"/>
        /// puesto y el trigger compensatorio retirado (con el filtro puesto, el delta
        /// del trigger legacy sería siempre 0).
        /// </summary>
        [Test]
        public void Ench_Afilado_RealAsset_RestrictsFacesInsteadOfCompensatingWithBonus()
        {
            var afilado = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(
                "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Afilado.asset");
            Assert.IsNotNull(afilado, "Ench_Afilado.asset no encontrado — ¿se movió el catálogo?");

            Assert.IsNotNull(afilado.FaceFilter, "Afilado debe tener un FaceFilter que restrinja caras.");
            Assert.IsInstanceOf<Filters.MinHalfMaxFilter>(afilado.FaceFilter,
                "Afilado debe usar MinHalfMaxFilter (mínimo = mitad del máximo redondeada arriba).");

            var allowed = afilado.FaceFilter.GetAllowedFaces(
                Rollgeon.Dice.DiceType.D4, new[] { 1, 2, 3, 4 });
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, allowed);

            CollectionAssert.IsEmpty(afilado.Triggers,
                "El trigger compensatorio (ExecuteEffectsOnDiceEvent/EffAddComboBonus/ReadCarrierRollDelta) " +
                "debe retirarse — con el filtro puesto el delta siempre daría 0.");
        }

        private static EnchantmentTriggerContext BuildPlayedCtx(string comboId)
        {
            return new EnchantmentTriggerContext
            {
                Effect = new EffectContext
                {
                    SourceGuid = Guid.NewGuid(),
                    DiceResult = new[] { 3, 3, 3, 2, 2 },
                    KeptDice = new[] { 3, 3, 3 },
                    KeptDiceOriginalIndices = new[] { 0, 1, 2 },
                    ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 3,
                        contributingIndices: new[] { 0, 1, 2 }),
                },
                Scratch = new EnchantmentScratch(),
                Slot = new EnchantmentSlotRef(Rollgeon.Dice.DiceType.D6, 0, 0),
                ComboId = comboId,
            };
        }

        private sealed class AuditFakeEconomy : IEconomyService
        {
            public int CurrentGold { get; private set; }
            public void Add(int amount) { if (amount > 0) CurrentGold += amount; }
            public bool Spend(int amount)
            {
                if (amount > CurrentGold) return false;
                CurrentGold -= amount;
                return true;
            }
            public bool CanAfford(int amount) => amount <= 0 || CurrentGold >= amount;
            public void ResetTo(int amount) => CurrentGold = amount;
        }

        /// <summary>
        /// Regression BUG-017: ComboMatched es preview y se re-dispara en cada toggle de
        /// hold del reroll. Un efecto de apply directo (oro, escudo…) en ese hook es
        /// farmeable infinito — los recursos van en ComboPlayed. En preview solo se
        /// admiten writers de scratch.
        /// </summary>
        [Test]
        public void EnchantmentAssets_ComboMatchedTriggers_OnlyContainScratchWriters()
        {
            var offenders = new StringBuilder();

            foreach (var guid in AssetDatabase.FindAssets("t:EnchantmentSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench?.Triggers == null) continue;

                foreach (var trigger in ench.Triggers)
                {
                    if (!(trigger is Triggers.ExecuteEffectsOnDiceEvent bridge)) continue;
                    if (bridge.Event != Triggers.EnchantmentHookEvent.ComboMatched) continue;
                    if (bridge.Effects == null) continue;

                    foreach (var group in bridge.Effects)
                    {
                        if (group?.Effects == null) continue;
                        foreach (var effect in group.Effects)
                        {
                            if (effect == null || effect is Rollgeon.Effects.IComboScratchWriter) continue;
                            offenders.AppendLine(
                                $"{path}: {effect.GetType().Name} en hook ComboMatched " +
                                "(apply directo en preview = farmeable por toggle de hold)");
                        }
                    }
                }
            }

            Assert.IsEmpty(offenders.ToString(),
                "Encantamientos con efectos de apply directo en ComboMatched:\n" + offenders);
        }

        /// <summary>
        /// Fix#0053: la transformación de la cara del carrier (Oxidado no suma, Volátil
        /// mitad/doble…) va por <c>EffMutateCarrierFace</c>. Dentro de
        /// <c>EffAddComboBonus</c> el dado sumaba su cara y un proc la deshacía — el tester lo
        /// leía como "suma y resta a la vez".
        /// </summary>
        [Test]
        public void EnchantmentAssets_DoNotAuthorFaceDeltasAsComboBonus()
        {
            var offenders = new StringBuilder();

            foreach (var guid in AssetDatabase.FindAssets("t:EnchantmentSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench?.Triggers == null) continue;

                foreach (var trigger in ench.Triggers)
                {
                    if (!(trigger is Triggers.ExecuteEffectsOnDiceEvent bridge) || bridge.Effects == null) continue;
                    foreach (var group in bridge.Effects)
                    {
                        if (group?.Effects == null) continue;
                        foreach (var effect in group.Effects)
                        {
                            if (effect is Rollgeon.Effects.Concretes.EffAddComboBonus bonus
                                && bonus.Amount is Readers.ReadCarrierRollDelta)
                                offenders.AppendLine($"{path}: EffAddComboBonus(ReadCarrierRollDelta) en {bridge.Event}");
                        }
                    }
                }
            }

            Assert.IsEmpty(offenders.ToString(),
                "Encantamientos con el canal de cara viejo (usar EffMutateCarrierFace):\n" + offenders);
        }

        /// <summary>
        /// Espejo del audit BUG-017 para el canal combos: en pasivas, el hook
        /// ComboMatched es preview (re-dispara por toggle de hold) — los efectos de
        /// apply directo (oro, escudo…) van en ComboPlayed; en preview solo writers
        /// de scratch (FlatDamageBonus vive aparte y es scratch-only).
        /// </summary>
        [Test]
        public void ComboPassiveAssets_ComboMatchedTriggers_OnlyContainScratchWriters()
        {
            var offenders = new StringBuilder();

            foreach (var guid in AssetDatabase.FindAssets("t:ComboPassiveSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var passive = AssetDatabase.LoadAssetAtPath<ComboPassiveSO>(path);
                if (passive?.ExtraTriggers == null) continue;

                foreach (var trigger in passive.ExtraTriggers)
                {
                    if (!(trigger is ExecuteEffectsOnEvent bridge)) continue;
                    if (bridge.Event != Rollgeon.Upgrades.Combos.Triggers.Concretes
                            .ComboPassiveHookEvent.ComboMatched) continue;
                    if (bridge.Effects == null) continue;

                    foreach (var group in bridge.Effects)
                    {
                        if (group?.Effects == null) continue;
                        foreach (var effect in group.Effects)
                        {
                            if (effect == null || effect is Rollgeon.Effects.IComboScratchWriter) continue;
                            offenders.AppendLine(
                                $"{path}: {effect.GetType().Name} en hook ComboMatched " +
                                "(apply directo en preview = farmeable por toggle de hold)");
                        }
                    }
                }
            }

            Assert.IsEmpty(offenders.ToString(),
                "Pasivas con efectos de apply directo en ComboMatched:\n" + offenders);
        }

        /// <summary>
        /// Regresión del bug "encantamientos condicionales afectan la tirada entera aunque
        /// el dado no participe": un trigger ComboMatched/ComboPlayed que gatea un efecto
        /// con <see cref="PcCarrierFace"/> (Resonante, Gemelo, Fragil, ParityGamble — todas
        /// dependen de qué dados formaron el combo) DEBE tener
        /// <c>RequireCarrierParticipates = true</c>, o el gate del carrier nunca se
        /// verifica contra el combo real y el efecto se evalúa sobre la tirada completa.
        /// Ver <c>Rollgeon.EditorTools.Upgrades.EnchantmentCarrierFlagFixer</c> (Editor,
        /// menú "Rollgeon/Upgrades/Fix Carrier Participation Flags") para el fix por asset.
        /// </summary>
        [Test]
        public void EnchantmentAssets_WithCarrierFacePrecondition_RequireCarrierParticipates()
        {
            var offenders = new StringBuilder();

            foreach (var guid in AssetDatabase.FindAssets("t:EnchantmentSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench?.Triggers == null) continue;

                foreach (var trigger in ench.Triggers)
                {
                    if (trigger is not Triggers.ExecuteEffectsOnDiceEvent bridge) continue;
                    if (bridge.Event != Triggers.EnchantmentHookEvent.ComboMatched
                        && bridge.Event != Triggers.EnchantmentHookEvent.ComboPlayed) continue;
                    if (bridge.RequireCarrierParticipates) continue;
                    if (bridge.Effects == null) continue;

                    bool usesCarrierFace = false;
                    foreach (var group in bridge.Effects)
                    {
                        if (group?.PreConditions == null) continue;
                        foreach (var pc in group.PreConditions)
                        {
                            if (pc is PcCarrierFace) usesCarrierFace = true;
                        }
                    }

                    if (usesCarrierFace)
                        offenders.AppendLine(
                            $"{path}: usa PcCarrierFace pero RequireCarrierParticipates=false " +
                            "(el gate del carrier no filtra por combo real).");
                }
            }

            Assert.IsEmpty(offenders.ToString(),
                "Encantamientos con PcCarrierFace sin RequireCarrierParticipates:\n" + offenders);
        }

        [Test]
        public void AllComboPassiveAssets_UseOnlyExecuteEffectsBridges()
        {
            var offenders = new StringBuilder();

            foreach (var guid in AssetDatabase.FindAssets("t:ComboPassiveSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var passive = AssetDatabase.LoadAssetAtPath<ComboPassiveSO>(path);
                var triggers = passive?.ExtraTriggers;
                if (triggers == null) continue;

                foreach (var trigger in triggers)
                {
                    if (trigger == null)
                    {
                        offenders.AppendLine($"{path}: trigger null (¿rid huérfano?)");
                        continue;
                    }
                    if (!(trigger is ExecuteEffectsOnEvent))
                        offenders.AppendLine($"{path}: trigger legacy {trigger.GetType().Name}");
                }
            }

            Assert.IsEmpty(offenders.ToString(), "Pasivas con triggers legacy sin migrar:\n" + offenders);
        }
    }
}
