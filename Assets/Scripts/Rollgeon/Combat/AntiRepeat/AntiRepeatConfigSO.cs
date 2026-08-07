using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AntiRepeat
{
    /// <summary>
    /// Config autorado del pasivo anti-repetición (A/B). Guarda el <see cref="Mode"/> por
    /// default con el que arranca la sesión. Se dropea en
    /// <c>ServiceBootstrapSO.SettingsAssets</c> (registrado global por su Type runtime,
    /// igual que <c>CameraConfigSO</c>) y lo lee <c>AntiRepeatModeService</c> al bootstrap
    /// para sembrar el valor vivo.
    /// <para>
    /// El valor vivo/runtime NO vive acá: lo tiene <c>IAntiRepeatModeService</c>, así el
    /// comando de consola (<c>passive dice|combo</c>) puede flipearlo sin ensuciar este asset.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Anti-Repeat Config", fileName = "AntiRepeatConfig")]
    public class AntiRepeatConfigSO : SerializedScriptableObject
    {
        [Title("Anti-Repeat Passive")]
        [InfoBox("Modo por default al arrancar la sesión. Combo = repetir el último combo hace 0 daño. " +
                 "Dice = bloquea un dado al azar cada turno. El comando de consola 'passive' lo flipea en vivo " +
                 "sin modificar este asset.")]
        public AntiRepeatMode Mode = AntiRepeatMode.Combo;
    }
}
