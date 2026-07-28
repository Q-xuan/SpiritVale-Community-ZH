using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace SpiritVale.ChinesePatch.Installer;

internal static class PatchInfo
{
    public const string Version = "1.2.30";
    public const string ReleaseChannel = "tiered-compatibility";
    public const string ReleaseLabel = "分级兼容版";
    public const string AppId = "3767850";
    public const string StateDirectory = ".SpiritValeChinesePatch";
    public const string OriginalStateManifestName = "original-state.json";
    public const string OriginalStateSealName = "original-state.sha256";
    public const string ActiveManifestName = "manifest.json";
    public const string PluginRelativePath = "BepInEx\\plugins\\SpiritVale.RuntimeLocalization\\SpiritVale.RuntimeLocalization.dll";
    public const string EntityCatalogRelativePath = "BepInEx\\plugins\\SpiritVale.RuntimeLocalization\\bilingual-entity-catalog.tsv";
    public const string XUnityDisableSuffix = ".disabled-by-spiritvale-zh";
}

internal sealed class PatchManifest
{
    public int SchemaVersion { get; set; } = 4;
    public string PatchVersion { get; set; } = PatchInfo.Version;
    public string ReleaseChannel { get; set; } = PatchInfo.ReleaseChannel;
    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;
    public string SteamBuildId { get; set; } = "";
    public string GameAssemblySha256 { get; set; } = "";
    public string MetadataSha256 { get; set; } = "";
    public string CompatibilityLevel { get; set; } = "";
    public string PayloadSha256 { get; set; } = "";
    public string PayloadPluginSha256 { get; set; } = "";
    public string PayloadDictionarySha256 { get; set; } = "";
    public string PayloadEntityCatalogSha256 { get; set; } = "";
    public string DefaultEntityNameMode { get; set; } = "Chinese";
    public string DefaultCompactSurfaceMode { get; set; } = "EnglishToggle";
    public string DefaultTemporaryEnglishKey { get; set; } = "Tab";
    public string OriginalStateSha256 { get; set; } = "";
    public List<PatchFileRecord> Files { get; set; } = [];
    public List<DisabledConflictRecord> DisabledConflicts { get; set; } = [];
}

internal sealed class PatchFileRecord
{
    public string RelativePath { get; set; } = "";
    public long InstalledSize { get; set; }
    public string InstalledSha256 { get; set; } = "";
    public bool HadOriginal { get; set; }
}

internal sealed class OriginalStateManifest
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<OriginalStateFileRecord> Files { get; set; } = [];
    public List<DisabledConflictRecord> DisabledConflicts { get; set; } = [];
}

internal sealed class OriginalStateFileRecord
{
    public string RelativePath { get; set; } = "";
    public bool Existed { get; set; }
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

internal sealed class DisabledConflictRecord
{
    public string OriginalRelativePath { get; set; } = "";
    public string DisabledRelativePath { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

internal sealed record DisabledConflictResult(List<DisabledConflictRecord> Records);

internal sealed record RestoreConflict(string RelativePath, string Reason);

internal sealed record PayloadArchiveEntry(ZipArchiveEntry Entry, string RelativePath);

internal sealed record OriginalStateSnapshot(
    OriginalStateManifest Manifest,
    IReadOnlyDictionary<string, OriginalStateFileRecord> Files,
    string Sha256,
    string Json);

internal sealed record SpiritValeProcessProbe(
    int ProcessId,
    string? ExecutablePath,
    bool PathReadSucceeded);

internal enum CompatibilityLevel
{
    Verified,
    CompatibleUnverified,
    Blocked
}

internal sealed class CompatibilityPolicy
{
    public int SchemaVersion { get; set; }
    public string SteamAppId { get; set; } = "";
    public List<GameBuildRule> VerifiedBuilds { get; set; } = [];
    public List<GameBuildRule> DeniedBuilds { get; set; } = [];
}

internal sealed class GameBuildRule
{
    public string SteamBuildId { get; set; } = "";
    public string GameAssemblySha256 { get; set; } = "";
    public string MetadataSha256 { get; set; } = "";
    public string VerifiedAtUtc { get; set; } = "";
    public string Reason { get; set; } = "";
}

internal sealed record SteamInstallIdentity(bool IsValid, string BuildId, string Reason);

internal sealed record StructureProbeResult(bool IsValid, string Reason);

internal sealed class InstallTransaction : IDisposable
{
    private sealed record FileSnapshot(
        string Destination,
        bool Existed,
        string? SnapshotPath,
        FileAttributes Attributes,
        DateTime LastWriteTimeUtc);

    private readonly Action<string> _log;
    private readonly string _operationName;
    private readonly string _snapshotRoot = Path.Combine(
        Path.GetTempPath(), "SpiritValePatchTransaction-" + Guid.NewGuid().ToString("N"));
    private readonly List<FileSnapshot> _snapshots = [];
    private readonly HashSet<string> _captured = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _createdDirectories = [];
    private bool _finished;

    public InstallTransaction(Action<string> log, string operationName = "安装")
    {
        _log = log;
        _operationName = operationName;
        Directory.CreateDirectory(_snapshotRoot);
    }

    public void CaptureFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!_captured.Add(fullPath)) return;
        if (Directory.Exists(fullPath)) throw new IOException($"应为文件的位置存在目录：{fullPath}");

        if (!File.Exists(fullPath))
        {
            _snapshots.Add(new FileSnapshot(fullPath, false, null, FileAttributes.Normal, default));
            return;
        }

        var snapshotPath = Path.Combine(_snapshotRoot, $"{_snapshots.Count:D6}.bin");
        File.Copy(fullPath, snapshotPath, false);
        _snapshots.Add(new FileSnapshot(
            fullPath,
            true,
            snapshotPath,
            File.GetAttributes(fullPath),
            File.GetLastWriteTimeUtc(fullPath)));
    }

    public void EnsureDirectory(string path)
    {
        var missing = new Stack<string>();
        var current = Path.GetFullPath(path);
        while (!Directory.Exists(current))
        {
            if (File.Exists(current)) throw new IOException($"应为目录的位置存在文件：{current}");
            missing.Push(current);
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || parent.Equals(current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }

        while (missing.Count > 0)
        {
            var directory = missing.Pop();
            Directory.CreateDirectory(directory);
            _createdDirectories.Add(directory);
        }
    }

    public void Commit()
    {
        _finished = true;
        DeleteSnapshotRoot();
    }

    public void Rollback()
    {
        var failures = new List<Exception>();
        foreach (var snapshot in _snapshots.AsEnumerable().Reverse())
        {
            try
            {
                if (snapshot.Existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(snapshot.Destination)!);
                    if (File.Exists(snapshot.Destination)) File.SetAttributes(snapshot.Destination, FileAttributes.Normal);
                    File.Copy(snapshot.SnapshotPath!, snapshot.Destination, true);
                    File.SetLastWriteTimeUtc(snapshot.Destination, snapshot.LastWriteTimeUtc);
                    File.SetAttributes(snapshot.Destination, snapshot.Attributes);
                }
                else if (File.Exists(snapshot.Destination))
                {
                    File.SetAttributes(snapshot.Destination, FileAttributes.Normal);
                    File.Delete(snapshot.Destination);
                }
            }
            catch (Exception ex)
            {
                failures.Add(new IOException($"无法恢复事务文件：{snapshot.Destination}", ex));
            }
        }

        foreach (var directory in _createdDirectories.AsEnumerable().Reverse())
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception ex)
            {
                failures.Add(new IOException($"无法清理事务目录：{directory}", ex));
            }
        }

        if (failures.Count > 0)
            throw new AggregateException($"{_operationName}事务回滚不完整；快照保留在 {_snapshotRoot}", failures);

        _finished = true;
        DeleteSnapshotRoot();
        _log($"{_operationName}失败，已恢复操作开始前的全部文件。");
    }

    public void Dispose()
    {
        if (_finished) DeleteSnapshotRoot();
    }

    private void DeleteSnapshotRoot()
    {
        try { if (Directory.Exists(_snapshotRoot)) Directory.Delete(_snapshotRoot, true); }
        catch (Exception ex) { _log($"无法清理临时事务快照：{ex.Message}"); }
    }
}

internal enum PatchState
{
    NotInstalled,
    Installed,
    NeedsRepair
}

internal sealed record GameInspection(
    bool IsValid,
    PatchState PatchState,
    CompatibilityLevel CompatibilityLevel,
    string Summary,
    string? SteamBuildId,
    string? GameHash,
    string? MetadataHash,
    bool CanInstall,
    bool CanRestore);

internal sealed class PatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private readonly Action<string> _log;
    private readonly bool _checkGameProcess;
    private readonly string? _failAfterPayloadPath;
    private readonly string? _failAfterRestorePath;
    private readonly Func<IReadOnlyList<SpiritValeProcessProbe>> _getSpiritValeProcesses;
    private readonly Func<string, string> _getGameAssemblyHash;
    private readonly Func<Stream> _openPayload;
    private readonly string _expectedPayloadSha256;
    private readonly CompatibilityPolicy _compatibilityPolicy;

    public PatchService(
        Action<string> log,
        bool checkGameProcess = true,
        string? failAfterPayloadPath = null,
        string? failAfterRestorePath = null,
        Func<IReadOnlyList<SpiritValeProcessProbe>>? getSpiritValeProcesses = null,
        Func<string, string>? getGameAssemblyHash = null,
        Func<Stream>? openPayload = null,
        string? expectedPayloadSha256 = null,
        CompatibilityPolicy? compatibilityPolicy = null)
    {
        _log = log;
        _checkGameProcess = checkGameProcess;
        _failAfterPayloadPath = failAfterPayloadPath;
        _failAfterRestorePath = failAfterRestorePath;
        _getSpiritValeProcesses = getSpiritValeProcesses ?? GetSpiritValeProcessProbes;
        _getGameAssemblyHash = getGameAssemblyHash ?? ComputeHash;
        _openPayload = openPayload ?? (() => OpenResource("SpiritValePatch.Payload.zip"));
        _expectedPayloadSha256 = expectedPayloadSha256 ?? ReadEmbeddedText("SpiritValePatch.Payload.sha256").Trim();
        _compatibilityPolicy = compatibilityPolicy ?? LoadCompatibilityPolicy();
    }

    public static bool IsGameDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return File.Exists(Path.Combine(path, "SpiritVale.exe"))
            && File.Exists(Path.Combine(path, "GameAssembly.dll"))
            && Directory.Exists(Path.Combine(path, "SpiritVale_Data"));
    }

    public static IReadOnlyList<string> FindGameDirectories(
        IEnumerable<string>? steamRoots = null,
        bool includeLocalFallbacks = true)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queuedLibraries = new Queue<string>();
        var seenLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (includeLocalFallbacks)
        {
            AddCandidate(AppContext.BaseDirectory);
            AddCandidate(Environment.CurrentDirectory);
        }

        foreach (var steamRoot in steamRoots ?? FindSteamRoots()) QueueLibrary(steamRoot);
        while (queuedLibraries.Count > 0)
        {
            var library = queuedLibraries.Dequeue();
            if (!seenLibraries.Add(library)) continue;

            var steamApps = Path.Combine(library, "steamapps");
            var appManifest = Path.Combine(steamApps, $"appmanifest_{PatchInfo.AppId}.acf");
            var installDirectory = ReadVdfValue(appManifest, "installdir");
            var manifestAppId = ReadVdfValue(appManifest, "appid");
            if (manifestAppId?.Equals(PatchInfo.AppId, StringComparison.Ordinal) == true
                && !string.IsNullOrWhiteSpace(installDirectory))
            {
                AddCandidate(Path.Combine(steamApps, "common", installDirectory));
            }

            var libraryFile = Path.Combine(steamApps, "libraryfolders.vdf");
            if (!File.Exists(libraryFile)) continue;

            try
            {
                foreach (var line in File.ReadLines(libraryFile))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        line, "\\\"path\\\"\\s+\\\"(?<path>.+?)\\\"",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (!match.Success) continue;
                    QueueLibrary(match.Groups["path"].Value.Replace("\\\\", "\\"));
                }
            }
            catch
            {
                // A locked or malformed Steam file should not block manual selection.
            }
        }

        if (includeLocalFallbacks)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    AddCandidate(Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "common", "SpiritVale"));
                }
                catch
                {
                    // An unavailable, disconnected, or policy-blocked drive must not prevent startup.
                }
            }
        }

        return candidates.Where(IsGameDirectory).ToArray();

        void AddCandidate(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (seen.Add(fullPath)) candidates.Add(fullPath);
            }
            catch { }
        }

        void QueueLibrary(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!seenLibraries.Contains(fullPath)) queuedLibraries.Enqueue(fullPath);
            }
            catch { }
        }
    }

    private static string? ReadVdfValue(string path, string name)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                File.ReadAllText(path),
                $"\\\"{System.Text.RegularExpressions.Regex.Escape(name)}\\\"\\s+\\\"(?<value>[^\\\"]+)\\\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value.Replace("\\\\", "\\") : null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in new[]
        {
            (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Valve\Steam", "InstallPath"),
            (Registry.LocalMachine, @"Software\Valve\Steam", "InstallPath")
        })
        {
            try
            {
                using var key = pair.Item1.OpenSubKey(pair.Item2);
                if (key?.GetValue(pair.Item3) is string value && Directory.Exists(value)) roots.Add(value);
            }
            catch { }
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFiles)) roots.Add(Path.Combine(programFiles, "Steam"));
        return roots.Where(Directory.Exists);
    }

    public GameInspection Inspect(string? gameDirectory)
    {
        if (!IsGameDirectory(gameDirectory))
        {
            return BlockedInspection(PatchState.NotInstalled, "未找到有效的 SpiritVale Steam 游戏目录。", null, null, null);
        }

        var manifest = LoadManifest(gameDirectory!);
        var pluginExists = File.Exists(Path.Combine(gameDirectory!, PatchInfo.PluginRelativePath));
        var state = manifest is null
            ? (pluginExists ? PatchState.NeedsRepair : PatchState.NotInstalled)
            : ManifestMatches(gameDirectory!, manifest)
                ? PatchState.Installed
                : PatchState.NeedsRepair;

        string gameHash;
        string metadataHash;
        try
        {
            gameHash = _getGameAssemblyHash(Path.Combine(gameDirectory!, "GameAssembly.dll"));
            metadataHash = ComputeHash(GetMetadataPath(gameDirectory!));
        }
        catch (Exception ex)
        {
            return BlockedInspection(state, "无法读取关键游戏文件：" + ex.Message, null, null, null);
        }

        var identity = ReadSteamInstallIdentity(gameDirectory!);
        if (!identity.IsValid)
            return BlockedInspection(state, identity.Reason, identity.BuildId, gameHash, metadataHash);

        var structure = ProbeGameStructure(gameDirectory!);
        if (!structure.IsValid)
            return BlockedInspection(state, structure.Reason, identity.BuildId, gameHash, metadataHash);

        var processBlock = GetGameProcessBlockReason(gameDirectory!);
        if (processBlock != null)
            return BlockedInspection(state, processBlock, identity.BuildId, gameHash, metadataHash);

        var denied = _compatibilityPolicy.DeniedBuilds.FirstOrDefault(rule => RuleMatches(rule, identity.BuildId, gameHash, metadataHash));
        if (denied != null)
        {
            var reason = string.IsNullOrWhiteSpace(denied.Reason) ? "该版本已列入明确不兼容清单。" : denied.Reason;
            return BlockedInspection(state, reason, identity.BuildId, gameHash, metadataHash);
        }

        try
        {
            ValidatePayloadCompatibilityConditions();
        }
        catch (Exception ex)
        {
            return BlockedInspection(state, "补丁载荷未通过离线完整性/IL2CPP 探针：" + ex.Message,
                identity.BuildId, gameHash, metadataHash);
        }

        var verified = _compatibilityPolicy.VerifiedBuilds.Any(rule =>
            RuleMatches(rule, identity.BuildId, gameHash, metadataHash));
        var level = verified ? CompatibilityLevel.Verified : CompatibilityLevel.CompatibleUnverified;
        var status = state switch
        {
            PatchState.Installed => $"已安装简体中文补丁 v{manifest!.PatchVersion}",
            PatchState.NeedsRepair => "检测到不完整安装，可以点击“修复 / 更新”",
            _ => "游戏目录有效，尚未安装汉化补丁"
        };
        status += verified
            ? "；Verified（已实机验证）"
            : "；Compatible-Unverified（结构兼容，尚未实机验证）";
        return new(true, state, level, status, identity.BuildId, gameHash, metadataHash, true,
            state != PatchState.NotInstalled);
    }

    public void Install(string gameDirectory, bool allowCompatibleUnverified = false)
    {
        var inspection = EnsureInstallTarget(gameDirectory, allowCompatibleUnverified);
        using var payload = OpenValidatedPayload(out var payloadSha256);
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        var payloadEntries = ValidatePayloadArchive(archive);
        var payloadPaths = new HashSet<string>(payloadEntries.Select(entry => entry.RelativePath), StringComparer.OrdinalIgnoreCase);
        var oldManifest = LoadManifest(gameDirectory);
        var oldRecords = (oldManifest?.Files ?? []).ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, PatchFileRecord>(StringComparer.OrdinalIgnoreCase);
        var statePath = Path.Combine(gameDirectory, PatchInfo.StateDirectory);
        var backupRoot = Path.Combine(statePath, "backup");
        var originalState = LoadAndValidateOriginalState(gameDirectory, oldManifest);
        var createOriginalState = originalState is null;
        var legacyOriginalState = false;
        if (originalState is null)
        {
            if (oldManifest is null)
            {
                if (Directory.Exists(statePath) && Directory.EnumerateFileSystemEntries(statePath).Any())
                    throw new InvalidOperationException("检测到没有不可变初始清单的残留状态目录，已停止安装以避免覆盖备份。");
                originalState = BuildOriginalState(gameDirectory, payloadEntries);
            }
            else
            {
                originalState = BuildLegacyOriginalState(gameDirectory, oldManifest, backupRoot);
                legacyOriginalState = true;
            }
        }

        var extendedOriginalRecords = ExtendOriginalStateForNewPayloadPaths(
            gameDirectory,
            originalState,
            payloadPaths,
            out originalState);
        var writeOriginalState = createOriginalState || extendedOriginalRecords.Count > 0;

        EnsureWritable(gameDirectory);
        _log("正在读取并校验内嵌补丁文件...");
        var records = new List<PatchFileRecord>();
        var temporaryPaths = new List<string>();
        DisabledConflictResult? conflictResult = null;
        using var transaction = new InstallTransaction(_log);

        try
        {
            transaction.EnsureDirectory(backupRoot);
            if (writeOriginalState)
            {
                var originalManifestPath = Path.Combine(statePath, PatchInfo.OriginalStateManifestName);
                var originalSealPath = Path.Combine(statePath, PatchInfo.OriginalStateSealName);
                transaction.CaptureFile(originalManifestPath);
                transaction.CaptureFile(originalSealPath);
                var backupRecords = createOriginalState && !legacyOriginalState
                    ? originalState.Manifest.Files.Where(record => record.Existed)
                    : extendedOriginalRecords.Where(record => record.Existed);
                foreach (var original in backupRecords)
                {
                    var source = SafeCombine(gameDirectory, original.RelativePath);
                    var backup = SafeCombine(backupRoot, original.RelativePath);
                    transaction.CaptureFile(backup);
                    transaction.EnsureDirectory(Path.GetDirectoryName(backup)!);
                    if (File.Exists(backup))
                        throw new InvalidOperationException($"新增载荷路径存在未登记的初始备份：{original.RelativePath}");
                    File.Copy(source, backup, false);
                    if (new FileInfo(backup).Length != original.Size
                        || !ComputeHash(backup).Equals(original.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new IOException($"创建初始备份时文件发生变化：{original.RelativePath}");
                    _log($"已建立不可变初始备份：{original.RelativePath}");
                }
                File.WriteAllText(originalManifestPath, originalState.Json, new UTF8Encoding(false));
                File.WriteAllText(originalSealPath, originalState.Sha256, Encoding.ASCII);
                if (extendedOriginalRecords.Count > 0)
                    _log($"初始状态已追加 {extendedOriginalRecords.Count} 个新载荷路径；既有备份记录保持不变。");
            }

            foreach (var payloadEntry in payloadEntries)
            {
                var entry = payloadEntry.Entry;
                var relative = payloadEntry.RelativePath;
                var destination = SafeCombine(gameDirectory, relative);
                transaction.CaptureFile(destination);
                transaction.EnsureDirectory(Path.GetDirectoryName(destination)!);

                var original = originalState.Files[relative];
                var hadOldRecord = oldRecords.TryGetValue(relative, out var oldRecord);
                if (hadOldRecord && File.Exists(destination)
                    && !ComputeHash(destination).Equals(oldRecord!.InstalledSha256, StringComparison.OrdinalIgnoreCase))
                {
                    PreserveModifiedFile(destination, transaction);
                }
                else if (!hadOldRecord && File.Exists(destination)
                         && (!original.Existed
                             || !ComputeHash(destination).Equals(original.Sha256, StringComparison.OrdinalIgnoreCase)))
                {
                    PreserveModifiedFile(destination, transaction);
                }

                var record = new PatchFileRecord
                {
                    RelativePath = relative,
                    HadOriginal = original.Existed
                };
                records.Add(record);

                var temporary = destination + ".svpatch.tmp";
                if (File.Exists(temporary))
                    throw new InvalidOperationException($"检测到未完成安装留下的临时文件：{relative}");
                temporaryPaths.Add(temporary);
                using (var source = entry.Open())
                using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(target);
                }
                File.Move(temporary, destination, true);
                temporaryPaths.Remove(temporary);
                record.InstalledSize = new FileInfo(destination).Length;
                record.InstalledSha256 = ComputeHash(destination);
                if (string.Equals(relative, _failAfterPayloadPath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"自检注入安装失败：{relative}");
            }

            foreach (var stale in oldRecords.Values.Where(file => !payloadPaths.Contains(file.RelativePath)))
            {
                if (!originalState.Files.TryGetValue(stale.RelativePath, out var original))
                    throw new InvalidOperationException($"初始状态清单缺少旧版覆盖路径：{stale.RelativePath}");
                RestoreRecord(gameDirectory, backupRoot, stale, original, preserveModified: true, transaction);
            }

            conflictResult = DisableXUnityConflicts(
                gameDirectory,
                oldManifest?.DisabledConflicts ?? originalState.Manifest.DisabledConflicts,
                transaction);

            var manifest = new PatchManifest
            {
                ReleaseChannel = PatchInfo.ReleaseChannel,
                InstalledAtUtc = oldManifest?.InstalledAtUtc ?? DateTime.UtcNow,
                SteamBuildId = inspection.SteamBuildId ?? "",
                GameAssemblySha256 = inspection.GameHash ?? "",
                MetadataSha256 = inspection.MetadataHash ?? "",
                CompatibilityLevel = inspection.CompatibilityLevel.ToString(),
                PayloadSha256 = payloadSha256,
                PayloadPluginSha256 = records.FirstOrDefault(record => record.RelativePath.Equals(
                    PatchInfo.PluginRelativePath, StringComparison.OrdinalIgnoreCase))?.InstalledSha256 ?? "",
                PayloadDictionarySha256 = records.FirstOrDefault(record => record.RelativePath.EndsWith(
                    "\\SpiritVale.RuntimeLocalization\\translations.tsv", StringComparison.OrdinalIgnoreCase))?.InstalledSha256 ?? "",
                PayloadEntityCatalogSha256 = records.FirstOrDefault(record => record.RelativePath.Equals(
                    PatchInfo.EntityCatalogRelativePath, StringComparison.OrdinalIgnoreCase))?.InstalledSha256 ?? "",
                OriginalStateSha256 = originalState.Sha256,
                Files = records,
                DisabledConflicts = conflictResult.Records
            };
            var manifestPath = Path.Combine(statePath, PatchInfo.ActiveManifestName);
            transaction.CaptureFile(manifestPath);
            transaction.CaptureFile(manifestPath + ".tmp");
            File.WriteAllText(manifestPath + ".tmp", JsonSerializer.Serialize(manifest, JsonOptions));
            File.Move(manifestPath + ".tmp", manifestPath, true);
            transaction.Commit();
            _log($"安装完成：写入 {records.Count} 个文件，禁用 {manifest.DisabledConflicts.Count} 个 XUnity 文件；首次备份保持不变。");
        }
        catch (Exception installError)
        {
            foreach (var temporary in temporaryPaths) try { File.Delete(temporary); } catch { }
            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("安装失败，并且事务快照未能完整恢复。", installError, rollbackError);
            }
            throw;
        }
    }

    public IReadOnlyList<RestoreConflict> FindRestoreConflicts(string gameDirectory)
    {
        EnsureGameDirectory(gameDirectory);
        var manifest = LoadManifest(gameDirectory);
        if (manifest is null) return [];
        var conflicts = new List<RestoreConflict>();
        foreach (var record in manifest.Files)
        {
            var destination = SafeCombine(gameDirectory, record.RelativePath);
            if (!File.Exists(destination)) continue;
            var size = new FileInfo(destination).Length;
            var hash = ComputeHash(destination);
            if (size != record.InstalledSize || !hash.Equals(record.InstalledSha256, StringComparison.OrdinalIgnoreCase))
                conflicts.Add(new RestoreConflict(record.RelativePath, $"当前文件已被修改（{size} 字节，SHA-256 {hash}）"));
        }
        foreach (var record in manifest.DisabledConflicts ?? [])
        {
            var original = SafeCombine(gameDirectory, record.OriginalRelativePath);
            var disabled = SafeCombine(gameDirectory, record.DisabledRelativePath);
            if (File.Exists(original) && File.Exists(disabled)
                && !ComputeHash(original).Equals(ComputeHash(disabled), StringComparison.OrdinalIgnoreCase))
                conflicts.Add(new RestoreConflict(record.OriginalRelativePath, "XUnity 原路径存在用户文件，禁用副本将另行保留"));
        }
        return conflicts;
    }

    public void RestoreOriginal(string gameDirectory, bool acceptUserModifiedFiles = false)
    {
        EnsureGameDirectory(gameDirectory);
        EnsureGameClosed(gameDirectory);
        var manifest = LoadManifest(gameDirectory)
            ?? throw new InvalidOperationException("没有找到安装记录，无法安全恢复原版。可使用 Steam 的“验证游戏文件完整性”恢复原版。");
        if (manifest.Files is not { Count: > 0 })
            throw new InvalidOperationException("安装记录不完整，无法安全恢复原版。请使用 Steam 的“验证游戏文件完整性”恢复原版。");
        var statePath = Path.Combine(gameDirectory, PatchInfo.StateDirectory);
        var backupRoot = Path.Combine(statePath, "backup");
        var originalState = LoadAndValidateOriginalState(gameDirectory, manifest)
                            ?? throw new InvalidOperationException(
                                "不可变初始状态清单缺失，已停止恢复且未修改游戏文件。请使用 Steam 的“验证游戏文件完整性”。");
        foreach (var record in manifest.Files)
        {
            if (!originalState.Files.ContainsKey(record.RelativePath))
                throw new InvalidOperationException(
                    $"不可变初始状态清单缺少活动载荷路径：{record.RelativePath}。已停止恢复且未修改游戏文件。");
        }
        var conflicts = FindRestoreConflicts(gameDirectory);
        if (conflicts.Count > 0 && !acceptUserModifiedFiles)
            throw new InvalidOperationException(
                "检测到安装后被用户修改的文件，必须先显示冲突列表并取得确认，恢复操作尚未开始。\r\n\r\n"
                + string.Join("\r\n", conflicts.Select(conflict => "• " + conflict.RelativePath)));

        using var transaction = new InstallTransaction(_log, "恢复原版");
        try
        {
            foreach (var record in manifest.Files.OrderByDescending(file => file.RelativePath.Count(c => c == '\\')))
            {
                RestoreRecord(
                    gameDirectory,
                    backupRoot,
                    record,
                    originalState.Files[record.RelativePath],
                    preserveModified: true,
                    transaction);
                InjectRestoreFailure(record.RelativePath);
            }

            RestoreDisabledConflicts(gameDirectory, manifest.DisabledConflicts ?? [], transaction);

            var manifestPath = Path.Combine(statePath, PatchInfo.ActiveManifestName);
            var manifestTemporaryPath = manifestPath + ".tmp";
            transaction.CaptureFile(manifestPath);
            transaction.CaptureFile(manifestTemporaryPath);
            if (File.Exists(manifestTemporaryPath)) File.Delete(manifestTemporaryPath);
            File.Delete(manifestPath);
            InjectRestoreFailure(PatchInfo.StateDirectory);

            DeleteEmptyDirectories(gameDirectory, manifest.Files.Select(file => Path.GetDirectoryName(file.RelativePath)));
            transaction.Commit();
            _log("恢复原版完成；不可变初始备份已保留，可供以后重新安装时复用。");
        }
        catch (Exception restoreError)
        {
            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("恢复原版失败，并且事务快照未能完整恢复。", restoreError, rollbackError);
            }
            throw;
        }
    }

    private void RestoreRecord(
        string gameDirectory,
        string backupRoot,
        PatchFileRecord record,
        OriginalStateFileRecord original,
        bool preserveModified,
        InstallTransaction? transaction = null)
    {
        var destination = SafeCombine(gameDirectory, record.RelativePath);
        var backup = SafeCombine(backupRoot, record.RelativePath);
        transaction?.CaptureFile(destination);
        if (File.Exists(destination) && preserveModified
            && !ComputeHash(destination).Equals(record.InstalledSha256, StringComparison.OrdinalIgnoreCase))
        {
            PreserveModifiedFile(destination, transaction);
        }

        if (original.Existed)
        {
            if (!File.Exists(backup))
                throw new InvalidOperationException($"无法恢复缺失的原文件备份：{record.RelativePath}");
            if (transaction is null) Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            else transaction.EnsureDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(backup, destination, true);
            _log($"已恢复原文件：{record.RelativePath}");
        }
        else if (File.Exists(destination))
        {
            File.Delete(destination);
        }
    }

    private static bool ManifestMatches(string gameDirectory, PatchManifest manifest)
    {
        if (!string.Equals(manifest.PatchVersion, PatchInfo.Version, StringComparison.OrdinalIgnoreCase)) return false;
        if (manifest.Files is not { Count: > 0 }) return false;

        try
        {
            return manifest.Files.All(record =>
                File.Exists(SafeCombine(gameDirectory, record.RelativePath))
                && new FileInfo(SafeCombine(gameDirectory, record.RelativePath)).Length == record.InstalledSize
                && ComputeHash(SafeCombine(gameDirectory, record.RelativePath))
                    .Equals(record.InstalledSha256, StringComparison.OrdinalIgnoreCase))
                && (manifest.DisabledConflicts ?? []).All(record =>
                    !File.Exists(SafeCombine(gameDirectory, record.OriginalRelativePath))
                    && File.Exists(SafeCombine(gameDirectory, record.DisabledRelativePath))
                    && ComputeHash(SafeCombine(gameDirectory, record.DisabledRelativePath))
                        .Equals(record.Sha256, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private void PreserveModifiedFile(string path, InstallTransaction? transaction = null)
    {
        var preserved = FindAvailablePath(path + ".user-modified");
        transaction?.CaptureFile(path);
        transaction?.CaptureFile(preserved);
        File.Move(path, preserved);
        _log($"保留了用户修改的文件：{Path.GetFileName(preserved)}");
    }

    private DisabledConflictResult DisableXUnityConflicts(
        string gameDirectory,
        IEnumerable<DisabledConflictRecord> previousRecords,
        InstallTransaction transaction)
    {
        var records = new List<DisabledConflictRecord>();
        var knownOriginals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var oldRecord in previousRecords)
        {
            var original = SafeCombine(gameDirectory, oldRecord.OriginalRelativePath);
            var disabled = SafeCombine(gameDirectory, oldRecord.DisabledRelativePath);
            if (File.Exists(original) && File.Exists(disabled))
                throw new InvalidOperationException($"XUnity 文件同时存在启用和禁用副本，请手动处理后重试：{oldRecord.OriginalRelativePath}");

            if (!File.Exists(disabled) && File.Exists(original))
            {
                transaction.CaptureFile(original);
                transaction.CaptureFile(disabled);
                transaction.EnsureDirectory(Path.GetDirectoryName(disabled)!);
                File.Move(original, disabled);
                _log($"已重新禁用 XUnity：{oldRecord.OriginalRelativePath}");
            }

            if (!File.Exists(disabled)) continue;
            records.Add(new DisabledConflictRecord
            {
                OriginalRelativePath = oldRecord.OriginalRelativePath,
                DisabledRelativePath = oldRecord.DisabledRelativePath,
                Size = new FileInfo(disabled).Length,
                Sha256 = ComputeHash(disabled)
            });
            knownOriginals.Add(oldRecord.OriginalRelativePath);
        }

        var pluginRoot = Path.Combine(gameDirectory, "BepInEx", "plugins");
        if (Directory.Exists(pluginRoot))
        {
            foreach (var file in Directory.EnumerateFiles(pluginRoot, "*.dll", SearchOption.AllDirectories)
                         .Where(file => IsXUnityPluginFile(pluginRoot, file)).ToArray())
            {
                var originalRelative = Path.GetRelativePath(gameDirectory, file).Replace('/', '\\');
                if (!knownOriginals.Add(originalRelative)) continue;
                var disabledRelative = originalRelative + PatchInfo.XUnityDisableSuffix;
                var disabled = SafeCombine(gameDirectory, disabledRelative);
                if (File.Exists(disabled))
                    throw new InvalidOperationException($"XUnity 禁用目标已存在，请手动处理后重试：{disabledRelative}");

                var record = new DisabledConflictRecord
                {
                    OriginalRelativePath = originalRelative,
                    DisabledRelativePath = disabledRelative,
                    Size = new FileInfo(file).Length,
                    Sha256 = ComputeHash(file)
                };
                transaction.CaptureFile(file);
                transaction.CaptureFile(disabled);
                transaction.EnsureDirectory(Path.GetDirectoryName(disabled)!);
                File.Move(file, disabled);
                records.Add(record);
                _log($"已安全禁用冲突的 XUnity 文件：{originalRelative}");
            }
        }

        return new DisabledConflictResult(records);
    }

    private static bool IsXUnityPluginFile(string pluginRoot, string path)
    {
        var relative = Path.GetRelativePath(pluginRoot, path).Replace('/', '\\');
        var segments = relative.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
                   segment.StartsWith("XUnity.AutoTranslator", StringComparison.OrdinalIgnoreCase)
                   || segment.StartsWith("XUnity.ResourceRedirector", StringComparison.OrdinalIgnoreCase))
               || Path.GetFileName(path).StartsWith("XUnity.AutoTranslator", StringComparison.OrdinalIgnoreCase)
               || Path.GetFileName(path).StartsWith("XUnity.ResourceRedirector", StringComparison.OrdinalIgnoreCase);
    }

    private void RestoreDisabledConflicts(
        string gameDirectory,
        IEnumerable<DisabledConflictRecord> records,
        InstallTransaction transaction)
    {
        foreach (var record in records.Reverse())
        {
            var original = SafeCombine(gameDirectory, record.OriginalRelativePath);
            var disabled = SafeCombine(gameDirectory, record.DisabledRelativePath);
            if (!File.Exists(disabled))
            {
                if (!File.Exists(original)) _log($"XUnity 禁用文件已不存在：{record.DisabledRelativePath}");
                continue;
            }

            transaction.CaptureFile(original);
            transaction.CaptureFile(disabled);
            transaction.EnsureDirectory(Path.GetDirectoryName(original)!);
            if (!File.Exists(original))
            {
                File.Move(disabled, original);
                _log($"已恢复 XUnity 文件：{record.OriginalRelativePath}");
                InjectRestoreFailure(record.OriginalRelativePath);
                continue;
            }

            if (ComputeHash(original).Equals(ComputeHash(disabled), StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(disabled);
                _log($"XUnity 文件已由用户恢复，已移除重复禁用副本：{record.OriginalRelativePath}");
                InjectRestoreFailure(record.OriginalRelativePath);
                continue;
            }

            var preserved = FindAvailablePath(original + ".pre-spiritvale-zh");
            transaction.CaptureFile(preserved);
            File.Move(disabled, preserved);
            _log($"XUnity 原路径已有其他文件，旧副本已保留为：{Path.GetFileName(preserved)}");
            InjectRestoreFailure(record.OriginalRelativePath);
        }
    }

    private void InjectRestoreFailure(string relativePath)
    {
        if (string.Equals(relativePath, _failAfterRestorePath, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"自检注入恢复失败：{relativePath}");
    }

    private static string FindAvailablePath(string preferred)
    {
        if (!File.Exists(preferred) && !Directory.Exists(preferred)) return preferred;
        for (var index = 1; ; index++)
        {
            var candidate = preferred + "." + index;
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    private static void DeleteEmptyDirectories(string root, IEnumerable<string?> relativeDirectories)
    {
        foreach (var relative in relativeDirectories.Where(path => !string.IsNullOrEmpty(path)).Distinct()
                     .OrderByDescending(path => path!.Count(c => c == '\\')))
        {
            var directory = SafeCombine(root, relative!);
            try { if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory); }
            catch { }
        }
    }

    private MemoryStream OpenValidatedPayload(out string payloadSha256)
    {
        using var source = _openPayload();
        var payload = new MemoryStream();
        source.CopyTo(payload);
        payload.Position = 0;
        payloadSha256 = ComputeHash(payload);
        payload.Position = 0;
        if (!IsSha256(_expectedPayloadSha256)
            || !payloadSha256.Equals(_expectedPayloadSha256, StringComparison.OrdinalIgnoreCase))
        {
            payload.Dispose();
            throw new InvalidDataException(
                $"内嵌 Payload.zip 校验失败，已在写入游戏目录前停止。期望 {_expectedPayloadSha256}，实际 {payloadSha256}。");
        }
        return payload;
    }

    private static List<PayloadArchiveEntry> ValidatePayloadArchive(ZipArchive archive)
    {
        var entries = new List<PayloadArchiveEntry>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
        {
            var relative = NormalizeArchivePath(entry.FullName);
            if (!paths.Add(relative)) throw new InvalidDataException($"补丁包包含重复路径：{relative}");
            if (IsForbiddenPayloadPath(relative)) throw new InvalidDataException($"补丁包包含禁止文件：{relative}");
            entries.Add(new PayloadArchiveEntry(entry, relative));
        }

        foreach (var required in new[]
                 {
                     ".doorstop_version",
                     "doorstop_config.ini",
                     "winhttp.dll",
                     "BepInEx\\config\\BepInEx.cfg",
                     "BepInEx\\config\\local.spiritvale.runtime-localization.cfg",
                     "BepInEx\\core\\BepInEx.Unity.IL2CPP.dll",
                     "BepInEx\\core\\Il2CppInterop.Generator.dll",
                     "BepInEx\\core\\Cpp2IL.Core.dll",
                     "BepInEx\\core\\LibCpp2IL.dll",
                     PatchInfo.PluginRelativePath,
                     "BepInEx\\plugins\\SpiritVale.RuntimeLocalization\\translations.tsv",
                     PatchInfo.EntityCatalogRelativePath
                 })
        {
            if (!paths.Contains(required)) throw new InvalidDataException($"补丁包缺少必要文件：{required}");
        }

        return entries;
    }

    private void ValidatePayloadCompatibilityConditions()
    {
        using var payload = OpenValidatedPayload(out _);
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        var entries = ValidatePayloadArchive(archive);
        var doorstop = ReadArchiveText(entries, "doorstop_config.ini");
        if (!doorstop.Contains("enabled = true", StringComparison.OrdinalIgnoreCase)
            || !doorstop.Contains("target_assembly = BepInEx\\core\\BepInEx.Unity.IL2CPP.dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Doorstop 未配置为加载 BepInEx IL2CPP。 ");

        var config = ReadArchiveText(entries, "BepInEx\\config\\BepInEx.cfg");
        if (!System.Text.RegularExpressions.Regex.IsMatch(config,
                @"(?im)^\s*UpdateInteropAssemblies\s*=\s*true\s*$"))
            throw new InvalidDataException("BepInEx 未启用 IL2CPP interop 自动生成。 ");
        if (!System.Text.RegularExpressions.Regex.IsMatch(config,
                @"(?im)^\s*UnityBaseLibrariesSource\s*=\s*$"))
            throw new InvalidDataException("BepInEx 未锁定为离线 Unity 基础库模式。 ");
        if (!entries.Any(entry => entry.RelativePath.StartsWith("BepInEx\\unity-libs\\", StringComparison.OrdinalIgnoreCase)
                                  && entry.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("载荷缺少离线 Unity 基础库。 ");

        var localizationConfig = ReadArchiveText(entries, "BepInEx\\config\\local.spiritvale.runtime-localization.cfg");
        if (!System.Text.RegularExpressions.Regex.IsMatch(localizationConfig,
                @"(?im)^\s*CompactSurfaceMode\s*=\s*EnglishToggle\s*$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(localizationConfig,
                @"(?im)^\s*TemporaryEnglishKey\s*=\s*Tab\s*$"))
            throw new InvalidDataException("补丁未启用 Tab 英文实体名称切换。 ");
    }

    private static string ReadArchiveText(IEnumerable<PayloadArchiveEntry> entries, string relativePath)
    {
        var entry = entries.Single(item => item.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
        using var stream = entry.Entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static bool IsForbiddenPayloadPath(string relative)
    {
        var normalized = relative.Replace('/', '\\');
        var extension = Path.GetExtension(normalized);
        return normalized.StartsWith("BepInEx\\interop\\", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("BepInEx\\cache\\", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("BepInEx\\plugins\\XUnity.AutoTranslator\\", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("BepInEx\\plugins\\XUnity.ResourceRedirector\\", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("untranslated-runtime", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("ErrorLog", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("LogOutput", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);
    }

    private static OriginalStateSnapshot BuildOriginalState(
        string gameDirectory,
        IEnumerable<PayloadArchiveEntry> payloadEntries)
    {
        var manifest = new OriginalStateManifest();
        foreach (var payloadEntry in payloadEntries.OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var destination = SafeCombine(gameDirectory, payloadEntry.RelativePath);
            if (Directory.Exists(destination))
                throw new InvalidOperationException($"补丁目标应为文件，但当前存在目录：{payloadEntry.RelativePath}");
            var existed = File.Exists(destination);
            manifest.Files.Add(new OriginalStateFileRecord
            {
                RelativePath = payloadEntry.RelativePath,
                Existed = existed,
                Size = existed ? new FileInfo(destination).Length : 0,
                Sha256 = existed ? ComputeHash(destination) : ""
            });
        }

        manifest.DisabledConflicts = DiscoverXUnityConflicts(gameDirectory);
        return CreateOriginalStateSnapshot(manifest);
    }

    private static OriginalStateSnapshot BuildLegacyOriginalState(
        string gameDirectory,
        PatchManifest activeManifest,
        string backupRoot)
    {
        var manifest = new OriginalStateManifest
        {
            CreatedAtUtc = activeManifest.InstalledAtUtc,
            DisabledConflicts = activeManifest.DisabledConflicts ?? []
        };
        foreach (var record in activeManifest.Files)
        {
            var backup = SafeCombine(backupRoot, record.RelativePath);
            if (record.HadOriginal && !File.Exists(backup))
                throw new InvalidOperationException(
                    $"旧版安装记录的原文件备份缺失，无法建立不可变初始状态：{record.RelativePath}。请使用 Steam 验证游戏文件。");
            manifest.Files.Add(new OriginalStateFileRecord
            {
                RelativePath = record.RelativePath,
                Existed = record.HadOriginal,
                Size = record.HadOriginal ? new FileInfo(backup).Length : 0,
                Sha256 = record.HadOriginal ? ComputeHash(backup) : ""
            });
        }
        return CreateOriginalStateSnapshot(manifest);
    }

    private static List<DisabledConflictRecord> DiscoverXUnityConflicts(string gameDirectory)
    {
        var records = new List<DisabledConflictRecord>();
        var pluginRoot = Path.Combine(gameDirectory, "BepInEx", "plugins");
        if (!Directory.Exists(pluginRoot)) return records;
        foreach (var file in Directory.EnumerateFiles(pluginRoot, "*.dll", SearchOption.AllDirectories)
                     .Where(file => IsXUnityPluginFile(pluginRoot, file)).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var originalRelative = Path.GetRelativePath(gameDirectory, file).Replace('/', '\\');
            var disabledRelative = originalRelative + PatchInfo.XUnityDisableSuffix;
            if (File.Exists(SafeCombine(gameDirectory, disabledRelative)))
                throw new InvalidOperationException($"XUnity 文件同时存在启用和禁用副本：{originalRelative}");
            records.Add(new DisabledConflictRecord
            {
                OriginalRelativePath = originalRelative,
                DisabledRelativePath = disabledRelative,
                Size = new FileInfo(file).Length,
                Sha256 = ComputeHash(file)
            });
        }
        return records;
    }

    private static OriginalStateSnapshot CreateOriginalStateSnapshot(OriginalStateManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        var files = manifest.Files.ToDictionary(record => record.RelativePath, StringComparer.OrdinalIgnoreCase);
        return new OriginalStateSnapshot(manifest, files, sha256, json);
    }

    private static List<OriginalStateFileRecord> ExtendOriginalStateForNewPayloadPaths(
        string gameDirectory,
        OriginalStateSnapshot snapshot,
        IEnumerable<string> requiredPaths,
        out OriginalStateSnapshot extendedSnapshot)
    {
        var additions = new List<OriginalStateFileRecord>();
        foreach (var relativePath in requiredPaths
                     .Where(path => !snapshot.Files.ContainsKey(path))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var destination = SafeCombine(gameDirectory, relativePath);
            if (Directory.Exists(destination))
                throw new InvalidOperationException($"补丁目标应为文件，但当前存在目录：{relativePath}");
            var existed = File.Exists(destination);
            var record = new OriginalStateFileRecord
            {
                RelativePath = relativePath,
                Existed = existed,
                Size = existed ? new FileInfo(destination).Length : 0,
                Sha256 = existed ? ComputeHash(destination) : ""
            };
            snapshot.Manifest.Files.Add(record);
            additions.Add(record);
        }

        extendedSnapshot = additions.Count == 0
            ? snapshot
            : CreateOriginalStateSnapshot(snapshot.Manifest);
        return additions;
    }

    private static OriginalStateSnapshot? LoadAndValidateOriginalState(
        string gameDirectory,
        PatchManifest? activeManifest)
    {
        var statePath = Path.Combine(gameDirectory, PatchInfo.StateDirectory);
        var manifestPath = Path.Combine(statePath, PatchInfo.OriginalStateManifestName);
        var sealPath = Path.Combine(statePath, PatchInfo.OriginalStateSealName);
        if (!File.Exists(manifestPath) && !File.Exists(sealPath)) return null;
        if (!File.Exists(manifestPath) || !File.Exists(sealPath))
            throw new InvalidOperationException("不可变初始状态清单或校验封印缺失，已停止操作。请使用 Steam 验证游戏文件。");

        var bytes = File.ReadAllBytes(manifestPath);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        var sealedHash = File.ReadAllText(sealPath).Trim();
        if (!IsSha256(sealedHash) || !sha256.Equals(sealedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("不可变初始状态清单校验失败，已停止操作。请使用 Steam 验证游戏文件。");
        if (!string.IsNullOrWhiteSpace(activeManifest?.OriginalStateSha256)
            && !sha256.Equals(activeManifest.OriginalStateSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("活动安装清单与初始状态清单不匹配，已停止操作。请使用 Steam 验证游戏文件。");

        OriginalStateManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<OriginalStateManifest>(bytes, JsonOptions)
                       ?? throw new InvalidDataException();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("不可变初始状态清单无法读取，已停止操作。请使用 Steam 验证游戏文件。", ex);
        }
        if (manifest.SchemaVersion != 1 || manifest.Files is not { Count: > 0 })
            throw new InvalidOperationException("不可变初始状态清单格式无效，已停止操作。请使用 Steam 验证游戏文件。");

        var files = new Dictionary<string, OriginalStateFileRecord>(StringComparer.OrdinalIgnoreCase);
        var backupRoot = Path.Combine(statePath, "backup");
        foreach (var record in manifest.Files)
        {
            var normalized = NormalizeArchivePath(record.RelativePath);
            if (!normalized.Equals(record.RelativePath, StringComparison.Ordinal)
                || !files.TryAdd(record.RelativePath, record))
                throw new InvalidOperationException("不可变初始状态清单包含重复或非规范路径，已停止操作。");
            var backup = SafeCombine(backupRoot, record.RelativePath);
            if (!record.Existed)
            {
                if (record.Size != 0 || !string.IsNullOrEmpty(record.Sha256) || File.Exists(backup))
                    throw new InvalidOperationException($"初始不存在文件的备份状态异常：{record.RelativePath}");
                continue;
            }
            if (!IsSha256(record.Sha256) || record.Size < 0 || !File.Exists(backup))
                throw new InvalidOperationException($"初始备份缺失或清单无效：{record.RelativePath}。请使用 Steam 验证游戏文件。");
            var actualSize = new FileInfo(backup).Length;
            var actualHash = ComputeHash(backup);
            if (actualSize != record.Size || !actualHash.Equals(record.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"初始备份大小或 SHA-256 不匹配：{record.RelativePath}。请使用 Steam 验证游戏文件。");
        }
        var conflictPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var conflict in manifest.DisabledConflicts ?? [])
        {
            var original = NormalizeArchivePath(conflict.OriginalRelativePath);
            var disabled = NormalizeArchivePath(conflict.DisabledRelativePath);
            if (!original.Equals(conflict.OriginalRelativePath, StringComparison.Ordinal)
                || !disabled.Equals(conflict.DisabledRelativePath, StringComparison.Ordinal)
                || !disabled.Equals(original + PatchInfo.XUnityDisableSuffix, StringComparison.OrdinalIgnoreCase)
                || !conflictPaths.Add(original)
                || conflict.Size < 0
                || !IsSha256(conflict.Sha256))
                throw new InvalidOperationException("不可变初始状态清单包含无效的 XUnity 初始记录，已停止操作。");
        }
        return new OriginalStateSnapshot(manifest, files, sha256, Encoding.UTF8.GetString(bytes));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => Uri.IsHexDigit(character));

    private static PatchManifest? LoadManifest(string gameDirectory)
    {
        var path = Path.Combine(gameDirectory, PatchInfo.StateDirectory, PatchInfo.ActiveManifestName);
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(path), JsonOptions); }
        catch { return null; }
    }

    private static Stream OpenResource(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"安装器缺少内嵌资源：{name}");

    private static string ReadEmbeddedText(string name)
    {
        using var stream = OpenResource(name);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static CompatibilityPolicy LoadCompatibilityPolicy()
    {
        CompatibilityPolicy policy;
        try
        {
            policy = JsonSerializer.Deserialize<CompatibilityPolicy>(
                         ReadEmbeddedText("SpiritValePatch.CompatibilityPolicy.json"), JsonOptions)
                     ?? throw new InvalidDataException();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("离线兼容清单无法读取。", ex);
        }
        if (policy.SchemaVersion != 1 || !policy.SteamAppId.Equals(PatchInfo.AppId, StringComparison.Ordinal)
            || policy.VerifiedBuilds is null || policy.DeniedBuilds is null)
            throw new InvalidDataException("离线兼容清单格式无效。 ");
        foreach (var rule in policy.VerifiedBuilds.Concat(policy.DeniedBuilds))
        {
            if (!IsSha256(rule.GameAssemblySha256)
                || (!string.IsNullOrEmpty(rule.MetadataSha256) && !IsSha256(rule.MetadataSha256)))
                throw new InvalidDataException("离线兼容清单包含无效 SHA-256。 ");
        }
        return policy;
    }

    private static string NormalizeArchivePath(string path)
    {
        var normalized = path.Replace('/', '\\').TrimStart('\\');
        if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized) || normalized.Split('\\').Contains(".."))
            throw new InvalidDataException($"补丁包包含不安全的路径：{path}");
        return normalized;
    }

    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"路径超出游戏目录：{relative}");
        return fullPath;
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return ComputeHash(stream);
    }

    private static string ComputeHash(Stream stream) => Convert.ToHexString(SHA256.HashData(stream));

    private static string GetMetadataPath(string gameDirectory) => Path.Combine(
        gameDirectory, "SpiritVale_Data", "il2cpp_data", "Metadata", "global-metadata.dat");

    private static GameInspection BlockedInspection(
        PatchState state,
        string reason,
        string? buildId,
        string? gameHash,
        string? metadataHash) =>
        new(gameHash != null, state, CompatibilityLevel.Blocked, "Blocked：" + reason,
            buildId, gameHash, metadataHash, false, false);

    private static bool RuleMatches(GameBuildRule rule, string buildId, string gameHash, string metadataHash)
    {
        if (!string.IsNullOrWhiteSpace(rule.SteamBuildId)
            && !rule.SteamBuildId.Equals(buildId, StringComparison.Ordinal)) return false;
        if (!rule.GameAssemblySha256.Equals(gameHash, StringComparison.OrdinalIgnoreCase)) return false;
        return string.IsNullOrWhiteSpace(rule.MetadataSha256)
               || rule.MetadataSha256.Equals(metadataHash, StringComparison.OrdinalIgnoreCase);
    }

    private static SteamInstallIdentity ReadSteamInstallIdentity(string gameDirectory)
    {
        try
        {
            var fullRoot = Path.GetFullPath(gameDirectory).TrimEnd(Path.DirectorySeparatorChar);
            var common = Directory.GetParent(fullRoot);
            var steamApps = common?.Parent;
            if (common == null || steamApps == null
                || !common.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
                || !steamApps.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                return new(false, "", "目录不在 Steam 的 steamapps\\common 下，无法验证 App ID。 ");

            var manifestPath = Path.Combine(steamApps.FullName, $"appmanifest_{PatchInfo.AppId}.acf");
            if (!File.Exists(manifestPath))
                return new(false, "", $"缺少 Steam App {PatchInfo.AppId} 清单。 ");
            var appId = ReadVdfValue(manifestPath, "appid");
            var buildId = ReadVdfValue(manifestPath, "buildid") ?? "";
            var installDir = ReadVdfValue(manifestPath, "installdir");
            if (!PatchInfo.AppId.Equals(appId, StringComparison.Ordinal))
                return new(false, buildId, $"Steam App ID 不匹配（期望 {PatchInfo.AppId}）。 ");
            if (string.IsNullOrWhiteSpace(buildId))
                return new(false, "", "Steam 清单缺少 Build ID。 ");
            if (string.IsNullOrWhiteSpace(installDir)
                || !Path.GetFileName(fullRoot).Equals(installDir, StringComparison.OrdinalIgnoreCase))
                return new(false, buildId, "Steam 清单的安装目录与所选目录不匹配。 ");
            return new(true, buildId, "");
        }
        catch (Exception ex)
        {
            return new(false, "", "无法验证 Steam App ID：" + ex.Message);
        }
    }

    private static StructureProbeResult ProbeGameStructure(string gameDirectory)
    {
        var metadataPath = GetMetadataPath(gameDirectory);
        if (!File.Exists(metadataPath))
            return new(false, "缺少 IL2CPP global-metadata.dat。 ");
        var exeProbe = ProbePeFile(Path.Combine(gameDirectory, "SpiritVale.exe"), 256 * 1024);
        if (!exeProbe.IsValid) return exeProbe;
        var assemblyProbe = ProbePeFile(Path.Combine(gameDirectory, "GameAssembly.dll"), 4 * 1024 * 1024);
        if (!assemblyProbe.IsValid) return assemblyProbe;

        try
        {
            using var stream = File.OpenRead(metadataPath);
            if (stream.Length < 1024 * 1024) return new(false, "global-metadata.dat 尺寸异常。 ");
            Span<byte> header = stackalloc byte[8];
            if (stream.Read(header) != header.Length) return new(false, "global-metadata.dat 头部不完整。 ");
            var magic = BitConverter.ToUInt32(header[..4]);
            var version = BitConverter.ToInt32(header[4..]);
            if (magic != 0xFAB11BAF || version is < 24 or > 40)
                return new(false, $"global-metadata.dat 探针异常（magic 0x{magic:X8}, version {version}）。 ");
        }
        catch (Exception ex)
        {
            return new(false, "无法探测 global-metadata.dat：" + ex.Message);
        }
        return new(true, "");
    }

    private static StructureProbeResult ProbePeFile(string path, long minimumSize)
    {
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length < minimumSize) return new(false, $"{Path.GetFileName(path)} 尺寸异常。 ");
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadUInt16() != 0x5A4D) return new(false, $"{Path.GetFileName(path)} 缺少 MZ 头。 ");
            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset < 0x40 || peOffset > stream.Length - 24) return new(false, $"{Path.GetFileName(path)} PE 偏移异常。 ");
            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x8664)
                return new(false, $"{Path.GetFileName(path)} 不是有效的 x64 PE 文件。 ");
            return new(true, "");
        }
        catch (Exception ex)
        {
            return new(false, $"无法探测 {Path.GetFileName(path)}：{ex.Message}");
        }
    }

    private string? GetGameProcessBlockReason(string gameDirectory)
    {
        if (!_checkGameProcess) return null;
        IReadOnlyList<SpiritValeProcessProbe> processes;
        try { processes = _getSpiritValeProcesses(); }
        catch { return "无法确认 SpiritVale 是否正在运行；为保护游戏文件，已停止操作。 "; }

        var selectedExecutable = Path.GetFullPath(Path.Combine(gameDirectory, "SpiritVale.exe"));
        foreach (var process in processes)
        {
            if (!process.PathReadSucceeded || string.IsNullOrWhiteSpace(process.ExecutablePath))
                return $"无法读取 SpiritVale 进程 {process.ProcessId} 的完整路径；请退出所有同名进程后重试。 ";
            try
            {
                if (Path.GetFullPath(process.ExecutablePath).Equals(selectedExecutable, StringComparison.OrdinalIgnoreCase))
                    return "所选目录中的 SpiritVale 正在运行，请先退出该游戏后重试。 ";
            }
            catch
            {
                return $"无法确认 SpiritVale 进程 {process.ProcessId} 的完整路径；已保守阻止操作。 ";
            }
        }
        return null;
    }

    private GameInspection EnsureInstallTarget(string gameDirectory, bool allowCompatibleUnverified)
    {
        var inspection = Inspect(gameDirectory);
        if (inspection.CompatibilityLevel == CompatibilityLevel.Blocked)
            throw new InvalidOperationException("安装已被安全探针阻止：" + inspection.Summary);
        if (inspection.CompatibilityLevel == CompatibilityLevel.CompatibleUnverified && !allowCompatibleUnverified)
            throw new InvalidOperationException(
                "当前版本为 Compatible-Unverified。必须在界面明确勾选“允许兼容尝试”后才能安装；该选择不会把版本标记为 Verified。 ");
        return inspection;
    }

    private static void EnsureWritable(string gameDirectory)
    {
        var probe = Path.Combine(gameDirectory, $".svpatch-write-test-{Guid.NewGuid():N}");
        try { File.WriteAllText(probe, "test"); }
        catch (UnauthorizedAccessException) { throw new UnauthorizedAccessException("游戏目录不可写。请将安装器移到可写位置，或右键“以管理员身份运行”。"); }
        finally { try { File.Delete(probe); } catch { } }
    }

    private static void EnsureGameDirectory(string gameDirectory)
    {
        if (!IsGameDirectory(gameDirectory)) throw new DirectoryNotFoundException("请选择包含 SpiritVale.exe 的游戏目录。");
    }

    private void EnsureGameClosed(string gameDirectory)
    {
        var reason = GetGameProcessBlockReason(gameDirectory);
        if (reason != null) throw new InvalidOperationException(reason);
    }

    private static IReadOnlyList<SpiritValeProcessProbe> GetSpiritValeProcessProbes()
    {
        var probes = new List<SpiritValeProcessProbe>();
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("SpiritVale");
        }
        catch
        {
            return [new SpiritValeProcessProbe(-1, null, false)];
        }

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    if (process.HasExited) continue;
                    var executablePath = process.MainModule?.FileName;
                    probes.Add(new SpiritValeProcessProbe(
                        process.Id,
                        executablePath,
                        !string.IsNullOrWhiteSpace(executablePath)));
                }
                catch
                {
                    try
                    {
                        if (process.HasExited) continue;
                    }
                    catch
                    {
                        // If process state itself is unreadable, conservatively treat it as running.
                    }
                    probes.Add(new SpiritValeProcessProbe(SafeGetProcessId(process), null, false));
                }
            }
        }

        return probes;
    }

    private static int SafeGetProcessId(Process process)
    {
        try { return process.Id; }
        catch { return -1; }
    }
}

internal static class SelfTest
{
    private sealed record TreeSnapshot(string[] Directories, Dictionary<string, string> FileHashes);

    public static int Run(string? requestedRoot)
    {
        var testRoot = requestedRoot ?? Path.Combine(Path.GetTempPath(), "SpiritValePatchSelfTest-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(testRoot, "Steam", "steamapps", "common", "SpiritVale");
        var logPath = Path.Combine(testRoot, "self-test.log");
        try
        {
            if (Directory.Exists(testRoot) && Directory.EnumerateFileSystemEntries(testRoot).Any())
                throw new InvalidOperationException("自检目录必须为空，以免覆盖已有文件。");

            Directory.CreateDirectory(Path.Combine(root, "SpiritVale_Data", "il2cpp_data", "Metadata"));
            WriteFakePe(Path.Combine(root, "SpiritVale.exe"), 512 * 1024);
            WriteFakePe(Path.Combine(root, "GameAssembly.dll"), 4 * 1024 * 1024);
            WriteFakeMetadata(Path.Combine(root, "SpiritVale_Data", "il2cpp_data", "Metadata", "global-metadata.dat"));
            WriteSteamManifest(root, PatchInfo.AppId, "24266225");
            File.WriteAllText(Path.Combine(root, "winhttp.dll"), "original-proxy");

            var autoTranslator = Path.Combine(root, "BepInEx", "plugins", "XUnity.AutoTranslator", "ExIni.dll");
            var resourceRedirector = Path.Combine(root, "BepInEx", "plugins", "XUnity.ResourceRedirector", "XUnity.ResourceRedirector.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(autoTranslator)!);
            Directory.CreateDirectory(Path.GetDirectoryName(resourceRedirector)!);
            File.WriteAllText(autoTranslator, "original-auto-translator");
            File.WriteAllText(resourceRedirector, "original-resource-redirector");

            Require(PatchService.IsGameDirectory(root), "完整测试游戏基线没有通过目录验证。");
            var lookalikeRoot = Path.Combine(root, "SpiritVale-lookalike");
            Directory.CreateDirectory(lookalikeRoot);
            File.WriteAllText(Path.Combine(lookalikeRoot, "SpiritVale.exe"), "fake-game");
            Require(!PatchService.IsGameDirectory(lookalikeRoot), "只有文件夹名和可执行文件的伪目录被错误接受。");
            Directory.Delete(lookalikeRoot, true);

            var discoveryRoot = Path.Combine(root, "steam-discovery");
            var primarySteamRoot = Path.Combine(discoveryRoot, "primary");
            var libraryRoot = Path.Combine(discoveryRoot, "library");
            var discoveredGame = Path.Combine(libraryRoot, "steamapps", "common", "SpiritVale");
            Directory.CreateDirectory(Path.Combine(primarySteamRoot, "steamapps"));
            Directory.CreateDirectory(Path.Combine(discoveredGame, "SpiritVale_Data"));
            File.WriteAllText(Path.Combine(discoveredGame, "SpiritVale.exe"), "fake-game");
            File.WriteAllText(Path.Combine(discoveredGame, "GameAssembly.dll"), "fake-assembly");
            var escapedLibraryRoot = libraryRoot.Replace("\\", "\\\\");
            File.WriteAllText(
                Path.Combine(primarySteamRoot, "steamapps", "libraryfolders.vdf"),
                $"\"libraryfolders\"\r\n{{\r\n  \"1\"\r\n  {{\r\n    \"path\" \"{escapedLibraryRoot}\"\r\n  }}\r\n}}\r\n");
            File.WriteAllText(
                Path.Combine(libraryRoot, "steamapps", $"appmanifest_{PatchInfo.AppId}.acf"),
                $"\"AppState\"\r\n{{\r\n  \"appid\" \"{PatchInfo.AppId}\"\r\n  \"buildid\" \"24266225\"\r\n  \"installdir\" \"SpiritVale\"\r\n}}\r\n");
            var discoveredDirectories = PatchService.FindGameDirectories([primarySteamRoot], includeLocalFallbacks: false);
            Require(discoveredDirectories.Count == 1
                    && Path.GetFullPath(discoveredDirectories[0]).Equals(Path.GetFullPath(discoveredGame), StringComparison.OrdinalIgnoreCase),
                "通过 libraryfolders.vdf 和 App 3767850 manifest 的自动识别失败。");
            Directory.Delete(discoveryRoot, true);

            var messages = new List<string>();
            messages.Add("自检通过：目标目录必须同时包含 SpiritVale.exe、GameAssembly.dll 和 SpiritVale_Data。");
            messages.Add("自检通过：自动识别遍历 Steam libraryfolders.vdf，并按 App 3767850 manifest 定位游戏；指定目录验证可用。");
            var selectedExecutable = Path.GetFullPath(Path.Combine(root, "SpiritVale.exe"));
            var demoExecutable = Path.GetFullPath(Path.Combine(root, "..", "SpiritValeDemo", "SpiritVale.exe"));
            Func<IReadOnlyList<SpiritValeProcessProbe>> demoProcessProbe =
                () => [new SpiritValeProcessProbe(103, demoExecutable, true)];
            Func<string, string> supportedGameHashProbe = _ => "D4442C72CC52C02A749CEFBCDFFC5502639E773C7C38783647E544BAC6A51E06";
            Func<string, string> unknownGameHashProbe = _ => new string('0', 64);

            var beforeUnknownInstall = CaptureTree(root);
            var unknownInstallService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: unknownGameHashProbe);
            var unknownInstallBlocked = false;
            try
            {
                unknownInstallService.Install(root);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Compatible-Unverified", StringComparison.Ordinal))
            {
                unknownInstallBlocked = true;
            }
            Require(unknownInstallBlocked, "未知游戏哈希没有在服务层阻止安装。");
            Require(TreeMatches(root, beforeUnknownInstall), "未知游戏哈希被拒绝后仍写入了游戏目录。");
            Require(unknownInstallService.Inspect(root).CompatibilityLevel == CompatibilityLevel.CompatibleUnverified,
                "未知但结构正常的版本没有分类为 Compatible-Unverified。");
            messages.Add("自检通过：未知 GameAssembly 哈希允许显式兼容尝试，但未确认时保持零写入。");

            var appManifestPath = Path.Combine(testRoot, "Steam", "steamapps", $"appmanifest_{PatchInfo.AppId}.acf");
            var appManifestBytes = File.ReadAllBytes(appManifestPath);
            WriteSteamManifest(root, "9999999", "24266225");
            Require(unknownInstallService.Inspect(root).CompatibilityLevel == CompatibilityLevel.Blocked,
                "错误 Steam App ID 没有分类为 Blocked。");
            File.WriteAllBytes(appManifestPath, appManifestBytes);

            var metadataPath = Path.Combine(root, "SpiritVale_Data", "il2cpp_data", "Metadata", "global-metadata.dat");
            var metadataHeader = File.ReadAllBytes(metadataPath)[..8];
            using (var metadata = new FileStream(metadataPath, FileMode.Open, FileAccess.Write, FileShare.None))
                metadata.Write(new byte[8]);
            Require(unknownInstallService.Inspect(root).CompatibilityLevel == CompatibilityLevel.Blocked,
                "异常 IL2CPP metadata 没有分类为 Blocked。");
            using (var metadata = new FileStream(metadataPath, FileMode.Open, FileAccess.Write, FileShare.None))
                metadata.Write(metadataHeader);

            var deniedService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: _ => new string('F', 64),
                compatibilityPolicy: new CompatibilityPolicy
                {
                    SchemaVersion = 1,
                    SteamAppId = PatchInfo.AppId,
                    DeniedBuilds =
                    [
                        new GameBuildRule
                        {
                            GameAssemblySha256 = new string('F', 64),
                            Reason = "Self-test denylist rule."
                        }
                    ]
                });
            Require(deniedService.Inspect(root).CompatibilityLevel == CompatibilityLevel.Blocked,
                "denylist GameAssembly 哈希没有分类为 Blocked。");
            Require(TreeMatches(root, beforeUnknownInstall), "Blocked 分类探针修改了游戏目录。");
            messages.Add("自检通过：错误 App ID、异常 metadata 与 denylist 哈希均为 Blocked 且零写入。");

            var compatibleRoot = Path.Combine(testRoot, "UnknownSteam", "steamapps", "common", "SpiritVale");
            CreateGameFixture(compatibleRoot, "99999999");
            var compatibleService = new PatchService(
                messages.Add,
                checkGameProcess: false,
                getGameAssemblyHash: unknownGameHashProbe);
            compatibleService.Install(compatibleRoot, allowCompatibleUnverified: true);
            var compatibleManifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(Path.Combine(
                compatibleRoot, PatchInfo.StateDirectory, PatchInfo.ActiveManifestName)));
            Require(compatibleManifest?.CompatibilityLevel == CompatibilityLevel.CompatibleUnverified.ToString()
                    && compatibleManifest.SteamBuildId == "99999999",
                "兼容尝试清单没有记录实际 Build 与 Compatible-Unverified 级别。");
            compatibleService.RestoreOriginal(compatibleRoot);
            Directory.Delete(Path.Combine(testRoot, "UnknownSteam"), true);
            messages.Add("自检通过：明确同意后未知版本可兼容安装并恢复，且不会冒充 Verified。");

            var migrationRoot = Path.Combine(testRoot, "MigrationSteam", "steamapps", "common", "SpiritVale");
            CreateGameFixture(migrationRoot, "24266225");
            var migrationService = new PatchService(
                messages.Add,
                checkGameProcess: false,
                getGameAssemblyHash: supportedGameHashProbe);
            migrationService.Install(migrationRoot);
            var migrationState = Path.Combine(migrationRoot, PatchInfo.StateDirectory);
            var migrationManifestPath = Path.Combine(migrationState, PatchInfo.ActiveManifestName);
            var legacyManifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(migrationManifestPath))
                                 ?? throw new Exception("无法建立清单迁移自检。 ");
            legacyManifest.SchemaVersion = 2;
            legacyManifest.SteamBuildId = "";
            legacyManifest.MetadataSha256 = "";
            legacyManifest.CompatibilityLevel = "";
            legacyManifest.PayloadPluginSha256 = "";
            legacyManifest.PayloadDictionarySha256 = "";
            legacyManifest.PayloadEntityCatalogSha256 = "";
            legacyManifest.Files.RemoveAll(record => record.RelativePath.Equals(
                PatchInfo.EntityCatalogRelativePath, StringComparison.OrdinalIgnoreCase));
            File.WriteAllText(migrationManifestPath, JsonSerializer.Serialize(legacyManifest));
            File.Delete(Path.Combine(migrationState, PatchInfo.OriginalStateManifestName));
            File.Delete(Path.Combine(migrationState, PatchInfo.OriginalStateSealName));
            var legacyCatalogPath = Path.Combine(migrationRoot, PatchInfo.EntityCatalogRelativePath);
            File.WriteAllText(legacyCatalogPath, "legacy-user-catalog", Encoding.UTF8);
            migrationService.Install(migrationRoot);
            var migratedManifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(migrationManifestPath));
            var migratedOriginal = JsonSerializer.Deserialize<OriginalStateManifest>(File.ReadAllText(Path.Combine(
                migrationState, PatchInfo.OriginalStateManifestName)));
            Require(migratedManifest?.SchemaVersion == 4
                    && migratedManifest.CompatibilityLevel == CompatibilityLevel.Verified.ToString()
                    && migratedOriginal?.Files.Single(record => record.RelativePath.Equals(
                        PatchInfo.EntityCatalogRelativePath, StringComparison.OrdinalIgnoreCase)).Existed == true
                    && File.Exists(Path.Combine(migrationState, PatchInfo.OriginalStateManifestName))
                    && File.Exists(Path.Combine(migrationState, PatchInfo.OriginalStateSealName)),
                "schema v2 安装清单没有迁移到 v4 与不可变初始状态。 ");
            migrationService.RestoreOriginal(migrationRoot);
            Require(File.ReadAllText(legacyCatalogPath, Encoding.UTF8) == "legacy-user-catalog",
                "旧版清单新增载荷路径的用户原文件没有在恢复时还原。 ");
            Directory.Delete(Path.Combine(testRoot, "MigrationSteam"), true);
            messages.Add("自检通过：旧 schema v2 活动清单迁移到 v4，追加新载荷路径并保留其用户原文件。");

            var sealedMigrationRoot = Path.Combine(testRoot, "SealedMigrationSteam", "steamapps", "common", "SpiritVale");
            CreateGameFixture(sealedMigrationRoot, "24266225");
            var sealedMigrationService = new PatchService(
                messages.Add,
                checkGameProcess: false,
                getGameAssemblyHash: supportedGameHashProbe);
            sealedMigrationService.Install(sealedMigrationRoot);
            var sealedState = Path.Combine(sealedMigrationRoot, PatchInfo.StateDirectory);
            var sealedManifestPath = Path.Combine(sealedState, PatchInfo.ActiveManifestName);
            var sealedOriginalPath = Path.Combine(sealedState, PatchInfo.OriginalStateManifestName);
            var sealedSealPath = Path.Combine(sealedState, PatchInfo.OriginalStateSealName);
            var sealedManifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(sealedManifestPath))
                                 ?? throw new Exception("无法建立封印清单扩展自检。 ");
            var sealedOriginal = JsonSerializer.Deserialize<OriginalStateManifest>(File.ReadAllText(sealedOriginalPath))
                                 ?? throw new Exception("无法建立封印初始状态扩展自检。 ");
            sealedManifest.SchemaVersion = 3;
            sealedManifest.Files.RemoveAll(record => record.RelativePath.Equals(
                PatchInfo.EntityCatalogRelativePath, StringComparison.OrdinalIgnoreCase));
            sealedManifest.PayloadEntityCatalogSha256 = "";
            sealedOriginal.Files.RemoveAll(record => record.RelativePath.Equals(
                PatchInfo.EntityCatalogRelativePath, StringComparison.OrdinalIgnoreCase));
            var sealedOriginalJson = JsonSerializer.Serialize(sealedOriginal);
            var sealedOriginalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sealedOriginalJson)));
            sealedManifest.OriginalStateSha256 = sealedOriginalHash;
            File.WriteAllText(sealedOriginalPath, sealedOriginalJson, new UTF8Encoding(false));
            File.WriteAllText(sealedSealPath, sealedOriginalHash, Encoding.ASCII);
            File.WriteAllText(sealedManifestPath, JsonSerializer.Serialize(sealedManifest));
            File.Delete(Path.Combine(sealedMigrationRoot, PatchInfo.EntityCatalogRelativePath));
            var existingBackupFiles = CaptureTree(Path.Combine(sealedState, "backup"));
            sealedMigrationService.Install(sealedMigrationRoot);
            var extendedSealedOriginal = JsonSerializer.Deserialize<OriginalStateManifest>(File.ReadAllText(sealedOriginalPath));
            Require(extendedSealedOriginal?.Files.Single(record => record.RelativePath.Equals(
                        PatchInfo.EntityCatalogRelativePath, StringComparison.OrdinalIgnoreCase)).Existed == false
                    && TreeMatches(Path.Combine(sealedState, "backup"), existingBackupFiles),
                "封印初始状态没有以只追加方式登记原本不存在的新载荷路径。 ");
            sealedMigrationService.RestoreOriginal(sealedMigrationRoot);
            Require(!File.Exists(Path.Combine(sealedMigrationRoot, PatchInfo.EntityCatalogRelativePath)),
                "恢复时没有移除由新版载荷首次引入的实体目录。 ");
            Directory.Delete(Path.Combine(testRoot, "SealedMigrationSteam"), true);
            messages.Add("自检通过：已封印的旧初始状态可事务化追加新载荷路径，既有备份逐字节不变。");

            var substitutedPayload = Encoding.UTF8.GetBytes("substituted-payload");
            var beforeSubstitutedPayload = CaptureTree(root);
            var substitutedPayloadService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: supportedGameHashProbe,
                openPayload: () => new MemoryStream(substitutedPayload, writable: false));
            var substitutedPayloadBlocked = false;
            try
            {
                substitutedPayloadService.Install(root);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Payload.zip 校验失败", StringComparison.Ordinal))
            {
                substitutedPayloadBlocked = true;
            }
            Require(substitutedPayloadBlocked, "替换后的 Payload 没有被 SHA-256 门禁拒绝。");
            Require(TreeMatches(root, beforeSubstitutedPayload), "替换 Payload 被拒绝后仍写入了目标目录。");

            var corruptedZip = Encoding.UTF8.GetBytes("not-a-zip");
            var corruptedZipHash = Convert.ToHexString(SHA256.HashData(corruptedZip));
            var beforeCorruptedZip = CaptureTree(root);
            var corruptedZipService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: supportedGameHashProbe,
                openPayload: () => new MemoryStream(corruptedZip, writable: false),
                expectedPayloadSha256: corruptedZipHash);
            var corruptedZipBlocked = false;
            try
            {
                corruptedZipService.Install(root);
            }
            catch (InvalidOperationException)
            {
                corruptedZipBlocked = true;
            }
            Require(corruptedZipBlocked, "结构损坏的 Payload 没有在目标写入前被拒绝。");
            Require(TreeMatches(root, beforeCorruptedZip), "损坏 Payload 被拒绝后仍写入了目标目录。");
            messages.Add("自检通过：替换或损坏 Payload 均在目标零写入状态下被拒绝。");

            var exactPathService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: () => [new SpiritValeProcessProbe(101, selectedExecutable, true)]);
            var exactPathBlocked = false;
            try
            {
                exactPathService.Install(root);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("正在运行", StringComparison.Ordinal))
            {
                exactPathBlocked = true;
            }
            Require(exactPathBlocked, "所选游戏目录的精确进程路径没有阻止安装。");
            Require(!Directory.Exists(Path.Combine(root, PatchInfo.StateDirectory)), "进程保护触发后仍写入了安装状态。");
            Require(File.ReadAllText(Path.Combine(root, "winhttp.dll")) == "original-proxy", "进程保护触发后仍修改了游戏文件。");
            messages.Add("自检通过：所选目录的精确 SpiritVale.exe 路径会阻止安装。");

            var unreadablePathService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: () => [new SpiritValeProcessProbe(102, null, false)]);
            var unreadablePathBlocked = false;
            try
            {
                unreadablePathService.Install(root);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("无法读取", StringComparison.Ordinal))
            {
                unreadablePathBlocked = true;
            }
            Require(unreadablePathBlocked, "路径不可读的同名进程没有被保守拦截。");
            Require(!Directory.Exists(Path.Combine(root, PatchInfo.StateDirectory)), "保守进程保护触发后仍写入了安装状态。");
            messages.Add("自检通过：同名进程路径不可读时会保守阻止安装。");

            var failedFirstInstallRoot = Path.Combine(testRoot, "FailedSteam", "steamapps", "common", "SpiritVale");
            CreateGameFixture(failedFirstInstallRoot, "24266225");
            var failedFirstInstallSnapshot = CaptureTree(failedFirstInstallRoot);
            var failedFirstInstallService = new PatchService(
                messages.Add,
                failAfterPayloadPath: "BepInEx\\plugins\\SpiritVale.RuntimeLocalization\\translations.tsv",
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: supportedGameHashProbe);
            var failedFirstInstallObserved = false;
            try
            {
                failedFirstInstallService.Install(failedFirstInstallRoot);
            }
            catch (IOException ex) when (ex.Message.StartsWith("自检注入安装失败", StringComparison.Ordinal))
            {
                failedFirstInstallObserved = true;
            }
            Require(failedFirstInstallObserved, "没有触发预期的首次安装失败。");
            Require(TreeMatches(failedFirstInstallRoot, failedFirstInstallSnapshot), "首次安装失败后没有完整回滚目标目录。");
            Directory.Delete(Path.Combine(testRoot, "FailedSteam"), true);
            messages.Add("自检通过：首次安装失败完整回滚，未留下 original-state、备份或覆盖文件。");

            var service = new PatchService(
                messages.Add,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: supportedGameHashProbe);
            service.Install(root);
            messages.Add("自检通过：异目录同名 SpiritVale 进程不会误拦截所选目录。");

            var plugin = Path.Combine(root, PatchInfo.PluginRelativePath);
            var translations = Path.Combine(root, "BepInEx", "plugins", "SpiritVale.RuntimeLocalization", "translations.tsv");
            var entityCatalog = Path.Combine(root, PatchInfo.EntityCatalogRelativePath);
            Require(File.Exists(plugin), "插件没有安装。");
            Require(File.Exists(translations), "词典没有安装。");
            Require(File.Exists(entityCatalog), "双语实体目录没有安装。");
            Require(File.ReadAllText(Path.Combine(root, "winhttp.dll")) != "original-proxy", "载荷没有覆盖测试文件。");
            Require(service.Inspect(root).PatchState == PatchState.Installed, "首次安装后的哈希状态不正确。");
            Require(service.Inspect(root).CompatibilityLevel == CompatibilityLevel.Verified, "自检的 Verified 哈希探针未生效。");
            var activeManifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(
                Path.Combine(root, PatchInfo.StateDirectory, PatchInfo.ActiveManifestName)));
            Require(activeManifest?.ReleaseChannel == PatchInfo.ReleaseChannel, "活动安装清单没有标记 compatibility release channel。");
            Require(activeManifest?.SchemaVersion == 4
                    && activeManifest.SteamBuildId == "24266225"
                    && activeManifest.CompatibilityLevel == CompatibilityLevel.Verified.ToString()
                    && IsManifestHash(activeManifest.GameAssemblySha256)
                    && IsManifestHash(activeManifest.MetadataSha256)
                    && IsManifestHash(activeManifest.PayloadSha256)
                    && IsManifestHash(activeManifest.PayloadPluginSha256)
                    && IsManifestHash(activeManifest.PayloadDictionarySha256)
                    && IsManifestHash(activeManifest.PayloadEntityCatalogSha256)
                    && activeManifest.DefaultEntityNameMode == "Chinese"
                    && activeManifest.DefaultCompactSurfaceMode == "EnglishToggle"
                    && activeManifest.DefaultTemporaryEnglishKey == "Tab",
                "活动安装清单没有记录 Build、兼容级别与完整载荷哈希。");
            Require(!File.Exists(autoTranslator) && File.Exists(autoTranslator + PatchInfo.XUnityDisableSuffix), "AutoTranslator 没有被禁用。");
            Require(!File.Exists(resourceRedirector) && File.Exists(resourceRedirector + PatchInfo.XUnityDisableSuffix), "ResourceRedirector 没有被禁用。");

            var statePath = Path.Combine(root, PatchInfo.StateDirectory);
            var originalStatePath = Path.Combine(statePath, PatchInfo.OriginalStateManifestName);
            var originalSealPath = Path.Combine(statePath, PatchInfo.OriginalStateSealName);
            var backupRoot = Path.Combine(statePath, "backup");
            var originalManifestBytes = File.ReadAllBytes(originalStatePath);
            var originalSealBytes = File.ReadAllBytes(originalSealPath);
            var originalBackupSnapshot = CaptureTree(backupRoot);
            var originalManifest = JsonSerializer.Deserialize<OriginalStateManifest>(originalManifestBytes)
                                   ?? throw new Exception("无法读取不可变初始状态清单。");
            var originalProxyRecord = originalManifest.Files.Single(record =>
                record.RelativePath.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase));
            var originalProxyBackupPath = Path.Combine(backupRoot, "winhttp.dll");
            var originalProxyBackupBytes = File.ReadAllBytes(originalProxyBackupPath);
            Require(originalProxyRecord.Existed, "初始状态清单没有记录原代理文件存在。");
            Require(originalProxyRecord.Size == Encoding.UTF8.GetByteCount("original-proxy"), "初始状态清单的原文件大小不正确。");
            Require(originalProxyRecord.Sha256.Equals(ComputeFileHash(originalProxyBackupPath), StringComparison.OrdinalIgnoreCase),
                "初始状态清单的原文件 SHA-256 不正确。");

            service.Install(root);
            Require(File.ReadAllBytes(originalStatePath).SequenceEqual(originalManifestBytes), "重复安装改写了不可变初始状态清单。");
            Require(File.ReadAllBytes(originalSealPath).SequenceEqual(originalSealBytes), "重复安装改写了初始状态校验封印。");
            Require(TreeMatches(backupRoot, originalBackupSnapshot), "重复安装改写了首次安装备份。");
            messages.Add("自检通过：重复安装后 original-state 清单、封印和首次备份逐字节不变。");

            WriteSteamManifest(root, PatchInfo.AppId, "24270694");
            var crossVersionService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: unknownGameHashProbe);
            crossVersionService.Install(root, allowCompatibleUnverified: true);
            var crossVersionManifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(Path.Combine(
                statePath, PatchInfo.ActiveManifestName)));
            Require(crossVersionManifest?.SteamBuildId == "24270694"
                    && crossVersionManifest.CompatibilityLevel == CompatibilityLevel.CompatibleUnverified.ToString(),
                "跨版本更新没有记录实际 Build 与兼容级别。");
            Require(File.ReadAllBytes(originalStatePath).SequenceEqual(originalManifestBytes)
                    && File.ReadAllBytes(originalSealPath).SequenceEqual(originalSealBytes)
                    && TreeMatches(backupRoot, originalBackupSnapshot),
                "跨版本更新改写了不可变首次备份。");
            WriteSteamManifest(root, PatchInfo.AppId, "24266225");
            service.Install(root);
            messages.Add("自检通过：Verified 到 Compatible-Unverified 的跨版本更新保留首次备份并记录实际版本。");

            var coreAssembly = Path.Combine(root, "BepInEx", "core", "0Harmony.dll");
            File.WriteAllText(plugin, "user-plugin-before-failed-upgrade");
            File.WriteAllText(translations, "user-translation-change");
            Require(service.Inspect(root).PatchState == PatchState.NeedsRepair, "用户修改后没有识别为需要修复。");

            var manifestPath = Path.Combine(statePath, PatchInfo.ActiveManifestName);
            var oldManifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(manifestPath))
                ?? throw new Exception("无法读取首次安装清单。");
            oldManifest.PatchVersion = "1.2.1";
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(oldManifest, new JsonSerializerOptions { WriteIndented = true }));

            var pluginBeforeFailure = File.ReadAllBytes(plugin);
            var translationsBeforeFailure = File.ReadAllBytes(translations);
            var coreBeforeFailure = File.ReadAllBytes(coreAssembly);
            var manifestBeforeFailure = File.ReadAllBytes(manifestPath);
            var autoTranslatorDisabledBeforeFailure = File.ReadAllBytes(autoTranslator + PatchInfo.XUnityDisableSuffix);
            var resourceRedirectorDisabledBeforeFailure = File.ReadAllBytes(resourceRedirector + PatchInfo.XUnityDisableSuffix);
            var failAfterTranslations = Path.GetRelativePath(root, translations).Replace('/', '\\');
            var failingService = new PatchService(
                messages.Add,
                checkGameProcess: false,
                failAfterPayloadPath: failAfterTranslations,
                getGameAssemblyHash: supportedGameHashProbe);
            var injectedFailureObserved = false;
            try
            {
                failingService.Install(root);
            }
            catch (IOException ex) when (ex.Message.StartsWith("自检注入安装失败", StringComparison.Ordinal))
            {
                injectedFailureObserved = true;
            }

            Require(injectedFailureObserved, "没有触发预期的中途安装失败。");
            Require(File.ReadAllBytes(plugin).SequenceEqual(pluginBeforeFailure), "失败回滚没有恢复升级前的用户插件。");
            Require(File.ReadAllBytes(translations).SequenceEqual(translationsBeforeFailure), "失败回滚没有恢复升级前的用户词典。");
            Require(File.ReadAllBytes(coreAssembly).SequenceEqual(coreBeforeFailure), "失败回滚没有恢复旧版核心文件。");
            Require(File.ReadAllBytes(manifestPath).SequenceEqual(manifestBeforeFailure), "失败回滚没有恢复旧版安装清单。");
            Require(File.ReadAllBytes(autoTranslator + PatchInfo.XUnityDisableSuffix).SequenceEqual(autoTranslatorDisabledBeforeFailure),
                "失败回滚改变了已禁用的 AutoTranslator 文件。");
            Require(File.ReadAllBytes(resourceRedirector + PatchInfo.XUnityDisableSuffix).SequenceEqual(resourceRedirectorDisabledBeforeFailure),
                "失败回滚改变了已禁用的 ResourceRedirector 文件。");
            Require(!FindPreservedFiles(plugin).Any() && !FindPreservedFiles(translations).Any(),
                "失败回滚留下了本次事务创建的用户修改副本。");
            Require(!File.Exists(autoTranslator) && !File.Exists(resourceRedirector), "失败回滚意外重新启用了 XUnity。");

            service.Install(root);

            Require(service.Inspect(root).PatchState == PatchState.Installed, "升级后的哈希状态不正确。");
            Require(File.ReadAllBytes(originalStatePath).SequenceEqual(originalManifestBytes), "覆盖升级改写了不可变初始状态清单。");
            Require(File.ReadAllBytes(originalSealPath).SequenceEqual(originalSealBytes), "覆盖升级改写了初始状态封印。");
            Require(TreeMatches(backupRoot, originalBackupSnapshot), "覆盖升级改写了首次安装备份。");
            Require(FindPreservedFiles(plugin).Any(path => File.ReadAllText(path) == "user-plugin-before-failed-upgrade"),
                "成功升级没有保留用户修改的旧插件。");
            Require(FindPreservedFiles(translations).Any(path => File.ReadAllText(path) == "user-translation-change"),
                "升级没有保留用户修改的词典。");

            File.WriteAllText(Path.Combine(root, "winhttp.dll"), "user-proxy-change");
            File.WriteAllText(plugin, "user-plugin-change-after-upgrade");
            Require(service.Inspect(root).PatchState == PatchState.NeedsRepair, "恢复前的用户修改没有被哈希检测到。");
            var unknownVersionService = new PatchService(
                messages.Add,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: unknownGameHashProbe);
            Require(unknownVersionService.Inspect(root).CompatibilityLevel == CompatibilityLevel.CompatibleUnverified,
                "未知游戏哈希恢复自测的前提不成立。");

            var reportedConflicts = unknownVersionService.FindRestoreConflicts(root);
            Require(reportedConflicts.Any(conflict => conflict.RelativePath.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase)),
                "恢复前没有报告用户修改的代理文件冲突。");
            Require(reportedConflicts.Any(conflict => conflict.RelativePath.Equals(PatchInfo.PluginRelativePath, StringComparison.OrdinalIgnoreCase)),
                "恢复前没有报告用户修改的插件冲突。");
            var beforeUnconfirmedRestore = CaptureTree(root);
            var unconfirmedRestoreBlocked = false;
            try
            {
                unknownVersionService.RestoreOriginal(root);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("必须先显示冲突列表并取得确认", StringComparison.Ordinal))
            {
                unconfirmedRestoreBlocked = true;
            }
            Require(unconfirmedRestoreBlocked, "存在用户修改冲突时，服务层没有要求显式确认。");
            Require(TreeMatches(root, beforeUnconfirmedRestore), "未确认冲突的恢复请求修改了目标目录。");
            messages.Add("自检通过：用户修改冲突会被列出，未确认时恢复保持零写入。");

            void RequireDamagedBackupRejected(string caseName)
            {
                var beforeRejectedRestore = CaptureTree(root);
                var rejected = false;
                try
                {
                    unknownVersionService.RestoreOriginal(root, acceptUserModifiedFiles: true);
                }
                catch (InvalidOperationException ex) when (
                    ex.Message.Contains("初始备份", StringComparison.Ordinal)
                    || ex.Message.Contains("初始状态", StringComparison.Ordinal))
                {
                    rejected = true;
                }
                Require(rejected, $"{caseName}没有在恢复写入前被拒绝。");
                Require(TreeMatches(root, beforeRejectedRestore), $"{caseName}被拒绝后仍修改了目标目录。");
            }

            File.Delete(originalProxyBackupPath);
            RequireDamagedBackupRejected("缺失初始备份");
            File.WriteAllBytes(originalProxyBackupPath, originalProxyBackupBytes);

            File.WriteAllBytes(originalProxyBackupPath, originalProxyBackupBytes[..Math.Max(1, originalProxyBackupBytes.Length / 2)]);
            RequireDamagedBackupRejected("截断初始备份");
            File.WriteAllBytes(originalProxyBackupPath, originalProxyBackupBytes);

            var hashMismatchBackup = originalProxyBackupBytes.ToArray();
            hashMismatchBackup[0] ^= 0x5A;
            File.WriteAllBytes(originalProxyBackupPath, hashMismatchBackup);
            RequireDamagedBackupRejected("哈希不匹配初始备份");
            File.WriteAllBytes(originalProxyBackupPath, originalProxyBackupBytes);
            messages.Add("自检通过：缺失、截断和同尺寸哈希不匹配的初始备份均在恢复零写入状态下被拒绝。");

            var beforeProcessBlockedRestore = CaptureTree(root);
            var exactRestoreBlocked = false;
            try
            {
                exactPathService.RestoreOriginal(root);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("正在运行", StringComparison.Ordinal))
            {
                exactRestoreBlocked = true;
            }
            Require(exactRestoreBlocked, "所选游戏目录的精确进程路径没有阻止恢复原版。");
            Require(TreeMatches(root, beforeProcessBlockedRestore), "恢复进程保护触发后仍修改了游戏目录。");

            var unreadableRestoreBlocked = false;
            try
            {
                unreadablePathService.RestoreOriginal(root);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("无法读取", StringComparison.Ordinal))
            {
                unreadableRestoreBlocked = true;
            }
            Require(unreadableRestoreBlocked, "路径不可读的同名进程没有保守阻止恢复原版。");
            Require(TreeMatches(root, beforeProcessBlockedRestore), "保守恢复进程保护触发后仍修改了游戏目录。");
            messages.Add("自检通过：恢复原版同样执行精确路径匹配与不可读路径保守拦截。");

            var beforeFailedRestore = CaptureTree(root);
            var failingRestoreService = new PatchService(
                messages.Add,
                failAfterRestorePath: PatchInfo.StateDirectory,
                getSpiritValeProcesses: demoProcessProbe,
                getGameAssemblyHash: unknownGameHashProbe);
            var injectedRestoreFailureObserved = false;
            try
            {
                failingRestoreService.RestoreOriginal(root, acceptUserModifiedFiles: true);
            }
            catch (IOException ex) when (ex.Message.StartsWith("自检注入恢复失败", StringComparison.Ordinal))
            {
                injectedRestoreFailureObserved = true;
            }
            Require(injectedRestoreFailureObserved, "没有触发预期的中途恢复失败。");
            Require(TreeMatches(root, beforeFailedRestore), "恢复失败回滚后目录内容与操作前不一致。");
            Require(!File.Exists(autoTranslator) && File.Exists(autoTranslator + PatchInfo.XUnityDisableSuffix),
                "恢复失败回滚没有重新还原 AutoTranslator 的禁用状态。");
            Require(!File.Exists(resourceRedirector) && File.Exists(resourceRedirector + PatchInfo.XUnityDisableSuffix),
                "恢复失败回滚没有重新还原 ResourceRedirector 的禁用状态。");
            messages.Add("自检通过：未知游戏哈希下的恢复失败完整回滚了载荷、XUnity 和状态目录。");

            unknownVersionService.RestoreOriginal(root, acceptUserModifiedFiles: true);

            Require(File.ReadAllText(Path.Combine(root, "winhttp.dll")) == "original-proxy", "原代理文件没有恢复。");
            Require(FindPreservedFiles(Path.Combine(root, "winhttp.dll")).Any(path => File.ReadAllText(path) == "user-proxy-change"),
                "恢复原版没有保留用户修改的代理文件。");
            Require(!File.Exists(plugin), "恢复原版没有移除补丁插件。");
            Require(FindPreservedFiles(plugin).Any(path => File.ReadAllText(path) == "user-plugin-change-after-upgrade"),
                "恢复原版没有保留用户修改的插件文件。");
            Require(File.Exists(autoTranslator) && File.ReadAllText(autoTranslator) == "original-auto-translator",
                "AutoTranslator 没有在恢复原版时还原。");
            Require(File.Exists(resourceRedirector) && File.ReadAllText(resourceRedirector) == "original-resource-redirector",
                "ResourceRedirector 没有在恢复原版时还原。");
            Require(!File.Exists(manifestPath), "恢复原版后仍残留活动安装清单。");
            Require(File.ReadAllBytes(originalStatePath).SequenceEqual(originalManifestBytes), "恢复原版改写了不可变初始状态清单。");
            Require(File.ReadAllBytes(originalSealPath).SequenceEqual(originalSealBytes), "恢复原版改写了初始状态封印。");
            Require(TreeMatches(backupRoot, originalBackupSnapshot), "恢复原版改写了首次安装备份。");
            Require(unknownVersionService.Inspect(root).PatchState == PatchState.NotInstalled, "恢复原版后状态不是未安装。");
            messages.Add("自检通过：未知游戏哈希恢复原版成功，保留用户修改、恢复 XUnity 并保留首次备份。");

            service.Install(root);
            Require(service.Inspect(root).PatchState == PatchState.Installed, "保留备份后的再次安装失败。");
            Require(File.ReadAllBytes(originalStatePath).SequenceEqual(originalManifestBytes), "恢复后再次安装改写了不可变初始状态清单。");
            Require(TreeMatches(backupRoot, originalBackupSnapshot), "恢复后再次安装改写了首次备份。");
            service.RestoreOriginal(root);
            Require(!File.Exists(manifestPath), "再次安装后的最终恢复仍残留活动清单。");
            messages.Add("自检通过：保留的首次备份可供再次安装和再次恢复，内容始终不变。");

            File.WriteAllLines(logPath, ["SELF-TEST PASSED", .. messages]);
            return 0;
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(testRoot);
            File.WriteAllText(logPath, "SELF-TEST FAILED\r\n" + ex);
            return 1;
        }
    }

    private static void WriteFakePe(string path, long length)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.SetLength(length);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        stream.Position = 0;
        writer.Write((ushort)0x5A4D);
        stream.Position = 0x3C;
        writer.Write(0x80);
        stream.Position = 0x80;
        writer.Write(0x00004550u);
        writer.Write((ushort)0x8664);
    }

    private static void WriteFakeMetadata(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.SetLength(1024 * 1024 + 16);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(0xFAB11BAFu);
        writer.Write(31);
    }

    private static void WriteSteamManifest(string gameRoot, string appId, string buildId)
    {
        var steamApps = Directory.GetParent(Directory.GetParent(gameRoot)!.FullName)!.FullName;
        Directory.CreateDirectory(steamApps);
        File.WriteAllText(Path.Combine(steamApps, $"appmanifest_{PatchInfo.AppId}.acf"),
            $"\"AppState\"\r\n{{\r\n  \"appid\" \"{appId}\"\r\n  \"buildid\" \"{buildId}\"\r\n  \"installdir\" \"SpiritVale\"\r\n}}\r\n");
    }

    private static void CreateGameFixture(string gameRoot, string buildId)
    {
        Directory.CreateDirectory(Path.Combine(gameRoot, "SpiritVale_Data", "il2cpp_data", "Metadata"));
        WriteFakePe(Path.Combine(gameRoot, "SpiritVale.exe"), 512 * 1024);
        WriteFakePe(Path.Combine(gameRoot, "GameAssembly.dll"), 4 * 1024 * 1024);
        WriteFakeMetadata(Path.Combine(gameRoot, "SpiritVale_Data", "il2cpp_data", "Metadata", "global-metadata.dat"));
        WriteSteamManifest(gameRoot, PatchInfo.AppId, buildId);
        File.WriteAllText(Path.Combine(gameRoot, "winhttp.dll"), "original-proxy");
    }

    private static bool IsManifestHash(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static TreeSnapshot CaptureTree(string root)
    {
        var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('/', '\\'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('/', '\\'),
                path => ComputeFileHash(path),
                StringComparer.OrdinalIgnoreCase);
        return new TreeSnapshot(directories, files);
    }

    private static bool TreeMatches(string root, TreeSnapshot expected)
    {
        var actual = CaptureTree(root);
        return actual.Directories.SequenceEqual(expected.Directories, StringComparer.OrdinalIgnoreCase)
               && actual.FileHashes.Count == expected.FileHashes.Count
               && expected.FileHashes.All(pair =>
                   actual.FileHashes.TryGetValue(pair.Key, out var hash)
                   && hash.Equals(pair.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static IEnumerable<string> FindPreservedFiles(string original) =>
        Directory.Exists(Path.GetDirectoryName(original))
            ? Directory.EnumerateFiles(Path.GetDirectoryName(original)!, Path.GetFileName(original) + ".user-modified*")
            : [];

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
