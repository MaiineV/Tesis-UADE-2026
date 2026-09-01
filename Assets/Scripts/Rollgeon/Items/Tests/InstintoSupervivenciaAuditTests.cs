using System.Text;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using UnityEditor;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Auditoría de datos del arreglo "Instinto de Supervivencia ES la pasiva del
    /// Warrior": el item usa el mismo <see cref="EffLowHpAttackBuff"/> con los mismos
    /// números que <c>CP_Warrior</c>, el Warrior lo recibe como starting item (así el
    /// gate <c>HasItem</c> lo saca de las pools) y conserva el innate id como cinturón.
    /// Si GD retunea uno de los dos assets, estos tests fuerzan a sincronizar el otro.
    /// </summary>
    [TestFixture]
    public class InstintoSupervivenciaAuditTests
    {
        private const string ItemPath = "Assets/Rollgeon/Items/Item_InstintoSupervivencia.asset";
        private const string PassivePath = "Assets/Rollgeon/Classes/CP_Warrior.asset";
        private const string ItemId = "instinto.supervivencia";

        private static readonly string[] WarriorHeroPaths =
        {
            "Assets/Rollgeon/Classes/CH_Warrior.asset",
            "Assets/Rollgeon/Tutorial/CH_Warrior_Tutorial.asset",
        };

        [Test]
        public void ItemAsset_UsesLowHpAttackBuff_WithWarriorNumbers()
        {
            // Arrange + Act
            var buff = FindItemBuff(out var item);

            // Assert
            Assert.IsNotNull(item, "No se encontró " + ItemPath);
            Assert.AreEqual(1, item.PassiveHooks.Count, "El item debe tener exactamente un hook");
            Assert.AreEqual(PassiveHookKind.EventBus, item.PassiveHooks[0].Kind);
            Assert.AreEqual(EventName.OnAttributeChanged, item.PassiveHooks[0].TriggerEvent);
            Assert.AreEqual(PassiveHookSubject.Source, item.PassiveHooks[0].Subject);
            Assert.IsNotNull(buff, "El hook debe contener un EffLowHpAttackBuff");
            Assert.AreEqual(30, buff.HpThreshold);
            Assert.AreEqual(5, buff.AttackBonus);
        }

        [Test]
        public void ItemNumbers_MatchWarriorClassPassive()
        {
            // Arrange
            var itemBuff = FindItemBuff(out _);
            var passiveBuff = FindClassPassiveBuff();

            // Assert — paridad: retunear uno sin el otro desincroniza pasiva e item.
            Assert.IsNotNull(itemBuff, "Item sin EffLowHpAttackBuff");
            Assert.IsNotNull(passiveBuff, "CP_Warrior sin EffLowHpAttackBuff");
            Assert.AreEqual(passiveBuff.HpThreshold, itemBuff.HpThreshold,
                "Umbral del item != umbral de la pasiva de clase");
            Assert.AreEqual(passiveBuff.AttackBonus, itemBuff.AttackBonus,
                "Bonus del item != bonus de la pasiva de clase");
        }

        [Test]
        public void WarriorHeroes_GrantInstintoAsStartingItem_AndKeepInnateId()
        {
            var problems = new StringBuilder();

            foreach (var path in WarriorHeroPaths)
            {
                var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(path);
                if (hero == null)
                {
                    problems.AppendLine(path + ": asset no encontrado");
                    continue;
                }

                bool starting = false;
                foreach (var item in hero.StartingItems)
                    if (item != null && item.ItemId == ItemId) starting = true;
                if (!starting) problems.AppendLine(path + ": Instinto falta en StartingItems");

                if (!hero.InnateItemIds.Contains(ItemId))
                    problems.AppendLine(path + ": Instinto falta en InnateItemIds (cinturón del gate)");
            }

            Assert.IsEmpty(problems.ToString(), problems.ToString());
        }

        [Test]
        public void ItemAsset_RemainsUniquePerRun()
        {
            // Arrange + Act
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(ItemPath);

            // Assert
            Assert.IsNotNull(item, "No se encontró " + ItemPath);
            Assert.IsTrue(item.UniquePerRun,
                "Sin UniquePerRun el gate no lo saca de las pools aunque el Warrior lo posea");
        }

        private static EffLowHpAttackBuff FindItemBuff(out ItemSO item)
        {
            item = AssetDatabase.LoadAssetAtPath<ItemSO>(ItemPath);
            if (item == null) return null;

            foreach (var hook in item.PassiveHooks)
            {
                if (hook?.Effect?.Effects == null) continue;
                foreach (var effect in hook.Effect.Effects)
                    if (effect is EffLowHpAttackBuff buff) return buff;
            }
            return null;
        }

        private static EffLowHpAttackBuff FindClassPassiveBuff()
        {
            var passive = AssetDatabase.LoadAssetAtPath<ClassPassiveSO>(PassivePath);
            if (passive == null) return null;

            foreach (var hook in passive.Hooks)
            {
                if (hook?.Effect?.Effects == null) continue;
                foreach (var effect in hook.Effect.Effects)
                    if (effect is EffLowHpAttackBuff buff) return buff;
            }
            return null;
        }
    }
}
