using System;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Runtime controller del prefab de puerta entre salas (TECHNICAL.md §13.6).
    /// Se parentes bajo la sala instanciada (<see cref="RoomInstance.SpawnedPrefab"/>)
    /// por el <c>DungeonManager</c> cuando detecta que la <see cref="DoorSlotRef"/>
    /// del <see cref="RoomLayout"/> tiene vecino. Si la sala no conecta por esa
    /// dirección, el DoorRoot queda activo y muestra el zócalo (<see cref="_meshWallFill"/>)
    /// que completa la pared (CNF-012 v2 rev: sin camino = pared tapiada, no hueco).
    /// </summary>
    [AddComponentMenu("Rollgeon/Dungeon/Door Controller")]
    public sealed class DoorController : MonoBehaviour
    {
        /// <summary>Owner runtime de la sala que contiene esta puerta.</summary>
        public Guid OwnerRoomInstanceId;

        /// <summary>Dirección cardinal de esta puerta en la sala dueña.</summary>
        public DoorDirection Direction;

        /// <summary>
        /// Puerta de SALIDA de piso (#158). No conecta a una sala vecina: cuando se abre
        /// (boss derrotado) y el player camina a su tile frente, dispara la transición al
        /// siguiente piso en vez de un <c>EnterRoomByDoor</c>. Se autorea en el prefab de la
        /// boss room con un mesh distinguible.
        /// </summary>
        public bool IsExit;

        /// <summary>
        /// Id determinístico para el <c>ObjectStates</c> dict de
        /// <see cref="RoomInstance"/> — matcheando
        /// <c>DungeonManager.DoorStateKey(Direction)</c> ("door_N/S/E/W").
        /// </summary>
        public string SpawnPointId;

        [Header("Visual children")]
        [Tooltip("Mesh + collider activos cuando la puerta está abierta.")]
        [SerializeField] private GameObject _meshOpen;

        [Tooltip("Puerta sólida cerrada. Sin estado asignado desde CNF-012 v2 (la reja " +
                 "representa bloqueada) — SetState la fuerza off porque el prefab la trae activa.")]
        [SerializeField] private GameObject _meshClosed;

        [Tooltip("La reja — visible cuando la puerta está bloqueada (lock combate o skill check).")]
        [SerializeField] private GameObject _wallPlug;

        [Tooltip("Zócalo que completa la pared cuando no hay camino (Tapiada). Único mesh " +
                 "visible en ese estado — reemplaza el viejo comportamiento de apagar el DoorRoot.")]
        [SerializeField] private GameObject _meshWallFill;

        public DoorVisualState CurrentState { get; private set; } = DoorVisualState.Open;

        /// <summary>
        /// La reja de este door root — expuesta para que el swap de boss door del
        /// <c>DungeonManager</c> pueda re-cablear <see cref="DoorSlotRef.WallPlug"/>.
        /// </summary>
        public GameObject WallPlugRef => _wallPlug;

#if UNITY_EDITOR
        /// <summary>Editor-only: nombre del campo serializado de meshOpen (para SerializedObject).</summary>
        public const string EditorMeshOpenField     = nameof(_meshOpen);
        public const string EditorMeshClosedField   = nameof(_meshClosed);
        public const string EditorWallPlugField     = nameof(_wallPlug);
        public const string EditorMeshWallFillField = nameof(_meshWallFill);

        public GameObject EditorMeshOpen     => _meshOpen;
        public GameObject EditorMeshClosed   => _meshClosed;
        public GameObject EditorWallPlug     => _wallPlug;
        public GameObject EditorMeshWallFill => _meshWallFill;
#endif

        private void Awake()
        {
            EnsureTooltipComponents();
            // SetState puede haber corrido con el GO inactivo (Sync usa
            // includeInactive) — re-aplicar el gate ahora que el trigger existe.
            ApplyTooltipGate();
        }

        // Un HeroActionTooltipBinder serializado en el prefab (quedó de cuando el
        // tooltip era el de la acción) corre su Awake DESPUÉS del nuestro y pisa el
        // TextProvider. Start corre después de todos los Awake: la última palabra la
        // tiene el mensaje corto de puerta.
        private void Start() => EnsureTooltipComponents();

        // Auto-attach del tooltip de puerta: un solo WorldTooltipTrigger en el root del
        // DoorController (siempre active). El trigger usa Physics.Raycast manual que
        // acepta hits en cualquier descendant — cubre los meshes hijos sin necesidad de
        // un trigger por cada uno.
        //
        // Antes mostraba el tooltip COMPLETO de la acción Forzar Puerta (costo, bonus de
        // ítems) sobre la propia puerta; pedido de playtest 04/09: mismo lugar que el
        // panel de enemigos (esquina superior derecha) y un mensaje corto de "esto se
        // puede forzar" — el detalle de la acción vive en el chip del HUD.
        private void EnsureTooltipComponents()
        {
            var trigger = GetComponent<WorldTooltipTrigger>();
            if (trigger == null)
                trigger = gameObject.AddComponent<WorldTooltipTrigger>();

            trigger.Placement = TooltipPlacementMode.ScreenTopRight;
            trigger.TextProvider = BuildDoorTooltip;

            // Un binder viejo (runs anteriores de este método) pisaría el provider.
            var staleBinder = GetComponent<HeroActionTooltipBinder>();
            if (staleBinder != null) Destroy(staleBinder);

            // En Exploración el cruce de puertas ya no es por click directo: el player
            // selecciona la casilla "frente a puerta" (roja) durante el movimiento y eso
            // dispara EnterRoomByDoor. En combate sigue siendo Force Door (con tirada).
        }

        // Solo durante combate (fuera de él la puerta se cruza caminando, sin tirada) y
        // con la tecla viva de la acción cuando el servicio de hotkeys está presente.
        private string BuildDoorTooltip()
        {
            if (!global::Patterns.ServiceLocator.TryGetService<IPhaseService>(out var phase)
                || phase == null || phase.CurrentBase != GamePhase.Combat)
            {
                return null;
            }

            string keyHint = string.Empty;
            if (global::Patterns.ServiceLocator.TryGetService<Rollgeon.Input.IGameplayHotkeyService>(
                    out var hotkeys) && hotkeys != null)
            {
                string key = hotkeys.GetKeyHint(Rollgeon.Input.GameplayHotkey.ForceDoor);
                if (!string.IsNullOrEmpty(key)) keyHint = $" ({key})";
            }

            string title = Rollgeon.Localization.LocalizedContent.Ui(
                "door.forceable.title", "Puerta forzable");
            string body = string.Format(Rollgeon.Localization.LocalizedContent.Ui(
                "door.forceable.body", "Podés intentar abrirla con Forzar Puerta{0}."), keyHint);

            return $"<b>{title}</b>\n{body}";
        }

        public void SetState(DoorVisualState state)
        {
            CurrentState = state;

            bool open    = state == DoorVisualState.Open;
            bool locked  = state == DoorVisualState.LockedCombat
                           || state == DoorVisualState.LockedSkillCheck;
            bool tapiada = state == DoorVisualState.Tapiada;

            // Mapa visual (CNF-012 v2 rev, feedback 2026-07-14): abierta = mesh open;
            // bloqueada/forzable = la reja; Tapiada (sin camino) = el zócalo que completa
            // la pared. La puerta sólida (_meshClosed) quedó sin estado — forzarla off pisa
            // el default activo del prefab.
            if (_meshOpen     != null) _meshOpen.SetActive(open);
            if (_meshClosed   != null) _meshClosed.SetActive(false);
            if (_wallPlug     != null) _wallPlug.SetActive(locked);
            if (_meshWallFill != null) _meshWallFill.SetActive(tapiada);

            ApplyTooltipGate();

            // Cartel de salida (solo DoorBoss.prefab lo trae): aparece con drop-in + bob
            // cuando esta puerta es la exit de piso y abre (boss derrotado).
            var exitSign = GetComponent<DoorExitSignView>();
            if (exitSign != null) exitSign.Apply(IsExit, state);
        }

        // CNF-012: una puerta tapiada (reja) no es una acción — el tooltip de
        // "Forzar Puerta" solo aplica a puertas reales. Null-safe: el trigger
        // recién existe después del Awake (EnsureTooltipComponents).
        private void ApplyTooltipGate()
        {
            var trigger = GetComponent<WorldTooltipTrigger>();
            if (trigger != null) trigger.enabled = CurrentState != DoorVisualState.Tapiada;
        }
    }

    /// <summary>
    /// Estados visuales de una puerta. El <c>DoorController</c> solo togglea
    /// los meshes; la lógica de locks vive en <c>DungeonManager</c> + los
    /// behaviors del InteractableComponent del prefab (§13.6 / §7.7).
    /// </summary>
    public enum DoorVisualState
    {
        /// <summary>Atravesable — sala actual Cleared o door <c>Forced</c>.</summary>
        Open = 0,

        /// <summary>Isaac-lock durante combate; se abre al <c>OnCombatEnd(Victory)</c>.</summary>
        LockedCombat = 1,

        /// <summary>Locked fuera de combate — sólo se abre con skill check exitoso.</summary>
        LockedSkillCheck = 2,

        /// <summary>No hay vecino por esta dirección — solo el zócalo (wall-fill) que completa la pared.</summary>
        Tapiada = 3,
    }
}
