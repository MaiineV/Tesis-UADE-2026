using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Outline rojo compartido para "no podés usar esto ahora" — mismo rojo que
    /// <see cref="ActionButton"/> (#D1365A) reutilizable por cualquier
    /// <see cref="Graphic"/> (chips de acción, slots de ítem activo, etc.).
    /// BUG-074: la ficha de poción no tenía ninguna señal roja al no poder usarla,
    /// a diferencia de los chips de acción.
    /// </summary>
    /// <remarks>
    /// El alpha del Graphic decorado queda intacto a propósito — el Outline de uGUI
    /// multiplica su alpha por el del gráfico, así que atenuarlo apaga el recuadro
    /// rojo (mismo gotcha ya documentado en <see cref="ActionButton.ApplyChipVisual"/>).
    /// </remarks>
    public static class UnavailableTint
    {
        /// <summary>Rojo canónico de "no disponible" de la paleta UI. #D1365A.</summary>
        public static readonly Color TintColor = new Color(0.820f, 0.212f, 0.353f, 1f);

        private static readonly Vector2 DefaultDistance = new Vector2(3f, -3f);

        /// <summary>
        /// Enciende el outline rojo sobre <paramref name="graphic"/>, agregando el
        /// componente <see cref="Outline"/> si todavía no existe (mismo patrón que
        /// <see cref="ActionButton"/> en su Awake).
        /// </summary>
        public static void Apply(Graphic graphic, Vector2? distance = null)
        {
            if (graphic == null) return;

            var outline = graphic.GetComponent<Outline>();
            if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();

            outline.effectColor = TintColor;
            outline.effectDistance = distance ?? DefaultDistance;
            outline.enabled = true;
        }

        /// <summary>Apaga el outline si existe. No-op si el Graphic nunca tuvo uno.</summary>
        public static void Remove(Graphic graphic)
        {
            if (graphic == null) return;

            var outline = graphic.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }
    }
}
