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
& '<dotnet.exe 完整路径>' build .\BasketballManager.sln
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

自检覆盖统计计算、事件撤销、SQLite 数据读写、队伍/比赛关联、连续备份和本地应用数据目录自检。

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

- `basketball.db`：SQLite 业务数据库。
- `basketball-data.json`：旧版本 JSON 数据文件；首次打开时会自动导入到 SQLite。
- `photos\`：导入后的球员照片。
- `backups\`：手动备份目录。

备份时可点击应用内“备份数据”，或复制整个 `%AppData%\BasketballManager\` 目录。

## 阶段 1 状态

- 已将主数据源迁移为 SQLite，并保留旧 JSON 自动导入。
- 已建立 `schema_info` 版本表和初始业务表。
- 球员资料支持状态、照片预览、学号唯一校验、删除确认、搜索、自定义字段和手动备份。

## 阶段 2 状态

- 已建立队伍管理：新增/编辑/停用队伍。
- 球员可关联队伍；比赛创建使用主队/客队下拉选择，不再只依赖手工输入队名。
- 比赛支持日期、地点、节数和每节时长。
- 参赛名单会校验重复球员、主客队重复、球衣号冲突和球员队伍归属。
- 数据库 schema 已升级到 v2，并提供 `MigrateDatabase()` 迁移框架。

## 阶段 0 状态

- 验证环境：.NET SDK 10.0.301；Windows 11（OS 版本 10.0.26200）。
- 已提供解决方案入口、构建命令、运行命令、发布命令和自检命令。
- 已去除测试启动脚本中的个人绝对路径依赖。
- 已通过 `dotnet build`、`--self-check` 和 win-x64 self-contained 发布验证。
