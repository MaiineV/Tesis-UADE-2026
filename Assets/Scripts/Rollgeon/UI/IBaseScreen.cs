namespace Rollgeon.UI
{
    /// <summary>
    /// Interface "skinny" que permite referenciar screens sin acoplarse a la MonoBehaviour base
    /// (<see cref="BaseScreen"/>). La expone <see cref="IScreenManager.Current"/>.
    /// Plan §4.2.
    /// </summary>
    /// <remarks>
    /// Divergencia documentada vs TECHNICAL.md §17.D.3 (que usa <c>ScreenId</c> enum y nombres
    /// <c>OnPush/OnPop/OnShow/OnHide</c>): este MVP usa <see cref="System.Type"/> keys + string ids
    /// (ver <see cref="ScreenStringId"/>) y nombres <c>OnPushed/OnPopped/OnGainFocus/OnLoseFocus</c>.
    /// El upgrade path al enum es aditivo (ver plan §10 R2/R3).
    /// </remarks>
    public interface IBaseScreen
    {
        /// <summary>
        /// Identificador string estable para la screen. Default en <see cref="BaseScreen"/>:
        /// <c>GetType().Name</c>. Overridable si se quiere un id estable distinto del nombre de tipo.
        /// Usado por <see cref="IScreenManager.PushByStringId"/>.
        /// </summary>
        string ScreenStringId { get; }

        /// <summary>
        /// Invocado por el <see cref="IScreenManager"/> cuando la screen entra al stack.
        /// Prefijo <c>_Internal_</c> senaliza que es infra y no debe llamarse directamente
        /// desde screens concretas (plan §4.2).
        /// </summary>
        void _Internal_OnPushed(IScreenPayload payload);

        /// <summary>Invocado por el <see cref="IScreenManager"/> cuando la screen sale del stack.</summary>
        void _Internal_OnPopped();

        /// <summary>Invocado cuando la screen pasa a ser top del stack (gana foco).</summary>
        void _Internal_OnGainFocus();

        /// <summary>Invocado cuando la screen deja de ser top del stack (pierde foco).</summary>
        void _Internal_OnLoseFocus();

        /// <summary>
        /// Helper que el <see cref="IScreenManager"/> usa para togglear <c>gameObject.SetActive</c>
        /// sin referenciar la MonoBehaviour directamente.
        /// </summary>
        void _Internal_SetVisible(bool visible);
    }
}
