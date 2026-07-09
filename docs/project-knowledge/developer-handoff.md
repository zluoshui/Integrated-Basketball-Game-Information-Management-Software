# 开发者交接

## 开始工作前

1. 阅读根目录 `AGENTS.md`。
2. 阅读本目录 [README.md](README.md) 和任务涉及的知识库文档。
3. 查看当前工作树状态：

```powershell
git status --short
```

4. 阅读任务相关代码，不要直接按记忆修改。

## 常用验证命令

发布前至少运行：

```powershell
dotnet build .\BasketballManager.sln -c Release --no-restore
.\src\BasketballManager\bin\Release\net10.0-windows\BasketballManager.exe --self-check
git --no-pager diff --check
```

依赖安全检查：

```powershell
dotnet list .\src\BasketballManager\BasketballManager.csproj package --vulnerable --include-transitive
```

如果 NuGet 或用户级配置访问被沙箱阻止，需要明确记录阻塞原因，必要时请求提升权限后重试。
WPF 启动脚本在构建前可清理对应项目的 `obj\<Configuration>\<TargetFramework>`，避免陈旧的 `App.g.cs`、`MainWindow.g.cs` 等 XAML 生成文件路径导致编译失败。
公开发布前确认 `src/BasketballManager.Core/AppInfo.cs` 与根目录 `update.json` 指向同一个 GitHub 仓库和 Release 标签，否则“检查更新”会访问错误清单或错误发布页。
`update.json` 使用 camelCase 字段（`latestVersion`、`releaseUrl`、`notes`）；后端更新检查必须按大小写不敏感方式读取，避免发布清单可访问但被判定为格式错误。
更新检查会访问 GitHub raw 清单，首次联网可能较慢；后端请求超时不应低于 20 秒。

## 常见开发注意事项

- 不要修改用户未要求处理的功能。
- 不要重排无关代码或做大型风格化重构。
- 数据库迁移必须事务化，并更新 schema 版本和本知识库。
- 比赛事件必须优先保留审计痕迹，不要用物理删除代替作废。
- 赛事导入不能覆盖同 ID 的本机赛事。
- 比赛日志导入不能接受不同赛事 ID 的文件。
- 结构化 JSON 是机器导入契约，CSV 只是人工报表。
- 当前 UI 仍是 WPF code-behind，除非任务要求，不要强行迁移 MVVM。

## 交接清单

完成一次开发后，在最终说明中至少交代：

- 修改了哪些主要文件。
- 解决了什么问题。
- 运行了哪些验证命令，结果如何。
- 有没有未完成项或阻塞项。
- 是否更新了项目知识库；如果没有，说明为什么不需要。

只有用户明确要求提交时才创建 git commit。

## 已知风险

- `MainWindow.xaml.cs` 承载较多 UI 状态和业务协调逻辑，后续复杂状态流转适合抽出服务。
- 缺少自动化 UI 测试，很多交互仍依赖人工验收和 `--self-check`。
- 全量保存适合当前小规模单机场景，未来大数据量或多人协作需要重新评估。
- 赛事 ID 修改会影响旧日志导入资格，操作前应让用户明确理解该后果。
- 安装包和正式发布流程尚未最终确定。

