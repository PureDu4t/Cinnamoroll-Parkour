using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneKeyboardNavigator : MonoBehaviour
{
    [Serializable]
    private class KeySceneCommand
    {
        public string label;
        public KeyCode key = KeyCode.None;
        public string sceneName;
        public bool quitApplication;
    }

    [SerializeField] private List<KeySceneCommand> commands =
        new List<KeySceneCommand>();

    [SerializeField] private bool debugLogging;

    [Serializable]
    public class MainMenuSpeech
    {
        [TextArea(2, 5)] public string chineseText;
        [TextArea(2, 5)] public string englishText;
    }

    [Header("Main Menu：依次为进入关卡、Tutorial、语音设置、退出")]
    [SerializeField] private MainMenuSpeech[] mainMenuSpeech =
    {
        new MainMenuSpeech { chineseText = "进入关卡", englishText = "Start adventure" },
        new MainMenuSpeech { chineseText = "教学关卡", englishText = "Tutorial" },
        new MainMenuSpeech { chineseText = "语音设置", englishText = "Voice settings" },
        new MainMenuSpeech { chineseText = "退出游戏", englishText = "Quit game" }
    };
    private int selectedMainMenuItem;
    public int SelectedMainMenuItem => selectedMainMenuItem;

    public void ActivateMainMenuItem(int index)
    {
        if (isLoading || GameSpeechSettings.BlocksGameInput) return;
        selectedMainMenuItem = Mathf.Clamp(index, 0, 3);
        switch (selectedMainMenuItem)
        {
            case 0: NavigateFromUI("Level Select"); break;
            case 1: NavigateFromUI("Tutorial"); break;
            case 2: SpeechSettingsMenu.OpenFromMenu(); break;
            case 3: QuitFromUI(); break;
        }
    }

    private void FocusMainMenuItem(int index)
    {
        selectedMainMenuItem = (index + 4) % 4;
        UiActionSfx.Play();
        if (mainMenuSpeech == null || selectedMainMenuItem >= mainMenuSpeech.Length) return;
        var speech = mainMenuSpeech[selectedMainMenuItem];
        if (speech == null) return;
        var text = GetComponent<TtsText>();
        if (text == null) text = gameObject.AddComponent<TtsText>();
        text.PlayBilingual(speech.chineseText, speech.englishText);
    }

    private bool isLoading;
    private bool wasChoosingSpeech;

    private void OnEnable()
    {
        selectedMainMenuItem = 0;
        wasChoosingSpeech = GameSpeechSettings.IsChoosing;
    }

    private IEnumerator Start()
    {
        if (gameObject.scene.name != "Main Menu") yield break;
        // Cached menus skip Loading, so restore their music independently of that flow.
        while (GameSpeechSettings.IsChoosing || TtsSceneLoading.IsLoading(gameObject.scene))
            yield return null;
        var settings = Resources.Load<TtsLoadingAudioSettings>("TtsLoadingAudioSettings");
        if (settings == null || string.IsNullOrWhiteSpace(settings.loadingStateGroup))
        {
            Debug.LogError("主菜单无法恢复音乐 State：缺少 TtsLoadingAudioSettings 或 State Group。", this);
            yield break;
        }
        string state = settings.StateFor(gameObject.scene.name);
        double deadline = Time.realtimeSinceStartupAsDouble + settings.initializationTimeout;
        while (!AkSoundEngine.IsInitialized() && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        if (!AkSoundEngine.IsInitialized())
        {
            Debug.LogError("主菜单恢复音乐 State 失败：Wwise 初始化超时。", this);
            yield break;
        }
        var result = AkSoundEngine.SetState(settings.loadingStateGroup, state);
        if (result != AKRESULT.AK_Success)
            Debug.LogError("主菜单恢复音乐 State 失败：" + settings.loadingStateGroup + "/" + state, this);
    }

    public void NavigateFromUI(string sceneName)
    {
        if (!isLoading && !GameSpeechSettings.BlocksGameInput)
            Execute(new KeySceneCommand { sceneName = sceneName });
    }
    public void QuitFromUI()
    {
        if (!GameSpeechSettings.BlocksGameInput) QuitApplication();
    }

    private void Update()
    {
        if (gameObject.scene.name == "Main Menu")
        {
            if (wasChoosingSpeech && !GameSpeechSettings.IsChoosing)
                selectedMainMenuItem = 0;
            wasChoosingSpeech = GameSpeechSettings.IsChoosing;
        }
        if (GameSpeechSettings.BlocksGameInput) return;
        if (isLoading)
        {
            return;
        }

        if (gameObject.scene.name == "Main Menu")
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                FocusMainMenuItem(selectedMainMenuItem - 1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                FocusMainMenuItem(selectedMainMenuItem + 1);
            else if (Input.GetKeyDown(KeyCode.E)) ActivateMainMenuItem(selectedMainMenuItem);
            else if (Input.GetKeyDown(KeyCode.T)) ActivateMainMenuItem(1);
            else if (Input.GetKeyDown(KeyCode.S)) ActivateMainMenuItem(2);
            else if (Input.GetKeyDown(KeyCode.Escape)) ActivateMainMenuItem(3);
            return;
        }

        for (int i = 0; i < commands.Count; i++)
        {
            KeySceneCommand command = commands[i];

            if (command == null || command.key == KeyCode.None)
            {
                continue;
            }

            if (!Input.GetKeyDown(command.key))
            {
                continue;
            }

            Execute(command);
            return;
        }
    }

    private void Execute(KeySceneCommand command)
    {
        if (command.quitApplication)
        {
            QuitApplication();
            return;
        }

        if (string.IsNullOrWhiteSpace(command.sceneName))
        {
            if (debugLogging)
            {
                Debug.Log(
                    name + " received "
                    + command.key
                    + " for "
                    + command.label
                    + ", but no Scene is configured yet."
                );
            }

            return;
        }

        UiActionSfx.Play();
        isLoading = true;
        // Read scene assets asynchronously so navigation does not force a synchronous load.
        SceneManager.LoadSceneAsync(command.sceneName, LoadSceneMode.Single);
    }

    private void QuitApplication()
    {
        if (debugLogging)
        {
            Debug.Log(name + " quitting application.");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
