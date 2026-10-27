using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectController : MonoBehaviour
{
    [Header("Level Configuration")]
    [Tooltip("包含关卡 Scene 和中英文 TTS 文本的配置文件。")]
    [SerializeField] private LevelSelectConfig config;

    [Tooltip("进入选关页面时默认选中的关卡。1代表第一关。")]
    [SerializeField] private int startingLevel = 1;

    [Header("Scene Navigation")]
    [Tooltip("按Esc时返回的主界面Scene名称。")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Keys")]
    [SerializeField] private KeyCode previousLevelKey =
        KeyCode.LeftArrow;

    [SerializeField] private KeyCode nextLevelKey =
        KeyCode.RightArrow;

    [SerializeField] private KeyCode moveUpFiveKey =
        KeyCode.UpArrow;

    [SerializeField] private KeyCode moveDownFiveKey =
        KeyCode.DownArrow;

    [SerializeField] private KeyCode confirmKey = KeyCode.E;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    // 数组编号从0开始，所以0代表第一关，14代表第十五关。
    private int selectedIndex;
    private bool isLoadingSelectedLevel;
    private bool hasPlayedInitialSelectionAnnouncement;
    public int SelectedLevel => selectedIndex + 1;
    public int LevelCount => config != null ? config.LevelCount : 0;
    public void SelectFromUI(int index)
    {
        if (!enabled || isLoadingSelectedLevel || GameSpeechSettings.BlocksGameInput) return;
        hasPlayedInitialSelectionAnnouncement = true;
        ChangeSelection(index - selectedIndex);
    }
    public void ConfirmFromUI()
    {
        if (enabled && !isLoadingSelectedLevel && !GameSpeechSettings.BlocksGameInput) LoadSelectedLevel();
    }
    public void BackFromUI()
    {
        if (!isLoadingSelectedLevel && !GameSpeechSettings.BlocksGameInput) ReturnToMainMenu();
    }

    private void Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        StartCoroutine(RestoreSelectionMusicState());
        selectedIndex = WrapIndex(startingLevel - 1);

        // 进入页面的说明由 LT_Start 播放，选关播报在切换时触发。
        Debug.Log("Currently selected level: " + (selectedIndex + 1));
    }

    private IEnumerator RestoreSelectionMusicState()
    {
        // The loading flow requests the destination State before its Cue.
        // Do not override Loading while entering this scene from Main Menu.
        while (TtsSceneLoading.IsLoading(gameObject.scene) || GameSpeechSettings.IsChoosing)
            yield return null;
        var settings = Resources.Load<TtsLoadingAudioSettings>("TtsLoadingAudioSettings");
        if (settings == null)
        {
            Debug.LogWarning("选关页无法设置音乐 State：缺少 TtsLoadingAudioSettings。", this);
            yield break;
        }
        var route = settings.sceneRoutes.Find(item => item != null
            && item.sourceScene == gameObject.scene.name);
        if (route == null || string.IsNullOrWhiteSpace(route.targetState)
            || string.IsNullOrWhiteSpace(settings.loadingStateGroup))
        {
            Debug.LogWarning("请在 TtsLoadingAudioSettings 的 Scene Routes 配置选关页 Target State。", this);
            yield break;
        }
        double deadline = Time.realtimeSinceStartupAsDouble + settings.initializationTimeout;
        while (!AkSoundEngine.IsInitialized() && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        if (!AkSoundEngine.IsInitialized())
        {
            Debug.LogError("选关页设置 State 失败：Wwise 初始化超时。", this);
            yield break;
        }
        var result = AkSoundEngine.SetState(settings.loadingStateGroup, route.targetState);
        if (result != AKRESULT.AK_Success)
            Debug.LogError("选关页设置 State 失败：" + settings.loadingStateGroup + "/" + route.targetState, this);
        else
            Debug.Log("进入选关页，已设置 State：" + settings.loadingStateGroup + "/" + route.targetState, this);
    }

    private void Update()
    {
        if (GameSpeechSettings.BlocksGameInput) return;
        if (isLoadingSelectedLevel)
        {
            return;
        }

        if (Input.GetKeyDown(previousLevelKey))
        {
            ChangeSelectionByHorizontalKey(-1);
        }
        else if (Input.GetKeyDown(nextLevelKey))
        {
            ChangeSelectionByHorizontalKey(1);
        }
        else if (Input.GetKeyDown(moveUpFiveKey))
        {
            // 按上箭头：第1关跳到第6关。
            ChangeSelection(5);
        }
        else if (Input.GetKeyDown(moveDownFiveKey))
        {
            // 按下箭头：第1关向前退5，循环到第11关。
            ChangeSelection(-5);
        }
        else if (Input.GetKeyDown(confirmKey))
        {
            LoadSelectedLevel();
        }
        else if (Input.GetKeyDown(cancelKey))
        {
            ReturnToMainMenu();
        }
    }

    private void ChangeSelection(int amount)
    {
        selectedIndex = WrapIndex(selectedIndex + amount);
        UiActionSfx.Play();

        Debug.Log("Currently selected level: " + (selectedIndex + 1));

        PlaySelectionAnnouncement();
    }

    private void ChangeSelectionByHorizontalKey(int amount)
    {
        if (!hasPlayedInitialSelectionAnnouncement)
        {
            UiActionSfx.Play();
            Debug.Log(
                "Currently selected level: " + (selectedIndex + 1)
            );
            hasPlayedInitialSelectionAnnouncement = true;
            PlaySelectionAnnouncement();
            return;
        }

        ChangeSelection(amount);
    }

    private int WrapIndex(int index)
    {
        int levelCount = config.LevelCount;

        // 普通的%可能产生负数，所以使用这个写法保证结果
        // 永远处于0到levelCount - 1之间。
        return ((index % levelCount) + levelCount) % levelCount;
    }

    private void PlaySelectionAnnouncement()
    {
        LevelSelectConfig.LevelEntry level =
            config.GetLevel(selectedIndex);

        if (level == null)
        {
            return;
        }

        var text = GetComponent<TtsText>();
        if (text == null) text = gameObject.AddComponent<TtsText>();
        if (!text.PlayBilingual(level.chineseTtsText, level.englishTtsText))
            TtsText.TryPlay(gameObject);

        hasPlayedInitialSelectionAnnouncement = true;
    }

    private void LoadSelectedLevel()
    {
        LevelSelectConfig.LevelEntry level =
            config.GetLevel(selectedIndex);

        if (level == null || string.IsNullOrWhiteSpace(level.sceneName))
        {
            Debug.LogError(
                "Level " + (selectedIndex + 1)
                + " has no Scene name."
            );

            return;
        }

        isLoadingSelectedLevel = true;
        UiActionSfx.Play();
        Debug.Log("Loading Scene: " + level.sceneName);
        SceneManager.LoadSceneAsync(level.sceneName, LoadSceneMode.Single);
    }

    private void ReturnToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("Main Menu Scene Name is empty.");
            return;
        }

        UiActionSfx.Play();
        isLoadingSelectedLevel = true;
        SceneManager.LoadSceneAsync(mainMenuSceneName, LoadSceneMode.Single);
    }

    private bool ValidateConfiguration()
    {
        if (config == null)
        {
            Debug.LogError(
                "LevelSelectController has no LevelSelectConfig."
            );

            return false;
        }

        if (config.LevelCount <= 0)
        {
            Debug.LogError(
                "LevelSelectConfig must contain at least one entry."
            );

            return false;
        }

        return true;
    }
}
