namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Resultado ya resuelto de la tirada del dado propio de un item activo: la cara
    /// final, la banda y (en Gradient/Hierarchy) la magnitud. Es lo que
    /// <see cref="ActiveItemBands.ResolveRoll(int,ItemSO)"/> devuelve y lo que viaja al
    /// efecto via <see cref="ActiveItemRollTriggerContext"/>.
    /// </summary>
    public readonly struct ActiveItemRollResolution
    {
        /// <summary>Cara final (post-encantamiento), clampeada a <c>[1, Faces]</c>.</summary>
        public readonly int Face;

        /// <summary>Cara cruda, antes del ajuste del encantamiento. Igual a <see cref="Face"/> si no hubo ajuste.</summary>
        public readonly int RawFace;

        /// <summary>Caras del dado propio del item.</summary>
        public readonly int Faces;

        /// <summary>Banda resuelta (siempre presente, incluso en Gradient/Hierarchy — ahi es solo "banda de feel").</summary>
        public readonly ActiveItemBand Band;

        /// <summary>Estructura de resolucion del item que produjo este resultado.</summary>
        public readonly ActiveItemResolution Structure;

        /// <summary>
        /// Magnitud del efecto: igual a <see cref="Face"/> en <see cref="ActiveItemResolution.Gradient"/>
        /// y <see cref="ActiveItemResolution.Hierarchy"/>, <c>0</c> en <see cref="ActiveItemResolution.Bands"/>
        /// y <see cref="ActiveItemResolution.Binary"/> (esas estructuras no tienen un "nivel" continuo).
        /// </summary>
        public readonly int Magnitude;

        /// <summary>Magnitud normalizada 0..1 sobre el rango del dado. 0 si el dado tiene 1 cara.</summary>
        public float Magnitude01 => Faces > 1 ? (float)(Face - 1) / (Faces - 1) : 0f;

        public ActiveItemRollResolution(int face, int rawFace, int faces, ActiveItemBand band,
            ActiveItemResolution structure, int magnitude)
        {
            Face = face;
            RawFace = rawFace;
            Faces = faces;
            Band = band;
            Structure = structure;
            Magnitude = magnitude;
        }
    }
}
