using UnityEngine;

[CreateAssetMenu(menuName = "Cinnamoroll Parkour/TTS Settings Content")]
public class TtsSettingsContent : ScriptableObject
{
    public string loadingText = "Loading";
    [TextArea(3, 8)] public string chineseInstructions = "选择你的语言。";
    [TextArea(3, 8)] public string englishInstructions = "Choose your language.";
    public string chineseSpeedPrompt = "请选择你的语速。";
    public string englishSpeedPrompt = "Please choose your speech speed.";
    public string chineseEnterPrompt = "按 E 进入游戏。";
    public string englishEnterPrompt = "Press E to enter the game.";
    [TextArea(3, 8)] public string chineseSample = "这是当前语音的效果。欢迎来到玉桂狗的世界！请听听这个速度是否适合你。";
    [TextArea(3, 8)] public string englishSample = "This is your current voice and speed. Welcome to Cinnamoroll's world! Listen and choose a comfortable speed.";
}
