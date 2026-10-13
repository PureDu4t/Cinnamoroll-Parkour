# TTS 语音触发

设计师：选择原来挂 Play_Lore 的物体，在 TTS Text 填中文或英文文本，选择语言和语速，保存场景。沿用原来的启动、E 键、碰撞等触发方式。Q 停止，R 重播最后一段文本。

TTS Text 为当前物体增加语音，不匹配 Event 名称。触发脚本查找当前物体上的启用 TtsText，读取对应语言文本与语速，同时继续播放原来配置的 Wwise Event。空文本只跳过 TTS，不影响 Wwise 音效。LoreInteractable 每次交互只播放一段 TTS，并播放该物体上 Trigger On 为 Nothing 的 AkEvent。没有 AkEvent 时也可单独使用 TTS。

GameTtsManager 在当前场景共用 Sherpa 引擎及居中的 Unity AudioSource，按文本、模型、语速缓存音频。生成和切换模型串行执行，等待原生任务完成后才释放引擎。

## 玩家选择语言和语速

游戏首先加载 Speech Setup 场景，显示 Loading 和加载条，优先生成并播放短句 Loading，然后预生成中英文设置提示、语言名称和当前语速样例。进度按已准备好的语音条数更新；全部准备好后才允许选择语言，并朗读「选择你的语言 / Choose your language」。A/D、左右键切换语言，1 选择中文，2 选择英文；切换时只朗读「中文」或「English」，不朗读按键说明。按 E 确认语言后，使用对应语言提示选择语速。A/D、左右键以 0.25 倍调整语速（0.5–4 倍），自动播放参考语音，R 重听，Q 停止。再次按 E 确认语速后，朗读对应语言的「按 E 进入游戏」。最后按 E 保存并加载 Main Menu，此前不会加载主菜单。也可用鼠标按钮。按 F2 可重新调整设置，默认带入上次保存的选择。

Speech Setup 已放在构建场景列表首位。在 Unity 编辑器中体验完整启动流程，请打开 Assets/Scenes/Speech Setup.unity 后运行；直接运行其他场景不会强制显示启动设置。

默认所有 TtsText 勾选「使用玩家语言和语速」，应用玩家选择；取消勾选可保留该文本自己的语言和语速。中文选择读取「中文文本」，英文选择读取「英文文本」，不会自动翻译。英文为空的物体会跳过 TTS 并在 Console 提示。

设计师可在 Assets/Resources/TtsSettingsContent.asset 的 Inspector 修改中英文操作提示及样例文本。设置界面自动创建，无需挂组件或修改场景。设置期间阻止游戏按键；开场 TTS 等确认后才播放。首次切换模型或首次生成样例仍需要等待，快速调整会停止旧样例，以最后一次选择为准。

场景加载时自动预生成场景内 TtsText 当前语言与语速的语音，包括暂未激活的物体。预生成只缓存语音，不播放、不锁定玩家、不触发插入音效。玩家触发朗读时优先处理该文本；后台当前正在合成的一句完成后，暂停剩余预生成，朗读结束后继续。缓存随场景中的管理器销毁而释放。无需额外填写或挂组件。

原生 AkEvent 经 IAkEventPlaybackOverride 调用项目组件，使用原有 Trigger On 配置。适配点在 Assets/Wwise/MonoBehaviour/Runtime/AkEvent.cs 的 HandleEvent 和 Stop。更新 Wwise Integration 后应保留或重新应用这两个适配点，以及 IAkEventPlaybackOverride.cs。适配器不会截断 Wwise 播放或停止操作。原生 AkEvent 的回调只来自 Wwise，不由 TTS 伪造；Lore 交互跟踪播放结束；播报节奏和关卡确认仍沿用原来的 Wwise 流程，不等待 TTS。

现有 Play_Lore 场景物体已附加 TtsText；Level 1 开场文案从 TtsLevelIntro 迁移，原示例组件已从场景移除。SoundBank 仍由原启动脚本加载，用于其他 Wwise 音效。

WwiseStartEvent 的 Play Start Event 只控制 Wwise 开场 Event。取消勾选后仍会加载 SoundBank，并播放同物体上的 TtsText；预生成只负责准备缓存，开场播放仍由 WwiseStartEvent 触发。

Lore 物体的按键输入由 PlayerInteraction 按所在交互区域处理；同物体上的旧全局按键脚本不重复触发，防止远处 LT_Win 抢播。

如果 Wwise Event 仍包含旧旁白录音，它会与 TTS 一起播放。设计师应在 Wwise 中移除不再需要的旁白播放动作，保留音效动作；代码不会按名称判断哪些声音应该删除。

## 在旁白中插入示范音效

在同一物体的 Tts Text → 音效插入列表中添加条目，填写名称并选择 Wwise Event。例如名称填写「道路」，文本中就使用 `[sfx:道路]`。中文和英文可以共用同一套音效名称。

```text
其中，[sfx:道路]代表道路，[sfx:墙壁]代表墙壁，[sfx:终点]代表终点。
[pause:0.5]按S进行从左到右的全局播报，了解关卡的样貌并找到终点吧！
```

- `[sfx:名称]`：暂停朗读，播放对应 Event，收到 Wwise 的 EndOfEvent 后继续。
- `[pause:0.5]`：停顿半秒。秒数使用英文小数点，不受游戏时间缩放影响。
- 音效 Event 所在 SoundBank 必须已加载。建议使用只播放该示范音效的 Event。
- 「最长等待秒数」默认 30 秒；循环音效或未返回结束回调的音效到时停止，继续朗读。
- Q 中断整个序列并停止当前插入音效；R 从头重播。其他 Wwise 音乐、音效继续按原流程播放。
- 已预生成的语音直接播放；未完成预生成时，第一句生成好就开始播放，后续句子继续生成。如果下一句还未生成完成，仍会等待。
- 不使用标记的原有文本仍然正常朗读。缺少音效绑定或无效停顿会在 Console 提示并跳过该标记。

Level 1 的 LT_1 的 XPulse：文本使用 `[sfx:XPulse]`，当前绑定独立示范 Event `Play_XPulse`。新增或修改 Wwise Event 后，先生成 SoundBank；若 Unity 的事件列表未更新，点击 `Tools → TTS → Refresh Wwise SoundBank Event Lists`。对应 SoundBank 需要由场景的 AkBank 组件加载。插入音效直接取得实际播放 ID；按 Q 或等待超时后，不会留下延迟触发的音效。

## 每关加载页与 Wwise 接口

Tutorial、Level 1–15 自动显示 TTS 加载页，并生成当前语言和语速的语音。生成成功、目标 Scene 预加载完成后，Unity 触发 Transition Event。Wwise 在 Next Bar 开始播放 stinger，到达 Custom Cue 后，Unity 激活目标 Scene 或开放本关操作。不会用固定 2.67 秒代替音乐回调。失败时按 R 重试。

配置位置：Assets/Resources/TtsLoadingAudioSettings.asset 的 Inspector。

- Sound Bank：包含两个 Event 的 SoundBank，当前为 Main。
- Loading Event：Play_Loading，播放 LoadingMusic。
- Transition Stinger Event：Play_Loading_Transition，设置 Loading_State → Finish。
- Loading Rtpc Name：Loading_RTPC，全局值 0–100，与语音生成进度一致。
- Finished Cue Name：LoadingFinished，区分大小写，必须是 Music Segment 中独立的 Custom Cue。
- Loading State Group / Loading Initial State：Loading_State / Loop；开始播放前重置状态。
- Transition Timeout：默认 30 秒。缺少 Cue 时显示错误供重试，不自动按秒数跳过。
- Tail Timeout：默认 15 秒。Cue 后音乐对象跨 Scene 保留，Event 自然结束时清理；超时才停止残留播放。
- BPM / Beats Per Bar：仅作参考，实际小节和 Cue 位置由 Wwise 决定。
- Startup Target Scene：启动设置结束后按 E 进入的 Scene，默认 Main Menu。
- Scene Routes：Source Scene 对应当前 Scene；Target Scene 留空或等于当前 Scene 时，直接进入当前关卡，不重新加载。不同目标必须加入构建列表。

启动设置在最后按 E 后预加载目标 Scene，准备好后才触发 Transition Event。Unity 订阅的是原始 Loading Event 的 AK_MusicSyncUserCue，Transition Event 只负责更改 State。收到 LoadingFinished 后在主线程激活 Scene。画面仍受场景初始化和渲染帧影响，音乐的卡点由 Wwise 完成。

不同目标 Scene 的预加载使用 LoadSceneAsync + allowSceneActivation=false，激活前其 Awake/Start 尚未运行，因此该 Scene 自身的关卡 TTS 加载流程仍会在激活后执行。通常关卡应使用留空目标或当前 Scene；Startup Target Scene 指向 Main Menu。

共享 SoundBank 清理：Loading 音乐结束时只释放自己的 Addressables bank 引用，使用 ignoreRefCount=false。不能直接调用 Bank.Unload() 强制卸载 Main，否则可能让下一关找不到 Transition Event 和音频。若场景组件提前卸载共享 bank，过渡前会重新加载并恢复 Loading 播放。

关卡开场语音：Tutorial、Level 1–15 的 Play_StartVo 自动 Start 触发由 TtsSceneLoading 暂存，只有收到 LoadingFinished 且加载成功后才调用原 AkEvent。加载失败时不播放，R 重试成功后播放一次。Level Select / Credits 没有关卡加载页，仍使用原有 Start 行为。

设置页 UI：保留背景图，采用圆角语言卡片、三步进度指示、语速滑条及 Replay/Stop 按钮。所有文字使用 Resources/Fonts/SpeechUiFont.otf（Noto Sans CJK SC，Include Font Data 已启用），字体及 OFL 授权随项目打包，不再调用 PingFang 等系统字体。键盘选择和 E/R/Q 的操作沿用原流程。
