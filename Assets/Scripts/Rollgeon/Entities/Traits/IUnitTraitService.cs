using System;

namespace Rollgeon.Entities.Traits
{
    /// <summary>
    /// Registro Guid → <see cref="UnitTraits"/>. Lo pueblan los spawners
    /// (<c>DefaultEnemySpawnResolver</c>, refuerzos, <c>GameplayBootstrapper</c> para el
    /// player); lo consultan Casillas Especiales y el pathing IA.
    /// </summary>
    /// <remarks>
    /// <see cref="Get"/> devuelve <c>default</c> (terrestre / no-jefe / Normal) para guids
    /// desconocidos: consultar traits nunca falla ni exige registro previo.
    /// </remarks>
    public interface IUnitTraitService
    {
        /// <summary>Registra (o pisa) los traits de una entidad spawneada.</summary>
        void Register(Guid entity, UnitTraits traits);

        /// <summary>Olvida una entidad. No-op si nunca se registró.</summary>
        void Unregister(Guid entity);

        /// <summary>Traits de la entidad, o <see cref="UnitTraits.DefaultGround"/> si no está registrada.</summary>
        UnitTraits Get(Guid entity);

        /// <summary><c>false</c> = la entidad nunca fue registrada (el out trae el default igual).</summary>
        bool TryGet(Guid entity, out UnitTraits traits);
    }
}
