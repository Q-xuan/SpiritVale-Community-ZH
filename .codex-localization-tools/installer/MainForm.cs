using System.Diagnostics;
using System.Reflection;

namespace SpiritVale.ChinesePatch.Installer;

internal sealed class MainForm : Form
{
    private readonly Color _ink = Color.FromArgb(31, 38, 36);
    private readonly Color _muted = Color.FromArgb(101, 111, 107);
    private readonly Color _accent = Color.FromArgb(29, 122, 86);
    private readonly Color _surface = Color.FromArgb(247, 248, 246);
    private readonly TextBox _pathBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _versionLabel = new();
    private readonly Button _installButton = new();
    private readonly Button _uninstallButton = new();
    private readonly Button _launchButton = new();
    private readonly CheckBox _compatibilityConsent = new();
    private readonly TextBox _logBox = new();
    private readonly TabControl _tabs = new();
    private readonly PatchService _patchService;
    private CancellationTokenSource? _inspectionCancellation;
    private GameInspection? _lastInspection;
    private bool _autoDetectRunning;

    public MainForm()
    {
        _patchService = new PatchService(Log);
        SuspendLayout();
        Text = $"SpiritVale 简体中文补丁 v{PatchInfo.Version} {PatchInfo.ReleaseLabel}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 590);
        Size = new Size(940, 660);
        BackColor = _surface;
        Font = new Font("Microsoft YaHei UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(shell);
        shell.Controls.Add(BuildSidebar(), 0, 0);
        shell.Controls.Add(BuildContent(), 1, 0);

        Shown += async (_, _) =>
        {
            await Task.Yield();
            await AutoDetectAsync();
        };
        FormClosed += (_, _) => _inspectionCancellation?.Cancel();
        ResumeLayout(true);
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = _ink, Padding = new Padding(28, 34, 24, 24) };
        var title = new Label
        {
            Text = "SPIRITVALE\r\n简体中文补丁",
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            AutoSize = true
        };
        panel.Controls.Add(title);

        var version = new Label
        {
            Text = $"auryx 个人汉化  v{PatchInfo.Version}\r\n{PatchInfo.ReleaseLabel}",
            ForeColor = Color.FromArgb(162, 210, 190),
            Location = new Point(30, 104),
            AutoSize = true
        };
        panel.Controls.Add(version);

        var steps = new Label
        {
            Text = "01   定位 Steam 游戏\r\n\r\n02   安装或更新补丁\r\n\r\n03   从 Steam 启动",
            ForeColor = Color.FromArgb(221, 227, 224),
            Location = new Point(30, 178),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 10F)
        };
        panel.Controls.Add(steps);

        var note = new Label
        {
            Text = "作者：auryx  ·  QQ群：882132807\r\n个人汉化学习作品，侵删\r\n\r\nWindows 10/11  ·  64 位\r\n不包含游戏本体",
            ForeColor = Color.FromArgb(145, 155, 151),
            Dock = DockStyle.Bottom,
            Height = 104,
            TextAlign = ContentAlignment.BottomLeft
        };
        panel.Controls.Add(note);
        return panel;
    }

    private Control BuildContent()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 24), BackColor = _surface };
        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = new Font("Microsoft YaHei UI", 9.5F);
        _tabs.Controls.Add(BuildInstallTab());
        _tabs.Controls.Add(BuildGuideTab());
        _tabs.Controls.Add(BuildAboutTab());
        panel.Controls.Add(_tabs);
        return panel;
    }

    private TabPage BuildInstallTab()
    {
        var page = NewPage("安装");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 1, Padding = new Padding(8, 14, 8, 4) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(layout);

        layout.Controls.Add(Heading("游戏位置"), 0, 0);
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 8, 0, 12) };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pathBox.Dock = DockStyle.Fill;
        _pathBox.Margin = new Padding(0, 2, 8, 2);
        _pathBox.TextChanged += (_, _) => RefreshInspection();
        pathRow.Controls.Add(_pathBox, 0, 0);
        var detect = MakeButton("自动查找", false);
        detect.Click += async (_, _) => await AutoDetectAsync();
        pathRow.Controls.Add(detect, 1, 0);
        var browse = MakeButton("选择文件", false);
        browse.Click += (_, _) => BrowseGame();
        pathRow.Controls.Add(browse, 2, 0);
        layout.Controls.Add(pathRow, 0, 1);

        var statusPanel = new Panel { Height = 84, Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(14, 10, 14, 8), Margin = new Padding(0, 0, 0, 8) };
        _statusLabel.Text = "正在检测游戏目录...";
        _statusLabel.ForeColor = _ink;
        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Height = 26;
        _statusLabel.Font = new Font(Font, FontStyle.Bold);
        _versionLabel.Text = "";
        _versionLabel.ForeColor = _muted;
        _versionLabel.Dock = DockStyle.Bottom;
        _versionLabel.Height = 24;
        statusPanel.Controls.Add(_statusLabel);
        statusPanel.Controls.Add(_versionLabel);
        layout.Controls.Add(statusPanel, 0, 2);

        _compatibilityConsent.AutoSize = true;
        _compatibilityConsent.Text = "我理解该版本尚未实机验证，允许兼容尝试（汉化完整度可能下降）";
        _compatibilityConsent.ForeColor = Color.FromArgb(143, 91, 18);
        _compatibilityConsent.Margin = new Padding(2, 0, 0, 10);
        _compatibilityConsent.Visible = false;
        _compatibilityConsent.CheckedChanged += (_, _) =>
        {
            if (_lastInspection is not null) ApplyInspection(_lastInspection);
        };
        layout.Controls.Add(_compatibilityConsent, 0, 3);

        var actionRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 14), WrapContents = false };
        _installButton.Text = "安装汉化";
        StyleButton(_installButton, true);
        _installButton.Click += async (_, _) => await RunOperationAsync(true);
        actionRow.Controls.Add(_installButton);
        _uninstallButton.Text = "恢复原版";
        StyleButton(_uninstallButton, false);
        _uninstallButton.Click += async (_, _) => await RunOperationAsync(false);
        actionRow.Controls.Add(_uninstallButton);
        _launchButton.Text = "从 Steam 启动";
        StyleButton(_launchButton, false);
        _launchButton.Click += (_, _) => LaunchGame();
        actionRow.Controls.Add(_launchButton);
        layout.Controls.Add(actionRow, 0, 4);

        layout.Controls.Add(Heading("操作记录"), 0, 5);
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.BackColor = Color.White;
        _logBox.BorderStyle = BorderStyle.FixedSingle;
        _logBox.Font = new Font("Consolas", 9F);
        _logBox.Margin = new Padding(0, 8, 0, 10);
        layout.Controls.Add(_logBox, 0, 6);

        var warning = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(143, 91, 18),
            Text = "Verified 正常安装；Compatible-Unverified 需明确同意；Blocked 不会写入。首次桥接可能需要 1-3 分钟。",
            Margin = new Padding(0)
        };
        layout.Controls.Add(warning, 0, 7);
        return page;
    }

    private TabPage BuildGuideTab()
    {
        var page = NewPage("使用说明");
        var guide = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = _surface,
            ForeColor = _ink,
            Font = new Font("Microsoft YaHei UI", 10F),
            Text = $"分级兼容说明\r\n\r\nVerified 表示该 Build 与哈希已完成实机验证，可正常安装。Compatible-Unverified 表示 App ID、Steam 目录、x64 PE、IL2CPP metadata、离线自动生成条件和补丁载荷均通过探针，但该哈希尚未实机验证；只有明确勾选风险确认后才允许兼容尝试，汉化完整度可能下降。Blocked 表示目录、关键文件、denylist、结构探针或运行中进程不安全，安装器不会写入。\r\n\r\n安装步骤\r\n\r\n1. 在 Steam 安装 SpiritVale，并完全退出游戏。\r\n\r\n2. 自动查找或手动选择 SpiritVale.exe。\r\n\r\n3. 查看兼容级别；未知但兼容时阅读提示并明确选择是否尝试。\r\n\r\n4. 安装后从 Steam 启动。BepInEx 会使用包内 Unity 基础库离线生成当前 IL2CPP 桥接文件，通常需要 1-3 分钟。\r\n\r\n更新与恢复\r\n\r\n首次安装会建立带大小和 SHA-256 的不可变原始备份；重复安装和跨版本更新不会替换它。“恢复原版”只处理本安装器修改的文件，并在操作前列出用户修改冲突；确认后冲突文件保存为 .user-modified*。未知游戏哈希不阻止恢复。\r\n\r\n安装器完全离线，不下载代码或兼容清单，不含遥测、广告、游戏本体、interop 或存档。兼容尝试不会自动把版本加入 Verified；只有维护流程完成两次冷启动、UI/日志检查和 RecordLive 后才能批准发布。"
        };
        page.Controls.Add(guide);
        return page;
    }

    private TabPage BuildAboutTab()
    {
        var page = NewPage("组件说明");
        var panel = new Panel { Dock = DockStyle.Fill };
        var box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = _surface,
            ForeColor = _ink,
            DetectUrls = true,
            Font = new Font("Microsoft YaHei UI", 9.5F),
            Text = $"SpiritVale 简体中文补丁 v{PatchInfo.Version} {PatchInfo.ReleaseLabel}\r\nRelease channel: {PatchInfo.ReleaseChannel}\r\n兼容清单：内嵌离线 Verified + denylist\r\n\r\n作者：auryx\r\nQQ群：882132807\r\n个人汉化学习作品，侵删。\r\n\r\n运行方式\r\n使用 BepInEx 6 IL2CPP 与 Unity Doorstop 加载 SpiritVale 专用 HarmonyX 插件，在游戏设置 UGUI、TextMeshPro 和 UI Toolkit 文本时进行运行时替换。不会修改物品 ID、存档或游戏资产包。\r\n\r\n成熟开源组件\r\nBepInEx 6: https://github.com/BepInEx/BepInEx\r\nUnity Doorstop: https://github.com/NeighTools/UnityDoorstop\r\nHarmonyX: https://github.com/BepInEx/HarmonyX\r\n\r\n本安装器不会加载 XUnity AutoTranslator，并会安全禁用检测到的已启用副本。运行时 Harmony 目标变化时只跳过对应功能并记录告警。\r\n\r\n完整版权与许可证说明已内嵌在安装器资源 THIRD_PARTY_NOTICES.txt 中。"
        };
        box.LinkClicked += (_, eventArgs) =>
        {
            if (string.IsNullOrWhiteSpace(eventArgs.LinkText)) return;
            try { Process.Start(new ProcessStartInfo(eventArgs.LinkText) { UseShellExecute = true }); } catch { }
        };
        var licenses = MakeButton("查看完整许可证", false);
        licenses.Dock = DockStyle.Bottom;
        licenses.Height = 38;
        licenses.Click += (_, _) => ShowLicenses();
        panel.Controls.Add(box);
        panel.Controls.Add(licenses);
        page.Controls.Add(panel);
        return page;
    }

    private void ShowLicenses()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("SpiritValePatch.Licenses.", StringComparison.Ordinal))
            .OrderBy(name => name)
            .ToArray();
        var text = new System.Text.StringBuilder();
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            text.AppendLine(new string('=', 72));
            text.AppendLine(name["SpiritValePatch.Licenses.".Length..]);
            text.AppendLine(new string('=', 72));
            text.AppendLine(reader.ReadToEnd());
        }

        using var dialog = new Form
        {
            Text = "第三方开源许可证",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(780, 590),
            MinimumSize = new Size(620, 440),
            Font = Font
        };
        dialog.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F),
            Text = text.ToString()
        });
        dialog.ShowDialog(this);
    }

    private TabPage NewPage(string title) => new(title) { BackColor = _surface, Padding = new Padding(6) };

    private Label Heading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = _ink,
        Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
        Margin = new Padding(0)
    };

    private Button MakeButton(string text, bool primary)
    {
        var button = new Button { Text = text };
        StyleButton(button, primary);
        return button;
    }

    private void StyleButton(Button button, bool primary)
    {
        button.AutoSize = true;
        button.MinimumSize = new Size(94, 34);
        button.Padding = new Padding(9, 2, 9, 2);
        button.Margin = new Padding(0, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(177, 185, 181);
        button.BackColor = primary ? _accent : Color.White;
        button.ForeColor = primary ? Color.White : _ink;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
    }

    private async Task AutoDetectAsync()
    {
        if (_autoDetectRunning) return;
        _autoDetectRunning = true;
        Log("正在搜索 Steam 游戏库...");
        try
        {
            var matches = await Task.Run(() => PatchService.FindGameDirectories());
            if (IsDisposed) return;
            if (matches.Count == 0)
            {
                Log("没有自动找到游戏，请手动选择 SpiritVale.exe。");
                RefreshInspection();
                return;
            }

            _pathBox.Text = matches[0];
            Log($"已找到游戏：{matches[0]}");
            if (matches.Count > 1) Log($"另检测到 {matches.Count - 1} 个游戏目录，当前使用第一个。 ");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write($"Steam auto-detection failed: {ex}");
            Log("自动查找遇到错误，请手动选择 SpiritVale.exe。");
            RefreshInspection();
        }
        finally
        {
            _autoDetectRunning = false;
        }
    }

    private void BrowseGame()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 SpiritVale.exe",
            Filter = "SpiritVale 游戏程序|SpiritVale.exe|可执行文件|*.exe",
            CheckFileExists = true,
            FileName = "SpiritVale.exe"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _pathBox.Text = Path.GetDirectoryName(dialog.FileName) ?? "";
    }

    private void RefreshInspection()
    {
        _inspectionCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _inspectionCancellation = cancellation;
        _lastInspection = null;
        _statusLabel.Text = "正在检查游戏版本...";
        _statusLabel.ForeColor = _muted;
        _versionLabel.Text = "";
        _installButton.Enabled = false;
        _uninstallButton.Enabled = false;
        _launchButton.Enabled = false;
        _ = RefreshInspectionAsync(_pathBox.Text.Trim(), cancellation);
    }

    private async Task RefreshInspectionAsync(string path, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(150, cancellation.Token);
            var inspection = await Task.Run(() => _patchService.Inspect(path), cancellation.Token);
            if (cancellation.IsCancellationRequested || IsDisposed || _inspectionCancellation != cancellation) return;
            _lastInspection = inspection;
            ApplyInspection(inspection);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (cancellation.IsCancellationRequested || IsDisposed) return;
            StartupDiagnostics.Write($"Game inspection failed: {ex}");
            _statusLabel.Text = "检查失败，请重新选择游戏目录";
            _statusLabel.ForeColor = Color.FromArgb(177, 55, 55);
            _versionLabel.Text = ex.Message;
            Log("游戏目录检查失败：" + ex.Message);
        }
    }

    private void ApplyInspection(GameInspection inspection)
    {
        _statusLabel.Text = inspection.Summary;
        _statusLabel.ForeColor = inspection.CompatibilityLevel switch
        {
            CompatibilityLevel.Verified => _accent,
            CompatibilityLevel.CompatibleUnverified => Color.FromArgb(159, 96, 17),
            _ => Color.FromArgb(177, 55, 55)
        };
        _versionLabel.Text = inspection.IsValid
            ? $"Build {inspection.SteamBuildId ?? "?"} / GameAssembly {ShortHash(inspection.GameHash)} / Metadata {ShortHash(inspection.MetadataHash)}"
            : "请选择包含 SpiritVale.exe、GameAssembly.dll 和 SpiritVale_Data 的目录";
        var needsConsent = inspection.CompatibilityLevel == CompatibilityLevel.CompatibleUnverified;
        if (!needsConsent && _compatibilityConsent.Checked) _compatibilityConsent.Checked = false;
        _compatibilityConsent.Visible = needsConsent;
        _installButton.Enabled = inspection.CanInstall && (!needsConsent || _compatibilityConsent.Checked);
        _installButton.Text = inspection.CompatibilityLevel == CompatibilityLevel.Blocked
            ? "已安全阻止"
            : needsConsent && !_compatibilityConsent.Checked
                ? "请先确认兼容尝试"
                : inspection.PatchState == PatchState.NotInstalled ? "安装汉化" : "修复 / 更新";
        _uninstallButton.Enabled = inspection.CanRestore;
        _launchButton.Enabled = inspection.IsValid;
    }

    private static string ShortHash(string? hash) => hash is { Length: >= 12 } ? hash[..12] + "..." : "?";

    private async Task RunOperationAsync(bool install)
    {
        var path = _pathBox.Text.Trim();
        if (!install)
        {
            var conflicts = _patchService.FindRestoreConflicts(path);
            var conflictNotice = conflicts.Count == 0
                ? "未检测到用户修改冲突。"
                : "检测到以下安装后修改的文件：\r\n\r\n"
                  + string.Join("\r\n", conflicts.Take(12).Select(conflict => $"• {conflict.RelativePath}\r\n  {conflict.Reason}"))
                  + (conflicts.Count > 12 ? $"\r\n• 另有 {conflicts.Count - 12} 项未显示" : "")
                  + "\r\n\r\n确认后，这些文件会保存为 .user-modified* 副本，不会被静默覆盖。";
            var confirmation = MessageBox.Show(this,
                "将只复原本安装器实际修改的文件，并恢复首次安装前的同名文件。\r\n"
                + "不可变初始备份会保留，供以后重新安装时继续使用。\r\n\r\n"
                + conflictNotice
                + "\r\n\r\n确定要恢复原版吗？",
                "恢复 SpiritVale 原版",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes) return;
        }

        SetBusy(true);
        try
        {
            await Task.Run(() =>
            {
                if (install) _patchService.Install(path, _compatibilityConsent.Checked);
                else _patchService.RestoreOriginal(path, acceptUserModifiedFiles: true);
            });
            MessageBox.Show(this, install ? "汉化补丁安装完成。请从 Steam 启动游戏。" : "已恢复到首次安装汉化前的状态；初始备份已保留。",
                $"SpiritVale 简体中文补丁 {PatchInfo.ReleaseLabel}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("错误：" + ex.Message);
            MessageBox.Show(this, ex.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshInspection();
        }
    }

    private void SetBusy(bool busy)
    {
        if (InvokeRequired) { Invoke(() => SetBusy(busy)); return; }
        UseWaitCursor = busy;
        _pathBox.Enabled = !busy;
        _tabs.Enabled = !busy;
    }

    private void LaunchGame()
    {
        try { Process.Start(new ProcessStartInfo($"steam://rungameid/{PatchInfo.AppId}") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法启动 Steam", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Log(message)); return; }
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }
}
