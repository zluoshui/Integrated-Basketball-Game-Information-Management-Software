using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BasketballManager.Api;

public sealed class LocalApiHost : IAsyncDisposable
{
    private WebApplication? _app;

    public string Url { get; private set; } = "";
    public int Port { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        Port = GetFreePort();
        Url = $"http://127.0.0.1:{Port}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls(Url);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });
        builder.Services.AddSingleton<BasketballManager.AppSession>();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
        });

        _app = builder.Build();
        // Reuse endpoint map by launching the compiled Program entry indirectly is hard.
        // Desktop will start the Api process instead for reliability.
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }

    public static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static Process StartApiProcess(int? port = null, string? contentRoot = null)
    {
        var selectedPort = port ?? GetFreePort();
        var apiProject = FindApiProject();
        var psi = new ProcessStartInfo
        {
            FileName = ResolveDotnet(),
            Arguments = $"run --project \"{apiProject}\" -c Debug --no-launch-profile --urls http://127.0.0.1:{selectedPort}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(apiProject)!
        };
        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            psi.Environment["ASPNETCORE_CONTENTROOT"] = contentRoot;
        }

        var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 API 进程。");
        process.StartInfo.Environment["BASKETBALL_MANAGER_API_PORT"] = selectedPort.ToString();
        return process;
    }

    private static string ResolveDotnet()
    {
        var preferred = @"D:\Programs\dotnet\dotnet.exe";
        return File.Exists(preferred) ? preferred : "dotnet";
    }

    private static string FindApiProject()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\BasketballManager.Api\BasketballManager.Api.csproj")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\BasketballManager.Api\BasketballManager.Api.csproj")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"src\BasketballManager.Api\BasketballManager.Api.csproj"))
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("找不到 BasketballManager.Api 项目。");
    }
}
