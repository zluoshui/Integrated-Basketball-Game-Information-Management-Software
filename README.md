# 篮球比赛信息管理 / Basketball Manager

Windows 桌面单机应用，用于学校篮球比赛的球员资料、赛前名单、记分台、事件流水和个人技术统计管理。

## 许可证

- 许可证：MIT License（见 [LICENSE](LICENSE)）
- 版本：v0.0.1

欢迎后续通过 GitHub 协作并在 Contributors 中补充贡献者。

## 功能概览

- 赛事管理：创建 / 导入 / 导出 / 切换 / 删除
- 队伍与球员：自定义字段、照片导入、停用
- 比赛准备：独立选择准备比赛、名单加入/更新/移出
- 记分台：独立选择记分比赛、时钟控制、技术统计、换人、暂停归属
- 比赛日志：统一时间轴、筛选、作废、CSV/JSON 导入导出

## 环境要求

- Windows 10 22H2 x64 或 Windows 11
- 开发：.NET 10 SDK、Node.js 18+（仅构建前端时需要）
- 运行现代界面：WebView2 Runtime（Windows 11 通常已内置）

## 项目结构

```text
src/
  BasketballManager.Core/      # 领域与 SQLite
  BasketballManager.Api/       # 本地 REST API + SPA 托管
  BasketballManager.WebUI/     # React 现代前端
  BasketballManager.Desktop/   # WebView2 桌面壳
  BasketballManager/           # 旧版 WPF（回归/自检）
  BasketballManager.Seed/      # 重置内置演示数据
preview/modern-ui/             # 早期静态预览
start_modern_app.bat           # 现代界面启动
start_test_app.bat             # 旧 WPF 启动
```

## 本地开发

### 现代界面

```powershell
# 可选：重置为内置演示赛事（2 队 × 6 人，无比赛）
dotnet run --project .\src\BasketballManager.Seed\BasketballManager.Seed.csproj

# 启动现代界面（会构建前端并拉起 Desktop + API）
.\start_modern_app.bat
```

### 旧版 WPF（回归）

```powershell
.\start_test_app.bat
# 或
dotnet run --project .\src\BasketballManager\BasketballManager.csproj -- --self-check
```

## 默认演示数据

首次启动（无可用赛事时）会自动创建：

- 赛事 ID：`demo001`
- 赛事名：`内置演示赛事`
- 蓝队 / 红队，各 6 名球员
- 无比赛记录

也可手动重置：

```powershell
dotnet run --project .\src\BasketballManager.Seed\BasketballManager.Seed.csproj
```

## 发布（v0.0.1）

```powershell
# 1) 构建前端
cd .\src\BasketballManager.WebUI
npm install
npm run build
cd ..\..

# 2) 复制前端到 API wwwroot
Remove-Item -Recurse -Force .\src\BasketballManager.Api\wwwroot -ErrorAction SilentlyContinue
New-Item -ItemType Directory .\src\BasketballManager.Api\wwwroot | Out-Null
Copy-Item -Recurse .\src\BasketballManager.WebUI\dist\* .\src\BasketballManager.Api\wwwroot\

# 3) 发布 API 与 Desktop
dotnet publish .\src\BasketballManager.Api\BasketballManager.Api.csproj -c Release -r win-x64 --self-contained true -o .\publish\v0.0.1\api
dotnet publish .\src\BasketballManager.Desktop\BasketballManager.Desktop.csproj -c Release -r win-x64 --self-contained true -o .\publish\v0.0.1\desktop
```

也可直接运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-v0.0.1.ps1
```

发布产物建议结构：

```text
publish/v0.0.1/
  BasketballManager.exe          # Desktop 壳
  api/                           # API 与 wwwroot
  LICENSE
  README.md
  update.json
```

## 更新检查

- 软件左下角提供 GitHub 地址与“检查更新”按钮
- 默认更新清单：`update.json`
- 发布地址集中在 `src/BasketballManager.Core/AppInfo.cs` 中：
  - `GitHubRepositoryUrl`
  - `UpdateManifestUrl`

## 数据目录

```text
%AppData%\BasketballManager\competitions\<competitionId>\
  competition.json
  basketball.db
  photos/
  exports/
  backups/
```


