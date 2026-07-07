***风险等级：P1-1：球员队伍归属仍允许自由文本，可能绕过稳定队伍关系
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml：约 41-42 行，球员队伍下拉框 IsEditable="True"。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 435-441 行，保存球员时如果未匹配到队伍，仍保存自由文本队伍名，TeamId 可为空。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 818-824 行，加入名单时只有在 player.TeamId 和 sideTeamId 都不为空且不一致时才阻止。
场景：
用户新建球员时在“队伍”里手工输入一个不存在的班级/队伍名。
该球员没有稳定 TeamId，之后加入比赛名单时可被加入主队或客队。
影响：
破坏 PLAN.txt 阶段 2 要求的“队伍与球员建立明确关系，避免仅依赖自由文本”。
学校现场可能把非本队球员加入错误阵营，后续得分、犯规、统计全部归属错误。
建议：
正式比赛名单只能选择已有启用队伍下的球员。
“临时球员/外援/无队伍球员”，应单独设计明确入口，并在加入名单时二次确认、记录原因。
普通球员保存时，建议不允许自由文本队伍直接成为正式归属。
***风险等级：P1-2：比赛开始后、甚至比赛结束后仍可修改名单
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 808-849 行，AddRoster_Click 没有检查比赛状态。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 1039-1054 行，只在开始计时时校验名单人数，之后没有锁定。
场景：
比赛已经进行中，工作人员发现下拉框里还有球员，可以继续“加入名单”。
比赛结束后，也可能继续给历史比赛补加名单成员。
影响：
真实篮球比赛中，赛前名单应在比赛开始后锁定，不能无审计地随意改变。
当前逻辑允许赛中补人，之后可立刻给新加入球员记数据，影响比赛合法性和统计可信度。
赛后导出时会出现未实际参赛、但被后加到名单中的球员，统计表失真。
建议：
比赛状态为 Running、Paused、Interval、Finished 后，不允许无审计的修改名单。
允许赛中有审计的更正名单，提供“名单更正”功能：必须填写原因、操作人、时间，并在日志/导出中体现。
结束比赛后不应允许修改有效名单，只允许管理员式（key）审计更正。
***风险等级：P1-3：名单误录后没有移除/编辑入口，不适合赛前实际核对
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml：约 151-160 行，名单区只有“加入名单”按钮。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml：约 178-181 行，RostersGrid 只读。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 808-849 行，只实现添加名单，没有对应删除/修改球衣号/修改首发。
场景：
赛前把球员加入错了阵营。
球衣号录错。
首发勾选错。
影响：
赛前名单管理是阶段 2 核心功能，当前只能添加不能更正，现场非常容易卡住。
用户可能被迫删除球员、重建比赛或接受错误名单。
建议：
增加“移出名单”“修改号码”“修改首发”入口。
比赛未开始时允许直接修改。
比赛开始后如需修改，走带原因和操作人的审计更正流程。
***风险等级：P1-4：异常关闭或崩溃后，计时恢复策略不可靠
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 371-390 行，重启后发现运行中比赛直接设置为暂停。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 1056-1083 行，计时只更新内存中的 RemainingSeconds，通常不周期性保存，只有归零时保存。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 1330-1334 行，正常关闭时才保存一次。
场景：
比赛进行到第 2 节 08:00，程序卡死或被强制结束。
重启后系统只看到上一次保存的时间和 LastClockUpdateUtc，但 PauseRunningMatchesAfterRestart 没有基于真实经过时间补偿，而是直接暂停。
影响：
实际剩余时间可能错误。
PLAN.txt 阶段 3 要求“异常关闭或正常关闭后重开，应能恢复到合理状态”。当前对强制关闭场景不够可靠。
建议：
采用明确策略之一：
重启时用 LastClockUpdateUtc 计算真实经过时间，补偿到当前剩余时间，再自动暂停并提示；
重启时提示用户选择“按上次保存时间恢复 / 按真实时间扣减”。
应在 README 和界面中说明。
***风险等级：P1-5：计时计算可能在 UI 卡顿时产生慢漂移
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 37-42 行，使用 DispatcherTimer。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 1064-1072 行，把真实时间差转成整数秒后，直接把 LastClockUpdateUtc 设为 now。
场景：
UI 繁忙、窗口卡顿、导出或大量刷新导致 tick 延迟。
如果某次实际间隔 1.3 秒，代码只扣 1 秒，然后把基准时间重置到当前，丢掉 0.3 秒。
影响：
比赛计时会偏慢。
篮球记分台计时是核心功能，现场比赛对秒级准确性较敏感。
建议：
不要丢弃小数秒余量。
可把 LastClockUpdateUtc 推进 elapsedSeconds 秒，而不是设为 now。
更好的是用单调时钟/Stopwatch 或基于固定“本节结束绝对时间”计算剩余时间。
***风险等级：P2-1：暂停被强制设计为“球员事件”，不符合多数篮球记分台习惯
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\Models.cs：约 120-130 行，TimeoutRequest 是 MatchEventKind。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\Statistics.cs：约 66-75 行，暂停既计入球员 TimeoutRequests，又计入球队暂停。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 1095 行，暂停按钮调用 RecordEvent(MatchEventKind.TimeoutRequest)，因此必须选择球员。
场景：
实际比赛中通常是教练或球队请求暂停，不一定归属某个球员。
现场工作人员为了记录暂停，被迫随便选一个球员。
影响：
如果随便选球员，个人统计不真实。
建议：
把暂停改为球队级事件，可选球员，不强制绑定球员。
统计表中主要展示球队暂停次数，不强制作为球员技术统计。
***风险等级：P2-2：球队犯规和暂停只做全场累计，未体现按节/半场规则
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\Statistics.cs：约 40-50 行，全场累计犯规。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\Statistics.cs：约 66-75 行，全场累计暂停。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 203-204 行，只显示总犯规/总暂停。
场景：
学校比赛可能采用每节团队犯规累计、每半场暂停限制、不同赛制有不同暂停规则。
当前界面只显示总数，无法判断本节团队犯规是否到罚球临界。
影响：
对正式记分台辅助价值不足。
统计报表虽有总数，但现场规则判断不够。
建议：
至少提供按节团队犯规统计。
暂停建议按半场/全场规则可配置。
***风险等级：P2-3：进入下一节可在剩余时间未归零时直接执行，缺少确认和审计
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 965-1003 行，NextPeriod_Click 在非运行状态下可直接进入下一节。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 944-952 行，重置本节有确认，但下一节没有类似确认。
场景：
工作人员误点“下一节”，当前节还有几分钟。
系统直接把节次加一、时间重置为新一节长度。
影响：
现场误操作风险较高。
原本剩余时间没有审计记录，事后难以复盘。
建议：
如果 RemainingSeconds > 0，进入下一节必须二次确认。
记录节次切换事件，包括操作时间、原剩余时间、操作人/原因。
比赛日志中应显示“计时控制类事件”。
***风险等级：P2-4：队伍改名会回写所有历史比赛名称，影响归档一致性
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 671-682 行，保存队伍时会遍历所有比赛并更新 HomeTeamName、AwayTeamName。
场景：
“高一 1 班”后来改名为“高二 1 班”或队伍名称调整。
历史比赛导出时也显示新名称。
影响：
历史比赛记录不再保持当时事实。
学校归档和赛后公示可能出现争议。
建议：
比赛创建时应保存主客队名称快照。
队伍主数据改名后，不应默认改写历史比赛。
***风险等级：P2-5：创建同名队伍时可能不是提示重复，而是静默编辑已有队伍
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 649-660 行。
场景：
用户未选中队伍，在“队伍名”输入已有名称，点击“新增/保存队伍”。
代码会把 team 指向已有同名队伍，而不是提示重复。
影响：
不符合 PLAN.txt 阶段 2 “队伍名称重复时应提示”。
用户可能无意中覆盖已有队伍备注或状态。
建议：
新增和编辑应分开判断。
未选中队伍时，如果名称已存在，应提示“队伍已存在”，不要直接编辑。
编辑已有队伍时才允许保存到该队伍。
***风险等级：P2-6：事件作废只能作废上一条有效事件，不能选择日志中任意错误事件
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml：约 262-269 行，只有“作废上一条有效事件”。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 1139-1183 行，按 CreatedAt 找上一条未作废事件。
场景：
几分钟后发现前面某个篮板或犯规记错。
当前只能连续作废最新事件，可能误伤后续正确记录。
影响：
真实记分台更正效率不足。
虽然阶段 4 最低要求是“撤销上一条”，但阶段 5 已有比赛日志，实际应支持在日志中选择指定事件作废。
建议：
在事件表格中选择某条事件，点击“作废选中事件”。
作废时保留当前已实现的原因、操作人、作废时间。
禁止作废已作废事件，或提供清晰提示。
***风险等级：P3-1：球衣号校验较弱，允许空号，且未标准化 1 与 01
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 833-838 行，只在非空时按字符串比较同阵营号码。
场景：
同队一个球员填 1，另一个球员填 01。
多名球员球衣号为空。
影响：
赛后报表对学校老师和裁判不够清晰。
现场按照号码找球员时容易混乱。
建议：
根据学校规则决定是否强制球衣号必填。
对号码做规范化，或明确允许 0、00、1-99 等范围。
空号应有提示，至少在开始比赛前提醒。
***风险等级：P3-2：比赛每节时长用“秒数”输入，对普通学校用户不够直观
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml：约 143-146 行，显示“每节秒数”。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 766-769 行，按正整数秒解析。
场景：
老师想设置“8 分钟一节”，需要知道填 480。
影响：
易用性不足，不符合学校非技术人员使用场景。
建议：
改为分钟输入，或提供常用按钮：8 分钟、10 分钟、12 分钟。
高级设置中再保留秒级输入。
***风险等级：P3-3：导出文件名未包含比赛日期，只包含比赛名和导出时间
文件/行号：
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\src\BasketballManager\MainWindow.xaml.cs：约 1194-1204 行。
d:\codeProgram\Integrated Basketball Game Information Management Software\.claude\worktrees\agent-a4f3dc08c603ebc87\PLAN.txt：约 506-508 行，要求文件名包含比赛名和日期。
场景：
多场同名比赛或补导出历史比赛。
影响：
文件归档不够清楚。
建议：
文件名中优先使用 match.ScheduledAt，例如 比赛名-20260708-导出时间.csv。
没有比赛日期时再使用当前导出时间。

---

## 修复记录（2026-07-08）

- P1-1 已修复：球员队伍改为只能选择已有启用队伍；正式比赛名单只允许加入所选阵营队伍下的球员。
- P1-2 已修复：比赛状态不是“未开始”时，加入、更新、移出名单均被阻止，并提示需后续专门审计入口。
- P1-3 已修复：赛前名单增加“更新名单”和“移出名单”，支持更正号码、阵营和首发。
- P1-4 已修复：启动恢复时按 `LastClockUpdateUtc` 计算真实经过时间并扣减，再自动停止计时并提示恢复结果。
- P1-5 已修复：计时更新保留不足 1 秒余量，避免 UI tick 延迟导致慢漂移，并按秒保存表钟状态。
- P2-1 已修复：暂停可作为球队级事件记录，不再强制选择球员；如选择了本队球员则保留可选个人归属。
- P2-2 已修复：统计投影增加按节团队犯规，记分台和 CSV 均展示相关数据。
- P2-3 已修复：剩余时间大于 0 时进入下一节需要二次确认，并写入计时控制类事件。
- P2-4 已修复：队伍改名只更新球员当前队伍名，不再回写历史比赛的主客队名称快照。
- P2-5 已修复：未选中队伍时输入已有队伍名会提示重复，不再静默编辑已有队伍。
- P2-6 已修复：比赛日志支持选中任意有效事件并作废，保留原因、时间和操作人。
- P3-1 已修复：球衣号规范为 0 到 99 的整数，`01` 会保存为 `1`；开始比赛前如有空号会提示确认。
- P3-2 已修复：比赛创建界面改为输入“每节分钟”，默认 10。
- P3-3 已修复：CSV 文件名包含比赛日期，格式为 `比赛名-比赛日期-导出时间.csv`。
