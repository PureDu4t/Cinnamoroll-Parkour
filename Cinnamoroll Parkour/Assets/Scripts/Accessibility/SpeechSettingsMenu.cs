using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Keyboard-first spoken settings, automatically shown at game startup.</summary>
[DefaultExecutionOrder(-1000)]
public class SpeechSettingsMenu : MonoBehaviour
{
    private static SpeechSettingsMenu instance;
    private TtsSettingsContent content;
    private GameTtsManager manager;
    private TtsText.SpeechLanguage language;
    private float speed;
    private int previewVersion;
    private double previewAt = -1;
    private string status;
    private enum Step { Loading, Mixer, Language, Speed, Ready, Transition }
    private Step step;
    private int selectedMixerChannel;
    private bool mixerPreviewPending;
    private bool startup;
    private const string SetupScene = "Speech Setup";
    private Font font;
    private Texture2D background;
    private float loadingProgress;
    private bool loadingFailed;
    private TtsLoadingMusic loadingMusic;
    private readonly TtsSceneTransition sceneTransition = new TtsSceneTransition();
    private GUIStyle titleStyle, bodyStyle, smallStyle, valueStyle, cardTitleStyle, actionStyle, sliderStyle, thumbStyle;
    private Texture2D thumbTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        instance = null;
        GameSpeechSettings.Load();
        GameSpeechSettings.IsChoosing = false;
        var root = new GameObject("Speech Settings");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SpeechSettingsMenu>();
    }

    private void Awake()
    {
        content = Resources.Load<TtsSettingsContent>("TtsSettingsContent");
        if (content == null) content = ScriptableObject.CreateInstance<TtsSettingsContent>();
        language = GameSpeechSettings.Language;
        speed = GameSpeechSettings.Speed;
        background = PixelArtSettings.BackgroundOr(Resources.Load<Texture2D>("SpeechSetupBackground"));
        font = Resources.Load<Font>("Fonts/CartoonUiFont") ?? Resources.Load<Font>("Fonts/SpeechUiFont");
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == SetupScene) Open(true);
    }

    private void Open(bool initial)
    {
        if (!initial) UiActionSfx.Play();
        startup = initial;
        GameSpeechSettings.IsChoosing = true;
        language = GameSpeechSettings.Language;
        speed = GameSpeechSettings.Speed;
        step = Step.Loading;
        selectedMixerChannel = 0;
        mixerPreviewPending = false;
        if (startup && Camera.main == null)
        {
            var cameraObject = new GameObject("Setup Camera");
            cameraObject.tag = "MainCamera";
            var setupCamera = cameraObject.AddComponent<Camera>();
            setupCamera.clearFlags = CameraClearFlags.SolidColor;
            setupCamera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            cameraObject.AddComponent<AkAudioListener>();
        }
        manager = GameTtsManager.GetOrCreate();
        if (startup && loadingMusic == null) loadingMusic = TtsLoadingMusic.Create();
        manager.Stop();
        previewAt = -1;
        LoadVoices(++previewVersion);
    }

    public static void OpenFromMenu()
    {
        if (instance != null && !GameSpeechSettings.BlocksGameInput)
        {
            instance.Open(false);
        }
    }

    private async void LoadVoices(int version)
    {
        loadingProgress = 0f;
        loadingFailed = false;
        status = "加载中…… / Loading...";
        const string english = "vits-piper-en_US-lessac-medium";
        const string chinese = "matcha-icefall-zh-baker";
        try
        {
            if (startup) await loadingMusic.BeginAsync();
            if (!IsCurrent(version)) return;
            // The short Loading utterance is the first request, before all other prompts.
            bool played = await manager.SpeakAsync(content.loadingText, 1.5f, english, owner: this);
            if (!IsCurrent(version)) return;
            if (!played) throw new InvalidOperationException("Loading 语音无法播放，请检查 TTS 模型。");
            var jobs = new List<(string text, float rate, string profile)>
            {
                (content.englishInstructions, 1.5f, english), ("English", 1.5f, english),
                (content.englishSpeedPrompt, 1.5f, english), (content.englishEnterPrompt, 1.5f, english),
                (PreviewText(false), speed, english),
                (content.chineseInstructions, 1.5f, chinese), ("中文", 1.5f, chinese),
                (content.chineseSpeedPrompt, 1.5f, chinese), (content.chineseEnterPrompt, 1.5f, chinese),
                (PreviewText(true), speed, chinese)
            };
            jobs.Add((content.chineseMixerPrompt, 1.5f, chinese));
            jobs.Add((content.englishMixerPrompt, 1.5f, english));
            jobs.Add((content.chineseMixerContinuePrompt, 1.5f, chinese));
            jobs.Add((content.englishMixerContinuePrompt, 1.5f, english));
            jobs.Add((content.chineseMixerSample, 1.5f, chinese));
            jobs.Add((content.englishMixerSample, 1.5f, english));
            foreach (string name in new[] { "音乐", "音效", "语音" }) jobs.Add((name, 1.5f, chinese));
            foreach (string name in new[] { "Music", "Sound effects", "Speech" }) jobs.Add((name, 1.5f, english));
            loadingProgress = 1f / (jobs.Count + 1);
            if (startup) loadingMusic.SetProgress(loadingProgress);
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                await manager.PrepareSpeechAsync(job.text, job.rate, job.profile);
                if (!IsCurrent(version)) return;
                loadingProgress = (i + 2f) / (jobs.Count + 1);
                if (startup) loadingMusic.SetProgress(loadingProgress);
            }
            step = Step.Mixer;
            AnnounceMixerPrompt(version);
        }
        catch (Exception exception)
        {
            if (!IsCurrent(version)) return;
            loadingFailed = true;
            status = L("语音加载失败，按 R 重试", "Voice loading failed. Press R to retry.");
            Debug.LogError("TTS 启动加载失败：" + exception.Message);
        }
    }

    private async void AnnounceLanguagePrompt(int version)
    {
        await SpeakPrompt(content.chineseInstructions, "matcha-icefall-zh-baker", version);
        if (IsCurrent(version)) await SpeakPrompt(content.englishInstructions, "vits-piper-en_US-lessac-medium", version);
    }

    private async void AnnounceMixerPrompt(int version)
    {
        await SpeakPrompt(content.chineseMixerPrompt, "matcha-icefall-zh-baker", version);
        if (IsCurrent(version)) await SpeakPrompt(content.englishMixerPrompt, "vits-piper-en_US-lessac-medium", version);
        if (IsCurrent(version)) await SpeakPrompt(content.chineseMixerContinuePrompt, "matcha-icefall-zh-baker", version);
        if (IsCurrent(version)) await SpeakPrompt(content.englishMixerContinuePrompt, "vits-piper-en_US-lessac-medium", version);
        if (IsCurrent(version)) status = MixerHelp;
    }

    private const string MixerHelp = "↑ / ↓ 选择 / Select · ← / → 调节 / Adjust · R 试听 / Preview";

    private void SelectMixerChannel(int index)
    {
        selectedMixerChannel = (index + 3) % 3;
        ++previewVersion;
        previewAt = -1;
        mixerPreviewPending = false;
        manager.StopFor(this);
        UiActionSfx.Play();
        status = MixerHelp;
        AnnounceMixerChannel(previewVersion);
    }

    private async void AnnounceMixerChannel(int version)
    {
        string[] chinese = { "音乐", "音效", "语音" };
        string[] english = { "Music", "Sound effects", "Speech" };
        int channel = selectedMixerChannel;
        await SpeakPrompt(chinese[channel], "matcha-icefall-zh-baker", version);
        if (IsCurrent(version)) await SpeakPrompt(english[channel], "vits-piper-en_US-lessac-medium", version);
        if (IsCurrent(version)) status = MixerHelp;
    }

    private void SetMixerVolume(int index, float value)
    {
        var channel = (GameAudioSettings.Channel)index;
        float next = Mathf.Round(Mathf.Clamp01(value) * 20f) / 20f;
        if (Mathf.Approximately(GameAudioSettings.GetVolume(channel), next)) return;
        selectedMixerChannel = index;
        GameAudioSettings.SetVolume(channel, next);
        status = MixerHelp;
        if (index == 1) UiActionSfx.Play();
        if (index == 2)
        {
            ++previewVersion;
            manager.StopFor(this);
            mixerPreviewPending = true;
            previewAt = Time.realtimeSinceStartupAsDouble + .25;
        }
    }

    private async void PlayMixerPreview(int version)
    {
        if (selectedMixerChannel == 0)
        {
            status = "正在播放的音乐会实时反映音量 / The current music reflects this volume.";
            return;
        }
        if (selectedMixerChannel == 1) { UiActionSfx.Play(); return; }
        await SpeakPrompt(content.chineseMixerSample, "matcha-icefall-zh-baker", version);
        if (IsCurrent(version)) await SpeakPrompt(content.englishMixerSample, "vits-piper-en_US-lessac-medium", version);
        if (IsCurrent(version)) status = MixerHelp;
    }

    private async Task SpeakPrompt(string text, string profile, int version)
    {
        status = step == Step.Language ? content.chineseInstructions + " / " + content.englishInstructions : text;
        try { await manager.SpeakAsync(text, 1.5f, profile, owner: this); }
        catch (Exception exception) { if (IsCurrent(version)) ReportError(exception); }
    }

    private string SelectedProfile => language == TtsText.SpeechLanguage.Chinese
        ? "matcha-icefall-zh-baker" : "vits-piper-en_US-lessac-medium";

    private bool IsCurrent(int version) => this != null && GameSpeechSettings.IsChoosing && version == previewVersion;

    private void Update()
    {
        if (!GameSpeechSettings.IsChoosing)
        {
            if (TtsSceneLoading.BlocksInput) return;
            if (SceneManager.GetActiveScene().name != "Main Menu" && Input.GetKeyDown(KeyCode.F2)) Open(false);
            return;
        }
        if (step == Step.Loading || step == Step.Transition)
        {
            if (loadingFailed && Input.GetKeyDown(KeyCode.R)) RetryLoading();
            return;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            Advance();
            return;
        }
        if (step == Step.Mixer)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) SelectMixerChannel(selectedMixerChannel - 1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) SelectMixerChannel(selectedMixerChannel + 1);
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                SetMixerVolume(selectedMixerChannel, GameAudioSettings.GetVolume((GameAudioSettings.Channel)selectedMixerChannel) - .05f);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                SetMixerVolume(selectedMixerChannel, GameAudioSettings.GetVolume((GameAudioSettings.Channel)selectedMixerChannel) + .05f);
            if (Input.GetKeyDown(KeyCode.R))
            {
                previewAt = -1;
                mixerPreviewPending = false;
                manager.StopFor(this);
                PlayMixerPreview(++previewVersion);
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                ++previewVersion;
                previewAt = -1;
                mixerPreviewPending = false;
                manager.StopFor(this);
                status = "已停止试听 / Preview stopped";
            }
            if (mixerPreviewPending && previewAt >= 0 && Time.realtimeSinceStartupAsDouble >= previewAt)
            {
                previewAt = -1;
                mixerPreviewPending = false;
                PlayMixerPreview(previewVersion);
            }
            return;
        }
        if (step == Step.Language)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SetLanguage(TtsText.SpeechLanguage.Chinese);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SetLanguage(TtsText.SpeechLanguage.English);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D)
                || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                SetLanguage(language == TtsText.SpeechLanguage.Chinese ? TtsText.SpeechLanguage.English : TtsText.SpeechLanguage.Chinese);
        }
        else if (step == Step.Speed)
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) SetSpeed(speed - 0.25f);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) SetSpeed(speed + 0.25f);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (step == Step.Speed) SchedulePreview();
            else if (step == Step.Language) SetLanguage(language);
            else AnnounceReady();
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            UiActionSfx.Play();
            ++previewVersion;
            previewAt = -1;
            manager.StopFor(this);
            status = L("已停止", "Stopped");
        }
        if (previewAt >= 0 && Time.realtimeSinceStartupAsDouble >= previewAt)
        {
            previewAt = -1;
            PlayPreview(previewVersion);
        }
    }

    private void SetLanguage(TtsText.SpeechLanguage value)
    {
        UiActionSfx.Play();
        language = value;
        GameSpeechSettings.PreviewLanguage(value);
        previewAt = -1;
        manager.StopFor(this);
        _ = SpeakPrompt(language == TtsText.SpeechLanguage.Chinese ? "中文" : "English", SelectedProfile, ++previewVersion);
    }

    private void SetSpeed(float value)
    {
        float nextSpeed = Mathf.Round(GameSpeechSettings.ClampSpeed(value) * 4f) / 4f;
        if (Mathf.Abs(speed - nextSpeed) <= 0.001f) return;
        speed = nextSpeed;
        SchedulePreview();
    }

    private void SchedulePreview()
    {
        UiActionSfx.Play();
        ++previewVersion;
        manager.StopFor(this);
        previewAt = Time.realtimeSinceStartupAsDouble + 0.2;
        status = L("正在准备样例……", "Preparing sample…");
    }

    private async void PlayPreview(int version)
    {
        bool chinese = language == TtsText.SpeechLanguage.Chinese;
        try
        {
            bool played = await manager.SpeakAsync(PreviewText(chinese), speed,
                chinese ? "matcha-icefall-zh-baker" : "vits-piper-en_US-lessac-medium", owner: this);
            if (IsCurrent(version)) status = played ? L("按 E 确认语速", "Press E to confirm speed")
                : L("样例未能播放，请检查模型", "Sample unavailable; check voice model");
        }
        catch (Exception exception) { if (IsCurrent(version)) ReportError(exception); }
    }

    private string PreviewText(bool chinese)
    {
        string number = speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return chinese ? "中文，语速 " + number + " 倍。" + content.chineseSample
            : "English. Speech speed " + number + ". " + content.englishSample;
    }

    private void ReportError(Exception exception)
    {
        status = L("语音暂不可用，仍可确认设置", "Voice unavailable; settings can still be confirmed");
        Debug.LogError("TTS 设置预览失败：" + exception.Message);
    }

    private async void Advance()
    {
        UiActionSfx.Play();
        if (step == Step.Mixer)
        {
            GameAudioSettings.Save();
            previewAt = -1;
            mixerPreviewPending = false;
            manager.StopFor(this);
            step = Step.Language;
            AnnounceLanguagePrompt(++previewVersion);
            return;
        }
        if (step == Step.Ready)
        {
            ++previewVersion;
            manager.StopFor(this);
            if (startup)
            {
                step = Step.Transition;
                loadingFailed = false;
                status = L("正在进入游戏……", "Entering game…");
                try
                {
                    var settings = Resources.Load<TtsLoadingAudioSettings>("TtsLoadingAudioSettings");
                    string target = settings != null ? settings.startupTargetScene : "Main Menu";
                    if (string.IsNullOrWhiteSpace(target) || !Application.CanStreamedLevelBeLoaded(target))
                        throw new InvalidOperationException("启动目标 Scene 未加入构建列表：" + target);
                    await SharedMenuSpeech.PrepareAsync(manager, language, speed,
                        value => { loadingProgress = value; loadingMusic.SetProgress(value); });
                    if (this == null) return;
                    await sceneTransition.PrepareAsync(target);
                    if (this == null) return;
                    await loadingMusic.FinishAsync(settings != null ? settings.startupTargetState : "MainTitle");
                    if (this == null) return;
                    GameSpeechSettings.Save(language, speed);
                    sceneTransition.Activate();
                }
                catch (Exception exception)
                {
                    loadingFailed = true;
                    status = L("跳转失败，按 R 重试", "Transition failed. Press R to retry.");
                    Debug.LogError(exception.Message);
                }
            }
            else
            {
                step = Step.Transition;
                loadingFailed = false;
                try
                {
                    await SharedMenuSpeech.PrepareAsync(manager, language, speed,
                        value => loadingProgress = value);
                    if (this == null) return;
                    GameSpeechSettings.Save(language, speed);
                    manager.RefreshScenePrewarm();
                }
                catch (Exception exception)
                {
                    loadingFailed = true;
                    status = L("语音准备失败，按 R 重试", "Voice preparation failed. Press R to retry.");
                    Debug.LogError(exception.Message);
                }
            }
            return;
        }
        previewAt = -1;
        manager.StopFor(this);
        if (step == Step.Language)
        {
            step = Step.Speed;
            int version = ++previewVersion;
            await SpeakPrompt(language == TtsText.SpeechLanguage.Chinese ? content.chineseSpeedPrompt : content.englishSpeedPrompt,
                SelectedProfile, version);
            if (IsCurrent(version)) SchedulePreview();
        }
        else
        {
            step = Step.Ready;
            AnnounceReady();
        }
    }

    private void RetryLoading()
    {
        if (step == Step.Transition)
        {
            step = Step.Ready;
            Advance();
        }
        else
        {
            UiActionSfx.Play();
            LoadVoices(++previewVersion);
        }
    }

    private void AnnounceReady()
    {
        manager.StopFor(this);
        _ = SpeakPrompt(language == TtsText.SpeechLanguage.Chinese ? content.chineseEnterPrompt : content.englishEnterPrompt,
            SelectedProfile, ++previewVersion);
    }

    private static readonly Color Ink = new Color(0.12f, 0.23f, 0.36f);
    private static readonly Color MutedInk = new Color(0.34f, 0.44f, 0.55f);
    private static readonly Color Accent = new Color(0.25f, 0.43f, 0.77f);

    private GUIStyle TextStyle(int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            font = font, fontSize = size, alignment = alignment, wordWrap = true,
            padding = new RectOffset(0, 0, 0, 0)
        };
        style.normal.textColor = color;
        return style;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = TextStyle(34, Ink);
        bodyStyle = TextStyle(17, MutedInk);
        smallStyle = TextStyle(13, MutedInk);
        valueStyle = TextStyle(56, Ink, TextAnchor.MiddleCenter);
        cardTitleStyle = TextStyle(38, Ink);
        actionStyle = TextStyle(19, Color.white, TextAnchor.MiddleCenter);
        thumbTexture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                float alpha = Mathf.Clamp01(12f - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(12, 12)));
                thumbTexture.SetPixel(x, y, new Color(Accent.r, Accent.g, Accent.b, alpha));
            }
        thumbTexture.Apply();
        sliderStyle = new GUIStyle { fixedHeight = 24 };
        thumbStyle = new GUIStyle { fixedWidth = 24, fixedHeight = 24 };
    }

    private static void Rounded(Rect rect, Color color, float radius = 18)
    {
        var theme = PixelArtSettings.Current;
        if (theme != null && theme.enabled && radius > 0)
        {
            PixelArtUi.Frame(rect, color, color);
            return;
        }
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);
    }

    private bool CardButton(Rect rect, string label, bool primary = false)
    {
        var theme = PixelArtSettings.Current;
        if (theme != null && theme.enabled && theme.uiSkin != null)
            return PixelArtUi.Button(rect, label, theme, primary);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        Rounded(rect, primary ? Accent : new Color(.89f, .93f, .99f), 14);
        GUI.Label(rect, label, primary ? actionStyle : TextStyle(17, Ink, TextAnchor.MiddleCenter));
        return clicked;
    }

    private void DrawLanguageCard(Rect rect, bool chinese)
    {
        bool selected = chinese == (language == TtsText.SpeechLanguage.Chinese);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        var theme = PixelArtSettings.Current;
        if (theme != null && theme.enabled && theme.uiSkin != null)
            PixelArtUi.Skin(rect, theme.uiSkin, selected ? 2 : 1);
        else
        {
            Rounded(rect, selected ? Accent : new Color(.82f, .88f, .95f));
            Rounded(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4),
                selected ? new Color(.91f, .95f, 1f) : new Color(.98f, .99f, 1f), 16);
        }
        GUI.Label(new Rect(rect.x + 24, rect.y + 17, rect.width - 48, 60), chinese ? L("中文", "Chinese") : L("英语", "English"), cardTitleStyle);
        GUI.Label(new Rect(rect.x + 24, rect.y + 84, rect.width - 48, 24), chinese ? L("简体中文", "Chinese") : L("英语", "English"), bodyStyle);
        GUI.Label(new Rect(rect.x + 24, rect.y + 119, rect.width - 48, 25), selected ? L("已选择", "Selected") : chinese ? L("按 1 选择", "Press 1") : L("按 2 选择", "Press 2"), smallStyle);
        if (clicked) SetLanguage(chinese ? TtsText.SpeechLanguage.Chinese : TtsText.SpeechLanguage.English);
    }

    private void DrawMixer()
    {
        GUI.Label(new Rect(105, 263, 684, 24),
            "音乐、音效、朗读分别调节 / Separate music, effects and speech", smallStyle);
        string[] names = { "音乐 / Music", "音效 / Sound Effects", "语音 / TTS" };
        string[] hints = { "包含音乐过渡 / Includes musical transitions",
            "UI、脚步、播报音效等 / UI, footsteps, navigation",
            "所有 TTS 朗读 / All synthesized speech" };
        for (int i = 0; i < 3; i++)
        {
            float y = 295 + i * 76;
            bool selected = selectedMixerChannel == i;
            Rect row = new Rect(105, y, 684, 68);
            Rounded(row, selected ? new Color(.87f, .93f, 1f) : new Color(.96f, .97f, .99f), 12);
            if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition)
                && selectedMixerChannel != i) SelectMixerChannel(i);
            GUI.Label(new Rect(123, y + 9, 255, 28), (selected ? "> " : "") + names[i],
                TextStyle(18, Ink, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(123, y + 39, 300, 22), hints[i], TextStyle(11, MutedInk));
            float volume = GameAudioSettings.GetVolume((GameAudioSettings.Channel)i);
            Rounded(new Rect(408, y + 32, 264, 8), new Color(.80f, .87f, .96f), 4);
            float next = GUI.HorizontalSlider(new Rect(396, y + 24, 288, 24),
                volume, 0f, 1f, sliderStyle, thumbStyle);
            GUI.DrawTexture(new Rect(396 + next * 264, y + 24, 24, 24), thumbTexture);
            GUI.Label(new Rect(697, y + 21, 72, 30), Mathf.RoundToInt(volume * 100) + "%",
                TextStyle(20, Ink, TextAnchor.MiddleRight));
            if (!Mathf.Approximately(next, volume)) SetMixerVolume(i, next);
        }
    }

    private string L(string chinese, string english) => language == TtsText.SpeechLanguage.Chinese ? chinese : english;

    private void OnGUI()
    {
        if (!GameSpeechSettings.IsChoosing) return;
        if (background != null)
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background, ScaleMode.ScaleAndCrop);
        Rounded(new Rect(0, 0, Screen.width, Screen.height), new Color(.88f, .94f, 1f, .15f), 0);
        EnsureStyles();
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUI.color = Color.white;
        float scale = Mathf.Min(Screen.width / 1200f, Screen.height / 760f);
        // Center the actual 780×620 panel, whose local origin is (70,65),
        // instead of centering the wider historical 1200×760 canvas.
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 780 * scale) / 2 - 70 * scale,
            (Screen.height - 620 * scale) / 2 - 65 * scale), Quaternion.identity, Vector3.one * scale);
        Rounded(new Rect(70, 65, 780, 620), new Color(1f, 1f, 1f, .97f), 28);
        GUI.Label(new Rect(105, 92, 680, 24), "玉桂狗跑酷 / CINNAMOROLL PARKOUR", smallStyle);
        int stage = step == Step.Mixer ? 0 : step == Step.Language ? 1 : step == Step.Speed ? 2 : step == Step.Ready || step == Step.Transition ? 3 : -1;
        string[] stages = { "01 音量 / Audio", "02 语言 / Language", "03 语速 / Speed", "04 出发 / Ready" };
        for (int i = 0; i < 4; i++)
        {
            Rect chip = new Rect(105 + i * 174, 135, 162, 34);
            Rounded(chip, stage == i ? new Color(.88f, .93f, 1f) : new Color(.96f, .97f, .99f), 12);
            GUI.Label(chip, stages[i], TextStyle(13, stage == i ? Accent : MutedInk, TextAnchor.MiddleCenter));
        }
        if (step == Step.Loading || step == Step.Transition)
        {
            GUI.Label(new Rect(105, 205, 690, 54), step == Step.Transition ? L("即将出发", "Ready to set off") : "正在准备声音", titleStyle);
            GUI.Label(new Rect(105, 265, 690, 32), step == Step.Transition ? L("冒险即将开始", "Your adventure is about to begin") : "Getting your voice ready…", bodyStyle);
            GUI.Label(new Rect(105, 330, 684, 85), Mathf.FloorToInt(loadingProgress * 100) + "%", valueStyle);
            PixelArtUi.Progress(new Rect(105, 440, 684, 28), loadingProgress);
            GUI.Label(new Rect(105, 490, 684, 78), status, bodyStyle);
            if (loadingFailed && CardButton(new Rect(105, 590, 684, 58), L("重试    R", "Retry    R"), true)) RetryLoading();
        }
        else
        {
            string heading = step == Step.Mixer ? "调整音量 / Adjust Volume" : step == Step.Language ? "选择你的语言 / Choose Your Language" : step == Step.Speed ? L("找到舒适的语速", "Find a comfortable speed") : L("准备出发", "Ready to go");
            string subtitle = step == Step.Language ? "" : step == Step.Speed ? L("调整语速，实时聆听效果。", "Adjust the speed. Hear a live preview.") : L("语音设置已就绪。", "Your voice settings are ready.");
            if (step == Step.Language || step == Step.Mixer)
            {
                // One label keeps both languages at exactly the same font and size.
                var bilingualTitleStyle = TextStyle(34, Ink);
                bilingualTitleStyle.wordWrap = false;
                while (bilingualTitleStyle.fontSize > 10
                    && bilingualTitleStyle.CalcSize(new GUIContent(heading)).x > 684)
                    bilingualTitleStyle.fontSize--;
                GUI.Label(new Rect(105, 205, 684, 55), heading, bilingualTitleStyle);
            }
            else
            {
                GUI.Label(new Rect(105, 205, 684, 55), heading, titleStyle);
                GUI.Label(new Rect(105, 265, 684, 32), subtitle, bodyStyle);
            }
            if (step == Step.Mixer)
            {
                DrawMixer();
            }
            else if (step == Step.Language)
            {
                DrawLanguageCard(new Rect(105, 320, 334, 160), true);
                DrawLanguageCard(new Rect(455, 320, 334, 160), false);
                GUI.Label(new Rect(105, 500, 684, 30), L("A / D 或 ← / → 切换    ·    1 中文 / 2 英语", "A / D or ← / → to switch    ·    1 Chinese / 2 English"), smallStyle);
            }
            else if (step == Step.Speed)
            {
                GUI.Label(new Rect(235, 302, 424, 80), speed.ToString("0.##") + "×", valueStyle);
                if (CardButton(new Rect(105, 318, 90, 54), "−")) SetSpeed(speed - .25f);
                if (CardButton(new Rect(699, 318, 90, 54), "+")) SetSpeed(speed + .25f);
                Rounded(new Rect(129, 406, 636, 8), new Color(.80f, .87f, .96f), 4);
                float newSpeed = GUI.HorizontalSlider(new Rect(117, 398, 660, 24), speed, GameSpeechSettings.MinSpeed, GameSpeechSettings.MaxSpeed, sliderStyle, thumbStyle);
                float thumbX = 117 + Mathf.InverseLerp(GameSpeechSettings.MinSpeed, GameSpeechSettings.MaxSpeed, newSpeed) * 636;
                GUI.DrawTexture(new Rect(thumbX, 398, 24, 24), thumbTexture);
                float roundedSpeed = Mathf.Round(newSpeed * 4f) / 4f;
                if (!Mathf.Approximately(roundedSpeed, speed)) SetSpeed(roundedSpeed);
                GUI.Label(new Rect(105, 430, 90, 26), "0.5×", smallStyle);
                GUI.Label(new Rect(260, 430, 380, 26), L("A / D 或 ← / →", "A / D or ← / →"), TextStyle(13, MutedInk, TextAnchor.UpperCenter));
                GUI.Label(new Rect(699, 430, 90, 26), "1.5×", TextStyle(13, MutedInk, TextAnchor.UpperRight));
                if (CardButton(new Rect(105, 474, 334, 42), L("重听样例    R", "Replay sample    R"))) SchedulePreview();
                if (CardButton(new Rect(455, 474, 334, 42), L("停止试听    Q", "Stop preview    Q")))
                {
                    ++previewVersion; previewAt = -1; manager.StopFor(this); status = L("已停止", "Stopped");
                }
            }
            else
            {
                Rounded(new Rect(105, 325, 684, 156), new Color(.93f, .96f, 1f));
                GUI.Label(new Rect(132, 347, 630, 48), language == TtsText.SpeechLanguage.Chinese ? L("中文", "Chinese") : L("英语", "English"), cardTitleStyle);
                GUI.Label(new Rect(132, 412, 630, 38), L("语速    ", "Speech speed    ") + speed.ToString("0.##") + "×", bodyStyle);
            }
            GUI.Label(new Rect(105, 535, 684, 45), status, smallStyle);
            if (CardButton(new Rect(105, 590, 684, 58), step == Step.Mixer ? "继续 / Continue    E" : step == Step.Ready ? L("进入游戏    E", "Enter game    E") : L("继续    E", "Continue    E"), true)) Advance();
        }
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        ++previewVersion;
        GameSpeechSettings.IsChoosing = false;
        GameAudioSettings.Save();
        if (thumbTexture != null) Destroy(thumbTexture);
    }
}
