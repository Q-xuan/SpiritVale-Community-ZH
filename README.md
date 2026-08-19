# SpiritVale-Community-ZH

SpiritVale 简体中文社区补丁

为 Steam 版 SpiritVale 提供简体中文运行时汉化和 Windows 离线安装器。补丁覆盖主要游戏界面、实体名称、技能、物品和说明文本，不包含游戏本体或玩家数据。

当前版本：[`v1.2.34`](https://github.com/Q-xuan/SpiritVale-Community-ZH/releases/tag/v1.2.34)  
适配版本：Steam Build `24773178` / App ID `3767850`

## 下载

- [下载标准安装器 EXE](https://github.com/Q-xuan/SpiritVale-Community-ZH/releases/download/v1.2.34/SpiritVale_Chinese_Patch_v1.2.34.exe)
- [下载 Windows x64 兼容版 ZIP](https://github.com/Q-xuan/SpiritVale-Community-ZH/releases/download/v1.2.34/SpiritVale_Chinese_Patch_v1.2.34_Compatibility_x64.zip)
- [查看全部版本与校验文件](https://github.com/Q-xuan/SpiritVale-Community-ZH/releases)

普通用户优先使用标准安装器。若 EXE 无法打开、被单文件解压环节阻止或没有生成安装器启动日志，请下载兼容版 ZIP，完整解压后运行其中的安装程序。

## v1.2.34 更新内容

- 集成词条品质评分 HUD，装备和神器详情显示稳定的品质星级，且与 `Tab` 中英文切换兼容。
- 修复新版拍卖行中文搜索，支持中文完整名称和歧义短词，同时保留高级筛选。
- 保留深度字体和运行时性能优化，实机帧率提升 20 多帧。
- 包含 5,229 条运行时翻译和 3,624 条双语实体目录记录。
- 适配 Steam Build `24773178`，已重新审计当前运行时实体和技能显示数据。
- 同时提供标准安装器和 Windows x64 兼容版。

## 安装方法

1. 在 Steam 中安装 SpiritVale，并完全退出游戏。
2. 运行下载的安装器。
3. 让安装器自动查找游戏目录，或手动选择 `SpiritVale.exe`。
4. 确认兼容状态后点击安装。
5. 从 Steam 启动游戏。首次启动会离线生成当前版本所需的 IL2CPP 桥接文件，通常需要等待 1–3 分钟。

安装、更新或恢复原版时都必须完全退出 SpiritVale。安装器离线运行，不下载代码、不收集遥测，也不包含广告。

## 显示与英文对照

- 默认显示中文实体名称；紧凑界面可直接按 `Tab` 在中文和英文名称之间切换。
- 配置文件位于 `BepInEx\config\local.spiritvale.runtime-localization.cfg`。
- 默认值为 `CompactSurfaceMode = EnglishToggle` 和 `TemporaryEnglishKey = Tab`。
- 每按一次 `Tab`，紧凑界面会在中文和英文名称之间切换；松开按键不会改变当前显示。

## 更新与恢复原版

- 安装新版本前先退出游戏，再直接运行新版安装器。
- 首次安装会保存原始文件备份，重复安装和跨版本更新不会覆盖首次备份。
- 需要卸载补丁时，重新打开安装器并选择“恢复原版”。
- 如果安装记录缺失或损坏，可在 Steam 中使用“验证游戏文件完整性”。

## 常见问题

### 安装器打不开

优先尝试兼容版 ZIP，并确保完整解压后再运行。安装器启动日志位于：

```text
%LOCALAPPDATA%\auryx\SpiritValeChinesePatch\Logs\installer-startup.log
```

### 游戏更新后提示版本未验证

等待补丁适配更新。安装器可能允许对结构兼容但尚未实机验证的版本进行兼容尝试，此时汉化完整度无法保证。

### 安全软件拦截安装器

请从本仓库 Releases 下载，并使用同一 Release 中的 `.sha256.txt` 文件核对 SHA-256。不要从不明来源下载重新打包的版本。

## 反馈

提交问题时请说明游戏 Build、补丁版本、复现步骤，并附上相关日志。安装器问题请提供上述 `installer-startup.log`；游戏内汉化问题请尽量附截图和原文位置。

作者：auryx  
QQ群：882132807

## 许可与声明

本项目为个人汉化学习作品。安装器所含第三方组件的许可证与声明位于 `.codex-localization-tools/installer/licenses` 和 `.codex-localization-tools/installer/THIRD_PARTY_NOTICES.txt`。SpiritVale 及其游戏资源归原权利人所有。
