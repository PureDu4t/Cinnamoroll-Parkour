using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.VisualScripting.YamlDotNet.RepresentationModel;

/// <summary>Refreshes the runtime menu catalog when designers save, play or build.</summary>
[InitializeOnLoad]
public class SharedMenuSpeechExporter : IPreprocessBuildWithReport
{
    private const string Output = "Assets/Resources/SharedMenuSpeech.json";
    public int callbackOrder => 0;

    static SharedMenuSpeechExporter()
    {
        EditorApplication.delayCall += RefreshWhenIdle;
        EditorSceneManager.sceneSaved += _ => RefreshWhenIdle();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Export();
        };
    }

    private static void RefreshWhenIdle()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) Export();
    }

    public void OnPreprocessBuild(BuildReport report) => Export();

    private static SharedMenuSpeech.Entry ReadText(SerializedObject data)
    {
        return new SharedMenuSpeech.Entry
        {
            chineseText = data.FindProperty("chineseText").stringValue,
            englishText = data.FindProperty("englishText").stringValue,
            usePlayerSpeechSettings = data.FindProperty("usePlayerSpeechSettings").boolValue,
            language = data.FindProperty("language").enumValueIndex,
            speechSpeed = data.FindProperty("speechSpeed").floatValue
        };
    }

    private static YamlNode Node(YamlNode mapping, string key)
        => mapping is YamlMappingNode map && map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
    private static string Scalar(YamlNode mapping, string key, string fallback = "")
        => (Node(mapping, key) as YamlScalarNode)?.Value ?? fallback;

    private static void AddConfig(LevelSelectConfig config, SharedMenuSpeech.Catalog catalog)
    {
        if (config == null) return;
        for (int i = 0; i < config.LevelCount; i++)
        {
            var item = config.GetLevel(i);
            if (item != null) catalog.entries.Add(new SharedMenuSpeech.Entry
            { chineseText = item.chineseTtsText, englishText = item.englishTtsText });
        }
    }

    private static void ReadSavedScene(string path, string name, SharedMenuSpeech.Catalog catalog)
    {
        // Read the saved scene without instantiating its Wwise/ExecuteAlways components.
        string yaml = Regex.Replace(File.ReadAllText(path), @"^%[^\r\n]*\r?\n", "", RegexOptions.Multiline);
        yaml = Regex.Replace(yaml, @"^--- !u!\d+ &\d+[^\r\n]*", "---", RegexOptions.Multiline);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        string textGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Interaction/TtsText.cs");
        string navGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Interaction/SceneKeyboardNavigator.cs");
        string selectGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/LevelSelect/LevelSelectController.cs");
        foreach (var document in stream.Documents)
        {
            var data = Node(document.RootNode, "MonoBehaviour");
            string script = Scalar(Node(data, "m_Script"), "guid");
            if (script == textGuid)
            {
                float.TryParse(Scalar(data, "speechSpeed", "2"), NumberStyles.Float, CultureInfo.InvariantCulture, out float speed);
                int.TryParse(Scalar(data, "language", "0"), out int language);
                catalog.entries.Add(new SharedMenuSpeech.Entry
                {
                    chineseText = Scalar(data, "chineseText"), englishText = Scalar(data, "englishText"),
                    usePlayerSpeechSettings = Scalar(data, "usePlayerSpeechSettings", "1") != "0",
                    language = language, speechSpeed = speed
                });
            }
            else if (script == navGuid && name == "Main Menu" && Node(data, "mainMenuSpeech") is YamlSequenceNode items)
            {
                foreach (var item in items.Children) catalog.entries.Add(new SharedMenuSpeech.Entry
                { chineseText = Scalar(item, "chineseText"), englishText = Scalar(item, "englishText") });
            }
            else if (script == selectGuid)
            {
                string configGuid = Scalar(Node(data, "config"), "guid");
                AddConfig(AssetDatabase.LoadAssetAtPath<LevelSelectConfig>(AssetDatabase.GUIDToAssetPath(configGuid)), catalog);
            }
        }
    }

    [MenuItem("Tools/TTS/Refresh Shared Menu Speech")]
    public static void Export()
    {
        var catalog = new SharedMenuSpeech.Catalog();
        foreach (string name in new[] { "Main Menu", "Level Select" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            if (!File.Exists(path)) throw new BuildFailedException("缺少菜单场景：" + path);
            Scene scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                ReadSavedScene(path, name, catalog);
                continue;
            }
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var text in root.GetComponentsInChildren<TtsText>(true))
                        catalog.entries.Add(ReadText(new SerializedObject(text)));
                    foreach (var navigator in root.GetComponentsInChildren<SceneKeyboardNavigator>(true))
                    {
                        if (name != "Main Menu") continue;
                        var items = new SerializedObject(navigator).FindProperty("mainMenuSpeech");
                        for (int i = 0; i < items.arraySize; i++)
                        {
                            var item = items.GetArrayElementAtIndex(i);
                            catalog.entries.Add(new SharedMenuSpeech.Entry
                            {
                                chineseText = item.FindPropertyRelative("chineseText").stringValue,
                                englishText = item.FindPropertyRelative("englishText").stringValue
                            });
                        }
                    }
                    foreach (var controller in root.GetComponentsInChildren<LevelSelectController>(true))
                    {
                        var config = new SerializedObject(controller).FindProperty("config").objectReferenceValue as LevelSelectConfig;
                        AddConfig(config, catalog);
                    }
                }
            }
        }
        string json = JsonUtility.ToJson(catalog, true);
        if (File.Exists(Output) && File.ReadAllText(Output) == json) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, json);
        AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceSynchronousImport);
    }
}
