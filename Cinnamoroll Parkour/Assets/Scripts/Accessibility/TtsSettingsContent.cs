using UnityEngine;

[CreateAssetMenu(menuName = "Cinnamoroll Parkour/TTS Settings Content")]
public class TtsSettingsContent : ScriptableObject
{
    public string loadingText = "Loading";
    [Header("音量设置（语言选择之前，双语）")]
    [TextArea(2, 5)] public string chineseMixerPrompt = "请调整音乐、音效和语音的音量。上下方向键选择，左右方向键调节。";
    [TextArea(2, 5)] public string englishMixerPrompt = "Adjust music, sound effects, and speech volume. Use up and down to select, left and right to adjust.";
    public string chineseMixerContinuePrompt = "按易继续。";
    public string englishMixerContinuePrompt = "Press E to continue.";
    public string chineseMixerSample = "这是语音音量的试听。";
    public string englishMixerSample = "This is the speech volume preview.";
    [TextArea(3, 8)] public string chineseInstructions = "选择你的语言。";
    [TextArea(3, 8)] public string englishInstructions = "Choose your language.";
    public string chineseSpeedPrompt = "请选择你的语速。";
    public string englishSpeedPrompt = "Please choose your speech speed.";
    public string chineseEnterPrompt = "按 E 进入游戏。";
    public string englishEnterPrompt = "Press E to enter the game.";
    [TextArea(3, 8)] public string chineseSample = "这是当前语音的效果。欢迎来到玉桂狗的世界！请听听这个速度是否适合你。";
    [TextArea(3, 8)] public string englishSample = "This is your current voice and speed. Welcome to Cinnamoroll's world! Listen and choose a comfortable speed.";
}
