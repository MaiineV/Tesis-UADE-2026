using System;

namespace Rollgeon.Combat.Skills.Push
{
    /// <summary>
    /// Habilidad de Clase del Guerrero — Empuje (GDD Combat System § "Habilidad de Clase").
    /// Empuja al objetivo adyacente N casillas alejándolo del empujador y resuelve el choque:
    /// obstáculo rompible → daño al empujado + el obstáculo se rompe; otro enemigo → daño a
    /// ambos + el segundo recibe el empuje restante con las mismas reglas (cadena); pared o
    /// prop sin vida → el empujado pierde 1 turno (stun).
    /// </summary>
    /// <remarks>
    /// La física de grilla (casillas especiales, hielo, portales) la pone
    /// <c>IForcedMovementService</c>; acá solo se clasifica contra qué frenó cada eslabón y se
    /// aplica el efecto. Los números (distancia por combo, daño de choque) vienen del caller —
    /// el resolver no conoce la tabla de la clase.
    /// </remarks>
    public interface IClassSkillPushResolver
    {
        /// <param name="pusher">Quien empuja (player). Dirección = pusher → target.</param>
        /// <param name="target">Enemigo adyacente (Manhattan 1) a empujar.</param>
        /// <param name="distance">Casillas a recorrer. &lt;= 0 ⇒ no-op.</param>
        /// <param name="collisionDamage">Daño de choque (empujado y, si es enemigo, bloqueador).</param>
        /// <param name="stunTurns">Turnos de stun al chocar contra pared / prop.</param>
        PushOutcome Resolve(Guid pusher, Guid target, int distance, int collisionDamage, int stunTurns = 1);
    }
}
