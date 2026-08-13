using System;
using System.Collections.Generic;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Estado de la rueda del Croupier durante un combate: el número (o los dos) que está cantando,
    /// el candado de corrimiento por número, y el modo de mesa (fase 1 / fase 2 con la rueda
    /// trucada). Es el único punto donde viven "el número en el aire" y sus consecuencias, porque
    /// tres nodos del árbol y un hook de daño fuera del árbol necesitan leer/escribir lo mismo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué un servicio y no estado en un nodo.</b> El corrimiento y la Represalia entran por
    /// <c>TypedEvent&lt;DamageResolvedPayload&gt;</c> — fuera del turno del jefe y fuera de cualquier
    /// tick del árbol. Un <c>[NonSerialized]</c> en un nodo (el patrón de
    /// <see cref="Decisions.AINode_Alternate"/>) alcanza cuando el estado lo lee un solo nodo; acá lo
    /// comparten el que canta, el que marca, el que detona, el que enciende el fuego, el reader que
    /// dirige la confiscación de dados y el hook de daño.
    /// </para>
    /// <para>
    /// <b>Ciclo de vida.</b> Global y lazy (<see cref="CroupierWheelService.ResolveOrCreate"/>, mismo
    /// patrón que <c>ThreatTelegraphOverlay</c> para no depender de wiring manual en
    /// <c>ServiceBootstrap.ExtraServices</c>), pero su estado es por combate: se resetea en
    /// <c>OnCombatEnd</c> / <c>OnRunEnd</c>, incluido el modo de mesa (una pelea nueva arranca
    /// siempre en fase 1, aunque la anterior haya terminado con la rueda trucada).
    /// </para>
    /// </remarks>
    public interface ICroupierWheelService
    {
        /// <summary>Fase de mesa (1 = un número; 2 = "pleno y color"). La setea el gate de HP.</summary>
        int PhaseIndex { get; }

        /// <summary>Cuántos números canta por turno (fase 1 = 1, fase 2 = 2).</summary>
        int NumbersPerTurn { get; }

        /// <summary>
        /// Rueda trucada: pegarle al jefe ya no corre la rueda <b>ni</b> cobra Represalia. Los dos
        /// son el mismo evento, así que se apagan juntos — el 8 era el precio de la palanca.
        /// </summary>
        bool Rigged { get; }

        /// <summary>Daño de la Represalia de mesa. Lo publica el nodo que canta (dato de autoría).</summary>
        int RetaliationDamage { get; set; }

        /// <summary>
        /// Números en el aire ahora mismo, ya corridos si el jugador pegó. Vacío fuera del windup.
        /// Es lo que tiene que mostrar el número enorme sobre el jefe.
        /// </summary>
        IReadOnlyList<int> SungNumbers { get; }

        /// <summary>
        /// <c>true</c> entre el momento en que canta y el momento en que detona: la ventana en la que
        /// pegarle mueve la rueda.
        /// </summary>
        bool WindupActive { get; }

        /// <summary>
        /// Sectores que detonaron en <b>este</b> turno del jefe, para que el nodo de ignición sepa
        /// qué prender fuego. Se limpia al encenderlo.
        /// </summary>
        IReadOnlyList<int> DetonatedSectors { get; }

        /// <summary>
        /// Se dispara cada vez que cambia el contenido de <see cref="SungNumbers"/> (canta, corre la
        /// rueda, detona). Canal C# y no <c>EventName</c> a propósito: hoy no hay UI que lo consuma y
        /// no se agrega una entry al enum global por un evento sin suscriptores.
        /// </summary>
        event Action<IReadOnlyList<int>> NumbersChanged;

        /// <summary>
        /// Ata el servicio al jefe <paramref name="bossGuid"/>: a partir de acá el daño recibido por
        /// ese guid corre la rueda y cobra Represalia. Idempotente; re-atar a otro guid reemplaza el
        /// anterior (combate nuevo, instancia nueva).
        /// </summary>
        void Bind(Guid bossGuid);

        /// <summary>Cambia el modo de mesa. Lo llama el setup de fase, envuelto en <c>Once</c>.</summary>
        void SetMode(int numbersPerTurn, bool rigged, int phaseIndex);

        /// <summary>
        /// Pone <paramref name="numbers"/> en el aire y abre el windup, descartando cualquier número
        /// anterior sin detonar. Reinicia el candado de corrimiento.
        /// </summary>
        void Sing(IReadOnlyList<int> numbers);

        /// <summary>
        /// Registra con qué daño/tipo quedó marcado el slot <paramref name="slot"/>, para que un
        /// corrimiento pueda re-marcar el área en el sector nuevo con los mismos números.
        /// </summary>
        void RecordMark(int slot, int damage, AttackKind kind);

        /// <summary>
        /// Cierra el windup: devuelve los slots que estaban en el aire (para detonarlos) y los
        /// publica en <see cref="DetonatedSectors"/>. Después de esto pegarle al jefe ya no mueve
        /// nada hasta que vuelva a cantar.
        /// </summary>
        IReadOnlyList<CroupierWheelSlot> ConsumeWindup();

        /// <summary>Limpia <see cref="DetonatedSectors"/> — lo llama el nodo de ignición al terminar.</summary>
        void ClearDetonated();

        /// <summary>Vuelve al estado de arranque (fase 1, sin números en el aire, sin binding).</summary>
        void Reset();
    }

    /// <summary>
    /// Un número cantado y su marca: qué sector amenaza, con cuánto daño quedó marcado y si ya gastó
    /// su corrimiento. Snapshot inmutable — el servicio es dueño del estado mutable.
    /// </summary>
    public readonly struct CroupierWheelSlot
    {
        /// <summary>Índice del slot (0 = primer número cantado del turno).</summary>
        public readonly int Slot;

        /// <summary>Sector del paño (1..6) que este número amenaza.</summary>
        public readonly int Sector;

        /// <summary>Daño con el que se marcó el área. 0 = todavía no se marcó.</summary>
        public readonly int Damage;

        public readonly AttackKind Kind;

        /// <summary><c>true</c> si este número ya gastó su único corrimiento en este windup.</summary>
        public readonly bool Nudged;

        public CroupierWheelSlot(int slot, int sector, int damage, AttackKind kind, bool nudged)
        {
            Slot = slot;
            Sector = sector;
            Damage = damage;
            Kind = kind;
            Nudged = nudged;
        }
    }
}
