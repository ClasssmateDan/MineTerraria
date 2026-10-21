# 素材来源

## v0.24.0 MineTerraria 下界合金

新材料直接使用已校验Minecraft Java 1.21.1官方客户端中的`assets/minecraft/textures/item/netherite_ingot.png`，逐字节提取到`DimensionalTerraria/Textures/Minecraft/netherite_ingot.png`。保留原16×16透明图标，SHA-256为`21418E66CA43961E91FB8CFBA645A4364CA340426BCA82F073D140F202DFE28B`。已本地查看；自动素材检查与客户端归档逐字节比较，打包后另用原生ImageIO在后台线程解码。没有AI重绘或新增其他材料图片。

本版与历史版本的来源清单均收录在可提交的`docs/asset-sources/`，不依赖被Git忽略的构建输出目录。模组更名不改变既有贴图路径、物品类型与存档标识。

## v0.23.0 烟花

使用已校验的Minecraft Java 1.21.1官方客户端归档，逐字节提取`assets/minecraft/textures/item/firework_rocket.png`为`DimensionalTerraria/Textures/Minecraft/firework_rocket.png`。保留16×16尺寸、原始像素与透明背景；SHA-256为`FEA5F8FDF3A1517EDBC7006D327FE09B8567A0E4C2E76F8AEB8AEAC68EABF786`。来源网址、客户端SHA-1及归档路径见`docs/asset-sources/asset-sources-v0.23.0.json`。没有AI重绘、修改其他素材或在加载线程读取GPU。

## v0.22.0 背景定位与录制怪群

没有新增或修改图片。三维背景继续使用泰拉瑞亚当前世界所选的原版Background材质；按真实纹理尺寸和原版缩放计算世界坐标，复用当前引擎SetBackgroundOffsets及层偏移数据，不修改玩家坐标或原版屏幕位置。背景图片下沿使用PointClamp延续最底行，避免垂直重复景物。怪群完全复用原版史莱姆王、腐化史莱姆、骷髅和猩红史莱姆的纹理与动画。无AI生成、图形设备创建或加载线程GPU读取。

## v0.21.0 箱子开合与精灵白边修复

没有新增外部素材，原PNG全部保留。箱子继续读取原Minecraft Java 1.21.1的64×64 `chest_normal`，盖子14×5、箱身14×10和锁扣2×4直接2倍采样，不再经16像素中间图变形；`ChestArt.Atlas`生成36×114的三状态原生图集，对应宝箱绘制的0／38／76像素偏移，每状态均包含2×2格及安全帧间距。半开／打开的箱口是深棕像素，关闭状态与物品三面材质使用同一原UV来源。

`SpriteEdges.Clean`在CPU加载阶段处理Steve、羊、村民、铁傀儡及Steve护甲叠层：丢弃Alpha低于240的残留像素，在原尺寸轮廓内收缩1至2源像素，透明RGB归零，保留内部RGB并使用不透明像素。这样匹配原生RawImgReader直接上传RGB及预乘Alpha混合的行为，避免近透明白色RGB在画面中被加亮。铁傀儡的内部白色金属与眼白不按颜色删除。叠层共用Steve本体遮罩，裁帧坐标及动作不变；铁傀儡缩略图同样清理。没有在加载线程访问GPU或重绘原图。

已查看`.dev/preview-v0.21.0.png`，包含箱子三个状态和三种主体清理前后在深色底上的像素预览；实际包后台检查验证四种本体及三层护甲的透明RGB、二值Alpha、内部颜色保留、主体保留比例和护甲轮廓约束。原来源清单继续使用此前版本记录。

## v0.20.0 Minecraft箱子、村民蛋和蹲下

箱子使用同一份已校验Minecraft Java 1.21.1客户端归档内的`assets/minecraft/textures/entity/chest/normal.png`，逐字节提取为`Textures/Minecraft/chest_normal.png`，64×64原版UV图。原始来源、路径及SHA-256见`docs/asset-sources/asset-sources-v0.20.0.json`。`Core/ChestArt.cs`按盖、底座、正面／侧面和锁扣UV采样：前景精灵32×34，切成带2像素帧间距的36×38四格图集；物品图标沿用24×24三面投影。实际包后台线程直接读原RGBA并生成，不请求图形设备或GPU读回。

村民蛋复用既有16×24代码像素蛋形，棕色壳`0x563C33`、皮色斑点`0xBD8B72`、深棕描边`0x2E211E`。没有重画村民，使用已有村民精灵和动画。蹲下没有新增人物图片：最终绘制层按屏幕行对既有Steve本体、原生人物、护甲、手持物品及染色数据做姿态变换，上身前倾、头部平移、腿脚固定，旋转中的物品整体随锚点偏移，保留原素材及投影护甲的全部动作。

已查看`.dev/construction-preview.png`中的箱子正面、立体物品图标、村民蛋，以及穿甲Steve的站立／蹲下对比。自动检查核对原MC文件字节、三个模型面的不透明UV、箱子四格帧间距、蛋形轮廓、原生DrawDataCache中的头／脚位置、染色和手持弹幕顺序；游戏内实际动作仍需用户验收。没有使用AI重绘素材。

箱子容器接口参照tModLoader官方[ExampleChest](https://github.com/tModLoader/tModLoader/blob/stable/ExampleMod/Content/Tiles/Furniture/ExampleChest.cs)，功能使用本地当前引擎的原生Chest／WorldFile／WorldIO实现。

## v0.19.1 Steve护甲与特效修正

Steve原始PNG保持逐字节不变。`scripts/PrepareProjectionAssets.ps1`新增三份1254×1254的`Textures/Equipment/SteveArmor_Head/Body/Legs.png`，沿用既有4×2的8个动作位置。根据原Steve各姿势的头部区域、衣服／裤子颜色和手臂姿势轴，把原版下界合金UV图采样为独立装备遮罩；透明度不超过原Steve同坐标像素，保留脸、眼睛、露出的手和原本体轮廓，按实际可见槽位分别叠加，支持染色。MC原版手臂UV中的透明部分自然保留手臂裸露区域。

普通角色头盔在原生帧的头部边界内使用方形下界合金侧面和正面UV，去除铜盔透明轮廓的尖角；普通胸甲、护腿和物品图标的像素未改变。已查看`.dev/steve-armor-preview.png`全部8个动作以及`.dev/projection-armor-preview.png`；自动检查覆盖三层与Steve原图尺寸／坐标／Alpha对应、每层各8帧非空及实际包的后台解码。

飞龙继续使用泰拉瑞亚原版`ProjectileID.DD2SquireSonicBoom`（684）纹理；依据本地引擎`Main.DrawProj`对该灰阶纹理的着色机制，三维增加橙黄色顶点色与橙红加亮层。天顶剑仍使用原版物品纹理和FinalFractal拖尾颜色，只调整飞行路径、范围及近镜头淡出。没有新外部素材、AI重绘或GPU读回。派生文件SHA-256及基底Steve哈希记录在`docs/asset-sources/asset-sources-v0.19.1.json`。

## v0.19.0 基岩与高维投影装备

从既有、SHA-1校验为`30c73b1c5da787909b2f73340419fdf13b9def88`的官方Minecraft Java 1.21.1客户端逐字节提取8张PNG：16×16的`bedrock`、`netherite_pickaxe`、`netherite_helmet`、`netherite_chestplate`、`netherite_leggings`、`netherite_boots`，以及64×32的`models/armor/netherite_layer_1`和`netherite_layer_2`。全部保存在`Textures/Minecraft/`，出处、归档路径和SHA-256见`docs/asset-sources/asset-sources-v0.19.0.json`。基岩背包图标沿用CPU原材质闭合立方体投影。

物品图标从原16像素图最近邻放大为32像素；穿戴材质由`scripts/PrepareProjectionAssets.ps1`以CPU适配到泰拉瑞亚的原生装备动画布局。使用用户本机Terraria原版铜护甲的透明轮廓与帧位置作动画模板，所有可见颜色采样下界合金头部、胸部、手臂、护腿和靴子的UV区域；没有复制铜色材质。头盔／护腿40×1120，胸甲360×224，包含原版男／女、移动及攻击部件。输出位于`Textures/Equipment/`。三维袖子直接使用原版下界合金64×32图集中手臂六面的UV，保留手部原色。适配是泰拉瑞亚尺寸的二维穿戴精灵，并非完整Minecraft三维盔甲模型。没有AI重绘、GPU读回或加载线程图形调用。

已检查`.dev/projection-armor-preview.png`，核对20帧头／腿非空、胸甲及各物品透明边缘与原生尺寸；实际穿戴、挥动、染色和三维视角仍需手动验收。装备格式依据tModLoader官方[护甲材质迁移说明](https://github.com/tModLoader/tModLoader/wiki/Armor-Texture-Migration-Guide)。

## v0.18.1 史莱姆蛋

史莱姆蛋复用`Core/ContentPixelArt.cs`中羊蛋的16×24代码像素轮廓，主色`0x78BDF2`、斑点`0x3472CB`、描边`0x203E76`，使用原有RawImage与资产读取器生成运行时纹理。羊蛋原有配色和像素输出保持一致。不是新生成的AI图片，没有新增外部素材；生物直接使用泰拉瑞亚原版蓝史莱姆及其素材、AI和掉落。自动检查核对蛋形透明度、蓝色通道及与羊蛋的可辨识差异。

## v0.18.0 村民

`DimensionalTerraria/Textures/Villager.png`由内置image_gen生成，1254×1254透明PNG、4列×2行，棕袍、长鼻、交叠手臂的Minecraft平原村民风格。八帧依次为待机、行走A、行走B、行走C、行走D、受惊奔跑、眨眼、受击；以待机高度统一缩放到42像素、脚底锚定，实体20×42，二维和三维共用IPixelCreature裁帧。没有攻击姿态。原图复制到项目内，未修改其他素材。

完整提示词、内置工具信息、原始位置和SHA-256见`artifacts/asset-prompts-v0.18.0.json`。源图已查看，自动检查确认8帧分别可裁切、脚底有效且背景透明；游戏内动作和尺寸仍需实测。

v0.17.0新增漏斗的原版16×16PNG：`hopper`（透明物品／二维方块精灵）、`hopper_top`（中空透明顶面）、`hopper_inside`和`hopper_outside`。均从同一份SHA-1已校验的Minecraft Java 1.21.1客户端逐字节提取，来源及SHA-256见`docs/asset-sources/asset-sources-v0.17.0.json`。三维几何参照原版`assets/minecraft/models/block/hopper.json`的七个长方体，包含四条漏口边、槽底、收窄底座和向下出料管；纹理按部件范围采样，保留漏口透明度，不是实心立方体图标。实际包后台线程读取四张rawimg，保留原字节解码结果，没有GPU读回。

v0.16.1新增3张原版16×16PNG：`observer_back`、`observer_back_on`、`observer_side`，从已校验的同一份Minecraft Java 1.21.1客户端归档逐字节提取，用于自定义红石脉冲器。来源、归档条目和SHA-256见`docs/asset-sources/asset-sources-v0.16.1.json`。CPU将灯面拼为16×36亮灭图集，帧间留2像素透明边距；物品为24×24原材质立方体。三维灯面随世界时钟切换，侧面使用侦测器侧面。功能为四向自动时钟，不是原版侦测器的方块变化检测。实际模组包测试在后台线程读取rawimg并生成图集／图标，不调用GPU读回。

v0.16.0新增7张原版PNG：`redstone`（物品）及`redstone_block`、`piston_top`、`piston_top_sticky`、`piston_bottom`、`piston_side`、`piston_inner`（方块）。从同一个SHA-1已校验的官方Minecraft Java 1.21.1客户端归档逐字节提取，源文件保持16×16，未使用AI重绘。来源、归档位置与SHA-256见`docs/asset-sources/asset-sources-v0.16.0.json`；自动检查与客户端原条目逐字节哈希比较。

红石块与两种活塞物品沿用24×24原材质立方体图标；二维活塞使用CPU制作的72×36图集，四列方向、两行状态／头部种类，帧间透明边距2像素。轴杆取原版`piston_side`顶部4像素区域，与原版`template_piston_head.json`的纹理区间一致。三维底座按朝向选择原版前面、背面与侧面，活塞头由四分之一格厚的帽和四分之一格宽的杆组成，粘液仅位于前面。图集与图标从包内rawimg读取，加载线程不调用GPU读回。已检查`.dev/redstone-preview.png`的四方向、伸缩状态、普通／粘性头和闭合立方体图标。

v0.15.0新增`Textures/Minecraft/oak_boat.png`，直接取自官方Minecraft Java 1.21.1客户端的`assets/minecraft/textures/item/oak_boat.png`，16×16透明PNG，未重新生成或重绘。物品使用原始图标，放置船体由CPU以最近邻采样放大到48×32，再分为3列×2行的16像素帧，帧间留2像素透明间距，得到54×36图集；二维及三维共用该精灵，尚不是完整三维船模型。来源与SHA-256保存在`docs/asset-sources/asset-sources-v0.15.0.json`。后台线程测试覆盖实际包中图标解码与全部六帧，避免加载线程访问GPU。

v0.14.1修正加载阶段的像素来源：tModLoader打包时已把PNG转换为`.rawimg`，现在通过`Mod.GetFileStream`与`ImageIO.ReadRaw`直接读取包内RGBA数据，在CPU上投影立方体、裁取铁傀儡待机帧并制作缩略图。不再在加载线程请求渲染用图集或通过`Texture2D.GetData`回读显卡。原版PNG文件、生成规则与尺寸均保持不变，最终纹理由已有资产读取器上传；后台线程回归检查直接使用实际模组包，不创建图形设备。

v0.14.0新增橡木板与Minecraft工作台，直接从Mojang官方Java 1.21.1客户端提取原版PNG，不使用AI重新生成。官方版本清单为`https://piston-meta.mojang.com/mc/game/version_manifest_v2.json`，客户端`https://piston-data.mojang.com/v1/objects/30c73b1c5da787909b2f73340419fdf13b9def88/client.jar`，SHA-1校验为`30c73b1c5da787909b2f73340419fdf13b9def88`。

项目内`DimensionalTerraria/Textures/Minecraft/`包含`oak_planks.png`、`crafting_table_front.png`、`crafting_table_side.png`、`crafting_table_top.png`，均为16×16不透明原始文件，与归档中的`assets/minecraft/textures/block/`对应文件逐字节哈希一致。完整URL、SHA-256和归档路径记录在`docs/asset-sources/asset-sources-v0.14.0.json`。这些原版材质来自Mojang/Minecraft。

立方体物品图标使用现有代码像素系统把上述三面投影到24×24透明图标，不修改四张源材质。三维工作台为不同面的纹理分别建立网格批次；二维正面保持原版16×16。预制铁傀儡没有新画皮肤，复用既有铁傀儡图集第0帧，透明裁剪后等比像素采样为32×40图标；所有物品绘制共用该运行时纹理，避免整张多姿势图集显示在背包或手中。预览与源材质尺寸／透明度／来源一致性已检查。

# v0.9.0 素材

生成方式：内置 `image_gen`，三次独立生成，透明背景；没有使用CLI/API密钥。生成原图已复制到模组目录，运行时自行读取透明区域和脚底锚点，不依赖生成目录。

| 文件 | 用途与动作 | 游戏内尺寸 |
|---|---|---|
| `DimensionalTerraria/Textures/Steve.png` | 4×2图集：待机、行走A、过渡、行走B、反向过渡、跳跃、攻击准备、攻击结束 | 静止高度42像素，保持各帧比例，脚底对齐 |
| `DimensionalTerraria/Textures/Sheep.png` | 4×2图集：待机、行走A、过渡、行走B、反向过渡、吃草、眨眼、受击 | 静止高度28像素，实体34×28，脚底对齐 |
| `DimensionalTerraria/Textures/HigherDimensionalProjection.png` | 附魔下界合金剑风格单帧，刃尖右上、剑柄左下 | 二维绘制源图最长边缩放到48像素，挥剑定位同样采用48像素逻辑帧；三维沿用手持物品尺寸上限 |

三张源图实际均为1254×1254 PNG。人物、羊的逻辑帧由4×2网格限定，再按透明区域计算绘制框，避免把整张图集叠画为一个实体。源图中各姿势并非完全等大，因此按静止姿势统一缩放，固定脚底，不逐帧拉伸。人物小地图头像暂保留原版。

从v0.9.2开始，Steve外观由单个角色存档中的身份标记决定，通过角色选择页“新建 Steve 角色”入口创建，不再全局替换所有人物。已有角色及普通新建角色不自动转换。

完整提示词、原始生成路径和模组内路径保存在 [`artifacts/asset-prompts-v0.9.0.json`](artifacts/asset-prompts-v0.9.0.json)。第一人称手臂是代码绘制的方形几何体，青蓝袖子RGB(0,165,173)、肉色手RGB(184,137,110)，没有额外贴图。背景复用当前tModLoader的原版素材。

v0.10.0的27种压缩方块与羊蛋采用代码定义的原生尺寸像素素材：[ContentPixelArt.cs](DimensionalTerraria/Core/ContentPixelArt.cs)。方块面16×16、立方体图标24×24、灰白斑点羊蛋16×24，按材料调色板区分。客户端通过当前引擎的RawImg读取器创建各模组物品／方块的纹理，不修改原版素材，不生成额外大图；真实引擎检查覆盖格式解码。方块每格显示独立的镶边面。

v0.10.1修正立方体侧面的投影公式：上表面与两侧面连续，底部中央接缝向下延伸，形成闭合六边形轮廓。自动检查覆盖逐行连通、中央无透明裂缝、左右明暗及透明外缘，材质调色板和原生尺寸保持不变。

v0.11.0的三维天顶剑剑影直接复用原版物品贴图与FinalFractalHelper中的拖尾颜色，飞龙剑气复用原版ProjectileID.DD2SquireSonicBoom贴图；拖尾使用接受三维深度测试的彩色网格。没有生成新图片，也没有直接调用原版基于屏幕坐标的FinalFractal着色器。

v0.12.0新增`DimensionalTerraria/Textures/IronGolem.png`，由内置image_gen生成，透明PNG，实际尺寸1774×887，4列×2行。八帧依次为待机、行走A、行走过渡、行走B、行走返回、蓄力、双臂上挑、受击。外观为白灰色铁质方形身体、长臂、红眼、长鼻及绿色藤蔓。以第0帧身高统一缩放到76像素，动作中不逐帧拉伸，按各帧实际脚底锚点绘制；二维与三维通过IPixelCreature共用裁帧、缩放、朝向和锚点。三维只在真实图集就绪时绘制，避免临时纹理或整张图集被当成单个生物。透明背景允许接近零的边缘Alpha，轮廓提取使用原有128阈值。

完整提示词、内置工具信息、生成路径与项目路径见[`artifacts/asset-prompts-v0.12.0.json`](artifacts/asset-prompts-v0.12.0.json)。自动检查确认八个姿态都可独立安全选择，脚底锚点有效且有真实透明背景；没有程序生成或重绘该生物素材。
