using System;
using Rollgeon.Combat.Pipelines;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Data-driven definition of one environmental hazard type (rain, fire, falling debris, ...).
    /// <see cref="HazardService"/> runs any number of them off the same generic turn-cycle loop.
    /// </summary>
    /// <remarks>
    /// <see cref="SourceId"/> is authored as a string because Unity's serializer has no native
    /// support for <see cref="Guid"/>, and it must be stable and unique per definition:
    /// <see cref="IThreatenedAreaService"/>/<see cref="IThreatOverlayService"/> key their state by
    /// source, so two active hazards sharing an id overwrite each other.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Hazard Definition", fileName = "HazardDefinition")]
    public class HazardDefinitionSO : ScriptableObject
    {
        [Header("Area")]
        [Tooltip("Shape of the telegraphed area. ScatteredSquares = rain-style independent zones; " +
                 "see ThreatShape for the rest.")]
        public ThreatShape Shape = ThreatShape.ScatteredSquares;

        [Tooltip("Radius for Square*/SquareAroundSelf, band width for Row/Column/DirectionalBand, " +
                 "or square width for ScatteredSquares. Ignored for HalfRoom.")]
        [Min(0)]
        public int Size = 1;

        [Tooltip("Depth (in tiles) of the directional band. Only used when Shape = DirectionalBand.")]
        [Min(1)]
        public int Depth = 2;

        [Tooltip("Number of independent squares to scatter. Only used when Shape = ScatteredSquares.")]
        [Min(1)]
        public int Count = 3;

        [Tooltip("Cut axis for HalfRoom. Ignored for every other shape.")]
        public HalfRoomAxis HalfAxis = HalfRoomAxis.Vertical;

        [Header("Damage")]
        [Tooltip("Damage applied next cycle if the player is still standing in the marked area.")]
        [Min(0)]
        public int Damage = 10;

        [Tooltip("AttackKind passed to the DamageContext when the telegraph resolves.")]
        public AttackKind Kind = AttackKind.Environmental;

        [Header("Cadence")]
        [Tooltip("Telegraph/resolve every N rounds (turn-queue round index, not per-hazard elapsed " +
                 "rounds — matches the original rain cadence: round % CycleRounds == 0). " +
                 "Only used when Trigger = CycleTelegraph.")]
        [Min(1)]
        public int CycleRounds = 2;

        [Header("Trigger")]
        [Tooltip("When this hazard hurts someone. CycleTelegraph = rain (mark now, resolve next " +
                 "cycle, uses Shape). OnTurnEndInTile = fire (hits whoever ends their turn on a " +
                 "tile). OnEnter = ice (hits on stepping in, scans the whole path). The last two " +
                 "need a dynamic area — activate them with the tiles overload of IHazardService.")]
        public HazardTriggerMode Trigger = HazardTriggerMode.CycleTelegraph;

        [Tooltip("Who pays this hazard. PlayerOnly = solo el jugador, así un jefe que cubre el " +
                 "paño con su propio fuego no se quema al terminar el turno adentro. Everyone = " +
                 "cualquier entidad que pise o cierre turno en una casilla, jugador incluido.")]
        public HazardAffects Affects = HazardAffects.PlayerOnly;

        [Tooltip("Rounds the hazard survives once activated. 0 = never expires on its own. " +
                 "Counted on round wrap, so a value of 1 means \"gone at the start of the next " +
                 "round\".")]
        [Min(0)]
        public int DurationRounds;

        [Tooltip("Ice: the tile that fired is removed from the area, so a trap is single-use per " +
                 "tile. When the last tile is consumed the whole instance dies.")]
        public bool ConsumeOnTrigger;

        [Header("Presentation")]
        [Tooltip("Colour of the telegraph quads for this hazard, so fire and ice don't read as the " +
                 "same threat. Alpha is driven by the overlay's pulse at runtime.")]
        public Color OverlayTint = DefaultOverlayTint;

        [Tooltip("Optional one-shot VFX spawned on the tile that fired. Leave empty for the " +
                 "tinted-quad-only look every hazard had before this field existed — the quad is " +
                 "the telegraph, this is the payoff.")]
        public GameObject TriggerVfxPrefab;

        [Tooltip("Seconds before the spawned TriggerVfxPrefab is destroyed. The project's VFX " +
                 "prefabs are ParticleSystems with stopAction = None, so nobody destroys them on " +
                 "their own and a hazard walked over ten times would leak ten of them.")]
        [Min(0.1f)]
        public float TriggerVfxLifetime = 2f;

        [Tooltip("Height above the tile where the trigger VFX spawns. Matches the overlay quad's " +
                 "own lift off the floor so the burst reads as coming out of the marked tile.")]
        public float TriggerVfxYOffset = 0.1f;

        [Tooltip("Optional VFX kept alive on every tile while the hazard lasts. The trigger burst " +
                 "above is the payoff for stepping in; this is what says 'this tile is on fire " +
                 "right now' between steps. Leave empty for the tinted quad alone.")]
        public GameObject PersistentVfxPrefab;

        [Tooltip("Height above the tile where the persistent VFX sits. Separate from the trigger " +
                 "offset because a standing flame reads better lifted than a ground burst.")]
        public float PersistentVfxYOffset = 0.1f;

        [Header("Identity")]
        [Tooltip("Stable unique id for this hazard source (GUID as string — see class remarks). " +
                 "Generate a fresh one per definition; never reuse another hazard's id.")]
        public string SourceId = Guid.NewGuid().ToString();

        [Tooltip("Nombre para editor/debug y fallback del tooltip. El nombre localizado sale de NameKey.")]
        public string DisplayName;

        [Tooltip("Key del nombre visible (tooltips). Sembrada por el seeder de localización.")]
        public string NameKey;

        [Tooltip("Key de la descripción visible (tooltips).")]
        public string DescriptionKey;

        /// <summary>Alias of <see cref="ThreatTelegraphOverlay.DefaultTint"/> and the default for
        /// <see cref="OverlayTint"/>. Points at the overlay's constant rather than re-declaring the
        /// colour so the two can't drift apart.</summary>
        public static readonly Color DefaultOverlayTint = ThreatTelegraphOverlay.DefaultTint;

        /// <summary>
        /// <see cref="OverlayTint"/>, falling back to <see cref="DefaultOverlayTint"/> when it is
        /// fully transparent.
        /// </summary>
        /// <remarks>
        /// A hand-edited <c>.asset</c> carrying an all-zero <c>OverlayTint</c> would otherwise paint
        /// invisible quads.
        /// </remarks>
        public Color EffectiveOverlayTint => OverlayTint.a <= 0f ? DefaultOverlayTint : OverlayTint;

        private Guid? _sourceGuidCache;

        /// <summary><see cref="SourceId"/> parsed to a <see cref="Guid"/>. Cached after first read
        /// since it never changes at runtime; invalidated if the field is edited in the Inspector.</summary>
        public Guid SourceGuid
        {
            get
            {
                if (_sourceGuidCache.HasValue) return _sourceGuidCache.Value;

                if (!Guid.TryParse(SourceId, out var parsed))
                {
                    Debug.LogError($"[HazardDefinitionSO] '{name}' has an invalid SourceId ('{SourceId}') — " +
                                    "hazard will not activate correctly.");
                    parsed = Guid.Empty;
                }

                _sourceGuidCache = parsed;
                return parsed;
            }
        }

        private void OnValidate() => _sourceGuidCache = null;
    }
}
