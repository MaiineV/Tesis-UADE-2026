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
    /// Not a <c>SerializedScriptableObject</c>: a flat data bag gains nothing from Odin polymorphism
    /// and would make every <c>.asset</c> carry a binary blob on top of the readable YAML. A room
    /// object that needs to decide anything belongs in <c>EnemyDataSO</c> instead.
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

        [Header("Owner armor")]
        [Tooltip("Fraction of incoming damage each one of these still standing takes off its OWNER. " +
                 "0 = no armor, which is the default: an object is terrain until someone says " +
                 "otherwise. Five of these at 0.14 make the boss take 70% less until you break them, " +
                 "and a broken one gives its share back for good. See RoomObjectArmorService.")]
        [Range(0f, 1f)]
        public float OwnerDamageReductionPerObject;

        /// <summary>
        /// Checked by the spawn node before publishing slot state to <c>RoomObjectArmorService</c>.
        /// </summary>
        public bool GrantsOwnerArmor => OwnerDamageReductionPerObject > 0f;

        /// <summary><see cref="DisplayName"/>, falling back to the asset name.</summary>
        public string EffectiveDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;

        /// <summary>
        /// <see cref="Hp"/> floored at 1. <c>[Min]</c> only constrains the Inspector drawer, and an
        /// object spawned at 0 HP is born dead: the spawner would break it, drop its
        /// <see cref="OnDeathHazard"/> and respawn it, forever.
        /// </summary>
        public int EffectiveHp => Hp < 1 ? 1 : Hp;

        /// <summary><c>false</c> when <see cref="RespawnDelayTurns"/> is negative — the object is
        /// gone for the rest of the fight once broken.</summary>
        public bool Respawns => RespawnDelayTurns >= 0;
    }
}
