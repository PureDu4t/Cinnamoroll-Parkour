#if UNITY_EDITOR
using System.IO;
using AK.Wwise.Unity.WwiseAddressables;
using UnityEditor;
using UnityEngine;

/// <summary>Refresh imported Event lists after adding an Event to a generated bank.</summary>
[InitializeOnLoad]
public static class TtsSoundBankRefresh
{
    private const string RefreshKey = "TtsSoundBankRefresh.XPulse.v1";

    static TtsSoundBankRefresh()
    {
        EditorApplication.delayCall += RefreshOnce;
    }

    private static void RefreshOnce()
    {
        if (SessionState.GetBool(RefreshKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(RefreshKey, true);
        RefreshBanks();
    }

    [MenuItem("Tools/TTS/Refresh Wwise SoundBank Event Lists")]
    public static void RefreshBanks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        AkAddressablesEditorUtilities.ClearSoundbankInfo();
        foreach (string platform in new[] { "Mac", "Windows" })
        {
            string path = "Assets/WwiseData/Bank/" + platform + "/Main.bnk";
            if (!File.Exists(path)) continue;
            var before = AssetDatabase.LoadAssetAtPath<WwiseSoundBankAsset>(path);
            bool hadEvent = before != null && before.eventNames.Contains("Play_XPulse");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var after = AssetDatabase.LoadAssetAtPath<WwiseSoundBankAsset>(path);
            bool hasEvent = after != null && after.eventNames.Contains("Play_XPulse");
            Debug.Log("TTS SoundBank " + platform + ": Play_XPulse before=" + hadEvent + ", after=" + hasEvent);
        }
    }
}
#endif
