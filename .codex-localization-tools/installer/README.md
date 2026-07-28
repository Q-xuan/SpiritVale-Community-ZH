# SpiritVale 简体中文补丁安装器

作者：auryx  
QQ群：882132807  
个人汉化学习作品，侵删。

面向 Windows 10/11 x64 的自包含、分级兼容安装器。主包为单文件，另提供不经过单文件
自解压链的多文件 ZIP 兼容包。两种包均内嵌 BepInEx 6 IL2CPP、
Unity Doorstop、Unity 6 基础库、SpiritVale 运行时汉化插件、离线词典和实体双语目录，
不包含游戏本体或预生成的 IL2CPP interop；首次启动使用包内 Unity 基础库离线生成桥接文件。
安装器不联网、不收集遥测、不显示广告，也不依赖在线服务。

## 构建

在游戏根目录已有验证可用的 BepInEx 环境、Release 插件 DLL 和词典时运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\.codex-localization-tools\installer\Publish.ps1
```

输出位于 `.codex-localization-tools\installer\dist`，同时生成 SHA-256 文件：

- `SpiritVale_Chinese_Patch.exe`：基于 .NET 8 LTS 的自包含、未压缩单文件主包。
- `SpiritVale_Chinese_Patch_Compatibility_x64.zip`：自包含多文件兼容包；必须完整解压后运行其中的 EXE。

构建脚本只会加入 Release DLL、`translations.tsv` 和 `bilingual-entity-catalog.tsv`，并拒绝日志、调试文件、
XUnity、缓存或 interop 进入载荷。

双语显示默认关闭。插件首次运行生成的配置位于
`BepInEx\config\local.spiritvale.runtime-localization.cfg`：详情实体可选 `Bilingual`，
紧凑实体可选 `EnglishToggle`，默认按键为 `Tab`；每次按下按键切换中文/英文，松开不改变显示。
旧配置值 `EnglishOnHold` 仍可读取，并按相同的切换行为处理。拍卖行输入、玩家名、聊天、公会名和排行榜显示名不进入双语展示目录。

## 自检

```powershell
.\.codex-localization-tools\installer\dist\SpiritVale_Chinese_Patch.exe --self-test C:\Temp\SpiritValePatchTest
```

自检目录必须为空。测试会验证首次安装、不可变 original-state 清单与备份、重复
安装/覆盖升级后首次备份逐字节不变、未知游戏哈希拒绝安装、损坏或替换 Payload
零写入拒绝、目标路径游戏进程保护、XUnity 安全禁用、安装/升级失败回滚、用户
修改冲突显示及保留、Verified/Compatible-Unverified/Blocked 分类、未知版本显式同意、
错误 App ID/结构/denylist 阻断、跨版本更新、清单迁移、新载荷路径追加、缺失/截断/哈希不匹配备份
零写入拒绝、未知游戏版本恢复、恢复失败回滚，以及保留首次备份后的再次安装与恢复。

安装器进入托管入口后会把启动诊断写入：
`%LOCALAPPDATA%\auryx\SpiritValeChinesePatch\Logs\installer-startup.log`。若界面无法打开，反馈时应同时提供该日志；
若该日志完全不存在，通常表示系统版本、UCRT、安全软件或单文件解压在应用入口前阻止了启动，应改用兼容 ZIP。

## 兼容级别

- `Verified`：内嵌离线清单中的 Build 与哈希已完成实机验证，正常安装。
- `Compatible-Unverified`：Steam、PE、metadata、IL2CPP 自动生成和载荷探针通过，但哈希未知；必须明确勾选兼容尝试，汉化完整度可能下降。
- `Blocked`：错误目录/App ID、关键文件异常、denylist、探针失败或目标游戏运行中；强制零写入。

兼容尝试不会把版本加入 Verified。只有迁移控制循环完成两次冷启动、日志/UI 检查和
`RecordLive` 后，`ApproveGameHash` 才能追加离线 Verified 清单并允许发布。

## 发布注意事项

- 普通用户优先使用 `dist\SpiritVale_Chinese_Patch.exe`；无法打开时改发
  `dist\SpiritVale_Chinese_Patch_Compatibility_x64.zip`，并要求完整解压后运行。
- 发布脚本会把上一份冻结 EXE 和兼容 ZIP 按版本与哈希归档到 `release\archive`，不会静默覆盖旧冻结产物。
- 建议对最终 EXE 做 Authenticode 签名，降低 SmartScreen 和安全软件误报。
- 游戏更新后先以 Compatible-Unverified 审计；实机通过后由控制循环追加 Verified 清单。
- `licenses` 中的上游许可证会嵌入 EXE，可从“组件说明”页查看。
