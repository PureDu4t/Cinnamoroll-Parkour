# 像素美术配置

## 设计方向
柔和可爱的像素风；奶油白、粉蓝、淡紫统一 UI 和角色。
新版故事：玉桂狗从 Cinnamon Café 出发，越过森林到达云端，最终把肉桂卷带给朋友。

## 配置位置
在 Unity Project 选中 `Assets/Resources/PixelArt/PixelArtSettings.asset`。
- Enabled：整套运行时美术开关（进入 Play 前设置）。
- Replace Character：使用新像素角色。
- Show Level Hud：显示关卡名称、章节和操作提示。
- Cafe：Level 1–5 的背景。
- Forest：Level 6–10 的背景。
- Sky：Level 11–15、Tutorial 和菜单背景。
- Tiles：4×4 地图素材图集；Character：角色。
- Ink / Accent / Panel / Border：UI 配色。

## 已接入范围
Speech Setup、Main Menu、Level Select、Tutorial、Level 1–15、Credits 和每关加载页。
游戏运行时自动添加 Pixel Art Presentation，按原 Tilemap 的 CellType 生成视觉层。原 Tilemap 继续提供格子数据、碰撞及声音定位；动态改变的格子会更新视觉层。
原素材保留，编辑模式仍显示原地图，Play 模式显示新美术。

## 素材
位于 `Assets/Resources/PixelArt/`：PixelSky.png、PixelCafe.png、PixelForest.png、PixelTiles.png、PixelPlayer.png。
图片由内置 image_gen 生成，完整提示词见 prompts.md。导入使用 Point 过滤，避免模糊像素边缘。

## 检查记录
已实际运行 Speech Setup → Main Menu → Level Select → Level 1，检查菜单排版、加载页、角色比例和地图对应关系；日志收到 LoadingFinished 后播放 Play_StartVo。
所有关卡通过统一的场景名称规则接入；尚未逐关完整通关或检查 WebGL 构建。

## 格子规则更新
- Death（原尖刺）：隐藏图像，死亡触发保留。
- AirWall（Invisible Wall）：可见的装饰屏障，保留原碰撞和 Broadcast 忽略规则；咖啡馆是垂下的桌布，森林是藤蔓，云端是云纱帘，使用独立的 Invisible Wall Tiles 三格图集。
- Level 1–5：Cafe Terrain（左墙、右路）；Level 6–10：Forest Terrain。
- Lore：提示书保持原图，只替换下方道路。
- Level 11–15：保留云端地图素材。

## 咖啡馆第二版与云端更新
- Cafe 改为 PixelCafeV2 平面奶油色墙面，减少背景家具与游戏家具之间的重复。
- Cafe Architecture 改为 PixelCafeFurnitureV2：柜台、储物柜、肉桂卷展示柜、杯碟柜的四种显示变化；下排包含木质路沿和垂挂桌布。
- Cafe Props 使用同色系的门、机关、炮台、电池与箭头；Lore 仍保留原书本图像。
- 所有家具变化只改变 Pixel Visual Tilemap；原 Tilemap、CellType、GridDatabase、碰撞、音效和 Broadcast 判定不修改。
- Wall 变体由坐标稳定选择，不会在刷新时随机变化；AirWall 继续使用桌布，属于独立类型。
- Cloud Invisible Wall 使用 PixelCloudBarrier 云朵图像，森林藤蔓保持不变。

## 咖啡馆边界与内部家具
咖啡馆原 Tilemap 最外圈的 Wall 使用实心奶油色方格边框。背景采用无图案的墙面区域；窗户、灯和招牌独立安放在原 Tilemap 的空白区域，避开所有非 Empty 格子与边界，并在地图更新后重新检查位置。
内部连续竖向 Wall 按总高度显示一个完整家具：1 格桌子、2 格冰箱、3 格高柜；更高的连续墙适配高柜高度。邻接不同高度的列分别显示家具。仅视觉层合并，原格子类型、数据、碰撞与音效读取保持不变。动态格子变化后重新计算家具高度。
Cafe Height Furniture 图集为三列：桌子占底部 1/3、冰箱占底部 2/3、高柜占完整高度。

## 第三版：连续木板与奶油边框
Cafe 使用 PixelCafeWallV3 完整纯木板背景；Cafe Decorations 使用独立透明的窗户、吊灯、肉桂卷招牌，不再裁切带旧木板纹理的图块。装饰只放在边框内的 2×2 空白区域，检查原 Tilemap 所有格子必须为 Empty，装饰之间也不重叠。Cafe Cream Boundary 使用带漩涡纹理、像素高光和柔和斜面阴影的实心奶油块。内部桌子、冰箱、高柜不变。

## 边框第四版
Cafe Cream Boundary 已改用 PixelCaramelBoundaryV4：比背景更深的焦糖糖果色，圆润斜面、局部高光、底部和右侧深色阴影；保留像素风和实心格子。

## 背景第五版
Cafe 改用 PixelCafeConcreteV5 浅棕色混凝土墙，无木纹或木板接缝。独立透明装饰仍只放在原 Tilemap 的空白区。

## 背景第六版
Cafe 使用暖白、仅略带棕色的混凝土墙，颗粒纹理减淡。新增时钟、挂画、植物，装饰按分散目标寻找空白区域；中央与镜头可见的边框外侧也可放装饰，边框外侧使用较小尺寸。所有装饰检查其占用区域没有非 Empty 格子，与其他装饰保持间距。

## 第七版：装饰绘入背景
Cafe 改用 PixelCafeBackgroundV7，米色墙面、装饰物、涂鸦和星星全部绘入单张背景。已删除独立背景装饰图集、配置字段和自动放置 Sprite 的代码。内部游戏家具及 AirWall 仍是场景格子的显示内容，不属于背景装饰。此背景按上方与中央留白设计，下方为较空区域；不再动态移动背景图案。

## 第八版：柔化背景与图集裁切
Cafe 使用 PixelCafeSoftBackgroundV8，淡米色、少量低对比装饰、轻微柔焦；背景使用 Bilinear，前景仍为 Point。咖啡馆道路图块只取底部20%，桌布排除上方10%的相邻图集行像素，并补偿位置，避免透明区域出现细线。

## 像素卡通 UI
Ui Skin 使用 PixelCartoonUiSkin 图集：云朵角面板、蓝色立体按钮、粉色星星选中按钮、奶油云牌。UI 采用九宫格绘制以保留角部图案；按钮有鼠标悬停与按下反馈。菜单、选关、Credits、关卡 HUD、语音设置和加载页共用皮肤；加载条为蓝粉交替的糖果分段。语音设置的中文字体与键盘流程保持原实现。

## HUD 修正与字体
文本容器统一使用矩形云朵角面板，不再九宫格拉伸云形剪影，避免长条状边缘和线。关卡牌、操作提示栏增加内部留白，文字使用自动缩小和区域裁切。UI 主字体改为 Google Fonts 的 ZCOOL KuaiLe（站酷快乐体），Include Font Data 开启，Noto Sans CJK SC 用于缺失字符回退；许可证保存在 Assets/Resources/Fonts/ZCOOL-KuaiLe-LICENSE.txt。代码编译通过，尚未在 Play 模式检查当前布局。

## 2026-10-25：前景图集边缘清理
PixelArtAtlasImporter 在编辑器导入时统一处理当前使用的十组透明前景图集：清除切片边界串入的相邻物件像素及透明区域的细线、孤立碎片，并保留主要轮廓。固定使用原始尺寸、Point、无 Mip Maps、无压缩，避免平台导入缩放改变切片位置。PixelArtDirector 与 PixelArtUi 的切片边界统一取整数像素；运行时不进行像素清理。原 PNG、关卡格子类型、碰撞和音效判定不变。
已导出并逐组检查 Unity 实际导入后的十组前景纹理；覆盖咖啡馆家具、道路、森林地形、云端平台、藤蔓、云朵、角色、道具及 UI 图集。未逐关运行，已有 Build 需要重新构建才包含修正。
