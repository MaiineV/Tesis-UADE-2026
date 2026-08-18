using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using UnityEngine;

namespace Rollgeon.Combat.Rooms
{
    /// <summary>
    /// Mientras los objetos de sala de un jefe sigan en pie, el daño que él recibe se reduce en
    /// <see cref="RoomObjectDefinitionSO.OwnerDamageReductionPerObject"/> por objeto. Romper uno se
    /// lo devuelve al jugador <b>para siempre</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El latch es monótono y va por <b>índice</b> de ranura, no por guid: el objeto repuesto vuelve
    /// a bloquear el paso, pero su parte de la reducción no vuelve.
    /// </para>
    /// <para>
    /// <b>Se evalúa al consultar, no al publicar.</b> El nodo del jefe tickea en el turno del jefe,
    /// y congelar la cuenta ahí haría que romper un objeto en el turno del jugador no bajara la
    /// reducción hasta el turno siguiente. Un objeto enterrado conserva su <see cref="Health"/> en
    /// &lt;= 0 (<c>CombatDeathWatcher</c> no lo desregistra de <see cref="AttributesManager"/>), así
    /// que HP &lt;= 0 o sin registro = roto.
    /// </para>
    /// <para>
    /// Global, y olvida mesas al terminar la pelea: una tabla que sobreviva le daría armadura a un
    /// guid que ya no existe.
    /// </para>
    /// </remarks>
    public sealed class RoomObjectArmorService : IIncomingDamageMultiplierProvider, IDisposable
    {
        /// <summary>Techo (0.9 = pega el 10%): ninguna definición puede volver al jefe invulnerable.</summary>
        public const float MaxReduction = 0.9f;

        private readonly Dictionary<Guid, Table> _tables = new Dictionary<Guid, Table>();

        private EventManager.EventReceiver _onScopeEnded;
        private bool _disposed;

        public RoomObjectArmorService()
        {
            _onScopeEnded = ForgetAllExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, _onScopeEnded);
        }

        /// <summary>Devuelve el registrado o crea y registra uno nuevo (Global).</summary>
        public static RoomObjectArmorService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IIncomingDamageMultiplierProvider>(out var existing)
                && existing is RoomObjectArmorService armor)
            {
                return armor;
            }

            var created = new RoomObjectArmorService();
            ServiceLocator.AddService<IIncomingDamageMultiplierProvider>(created, ServiceScope.Global);
            return created;
        }

        // ======================================================================
        // Publicación
        // ======================================================================

        /// <summary>
        /// <paramref name="slotGuids"/> va <b>por índice</b>: <see cref="Guid.Empty"/> en una ranura
        /// vacía. Idempotente, pensado para llamarse todos los turnos del jefe.
        /// </summary>
        /// <param name="reductionPerObject">Descuento de cada ranura nunca rota; 0 o menos borra la
        /// mesa.</param>
        public void Publish(Guid ownerGuid, IReadOnlyList<Guid> slotGuids, float reductionPerObject)
        {
            if (ownerGuid == Guid.Empty) return;

            if (slotGuids == null || slotGuids.Count == 0 || reductionPerObject <= 0f)
            {
                _tables.Remove(ownerGuid);
                return;
            }

            if (!_tables.TryGetValue(ownerGuid, out var table))
            {
                table = new Table(slotGuids.Count);
                _tables[ownerGuid] = table;
            }

            table.ReductionPerObject = reductionPerObject;
            table.SetSlots(slotGuids);
        }

        /// <summary>Olvida la mesa de un jefe. La llama el fin de pelea; nadie más debería.</summary>
        public void Forget(Guid ownerGuid) => _tables.Remove(ownerGuid);

        // ======================================================================
        // IIncomingDamageMultiplierProvider
        // ======================================================================

        /// <inheritdoc />
        public bool TryGetMultiplier(Guid targetId, out float multiplier)
        {
            multiplier = 1f;
            if (!_tables.TryGetValue(targetId, out var table)) return false;

            float reduction = ResolveReduction(table);
            if (reduction <= 0f) return false;

            multiplier = 1f - reduction;
            return true;
        }

        /// <summary>
        /// En 0..<see cref="MaxReduction"/>. Pública para que el número que ve el jugador salga de
        /// la misma cuenta que le baja la vida.
        /// </summary>
        public float ReductionFor(Guid ownerGuid) =>
            _tables.TryGetValue(ownerGuid, out var table) ? ResolveReduction(table) : 0f;

        /// <summary>Ranuras que nunca se rompieron.</summary>
        public int IntactCountFor(Guid ownerGuid)
        {
            if (!_tables.TryGetValue(ownerGuid, out var table)) return 0;

            LatchBroken(table);
            return table.IntactCount();
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onScopeEnded != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onScopeEnded);
                EventManager.UnSubscribe(EventName.OnRunEnd, _onScopeEnded);
                _onScopeEnded = null;
            }
            _tables.Clear();
        }

        private void ForgetAllExternal(params object[] args) => _tables.Clear();

        // ======================================================================
        // Internals
        // ======================================================================

        private float ResolveReduction(Table table)
        {
            LatchBroken(table);

            float reduction = table.ReductionPerObject * table.IntactCount();
            if (reduction < 0f) return 0f;
            return reduction > MaxReduction ? MaxReduction : reduction;
        }

        /// <summary>
        /// Marca como rota —para siempre— toda ranura sin vida. Sin <see cref="AttributesManager"/>
        /// no se latchea nada: fallar hacia "el jefe conserva su armadura", no hacia regalar la
        /// pelea.
        /// </summary>
        private static void LatchBroken(Table table)
        {
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return;

            for (int i = 0; i < table.Slots.Length; i++)
            {
                if (table.EverBroken[i]) continue;

                var guid = table.Slots[i];
                if (guid == Guid.Empty) continue; // Nunca se llenó: todavía no es "rota".

                var health = attrs.GetAttribute<Health>(guid);
                if (health == null || health.Value <= 0) table.EverBroken[i] = true;
            }
        }

        /// <summary>
        /// Los arrays crecen pero nunca se encogen: perder el latch de una ranura le devolvería
        /// armadura ya pagada.
        /// </summary>
        private sealed class Table
        {
            public Guid[] Slots;
            public bool[] EverBroken;
            public float ReductionPerObject;

            public Table(int size)
            {
                Slots = new Guid[size];
                EverBroken = new bool[size];
            }

            public void SetSlots(IReadOnlyList<Guid> slotGuids)
            {
                if (slotGuids.Count > Slots.Length) Grow(slotGuids.Count);

                for (int i = 0; i < slotGuids.Count; i++) Slots[i] = slotGuids[i];

                // Las ranuras que el publish nuevo no menciona conservan su último guid, para que el
                // latch pueda seguir viéndolas romperse.
            }

            public int IntactCount()
            {
                int intact = 0;
                for (int i = 0; i < EverBroken.Length; i++)
                {
                    if (!EverBroken[i]) intact++;
                }
                return intact;
            }

            private void Grow(int size)
            {
                var slots = new Guid[size];
                var broken = new bool[size];
                Array.Copy(Slots, slots, Slots.Length);
                Array.Copy(EverBroken, broken, EverBroken.Length);
                Slots = slots;
                EverBroken = broken;

                Debug.LogWarning("[RoomObjectArmorService] La mesa creció de ranuras a mitad de pelea. " +
                                 "El latch de las viejas se conserva, pero un jefe que agrega ranuras " +
                                 "sobre la marcha le devuelve armadura al total.");
            }
        }
    }
}
