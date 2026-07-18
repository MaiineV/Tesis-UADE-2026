using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Combos.Triggers.Concretes;
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
