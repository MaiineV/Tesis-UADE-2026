using Rollgeon.Combat.Threat;
using UnityEngine;

namespace Rollgeon.Combat.Rooms
{
    /// <summary>
    /// Data-driven definition of one destructible room object: a piece of the room that sits on a
    /// tile, blocks it, takes damage and breaks — La Bandida's reels, La Generala's dice — without
    /// being a combatant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the type exists.</b> Those objects ship today as <c>EnemyDataSO</c> pushed through a
    /// reinforcement-shaped spawn node, so they inherit an enemy portrait, an enemy health bar and a
    /// slot in the turn queue. The design asks for none of that — it is what being an enemy happens
    /// to drag along. This is the sibling of <see cref="HazardDefinitionSO"/> for the other half of
    /// room state: a hazard owns tiles that <i>hurt</i>, this owns tiles that are <i>taken</i>.
    /// </para>
    /// <para>
    /// <b>Not a <c>SerializedScriptableObject</c></b>, for the same reason
    /// <see cref="HazardDefinitionSO"/> isn't: a flat data bag gains nothing from Odin polymorphism
    /// and would force every <c>.asset</c> to carry Odin's binary blob on top of the readable YAML.
    /// </para>
    /// <para>
    /// <b>No stats beyond <see cref="Hp"/>.</b> No Speed, no Attack, no AI tree. A room object that
    /// needs to decide anything is an enemy wearing furniture, and belongs in <c>EnemyDataSO</c>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Room Object Definition", fileName = "RoomObjectDefinition")]
    public class RoomObjectDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Canonical id, e.g. 'roomobj.bandida.reel'. Stable and designer-readable — logs and " +
                 "boss code name the object by this, never by asset filename.")]
        public string Id;

        [Tooltip("Readable name for UI and logs.")]
        public string DisplayName;

        [Header("Presence")]
        [Tooltip("Hit points the object spawns with, and the only stat it has: a room object is a " +
                 "wall with a health pool.")]
        [Min(1)]
        public int Hp = 50;

        [Tooltip("Takes its tile on the occupancy map, so nothing can walk or spawn through it. " +
                 "Off = the object stands on the tile without closing it — breakable decor. It stays " +
                 "clickable either way: targeting picks the pawn, not the tile.")]
        public bool Blocks = true;

        [Tooltip("Keeps the object out of the initiative queue: no slot, no portrait, no turn of its " +
                 "own. This is the point of the type — furniture that reads as furniture. Turn it " +
                 "off only for an object that genuinely has to act.")]
        public bool HideFromTurnQueue = true;

        [Header("Respawn")]
        [Tooltip("Boss turns the slot stays empty after the object breaks, counting the turn the " +
                 "break is noticed; then an identical object returns to the SAME tile. 2 = back on " +
                 "the second boss turn after the one that noticed. 0 = back on that same turn. " +
                 "-1 = broken is permanent.")]
        [Min(-1)]
        public int RespawnDelayTurns = 2;

        [Header("Presentation")]
        [Tooltip("Prefab instanced as the object's pawn. Spawned as a prop, never as an enemy, so it " +
                 "gets no enemy health bar — whatever gauge the object shows lives in this prefab.")]
        public GameObject VisualPrefab;

        [Tooltip("Only read when HideFromTurnQueue is off: the queue slot resolves its icon by guid " +
                 "and renders blank for anything that never registered one.")]
        public Sprite Portrait;

        [Header("On death")]
        [Tooltip("Hazard activated over the tile the object was holding when it breaks — the broken " +
                 "reel leaving fire behind. Empty = breaking it leaves clean floor.")]
        public HazardDefinitionSO OnDeathHazard;

        /// <summary>
        /// <see cref="DisplayName"/>, falling back to the asset name when it was never authored, so
        /// UI and logs always have something to print instead of an empty string.
        /// </summary>
        public string EffectiveDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;

        /// <summary>
        /// <see cref="Hp"/> floored at 1.
        /// </summary>
        /// <remarks>
        /// <c>[Min]</c> only constrains the Inspector drawer — an asset written by an editor builder
        /// or by hand can still hold 0. An object spawned at 0 HP is born dead, so the spawner would
        /// break it, drop its <see cref="OnDeathHazard"/> and respawn it, forever. Floor the value
        /// instead: an unbreakable-looking object is a bug someone reports, an infinite fire loop is
        /// a hang.
        /// </remarks>
        public int EffectiveHp => Hp < 1 ? 1 : Hp;

        /// <summary><c>false</c> when <see cref="RespawnDelayTurns"/> is negative — the object is
        /// gone for the rest of the fight once broken.</summary>
        public bool Respawns => RespawnDelayTurns >= 0;
    }
}
