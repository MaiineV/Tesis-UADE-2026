using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Skills.Push;
using Rollgeon.Combos;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Habilidad de Clase del Guerrero — Empuje. Traduce el combo de la tirada a casillas via
    /// <see cref="ClassSkillPushTableSO"/> y delega la física y los choques en
    /// <see cref="IClassSkillPushResolver"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin combo ⇒ sin efecto, y siempre <c>true</c>.</b> El roll ya se cobró al elegir
    /// objetivo (compromiso ciego); devolver <c>false</c> cortaría la fase del chain sin
    /// ganar nada. Es la única acción núcleo sin piso de dado más alto (GDD).
    /// </para>
    /// <para>
    /// Vive dentro de la fase de un <c>EffChain</c> igual que <c>EffDealDamage</c> en el Base
    /// Attack: la selección <c>BeforeRoll</c> (Occupied, Enemies, Range 1) exige la adyacencia.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffClassSkillPush : BaseEffect, IHasTooltipInfo
    {
        [Title("Push")]
        [SerializeField, Required, AssetsOnly]
        [Tooltip("Tabla combo → casillas + daño de choque de la clase.")]
        private ClassSkillPushTableSO _table;

        public ClassSkillPushTableSO Table
        {
            get => _table;
            set => _table = value;
        }

        public override string GetEffectName() => "Class Skill Push";

        /// <summary>Casillas que empuja la tirada; 0 = sin combo / sin entrada / sin tabla.</summary>
        public int ResolveDistance(ComboDetectionResult? combo)
        {
            if (_table == null) return 0;
            if (!(combo is { IsMatch: true } matched)) return 0;
            return _table.GetTiles(matched.ComboId);
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            int distance = ResolveDistance(context.ComboResult);
            if (distance <= 0)
            {
                Debug.Log("[EffClassSkillPush] Sin combo con distancia — la tirada se consume sin empuje.");
                return true;
            }

            var targets = ResolveTargetGuids(context);
            if (targets.Count == 0) return true;

            if (!ServiceLocator.TryGetService<IClassSkillPushResolver>(out var resolver) || resolver == null)
            {
                Debug.LogWarning("[EffClassSkillPush] IClassSkillPushResolver no registrado — sin empuje. " +
                                 "Agregá ClassSkillPushResolverBootstrap a ExtraServices.");
                return true;
            }

            var source = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;
            foreach (var target in targets)
                resolver.Resolve(source, target, distance, _table.CollisionDamage);

            return true;
        }

        // Mismo criterio que EffDealDamage: celdas seleccionadas → ocupante; sin selección, TargetGuid.
        private static List<Guid> ResolveTargetGuids(EffectContext context)
        {
            var result = new List<Guid>();

            if (context.SelectionResult?.SelectedTargets != null
                && ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                foreach (var target in context.SelectionResult.SelectedTargets)
                {
                    if (grid.TryGetOccupant(target.Coord, out var occupant) && occupant != Guid.Empty)
                        result.Add(occupant);
                }
            }

            if (result.Count == 0 && context.TargetGuid != Guid.Empty)
                result.Add(context.TargetGuid);

            return result;
        }

        // ---- IHasTooltipInfo ----------------------------------------------

        public string BuildTooltip()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(LocalizedContent.Ui("tooltip.effect.push.header", "Empuje: casillas según el combo"));

            if (_table != null && _table.Entries != null)
            {
                foreach (var entry in _table.Entries)
                {
                    if (string.IsNullOrEmpty(entry.ComboId) || entry.Tiles <= 0) continue;
                    sb.Append('\n').Append(ComboDisplayName(entry.ComboId)).Append(": ").Append(entry.Tiles);
                }
            }

            sb.Append('\n').Append(LocalizedContent.Ui("tooltip.effect.push.no_combo",
                "Sin combo: la tirada se pierde sin efecto"));
            return sb.ToString();
        }

        private static string ComboDisplayName(string comboId)
        {
            if (ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) && catalog != null)
            {
                var combo = catalog.GetById(comboId);
                if (combo != null && !string.IsNullOrEmpty(combo.DisplayName)) return combo.DisplayName;
            }
            return comboId;
        }
    }
}
