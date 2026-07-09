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

自检覆盖统计计算、球队级暂停、计时控制事件、名单审计事件、换人事件、按节团队犯规、审计式事件作废、SQLite 数据读写、赛事工作区创建/改名/导出/导入、结构化比赛日志导入导出、重复日志跳过、跨赛事日志拒绝、队伍/比赛关联、比赛状态/节次/表钟持久化、场上球员状态、事件备注、主客队统计、CSV 相关数据源、连续备份和本地应用数据目录自检。

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

- `competitions\<competitionId>\competition.json`：赛事 manifest，保存赛事名、赛事 ID 和格式版本。
- `competitions\<competitionId>\basketball.db`：该赛事独立 SQLite 业务数据库。
- `competitions\<competitionId>\photos\`：该赛事导入后的球员照片。
- `competitions\<competitionId>\exports\`：该赛事默认导出目录。
- `competitions\<competitionId>\backups\`：该赛事手动备份目录。
- 根目录旧版 `basketball.db`：启动时如检测到旧数据库且还没有赛事工作区，会自动迁移为 `legacy001` 赛事。

CSV 导出位置可在“比赛日志”页点击“设置导出位置”修改。未修改时默认写入当前赛事的 `exports\`；点击“导出 CSV”会直接生成文件，不再弹出另存为窗口。

备份时可点击应用内“备份数据”，或复制整个 `%AppData%\BasketballManager\competitions\<competitionId>\` 目录。

## 赛事工作区与日志

- “赛事管理”页可创建赛事、导入其他设备导出的赛事结构化目录、从已导入赛事列表切换赛事、修改当前赛事名 / 赛事 ID、导出当前赛事目录。
- 赛事 ID 仅允许英文字母和数字。修改赛事 ID 后，只认当前 ID；旧 ID 导出的比赛日志不允许导入。
- 外部赛事目录不能直接作为当前工作区打开，必须先导入到软件管理目录；重复赛事 ID 会拒绝导入。
- 导出赛事会生成包含赛事 ID 的结构化目录，目录内包含 `competition.json`、`basketball.db`、`photos\`、`exports\` 和 `backups\`。
- “比赛日志”页显示当前赛事历史比赛，按创建时间倒序。单击或双击比赛只切换本页下方日志查看，不影响比赛准备和记分台的当前比赛选择；勾选“导出”可批量导出结构化 JSON 日志。
- 结构化日志文件名为 `matchlog-<competitionId>-<matchId>-<timestamp>.json`，包含赛事 ID、比赛 ID、事件 ID、比赛、队伍、球员、名单和完整事件流水。
- 导入结构化日志时仅接受当前赛事 ID 的日志；比赛 ID 已存在时自动跳过，不覆盖已有主数据。

## 阶段 1 状态

- 已将主数据源迁移为 SQLite，并保留旧 JSON 自动导入。
- 已建立 `schema_info` 版本表和初始业务表。
- 球员资料支持状态、照片预览、学号唯一校验、删除确认、搜索、自定义字段和手动备份。

## 阶段 2 状态

- 已建立队伍管理：新增/编辑/停用队伍。
- 球员必须关联已有启用队伍；比赛创建使用主队/客队下拉选择，不再只依赖手工输入队名。
- 比赛支持日期、地点、节数和每节分钟数。
- 参赛名单会校验重复球员、主客队重复、球衣号冲突和球员队伍归属。
- 比赛未开始前可选中名单记录更新阵营、号码、首发或移出名单；赛中或赛后修改会进入名单审计更正，要求填写操作人和备注，并写入比赛日志。
- 数据库 schema 已升级到 v2，并提供 `MigrateDatabase()` 迁移框架。

## 阶段 3 状态

- 比赛状态支持未开始、进行中、暂停中、节间和已结束。
- 记分台支持开始、暂停、继续、重置本节、下一节和结束比赛。
- 计时按真实时间差计算并保留不足 1 秒的余量；关闭重开时仍在计时的比赛会先按真实经过时间扣减，再自动停止计时并提示。
- 只有进行中的比赛允许记录事件，已结束比赛不能继续计时或记录。
- 数据库 schema 已升级到 v3，持久化比赛状态。

## 阶段 4 状态

- 记录事件时可按主队/客队筛选本场球员，减少现场误选。
- 事件支持填写备注，比赛日志会显示备注并持久化。
- 重置本节需要二次确认；运行中不能直接进入下一节。
- 事件更正采用审计式作废：保留原事件，记录作废时间、原因和可选操作人。
- 后续建议抽出 `ClockService` / `MatchService`，把状态流转和事件校验从窗口代码中拆出，便于测试。

## 阶段 5 状态

- 比赛日志支持按队伍、事件类型和球员文本筛选。
- 日志显示有效/作废状态、作废时间、作废原因和操作人。
- 球队统计和球员统计由有效事件流水推导，作废事件不计入统计。
- 暂停申请从当前场上球员发起，并同步计入该阵营暂停次数。
- 支持按节团队犯规统计；提前进入下一节需要二次确认，并写入计时控制日志。
- 支持作废选中的任意有效事件，同时保留“作废上一条有效事件”快捷入口。
- 支持导出 CSV，默认导出到当前赛事 `exports\`，可单独设置导出目录；点击导出会直接写入预设目录，文件名包含比赛日期。
- 数据库 schema 已升级到 v5，持久化事件作废审计字段，并允许球队级暂停/计时控制事件不绑定球员。

## 独立赛事状态

- 已支持一个赛事一个独立结构化目录，赛事目录内保存 manifest、SQLite 数据库、照片、导出和备份。
- 创建赛事需要赛事名和唯一英文数字赛事 ID；赛事 ID 可修改，修改后只接受当前 ID 归属的结构化比赛日志。
- 启动时会优先打开最近赛事；旧版根目录 `basketball.db` 会自动迁移为 `legacy001` 赛事。
- 支持导入其他设备导出的赛事目录，缺少 `competition.json` 或 `basketball.db` 会拒绝导入；当前赛事只能从已导入列表打开。
- 支持批量导出结构化比赛日志 JSON，并导入同赛事 ID 且未重复的比赛日志。
- 记分台仅展示当前场上球员；比赛未开始时场上球员由首发同步，暂停状态可执行换人并写入换人事件。
- 数据库 schema 已升级到 v6，持久化当前场上状态和换人事件关联球员。

## 阶段 0 状态

- 验证环境：.NET SDK 10.0.301；Windows 11（OS 版本 10.0.26200）。
- 已提供解决方案入口、构建命令、运行命令、发布命令和自检命令。
- 已去除测试启动脚本中的个人绝对路径依赖。
- 已通过 `dotnet build`、`--self-check` 和 win-x64 self-contained 发布验证。
