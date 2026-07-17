using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Qué sprite del set de un dado se está mirando. El arte viene en filas de 5
    /// (<c>Assets/Art/UI/Dices/Dices.png</c>) y este enum es el orden de esas columnas.
    /// </summary>
    public enum DiceShapeRole
    {
        /// <summary>Cara frontal: el dado quieto.</summary>
        Front = 0,

        /// <summary>Lateral A — junto con <see cref="SideB"/> vende el giro del roll.</summary>
        SideA = 1,

        /// <summary>Lateral B.</summary>
        SideB = 2,

        /// <summary>Puntero encima del dado.</summary>
        Hover = 3,

        /// <summary>Dado holdeado.</summary>
        Selected = 4,
    }

    /// <summary>
    /// Set de sprites por <see cref="DiceType"/>: el D4 se ve triangular, el D6
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
        [Tooltip("Set de sprites de cada tipo de dado. Debe cubrir todos los valores de DiceType.")]
        public List<DiceShapeEntry> Shapes = new();

        [Optional]
        [Tooltip("Forma usada cuando un tipo no tiene entrada. Opcional: sin fallback, " +
                 "el dado se dibuja como el cuadrado plano de siempre.")]
        public Sprite FallbackShape;

        /// <summary>
        /// Sprite de <paramref name="type"/> para <paramref name="role"/>. Degrada en cadena
        /// <c>role → Front → FallbackShape</c>: un catálogo autorado antes de que existieran los
        /// roles (o con un rol sin arte) sigue dibujando el frontal en vez de desaparecer.
        /// Puede devolver <c>null</c>: el caller asigna igual y <c>Image</c> degrada al quad de
        /// color plano.
        /// </summary>
        /// <remarks>
        /// El default <see cref="DiceShapeRole.Front"/> deja intactos a los call-sites que no
        /// conocen roles (<c>DiceThrowDieView</c>).
        /// </remarks>
        public Sprite GetShape(DiceType type, DiceShapeRole role = DiceShapeRole.Front)
        {
            if (Shapes != null)
            {
                for (int i = 0; i < Shapes.Count; i++)
                {
                    if (Shapes[i].Type != type) continue;

                    var sprite = Shapes[i].Get(role);
                    if (sprite != null) return sprite;

                    // Entrada encontrada pero sin arte para el rol: el frontal es la
                    // degradación, no seguir buscando otra entrada del mismo tipo.
                    if (Shapes[i].Front != null) return Shapes[i].Front;
                    break;
                }
            }
            return FallbackShape;
        }

        /// <summary>
        /// <c>true</c> si el catálogo cubre todos los <see cref="DiceType"/> con un frontal
        /// real y sin duplicados. NO exige el resto del set — eso lo chequea
        /// <see cref="ValidateRoles"/>, para que un catálogo a medio autorar falle con el
        /// mensaje que corresponde y no con "falta SideA".
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
                if (entry.Front == null)
                {
                    error = $"{entry.Type}: sin sprite frontal asignado.";
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
        /// <c>true</c> si además de <see cref="Validate"/> cada tipo tiene los 5 roles con arte
        /// real. Es la red que avisa cuando llega un dado nuevo y solo se autoró el frontal:
        /// sin esto el dado se vería quieto durante el giro y sin feedback de hover/held, que es
        /// un bug silencioso.
        /// </summary>
        public bool ValidateRoles(out string error)
        {
            if (!Validate(out error)) return false;

            foreach (var entry in Shapes)
            {
                foreach (DiceShapeRole role in Enum.GetValues(typeof(DiceShapeRole)))
                {
                    if (entry.Get(role) == null)
                    {
                        error = $"{entry.Type}: sin sprite para el rol {role}.";
                        return false;
                    }
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
            if (!ValidateRoles(out var error))
            {
                Debug.LogWarning($"{name}: {error}", this);
            }
        }
    }

    /// <summary>El set de sprites de un tipo de dado dentro de un <see cref="DiceShapeCatalogSO"/>.</summary>
    /// <remarks>
    /// El orden de los campos espeja las columnas del sheet de arte (ver
    /// <see cref="DiceShapeRole"/>).
    /// </remarks>
    [Serializable]
    public struct DiceShapeEntry
    {
        public DiceType Type;

        [FormerlySerializedAs("Shape")]
        [Tooltip("Dado quieto. El número TMP se dibuja encima.")]
        public Sprite Front;

        [Tooltip("Lateral A del giro.")]
        public Sprite SideA;

        [Tooltip("Lateral B del giro.")]
        public Sprite SideB;

        [Tooltip("Puntero encima del dado.")]
        public Sprite Hover;

        [Tooltip("Dado holdeado.")]
        public Sprite Selected;

        /// <summary>Sprite de <paramref name="role"/>, o <c>null</c> si ese rol no tiene arte.</summary>
        public Sprite Get(DiceShapeRole role) => role switch
        {
            DiceShapeRole.SideA => SideA,
            DiceShapeRole.SideB => SideB,
            DiceShapeRole.Hover => Hover,
            DiceShapeRole.Selected => Selected,
            _ => Front,
        };
    }
}
