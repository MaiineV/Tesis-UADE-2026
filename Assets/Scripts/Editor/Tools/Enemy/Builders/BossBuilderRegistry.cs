using System.Collections.Generic;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Qué assets de enemigo genera cada builder de jefe. Los builders escriben <c>AIRoot</c>,
    /// stats y ficha incondicionalmente, así que cualquier edición hecha en el Editor de enemigos
    /// sobre esos assets se pierde al volver a correr el builder. El editor consulta este registro
    /// para avisarlo.
    /// </summary>
    public static class BossBuilderRegistry
    {
        public static readonly IReadOnlyDictionary<string, string> ByAssetPath = new Dictionary<string, string>
        {
            [CajeroAssetBuilder.EnemyAssetPath]   = CajeroAssetBuilder.MenuPath,
            [CajeroAssetBuilder.CritterAssetPath] = CajeroAssetBuilder.MenuPath,
            [CroupierAssetBuilder.BossAssetPath]  = CroupierAssetBuilder.MenuPath,
            [GeneralaAssetBuilder.BossAssetPath]  = GeneralaAssetBuilder.MenuPath,
            [GeneralaAssetBuilder.DiceAssetPath]  = GeneralaAssetBuilder.MenuPath,
            [TahurAssetBuilder.AssetPath]         = TahurAssetBuilder.MenuPath,
            [BandidaAssetBuilder.BossAssetPath]   = BandidaAssetBuilder.MenuPath,
            [BandidaAssetBuilder.ReelAssetPath]   = BandidaAssetBuilder.MenuPath,
            [AnotadorAssetBuilder.EnemyAssetPath] = AnotadorAssetBuilder.MenuPath,
        };

        public static bool TryGetBuilder(UnityEngine.Object asset, out string menuPath)
        {
            menuPath = null;
            if (asset == null) return false;
            string path = AssetDatabase.GetAssetPath(asset);
            return TryGetBuilderForPath(path, out menuPath);
        }

        public static bool TryGetBuilderForPath(string assetPath, out string menuPath)
        {
            menuPath = null;
            if (string.IsNullOrEmpty(assetPath)) return false;
            return ByAssetPath.TryGetValue(assetPath.Replace('\\', '/'), out menuPath);
        }

        public static string BannerText(string menuPath)
            => $"Este enemigo lo genera {menuPath}: correr el builder sobrescribe el árbol de IA y la ficha. " +
               "Para un cambio permanente, editá el builder.";
    }
}
