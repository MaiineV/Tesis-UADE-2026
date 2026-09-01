using System;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Bloque de autoría de <see cref="ItemSO"/> para items que REDEFINEN el daño base
    /// del jugador (categoría excluyente del GDD: Furia Contenida, Egoísta). Mientras el
    /// item esté en el inventario, <c>dmg_base_PJ</c> de la fórmula N×M lo resuelve
    /// <see cref="BaseValue"/> en cada golpe, en vez del Attack raw.
    /// </summary>
    /// <remarks>
    /// Espejo de <see cref="PersistentModifierDef"/>: lo aplica/remueve
    /// <c>InventoryService</c> al entrar/salir el item, vía
    /// <c>IBaseDamageOverrideService</c>. Furia = <c>ReadCleanTurnStreakScaled</c>;
    /// Egoísta = <c>ReadCurrentGoldSqrtScaled</c> — cero código por item.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class BaseDamageOverrideDef
    {
        [Tooltip("Activa el override mientras el item esté en el inventario.")]
        public bool Enabled;

        [OdinSerialize, SerializeReference]
        [ShowIf(nameof(Enabled))]
        [Tooltip("Reader que resuelve el daño base en cada golpe (dinámico).")]
        public EffectIntReader BaseValue;

        [ShowIf(nameof(Enabled))]
        [Tooltip("Con dos items de la categoría (no debería pasar) gana el mayor.")]
        public int Priority;
    }
}
