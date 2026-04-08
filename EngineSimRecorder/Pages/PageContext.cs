using System;

namespace EngineSimRecorder.Pages
{
    /// <summary>
    /// Static page references + loaded callbacks.
    /// Pages created by NavigationView register themselves here.
    /// </summary>
    public static class PageContext
    {
        public static RecorderPage? Recorder { get; set; }
        public static LogPage? Log { get; set; }
        public static OptionsPage? Options { get; set; }

        public static event Action? RecorderLoaded;
        public static event Action? OptionsLoaded;

        public static void RaiseRecorderLoaded() => RecorderLoaded?.Invoke();
        public static void RaiseOptionsLoaded() => OptionsLoaded?.Invoke();
    }
}
