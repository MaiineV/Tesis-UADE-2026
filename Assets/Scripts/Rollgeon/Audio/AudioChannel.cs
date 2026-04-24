namespace Rollgeon.Audio
{
    /// <summary>
    /// Canales de mezcla expuestos por <see cref="IAudioService"/>.
    /// TECHNICAL.md §17.A.1. Cada uno mapea a un parámetro expuesto del
    /// <c>AudioMixer</c> autoral (ver <see cref="AudioSettingsSO"/>).
    /// </summary>
    public enum AudioChannel
    {
        Master = 0,
        Music  = 1,
        Sfx    = 2,
        Ui     = 3,
    }
}
