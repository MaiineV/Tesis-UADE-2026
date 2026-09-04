using Rollgeon.Combat.Pipelines;
using UnityEngine;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Definición data-driven de una Casilla Especial (GDD Casillas Especiales). Un asset
    /// por tipo de casilla (Pinchos, Fuego, ...); las instancias vivas las administra
    /// <see cref="SpecialTileService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin Odin a propósito</b> (mismo criterio documentado en <c>HazardDefinitionSO</c>):
    /// data-bag chato, YAML legible y editable a mano. Los campos de parámetros son un
    /// superset — cada categoría lee solo los suyos y el resto queda en default.
    /// </para>
    /// <para>
    /// <b>Arte swapeable:</b> <see cref="VisualPrefab"/> es el anchor del modelo 3D futuro.
    /// Enchufar arte = asignar el prefab acá, sin tocar código; los cambios de estado
    /// (armado/desarmado/trigger) llegan por eventos (<c>SpecialTileVisualBinding</c>).
    /// Sin prefab, el fallback es el overlay tintado con <see cref="OverlayTint"/>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Tiles/Special Tile Definition", fileName = "SpecialTile")]
    public class SpecialTileDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Id estable del catálogo (TILE_SPIKES, TILE_FIRE...). Clave de autoría y persistencia.")]
        public string TileId;

        [Tooltip("Nombre para editor/debug. El nombre localizado para el jugador sale de NameKey.")]
        public string DisplayName;

        public SpecialTileType TileType;

        [Header("Rules")]
        [Tooltip("Máscara de triggers a los que reacciona (Fuego = OnEnter | OnTurnStart).")]
        public TileTrigger Triggers;

        [Tooltip("UNA sola categoría de efecto por casilla física (invariante del GDD §9).")]
        public TileEffectCategory Category;

        [Tooltip("GroundOnly = las voladoras la ignoran (Pinchos, Hielo, Veneno).")]
        public TileAffinity Affinity = TileAffinity.All;

        [Header("Damage")]
        public AttackKind DamageKind = AttackKind.Environmental;

        [Min(0)]
        [Tooltip("Daño al entrar/atravesar (Pinchos 12, Fuego 8, Fuego Temporal 6).")]
        public int EnterDamage;

        [Min(0)]
        [Tooltip("Daño recurrente al iniciar turno parado acá (Fuego 12, Fuego Temporal 10).")]
        public int TurnStartDamage;

        [Header("Heal")]
        [Min(0)]
        [Tooltip("Curación al terminar el turno sobre la casilla (Curación 12).")]
        public int HealAmount;

        [Header("Status")]
        public TileStatusKind StatusKind;

        [Min(0)]
        [Tooltip("Turnos del estado (Veneno 3, Charco 1).")]
        public int StatusTurns;

        [Min(0)]
        [Tooltip("Daño por tick del estado, si aplica (Veneno 5).")]
        public int StatusTickDamage;

        [Header("Buffs")]
        [Min(0)]
        [Tooltip("Fortaleza: daño plano extra al combo ofensivo mientras permanece (+10).")]
        public int ComboDamageBonus;

        [Min(0)]
        [Tooltip("Impulso: bono a la próxima tirada de movimiento (+2). INERTE hasta que exista tirada real.")]
        public int MoveRangeBonus;

        [Header("Lifecycle")]
        [Min(0)]
        [Tooltip("Rondas de vida por default. 0 = permanente. Cuenta en el wrap de ronda: 1 = desaparece al empezar la ronda siguiente.")]
        public int DefaultDurationRounds;

        [Tooltip("La celda se desarma al disparar y no vuelve a disparar hasta rearmarse (Pinchos).")]
        public bool DisarmOnTrigger;

        [Tooltip("Las celdas desarmadas se rearman en cada wrap de ronda (Pinchos: 'listo para el próximo ciclo').")]
        public bool RearmOnRoundWrap;

        [Tooltip("Regla de ownership: los jefes no son afectados por casillas cuyo owner es un jefe. " +
                 "Apagar esto es la 'disposición en contrario' del GDD §15.")]
        public bool OwnerBossImmune = true;

        [Tooltip("El dueño de la casilla y sus aliados no la activan (rastros del dado de Movimiento: " +
                 "'el jugador y sus aliados no reciben daño por sus propias llamas').")]
        public bool OwnerAndAlliesImmune;

        [Tooltip("Entrar en la casilla termina el movimiento de la unidad afectada (Sendero de espinas), " +
                 "igual que el hielo trunca el path. Se evalúa con los mismos filtros de afectación.")]
        public bool EndsMovementOnEnter;

        [Header("Telegraph")]
        [Tooltip("Qué casilla spawnea al vencer la advertencia (solo Category = Telegraph).")]
        public SpecialTileDefinitionSO TelegraphPayload;

        [Min(0)]
        [Tooltip("Rondas de advertencia (default GDD: 1 ronda completa).")]
        public int TelegraphRounds = 1;

        [Header("Teleport")]
        [Min(0)]
        [Tooltip("Turnos de 'recién teletransportado' tras usar este portal: mientras dure, pisar " +
                 "cualquier portal es celda común (no teleporta ni trunca el path). 0 = sin cooldown. " +
                 "Solo aplica en combate: en exploración no hay turnos que lo hagan expirar.")]
        public int TeleportCooldownTurns = 2;

        [Header("Safe Zone")]
        [Tooltip("De qué tipos de casilla protege esta zona (solo Category = ConditionalProtection). " +
                 "NO es inmunidad general: cada zona declara su lista.")]
        public SpecialTileType[] ProtectedTileTypes;

        [Header("Presentation")]
        [Tooltip("Anchor de arte: prefab instanciado por celda. Swap del modelo 3D sin tocar código.")]
        public GameObject VisualPrefab;

        [Tooltip("Offset Y del visual sobre el piso de la celda.")]
        public float VisualYOffset = 0.02f;

        [Tooltip("Fallback visual sin prefab: tinte del overlay de la celda.")]
        public Color OverlayTint = new Color(1f, 1f, 1f, 0.35f);

        [Tooltip("VFX one-shot al disparar el efecto (opcional).")]
        public GameObject TriggerVfxPrefab;

        [Min(0.1f)]
        public float TriggerVfxLifetime = 2f;

        public float TriggerVfxYOffset = 0.1f;

        [Header("AI (pathing)")]
        [Min(0)]
        [Tooltip("Daño VIRTUAL para el costo IA (Charco Eléctrico: 25 por el stun). No es daño " +
                 "real: solo encarece la ruta, no entra al filtro de supervivencia.")]
        public int AIVirtualEnterDamage;

        [Tooltip("Solo Telegraph: anuncia muerte/ataque letal — la IA bloquea la ruta salvo Kamikaze.")]
        public bool AIAnnouncesLethal;

        [Header("Editor (Room Tool)")]
        [Tooltip("Icono del botón en la paleta del Room Editor.")]
        public Texture2D EditorIcon;

        [Tooltip("Color del gizmo/paleta en el Room Editor.")]
        public Color EditorColor = Color.white;

        [Header("Localization")]
        [Tooltip("Key del nombre visible (tooltips). Sembrada por el seeder de localización.")]
        public string NameKey;

        [Tooltip("Key de la descripción visible (tooltips).")]
        public string DescriptionKey;
    }
}
