using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Implementación runtime de <see cref="ITutorialActionGateService"/>. Mantiene
    /// el set de slots bloqueados y el mapa <c>ActionName → Slot</c> construido
    /// desde los <c>PhaseBehaviors</c> del héroe del tutorial (para el backstop por
    /// actionId del <c>TurnManager</c>). ActionNames que no pertenecen al héroe
    /// (behaviors de enemigos, acciones de catálogo) nunca bloquean.
    /// </summary>
    public sealed class TutorialActionGateService : ITutorialActionGateService
    {
        private const string LogPrefix = "[TutorialActionGateService] ";

        private readonly HashSet<HeroBehaviorSlot> _lockedSlots;
        private readonly Dictionary<string, HeroBehaviorSlot> _slotByActionName;

        public TutorialActionGateService(ClassHeroSO hero, IEnumerable<HeroBehaviorSlot> initiallyLocked)
        {
            _lockedSlots = initiallyLocked != null
                ? new HashSet<HeroBehaviorSlot>(initiallyLocked)
                : new HashSet<HeroBehaviorSlot>();

            _slotByActionName = new Dictionary<string, HeroBehaviorSlot>(StringComparer.Ordinal);
            if (hero?.PhaseBehaviors != null)
            {
                foreach (var behavior in hero.PhaseBehaviors)
                {
                    if (behavior == null || string.IsNullOrEmpty(behavior.ActionName)) continue;
                    // Ante ActionNames duplicados entre fases, gana el primero — el
                    // slot es el mismo para el par exploración/combate de una acción.
                    _slotByActionName.TryAdd(behavior.ActionName, behavior.Slot);
                }
            }
        }

        /// <summary>
        /// Factory: crea el gate con el lock inicial del tutorial
        /// ({BaseAttack, ClassSkill, Healing, ForceDoor, Defense} — solo Movement
        /// libre; cada acción se desbloquea recién en su paso de enseñanza) y lo
        /// registra en <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static TutorialActionGateService CreateAndRegister(ClassHeroSO hero)
        {
            var service = new TutorialActionGateService(hero, new[]
            {
                HeroBehaviorSlot.BaseAttack,
                HeroBehaviorSlot.ClassSkill,
                HeroBehaviorSlot.Healing,
                HeroBehaviorSlot.ForceDoor,
                HeroBehaviorSlot.Defense,
            });
            ServiceLocator.AddService<ITutorialActionGateService>(service, ServiceScope.Run);
            return service;
        }

        public bool IsSlotLocked(HeroBehaviorSlot slot) => _lockedSlots.Contains(slot);

        public bool IsActionLocked(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            return _slotByActionName.TryGetValue(actionId, out var slot)
                   && _lockedSlots.Contains(slot);
        }

        public void Unlock(HeroBehaviorSlot slot)
        {
            if (!_lockedSlots.Remove(slot)) return;

            Debug.Log(LogPrefix + $"Acción desbloqueada: {slot}.");
            EventManager.Trigger(EventName.OnTutorialActionUnlocked, slot);
        }

        /// <summary>
        /// Re-bloquea un slot (ej. durante la lección de escape solo queda Forzar
        /// Puerta). Dispara el mismo evento que <see cref="Unlock"/> — los HUDs
        /// solo lo usan como señal de "recomputá estados de botones".
        /// </summary>
        public void Lock(HeroBehaviorSlot slot)
        {
            if (!_lockedSlots.Add(slot)) return;

            Debug.Log(LogPrefix + $"Acción re-bloqueada: {slot}.");
            EventManager.Trigger(EventName.OnTutorialActionUnlocked, slot);
        }

        // ==================================================================
        // BUG-019 — lock temporal por paso (ventana exclusiva)
        // ==================================================================

        /// <summary>
        /// Copia del set de slots bloqueados, para restaurar tras un lock temporal
        /// por paso (<see cref="LockAllExcept"/> → <see cref="RestoreTo"/>).
        /// </summary>
        public HashSet<HeroBehaviorSlot> SnapshotLocked()
            => new HashSet<HeroBehaviorSlot>(_lockedSlots);

        /// <summary>
        /// Bloquea todos los slots salvo <paramref name="allowed"/>. Reusa
        /// <see cref="Lock"/>: idempotente y con evento por slot que cambia.
        /// </summary>
        public void LockAllExcept(HeroBehaviorSlot allowed)
        {
            foreach (HeroBehaviorSlot slot in Enum.GetValues(typeof(HeroBehaviorSlot)))
            {
                if (slot != allowed) Lock(slot);
            }
        }

        /// <summary>
        /// Restaura el set exacto de un snapshot: lockea lo que estaba lockeado y
        /// deslockea el resto. No-op con snapshot null.
        /// </summary>
        public void RestoreTo(HashSet<HeroBehaviorSlot> lockedSnapshot)
        {
            if (lockedSnapshot == null) return;
            foreach (HeroBehaviorSlot slot in Enum.GetValues(typeof(HeroBehaviorSlot)))
            {
                if (lockedSnapshot.Contains(slot)) Lock(slot);
                else Unlock(slot);
            }
        }
    }
}
