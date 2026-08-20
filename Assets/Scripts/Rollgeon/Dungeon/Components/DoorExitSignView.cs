using Patterns;
using PrimeTween;
using Rollgeon.Grid;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Señalización de la salida de piso. Vive en el root del DoorBoss.prefab junto
    /// al <see cref="DoorController"/>, que lo notifica desde <c>SetState</c> (mismo
    /// patrón directo que el tooltip gate — inmune al gotcha de SetState corriendo
    /// antes del Awake en GOs inactivos). Solo aplica a la puerta exit designada por
    /// <c>MarkBossExitDoor</c> (una sola por piso) y recién cuando abre (boss
    /// derrotado). Las DoorBoss que llevan HACIA la boss room comparten el prefab
    /// pero nunca son IsExit.
    /// <para>
    /// El display ya no es el cartel 3D en mundo (no se leía bien en el mapa; el hijo
    /// ExitSign del DoorBoss queda inactivo): delega en <see cref="ExitSignIndicator"/>,
    /// que muestra el MISMO cartel pero bakeado a sprite y en screen-space, anclado al
    /// centro de la casilla frente a la puerta — la misma casilla que dispara la
    /// transición de piso (<c>DoorTileQuery.GetOpenExitDoorFrontTiles</c>).
    /// </para>
    /// <para>
    /// Gate por <c>isActiveAndEnabled</c> en <see cref="Show"/>: SetState corre con
    /// el root de la sala inactivo (Sync usa includeInactive) y, como el indicador NO
    /// es hijo del root, sin el gate la flecha se vería desde otra sala. <c>_shown</c>
    /// se setea igual y <c>OnEnable</c> muestra cuando el fog of war revela la sala.
    /// </para>
    /// </summary>
    [AddComponentMenu("Rollgeon/Dungeon/Door Exit Sign View")]
    public sealed class DoorExitSignView : MonoBehaviour
    {
        [Tooltip("Sprite del cartel (bake 2D del modelo ExitSign — lo genera el installer).")]
        [SerializeField] private Sprite _arrowSprite;

        [Header("Layout")]
        [Tooltip("Tamaño del cartel en unidades de canvas (ref. 1920x1080). El installer lo deriva del aspect del sprite bakeado.")]
        [SerializeField] private Vector2 _arrowSize = new Vector2(64f, 88f);
        [Tooltip("Separación entre la casilla y la base del cartel, en píxeles.")]
        [SerializeField] private float _gapPx = 64f;

        [Header("Drop-in")]
        [Tooltip("Píxeles desde los que cae la flecha al aparecer.")]
        [SerializeField] private float _dropPixels = 120f;
        [SerializeField] private float _dropDuration = 0.5f;
        [SerializeField] private Ease _dropEase = Ease.OutBack;

        [Header("Bob continuo")]
        [SerializeField] private float _bobAmplitudePx = 10f;
        [Tooltip("Duración de medio ciclo (subir o bajar).")]
        [SerializeField] private float _bobDuration = 0.6f;

        private bool _shown;
        private DoorController _door;

#if UNITY_EDITOR
        public const string EditorArrowSpriteField = nameof(_arrowSprite);
        public const string EditorArrowSizeField = nameof(_arrowSize);
        public const string EditorGapPxField = nameof(_gapPx);
        public Sprite EditorArrowSprite => _arrowSprite;
#endif

        /// <summary>Estado lógico del cartel (independiente de si el overlay pudo
        /// mostrarse ya — ver gate de <see cref="Show"/>). Observable para tests.</summary>
        public bool IsShowing => _shown;

        /// <summary>
        /// Sincroniza el indicador con el estado de la puerta. Idempotente: los Sync
        /// repetidos del DungeonManager no re-disparan la animación mientras el
        /// estado no cambie.
        /// </summary>
        public void Apply(bool isExit, DoorVisualState state)
        {
            bool show = isExit && state == DoorVisualState.Open;
            if (show == _shown) return;

            _shown = show;
            if (show) Show();
            else Hide();
        }

        private void OnEnable()
        {
            // Re-entrada a la boss room cleared: el fog of war reactiva el root
            // (RefreshRoomVisibility) y la flecha re-cae para volver a señalar.
            if (_shown) Show();
        }

        private void OnDisable()
        {
            // Cubre también la transición de piso: OnDisable corre al destruir.
            ExitSignIndicator.Hide(GetInstanceID());
        }

        private void Show()
        {
            // Sin play mode no hay overlay que crear (installers/tests EditMode);
            // con el root inactivo, mostrar recién cuando OnEnable revele la sala.
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            ExitSignIndicator.Show(GetInstanceID(), ResolveTargetWorldPos, _arrowSprite,
                new ExitSignIndicatorStyle(_arrowSize, _gapPx, _bobAmplitudePx, _bobDuration,
                    _dropPixels, _dropDuration, _dropEase));
        }

        private void Hide()
        {
            ExitSignIndicator.Hide(GetInstanceID());
        }

        // Evaluado por frame por el indicador: si el grid todavía no está registrado
        // en el primer Show (SetState corre durante el build del dungeon), el anchor
        // cae a la puerta y se corrige solo apenas el servicio aparece.
        private Vector3 ResolveTargetWorldPos()
        {
            // DoorController lazy — Apply puede correr antes que cualquier Awake.
            if (_door == null) _door = GetComponent<DoorController>();

            if (_door != null
                && ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                return ResolveFrontTileCenter(grid, transform.position, _door.Direction);
            }

            return transform.position;
        }

        /// <summary>
        /// Centro de la primera casilla interior frente a la puerta — misma
        /// convención que <c>DoorTileQuery</c> y el spawn de sala.
        /// </summary>
        internal static Vector3 ResolveFrontTileCenter(IGridManager grid, Vector3 doorWorldPos, DoorDirection direction)
        {
            return grid.GridToWorld(grid.WorldToGrid(doorWorldPos) + direction.InwardOffset());
        }
    }
}
