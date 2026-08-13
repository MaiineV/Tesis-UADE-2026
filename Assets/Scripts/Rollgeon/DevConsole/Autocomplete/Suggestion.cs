namespace Rollgeon.DevConsole.Autocomplete
{
    /// <summary>Una sugerencia de autocompletado.</summary>
    public readonly struct Suggestion
    {
        /// <summary>Valor que se inserta al aceptar.</summary>
        public string Text { get; }
        /// <summary>Texto mostrado en la lista (default = Text).</summary>
        public string Display { get; }
        /// <summary>Texto secundario opcional (tipo de arg, descripción).</summary>
        public string Hint { get; }

        public Suggestion(string text, string display = null, string hint = null)
        {
            Text = text;
            Display = display ?? text;
            Hint = hint;
        }
    }
}
