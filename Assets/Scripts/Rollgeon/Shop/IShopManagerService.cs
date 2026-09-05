using System;
using System.Collections.Generic;
using Rollgeon.Dungeon;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Coordina shop rooms a nivel de piso. TECHNICAL.md §17.F.1. Escucha
    /// <c>OnRoomEntered</c> y hace lazy-init de los slots la primera vez que
    /// el jugador entra a una room de tipo <c>Shop</c>; re-entradas re-leen
    /// los <c>ShopItemState</c> ya persistidos sin re-rolear.
    /// </summary>
    /// <remarks>
    /// MVP: registrado en <see cref="ServiceScope.Global"/> vía
    /// <c>ShopManagerBootstrap</c>. Los pedestales instanciados viven dentro
    /// del <c>RoomInstance.SpawnedPrefab</c> — no hay teardown por transición
    /// porque el <c>DungeonManager</c> mantiene los prefabs instanciados toda
    /// la run (§13.6).
    /// </remarks>
    public interface IShopManagerService
    {
        /// <summary>
        /// Slots actualmente cargados — incluye purchased. La vista los filtra
        /// si necesita. Ordenados por <c>SpawnPointId</c> ascendente.
        /// </summary>
        IReadOnlyList<ShopSlot> GetSlots(Guid roomInstanceId);

        /// <summary>
        /// <c>true</c> si el service ya cargó (o roleó por primera vez) los
        /// slots de esa room. Diagnóstico / tests.
        /// </summary>
        bool IsInitialized(Guid roomInstanceId);

        /// <summary>
        /// Busca el slot de <paramref name="spawnPointId"/> en la room indicada.
        /// Devuelve <c>null</c> si no existe o ya está purchased.
        /// </summary>
        ShopSlot FindActiveSlot(Guid roomInstanceId, string spawnPointId);

        /// <summary>
        /// Callback del <c>ShopItemPedestalInteractable</c> cuando cerró una
        /// compra. Marca el slot + <c>ShopItemState</c> como purchased, destruye
        /// el visual, dispara <c>OnShopItemPurchased</c>. No cobra gold — eso
        /// ya pasó en el interactable (MVP) o lo hará el <c>EffDeductGold</c>
        /// cuando aterrice.
        /// </summary>
        void NotifyItemPurchased(Guid roomInstanceId, string spawnPointId, int pricePaid);

        /// <summary><c>true</c> si el <c>ShopConfigSO</c> activo permite restock y quedan usos
        /// (<c>MaxRestocks &lt;= 0</c> = infinitos).</summary>
        bool CanRestock(Guid roomInstanceId);

        /// <summary>Costo del PRÓXIMO uso de la máquina — base × mult^usos, la misma
        /// fórmula compuesta que el reroll del altar de encantamientos.</summary>
        int GetRestockCost(Guid roomInstanceId);

        /// <summary>
        /// La máquina de reroll (§17.F.5): cobra el oro, re-rolea TODOS los slots —
        /// incluidos los comprados (Isaac real) — con precios nuevos, persiste el
        /// stock y los usos en <c>ObjectStates</c>, y anima el recambio de visuales.
        /// <c>false</c> si no está permitido, no quedan usos o el oro no alcanza.
        /// </summary>
        bool TryRestock(Guid roomInstanceId);

        /// <summary>Compat: <see cref="TryRestock"/> descartando el resultado.</summary>
        void Restock(Guid roomInstanceId);

        /// <summary>
        /// Bootstrap hook — llamado por el <c>DungeonManager</c> si decide
        /// cablear el init al crear el piso. En el MVP el init es lazy vía
        /// <c>OnRoomEntered</c>; este método queda para que el caller externo
        /// force una room concreta (tests).
        /// </summary>
        void Initialize(RoomInstance room, int floorDepth);

        /// <summary>
        /// Override de config/pool para el tutorial (tienda de 1 solo item).
        /// Afecta rooms que se inicializan DESPUÉS del set. El service es Global —
        /// el teardown del tutorial DEBE llamar <see cref="ClearTutorialOverride"/>.
        /// </summary>
        void SetTutorialOverride(ShopConfigSO config, ShopPoolSO pool);

        /// <summary>Vuelve al config/pool del bootstrap. Safe si no había override.</summary>
        void ClearTutorialOverride();
    }
}
