using System;
using System.Collections.Generic;
using Patterns;
using Patterns.Save;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combat.Resume;
using Rollgeon.Combat.Threat;
using Rollgeon.Dungeon;
using Rollgeon.Phase;
using Rollgeon.Player;

namespace Rollgeon.Run
{
    /// <summary>
    /// Persistencia + restauración del estado de un combate en curso (Feature#0028 Fase 3/4).
    /// Es a la vez el <see cref="ISaveable"/> del estado (<c>run.combat_state</c>) y el
    /// <see cref="ICombatResumeCoordinator"/> que <c>CombatEnterState</c> consulta al arrancar.
    /// <para>
    /// <b>Stage-then-apply.</b> En resume, el auto-restore de <see cref="SaveSystem.Register"/>
    /// corre en <c>RunController.OnRunStart</c> — mucho antes de que existan los enemigos y la
    /// FSM. Por eso <see cref="RestoreState"/> sólo <b>stagea</b> el snapshot; la aplicación real
    /// ocurre en <see cref="TryBeginResume"/> cuando la FSM arranca el combate.
    /// </para>
    /// </summary>
    public sealed class CombatResumeService : ISaveable, ICombatResumeCoordinator, IDisposable
    {
        public const string SaveKeyConst = "run.combat_state";
        public string SaveKey => SaveKeyConst;

        private CombatResumeSnapshot _pending;

        // Último snapshot de combate válido — se reusa si el capture corre con el player ya
        // limpiado (EndRun llama ClearPlayer antes del capture de OnRunEnd, #0028).
        private CombatResumeSnapshot _lastState;

        // ================================================================
        // ISaveable
        // ================================================================

        /// <summary>
        /// Captura el estado de turno si hay combate activo; si no, un centinela
        /// <see cref="CombatResumeSnapshot.Active"/>=false (el saveable siempre está registrado).
        /// </summary>
        public object CaptureState()
        {
            // Player ya limpiado (teardown de EndRun corre ClearPlayer antes del capture de
            // OnRunEnd): preservar el último snapshot de combate válido en vez de descartarlo
            // por no poder leer la energía del player (#0028). Solo aplica si el servicio
            // existe pero el guid es Empty — si no está registrado (tests) seguimos normal.
            bool serviceRegistered = ServiceLocator.TryGetService<IPlayerService>(out var pv) && pv != null;
            if (serviceRegistered && pv.PlayerGuid == Guid.Empty)
                return _lastState ?? new CombatResumeSnapshot();

            var snap = BuildSnapshot();
            if (serviceRegistered && pv.PlayerGuid != Guid.Empty)
                _lastState = snap.Active ? snap : null;
            return snap;
        }

        private CombatResumeSnapshot BuildSnapshot()
        {
            var snap = new CombatResumeSnapshot();

            if (!ServiceLocator.TryGetService<IPhaseService>(out var phase) || phase == null
                || phase.CurrentBase != GamePhase.Combat)
                return snap;
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null
                || turnOrder.ParticipantCount == 0)
                return snap;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                || dungeon?.CurrentRoomInstance == null)
                return snap;

            snap.Active = true;
            snap.CurrentCell = dungeon.CurrentRoomInstance.GridCell;
            snap.RoundIndex = turnOrder.RoundIndex;

            foreach (var g in turnOrder.OrderForRound) snap.Order.Add(g.ToString());
            var active = turnOrder.Current;
            snap.ActiveEntityId = active.ToString();
            snap.Cursor = IndexOf(turnOrder.OrderForRound, active);

            if (ServiceLocator.TryGetService<IPlayerService>(out var player) && player != null
                && player.PlayerGuid != Guid.Empty)
            {
                snap.PlayerGuid = player.PlayerGuid.ToString();
                if (ServiceLocator.TryGetService<IRollPoolService>(out var rolls) && rolls != null)
                    snap.PlayerRolls = rolls.GetCurrent(player.PlayerGuid);
            }

            // Buffs/debuffs/shields por entidad (Fase 4): player + enemigos vivos de la cola.
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrsMgr) && attrsMgr != null)
            {
                foreach (var g in turnOrder.OrderForRound)
                {
                    var block = CaptureEntityBuffs(attrsMgr, g);
                    if (block != null) snap.Buffs.Add(block);
                }
            }

            CaptureBossState(snap);
            return snap;
        }

        private static void CaptureBossState(CombatResumeSnapshot snap)
        {
            if (ServiceLocator.TryGetService<IComboLogService>(out var comboLog) && comboLog != null)
                foreach (var c in comboLog.Last(int.MaxValue)) snap.ComboHistory.Add(c);

            if (ServiceLocator.TryGetService<IDiceBlockService>(out var diceBlock) && diceBlock != null)
                foreach (var i in diceBlock.BlockedIndices) snap.BlockedDice.Add(i);

            if (ServiceLocator.TryGetService<IThreatenedAreaService>(out var threatIf)
                && threatIf is ThreatenedAreaService threat)
            {
                foreach (var area in threat.SnapshotPending())
                {
                    var t = new TelegraphEntry
                    {
                        SourceId = area.SourceGuid.ToString(),
                        Damage = area.Damage,
                        Kind = area.Kind,
                    };
                    if (area.Tiles != null) foreach (var tile in area.Tiles) t.Tiles.Add(tile);
                    snap.Telegraphs.Add(t);
                }
            }
        }

        /// <summary>Sólo stagea — la aplicación real es <see cref="TryBeginResume"/>.</summary>
        public void RestoreState(object state)
        {
            if (state is CombatResumeSnapshot snap) _pending = snap;
        }

        // ================================================================
        // ICombatResumeCoordinator
        // ================================================================

        public bool TryBeginResume(TurnOrderService turnOrder, IReadOnlyList<Guid> liveParticipants,
            Guid livePlayerId)
        {
            var snap = _pending;
            if (snap == null || !snap.Active) return false;
            _pending = null; // one-shot consume

            if (turnOrder == null) return false;

            // Match por celda (el InstanceId se regenera).
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                || dungeon?.CurrentRoomInstance == null
                || dungeon.CurrentRoomInstance.GridCell != snap.CurrentCell)
                return false;

            // Re-map del GUID del player: el guardado (snap.PlayerGuid, o Order[0] por CNF-006)
            // se traduce al GUID vivo. Los enemigos ya spawnean con su GUID preservado (Fase 2),
            // así que sólo el player necesita remap — no dependemos de que su preservación
            // cross-sesión haya funcionado.
            Guid savedPlayer = ParseGuid(snap.PlayerGuid);
            if (savedPlayer == Guid.Empty && snap.Order != null && snap.Order.Count > 0)
                savedPlayer = ParseGuid(snap.Order[0]);
            Guid Remap(Guid g) => (savedPlayer != Guid.Empty && g == savedPlayer) ? livePlayerId : g;

            // Reconstruye la cola (re-mapeando el player), filtrando a los vivos — un enemigo
            // del save que no respawneó (murió) se descarta.
            var live = new HashSet<Guid>();
            if (liveParticipants != null) foreach (var g in liveParticipants) live.Add(g);

            var order = new List<Guid>();
            if (snap.Order != null)
                foreach (var s in snap.Order)
                {
                    var g = Remap(ParseGuid(s));
                    if (g != Guid.Empty && live.Contains(g)) order.Add(g);
                }
            if (order.Count == 0) return false; // nada consistente que restaurar

            // BUG-078: si hay enemigos VIVOS entre los participantes pero la cola
            // restaurada quedó solo-player (el GUID del enemigo guardado no matchea el
            // respawneado), restaurarla bypassearía el guard de no-combatants de
            // CombatEnterState — CachedParticipants sí tiene al boss, pero la cola
            // ciclaría al player solo. Devolvemos false para que el caller arme una cola
            // fresca con los vivos (BuildForCombat). Si NO hay enemigos vivos, no es este
            // caso: el guard de CombatEnterState ya lo corta antes de llegar acá.
            bool hasLiveNonPlayer = false;
            foreach (var g in live)
            {
                if (g != livePlayerId) { hasLiveNonPlayer = true; break; }
            }
            if (hasLiveNonPlayer)
            {
                bool orderHasNonPlayer = false;
                foreach (var g in order)
                {
                    if (g != livePlayerId) { orderHasNonPlayer = true; break; }
                }
                if (!orderHasNonPlayer)
                {
                    UnityEngine.Debug.LogWarning(
                        "[CombatResumeService] Cola restaurada sin los enemigos vivos (GUIDs del " +
                        "save no matchean los respawneados) — descartando snapshot, cola fresca (BUG-078).");
                    return false;
                }
            }

            // Clampea el cursor a la entidad activa sobreviviente.
            Guid activeGuid = Remap(ParseGuid(snap.ActiveEntityId));
            int cursor = order.IndexOf(activeGuid);
            if (cursor < 0) cursor = 0;

            turnOrder.RestoreState(order, cursor, snap.RoundIndex);

            // Pool de rolls del player: sobrescribe el arranque-en-5 de OnCombatStart.
            // PlayerRolls == -1 = snapshot viejo sin el campo — no restaurar.
            if (snap.PlayerRolls >= 0
                && ServiceLocator.TryGetService<IPlayerService>(out var player) && player != null
                && player.PlayerGuid != Guid.Empty
                && ServiceLocator.TryGetService<IRollPoolService>(out var rollPool) && rollPool != null)
            {
                rollPool.RestoreCurrent(player.PlayerGuid, snap.PlayerRolls);
            }

            // Buffs/debuffs/shields por entidad (Fase 4): se aplican sobre las entidades ya
            // spawneadas con GUID preservado. El shield del que arranca su turno lo resetea
            // ShieldResetHandler (correcto); los de las demás entidades sobreviven.
            if (snap.Buffs != null && snap.Buffs.Count > 0
                && ServiceLocator.TryGetService<AttributesManager>(out var buffAttrs) && buffAttrs != null)
            {
                foreach (var block in snap.Buffs)
                {
                    if (block != null)
                    {
                        var g = Remap(ParseGuid(block.EntityId));
                        if (g != Guid.Empty) block.EntityId = g.ToString();
                    }
                    RestoreEntityBuffs(buffAttrs, block);
                }
            }

            RestoreBossState(snap);

            return true;
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static int IndexOf(IReadOnlyList<Guid> list, Guid g)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == g) return i;
            return 0;
        }

        // ---- Buffs/shields (Fase 4) --------------------------------------

        private static EntityBuffBlock CaptureEntityBuffs(AttributesManager attrs, Guid guid)
        {
            if (guid == Guid.Empty || !attrs.IsRegistered(guid)) return null;

            var block = new EntityBuffBlock { EntityId = guid.ToString() };
            bool any = false;

            var shield = attrs.GetAttribute<Shield>(guid) as BaseAttribute<int>;
            if (shield != null)
            {
                block.HasShield = true;
                block.Shield = shield.Value;
                if (shield.Value != 0) any = true;
            }

            any |= CaptureMods(attrs.GetAttribute<Health>(guid) as BaseAttribute<int>, nameof(Health), block);
            any |= CaptureMods(attrs.GetAttribute<Attack>(guid) as BaseAttribute<int>, nameof(Attack), block);
            any |= CaptureMods(attrs.GetAttribute<Speed>(guid) as BaseAttribute<int>, nameof(Speed), block);
            any |= CaptureMods(shield, nameof(Shield), block);

            return any ? block : null;
        }

        /// <summary>Captura solo los modifiers combat-scoped (Turns/Encounter) del stat.</summary>
        private static bool CaptureMods(BaseAttribute<int> attr, string statName, EntityBuffBlock block)
        {
            if (attr == null) return false;

            CombatStatEntry entry = null;
            foreach (var mod in attr.GetRawModifiers())
            {
                if (mod.Lifetime != ModifierLifetime.Turns && mod.Lifetime != ModifierLifetime.Encounter)
                    continue;
                entry ??= new CombatStatEntry { Stat = statName };
                entry.Modifiers.Add(new CombatStatModifierEntry
                {
                    Amount = mod.Amount,
                    Operation = mod.Operation,
                    Duration = mod.Duration,
                    ModifierId = mod.ModifierId.ToString(),
                    CarrierId = mod.CarrierId.ToString(),
                    SourceId = mod.SourceId.ToString(),
                    Direction = mod.Direction,
                    Lifetime = mod.Lifetime,
                    TickEvent = mod.TickEvent,
                });
            }

            if (entry == null) return false;
            block.Stats.Add(entry);
            return true;
        }

        private static void RestoreEntityBuffs(AttributesManager attrs, EntityBuffBlock block)
        {
            if (block == null || !Guid.TryParse(block.EntityId, out var guid)) return;
            if (!attrs.IsRegistered(guid)) return; // enemigo muerto / no respawneó

            if (block.HasShield && attrs.GetAttribute<Shield>(guid) is BaseAttribute<int> shield)
                shield.Value = block.Shield;

            if (block.Stats == null) return;
            foreach (var entry in block.Stats)
            {
                var attr = ResolveStat(attrs, guid, entry.Stat);
                if (attr == null) continue;
                foreach (var m in entry.Modifiers)
                {
                    var mod = new Modifier<int>
                    {
                        Amount = m.Amount,
                        Operation = m.Operation,
                        Duration = m.Duration,
                        ModifierId = ParseGuid(m.ModifierId),
                        CarrierId = ParseGuid(m.CarrierId),
                        SourceId = ParseGuid(m.SourceId),
                        Direction = m.Direction,
                        Lifetime = m.Lifetime,
                        TickEvent = m.TickEvent,
                    };
                    attr.AddModifier<int>(mod);
                    mod.OnLoad(); // re-hook de eventos post-deserialize (§3.5)
                }
            }
        }

        private static BaseAttribute<int> ResolveStat(AttributesManager attrs, Guid guid, string name) => name switch
        {
            nameof(Health) => attrs.GetAttribute<Health>(guid) as BaseAttribute<int>,
            nameof(Attack) => attrs.GetAttribute<Attack>(guid) as BaseAttribute<int>,
            nameof(Speed) => attrs.GetAttribute<Speed>(guid) as BaseAttribute<int>,
            nameof(Shield) => attrs.GetAttribute<Shield>(guid) as BaseAttribute<int>,
            _ => null,
        };

        private static Guid ParseGuid(string s) => Guid.TryParse(s, out var g) ? g : Guid.Empty;

        // ---- Estado de boss (Fase 4) -------------------------------------

        private static void RestoreBossState(CombatResumeSnapshot snap)
        {
            if (snap.ComboHistory != null && snap.ComboHistory.Count > 0
                && ServiceLocator.TryGetService<IComboLogService>(out var comboLog) && comboLog != null)
            {
                comboLog.Clear();
                // History[0] = más reciente; Record inserta al frente → recorrer de viejo a nuevo.
                for (int i = snap.ComboHistory.Count - 1; i >= 0; i--)
                    comboLog.Record(snap.ComboHistory[i]);
            }

            if (snap.BlockedDice != null && snap.BlockedDice.Count > 0
                && ServiceLocator.TryGetService<IDiceBlockService>(out var diceBlock) && diceBlock != null)
            {
                diceBlock.Clear();
                foreach (var i in snap.BlockedDice) diceBlock.Block(i);
            }

            if (snap.Telegraphs != null && snap.Telegraphs.Count > 0
                && ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) && threat != null)
            {
                foreach (var t in snap.Telegraphs)
                    if (Guid.TryParse(t.SourceId, out var src))
                        threat.Mark(src, t.Tiles, t.Damage, t.Kind);
            }
        }

        public void Dispose()
        {
            SaveSystem.Unregister(this);
        }
    }
}
