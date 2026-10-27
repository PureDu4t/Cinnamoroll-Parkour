using UnityEngine;

[CreateAssetMenu(menuName = "Cinnamoroll Parkour/Audio Mixer Settings")]
public sealed class GameAudioMixerSettings : ScriptableObject
{
    [Tooltip("全局 Wwise Game Parameter，范围 0–100，控制 Music Audio Bus 音量。")]
    public string musicVolumeRtpc = "Music_Volume";
    [Tooltip("全局 Wwise Game Parameter，范围 0–100，控制 SFX Audio Bus 音量。")]
    public string soundEffectsVolumeRtpc = "SFX_Volume";
    [Min(0), Tooltip("调节 Wwise 音量时的平滑过渡时间，毫秒。")]
    public int fadeMilliseconds = 50;
}
