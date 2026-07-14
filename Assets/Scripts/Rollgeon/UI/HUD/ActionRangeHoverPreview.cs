using System.Linq;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Al hoverear un botón de acción con elección de target, pinta el rango/targets
    /// válidos en el grid (BFS o Manhattan según el <see cref="SelectionSettings.RangeMode"/>
    /// del effect). Acciones sin selección (ForceDoor, Heal self) no pintan nada — el
    /// gate es <see cref="HeroActionBehavior.HasEffectsWithSelectionAt"/>.
    /// Convive con <c>UITooltipTrigger</c> en el mismo GO: Unity despacha los pointer
    /// callbacks a todos los handlers.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Action Range Hover Preview")]
    public sealed class ActionRangeHoverPreview : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Fase usada para resolver el behavior del slot (Combat o Exploration).")]
        [SerializeField] private GamePhase _phase = GamePhase.Combat;

        [Tooltip("Si true, el slot se lee del ActionButton del mismo GO; si false (o no " +
                 "hay ActionButton, ej. botones de exploración) se usa el slot explícito.")]
        [SerializeField] private bool _slotFromActionButton = true;

        [SerializeField] private HeroBehaviorSlot _slot = HeroBehaviorSlot.Movement;

        private bool _painted;

        public void OnPointerEnter(PointerEventData eventData) => TryShowPreview();

        public void OnPointerExit(PointerEventData eventData) => ClearPreview();

        private void OnDisable() => ClearPreview();

        private void TryShowPreview()
        {
            if (_painted) return;
            if (TryGetComponent<Button>(out var button) && !button.interactable) return;

            // No pisar una selección activa con rango visible (en Exploración el
            // movimiento armado tiene el rango suprimido, así que el hover sí pinta).
            if (!ServiceLocator.TryGetService<ISelectionController>(out var selection)
                || !selection.CanOverlayHoverPreview)
                return;

            var slot = _slot;
            if (_slotFromActionButton && TryGetComponent<ActionButton>(out var actionButton))
                slot = actionButton.Slot;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)
                || playerService?.CurrentHero == null)
                return;

            var behavior = playerService.CurrentHero.ResolveBaseBehavior(slot, _phase);
            if (behavior == null
                || !behavior.HasEffectsWithSelectionAt(SelectionTiming.BeforeRoll))
                return;

            var settings = behavior.Effects?
                .Where(g => g?.Effects != null)
                .SelectMany(g => g.Effects)
                .FirstOrDefault(e => e != null && e.RequiresSelectionAt(SelectionTiming.BeforeRoll))
                ?.GetSelection();
            if (settings == null || settings.SlotState == SlotState.Self) return;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || !grid.TryGetPosition(playerService.PlayerGuid, out var ownerPos))
                return;

            var validTiles = settings.ResolveValidTiles(ownerPos, playerService.PlayerGuid);

            if (!ServiceLocator.TryGetService<ITileHighlightService>(out var highlight)) return;

            // Misma regla que CombatHandoffService (CNF-002): selecciones de enemigo en
            // rojo "attack", el resto celeste "move". Para destinos vacíos (movimiento)
            // el EntityFilter no aplica, siempre "move".
            if (settings.TargetsEnemies)
            {
                // Ataque / ataque especial: pintar TODO el rango con el tinte base "range"
                // y los enemigos targeteables con "attack" por encima. Aunque no haya
                // ningún enemigo en rango, igual se muestra el alcance de la habilidad.
                var rangeTiles = settings.ResolveRangeTiles(ownerPos, playerService.PlayerGuid);
                if (rangeTiles.Count == 0 && validTiles.Count == 0) return;

                highlight.Highlight(rangeTiles, "range");
                highlight.Highlight(validTiles.Select(t => t.Coord), "attack");
            }
            else
            {
                if (validTiles.Count == 0) return;
                highlight.Highlight(validTiles.Select(t => t.Coord), "move");
            }
            _painted = true;
        }

        private void ClearPreview()
        {
            if (!_painted) return;
            _painted = false;

            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight))
                highlight.ClearAll();

            // Si mientras se hovereaba arrancó una selección (click en este botón), el
            // ClearAll la borró — el controller repinta su rango y las puertas.
            if (ServiceLocator.TryGetService<ISelectionController>(out var selection))
                selection.RefreshHighlights();
        }
    }
}
