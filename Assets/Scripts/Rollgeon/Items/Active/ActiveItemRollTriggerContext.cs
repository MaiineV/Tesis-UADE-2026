using Rollgeon.Effects;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.Items.Active.Choice;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// <see cref="BehaviorContext"/> que <c>ActiveItemActivationService</c> cuelga del
    /// <c>EffectContext</c> de cada activacion resuelta. Le da a los efectos de banda
    /// acceso a la cara, la banda/magnitud ya resueltas, la direccion elegida (si el
    /// item la pidio) y el punto de entrada para pedir una eleccion post-tirada.
    /// Feature#0084 — GDD "Ítems Activos Rediseñados" §A3.
    /// </summary>
    public sealed class ActiveItemRollTriggerContext : BehaviorContext
    {
        /// <summary>Item activo que se esta resolviendo.</summary>
        public ItemSO Item;

        /// <summary>Cara final (post-encantamiento), clampeada a <c>[1, Faces]</c>. Es la que decidio la banda.</summary>
        public int Face;

        /// <summary>Cara cruda, antes del ajuste del encantamiento.</summary>
        public int RawFace;

        /// <summary>Caras del dado propio del item.</summary>
        public int Faces;

        /// <summary>Banda resuelta.</summary>
        public ActiveItemBand Band;

        /// <summary>Estructura de resolucion del item.</summary>
        public ActiveItemResolution Structure;

        /// <summary>Magnitud del efecto (0 fuera de Gradient/Hierarchy — ver <see cref="ActiveItemRollResolution.Magnitude"/>).</summary>
        public int Magnitude;

        /// <summary>Magnitud normalizada 0..1.</summary>
        public float Magnitude01;

        /// <summary>
        /// Direccion elegida por el jugador, si el item corrio el flujo de targeting por
        /// direccion (GDD §A4). <c>null</c> para items sin ese flujo.
        /// </summary>
        public Cardinal? Direction;

        /// <summary>Posicion del owner al momento de resolver — ancla del flujo de direccion y de spawns.</summary>
        public GridCoord Origin;

        /// <summary>
        /// Punto de entrada para pedir una eleccion post-tirada (GDD §A5). <c>null</c> si
        /// el caller no lo armo (ej. tests que no ejercitan el flujo de eleccion) — un
        /// efecto que la necesita debe tolerar eso y degradar, nunca explotar.
        /// </summary>
        public IActiveItemChoiceHost Choices;

        /// <summary>
        /// Acceso tipado desde un efecto: <c>false</c> si el contexto no viene de una
        /// activacion de item activo (ej. un item con <see cref="EffectContext.TriggerContext"/>
        /// de otro subtipo, o sin trigger context en absoluto).
        /// </summary>
        public static bool TryGet(EffectContext ctx, out ActiveItemRollTriggerContext rollContext)
        {
            rollContext = ctx?.TriggerContext as ActiveItemRollTriggerContext;
            return rollContext != null;
        }
    }
}
