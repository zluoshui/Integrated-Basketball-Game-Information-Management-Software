# 维护与发布操作指南

这份指南面向第一次维护本项目的人。按顺序做，不要跳过检查步骤。

## 1. 先确认本地状态

在项目根目录打开 PowerShell：

```powershell
cd "D:\codeProgram\Integrated Basketball Game Information Management Software"
git status -sb
# 只有工作区干净时，再执行下一行
git pull origin main
```

如果 `git status -sb` 显示有未提交修改，先确认这些修改是不是你自己的。不要在不清楚的情况下覆盖或删除它们。

## 2. 本地启动和日常验证

启动现代界面：

```powershell
.\start_modern_app.bat
```

常用验证命令：

```powershell
dotnet build .\BasketballManager.sln -c Release --no-restore
dotnet run --project .\src\BasketballManager\BasketballManager.csproj -- --self-check
git --no-pager diff --check
```

如果启动时报 WPF 生成文件缺失，例如 `MainWindow.g.cs`、`App.g.cs` 找不到，优先重新运行 `start_modern_app.bat`。该脚本会清理 Desktop 项目的对应 `obj` 中间目录后重建。

## 3. 改代码或文档后的基本流程

1. 修改前先读相关文件，不按记忆改。
2. 修改后运行上面的验证命令。
3. 查看改动：

```powershell
git --no-pager diff
```

4. 确认只包含本次想提交的内容，再提交：

```powershell
git add <修改过的文件>
git commit -m "简短说明"
git push origin main
```

## 4. 发布新版本前要改的地方

假设新版本是 `0.0.2`，需要同步检查这些位置：

- `src/BasketballManager.Core/AppInfo.cs`
  - `Version`
  - `GitHubRepositoryUrl`
  - `UpdateManifestUrl`
- 根目录 `update.json`
  - `latestVersion`
  - `releaseUrl`
  - `notes`

`update.json` 示例：

```json
{
  "latestVersion": "0.0.2",
  "releaseUrl": "https://github.com/zluoshui/Integrated-Basketball-Game-Information-Management-Software/releases/tag/v0.0.2",
  "notes": "简短说明这次更新内容。"
}
```

版本号和 Release 标签必须对应：`0.0.2` 对应 GitHub 标签 `v0.0.2`。

## 5. 构建发布包

运行一键发布脚本：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-v0.0.1.ps1 -Version "0.0.2"
```

产物目录：

```text
publish/v0.0.2/
  api/
  desktop/
  run.bat
  LICENSE
  README.md
  update.json
  RELEASE_NOTES.md
```

压缩为 Release 资产：

```powershell
Compress-Archive -LiteralPath `
  ".\publish\v0.0.2\api", `
  ".\publish\v0.0.2\desktop", `
  ".\publish\v0.0.2\CONTRIBUTORS.md", `
  ".\publish\v0.0.2\LICENSE", `
  ".\publish\v0.0.2\README.md", `
  ".\publish\v0.0.2\RELEASE_NOTES.md", `
  ".\publish\v0.0.2\run.bat", `
  ".\publish\v0.0.2\update.json" `
  -DestinationPath ".\publish\BasketballManager-v0.0.2-win-x64.zip" `
  -Force
```

## 6. 发布前本地测试

先测试发布版 API：

```powershell
$api = ".\publish\v0.0.2\api\BasketballManager.Api.exe"
$port = 59001
$p = Start-Process -FilePath $api -ArgumentList "--urls http://127.0.0.1:$port" -WorkingDirectory (Split-Path -Parent $api) -PassThru -WindowStyle Hidden
try {
  Invoke-RestMethod "http://127.0.0.1:$port/api/health"
  Invoke-RestMethod "http://127.0.0.1:$port/api/app/info"
  Invoke-RestMethod "http://127.0.0.1:$port/api/app/update-check"
} finally {
  if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force }
}
```

再测试桌面启动：

```powershell
.\publish\v0.0.2\run.bat
```

`/api/app/update-check` 应返回当前版本、最新版本、Release 地址和明确消息。不要发布一个更新检查仍报错的包。

## 7. 推送代码并创建 GitHub Release

确认 GitHub CLI 已登录：

```powershell
D:\Programs\GitHubCLI\gh.exe auth status
```

提交并推送代码：

```powershell
git status -sb
# 如果上面只显示本次发布相关改动，才用这一行
git add .
git commit -m "Release v0.0.2"
git push origin main
```

如果 `git status -sb` 里混有别人的改动或你不确定的文件，不要用 `git add .`，改成逐个添加你确认过的文件。

创建 Release 并上传 zip：

```powershell
D:\Programs\GitHubCLI\gh.exe release create v0.0.2 `
  ".\publish\BasketballManager-v0.0.2-win-x64.zip" `
  --repo zluoshui/Integrated-Basketball-Game-Information-Management-Software `
  --target main `
  --title "Basketball Manager v0.0.2" `
  --notes-file ".\publish\v0.0.2\RELEASE_NOTES.md"
```

如果只是替换同一个 Release 的 zip：

```powershell
D:\Programs\GitHubCLI\gh.exe release upload v0.0.2 `
  ".\publish\BasketballManager-v0.0.2-win-x64.zip" `
  --repo zluoshui/Integrated-Basketball-Game-Information-Management-Software `
  --clobber
```

不要随便强制移动已经公开的 tag。公开 tag 出错时，优先发一个新的补丁版本。

## 8. 发布后检查

检查仓库公开状态：

```powershell
D:\Programs\GitHubCLI\gh.exe repo view zluoshui/Integrated-Basketball-Game-Information-Management-Software --json nameWithOwner,visibility,defaultBranchRef,url
```

检查 Release：

```powershell
D:\Programs\GitHubCLI\gh.exe release view v0.0.2 --repo zluoshui/Integrated-Basketball-Game-Information-Management-Software --json tagName,name,url,isDraft,isPrerelease,assets,targetCommitish
```

检查远端更新清单：

```powershell
curl.exe -L https://raw.githubusercontent.com/zluoshui/Integrated-Basketball-Game-Information-Management-Software/main/update.json
```

最后确认本地干净：

```powershell
git status -sb
```

## 9. 出错时怎么处理

- `gh` 找不到：优先使用 `D:\Programs\GitHubCLI\gh.exe`。
- `gh auth status` 未登录：运行 `D:\Programs\GitHubCLI\gh.exe auth login`。
- `update-check` 超时：先用 `curl.exe -L <UpdateManifestUrl>` 确认 raw 清单能打开；如果 raw 可打开，再检查 `AppInfo.cs` 的超时时间和 URL。
- `update-check` 提示清单格式不正确：检查 `update.json` 是否包含 `latestVersion`、`releaseUrl`、`notes`。
- Release 包上传错了：如果版本还没公开使用，可以用 `release upload --clobber` 覆盖资产；如果已经公开使用，发新版本更稳。
- 已经发布了错误的 `update.json`：把 `update.json` 改回上一个稳定版本，提交并推送，用户的“检查更新”会重新指向稳定 Release。
