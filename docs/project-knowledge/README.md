# 项目知识库

本目录是“篮球积分及球员信息综合管理系统”的长期项目知识库，用于开发者、测试者和 AI 开发者交接。后续任何影响架构、数据结构、业务流程、验证方式或产品边界的变更，都必须同步更新这里的对应文档。

## 必读顺序

1. [architecture.md](architecture.md)：理解当前桌面应用结构、核心模块和边界。
2. [domain-and-data.md](domain-and-data.md)：理解赛事、比赛、球员、名单、事件、数据库和导入导出契约。
3. [operator-workflows.md](operator-workflows.md)：理解软件面向记分台和赛事管理人员的实际工作流。
4. [developer-handoff.md](developer-handoff.md)：理解本地开发、验证命令、常见风险和交接清单。
5. [maintenance-and-release-guide.md](maintenance-and-release-guide.md)：面向新手的维护、构建、发布和更新检查操作步骤。
6. [decision-log.md](decision-log.md)：理解已经做出的关键技术和产品决策。

## 当前项目快照

- 应用类型：Windows 10/11 桌面单机应用。
- 技术栈：C#、.NET 10、WPF、SQLite。
- 数据模式：一个赛事一个独立工作区，赛事工作区内独立保存数据库、照片、导出文件和备份。
- 当前数据库 schema：v6。
- 核心范围：球员资料库、队伍管理、比赛准备、记分台、比赛日志、赛事导入导出、结构化比赛日志导入导出。
- 当前不做：账号系统、联网同步、云备份、多端协作、安装包定稿。

## 知识库维护规则

- 修改数据库表、模型字段、JSON 契约或迁移逻辑时，更新 [domain-and-data.md](domain-and-data.md)。
- 修改 UI 主流程、记分台操作、导入导出方式或赛事管理方式时，更新 [operator-workflows.md](operator-workflows.md)。
- 修改模块边界、服务职责、状态流转方式或大型重构方向时，更新 [architecture.md](architecture.md)。
- 修改构建、自检、发布、测试、已知风险或交接步骤时，更新 [developer-handoff.md](developer-handoff.md)。
- 形成会影响后续实现路线的产品或技术决策时，追加到 [decision-log.md](decision-log.md)。
