using System;
using System.Diagnostics;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Dibuja un <c>bool[]</c> como grilla clickeable en el inspector (drawer en
    /// <c>Assets/Scripts/Editor/Drawers/BoolGridAttributeDrawer.cs</c>). Los nombres
    /// referencian campos hermanos: filas, columnas y (opcional) la celda "centro"
    /// que se resalta en amarillo. Port de Bot-Game.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    [Conditional("UNITY_EDITOR")]
    public class BoolGridAttribute : Attribute
    {
        public string RowsProperty { get; }
        public string ColsProperty { get; }
        public string CenterProperty { get; }

        public BoolGridAttribute(string rowsProperty, string colsProperty, string centerProperty = null)
        {
            RowsProperty = rowsProperty;
            ColsProperty = colsProperty;
            CenterProperty = centerProperty;
        }
    }
}
