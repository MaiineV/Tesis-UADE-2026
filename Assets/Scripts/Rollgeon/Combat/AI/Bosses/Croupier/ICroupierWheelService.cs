using System;
using System.Collections.Generic;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>Global y lazy, pero su estado es por combate: se resetea en <c>OnCombatEnd</c>/<c>OnRunEnd</c>, modo de mesa incluido, así que se arranca en fase 1.</summary>
    public interface ICroupierWheelService
    {
        /// <summary>Fase de mesa (1 = un número; 2 = "pleno y color"). La setea el gate de HP.</summary>
        int PhaseIndex { get; }

        /// <summary>Cuántos números canta por turno (fase 1 = 1, fase 2 = 2).</summary>
        int NumbersPerTurn { get; }

        /// <summary>Rueda trucada: terminar el turno dentro del sector cantado ya no corre la rueda. Es lo único que apaga — la Represalia se cobra igual.</summary>
        bool Rigged { get; }

        /// <summary>Daño de la Represalia de mesa: lo que cuesta pegarle, siempre.</summary>
        int RetaliationDamage { get; set; }

        /// <summary>Números en el aire ahora mismo, ya corridos si el jugador pegó. Vacío fuera del windup.</summary>
        IReadOnlyList<int> SungNumbers { get; }

        /// <summary><c>true</c> entre cantar y detonar: la ventana en la que cerrar el turno dentro del sector cantado mueve la rueda.</summary>
        bool WindupActive { get; }

        /// <summary>Sectores que detonaron en <b>este</b> turno del jefe; los limpia el nodo de ignición al prenderlos.</summary>
        IReadOnlyList<int> DetonatedSectors { get; }

        event Action<IReadOnlyList<int>> NumbersChanged;

        /// <summary>
        /// A partir de acá el daño recibido por ese guid cobra Represalia y el cierre de turno del
        /// jugador corre la rueda. Idempotente; re-atar a otro guid reemplaza el anterior.
        /// </summary>
        void Bind(Guid bossGuid);

        void SetMode(int numbersPerTurn, bool rigged, int phaseIndex);

        /// <summary>Pone los números en el aire y abre el windup, descartando cualquier número anterior sin detonar. Reinicia el candado de corrimiento.</summary>
        void Sing(IReadOnlyList<int> numbers);

        /// <summary>Registra con qué daño/tipo quedó marcado el slot, para que un corrimiento pueda re-marcar el área en el sector nuevo con los mismos números.</summary>
        void RecordMark(int slot, int damage, AttackKind kind);

        /// <summary>Cierra el windup: devuelve los slots que estaban en el aire (para detonarlos) y los publica en <see cref="DetonatedSectors"/>.</summary>
        IReadOnlyList<CroupierWheelSlot> ConsumeWindup();

        void ClearDetonated();

        void Reset();
    }

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
