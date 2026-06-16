using System;

namespace Rollgeon.ActionRolls
{
    /// <summary>
    /// Marker que un effect implementa para opt-in al flujo de tirada de accion
    /// (Forzar Puerta, Curarse). El <see cref="ExplorationBehaviorService"/>
    /// detecta este interface en los effects de un behavior y, si <see cref="TryGetRollSpec"/>
    /// devuelve true, ruta el flujo a traves del <see cref="IActionRollService"/>
    /// en vez de invocar el effect directo.
    /// </summary>
    /// <remarks>
    /// Si <see cref="TryGetRollSpec"/> devuelve <c>false</c>, el effect se ejecuta
    /// como siempre (path "instantaneo" — ej. Forzar Puerta fuera de combate, que
    /// no tira dados ni gasta energia).
    /// </remarks>
    public interface IActionRollEffect
    {
        bool TryGetRollSpec(Guid playerGuid, out ActionRollSpec spec);
    }
}
