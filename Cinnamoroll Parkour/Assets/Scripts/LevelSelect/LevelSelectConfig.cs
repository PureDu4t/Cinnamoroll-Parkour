using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelSelectConfig",
    menuName = "Cinnamoroll Parkour/Level Select Config"
)]
public class LevelSelectConfig : ScriptableObject
{
    [Serializable]
    public class LevelEntry
    {
        [Tooltip("Unity中对应关卡Scene的名字。")]
        public string sceneName;

        [Tooltip("选中该关卡时设置的Wwise Switch。")]
        public AK.Wwise.Switch announcementSwitch;
    }

    [Tooltip("按照需要播报的顺序填写。选关界面可以填关卡，教学界面可以填教学语音。")]
    [SerializeField] private LevelEntry[] levels =
        new LevelEntry[15];

    public int LevelCount
    {
        get { return levels.Length; }
    }

    public LevelEntry GetLevel(int index)
    {
        if (index < 0 || index >= levels.Length)
        {
            Debug.LogError(
                "关卡编号超出LevelSelectConfig范围：" + index
            );

            return null;
        }

        return levels[index];
    }
}
