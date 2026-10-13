# Broadcast 鼓声与格子声同步修复

## 证据
Windows 录像时长 11.869 秒，AAC 48 kHz。鼓源 MUS_Broadcast_Drums.wav 时长32秒，场景配置120 BPM、每列1拍。
代码此前在 Wwise 播放独立循环鼓轨，而每列使用 WaitForSeconds(0.5)。相对等待每次取决于下一帧，误差会累积；与平台、帧率和卡顿有关，并非 Windows 独有。
录像与原始鼓、Ground 音频的短片段相关性分析中，部分强匹配的格子起音相对鼓轨约落后35–83ms。混音和不同格子音高会干扰匹配，这只是支持调度漂移的证据，不是完整的音源分离测量。
鼓轨含切分节奏，不能要求每个鼓击与每列重合。

## 修改
GridBroadcastController 以 GetSourcePlayPosition 的真实 Wwise 音源位置为列拍时间。BroadcastAudioClock 处理32秒循环和查询抖动。每列使用绝对拍点，不再累加相对等待。
首列与鼓声在同一帧提交；先分配/注册格子声对象。最后一列保留一个完整拍长后再停止鼓。
重播前立即结束旧鼓声，避免鼓源单实例上限与旧声50ms淡出重叠。
若鼓轨未配置，则采用绝对 realtime 计时。音源未开始时等待查询，2秒无法取得时告警并回退。
Inspector 新字段 Drum Loop Length Seconds=32，只有替换不同长度的鼓轨时才需修改。原有 BPM、按键、格子、音高与声像映射保留。
无需修改 Wwise Event 或重新生成 SoundBank；Windows 玩家需要重新打包含本次脚本的版本。

## 模拟验证
Tests/Audio/BroadcastClockTests.cs 对实际 BroadcastAudioClock 类进行C#测试，30秒调度、120BPM，模拟音源毫秒位置。
15/30/60/144FPS 最大拍点触发延迟为66/33/16/6ms，没有逐拍累积漂移。
30/60FPS 插入250ms主线程卡顿时，受影响拍点延迟183/166ms；恢复后回到16ms以内，不继续累积。
32秒循环及小幅倒退查询测试通过。完整项目脚本编译通过（仅既有AkSoundEngine弃用警告）。

## 验证范围
用户确认暂时没有可连接Windows设备，采用模拟验证。测试模拟帧率与卡顿，未模拟完整Windows音频驱动、设备缓冲或不同SoundBank转换设置。
本次未取得本项目Unity Play Mode实际音频验证：当前打开的是另一个Unity项目；未操作该项目。
帧驱动的PostEvent仍可能有一帧及一个音频命令块的抖动。模拟不能证明Windows最终音频完全无偏移；需新Windows包实机录音复核。
