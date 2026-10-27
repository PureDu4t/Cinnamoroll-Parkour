using System;
using UnityEngine;

/// <summary>Three independent saved gains. Wwise owns music/SFX; Unity owns synthesized speech.</summary>
public static class GameAudioSettings
{
    public enum Channel { Music, SoundEffects, Tts }
    private static readonly float[] volumes = { 1f, 1f, 1f };
    private static readonly string[] keys = { "Audio.Music", "Audio.SoundEffects", "Audio.Tts" };
    private static readonly bool[] warned = new bool[2];
    private static bool wwiseReady;
    private static GameAudioMixerSettings settings;
    public static event Action VolumeChanged;
    public static float TtsVolume => volumes[2];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Load()
    {
        VolumeChanged = null;
        settings = null;
        wwiseReady = false;
        for (int i = 0; i < volumes.Length; i++)
        {
            float saved = PlayerPrefs.GetFloat(keys[i], 1f);
            volumes[i] = float.IsNaN(saved) || float.IsInfinity(saved) ? 1f : Mathf.Clamp01(saved);
        }
        Array.Clear(warned, 0, warned.Length);
    }

    public static float GetVolume(Channel channel) => volumes[(int)channel];

    public static void SetVolume(Channel channel, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        int index = (int)channel;
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(volumes[index], value)) return;
        volumes[index] = value;
        PlayerPrefs.SetFloat(keys[index], value);
        if (index < 2 && wwiseReady) ApplyChannel(index);
        VolumeChanged?.Invoke();
    }

    public static void Save() => PlayerPrefs.Save();

    /// <summary>Called after the loading SoundBank is ready, before its music starts.</summary>
    public static void ApplyWwiseVolumes()
    {
        if (!AkSoundEngine.IsInitialized()) { wwiseReady = false; return; }
        wwiseReady = true;
        settings = Resources.Load<GameAudioMixerSettings>("AudioMixerSettings");
        ApplyChannel(0);
        ApplyChannel(1);
    }

    private static void ApplyChannel(int index)
    {
        if (!AkSoundEngine.IsInitialized()) { wwiseReady = false; return; }
        string rtpc = index == 0
            ? (settings != null ? settings.musicVolumeRtpc : "Music_Volume")
            : (settings != null ? settings.soundEffectsVolumeRtpc : "SFX_Volume");
        if (string.IsNullOrWhiteSpace(rtpc)) return;
        int milliseconds = settings != null ? Mathf.Max(0, settings.fadeMilliseconds) : 50;
        var result = AkSoundEngine.SetRTPCValue(rtpc, volumes[index] * 100f,
            (GameObject)null, milliseconds);
        if (result == AKRESULT.AK_Success) { warned[index] = false; return; }
        if (warned[index]) return;
        warned[index] = true;
        Debug.LogWarning("音量 Mixer：无法设置 " + rtpc + "（" + result
            + "）。请配置对应的 Wwise Game Parameter、Bus RTPC 并重新生成 SoundBanks。");
    }
}
