using System;
using Rollgeon.Attributes;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice
{
    public enum ResourceKind
    {
        Gold,
        Stat,
    }

    /// <summary>
    /// Recurso objetivo de un trigger genérico: oro del jugador o una stat concreta.
    /// Sirve también como clave de los acumuladores en <see cref="EnchantmentScratch"/>
    /// (de ahí <see cref="IEquatable{T}"/> y el hash estable).
    /// </summary>
    [Serializable]
    public struct ResourceTarget : IEquatable<ResourceTarget>
    {
        public ResourceKind Kind;

        [ShowIf(nameof(Kind), ResourceKind.Stat)]
        public StatType Stat;

        public static ResourceTarget Gold => new ResourceTarget { Kind = ResourceKind.Gold };

        public static ResourceTarget OfStat(StatType stat) =>
            new ResourceTarget { Kind = ResourceKind.Stat, Stat = stat };

        public bool Equals(ResourceTarget other) =>
            Kind == other.Kind && (Kind == ResourceKind.Gold || Stat == other.Stat);

        public override bool Equals(object obj) => obj is ResourceTarget o && Equals(o);

        // Gold colapsa a un único bucket (0); las stats se separan por StatType.
        public override int GetHashCode() => Kind == ResourceKind.Gold ? 0 : 1 + (int)Stat;
    }
}
