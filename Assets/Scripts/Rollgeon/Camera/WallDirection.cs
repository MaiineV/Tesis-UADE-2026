namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Etiqueta que el diseñador pone en cada <see cref="WallOccluder"/> del
    /// prefab de sala. El <see cref="CameraService"/> la cruza contra
    /// <see cref="CameraConfigSO.OcclusionMap"/> para decidir qué paredes
    /// ocultar según el <see cref="CameraFacing"/> actual (§17.E.8).
    /// </summary>
    public enum WallDirection
    {
        N,
        NE,
        E,
        SE,
        S,
        SW,
        W,
        NW,
    }
}
