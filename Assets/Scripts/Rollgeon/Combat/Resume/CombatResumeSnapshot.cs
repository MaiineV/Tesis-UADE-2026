using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Resume
{
    /// <summary>
    /// Snapshot del estado efímero de un combate en curso (Feature#0028 Fase 3/4) —
    /// SaveKey <c>run.combat_state</c>. Sólo tiene contenido si se guardó durante una
    /// pelea (<see cref="Active"/>); si no, es un centinela inerte.
    /// <para>
    /// Los GUIDs (<see cref="Order"/>, <see cref="ActiveEntityId"/>,
    /// <see cref="EntityBuffBlock.EntityId"/>) son los <b>preservados</b> — el player y
    /// los enemigos re-spawnean en resume con esos mismos GUIDs, así que las referencias
    /// del snapshot matchean sin re-mapeo.
    /// </para>
    /// </summary>
    [Serializable]
    public class CombatResumeSnapshot
    {
        public bool Active;

        /// <summary>Celda de la sala del combate (clave estable — el InstanceId se regenera).</summary>
        public Vector2Int CurrentCell;

        /// <summary>Cola de turnos del round (GUIDs preservados como string).</summary>
        public List<string> Order = new List<string>();

        /// <summary>Índice del turno activo dentro de <see cref="Order"/>.</summary>
        public int Cursor;

        public int RoundIndex;

        /// <summary>GUID de la entidad cuyo turno estaba activo (validador / clamp).</summary>
        public string ActiveEntityId;

        /// <summary>
        /// GUID del player al guardar. En resume se re-mapea al GUID vivo del player
        /// (<c>Context.PlayerId</c>) — no dependemos de que la preservación cross-sesión
        /// del GUID haya funcionado. Fallback: <see cref="Order"/>[0] (CNF-006 fuerza al
        /// player al frente de la cola).
        /// </summary>
        public string PlayerGuid;

        /// <summary>
        /// Rolls del pool del jugador al guardar (Feature#0050). -1 = snapshot viejo
        /// sin el campo — el resume no restaura y queda el arranque-en-5 de
        /// <c>OnCombatStart</c>.
        /// </summary>
        public int PlayerRolls = -1;

        /// <summary>Buffs/debuffs/shields de combate por entidad (Fase 4), keyed por GUID preservado.</summary>
        public List<EntityBuffBlock> Buffs = new List<EntityBuffBlock>();

        // ---- Estado de boss (Fase 4) ------------------------------------
        // ContractModifierService NO se persiste: el boss AI (AINode_RotateBlock) lo re-deriva
        // del ComboHistory cada turno, así que restaurar el historial lo cubre.

        /// <summary>Historial de combos (más reciente primero) — alimenta la rotación de bloqueo del boss.</summary>
        public List<string> ComboHistory = new List<string>();

        /// <summary>Índices de dados bloqueados por el boss para el próximo turno del player.</summary>
        public List<int> BlockedDice = new List<int>();

        /// <summary>Áreas telegrafiadas pendientes (por fuente/boss).</summary>
        public List<TelegraphEntry> Telegraphs = new List<TelegraphEntry>();
    }

    /// <summary>Área amenazada pendiente de un boss (Feature#0028 Fase 4).</summary>
    [Serializable]
    public class TelegraphEntry
    {
        public string SourceId;
        public List<GridCoord> Tiles = new List<GridCoord>();
        public int Damage;
        public AttackKind Kind;
    }

    /// <summary>
    /// Buffs/debuffs de combate (modifiers lifetime <c>Turns</c>/<c>Encounter</c>) + valor del
    /// stat <c>Shield</c> de una entidad, keyed por su GUID preservado (Feature#0028 Fase 4).
    /// </summary>
    [Serializable]
    public class EntityBuffBlock
    {
        public string EntityId;
        public bool HasShield;
        public int Shield;
        public List<CombatStatEntry> Stats = new List<CombatStatEntry>();
    }

    /// <summary>Modifiers combat-scoped de un stat concreto de una entidad.</summary>
    [Serializable]
    public class CombatStatEntry
    {
        public string Stat;
        public List<CombatStatModifierEntry> Modifiers = new List<CombatStatModifierEntry>();
    }

    /// <summary>
    /// DTO de un <see cref="Modifier{T}"/> combat-scoped — mismo shape que
    /// <c>PlayerStatModifierEntry</c>. Al restaurar hay que llamar <c>Modifier.OnLoad()</c>
    /// para re-suscribir el <see cref="TickEvent"/> / <c>OnCombatEnd</c>.
    /// </summary>
    [Serializable]
    public class CombatStatModifierEntry
    {
        public int Amount;
        public ModifierOperation Operation;
        public int Duration;
        public string ModifierId;
        public string CarrierId;
        public string SourceId;
        public ModifierDirection Direction;
        public ModifierLifetime Lifetime;
        public EventName TickEvent;
    }
}
