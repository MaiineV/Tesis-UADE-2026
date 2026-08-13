using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combos;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si la mano de dados que el owner tiene sobre la mesa
    /// (<see cref="IBossDiceHandService"/>) matchea el combo pedido. Es el gate de las ramas de
    /// ataque de La Generala: el combo que le salió <b>es</b> el ataque, así que el árbol ramifica
    /// leyendo su propia tirada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Veta cuando no hay mano.</b> Sin servicio, sin mano publicada, o con la mano solo
    /// <i>cantada</i> (ver <see cref="RequireArmed"/>), devuelve false: el turno de la ronda extra de
    /// aviso no tiene que marcar nada, y el Selector que agrupa las ramas cae a su fallback.
    /// </para>
    /// <para>
    /// El servicio lo crea <c>AINode_RollHand</c> (lazy self-register), así que para cuando esta PC
    /// corre — después de tirar, en el mismo turno — ya existe.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcBossHandCombo : BasePreCondition
    {
        /// <summary>Qué se compara contra la mano.</summary>
        public enum HandMatch
        {
            /// <summary>El combo de la mano es exactamente <see cref="ComboId"/>.</summary>
            Combo,

            /// <summary>La mano no formó ningún combo (bust).</summary>
            NoCombo,

            /// <summary>La mano formó algún combo, cualquiera.</summary>
            AnyCombo,
        }

        [Tooltip("Combo = compara contra ComboId. NoCombo = la tirada salió bust. AnyCombo = cualquier combo.")]
        public HandMatch Match = HandMatch.Combo;

        [ValueDropdown(nameof(GetComboIds))]
        [ShowIf(nameof(Match), HandMatch.Combo)]
        [Tooltip("ComboId a matchear. Se alimenta del ComboCatalogSO — los ids no se tipean a mano.")]
        public string ComboId = Rollgeon.Combos.ComboId.Generala;

        [Tooltip("Si true, la mano solo cuenta cuando ya está armada. Dejalo en true: sin esto, la " +
                 "ronda extra de aviso de la mano grande marcaría igual y se perdería el aviso.")]
        public bool RequireArmed = true;

        public override string ConditionName => Match switch
        {
            HandMatch.NoCombo => "Boss hand: bust",
            HandMatch.AnyCombo => "Boss hand: any combo",
            _ => $"Boss hand: {ComboId}",
        };

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IBossDiceHandService>(out var hands) || hands == null) return false;
            if (!hands.TryGetHand(context.OwnerGuid, out var hand)) return false;
            if (RequireArmed && !hand.Armed) return false;

            switch (Match)
            {
                case HandMatch.NoCombo: return !hand.HasCombo;
                case HandMatch.AnyCombo: return hand.HasCombo;
                case HandMatch.Combo:
                    return hand.HasCombo && !string.IsNullOrEmpty(ComboId)
                           && string.Equals(hand.ComboId, ComboId, StringComparison.Ordinal);
                default: return false;
            }
        }

        // ---- Odin dropdown source (mismo patrón que EnemyDataSO.GetComboIds) ----

        private static IEnumerable<string> GetComboIds()
        {
            if (Application.isPlaying)
            {
                if (ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) && catalog != null)
                    return catalog.AllIds;
                return Array.Empty<string>();
            }

#if UNITY_EDITOR
            var ids = new SortedSet<string>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:BaseComboSO"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseComboSO>(path);
                if (asset != null && !string.IsNullOrEmpty(asset.ComboId))
                    ids.Add(asset.ComboId);
            }
            return ids;
#else
            return Array.Empty<string>();
#endif
        }
    }
}
