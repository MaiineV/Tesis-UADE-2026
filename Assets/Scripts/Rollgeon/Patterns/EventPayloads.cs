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

        /// <summary>
        /// Multiplicador de daño entrante aplicado en el stage 3 (<c>IIncomingDamageMultiplierProvider</c>):
        /// la mesa de La Generala en pie le descuenta el golpe. <c>1</c> = nada lo modificó.
        /// </summary>
        /// <remarks>
        /// Es un struct, así que un payload armado a mano lo trae en <c>0</c>. Los consumidores tratan
        /// "hubo reducción" como <c>&gt; 0 &amp;&amp; &lt; 1</c> y no como <c>!= 1</c>: el pisa
        /// <c>0</c> sería "-100%", y una reducción total no existe (el pipeline clampea el daño a un
        /// mínimo de 1).
        /// </remarks>
        public float IncomingMultiplier;
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
    /// Payload tipado para "el jugador intentó usar una acción que no puede pagar". Canalizado
    /// únicamente vía <c>TypedEvent&lt;InsufficientEnergyPayload&gt;</c> — no existe entry legacy
    /// en <see cref="EventName"/>.
    /// </summary>
    /// <remarks>
    /// Lo emite el chip de la acción y lo consume la pila de energía del HUD, que vive en otro
    /// prefab (<c>Canvas_PlayerStatus</c>) y tiene su propio ciclo de vida — la bindea tanto el
    /// HUD de combate como el de exploración. Un evento evita la ref cruzada y deja que mañana
    /// se enganchen audio o shake de cámara sin tocar a ninguno de los dos.
    /// </remarks>
    public struct InsufficientEnergyPayload
    {
        /// <summary>InstanceId del jugador que intentó la acción.</summary>
        public Guid PlayerGuid;

        /// <summary>Costo real de la acción rechazada (el que se iba a cobrar, no el legacy).</summary>
        public int Cost;

        /// <summary>Energía disponible al momento del rechazo.</summary>
        public int Current;
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

        /// <summary>Daño base PLANO del combo (término comboBase de la fórmula v3), antes de
        /// mitigaciones / multiplicadores. Las caras contribuyentes NO viven acá (Fix#0047).</summary>
        public int BaseDamage;

        /// <summary>Parte dinámica de los combos de base variable (SumaX, Higher Number,
        /// Fuerza Bruta) — 0 para combos planos. Solo para consumers de formula B legacy;
        /// la fórmula v3 suma las caras vía <see cref="ContributingDice"/>.</summary>
        public int DynamicBonus;

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

    /// <summary>
    /// Payload tipado para "cofre abierto por el jugador". Se emite DESPUÉS de que la
    /// recompensa fue otorgada (inventario/oro ya actualizados) — el reveal gacha es
    /// presentación pura. Canalizado únicamente vía
    /// <c>TypedEvent&lt;ChestOpenedPayload&gt;</c>; el entry legacy
    /// <see cref="EventName.OnChestOpened"/> existe solo para telemetría/tests.
    /// </summary>
    public struct ChestOpenedPayload
    {
        /// <summary>InstanceId del cofre abierto.</summary>
        public Guid ChestGuid;

        /// <summary>Tier del cofre (Common/Uncommon/Rare/Legendary).</summary>
        public Rollgeon.Items.ItemRarity Tier;

        /// <summary>Ítem otorgado. <c>null</c> cuando la recompensa fue oro.</summary>
        public Rollgeon.Items.ItemSO Item;

        /// <summary>Oro otorgado. 0 cuando la recompensa fue un ítem.</summary>
        public int GoldAmount;

        /// <summary><c>true</c> si el roll fue un ítem pero el inventario no pudo
        /// recibirlo (slots activos llenos) y se otorgó oro de fallback en su lugar.</summary>
        public bool WasInventoryFullFallback;

        /// <summary>Ítems del pool del tier, relleno para el reel gacha. Puede ser
        /// vacío — la UI debe degradar (repetir ganador + fillers de oro).</summary>
        public IReadOnlyList<Rollgeon.Items.ItemSO> PoolPreview;
    }
}
