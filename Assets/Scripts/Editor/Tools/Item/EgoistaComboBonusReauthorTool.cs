using System.Collections.Generic;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Items;
using Rollgeon.PreConditions;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Readers;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Items
{
    /// <summary>
    /// BUG-080: re-autora <c>Item_Egoista.asset</c> por código. Diseño viejo: hook
    /// <c>EventBus</c>/<c>OnDamageOutgoing</c> con <see cref="EffModifyIntAttribute"/>
    /// que sumaba TODO el oro actual al Attack BASE de forma <b>permanente</b> en cada
    /// golpe (<c>SetAttributeValue</c>) y, al dispararse post-cálculo de daño, llegaba
    /// siempre un ataque tarde.
    /// <para>
    /// Diseño nuevo: hook <c>ComboPlayed</c> restringido a <see cref="RollActionKind.Attack"/>
    /// (BUG-060 — el bono de daño de un ítem no debe leakear a Heal/Movement, que
    /// comparten el mismo play scratch) con <see cref="EffAddComboBonus"/> alimentado por
    /// <see cref="ReadCurrentGoldSqrtScaled"/>: <c>bono = floor(sqrt(oro_actual × 5))</c>,
    /// de solo lectura, calculado al momento del golpe, sin mutar ningún atributo.
    /// </para>
    /// <para>
    /// Por tool y no editando el YAML a mano: <c>ItemSO</c> es un
    /// <c>SerializedScriptableObject</c> (Odin) — el stream de <c>SerializationNodes</c>
    /// renumera índices de tipo por orden de aparición y un edit manual lo desincroniza
    /// en silencio (mismo gotcha que <c>AfiladoFaceFilterFixTool</c>). Acá se arma el
    /// hook en memoria y se deja que Odin re-serialice al guardar. Idempotente — pisa
    /// <c>PassiveHooks</c> completo con el diseño nuevo en cada corrida.
    /// </para>
    /// </summary>
    public static class EgoistaComboBonusReauthorTool
    {
        private const string AssetPath = "Assets/Rollgeon/Items/Item_Egoista.asset";

        [MenuItem("Rollgeon/Items/Re-author Egoista Combo Bonus (BUG-080)")]
        public static void Reauthor()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetPath);
            if (item == null)
            {
                Debug.LogError($"[EgoistaComboBonusReauthorTool] No se encontró {AssetPath}");
                return;
            }

            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ComboFilter = new ComboFilter { Mode = ComboFilterMode.AnyCombo },
                ActionKindFilter = RollActionKind.Attack,
                Effect = new EffectData
                {
                    Label = "Effect Group",
                    PreConditions = new List<BasePreCondition>(),
                    Effects = new List<IEffect>
                    {
                        new EffAddComboBonus
                        {
                            Amount = new ReadCurrentGoldSqrtScaled { Factor = 5f },
                        },
                    },
                    TargetSelector = null,
                },
                PersistentModifiers = new List<PersistentModifierDef>(),
            };

            item.PassiveHooks = new List<PassiveItemHook> { hook };

            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[EgoistaComboBonusReauthorTool] Item_Egoista re-autorado: " +
                      "ComboPlayed/Attack + EffAddComboBonus(ReadCurrentGoldSqrtScaled factor=5).");
        }
    }
}
