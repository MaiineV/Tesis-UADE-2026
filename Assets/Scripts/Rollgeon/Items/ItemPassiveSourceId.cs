using System;
using System.Security.Cryptography;
using System.Text;

namespace Rollgeon.Items
{
    /// <summary>
    /// Deriva un <see cref="Guid"/> determinístico por <c>ItemId</c>, usado como
    /// <c>Modifier.SourceId</c> de todo lo que un item pasivo aplica sobre el jugador.
    /// </summary>
    /// <remarks>
    /// Determinístico a propósito: remover el item barre TODO lo suyo con
    /// <c>AttributesManager.RemoveAllModifiersBySource</c> sin bookkeeping de ids de
    /// instancia — cubre también modifiers que agregó un effect del propio item
    /// (no solo el lifecycle del inventario) y sobrevive save/restore. MD5 no es uso
    /// criptográfico acá, solo hashing estable de 128 bits → Guid.
    /// </remarks>
    public static class ItemPassiveSourceId
    {
        public static Guid For(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return Guid.Empty;
            using (var md5 = MD5.Create())
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes("item:" + itemId)));
        }
    }
}
