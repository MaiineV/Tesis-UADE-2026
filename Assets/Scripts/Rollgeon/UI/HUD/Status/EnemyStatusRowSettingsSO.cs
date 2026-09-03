using UnityEngine;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Cómo se ve la fila de estados que flota sobre un enemigo. Se carga por
    /// <see cref="Resources"/> porque la fila la cuelga el spawn de cada enemigo por código.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Enemy Status Row Settings",
        fileName = "EnemyStatusRowSettings")]
    public sealed class EnemyStatusRowSettingsSO : ScriptableObject
    {
        public const string ResourcePath = "UI/EnemyStatusRowSettings";

        [Tooltip("Dibuja la fila de íconos flotando sobre la cabeza. Apagada (decisión 03/09): " +
                 "los estados se leen en el panel del tooltip; la fila duplicaba esa info y " +
                 "ensuciaba la lectura del bicho. El objeto igual se crea — es la fuente de las " +
                 "tarjetas del panel y del badge de aura del Guardian. Bool nuevo deserializa " +
                 "FALSE en el asset ya autorado: apagada por defecto, se re-activa tildando acá.")]
        public bool ShowFloatingRow;

        [Tooltip("Prefab de un ícono suelto. Se reusa el mismo que la fila del player.")]
        public StatusEffectIconView IconPrefab;

        [Tooltip("Mapa id → sprite. El mismo catálogo que el player: el ícono que flota sobre el " +
                 "enemigo y el de su tarjeta tienen que ser la misma imagen.")]
        public StatusIconCatalogSO Catalog;

        [Tooltip("Offset local sobre el pawn, solo para uno sin barra de vida.")]
        public Vector3 Offset = new Vector3(0f, 2.6f, 0f);

        [Tooltip("Alto del centro de la fila sobre la barra de vida, en unidades de mundo. " +
                 "Escala con el zoom, igual que la barra.")]
        public float LiftAboveBar = 1f;

        [Tooltip("Lado del ícono en píxeles de canvas.")]
        public float IconSize = 28f;

        [Tooltip("Separación entre íconos.")]
        public float Spacing = 4f;

        [Tooltip("orthographicSize de referencia al que la fila queda a escala 1x. El mismo que " +
                 "usa la barra de vida, para que crezcan juntas al alejar la cámara.")]
        public float ReferenceZoom = 9f;

        [Tooltip("Escala del canvas world-space: convierte los píxeles del prefab (72x76 el " +
                 "ícono) a unidades de mundo.")]
        public float WorldScale = 0.01f;
    }
}
