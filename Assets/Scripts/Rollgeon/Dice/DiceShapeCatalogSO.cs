using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Sprite de forma por <see cref="DiceType"/>: el D4 se ve triangular, el D6
    /// cuadrado, etc. Lo consumen <c>DiceSlotView</c> (classic) y
    /// <c>DiceThrowDieView</c> (modo 2D) para que el jugador distinga el tipo de
    /// dado sin leer el número.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Dice/Dice Shape Catalog", fileName = "DiceShapeCatalog")]
    public class DiceShapeCatalogSO : ScriptableObject
    {
        /// <summary>Path de <see cref="Resources"/> para <see cref="Resolve"/>.</summary>
        public const string ResourcePath = "Dice/DiceShapeCatalog";

        [ListDrawerSettings(ShowFoldout = false)]
        [Tooltip("Forma de cada tipo de dado. Debe cubrir todos los valores de DiceType.")]
        public List<DiceShapeEntry> Shapes = new();

        [Optional]
        [Tooltip("Forma usada cuando un tipo no tiene entrada. Opcional: sin fallback, " +
                 "el dado se dibuja como el cuadrado plano de siempre.")]
        public Sprite FallbackShape;

        /// <summary>
        /// Forma de <paramref name="type"/>, o <see cref="FallbackShape"/> si no tiene
        /// entrada (o la tiene vacía). Puede devolver <c>null</c>: el caller asigna igual
        /// y <c>Image</c> degrada al quad de color plano.
        /// </summary>
        public Sprite GetShape(DiceType type)
        {
            if (Shapes != null)
            {
                for (int i = 0; i < Shapes.Count; i++)
                    if (Shapes[i].Type == type && Shapes[i].Shape != null)
                        return Shapes[i].Shape;
            }
            return FallbackShape;
        }

        /// <summary>
        /// <c>true</c> si el catálogo cubre todos los <see cref="DiceType"/> con un sprite
        /// real y sin duplicados.
        /// </summary>
        public bool Validate(out string error)
        {
            if (Shapes == null || Shapes.Count == 0)
            {
                error = "Catálogo sin Shapes — ningún dado tendría forma.";
                return false;
            }

            var seen = new HashSet<DiceType>();
            foreach (var entry in Shapes)
            {
                if (!seen.Add(entry.Type))
                {
                    error = $"{entry.Type}: entrada duplicada — GetShape solo usaría la primera.";
                    return false;
                }
                if (entry.Shape == null)
                {
                    error = $"{entry.Type}: sin sprite asignado.";
                    return false;
                }
            }

            // DiceType.cs documenta que los valores nuevos se agregan al final: este chequeo
            // es el que pone el test en rojo el día que alguien sume un tipo y olvide la forma.
            foreach (DiceType type in Enum.GetValues(typeof(DiceType)))
            {
                if (!seen.Contains(type))
                {
                    error = $"{type}: falta su entrada en el catálogo.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Catálogo autorado en el prefab, o el de <see cref="Resources"/> si el slot quedó
        /// vacío. Devuelve <c>null</c> si tampoco hay asset — el caller degrada; instanciar
        /// uno vacío acá filtraría un SO por slot.
        /// </summary>
        public static DiceShapeCatalogSO Resolve(DiceShapeCatalogSO authored)
        {
            // != null explícito y no ??: el fake-null de UnityEngine.Object no lo respeta.
            if (authored != null) return authored;
            return Resources.Load<DiceShapeCatalogSO>(ResourcePath);
        }

        private void OnValidate()
        {
            if (Shapes == null) return;
            if (!Validate(out var error))
            {
                Debug.LogWarning($"{name}: {error}", this);
            }
        }
    }

    /// <summary>La forma de un tipo de dado dentro de un <see cref="DiceShapeCatalogSO"/>.</summary>
    [Serializable]
    public struct DiceShapeEntry
    {
        public DiceType Type;

        [Tooltip("Silueta del dado. El número TMP se dibuja encima.")]
        public Sprite Shape;
    }
}
