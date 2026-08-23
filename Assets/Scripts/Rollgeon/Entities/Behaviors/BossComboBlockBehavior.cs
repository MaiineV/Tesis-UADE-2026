using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.ComboBlock;
using Rollgeon.Combos;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using Rollgeon.Heroes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// <see cref="_bossTurnCounter"/> es un campo <c>[NonSerialized]</c> — persiste en la instancia
    /// clonada via <c>SerializationUtility.CreateCopy</c> (§7.2). El contador vive mientras el boss
    /// vive; al morir, el clone se descarta.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class BossComboBlockBehavior : BaseBehavior
    {
        public override string BehaviorName => "Boss Combo Block";

        /// <summary>Si null al <c>Execute</c> el behavior intenta resolver desde la entity; si tampoco, warn + return.</summary>
        [Tooltip("Override opcional del BossFloorManagerSO con los tuning values. Si null, se " +
                 "resuelve por default desde el SO de la entidad owner (plan §4.3).")]
        public BossFloorManagerSO BossDataOverride;

        [NonSerialized]
        private int _bossTurnCounter;

        /// <summary>Inyectado por el spawner (runtime) o por el test setup. Si null, el behavior loguea warning y no bloquea.</summary>
        [NonSerialized]
        public Func<ContractSheet> SheetResolver;

        /// <summary>Default <c>UnityEngine.Random.Range</c> via wrapper. Inyectable en tests para determinismo.</summary>
        [NonSerialized]
        public Func<int, int> RandomSource; // takes exclusive upper bound.

        public override void Execute(BehaviorContext ctx)
        {
            if (ctx == null || ctx.SourceEntity == null) return;

            var bossSO = ResolveBossDataSO(ctx);
            if (bossSO == null)
            {
                Debug.LogWarning(
                    "[BossComboBlockBehavior] BossFloorManagerSO no resuelto (ni override ni via SourceEntity). " +
                    "Asigna BossDataOverride en el Inspector o spawnea el boss con un BossFloorManagerSO.");
                return;
            }

            _bossTurnCounter++;

            if (_bossTurnCounter % bossSO.ComboBlockIntervalTurns != 0) return;

            var sheet = ResolveSheet();
            if (sheet == null)
            {
                Debug.LogWarning(
                    "[BossComboBlockBehavior] No se pudo resolver el ContractSheet del jugador " +
                    "(SheetResolver null o devolvio null). Skipping block.");
                return;
            }

            if (!ServiceLocator.TryGetService<IComboBlockService>(out var block) || block == null)
            {
                Debug.LogError(
                    "[BossComboBlockBehavior] IComboBlockService no esta registrado en ServiceLocator. " +
                    "Agrega ComboBlockServiceBootstrap a ServiceBootstrapSO.ExtraServices.");
                return;
            }

            var candidates = new List<BaseComboSO>();
            if (sheet.Combos != null)
            {
                foreach (var combo in sheet.Combos)
                {
                    if (combo == null) continue;
                    if (string.IsNullOrEmpty(combo.ComboId)) continue;
                    if (block.IsBlocked(combo.ComboId)) continue;
                    if (sheet.IsCrossed(combo)) continue;
                    candidates.Add(combo);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.Log(
                    "[BossComboBlockBehavior] No hay combos disponibles para bloquear " +
                    "(todos bloqueados o tachados). Skipping this turn; counter NOT reset.");
                return;
            }

            int index = RandomSource != null
                ? RandomSource(candidates.Count)
                : UnityEngine.Random.Range(0, candidates.Count);
            if (index < 0) index = 0;
            if (index >= candidates.Count) index = candidates.Count - 1;

            var pick = candidates[index];
            block.Block(pick.ComboId, bossSO.ComboBlockDurationTurns);
        }

        /// <summary>Test-friendly accessor — expone el counter sin exponer el campo.</summary>
        public int DebugTurnCounter => _bossTurnCounter;

        private BossFloorManagerSO ResolveBossDataSO(BehaviorContext ctx)
        {
            if (BossDataOverride != null) return BossDataOverride;
            // ctx.SourceEntity.DataSO no existe en el Entity stub: el override es la ruta canonica.
            return null;
        }

        private ContractSheet ResolveSheet()
        {
            return SheetResolver != null ? SheetResolver() : null;
        }
    }
}
