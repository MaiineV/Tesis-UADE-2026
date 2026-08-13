using System;
using System.Collections.Generic;

namespace Patterns.Save
{
    /// <summary>
    /// Wrapper del cache que va a disco (§15.3). Se serializa con Odin
    /// <c>SerializationUtility</c> — <c>Data</c> guarda valores polimórficos
    /// (ints boxeados, diccionarios, DTOs) que <c>JsonUtility</c> no soporta.
    /// </summary>
    [Serializable]
    public class SavePayload
    {
        public int SchemaVersion;
        public Dictionary<string, object> Data;
    }
}
