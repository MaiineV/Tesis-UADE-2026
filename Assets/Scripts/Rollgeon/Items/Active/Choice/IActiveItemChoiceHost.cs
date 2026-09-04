namespace Rollgeon.Items.Active.Choice
{
    /// <summary>
    /// Expuesto en <see cref="ActiveItemRollTriggerContext.Choices"/>: el punto de
    /// entrada que un efecto de banda usa para pedir una eleccion post-tirada
    /// (Probability Drive cara 4: "elegi 1 de 3 tiles"). GDD §A5.
    /// </summary>
    public interface IActiveItemChoiceHost
    {
        /// <summary>
        /// Encola el pedido. <c>false</c> si ya habia uno encolado en esta activacion —
        /// solo el primer pedido por activacion se honra (el resto loguea warning y se
        /// descarta). El roll ya esta pagado: el efecto no debe cortar la cadena por esto.
        /// </summary>
        bool RequestChoice(ActiveItemChoiceRequest request);
    }
}
