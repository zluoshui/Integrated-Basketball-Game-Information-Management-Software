# 篮球比赛综合信息管理系统

Windows 10/11 桌面单机应用，用于学校篮球比赛的球员资料、赛前名单、记分台、事件流水和个人技术统计管理。

## 环境要求

- Windows 11，或 Windows 10 22H2 x64。
- 开发环境需要 .NET 10 SDK。
- 普通客户机器优先使用 self-contained 发布包，不需要安装 .NET SDK。

当前项目目标框架：`net10.0-windows`。

## 项目结构

- `BasketballManager.sln`：解决方案入口。
- `src/BasketballManager/`：WPF 应用项目。
- `start_test_app.bat`：本地测试启动脚本，默认使用 PATH 中的 `dotnet`；也可先设置 `DOTNET_EXE` 指向自定义 `dotnet.exe`。
- `PLAN.txt`：阶段开发与验收计划。

## 构建

```powershell
dotnet build .\BasketballManager.sln
```

如果当前终端找不到 `dotnet`，先安装 .NET 10 SDK，或把命令中的 `dotnet` 替换为完整路径，例如：

```powershell
& 'D:\Programs\dotnet\dotnet.exe' build .\BasketballManager.sln
```

## 本地运行

```powershell
dotnet run --project .\src\BasketballManager\BasketballManager.csproj
```

也可以双击根目录的 `start_test_app.bat`。

## 自检

```powershell
dotnet run --project .\src\BasketballManager\BasketballManager.csproj -- --self-check
```

自检覆盖统计计算、事件撤销、基础数据保存和读取。

## 发布

```powershell
dotnet publish .\src\BasketballManager\BasketballManager.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

发布输出目录：

```text
src\BasketballManager\bin\Release\net10.0-windows\win-x64\publish\
```

将该目录整体复制到目标 Windows 机器后，运行 `BasketballManager.exe`。

## 数据位置

应用数据默认保存在：

```text
%AppData%\BasketballManager\
```

当前阶段使用：

- `basketball-data.json`：业务数据。
- `photos\`：导入后的球员照片。

备份时复制整个 `%AppData%\BasketballManager\` 目录即可。正式数据底座将在后续阶段迁移到 SQLite。

## 阶段 0 状态

- 已提供解决方案入口、构建命令、运行命令、发布命令和自检命令。
- 已去除测试启动脚本中的个人绝对路径依赖。
- 已通过 `dotnet build`、`--self-check` 和 win-x64 self-contained 发布验证。
