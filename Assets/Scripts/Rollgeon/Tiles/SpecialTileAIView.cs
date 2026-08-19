using System;
using Rollgeon.Grid;

namespace Rollgeon.Tiles
{
    /// <summary>Qué clase de beneficio ofrece una casilla como destino (pathing IA).</summary>
    public enum BeneficialTileKind
    {
        None = 0,
        Healing = 1,
        Fortress = 2,
        /// <summary>Impulso — INERTE hasta que exista tirada real de movimiento (BenefitValue 0).</summary>
        Impulse = 3,
        SafeZone = 4,
    }

    /// <summary>
    /// Vista de una celda para el pathing IA, ya contextualizada a la unidad que pregunta
    /// (Veneno: 15 esperado si no está envenenada, 5 si ya lo está; una voladora ni ve los
    /// tiles GroundOnly). Contrato de integración planner ↔ casillas.
    /// </summary>
    public readonly struct SpecialTileAIView
    {
        /// <summary>Daño REAL esperado al entrar — reduce HP Proyectado y alimenta el filtro
        /// de supervivencia además del costo.</summary>
        public readonly int EnterDamage;

        /// <summary>Daño real esperado por PERMANECER (tick de inicio de turno del Fuego).</summary>
        public readonly int StayDamage;

        /// <summary>Daño VIRTUAL (Charco: 25 por el stun) — solo penaliza costo, no toca
        /// HP Proyectado ni dispara el filtro de supervivencia.</summary>
        public readonly int VirtualEnterDamage;

        public readonly BeneficialTileKind Benefit;

        /// <summary>Hielo o Portal: la celda mueve a la unidad a otro lado.</summary>
        public readonly bool HasForcedMove;

        /// <summary><c>true</c> = Portal, <c>false</c> = Hielo (válido solo con HasForcedMove).</summary>
        public readonly bool IsPortal;

        /// <summary>Fin del deslizamiento (según dirección de entrada) o portal de salida.</summary>
        public readonly GridCoord ForcedDestination;

        public readonly bool HasTelegraph;

        /// <summary>Daño anunciado por el Telegraph (0 si es letal-puro).</summary>
        public readonly int TelegraphDamage;

        /// <summary>Anuncia muerte/ataque letal: bloquea la ruta salvo IA Kamikaze.</summary>
        public readonly bool TelegraphLethal;

        public SpecialTileAIView(int enterDamage, int stayDamage, int virtualEnterDamage,
            BeneficialTileKind benefit, bool hasForcedMove, bool isPortal, GridCoord forcedDestination,
            bool hasTelegraph, int telegraphDamage, bool telegraphLethal)
        {
            EnterDamage = enterDamage;
            StayDamage = stayDamage;
            VirtualEnterDamage = virtualEnterDamage;
            Benefit = benefit;
            HasForcedMove = hasForcedMove;
            IsPortal = isPortal;
            ForcedDestination = forcedDestination;
            HasTelegraph = hasTelegraph;
            TelegraphDamage = telegraphDamage;
            TelegraphLethal = telegraphLethal;
        }
    }

    /// <summary>
    /// Cara de consulta del sistema de casillas para el pathing IA. Interfaz propia (y no
    /// parte de <see cref="ISpecialTileService"/>) para que el planner se testee con un fake
    /// chico sin arrastrar el servicio entero.
    /// </summary>
    public interface ISpecialTileAIQuery
    {
        /// <summary>Fast-path: sin casillas, el planner delega al scoring legacy exacto.</summary>
        bool HasAnySpecialTiles { get; }

        /// <summary>Condición del BenefitValue de Zona de Seguridad (3): hay un Telegraph
        /// peligroso activo en la sala.</summary>
        bool AnyActiveDangerTelegraph { get; }

        /// <summary><c>false</c> = celda común para esta unidad (sin costo extra ni beneficio).</summary>
        bool TryGetTileFor(GridCoord coord, Guid entity, Cardinal entryDirection, out SpecialTileAIView view);
    }
}
