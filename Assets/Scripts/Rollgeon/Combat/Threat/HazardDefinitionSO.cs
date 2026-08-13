using System;
using Rollgeon.Combat.Pipelines;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Data-driven definition of one environmental hazard type (rain, fire, falling debris, ...).
    /// Every field here used to be a hardcoded constant on <see cref="RainHazardService"/>;
    /// pulling them out lets <see cref="HazardService"/> run any number of hazard types from the
    /// same generic turn-cycle loop — adding a new one is "author a SO, point a boss node at it."
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a <c>SerializedScriptableObject</c>.</b> Odin polymorphic serialization is overkill
    /// for a flat data bag and would force every hand-authored <c>.asset</c> to carry Odin's
    /// binary blob — plain Unity fields keep the YAML readable and editable without the editor.
    /// </para>
    /// <para>
    /// <b><see cref="SourceId"/> is a string, not a <see cref="Guid"/>.</b> Unity's built-in
    /// serializer has no native support for <see cref="Guid"/> (no drawer, no YAML-friendly
    /// representation), so the field is authored as a string and parsed on demand via
    /// <see cref="SourceGuid"/>. Each definition needs a stable, unique id because
    /// <see cref="IThreatenedAreaService"/>/<see cref="IThreatOverlayService"/> key their state by
    /// source — two active hazards must never collide.
    /// </para>
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

        [Tooltip("Rounds the hazard survives once activated. 0 = never expires on its own (the " +
                 "historical rain behavior). Counted on round wrap, so a value of 1 means \"gone " +
                 "at the start of the next round\".")]
        [Min(0)]
        public int DurationRounds;

        [Tooltip("Ice: the tile that fired is removed from the area, so a trap is single-use per " +
                 "tile. When the last tile is consumed the whole instance dies.")]
        public bool ConsumeOnTrigger;

        [Header("Presentation")]
        [Tooltip("Colour of the telegraph quads for this hazard, so fire and ice don't read as the " +
                 "same threat. Alpha is driven by the overlay's pulse at runtime.")]
        public Color OverlayTint = DefaultOverlayTint;

        [Header("Identity")]
        [Tooltip("Stable unique id for this hazard source (GUID as string — see class remarks). " +
                 "Generate a fresh one per definition; never reuse another hazard's id.")]
        public string SourceId = Guid.NewGuid().ToString();

        /// <summary>Alias of <see cref="ThreatTelegraphOverlay.DefaultTint"/> — the orange painted for
        /// every threat before hazards could be tinted individually, and the default for
        /// <see cref="OverlayTint"/> so definitions authored before that field existed keep their exact
        /// look. Points at the overlay's constant rather than re-declaring the colour so the two can't
        /// drift apart.</summary>
        public static readonly Color DefaultOverlayTint = ThreatTelegraphOverlay.DefaultTint;

        /// <summary>
        /// <see cref="OverlayTint"/>, falling back to <see cref="DefaultOverlayTint"/> when it is
        /// fully transparent.
        /// </summary>
        /// <remarks>
        /// Guards the one case the field initializer can't: a hand-edited <c>.asset</c> that carries
        /// an explicit all-zero <c>OverlayTint</c> (or one written by a tool that serialized the
        /// struct default) would otherwise paint invisible quads and read as "the hazard is broken".
        /// The trade-off is that an intentionally invisible hazard isn't authorable — no design
        /// wants one today, and a silently missing telegraph is the far worse failure.
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
