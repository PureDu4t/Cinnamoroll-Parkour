using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AkGameObj))]
public class LevelSelectController : MonoBehaviour
{
    [Header("Level Configuration")]
    [Tooltip("包含15关Scene和Wwise Switch的配置文件。")]
    [SerializeField] private LevelSelectConfig config;

    [Tooltip("进入选关页面时默认选中的关卡。1代表第一关。")]
    [SerializeField] private int startingLevel = 1;

    [Header("Scene Navigation")]
    [Tooltip("按Esc时返回的主界面Scene名称。")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Wwise")]
    [Tooltip("统一播放关卡播报的Wwise Event。")]
    [SerializeField] private AK.Wwise.Event playLevelSelectionEvent;

    [Tooltip("按E确认关卡时播放的Wwise Event，例如玉桂狗叫声。")]
    [SerializeField] private AK.Wwise.Event confirmSelectionEvent;

    [Tooltip("确认Event缺失或播放失败时，等待多久再切换Scene。")]
    [SerializeField] private float confirmLoadDelay = 0.35f;

    [Tooltip("确认Event如果一直没有结束，最多等待多久再切换Scene。")]
    [SerializeField] private float maximumConfirmWaitTime = 1.5f;

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
    private bool confirmEventFinished;
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

        selectedIndex = WrapIndex(startingLevel - 1);

        // 暂时不在Start中播放，避免SoundBank尚未加载完成。
        Debug.Log("Currently selected level: " + (selectedIndex + 1));
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

        Debug.Log("Currently selected level: " + (selectedIndex + 1));

        PlaySelectionAnnouncement();
    }

    private void ChangeSelectionByHorizontalKey(int amount)
    {
        if (!hasPlayedInitialSelectionAnnouncement)
        {
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

        TtsText.TryPlay(gameObject);

        if (level.announcementSwitch == null)
        {
            Debug.LogError(
                "Level " + (selectedIndex + 1)
                + " has no Wwise Switch."
            );

            return;
        }

        if (playLevelSelectionEvent == null)
        {
            Debug.LogError(
                "Play Level Selection Event is not configured."
            );

            return;
        }

        // 先告诉Wwise当前选择的是哪一关。
        level.announcementSwitch.SetValue(gameObject);

        // 再播放统一Event。
        // Switch Container会根据刚才设置的Switch决定播放哪条语音。
        playLevelSelectionEvent.Post(gameObject);
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

        StartCoroutine(LoadSelectedLevelAfterConfirm(level.sceneName));
    }

    private IEnumerator LoadSelectedLevelAfterConfirm(string sceneName)
    {
        isLoadingSelectedLevel = true;
        confirmEventFinished = false;

        TtsText.TryPlay(gameObject);
        if (confirmSelectionEvent != null)
        {
            confirmEventFinished = false;

            uint playingId = confirmSelectionEvent.Post(
                gameObject,
                (uint)AkCallbackType.AK_EndOfEvent,
                OnConfirmEventCallback,
                this
            );

            if (playingId == AkSoundEngine.AK_INVALID_PLAYING_ID)
            {
                Debug.LogError(
                    "Wwise could not play the level confirm Event."
                );

                if (confirmLoadDelay > 0f)
                {
                    yield return new WaitForSeconds(confirmLoadDelay);
                }
            }
            else
            {
                float waitStartedAt = Time.time;
                while (!confirmEventFinished
                    && Time.time - waitStartedAt < maximumConfirmWaitTime)
                {
                    yield return null;
                }
            }
        }

        if (confirmSelectionEvent == null && confirmLoadDelay > 0f)
        {
            yield return new WaitForSeconds(confirmLoadDelay);
        }

        Debug.Log("Loading Scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    private void OnConfirmEventCallback(
        object cookie,
        AkCallbackType callbackType,
        AkCallbackInfo callbackInfo
    )
    {
        if (callbackType == AkCallbackType.AK_EndOfEvent)
        {
            confirmEventFinished = true;
        }
    }

    private void ReturnToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("Main Menu Scene Name is empty.");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
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
