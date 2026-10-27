using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Sherpa's shared assembly references Microphone even with ASR disabled.</summary>
internal sealed class MacSpeechBuildSettings : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    [InitializeOnLoadMethod]
    private static void Configure()
    {
        if (!string.IsNullOrWhiteSpace(PlayerSettings.macOS.microphoneUsageDescription)) return;
        // A usage declaration does not request permission or start microphone recording.
        PlayerSettings.macOS.microphoneUsageDescription =
            "Microphone input is used only when speech recognition is enabled.";
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.StandaloneOSX) Configure();
    }
}
