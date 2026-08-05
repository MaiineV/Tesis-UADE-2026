using System;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects
{
    [Serializable, HideReferenceObjectPicker]
    public class ChainPhase
    {
        [PropertyOrder(-1)]
        public string Label = "Phase";

        [OdinSerialize, SerializeReference]
        public EffectData Effects = new EffectData();

        [ToggleLeft]
        [Tooltip("Si true, esta fase usa su propio board skin en vez de heredar el del behavior. " +
                 "Ej: la fase de escudo de un ataque usa Defense aunque el ataque sea Attack.")]
        public bool OverrideBoardType;

        [ShowIf(nameof(OverrideBoardType))]
        [Tooltip("Skin del tablero para la tirada de esta fase. Lo consume DiceBoardSkinView.")]
        public DiceBoardType BoardType = DiceBoardType.Default;

        /// <summary>Board efectivo de la fase: el propio si overridea, si no el del behavior.</summary>
        public DiceBoardType ResolveBoardType(DiceBoardType behaviorBoardType)
            => OverrideBoardType ? BoardType : behaviorBoardType;
    }
}
