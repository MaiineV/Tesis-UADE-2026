using System;
using System.Collections.Generic;

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
    /// Payload tipado para "empezó un encuentro de jefe". Lo consume la barra de vida de jefe
    /// en pantalla (<c>BossBarView</c>) para encenderse, bindearse al jefe y poblar su nombre.
    /// Canalizado únicamente vía <c>TypedEvent&lt;BossEncounterStartedPayload&gt;</c> — no existe
    /// entry legacy en <see cref="EventName"/>. El apagado reusa <see cref="EventName.OnCombatEnd"/>
    /// y <see cref="EventName.OnEntityDestroyed"/>.
    /// </summary>
    public struct BossEncounterStartedPayload
    {
        /// <summary>InstanceId del jefe al que se bindea la barra (su <c>Health</c>).</summary>
        public Guid BossGuid;

        /// <summary>Nombre del jefe ya localizado, listo para mostrar en la UI.</summary>
        public string DisplayName;
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

        /// <summary>
        /// Dados que contribuyen al combo (slot + cara + tipo), para que el preview del HUD
        /// recompute el daño real con <c>PlayerComboDamage.Resolve</c> (misma fórmula que el
        /// golpe) en vez de una copia paralela. <c>null</c> = sin combo o sin bag disponible.
        /// </summary>
        public IReadOnlyList<Rollgeon.Combat.Damage.ContributingDie> ContributingDice;
    }

    /// <summary>
    /// Payload tipado para "combo jugado": la acción con combo matcheado se está ejecutando,
    /// ANTES de que ningún efecto consuma <c>ComboResult</c> (daño, escudo, heal). Se emite
    /// UNA vez por ejecución de acción — a diferencia de <see cref="ComboMatchedPayload"/>,
    /// que dispara en cada preview de hold sin que el jugador ataque. Canalizado únicamente
    /// vía <c>TypedEvent&lt;ComboPlayedPayload&gt;</c> — no existe entry legacy en <see cref="EventName"/>.
    /// </summary>
    public struct ComboPlayedPayload
    {
        /// <summary>InstanceId de la entidad que juega el combo (quien ejecuta la acción).</summary>
        public Guid SourceGuid;

        /// <summary>Target resuelto de la acción. <see cref="Guid.Empty"/> fuera de combate
        /// (Heal / ForceDoor en exploración) o sin target específico.</summary>
        public Guid TargetGuid;

        /// <summary>Id del combo jugado (clave del catálogo). Nunca vacío: los resultados
        /// sintéticos sin id (action rolls) no emiten este evento.</summary>
        public string ComboId;

        /// <summary>Resultado completo del match (BaseDamage post contract-mods,
        /// ContributingIndices, CountUsed).</summary>
        public Rollgeon.Combos.ComboDetectionResult ComboResult;

        /// <summary>Tirada completa (las caras). Null si el behavior no usa dados.</summary>
        public IReadOnlyList<int> DiceResult;

        /// <summary>Subset holdeado que participa del ataque. Null = sin keep explícito.</summary>
        public IReadOnlyList<int> KeptDice;

        /// <summary>Índices de slot del bag (0-based) 1:1 con <see cref="KeptDice"/> —
        /// permite checks "mi dado participó" del canal de encantamientos.</summary>
        public IReadOnlyList<int> KeptDiceOriginalIndices;
    }
}
