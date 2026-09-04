using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Movement.Die
{
    /// <summary>Runtime impl de <see cref="IMovementDieService"/> (TECHNICAL.md §6.6).</summary>
    /// <remarks>
    /// RNG propio: el dado de Movimiento no comparte roller con la build (ver remarks de la
    /// interfaz). El "generation" invalida reveals tardíos — si el combate termina o la
    /// acción se cancela mientras el presenter todavía anima, el callback del presenter
    /// llega con una generación vieja y se ignora.
    /// </remarks>
    public sealed class MovementDieService : IMovementDieService, IDisposable
    {
        private readonly IPlayerService _player;
        private readonly System.Random _rng;

        private IMovementDiePresenter _presenter;
        private DiceType? _typeOverride;
        private readonly List<MovementRangeContribution> _contributions = new List<MovementRangeContribution>();
        private readonly List<MovementRangeContribution> _itemContributions = new List<MovementRangeContribution>();

        private Guid _activeGuid;
        private int _activeRange;
        private bool _hasActive;
        private bool _revealPending;
        private int _generation;

        public event Action<Guid, int> OnRolled;
        public event Action OnCleared;

        public MovementDieService(IPlayerService player, int? seed = null)
        {
            _player = player;
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            EventManager.Subscribe(EventName.OnCombatStart, HandleCombatBoundary);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleCombatBoundary);
        }

        public DiceType CurrentType
        {
            get
            {
                if (_typeOverride.HasValue) return _typeOverride.Value;
                var authored = _player?.CurrentHero?.StartingMovementDie;
                return authored != null ? authored.Type : MovementDieSO.DefaultType;
            }
        }

        public int LastFace { get; private set; }

        public int MaxFace
        {
            get
            {
                if (ServiceLocator.TryGetService<IDiceEnchantmentService>(out var ench)
                    && ench != null && ench.IsReady)
                    return ench.MovementDieMaxFace;
                return CurrentType.MaxFace();
            }
        }

        public void SetTypeOverride(DiceType? type) => _typeOverride = type;

        public void SetPresenter(IMovementDiePresenter presenter) => _presenter = presenter;

        public void Roll(Guid playerGuid, Action<int> onRevealed)
        {
            if (_revealPending)
            {
                Debug.LogWarning("[MovementDieService] Roll ignorado — hay un reveal pendiente.");
                return;
            }

            var type = CurrentType;
            int face = PickFace(type);
            int generation = ++_generation;
            _revealPending = true;

            // Hook MovementDieRolled ANTES de presentar: el bono de la tirada (Torbellino +2)
            // tiene que entrar al rango de ESTE movimiento y verse como chip en el dado, igual
            // que las Botas. Despachado en el reveal llegaba tarde para las dos cosas.
            _contributions.Clear();
            int enchantmentBonus = ResolveEnchantmentBonus(playerGuid, face, _contributions);

            void Reveal()
            {
                if (generation != _generation) return; // Clear / fin de combate lo invalidó
                _revealPending = false;
                _activeGuid = playerGuid;
                // El rango activo lleva el bono de la tirada; la cara cruda sigue siendo lo que
                // se revela (callback, OnRolled, evento) — MoveRange se suma después en
                // SelectionSettings.ResolveEffectiveRange, como siempre.
                _activeRange = face + enchantmentBonus;
                _hasActive = true;
                LastFace = face;
                onRevealed?.Invoke(face);
                OnRolled?.Invoke(playerGuid, face);
                EventManager.Trigger(EventName.OnMovementDieRolled, playerGuid, face, type);
            }

            if (_presenter != null)
            {
                // La mesa de dados se abre con este evento y se cierra con OnMovementDieRolled.
                // Se emite ANTES de presentar: si el presenter revela sincrónico (sin
                // animación) el par abre/cierra queda en orden. Sin presenter no hay mesa.
                EventManager.Trigger(EventName.OnMovementDieRollStarted, playerGuid, type);
                // Chips en orden de aplicación: primero los encantamientos del dado, después los items.
                MovementRangeAttribution.Resolve(playerGuid, _itemContributions);
                _contributions.AddRange(_itemContributions);
                int rangeBonus = enchantmentBonus + ResolveRangeBonus(playerGuid);
                if (_presenter.TryPresent(type, face, rangeBonus, _contributions, Reveal)) return;
            }
            Reveal();
        }

        /// <summary>
        /// Despacha el hook del carril de encantamientos y traduce el journal a chips: cada fuente
        /// que aportó al bono de la tirada (<c>MovementDieBonusDelta</c>) es una contribución con
        /// su asset (icono + nombre). Devuelve el bono total; 0 sin service, fuera de combate o
        /// sin encantamientos que escriban.
        /// </summary>
        private static int ResolveEnchantmentBonus(Guid playerGuid, int face, List<MovementRangeContribution> into)
        {
            if (playerGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var ench)
                || ench == null || !ench.IsReady)
                return 0;

            var scratch = ench.DispatchMovementDieRolled(playerGuid, face);
            if (scratch == null) return 0;

            var journal = scratch.Journal;
            if (journal != null)
            {
                for (int i = 0; i < journal.Count; i++)
                {
                    var entry = journal[i];
                    if (entry.MovementDieBonusDelta == 0) continue;
                    into.Add(new MovementRangeContribution(entry.SourceAsset, entry.MovementDieBonusDelta));
                }
            }
            return scratch.MovementDieBonus;
        }

        public bool TryGetActiveRange(Guid playerGuid, out int range)
        {
            if (_hasActive && _activeGuid == playerGuid)
            {
                range = _activeRange;
                return true;
            }
            range = 0;
            return false;
        }

        public void ClearActiveRange()
        {
            bool hadSomething = _hasActive || _revealPending;
            bool animating = _revealPending;
            _generation++;
            _revealPending = false;
            _hasActive = false;
            _activeRange = 0;
            _activeGuid = Guid.Empty;
            if (!hadSomething) return;
            // Con la animación en vuelo se corta en seco (Abort); con la cara ya
            // revelada el presenter decide cómo despedirse (fade-out) vía OnCleared.
            if (animating) _presenter?.Abort();
            OnCleared?.Invoke();
        }

        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnCombatStart, HandleCombatBoundary);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleCombatBoundary);
        }

        private void HandleCombatBoundary(params object[] _)
        {
            ClearActiveRange();
            LastFace = 0;
        }

        /// <summary>
        /// Cara tirada. Con el carril de encantamientos del dado listo, elige uniforme entre
        /// las caras válidas (filtros + caras extra), como <c>EnchantedDiceRoller.PickFromSet</c>;
        /// sin él, RNG plano sobre el tipo. En ambos casos consume UNA muestra del RNG propio.
        /// </summary>
        private int PickFace(DiceType type)
        {
            IReadOnlyCollection<int> faces = null;
            if (ServiceLocator.TryGetService<IDiceEnchantmentService>(out var ench)
                && ench != null && ench.IsReady)
                faces = ench.ComputeMovementDieFaces();

            if (faces == null || faces.Count == 0) return _rng.Next(1, type.MaxFace() + 1);

            int idx = _rng.Next(0, faces.Count);
            int i = 0;
            foreach (var f in faces)
            {
                if (i == idx) return f;
                i++;
            }
            return type.MaxFace();
        }

        // Mismo bonus que SelectionSettings.ResolveEffectiveRange suma al rango real:
        // se pasa al presenter solo para que el jugador VEA de dónde salió el rango.
        private static int ResolveRangeBonus(Guid playerGuid)
        {
            if (playerGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<Rollgeon.Attributes.AttributesManager>(out var attrs)
                || attrs == null)
                return 0;
            return attrs.GetAttributeModifiedValue<Rollgeon.Attributes.Stats.MoveRange, int>(playerGuid);
        }
    }
}
