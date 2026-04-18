using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.ComboBlock;
using Rollgeon.Combos;
using Rollgeon.Effects.Stubs;
using Rollgeon.Entities.Bosses;
using Rollgeon.Heroes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Behavior del Boss Floor Manager (Content#0103) — en su turno cuenta turnos propios y cada
    /// <see cref="BossFloorManagerSO.ComboBlockIntervalTurns"/> elige un combo <b>no bloqueado</b>
    /// del <see cref="ContractSheet"/> del jugador y lo bloquea via
    /// <see cref="IComboBlockService.Block"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pick aleatorio.</b> Usa <see cref="UnityEngine.Random"/>. Inyectable en tests via
    /// <see cref="RandomSource"/>.
    /// </para>
    /// <para>
    /// <b>Contract access.</b> No dependemos de <c>IPlayerService.Active.Sheet</c> (no existe
    /// en el stub). El caller / spawner inyecta un <see cref="SheetResolver"/> que devuelve la
    /// <see cref="ContractSheet"/> runtime del jugador. Patron identico al
    /// <c>MaxHpResolver</c> del <c>SupportHealBehavior</c>.
    /// </para>
    /// <para>
    /// <b>Counter storage.</b> <see cref="_bossTurnCounter"/> es un campo <c>[NonSerialized]</c>
    /// — persiste en la instancia clonada via <c>SerializationUtility.CreateCopy</c> (§7.2). El
    /// contador vive mientras el boss vive; al morir, el clone se descarta.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class BossComboBlockBehavior : BaseBehavior
    {
        public override string BehaviorName => "Boss Combo Block";

        /// <summary>
        /// SO con los defaults de intervalo/duracion. Si null al <c>Execute</c> el behavior
        /// intenta resolver desde <c>ctx.SourceEntity.DataSO</c>; si tampoco, warn + return.
        /// Expuesto Inspector para permitir overrides per-instance.
        /// </summary>
        [Tooltip("Override opcional del BossFloorManagerSO con los tuning values. Si null, se " +
                 "resuelve por default desde el SO de la entidad owner (plan §4.3).")]
        public BossFloorManagerSO BossDataOverride;

        /// <summary>Turnos contados del Boss. Resetea al spawnear (campo NonSerialized).</summary>
        [NonSerialized]
        private int _bossTurnCounter;

        /// <summary>
        /// Resolver de la <see cref="ContractSheet"/> del jugador. Inyectado por el spawner
        /// (runtime) o por el test setup. Si null, el behavior loguea warning y no bloquea
        /// (fallback defensivo).
        /// </summary>
        [NonSerialized]
        public Func<ContractSheet> SheetResolver;

        /// <summary>
        /// Funcion rng para elegir el combo al azar. Default <c>UnityEngine.Random.Range</c>
        /// via wrapper. Inyectable en tests para determinismo.
        /// </summary>
        [NonSerialized]
        public Func<int, int> RandomSource; // takes exclusive upper bound.

        /// <inheritdoc />
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
            // ctx.SourceEntity.DataSO no existe en el Entity stub — el override es la ruta
            // canonica en el FP. [FOLLOWUP] Cuando Entity real exponga DataSO, resolverlo aqui.
            return null;
        }

        private ContractSheet ResolveSheet()
        {
            return SheetResolver != null ? SheetResolver() : null;
        }
    }
}
