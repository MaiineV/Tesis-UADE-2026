namespace Rollgeon.Survey
{
    /// <summary>
    /// Puente entre el define de compilación de la build de evento y el código
    /// testeable. <c>Rollgeon → Build → Windows 64 (Evento)</c> compila el player con
    /// <c>ROLLGEON_EVENT_BUILD</c> vía <c>extraScriptingDefines</c>; el resto de las
    /// builds y el editor no lo tienen.
    /// </summary>
    public static class SurveyDefines
    {
        public const string EventBuildDefine = "ROLLGEON_EVENT_BUILD";

#if ROLLGEON_EVENT_BUILD
        public const bool IsEventBuild = true;
#else
        public const bool IsEventBuild = false;
#endif
    }
}
