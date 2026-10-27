using UnityEngine;

/// <summary>Player preferences shared by every scene and TtsText.</summary>
public static class GameSpeechSettings
{
    public const float MinSpeed = 0.5f;
    public const float MaxSpeed = 1.5f;
    public static float ClampSpeed(float speed) =>
        float.IsNaN(speed) || float.IsInfinity(speed) ? 1.5f : Mathf.Clamp(speed, MinSpeed, MaxSpeed);
    public static TtsText.SpeechLanguage Language { get; private set; }
    public static float Speed { get; private set; }
    public static bool IsChoosing { get; internal set; }
    private static int closedFrame = -1;
    public static bool BlocksGameInput => IsChoosing || TtsSceneLoading.BlocksInput || Time.frameCount == closedFrame;

    internal static void Load()
    {
        Language = PlayerPrefs.GetInt("TTS.Language", 0) == 1
            ? TtsText.SpeechLanguage.English : TtsText.SpeechLanguage.Chinese;
        Speed = ClampSpeed(PlayerPrefs.GetFloat("TTS.Speed", 1.5f));
        closedFrame = -1;
    }

    internal static void PreviewLanguage(TtsText.SpeechLanguage language)
    {
        Language = language;
    }

    internal static void Save(TtsText.SpeechLanguage language, float speed)
    {
        Language = language;
        Speed = ClampSpeed(speed);
        PlayerPrefs.SetInt("TTS.Language", (int)Language);
        PlayerPrefs.SetFloat("TTS.Speed", Speed);
        PlayerPrefs.Save();
        closedFrame = Time.frameCount;
        IsChoosing = false;
    }
}
