# 架构说明

## 产品定位

本项目是面向学校、社团或小型赛事记分台的 Windows 桌面单机应用。第一目标是离线稳定、可追溯、便于人工验收，不依赖服务器或账号体系。

## 当前技术栈

- UI（经典）：WPF code-behind，`start_test_app.bat`。
- UI（现代）：WebView2 桌面壳 + React/Vite SPA + 本地 ASP.NET Core Minimal API，`start_modern_app.bat`。
- 语言和运行时：C#、.NET 10。
- 数据库：SQLite。
- 数据序列化：JSON 用于结构化比赛日志和赛事 manifest。
- 发布形态：当前以本地构建和自检为主，安装包暂缓。

## 主要模块

- `BasketballManager.Core`：领域模型、SQLite、赛事工作区、统计投影、会话 `AppSession`。
- `BasketballManager.Api`：REST API，默认绑定本机环回地址；可托管 SPA 静态文件。
- `BasketballManager.Desktop`：WebView2 壳，启动 API 并加载本地界面。
- `BasketballManager.WebUI`：现代前端源码（Vite + React + TypeScript）。
- `BasketballManager`：旧 WPF 界面，继续用于回归与 `--self-check`。
- `preview/modern-ui`：静态高保真预览（审阅用）。

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

