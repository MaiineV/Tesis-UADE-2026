using System;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// No hace daño este turno. Cada slot marca su área por separado (ver
    /// <see cref="CroupierSectorTelegraph"/>): en fase 2 las dos áreas tienen que resolverse aparte
    /// para que la columna de costura cobre los dos golpes.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_MarkSungSectors : AIActionNode
    {
        [Tooltip("Daño del sector en fase 1 (un solo número cantado).")]
        [MinValue(0)]
        public int SectorDamage = 20;

        [Tooltip("Daño de CADA sector en fase 2. Los dos sectores se resuelven por separado: en la " +
                 "columna de costura, donde se pisan, el jugador cobra los dos (2 × este valor).")]
        [MinValue(0)]
        public int SectorDamagePhase2 = 12;

        [Tooltip("Tipo de ataque del DamageContext al detonar.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        public override string NodeName => $"Mark Sung Sectors (Croupier, {SectorDamage}/{SectorDamagePhase2})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return AIResult.Failed;

            var numbers = wheel.SungNumbers;
            if (numbers == null || numbers.Count == 0) return AIResult.Failed;

            int damage = wheel.PhaseIndex >= 2 ? SectorDamagePhase2 : SectorDamage;

            bool markedAny = false;
            for (int slot = 0; slot < numbers.Count; slot++)
            {
                if (!CroupierSectorTelegraph.Mark(context.SelfGuid, slot, numbers[slot], damage, Kind)) continue;

                wheel.RecordMark(slot, damage, Kind);
                markedAny = true;
            }

            if (!markedAny)
            {
                Debug.LogWarning("[AINode_MarkSungSectors] No se marcó ningún sector — ¿sala sin bounds " +
                                 "o IThreatenedAreaService sin registrar? El paño no va a detonar.");
                return AIResult.Failed;
            }

            return AIResult.Succeeded;
        }
    }
}
