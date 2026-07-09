using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace BasketballManager;

public sealed class CompetitionWorkspaceManager
{
    private const string DefaultCompetitionId = "legacy001";
    private static readonly Regex CompetitionIdPattern = new("^[A-Za-z0-9]+$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CompetitionWorkspaceManager(string? applicationDirectory = null)
    {
        ApplicationDirectory = string.IsNullOrWhiteSpace(applicationDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BasketballManager")
            : applicationDirectory;
    }

    public string ApplicationDirectory { get; }
    public string CompetitionsDirectory => Path.Combine(ApplicationDirectory, "competitions");
    public string SettingsPath => Path.Combine(ApplicationDirectory, "competition-settings.json");

    public CompetitionWorkspace OpenOrCreateInitialWorkspace()
    {
        Directory.CreateDirectory(ApplicationDirectory);
        Directory.CreateDirectory(CompetitionsDirectory);

        var settings = LoadSettings();
        if (!string.IsNullOrWhiteSpace(settings.CurrentWorkspacePath)
            && IsManagedWorkspacePath(settings.CurrentWorkspacePath)
            && IsWorkspaceUsable(settings.CurrentWorkspacePath))
        {
            return OpenWorkspace(settings.CurrentWorkspacePath);
        }

        var existingWorkspacePath = Directory.GetDirectories(CompetitionsDirectory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(IsWorkspaceUsable);
        if (existingWorkspacePath is not null)
        {
            return OpenWorkspace(existingWorkspacePath);
        }

        var legacyDatabasePath = Path.Combine(ApplicationDirectory, "basketball.db");
        if (File.Exists(legacyDatabasePath))
        {
            return MigrateLegacyWorkspace(legacyDatabasePath);
        }

        var workspace = CreateCompetition("默认赛事", DefaultCompetitionId);
        new DataStore(workspace.DirectoryPath).Load();
        SaveCurrentWorkspace(workspace.DirectoryPath);
        return workspace;
    }

    public List<CompetitionWorkspace> ListImportedWorkspaces()
    {
        Directory.CreateDirectory(CompetitionsDirectory);
        return Directory.GetDirectories(CompetitionsDirectory)
            .Where(IsWorkspaceUsable)
            .Select(LoadWorkspace)
            .OrderBy(workspace => workspace.Manifest.Name)
            .ThenBy(workspace => workspace.Manifest.CompetitionId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public CompetitionWorkspace ImportWorkspace(string sourceDirectory)
    {
        var sourceWorkspace = LoadWorkspace(sourceDirectory);
        ValidateCompetitionId(sourceWorkspace.Manifest.CompetitionId);

        Directory.CreateDirectory(CompetitionsDirectory);
        var targetDirectory = Path.Combine(CompetitionsDirectory, sourceWorkspace.Manifest.CompetitionId);
        if (Directory.Exists(targetDirectory))
        {
            throw new InvalidOperationException($"赛事 ID“{sourceWorkspace.Manifest.CompetitionId}”已导入，不能重复导入或覆盖本机赛事。");
        }

        SqliteConnection.ClearAllPools();
        Directory.CreateDirectory(targetDirectory);
        File.Copy(sourceWorkspace.ManifestPath, Path.Combine(targetDirectory, "competition.json"), overwrite: false);
        File.Copy(sourceWorkspace.DatabasePath, Path.Combine(targetDirectory, "basketball.db"), overwrite: false);
        CopyDirectory(sourceWorkspace.PhotoDirectory, Path.Combine(targetDirectory, "photos"));
        Directory.CreateDirectory(Path.Combine(targetDirectory, "exports"));
        Directory.CreateDirectory(Path.Combine(targetDirectory, "backups"));

        var imported = OpenWorkspace(targetDirectory);
        SaveCurrentWorkspace(imported.DirectoryPath);
        return imported;
    }

    public CompetitionWorkspace CreateCompetition(string name, string competitionId)
    {
        var normalizedName = NormalizeCompetitionName(name);
        ValidateCompetitionId(competitionId);

        Directory.CreateDirectory(CompetitionsDirectory);
        var workspacePath = Path.Combine(CompetitionsDirectory, competitionId);
        if (Directory.Exists(workspacePath))
        {
            throw new InvalidOperationException($"赛事 ID“{competitionId}”已存在，请使用其他英文数字 ID。");
        }

        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, "photos"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "exports"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "backups"));

        var manifest = new CompetitionManifest
        {
            CompetitionId = competitionId,
            Name = normalizedName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var workspace = new CompetitionWorkspace { DirectoryPath = workspacePath, Manifest = manifest };
        SaveManifest(workspace);
        new DataStore(workspace.DirectoryPath).Load();
        SaveCurrentWorkspace(workspace.DirectoryPath);
        return workspace;
    }

    public CompetitionWorkspace OpenWorkspace(string workspacePath)
    {
        if (!IsManagedWorkspacePath(workspacePath))
        {
            throw new InvalidOperationException("只能打开已导入到软件内的赛事。请先使用“导入赛事”。");
        }

        var workspace = LoadWorkspace(workspacePath);
        SaveCurrentWorkspace(workspace.DirectoryPath);
        return workspace;
    }

    private CompetitionWorkspace LoadWorkspace(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new InvalidOperationException("赛事目录不能为空。");
        }

        var manifestPath = Path.Combine(workspacePath, "competition.json");
        var databasePath = Path.Combine(workspacePath, "basketball.db");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"赛事目录缺少 competition.json：{workspacePath}");
        }

        if (!File.Exists(databasePath))
        {
            throw new InvalidOperationException($"赛事目录缺少 basketball.db：{workspacePath}");
        }

        var manifest = LoadManifest(manifestPath);
        ValidateCompetitionId(manifest.CompetitionId);
        Directory.CreateDirectory(Path.Combine(workspacePath, "photos"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "exports"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "backups"));

        var workspace = new CompetitionWorkspace { DirectoryPath = workspacePath, Manifest = manifest };
        return workspace;
    }

    public CompetitionWorkspace UpdateManifest(CompetitionWorkspace workspace, string name, string competitionId)
    {
        var normalizedName = NormalizeCompetitionName(name);
        ValidateCompetitionId(competitionId);

        var oldPath = workspace.DirectoryPath;
        var targetPath = oldPath;
        if (!workspace.Manifest.CompetitionId.Equals(competitionId, StringComparison.Ordinal))
        {
            var parent = Directory.GetParent(oldPath)?.FullName ?? CompetitionsDirectory;
            if (parent.Equals(CompetitionsDirectory, StringComparison.OrdinalIgnoreCase))
            {
                targetPath = Path.Combine(parent, competitionId);
                if (Directory.Exists(targetPath))
                {
                    throw new InvalidOperationException($"赛事 ID“{competitionId}”已存在，请使用其他英文数字 ID。");
                }

                SqliteConnection.ClearAllPools();
                Directory.Move(oldPath, targetPath);
            }
        }

        var updated = new CompetitionWorkspace
        {
            DirectoryPath = targetPath,
            Manifest = new CompetitionManifest
            {
                FormatVersion = workspace.Manifest.FormatVersion,
                CompetitionId = competitionId,
                Name = normalizedName,
                CreatedAt = workspace.Manifest.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            }
        };
        SaveManifest(updated);
        SaveCurrentWorkspace(updated.DirectoryPath);
        return updated;
    }

    public string ExportWorkspace(CompetitionWorkspace workspace, string targetRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetRootDirectory))
        {
            throw new InvalidOperationException("导出目标目录不能为空。");
        }

        Directory.CreateDirectory(targetRootDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var targetDirectory = Path.Combine(targetRootDirectory, $"competition-{workspace.Manifest.CompetitionId}-{stamp}");
        var suffix = 1;
        while (Directory.Exists(targetDirectory))
        {
            targetDirectory = Path.Combine(targetRootDirectory, $"competition-{workspace.Manifest.CompetitionId}-{stamp}-{suffix}");
            suffix++;
        }

        SqliteConnection.ClearAllPools();
        Directory.CreateDirectory(targetDirectory);
        File.Copy(workspace.ManifestPath, Path.Combine(targetDirectory, "competition.json"), overwrite: false);
        File.Copy(workspace.DatabasePath, Path.Combine(targetDirectory, "basketball.db"), overwrite: false);
        CopyDirectory(workspace.PhotoDirectory, Path.Combine(targetDirectory, "photos"));
        Directory.CreateDirectory(Path.Combine(targetDirectory, "exports"));
        Directory.CreateDirectory(Path.Combine(targetDirectory, "backups"));
        return targetDirectory;
    }

    public void SaveCurrentWorkspace(string workspacePath)
    {
        Directory.CreateDirectory(ApplicationDirectory);
        var settings = LoadSettings();
        settings.CurrentWorkspacePath = workspacePath;
        settings.RecentWorkspacePaths.RemoveAll(path => path.Equals(workspacePath, StringComparison.OrdinalIgnoreCase));
        settings.RecentWorkspacePaths.Insert(0, workspacePath);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public void DeleteCompetition(string competitionId)
    {
        ValidateCompetitionId(competitionId);
        var workspacePath = Path.Combine(CompetitionsDirectory, competitionId);
        if (!Directory.Exists(workspacePath) || !IsManagedWorkspacePath(workspacePath))
        {
            throw new InvalidOperationException("未找到可删除的赛事。");
        }

        SqliteConnection.ClearAllPools();
        var settings = LoadSettings();
        var isCurrent = settings.CurrentWorkspacePath.Equals(workspacePath, StringComparison.OrdinalIgnoreCase);
        settings.RecentWorkspacePaths.RemoveAll(path => path.Equals(workspacePath, StringComparison.OrdinalIgnoreCase));
        if (isCurrent)
        {
            settings.CurrentWorkspacePath = "";
        }
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));

        // Retry a few times in case SQLite handle release lags.
        Exception? last = null;
        for (var i = 0; i < 5; i++)
        {
            try
            {
                Directory.Delete(workspacePath, recursive: true);
                last = null;
                break;
            }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(120);
                SqliteConnection.ClearAllPools();
            }
        }

        if (last is not null)
        {
            throw new InvalidOperationException($"删除赛事失败：{last.Message}", last);
        }
    }

    public static void ValidateCompetitionId(string competitionId)
    {
        if (string.IsNullOrWhiteSpace(competitionId) || !CompetitionIdPattern.IsMatch(competitionId))
        {
            throw new InvalidOperationException("赛事 ID 只能包含英文字母和数字，不能包含中文、空格或符号。");
        }
    }

    private CompetitionWorkspace MigrateLegacyWorkspace(string legacyDatabasePath)
    {
        var workspacePath = Path.Combine(CompetitionsDirectory, DefaultCompetitionId);
        if (Directory.Exists(workspacePath))
        {
            throw new InvalidOperationException($"无法迁移旧数据库，目标赛事目录已存在：{workspacePath}");
        }

        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, "photos"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "exports"));
        Directory.CreateDirectory(Path.Combine(workspacePath, "backups"));

        SqliteConnection.ClearAllPools();
        File.Copy(legacyDatabasePath, Path.Combine(workspacePath, "basketball.db"), overwrite: false);
        var legacyPhotoDirectory = Path.Combine(ApplicationDirectory, "photos");
        if (Directory.Exists(legacyPhotoDirectory))
        {
            CopyDirectory(legacyPhotoDirectory, Path.Combine(workspacePath, "photos"));
        }

        var workspace = new CompetitionWorkspace
        {
            DirectoryPath = workspacePath,
            Manifest = new CompetitionManifest
            {
                CompetitionId = DefaultCompetitionId,
                Name = "旧数据迁移赛事",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        SaveManifest(workspace);
        SaveCurrentWorkspace(workspace.DirectoryPath);
        return workspace;
    }

    private static string NormalizeCompetitionName(string name)
    {
        var normalized = name.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("赛事名不能为空。");
        }

        return normalized;
    }

    private static void SaveManifest(CompetitionWorkspace workspace)
    {
        Directory.CreateDirectory(workspace.DirectoryPath);
        File.WriteAllText(workspace.ManifestPath, JsonSerializer.Serialize(workspace.Manifest, JsonOptions));
    }

    private static CompetitionManifest LoadManifest(string manifestPath)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<CompetitionManifest>(File.ReadAllText(manifestPath), JsonOptions);
            if (manifest is null)
            {
                throw new InvalidOperationException("manifest 内容为空。");
            }

            return manifest;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法读取赛事 manifest：{manifestPath}. {ex.Message}", ex);
        }
    }

    private bool IsWorkspaceUsable(string workspacePath)
    {
        return File.Exists(Path.Combine(workspacePath, "competition.json"))
            && File.Exists(Path.Combine(workspacePath, "basketball.db"));
    }

    private bool IsManagedWorkspacePath(string workspacePath)
    {
        var fullCompetitionsDirectory = Path.GetFullPath(CompetitionsDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullWorkspacePath = Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullWorkspacePath.StartsWith(fullCompetitionsDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private CompetitionSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            return new CompetitionSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<CompetitionSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                ?? new CompetitionSettings();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法读取赛事工作区设置：{SettingsPath}. {ex.Message}", ex);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
            return;
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private sealed class CompetitionSettings
    {
        public string CurrentWorkspacePath { get; set; } = "";
        public List<string> RecentWorkspacePaths { get; set; } = [];
    }
}
