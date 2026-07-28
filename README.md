# SpiritVale-Community-ZH

SpiritVale 简体中文社区补丁。项目通过 BepInEx 6 IL2CPP 运行时插件替换受控的游戏界面文本，并提供可恢复原始文件的 Windows 安装器。

本仓库只包含汉化源码、审核后的词典、测试、维护技能和安装器源码，不包含 SpiritVale 游戏本体、Unity 资源、BepInEx 运行时二进制或玩家数据。

## 获取补丁

从 GitHub Releases 下载与版本对应的 `SpiritVale_Chinese_Patch_vX.Y.Z.exe`。兼容包仅用于单文件安装器无法启动的 Windows 环境，使用前应核对同一 Release 中的 SHA-256 文件。

安装、更新、恢复原版前请完全退出 SpiritVale。补丁仅适用于 Steam App ID `3767850`。

## 源码结构

- `.codex-localization-tools/SpiritVale.RuntimeLocalization`：运行时汉化插件。
- `.codex-localization-tools/SpiritVale.*.Tests`：无需游戏进程即可运行的托管测试程序。
- `.codex-localization-tools/installer`：离线安装器、兼容策略和第三方许可。
- `.codex-localization-tools/skills`：汉化维护与打包 Agent Loop。
- `.codex-localization-tools/artifacts`：当前审核通过的词典、实体目录和哈希绑定审计。

## 本地验证

以下测试只编译与游戏程序集无关的纯逻辑：

```powershell
dotnet run --project .\.codex-localization-tools\SpiritVale.BilingualDisplay.Tests\SpiritVale.BilingualDisplay.Tests.csproj -c Release
dotnet run --project .\.codex-localization-tools\SpiritVale.MarketSearch.Tests\SpiritVale.MarketSearch.Tests.csproj -c Release
```

完整的 `Status`、`Queue`、`Audit`、`Validate`、`Build`、`RecordLive` 和 `Package` 流程必须在合法安装的 SpiritVale 游戏目录中运行，并遵守 `.codex-localization-tools/skills/update-spiritvale-localization/SKILL.md` 的安全门禁。

## 版本与发布

仓库版本以根目录 `VERSION` 为准，发布说明来自 `CHANGELOG.md` 中唯一、非空且日期有效的同版本章节。内容版本、安装器版本、最后实机验证版本是不同状态；只有全部发布门禁通过后才能创建 `vX.Y.Z` tag。

Commit CI 在所有分支的 push 和面向 `master` 的 pull request 上运行。Release workflow 只接受严格的 `vX.Y.Z` tag，使用专用 Windows self-hosted runner 构建经实机验证的候选，再由 GitHub 托管 runner 校验哈希并发布 Release。运行器需要标签 `self-hosted`、`Windows`、`X64`、`spiritvale-release`，并设置 `SPIRITVALE_ROOT` 指向本机游戏目录。

不要为测试 workflow 而创建 tag；tag 会触发真实发布流程。

## 第三方组件

安装器中随附组件的许可与声明位于 `.codex-localization-tools/installer/licenses` 和 `THIRD_PARTY_NOTICES.txt`。SpiritVale 及其游戏资源归原权利人所有。
