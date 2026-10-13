using UnityEngine;

/// <summary>Player preferences shared by every scene and TtsText.</summary>
public static class GameSpeechSettings
{
    public static TtsText.SpeechLanguage Language { get; private set; }
    public static float Speed { get; private set; }
    public static bool IsChoosing { get; internal set; }
    private static int closedFrame = -1;
    public static bool BlocksGameInput => IsChoosing || TtsSceneLoading.BlocksInput || Time.frameCount == closedFrame;

    internal static void Load()
    {
        Language = PlayerPrefs.GetInt("TTS.Language", 0) == 1
            ? TtsText.SpeechLanguage.English : TtsText.SpeechLanguage.Chinese;
        float saved = PlayerPrefs.GetFloat("TTS.Speed", 2f);
        Speed = float.IsNaN(saved) || float.IsInfinity(saved) ? 2f : Mathf.Clamp(saved, 0.5f, 4f);
        closedFrame = -1;
    }

    internal static void Save(TtsText.SpeechLanguage language, float speed)
    {
        Language = language;
        Speed = Mathf.Clamp(speed, 0.5f, 4f);
        PlayerPrefs.SetInt("TTS.Language", (int)Language);
        PlayerPrefs.SetFloat("TTS.Speed", Speed);
        PlayerPrefs.Save();
        closedFrame = Time.frameCount;
        IsChoosing = false;
    }
}
