using System;

namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Seam opcional del <see cref="DamagePipeline"/>: multiplicador de daño <b>entrante</b> por
    /// target. Llena el stage 3 que TECHNICAL.md §12.2 tenía reservado como placeholder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mismo patrón que <see cref="IMinHpClampProvider"/> y <see cref="ILethalDamageOverride"/>: sin
    /// provider registrado el pipeline se comporta idéntico. El pipeline no sabe de dónde sale el
    /// número — que es el punto, porque la reducción de La Generala depende de cuántos dados le
    /// quedan en pie y eso no es un stat, es estado de la sala.
    /// </para>
    /// <para>
    /// <b>Por target y no por (target, source).</b> Lo que se modela es cuánto aguanta el que recibe,
    /// no de quién. Una armadura que dependiera de quién pega necesitaría otra firma, y hasta que
    /// haya un caso así agregarla sería inventar un eje.
    /// </para>
    /// <para>
    /// <b>Consumidor actual:</b> <c>RoomObjectArmorService</c> — la mesa de La Generala en pie le
    /// descuenta el daño, y cada dado roto se lo devuelve.
    /// </para>
    /// </remarks>
    public interface IIncomingDamageMultiplierProvider
    {
        /// <summary>
        /// <c>true</c> si el daño que recibe <paramref name="targetId"/> debe multiplicarse por
        /// <paramref name="multiplier"/>. <c>false</c> = sin modificar (equivale a 1).
        /// </summary>
        /// <remarks>
        /// El pipeline clampea el resultado a un mínimo de 1 cuando el daño de entrada era positivo:
        /// un golpe que muestra 0 se lee como un bug, no como una armadura.
        /// </remarks>
        bool TryGetMultiplier(Guid targetId, out float multiplier);
    }
}
