# 更新记录

## 0.24.2

- 公开源码目录改为MineTerraria，工程文件与入口源文件改为MineTerraria.csproj和MineTerraria.cs。
- 同步构建、测试、素材工具与文档中的文件路径。
- 内部模组标识保持DimensionalTerraria，官方打包改从Git忽略的临时目录执行，以继续使用原存档中的物品与世界数据标识。
- 直接dotnet build只编译程序集，正式打包统一使用scripts/Build.ps1。

## 0.24.1

- 漏斗增加使用Minecraft箱子的替代配方：5个铁锭＋1个Minecraft箱子，在Minecraft工作台旁合成1个漏斗。
- 原版木箱配方保留。

## 0.24.0 — MineTerraria

- 对外名称改为 MineTerraria，内部标识保留 DimensionalTerraria，以兼容原有存档。
- 新增下界合金材料，采用 Minecraft 原版下界合金锭材质。
- 投影剑改用下界合金配方；为投影镐、头盔、胸甲、护腿补充配方。
- 补齐基岩、红石、羊蛋、史莱姆蛋与村民蛋的配方；保留烟花现有配方。
- 除木板和工作台的引导配方外，相关配方统一要求 Minecraft 工作台。
- 移除召怪和测试按钮、永久白天、F6 录制召怪、F3 坐标、就绪提示和 status 诊断。
- 保留本模组及原版物品图鉴、搜索、点击领取；调试远程枪保留于图鉴且无配方。
- 精简设置说明与模组介绍，整理公开 README、配方文档和可提交的素材来源清单。
- 新包使用 MineTerraria-v0.24.0.tmod 文件名，既有版本保留。

更早版本的功能沿革见 [历史开发说明](docs/DEVELOPMENT_HISTORY.md)；测试与实机边界见 [验证记录](VALIDATION.md)。
