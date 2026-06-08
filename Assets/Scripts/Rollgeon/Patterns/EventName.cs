namespace Patterns
{
    /// <summary>
    /// Familia mínima de eventos del bus legacy <see cref="EventManager"/>.
    /// Schema definido en TECHNICAL.md §1.2 (líneas 500–621).
    /// <para>
    /// <b>Regla transversal de <c>args[0]</c>.</b> Para todo evento cuyo payload referencie a una
    /// entidad concreta, <c>args[0]</c> es <b>siempre</b> <see cref="System.Guid"/> — el
    /// <c>InstanceId</c> de la entidad primaria. Nunca se pasa <c>Entity</c> ni
    /// <c>MonoBehaviour</c>. Ver TECHNICAL.md §1.2 línea 525.
    /// </para>
    /// <para>
    /// <b>Regla de canal único.</b> <c>OnDamageResolved</c>, <c>OnHealthChanged</c> y
    /// <c>OnComboMatched</c> viven únicamente como <see cref="TypedEvent{T}"/>
    /// (<c>DamageResolvedPayload</c>, <c>HealthChangedPayload</c>, <c>ComboMatchedPayload</c>).
    /// No tienen entry en este enum por diseño. Ver TECHNICAL.md §1.2.1.
    /// </para>
    /// <para>
    /// <b>No presente por diseño:</b> <c>OnScreenPushed</c>, <c>OnScreenPopped</c>,
    /// <c>OnPauseChanged</c> viven en <c>IScreenManager</c> (§17.D), no en el bus legacy.
    /// </para>
    /// </summary>
    public enum EventName
    {
        // --- Run lifecycle ------------------------------------------------------
        /// <summary>args: [Guid runId, string rulesetId]</summary>
        OnRunStart,
        /// <summary>args: [Guid runId, RunOutcome outcome]</summary>
        OnRunEnd,

        // --- Combat lifecycle ---------------------------------------------------
        /// <summary>args: [Guid roomInstanceId]</summary>
        OnCombatStart,
        /// <summary>args: [Guid roomInstanceId, CombatOutcome outcome]</summary>
        OnCombatEnd,
        /// <summary>args: [Guid runId]</summary>
        OnPlayerDefeated,

        // --- Damage pipeline ----------------------------------------------------
        /// <summary>args: [Guid sourceGuid, Guid targetGuid, int baseDamage]</summary>
        OnDamageOutgoing,
        /// <summary>args: [Guid sourceGuid, Guid targetGuid, int incomingDamage]</summary>
        OnDamageIncoming,
        // OnDamageResolved NO existe acá — vive como TypedEvent<DamageResolvedPayload>.

        // --- Turn / initiative --------------------------------------------------
        /// <summary>args: [Guid entityGuid]. Quien arranca su turno.</summary>
        OnTurnStarted,
        /// <summary>args: [Guid entityGuid]. Quien cierra su turno — lo consume Modifier&lt;T&gt; para decrementar Duration.</summary>
        OnTurnFinished,
        /// <summary>args: [Guid entityGuid, int current, int max]</summary>
        OnEnergyChanged,
        /// <summary>args: [IReadOnlyList&lt;Guid&gt; orderForRound, int roundIndex]</summary>
        OnTurnQueueBuilt,

        // --- Phase lifecycle (§12.0) --------------------------------------------
        /// <summary>args: [GamePhase exiting]</summary>
        OnPhaseExit,
        /// <summary>args: [GamePhase entering]</summary>
        OnPhaseEnter,
        /// <summary>args: [PhaseOverlay overlay]</summary>
        OnOverlayPushed,
        /// <summary>args: [PhaseOverlay overlay]</summary>
        OnOverlayPopped,

        // --- Roll ---------------------------------------------------------------
        /// <summary>args: [Guid sourceGuid]. Se dispara al iniciar una tirada.</summary>
        OnRollStarted,
        /// <summary>args: [Guid sourceGuid, IReadOnlyList&lt;int&gt; faces]. Resultado crudo de los dados tirados.</summary>
        OnDiceRolled,
        /// <summary>args: [Guid sourceGuid, int rerollIndex]. Se dispara al iniciar un reroll.</summary>
        OnRerollStarted,
        /// <summary>args: [Guid sourceGuid, IReadOnlyList&lt;int&gt; finalFaces]. Roll finalizado y lockeado, tras rerolls.</summary>
        OnRollResolved,

        // --- Chain -----------------------------------------------------------------
        /// <summary>args: [Guid sourceGuid]. Una accion con EffChain fue seleccionada
        /// y el chain quedo activo (antes del primer roll). La UI lo usa para mantener
        /// los botones de behavior lockeados entre fases del chain.</summary>
        OnChainStarted,
        /// <summary>args: [Guid sourceGuid, int phaseIndex, int totalPhases]</summary>
        OnChainPhaseStarted,
        /// <summary>args: [Guid sourceGuid, int phasesCompleted, int totalPhases, bool wasPass]</summary>
        OnChainCompleted,
        /// <summary>args: [Guid sourceGuid]. Una accion sin tirada (ej. Movement) quedo
        /// comprometida y esta esperando que el jugador elija el tile target. La accion ya
        /// se cobro y se ejecuta de forma asincrona al clickear el destino; mientras tanto
        /// la UI debe lockear los demas slots para impedir iniciar otra accion en paralelo
        /// (BUG-013). El lock se libera con el <see cref="OnBehaviorExecuted"/> que dispara
        /// la accion al completarse.</summary>
        OnActionSelectionStarted,
        /// <summary>args: [Guid sourceGuid, string actionName, bool blockOnRepeat].
        /// El behavior termino de ejecutarse en el turno (sea simple o chain). La UI
        /// lo usa para transicionar el boton del slot a Used/Available segun
        /// blockOnRepeat.</summary>
        OnBehaviorExecuted,

        // --- Combat resolve -----------------------------------------------------
        // OnHealthChanged NO existe acá — vive como TypedEvent<HealthChangedPayload>.
        /// <summary>args: [Guid entityGuid, int currentShield]</summary>
        OnShieldChanged,
        /// <summary>args: [Guid entityGuid, Guid sourceGuid]. Entidad destruida por source.</summary>
        OnEntityDestroyed,

        // --- Contract -----------------------------------------------------------
        // OnComboMatched NO existe acá — vive como TypedEvent<ComboMatchedPayload>.
        /// <summary>args: [Guid sourceGuid, string comboId]. Combo strikable crossed.</summary>
        OnComboCrossed,
        /// <summary>args: [Guid sourceGuid, Guid targetGuid]. Se pegó contra una debilidad.</summary>
        OnWeaknessHit,
        /// <summary>args: [string comboId, int durationTurns]. Boss FloorManager bloquea un combo del ContractSheet. [T103]</summary>
        OnComboBlocked,
        /// <summary>args: [string comboId]. Bloqueo expirado (duration llego a 0). [T103]</summary>
        OnComboUnblocked,
        /// <summary>args: [string comboId, int newCount]. Contador run-scoped de un combo incrementado (§5.5 — T97c).</summary>
        OnComboCounterIncremented,
        /// <summary>args: [Guid playerGuid, int used, int cap]. Estado del reroll budget cambió (T104 extensión — opcional, dispara si el servicio lo emite).</summary>
        OnRerollBudgetChanged,

        // --- Modifier / attributes ---------------------------------------------
        /// <summary>args: [Guid entityId, Type attributeType]. Notifica que un atributo
        /// de la entidad cambió su valor calculado (consumido por Foundation#0003 Attributes + Modifiers).</summary>
        OnAttributeChanged,
        /// <summary>args: [Guid ownerGuid, Guid modifierId]</summary>
        OnModifierAdded,
        /// <summary>args: [Guid ownerGuid, Guid modifierId]</summary>
        OnModifierRemoved,

        // --- Dungeon ------------------------------------------------------------
        /// <summary>args: [Guid roomInstanceId, string roomId]</summary>
        OnRoomEntered,
        /// <summary>args: [Guid roomInstanceId]</summary>
        OnRoomCleared,
        /// <summary>args: [Guid runId, int floorIndex]</summary>
        OnFloorCleared,
        /// <summary>args: [Guid runId, int newFloorIndex]. Fired by RunContext.AdvanceFloor().</summary>
        OnFloorChanged,
        /// <summary>args: [Guid roomInstanceId]. El player activó una puerta de salida física
        /// (caminó al tile de salida). Lo consume FloorProgressionService para transicionar
        /// al siguiente piso (#158).</summary>
        OnFloorExitRequested,

        // --- HUD bindings (le hablan al §D ScreenManager) ----------------------
        /// <summary>args: [Guid entityGuid, int current, int max]</summary>
        OnPlayerHealthChanged,
        /// <summary>args: [Guid entityGuid, int current, int max]</summary>
        OnPlayerEnergyChanged,
        /// <summary>args: [int current, int delta]</summary>
        OnGoldChanged,
        /// <summary>args: [Guid targetGuid, FloatingNumberType type, float value, Vector3 offset]</summary>
        OnFloatingNumberRequested,

        // --- Craps --------------------------------------------------------------
        /// <summary>args: [Guid sessionId, Guid playerGuid]</summary>
        OnCrapsSessionStarted,
        /// <summary>args: [Guid sessionId, string comboId, int stake]</summary>
        OnCrapsBetPlaced,
        /// <summary>args: [Guid sessionId, CrapsOutcome outcome, int payout]</summary>
        OnCrapsResolved,

        // --- Save (§15) ---------------------------------------------------------
        /// <summary>args: []. Request global para que cada servicio que implementa captura
        /// serialice su estado al contenedor de save activo.</summary>
        OnCaptureRequested,
        /// <summary>args: []. Se dispara cuando la restauración de save terminó de hidratar a todos los servicios.</summary>
        OnRestoreCompleted,

        // --- Feedback -----------------------------------------------------------
        /// <summary>args: [Guid instanceId, string feedbackId]. Un feedback comenzó a ejecutarse.</summary>
        OnFeedbackStarted,
        /// <summary>args: [Guid instanceId, string feedbackId]. Un feedback terminó su ciclo de vida.</summary>
        OnFeedbackCompleted,

        // --- Interaction (§7.7) ------------------------------------------------
        /// <summary>args: [Guid targetGuid, string resolvedLabel, bool isAvailable].
        /// targetGuid == Guid.Empty significa "no hay target, esconder el prompt".
        /// resolvedLabel es el LocalizedString del label ya resuelto por el
        /// LocalizationManager. isAvailable == false =&gt; prompt grayed out.</summary>
        OnInteractionTargetChanged,
        /// <summary>args: [Guid targetGuid]</summary>
        OnInteractionExecuted,

        // --- Shop (§17.F) ------------------------------------------------------
        /// <summary>args: [bool hasTarget, string itemName, string description, int price, Sprite icon].
        /// hasTarget == false → esconder el ItemInspectView.</summary>
        OnShopItemTargetChanged,
        /// <summary>args: [string spawnPointId, string rewardId, int pricePaid]</summary>
        OnShopItemPurchased,
        /// <summary>args: [Guid roomInstanceId, int slotsRestocked]</summary>
        OnShopRestocked,

        // --- Status (§20) ------------------------------------------------------
        /// <summary>args: [Guid targetGuid, StatusEffectSO status, int stacks]</summary>
        OnStatusApplied,
        /// <summary>args: [Guid targetGuid, StatusEffectSO status]</summary>
        OnStatusRemoved,
        /// <summary>args: [Guid targetGuid, StatusEffectSO status, int deltaAmount]. deltaAmount = damage/heal aplicado en el tick.</summary>
        OnStatusTicked,

        // --- Items (§18) -------------------------------------------------------
        /// <summary>args: [Guid ownerGuid, string itemId]. Cubre pickups y upgrades (payload uniforme).</summary>
        OnItemObtained,
        /// <summary>args: [Guid ownerGuid, string itemId]</summary>
        OnItemRemoved,
        /// <summary>args: [Guid sourceGuid, string itemId]. Se usó un item activo.</summary>
        OnActiveItemUsed,

        // --- Quest (§21) -------------------------------------------------------
        /// <summary>args: [string questId, QuestState state]</summary>
        OnQuestStateChanged,

        // --- Exploration -------------------------------------------------------
        /// <summary>args: [Guid runId]</summary>
        OnExplorationStarted,
        /// <summary>args: [Guid roomInstanceId, string roomId, RoomType roomType]</summary>
        OnCombatTriggered,

        // --- Scene (§K) --------------------------------------------------------
        /// <summary>args: [string sceneName]. Scene aditiva terminó de cargar.</summary>
        OnSceneLoaded,
        /// <summary>args: [string sceneName]. Scene aditiva terminó de descargarse.</summary>
        OnSceneUnloaded,

        // --- Upgrades / Enchantments -------------------------------------------
        /// <summary>args: [Guid playerGuid, string enchantmentId, int bagIndex, int enchSlotIndex]. Encantamiento aplicado a un cupo del dado.</summary>
        OnEnchantmentApplied,
        /// <summary>args: [Guid playerGuid, string enchantmentId, int bagIndex, int enchSlotIndex]. Encantamiento removido (manual o por trigger self-destruct).</summary>
        OnEnchantmentRemoved,
        /// <summary>args: [Guid playerGuid, Guid roomInstanceId, int baseCost]. El player presionó interact sobre el altar — la UI debe abrir la pantalla de selección de dado/slot.</summary>
        OnEnchantmentAltarActivated,

        // --- Camera (§17.E) ----------------------------------------------------
        /// <summary>args: [Rollgeon.Camera.CameraFacing newFacing]. Yaw discreto cambió tras un RotateBy45 (§17.E.5).</summary>
        OnCameraFacingChanged,
        /// <summary>args: [bool enabled]. Cruce del umbral de floor view (§17.E.9). true = shells visibles, sala actual hidden.</summary>
        OnCameraFloorViewToggled,
        /// <summary>args: [bool instant]. Cámara hizo recenter — instant omite el tween (§17.E.6.4).</summary>
        OnCameraRecentered,
        /// <summary>args: [float amplitude, float durationSeconds]. Feedback pide un camera shake; el CameraService lo consume (§17.E.10, TODO v8).</summary>
        OnCameraShakeRequested,
    }
}
