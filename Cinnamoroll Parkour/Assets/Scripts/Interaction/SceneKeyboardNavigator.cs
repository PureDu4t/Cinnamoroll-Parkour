using System;
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

    private bool isLoading;
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
        if (GameSpeechSettings.BlocksGameInput) return;
        if (isLoading)
        {
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

        isLoading = true;
        SceneManager.LoadScene(command.sceneName);
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
