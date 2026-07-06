using System.IO;
using System.Text.Json;

namespace BasketballManager;

public sealed class DataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BasketballManager");

    public string DataPath => Path.Combine(DataDirectory, "basketball-data.json");
    public string PhotoDirectory => Path.Combine(DataDirectory, "photos");

    public AppData Load()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(PhotoDirectory);

        if (!File.Exists(DataPath))
        {
            return new AppData();
        }

        try
        {
            var json = File.ReadAllText(DataPath);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法读取本地数据文件: {DataPath}. {ex.Message}", ex);
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(PhotoDirectory);

        try
        {
            File.WriteAllText(DataPath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法保存本地数据文件: {DataPath}. {ex.Message}", ex);
        }
    }

    public string ImportPhoto(string sourcePath)
    {
        Directory.CreateDirectory(PhotoDirectory);

        var extension = Path.GetExtension(sourcePath);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var targetPath = Path.Combine(PhotoDirectory, fileName);
        File.Copy(sourcePath, targetPath, overwrite: false);
        return Path.Combine("photos", fileName);
    }

    public string ResolvePath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? ""
            : Path.Combine(DataDirectory, relativePath);
    }
}
