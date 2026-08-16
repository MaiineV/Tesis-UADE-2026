using System;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Resolución y aplicación del visual del cofre por tier: prefab (override del
    /// tier o el global de config) y materiales por slot — override de material si
    /// el tier lo define, tint por MaterialPropertyBlock si no. Estático y sin
    /// estado para poder testearse sin ChestService.
    /// </summary>
    public static class ChestVisuals
    {
        public enum Slot
        {
            None,
            Body,
            Fittings,
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Prefab a spawnear: el override del tier, o el global de config.</summary>
        public static GameObject ResolvePrefab(ChestConfigSO config, ChestTierDef tierDef)
        {
            if (tierDef != null && tierDef.ChestPrefabOverride != null)
                return tierDef.ChestPrefabOverride;
            return config != null ? config.ChestPrefab : null;
        }

        /// <summary>
        /// Slot lógico de un material por nombre: 'Wood'/'Body' → Body,
        /// 'Frame'/'Fittings' → Fittings (case-insensitive, por substring).
        /// </summary>
        public static Slot ClassifySlot(string materialName)
        {
            if (NameContains(materialName, "Wood") || NameContains(materialName, "Body"))
                return Slot.Body;
            if (NameContains(materialName, "Frame") || NameContains(materialName, "Fittings"))
                return Slot.Fittings;
            return Slot.None;
        }

        /// <summary>
        /// Aplica el visual del tier sobre una instancia ya spawneada. Por slot de
        /// material: si el tier define un material override para ese slot se asigna
        /// (copia de <c>sharedMaterials</c> — nunca <c>.material</c>, que instancia
        /// y leakea) y el slot NO se tinta; sin override, tint clásico por
        /// MaterialPropertyBlock con BodyColor/fittingsColor. Fallback legacy por
        /// nombre de renderer ('Body'/'Fittings', PF_ChestPlaceholder) solo tinta.
        /// </summary>
        public static void Apply(GameObject go, ChestTierDef tierDef, Color fittingsColor)
        {
            if (go == null || tierDef == null) return;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;

                var mats = r.sharedMaterials;
                Material[] replaced = null;
                bool slotMatched = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var slot = ClassifySlot(mats[i] != null ? mats[i].name : null);
                    if (slot == Slot.None) continue;
                    slotMatched = true;

                    var overrideMat = slot == Slot.Body
                        ? tierDef.BodyMaterialOverride
                        : tierDef.FittingsMaterialOverride;
                    if (overrideMat != null)
                    {
                        replaced ??= (Material[])mats.Clone();
                        replaced[i] = overrideMat;
                        continue;
                    }

                    var tint = slot == Slot.Body ? tierDef.BodyColor : fittingsColor;
                    r.GetPropertyBlock(mpb, i);
                    mpb.SetColor(BaseColorId, tint);
                    mpb.SetColor(ColorId, tint);
                    r.SetPropertyBlock(mpb, i);
                }
                if (replaced != null) r.sharedMaterials = replaced;
                if (slotMatched) continue;

                Color? rendererTint = null;
                if (NameContains(r.gameObject.name, "Body"))
                    rendererTint = tierDef.BodyColor;
                else if (NameContains(r.gameObject.name, "Fittings"))
                    rendererTint = fittingsColor;
                if (rendererTint == null) continue;

                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, rendererTint.Value);
                mpb.SetColor(ColorId, rendererTint.Value);
                r.SetPropertyBlock(mpb);
            }
        }

        private static bool NameContains(string name, string token)
            => name != null && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
