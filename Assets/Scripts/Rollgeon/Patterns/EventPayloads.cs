using System;

namespace Patterns
{
    /// <summary>
    /// Payload tipado para el evento "daño resuelto". Canalizado únicamente vía
    /// <c>TypedEvent&lt;DamageResolvedPayload&gt;</c> — no existe entry legacy en <see cref="EventName"/>.
    /// </summary>
    public struct DamageResolvedPayload
    {
        /// <summary>InstanceId de la entidad que causó el daño.</summary>
        public Guid SourceGuid;

        /// <summary>InstanceId de la entidad que recibió el daño.</summary>
        public Guid TargetGuid;

        /// <summary>Daño final aplicado tras mitigaciones, escudos, multiplicadores, etc.</summary>
        public int FinalDamage;

        /// <summary><c>true</c> si el golpe impactó una debilidad del target.</summary>
        public bool WeaknessHit;

        /// <summary><c>true</c> si el Health del target llegó a 0 como resultado de este daño.</summary>
        public bool WasLethal;

        /// <summary>Daño absorbido por Shield antes de aplicar a Health.</summary>
        public int ShieldAbsorbed;

        /// <summary><c>true</c> si todo el daño fue absorbido por shield (FinalDamage == 0
        /// y ShieldAbsorbed &gt; 0). Útil para feedback visual: en este caso no mostramos
        /// "0" rojo sino "0" en color shield.</summary>
        public bool BlockedByShield;

        /// <summary><c>true</c> si el shield del target estaba activo (&gt; 0) antes de
        /// este hit y quedó en 0 después. Útil para spawnear un "Broken Shield" además
        /// del número de daño residual.</summary>
        public bool ShieldBroken;
    }

    /// <summary>
    /// Payload tipado para el evento "heal resuelto". Canalizado únicamente vía
    /// <c>TypedEvent&lt;HealResolvedPayload&gt;</c> — no existe entry legacy en <see cref="EventName"/>.
    /// </summary>
    public struct HealResolvedPayload
    {
        /// <summary>InstanceId de la entidad que proporcionó la curación.</summary>
        public Guid SourceGuid;

        /// <summary>InstanceId de la entidad que recibió la curación.</summary>
        public Guid TargetGuid;

        /// <summary>Curación final aplicada tras clamps y multiplicadores.</summary>
        public int FinalHeal;

        /// <summary><c>true</c> si el heal fue basado en porcentaje del HP máximo.</summary>
        public bool WasPercentBased;
    }

    /// <summary>
    /// Payload tipado para cambios en el <c>Health</c> de una entidad. Canalizado únicamente vía
    /// <c>TypedEvent&lt;HealthChangedPayload&gt;</c> — no existe entry legacy en <see cref="EventName"/>.
    /// </summary>
    public struct HealthChangedPayload
    {
        /// <summary>InstanceId de la entidad cuyo health cambió.</summary>
        public Guid EntityGuid;

        /// <summary>Health actual tras el cambio.</summary>
        public int Current;

        /// <summary>Health máximo actual de la entidad (puede haber cambiado por modifiers).</summary>
        public int Max;
    }

    /// <summary>
    /// Payload tipado para "combo matcheado" al resolver una tirada. Canalizado únicamente vía
    /// <c>TypedEvent&lt;ComboMatchedPayload&gt;</c> — no existe entry legacy en <see cref="EventName"/>.
    /// </summary>
    public struct ComboMatchedPayload
    {
        /// <summary>InstanceId de la entidad que generó el combo (quien tiró).</summary>
        public Guid SourceGuid;

        /// <summary>Id del combo matcheado (clave del catálogo de combos).</summary>
        public string ComboId;

        /// <summary>Nombre legible del combo para UI.</summary>
        public string DisplayName;

        /// <summary>Daño base del combo antes de mitigaciones / multiplicadores.</summary>
        public int BaseDamage;
    }
}
