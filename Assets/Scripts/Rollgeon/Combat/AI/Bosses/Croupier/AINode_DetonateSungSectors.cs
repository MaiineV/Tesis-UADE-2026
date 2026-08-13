using System;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Detona los sectores que el Croupier cantó el turno pasado: consume el área pendiente de cada
    /// slot y, si el jugador está adentro, aplica su daño. Cierra el windup — a partir de acá pegarle
    /// al jefe ya no corre la rueda hasta que vuelva a cantar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va primero en el Sequence raíz, igual que <c>AINode_ExecuteTelegraph</c>, y como él devuelve
    /// siempre <see cref="AIResult.Succeeded"/>: "no había nada marcado" (turno 1) o "el jugador se
    /// fue del sector" son resoluciones válidas, no fallos que deban cortarle el turno al jefe.
    /// </para>
    /// <para>
    /// <b>Un golpe por sector, no un golpe por casilla.</b> En fase 2 las dos áreas se resuelven una
    /// por una, así que el jugador parado en la columna de costura recibe dos impactos de 12 (24 en el
    /// turno) en vez de uno de 24. Dos hits mantienen cada golpe individual bajo el techo de daño del
    /// piso y hacen que escudo/mitigación se apliquen como en cualquier otro par de golpes.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_DetonateSungSectors : AIActionNode
    {
        public override string NodeName => "Detonate Sung Sectors (Croupier)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Succeeded;

            var wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return AIResult.Succeeded;

            // Se cierra el windup ANTES de resolver el daño: el golpe que detona puede matar al
            // jugador y disparar el fin del combate, y con el windup abierto la rueda quedaría
            // esperando un corrimiento de una pelea que ya terminó.
            var slots = wheel.ConsumeWindup();
            if (slots == null || slots.Count == 0) return AIResult.Succeeded;

            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat);

            bool anyHit = false;
            foreach (var slot in slots)
            {
                var slotGuid = CroupierSectorTelegraph.SlotGuid(context.SelfGuid, slot.Slot);
                CroupierSectorTelegraph.ClearOverlay(context.SelfGuid, slot.Slot);

                if (threat == null || !threat.TryConsume(slotGuid, out var area)) continue;
                if (Resolve(context, area)) anyHit = true;
            }

            EventManager.Trigger(EventName.OnThreatenedAreaResolved, context.SelfGuid, anyHit);
            return AIResult.Succeeded;
        }

        private static bool Resolve(AIContext context, ThreatenedArea area)
        {
            var grid = context.Grid;
            if (grid == null) return false;
            if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;
            if (!area.Contains(playerCoord)) return false;

            if (context.DamagePipeline != null && area.Damage > 0)
            {
                context.DamagePipeline.Resolve(new DamageContext
                {
                    // El source es el jefe, no el guid derivado del slot: la atribución del daño, la
                    // debilidad y el feedback siguen apuntando al Croupier.
                    SourceId = context.SelfGuid,
                    TargetId = context.PlayerGuid,
                    BaseDamage = area.Damage,
                    Kind = area.Kind,
                });
            }
            return true;
        }
    }
}
