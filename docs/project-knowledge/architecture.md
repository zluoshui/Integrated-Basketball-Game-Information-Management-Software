# 架构说明

## 产品定位

本项目是面向学校、社团或小型赛事记分台的 Windows 桌面单机应用。第一目标是离线稳定、可追溯、便于人工验收，不依赖服务器或账号体系。

## 当前技术栈

- UI：WPF。
- 语言和运行时：C#、.NET 10。
- 数据库：SQLite。
- 数据序列化：JSON 用于结构化比赛日志和赛事 manifest。
- 发布形态：当前以本地构建和自检为主，安装包暂缓。

## 主要模块

- `MainWindow.xaml` / `MainWindow.xaml.cs`：当前主要 UI 和交互入口，仍沿用 WPF code-behind 风格。
- `Models.cs`：领域模型、枚举、赛事 manifest、工作区模型、结构化日志 DTO。
- `DataStore.cs`：SQLite 初始化、迁移、读写、备份、自定义字段、照片路径、导出目录配置。
- `CompetitionWorkspaceManager.cs`：赛事工作区创建、导入、导出、列出、打开和最近赛事设置。
- `MatchLogService.cs`：结构化比赛日志 JSON 导出和导入。
- `Statistics.cs`：从事件流水投影比分、球队统计和球员技术统计。
- `App.xaml.cs`：应用启动和 `--self-check` 自检入口。

## 状态与数据流

应用当前采用“内存聚合 + 全量保存”的方式：

1. UI 加载当前赛事工作区。
2. `DataStore` 从赛事内的 `basketball.db` 读取为 `AppData`。
3. UI 操作修改内存中的 `AppData`。
4. 保存时 `DataStore.SaveData` 在事务中写回完整数据图。
5. 比分、个人统计和日志展示由事件流水重新投影。

这种方式适合当前单机、小规模赛事场景。未来如果数据量扩大或多人协作，需要再拆分更细粒度的命令和增量持久化。

## 赛事工作区边界

软件只打开已导入到本机应用数据目录下的赛事工作区。外部设备导出的赛事目录必须先通过“导入赛事”复制进软件管理目录，再从已导入赛事列表中打开。

工作区结构：

```text
competitions/
  <competitionId>/
    competition.json
    basketball.db
    photos/
    exports/
    backups/
```

`competitionId` 只允许英文字母和数字。赛事 ID 修改后只认当前 ID，旧 ID 导出的比赛日志不再允许导入。

## 设计约束

- 当前阶段不进行大型 MVVM 重构，优先保持既有 code-behind 风格并控制改动范围。
- 可复用、可验证的业务逻辑优先放到服务类或投影函数中，例如 `Statistics`、`MatchLogService`、`CompetitionWorkspaceManager`。
- 数据可靠性优先于 UI 便利性：审计、作废、导入去重、迁移事务和赛事隔离不能为了简化交互而绕过。
- 后续适合抽出的模块：`ClockService`、`MatchService`、`RosterService`，用于测试比赛状态流转和换人规则。

