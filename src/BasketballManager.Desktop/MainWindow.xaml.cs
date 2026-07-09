using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;

namespace BasketballManager.Desktop;

public partial class MainWindow : Window
{
    private Process? _apiProcess;
    private int _port;
    private readonly DispatcherTimer _healthTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };
    private DateTime _startedAt;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        _healthTimer.Tick += async (_, _) => await TryNavigateWhenReadyAsync();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "正在启动本地服务…";
            _port = GetFreePort();
            _apiProcess = StartApiProcess(_port);
            _startedAt = DateTime.UtcNow;
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _healthTimer.Start();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"启动失败：{ex.Message}";
            MessageBox.Show(ex.Message, "现代界面启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task TryNavigateWhenReadyAsync()
    {
        if ((DateTime.UtcNow - _startedAt).TotalSeconds > 60)
        {
            _healthTimer.Stop();
            StatusText.Text = "本地服务启动超时，请检查 .NET SDK、WebView2 与端口占用。";
            return;
        }

        try
        {
            if (_apiProcess is { HasExited: true })
            {
                _healthTimer.Stop();
                StatusText.Text = "本地 API 进程已退出，请重新运行 start_modern_app.bat。";
                return;
            }

            var health = await _http.GetAsync($"http://127.0.0.1:{_port}/api/health");
            if (!health.IsSuccessStatusCode)
            {
                return;
            }

            // Ensure SPA index is actually served, not just the API.
            var page = await _http.GetAsync($"http://127.0.0.1:{_port}/");
            if (!page.IsSuccessStatusCode)
            {
                StatusText.Text = "API 已启动，但前端页面未找到。请重新执行 start_modern_app.bat 构建 WebUI。";
                return;
            }

            _healthTimer.Stop();
            StatusText.Text = "";
            StatusText.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Visible;
            webView.Source = new Uri($"http://127.0.0.1:{_port}/");
        }
        catch
        {
            // keep polling
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _healthTimer.Stop();
        try
        {
            if (_apiProcess is { HasExited: false })
            {
                _apiProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // ignore shutdown races
        }
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static Process StartApiProcess(int port)
    {
        var apiExe = FindPublishedApiExe();
        if (apiExe is not null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = apiExe,
                Arguments = $"--urls http://127.0.0.1:{port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(apiExe)!
            };
            return Process.Start(psi) ?? throw new InvalidOperationException("无法启动已发布的 API 进程。");
        }

        var apiProject = FindApiProject();
        var builtDll = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(apiProject)!,
            "bin", "Debug", "net10.0", "BasketballManager.Api.dll"));

        ProcessStartInfo startInfo;
        if (File.Exists(builtDll))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = ResolveDotnet(),
                Arguments = $"\"{builtDll}\" --urls http://127.0.0.1:{port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(builtDll)!
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = ResolveDotnet(),
                Arguments = $"run --project \"{apiProject}\" -c Debug --urls http://127.0.0.1:{port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(apiProject)!
            };
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 API 进程。");
    }

    private static string? FindPublishedApiExe()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\api\BasketballManager.Api.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"api\BasketballManager.Api.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "BasketballManager.Api.exe")),
        };
        return candidates.FirstOrDefault(File.Exists);
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
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), @"src\BasketballManager.Api\BasketballManager.Api.csproj")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\BasketballManager.Api\BasketballManager.Api.csproj"))
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("找不到 BasketballManager.Api 项目或已发布的 API 可执行文件。");
    }
}
