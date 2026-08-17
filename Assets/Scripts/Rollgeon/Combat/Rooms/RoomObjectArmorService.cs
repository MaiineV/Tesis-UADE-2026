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
    /// La armadura de la mesa: mientras los objetos de sala de un jefe sigan en pie, el daño que él
    /// recibe se reduce. Cada objeto aporta
    /// <see cref="RoomObjectDefinitionSO.OwnerDamageReductionPerObject"/>, y romper uno se lo devuelve
    /// al jugador <b>para siempre</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Qué agrega a la pelea.</b> Los cinco dados de La Generala hacían dos cosas invisibles:
    /// bloquear el paso y borrarle una categoría de la mano. Ninguna aparece en pantalla, así que la
    /// mesa se leía como decorado y romperla parecía una pérdida de turnos. La reducción le pone el
    /// número adelante: el primer golpe hace 9 en vez de 30, y sube cada vez que rompés un dado.
    /// </para>
    /// <para>
    /// <b>Progreso permanente, por ranura.</b> El latch es monótono y va por <b>índice</b> de ranura,
    /// no por guid: el dado repuesto vuelve a bloquear y a darle la categoría, pero su parte de la
    /// reducción no vuelve. Sin eso la mesa sería una noria —limpiás cinco dados, se reponen, volvés a
    /// empezar— y la inversión de romperlos no compraría nada estable.
    /// </para>
    /// <para>
    /// <b>El latch se evalúa al consultar, no al publicar.</b> El nodo del jefe tickea en el turno del
    /// jefe; si la cuenta se congelara ahí, romper un dado en el turno del jugador no bajaría la
    /// reducción hasta el turno siguiente y el golpe de después seguiría reducido. Eso se lee como que
    /// el juego no registró el impacto. Así que <see cref="TryGetMultiplier"/> lee la
    /// <see cref="Health"/> viva de cada guid publicado en ese momento. Un objeto enterrado conserva su
    /// Health en &lt;= 0 (<c>CombatDeathWatcher</c> no lo desregistra de
    /// <see cref="AttributesManager"/>), así que HP &lt;= 0 o sin registro = roto — misma fuente de
    /// verdad que el alive-check de la AI de targeting.
    /// </para>
    /// <para>
    /// <b>Techo.</b> <see cref="MaxReduction"/> impide que una definición mal autorada (o un jefe con
    /// muchos objetos) lo vuelva invulnerable. Una reducción del 100% no es una mecánica dura, es una
    /// pelea que no termina.
    /// </para>
    /// <para>
    /// <b>Se suscribe a los scopes</b> para olvidar mesas al terminar la pelea: el servicio es Global y
    /// una tabla que sobreviva le daría armadura a un guid que ya no existe.
    /// </para>
    /// </remarks>
    public sealed class RoomObjectArmorService : IIncomingDamageMultiplierProvider, IDisposable
    {
        /// <summary>Reducción máxima, sin importar cuántos objetos aporten. 0.9 = pega el 10%.</summary>
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
        /// Registra el estado de las ranuras de <paramref name="ownerGuid"/>.
        /// <paramref name="slotGuids"/> va <b>por índice</b>: <see cref="Guid.Empty"/> en una ranura
        /// vacía. Idempotente y pensado para llamarse todos los turnos del jefe.
        /// </summary>
        /// <param name="reductionPerObject">
        /// Cuánto descuenta cada ranura nunca rota. 0 o menos borra la mesa: una definición sin
        /// armadura no tiene por qué dejar estado colgado.
        /// </param>
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
        /// Reducción viva de <paramref name="ownerGuid"/>, en 0..<see cref="MaxReduction"/>. 0 si no
        /// tiene mesa publicada. Pública para la UI y para los asserts de tests: el número que el
        /// jugador ve tiene que salir de la misma cuenta que el que le baja la vida.
        /// </summary>
        public float ReductionFor(Guid ownerGuid) =>
            _tables.TryGetValue(ownerGuid, out var table) ? ResolveReduction(table) : 0f;

        /// <summary>Ranuras que nunca se rompieron. Para la UI y los tests.</summary>
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
        /// Marca como rota —y para siempre— toda ranura cuyo objeto esté sin vida ahora mismo.
        /// </summary>
        /// <remarks>
        /// Sin <see cref="AttributesManager"/> registrado no se puede saber qué está roto, así que no
        /// se latchea nada: la reducción queda como estaba. Fallar hacia "el jefe conserva su
        /// armadura" y no hacia "la perdió" es lo conservador — lo segundo le regalaría la pelea sin
        /// que nada lo explique.
        /// </remarks>
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
        /// Una mesa: el guid vivo de cada ranura y el latch monótono de cuáles se rompieron alguna vez.
        /// Los arrays crecen pero nunca se encogen — perder el latch de una ranura le devolvería
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

                // Las ranuras que el publish nuevo ya no menciona quedan con su último guid: no se
                // limpian, para que el latch pueda seguir viéndolas romperse.
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
