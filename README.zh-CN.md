# 不歪种神器 (Seedline)

注：机翻

《杀戮尖塔2》模组，让同一种子对局在不同路线和决策下，拥有相近的奖励、选择与战斗挑战。

Seedline 减少无关操作对后续运气的影响。例如，原版中某场战斗多洗一次牌，可能改变下一场的起手。Seedline 将各场战斗的洗牌分开，因此仅仅多洗一次牌不会改变下一场起手。

为了让同种子体验更接近，Seedline 调整了部分奖励概率，以及操作影响后续随机结果的方式。

如果你通过存读档尝试不同操作（SL），与原版的差异会更明显。一些原本能重掷结果的操作不再有效，因此这种玩法的体验可能变化很大。详见[玩法变化](design/DESIGN.zh-CN.md#共享原版偏差)。

## 支持环境

需要 Windows 版 **StS2 v0.114.0（`public-beta`）**。支持标准和恐惧单人对局。

自定义对局支持任意组合的**飞行、嗜杀成性、身患绝症、暗夜惊魂、点金手、精英猎手、各角色卡牌、囤积癖、选牌**。

多人、极速、每日挑战及含其他自定义调整项的对局正常运行，Seedline 在这些对局中不生效。

本模组使用全部已解锁内容，不改变档案解锁状态或进阶上限。

## 安装

推荐在 [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3843171761)订阅。

手动安装时，从[最新发布](https://github.com/asHOH/seedline/releases/latest)下载 `Seedline-<version>.zip`。关闭游戏，将 `Seedline` 文件夹解压到游戏的 `mods` 文件夹（例如D:\Programs\Steam\steamapps\common\Slay the Spire 2\mods）；若不存在则创建 `mods`。

## 存档

一局中请保持游戏版本、Seedline 和 RitsuLib 配置不变。禁用 Seedline 可能丢失其存档状态。详见[兼容性与存档说明](docs/compatibility.md)。

## 问题反馈

请优先在 Steam 创意工坊讨论区反馈，也可联系 QQ 群 **609823795**，说明预期与实际表现和复现步骤。
在设置中选择**不歪种神器诊断报告 → 导出诊断报告**，将生成的 ZIP 私信给维护者。

## 进一步了解

- [玩法设计](design/DESIGN.zh-CN.md)——规则与取舍。
- [开发](docs/development.md)——构建、测试与发布。
- [研究](research/README.md)——证据与方法。

## 许可证

[MIT](LICENSE)。
