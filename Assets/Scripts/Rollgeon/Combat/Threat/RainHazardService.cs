using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Back-compat shim over <see cref="HazardService"/>. This used to be a standalone
    /// "rain of zones" hazard with its own <c>OnTurnQueueBuilt</c> loop and hardcoded constants;
    /// that loop is now generic (<see cref="HazardService"/>) and rain is just one
    /// <see cref="HazardDefinitionSO"/> among many. The type, its public API, and
    /// <see cref="RainSourceId"/> are kept unchanged on purpose: <c>ED_Boss_Sunken_Grand.asset</c>
    /// references <see cref="AI.Decisions.AINode_ActivateRainHazard"/> by full type name (Odin
    /// polymorphic serialization), and <c>RainHazardServiceBootstrap.asset</c> references this
    /// class by script GUID — renaming or removing either would desync those assets outside of
    /// Unity, where we can't re-author them. Rain's behavior is unchanged: same constants, same
    /// cadence, same source id, just routed through the generic service instead of owning its own
    /// event loop.
    /// </summary>
    public sealed class RainHazardService : IPreloadableService, IDisposable
    {
        /// <summary>GUID fijo de esta fuente — nunca el del boss, así ambas amenazas conviven
        /// sin pisarse en <see cref="IThreatenedAreaService"/>/<see cref="ThreatTelegraphOverlay"/>.</summary>
        public static readonly Guid RainSourceId = new Guid("6c1f3a2e-7b4d-4a9e-9c3f-1a2b3c4d5e6f");

        private const int CycleRounds = 2;
        private const int SquareCount = 10;
        private const int SquareSize = 1;
        private const int Damage = 6;

        private HazardDefinitionSO _definition;

        /// <summary>Junto al resto de servicios de combate (ver <c>ThreatenedAreaService.Priority</c> = 80).</summary>
        public int Priority => 80;

        /// <summary>True una vez que algo la activó — sigue activa el resto de la pelea aunque el HP suba.</summary>
        public bool IsActive =>
            ServiceLocator.TryGetService<IHazardService>(out var hazard) && hazard != null && hazard.IsActive(RainSourceId);

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            // El bootstrap histórico (RainHazardServiceBootstrap.asset) solo conoce este tipo — si
            // nadie más registró el HazardService genérico todavía, lo hacemos nosotros acá.
            // Idempotente: ServiceLocator.AddService hace upsert y HazardService.Register no tiene
            // estado que duplicar por una segunda invocación externa.
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazard) || hazard == null)
            {
                var service = new HazardService();
                service.Register();
            }

            _definition = BuildDefinition();

            ServiceLocator.AddService<RainHazardService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            // El HazardService genérico es dueño de la suscripción a eventos y del cleanup de
            // OnCombatEnd/OnRunEnd — este shim no tiene estado propio que liberar.
        }

        // ======================================================================
        // API
        // ======================================================================

        /// <summary>Activa la lluvia (idempotente — llamar de nuevo mientras ya está activa no hace nada).</summary>
        public void Activate()
        {
            if (ServiceLocator.TryGetService<IHazardService>(out var hazard) && hazard != null)
                hazard.Activate(_definition);
        }

        // ======================================================================
        // Internals
        // ======================================================================

        // Instancia en memoria (no el .asset RainHazardDefinition.asset) — el shim no depende de
        // que ese asset de ejemplo exista ni esté bien wireado; reproduce sus mismos valores en
        // código para que el comportamiento sea idéntico al de antes del refactor.
        private static HazardDefinitionSO BuildDefinition()
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.name = "RainHazardDefinition (runtime)";
            def.Shape = ThreatShape.ScatteredSquares;
            def.Size = SquareSize;
            def.Count = SquareCount;
            def.Damage = Damage;
            def.Kind = AttackKind.Environmental;
            def.CycleRounds = CycleRounds;
            def.SourceId = RainSourceId.ToString();
            return def;
        }
    }
}
