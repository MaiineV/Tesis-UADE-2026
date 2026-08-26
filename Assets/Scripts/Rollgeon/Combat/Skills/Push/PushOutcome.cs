using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Skills.Push
{
    /// <summary>Cómo terminó un eslabón del empuje (una unidad empujada).</summary>
    public enum PushHopStop
    {
        /// <summary>Recorrió toda la distancia pedida.</summary>
        Completed = 0,

        /// <summary>Chocó contra pared / fuera de grilla → stun.</summary>
        Wall = 1,

        /// <summary>Chocó contra un prop sin vida (cofre, pedestal, guid sintético) → stun, como pared.</summary>
        NonBreakableProp = 2,

        /// <summary>Chocó contra un objeto de sala rompible → daño al empujado, el objeto se rompe.</summary>
        BreakableObstacle = 3,

        /// <summary>Chocó contra otro enemigo → daño a ambos, el otro recibe el remanente.</summary>
        Enemy = 4,

        /// <summary>Murió a mitad del recorrido (pinchos, fuego): sin choque.</summary>
        Died = 5,

        /// <summary>Un portal sin salida cortó el recorrido.</summary>
        PortalBlocked = 6,

        /// <summary>La unidad no estaba en la grilla al empezar el eslabón.</summary>
        NotOnGrid = 7,
    }

    /// <summary>Un eslabón de la cadena de empuje: una unidad, lo que recorrió y contra qué frenó.</summary>
    public readonly struct PushHop
    {
        public readonly Guid Entity;
        public readonly GridCoord From;
        public readonly GridCoord FinalCoord;
        public readonly int Requested;
        public readonly int Traveled;
        public readonly PushHopStop Stop;
        public readonly Guid BlockerGuid;
        public readonly int DamageToPushed;
        public readonly int DamageToBlocker;
        public readonly bool PushedStunned;
        public readonly bool PushedDied;
        public readonly bool BlockerBroken;
        public readonly bool BlockerDied;

        public PushHop(Guid entity, GridCoord from, GridCoord finalCoord, int requested, int traveled,
            PushHopStop stop, Guid blockerGuid = default, int damageToPushed = 0, int damageToBlocker = 0,
            bool pushedStunned = false, bool pushedDied = false, bool blockerBroken = false, bool blockerDied = false)
        {
            Entity = entity;
            From = from;
            FinalCoord = finalCoord;
            Requested = requested;
            Traveled = traveled;
            Stop = stop;
            BlockerGuid = blockerGuid;
            DamageToPushed = damageToPushed;
            DamageToBlocker = damageToBlocker;
            PushedStunned = pushedStunned;
            PushedDied = pushedDied;
            BlockerBroken = blockerBroken;
            BlockerDied = blockerDied;
        }

        public override string ToString()
            => $"{Entity.ToString().Substring(0, 8)} {From}->{FinalCoord} ({Traveled}/{Requested}) {Stop}" +
               (DamageToPushed > 0 ? $" dmgPushed={DamageToPushed}" : "") +
               (DamageToBlocker > 0 ? $" dmgBlocker={DamageToBlocker}" : "") +
               (PushedStunned ? " stunned" : "") +
               (PushedDied ? " died" : "") +
               (BlockerBroken ? " broke" : "") +
               (BlockerDied ? " blockerDied" : "");
    }

    /// <summary>Resultado completo de <see cref="IClassSkillPushResolver.Resolve"/>.</summary>
    public sealed class PushOutcome
    {
        public Cardinal Direction;
        public readonly List<PushHop> Hops = new List<PushHop>();

        public bool Moved
        {
            get
            {
                for (int i = 0; i < Hops.Count; i++) if (Hops[i].Traveled > 0) return true;
                return false;
            }
        }

        public bool AnyCollision
        {
            get
            {
                for (int i = 0; i < Hops.Count; i++)
                {
                    var s = Hops[i].Stop;
                    if (s == PushHopStop.Wall || s == PushHopStop.NonBreakableProp
                        || s == PushHopStop.BreakableObstacle || s == PushHopStop.Enemy) return true;
                }
                return false;
            }
        }

        public int TotalDamage
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Hops.Count; i++) total += Hops[i].DamageToPushed + Hops[i].DamageToBlocker;
                return total;
            }
        }
    }
}
