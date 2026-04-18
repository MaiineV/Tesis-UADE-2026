using System;

namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — reemplazado por Entities (Sprint 03 tarea de entities / Foundation de Entidades).
    /// Superficie mínima que <see cref="EffectContext"/>, <see cref="Rollgeon.PreConditions.PreConditionContext"/>
    /// y las queries de <see cref="Selection.BaseTargetQuery"/> necesitan para compilar.
    /// Cuando la foundation real de Entities mergee, este archivo se elimina y los
    /// consumers cambian sus <c>using Rollgeon.Effects.Stubs;</c> por el namespace real.
    /// </summary>
    public class Entity
    {
        /// <summary>Identidad estable de la entidad. Usada por preconditions, readers y selección.</summary>
        public Guid Guid;
    }
}
