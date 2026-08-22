using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Back-compat shim over <see cref="HazardService"/>: rain is one
    /// <see cref="HazardDefinitionSO"/> like any other and the generic service owns the turn loop.
    /// The type name and <see cref="RainSourceId"/> are load-bearing —
    /// <c>ED_Boss_Sunken_Grand.asset</c> references
    /// <see cref="AI.Decisions.AINode_ActivateRainHazard"/> by full type name (Odin polymorphic
    /// serialization) and <c>RainHazardServiceBootstrap.asset</c> references this class by script
    /// GUID.
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
            // RainHazardServiceBootstrap.asset solo conoce este tipo: si nadie registró el
            // HazardService genérico todavía, lo hacemos acá.
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

        // Instancia en memoria: el shim no depende de que RainHazardDefinition.asset exista ni
        // esté bien wireado.
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
