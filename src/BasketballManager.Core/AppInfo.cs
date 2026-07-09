using System.Text.Json;

namespace BasketballManager;

public static class AppInfo
{
    public const string AppName = "篮球比赛信息管理";
    public const string AppNameEn = "Basketball Manager";
    public const string Version = "0.0.1";
    public const string PrimaryAuthor = "张学儒";
    public const string SecondaryAuthors = "刘昭元; MSE,CUFE";
    public const string GitHubRepositoryUrl = "https://github.com/zluoshui/Integrated-Basketball-Game-Information-Management-Software";
    public const string UpdateManifestUrl = "https://raw.githubusercontent.com/zluoshui/Integrated-Basketball-Game-Information-Management-Software/main/update.json";
}

public sealed class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = AppInfo.Version;
    public string? LatestVersion { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? Notes { get; set; }
    public string Message { get; set; } = "";
}

public static class UpdateChecker
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = AppInfo.Version,
            Message = "正在检查更新。"
        };

        try
        {
            using var response = await Http.GetAsync(AppInfo.UpdateManifestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                result.Message = $"无法访问更新源（HTTP {(int)response.StatusCode}）。请确认更新清单已发布。";
                return result;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.LatestVersion))
            {
                result.Message = "更新清单为空或格式不正确。";
                return result;
            }

            result.LatestVersion = manifest.LatestVersion.Trim();
            result.ReleaseUrl = string.IsNullOrWhiteSpace(manifest.ReleaseUrl) ? AppInfo.GitHubRepositoryUrl : manifest.ReleaseUrl;
            result.Notes = manifest.Notes ?? "";
            result.HasUpdate = IsNewer(result.LatestVersion, result.CurrentVersion);
            result.Message = result.HasUpdate
                ? $"发现新版本 {result.LatestVersion}。"
                : $"当前已是最新版本（{result.CurrentVersion}）。";
            return result;
        }
        catch (Exception ex)
        {
            result.Message = $"检查更新失败：{ex.Message}";
            return result;
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        static Version Parse(string value)
        {
            var cleaned = value.Trim().TrimStart('v', 'V');
            return Version.TryParse(cleaned, out var version) ? version : new Version(0, 0, 0);
        }

        return Parse(latest) > Parse(current);
    }

    private sealed class UpdateManifest
    {
        public string? LatestVersion { get; set; }
        public string? ReleaseUrl { get; set; }
        public string? Notes { get; set; }
    }
}
