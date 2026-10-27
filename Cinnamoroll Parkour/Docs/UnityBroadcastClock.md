# GridBroadcast Unity 时钟配置

在关卡的 GridBroadcast 对象上找到 Grid Broadcast Controller。

- **Beats Per Minute**：Unity 时钟 BPM，现有 1–15 关已设为 90。不会把 BPM 发给 Wwise。
- **Beats Per Step**：每列持续几拍，默认 1。设为 1 时按键等待下一拍开始。
- **Clock Start Mode**：默认 Scene Music Entry。Unity 监听持久 `Start_Game` Event：收到 `LoadingFinished` 后等待下一段 Music Segment 的 Entry Cue，再以该时刻启动 clock。On Enable 立即启动；Manual 需要主动调用 RestartClock。
- **Play Tick**：可选 Wwise Event。每个 clock 节拍先提交一次 Tick，再在同一帧提交该拍的格子音高 Events；可用它替换原本单独的鼓声。
- **Row Events / Default Broadcast Event / Cell Type Events**：原有格子音效接口。

Clock 不会额外 Post 音乐 Event，也不会改变 State。`LoadingFinished` 位于转场段中，所以转场段之后的下一个 Music Segment Entry 才会启动 clock。若在 Editor 里直接运行关卡、没有经过加载流程，可暂时选 On Enable 或 Manual。

实现使用 AudioSettings.dspTime 和第 n 拍的绝对目标时间，不累计 Time.deltaTime，也不读取鼓音源播放位置。停止、重新按键不会重置 clock。修改运行中的 BPM 后调用 RestartClock 使配置生效；该操作会取消当前播报并从当前时刻重新计拍。

长卡顿超过一列间隔时，保留尚未播报的列，等待下一个将来的拍点继续，不集中补播。最后一列保留一个完整周期再结束播报。

Wwise 回调由 `AkSoundEngineController.LateUpdate` 分发，通知可能比实际 Entry 晚一帧左右；Unity clock 因此按回调收到时的 DSP 时间启动。这比“输入解锁”更贴近真实音乐起点，但不是采样级音频调度。

验证：Tests/Audio/UnityBeatClockTests.cs 使用真实 BroadcastBeatClock 类，检查下一拍起播、精确拍点、半拍、15/30/60/144 FPS 的 30 分钟时间模拟和长卡顿后的时钟相位。模拟不代表 Windows 实机音频测试。
