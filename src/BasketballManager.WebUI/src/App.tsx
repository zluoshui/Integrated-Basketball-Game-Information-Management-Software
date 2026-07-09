import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  api,
  AppInfo,
  Competition,
  formatClock,
  MatchEvent,
  MatchSummary,
  Player,
  PlayerField,
  Roster,
  Scoreboard,
  Team,
} from './api'

type View = 'teams' | 'prep' | 'scoreboard' | 'logs' | 'settings'

const STAT_LABELS = ['得分', '犯规', '篮板', '助攻', '抢断', '盖帽', '失误', '申请暂停'] as const

export default function App() {
  const [view, setView] = useState<View>('teams')
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [toast, setToast] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const [competitionMenuOpen, setCompetitionMenuOpen] = useState(false)
  const [competitions, setCompetitions] = useState<Competition[]>([])
  const [current, setCurrent] = useState<Competition | null>(null)
  const [appInfo, setAppInfo] = useState<AppInfo | null>(null)

  const [teams, setTeams] = useState<Team[]>([])
  const [players, setPlayers] = useState<Player[]>([])
  const [fields, setFields] = useState<PlayerField[]>([])
  const [selectedTeamId, setSelectedTeamId] = useState<string>('')
  const [selectedPlayerId, setSelectedPlayerId] = useState<string>('')

  const [matches, setMatches] = useState<MatchSummary[]>([])
  const [prepMatchId, setPrepMatchId] = useState('')
  const [scoreMatchId, setScoreMatchId] = useState('')
  const [logMatchId, setLogMatchId] = useState('')
  const [roster, setRoster] = useState<Roster[]>([])
  const [scoreboard, setScoreboard] = useState<Scoreboard | null>(null)
  const [selectedOnCourtId, setSelectedOnCourtId] = useState('')
  const [events, setEvents] = useState<MatchEvent[]>([])

  const [createCompOpen, setCreateCompOpen] = useState(false)
  const [createTeamOpen, setCreateTeamOpen] = useState(false)
  const [createPlayerOpen, setCreatePlayerOpen] = useState(false)
  const [createMatchOpen, setCreateMatchOpen] = useState(false)
  const [voidOpen, setVoidOpen] = useState(false)
  const [voidTarget, setVoidTarget] = useState<MatchEvent | null>(null)
  const [contextMenu, setContextMenu] = useState<{ x: number; y: number; event?: MatchEvent; match?: MatchSummary } | null>(null)
  const [pauseOpen, setPauseOpen] = useState(false)
  const [endConfirmOpen, setEndConfirmOpen] = useState(false)
  const [deleteCompOpen, setDeleteCompOpen] = useState(false)
  const [eventFilterKind, setEventFilterKind] = useState('全部')
  const [eventFilterPlayerId, setEventFilterPlayerId] = useState('全部')
  const [allPlayers, setAllPlayers] = useState<Player[]>([])

  const showToast = (message: string) => {
    setToast(message)
    window.setTimeout(() => setToast(''), 1800)
  }

  const refreshShell = useCallback(async (options?: { resetSelection?: boolean }) => {
    const [comps, cur, teamList, fieldList, matchList, playerList, info] = await Promise.all([
      api.competitions(),
      api.currentCompetition(),
      api.teams(),
      api.playerFields(),
      api.matches(),
      api.players(),
      api.appInfo().catch(() => null),
    ])
    setCompetitions(comps)
    setCurrent(cur)
    setTeams(teamList)
    setFields(fieldList)
    setMatches(matchList)
    setAllPlayers(playerList)
    if (info) setAppInfo(info)

    const pickTeam = () => teamList.find((t) => t.status === '启用')?.id || teamList[0]?.id || ''
    const pickMatch = () => matchList[0]?.id || ''

    if (options?.resetSelection) {
      const teamId = pickTeam()
      const matchId = pickMatch()
      setSelectedTeamId(teamId)
      setSelectedPlayerId('')
      setPrepMatchId(matchId)
      setScoreMatchId(matchId)
      setLogMatchId(matchId)
      setEventFilterKind('全部')
      setEventFilterPlayerId('全部')
      if (teamId) {
        const list = await api.players(teamId)
        setPlayers(list)
        setSelectedPlayerId(list[0]?.id || '')
      } else {
        setPlayers([])
      }
      if (matchId) {
        const [r, board, ev] = await Promise.all([api.roster(matchId), api.scoreboard(matchId), api.events(matchId)])
        setRoster(r)
        setScoreboard(board)
        setSelectedOnCourtId(board.homeOnCourt[0]?.playerId || board.awayOnCourt[0]?.playerId || '')
        setEvents(ev)
      } else {
        setRoster([])
        setScoreboard(null)
        setEvents([])
      }
      return
    }

    setSelectedTeamId((prev) => (teamList.some((t) => t.id === prev) ? prev : pickTeam()))
    setPrepMatchId((prev) => (matchList.some((m) => m.id === prev) ? prev : pickMatch()))
    setScoreMatchId((prev) => (matchList.some((m) => m.id === prev) ? prev : pickMatch()))
    setLogMatchId((prev) => (matchList.some((m) => m.id === prev) ? prev : pickMatch()))
  }, [])

  const refreshPlayers = useCallback(async (teamId: string) => {
    if (!teamId) {
      setPlayers([])
      return
    }
    const list = await api.players(teamId)
    setPlayers(list)
    setSelectedPlayerId((prev) => (list.some((p) => p.id === prev) ? prev : list[0]?.id || ''))
  }, [])

  const refreshRoster = useCallback(async (matchId: string) => {
    if (!matchId) {
      setRoster([])
      return
    }
    setRoster(await api.roster(matchId))
  }, [])

  const refreshScoreboard = useCallback(async (matchId: string) => {
    if (!matchId) {
      setScoreboard(null)
      return
    }
    const board = await api.scoreboard(matchId)
    setScoreboard(board)
    setSelectedOnCourtId((prev) => {
      const all = [...board.homeOnCourt, ...board.awayOnCourt]
      return all.some((p) => p.playerId === prev) ? prev : all[0]?.playerId || ''
    })
  }, [])

  const refreshEvents = useCallback(async (matchId: string) => {
    if (!matchId) {
      setEvents([])
      return
    }
    setEvents(await api.events(matchId))
  }, [])

  const refreshAfterMutation = useCallback(async () => {
    await refreshShell()
    if (selectedTeamId) await refreshPlayers(selectedTeamId)
    if (prepMatchId) await refreshRoster(prepMatchId)
    if (scoreMatchId) await refreshScoreboard(scoreMatchId)
    if (logMatchId) await refreshEvents(logMatchId)
  }, [refreshShell, refreshPlayers, refreshRoster, refreshScoreboard, refreshEvents, selectedTeamId, prepMatchId, scoreMatchId, logMatchId])

  useEffect(() => {
    ;(async () => {
      try {
        setLoading(true)
        await refreshShell({ resetSelection: true })
        setError('')
      } catch (e) {
        setError(e instanceof Error ? e.message : String(e))
      } finally {
        setLoading(false)
      }
    })()
  }, [refreshShell])

  useEffect(() => {
    if (selectedTeamId) refreshPlayers(selectedTeamId).catch((e) => showToast(e.message))
  }, [selectedTeamId, refreshPlayers])

  useEffect(() => {
    if (prepMatchId) refreshRoster(prepMatchId).catch((e) => showToast(e.message))
  }, [prepMatchId, refreshRoster])

  useEffect(() => {
    if (scoreMatchId) refreshScoreboard(scoreMatchId).catch((e) => showToast(e.message))
  }, [scoreMatchId, refreshScoreboard])

  useEffect(() => {
    if (logMatchId) refreshEvents(logMatchId).catch((e) => showToast(e.message))
  }, [logMatchId, refreshEvents])

  useEffect(() => {
    if (view !== 'scoreboard' || !scoreMatchId) return
    const timer = window.setInterval(() => {
      refreshScoreboard(scoreMatchId).catch(() => undefined)
      // Keep match list/event counts roughly fresh while scoring.
      api.matches().then(setMatches).catch(() => undefined)
      if (logMatchId === scoreMatchId) {
        refreshEvents(logMatchId).catch(() => undefined)
      }
    }, 1000)
    return () => window.clearInterval(timer)
  }, [view, scoreMatchId, logMatchId, refreshScoreboard, refreshEvents])

  const selectedTeam = teams.find((t) => t.id === selectedTeamId) || null
  const selectedPlayer = players.find((p) => p.id === selectedPlayerId) || null
  const prepMatch = matches.find((m) => m.id === prepMatchId) || null
  const logMatch = matches.find((m) => m.id === logMatchId) || null
  const selectedOnCourt = useMemo(() => {
    if (!scoreboard) return null
    return [...scoreboard.homeOnCourt, ...scoreboard.awayOnCourt].find((p) => p.playerId === selectedOnCourtId) || null
  }, [scoreboard, selectedOnCourtId])

  const homeRoster = roster.filter((r) => r.side === '主队')
  const awayRoster = roster.filter((r) => r.side === '客队')

  const filteredEvents = useMemo(() => {
    return [...events]
      .sort((a, b) => +new Date(b.createdAt) - +new Date(a.createdAt))
      .filter((e) => {
        if (eventFilterKind !== '全部' && e.kind !== eventFilterKind) return false
        if (eventFilterPlayerId !== '全部' && e.playerId !== eventFilterPlayerId) return false
        return true
      })
  }, [events, eventFilterKind, eventFilterPlayerId])

  const logPlayerOptions = useMemo(() => {
    const ids = new Set(events.map((e) => e.playerId).filter(Boolean) as string[])
    return allPlayers.filter((p) => ids.has(p.id))
  }, [events, allPlayers])

  async function withToast(action: () => Promise<void>, success?: string) {
    try {
      await action()
      if (success) showToast(success)
      setError('')
    } catch (e) {
      const message = e instanceof Error ? e.message : String(e)
      setError(message)
      showToast(message)
    }
  }

  if (loading) {
    return <div className="content" style={{ padding: 40 }}>正在连接本地服务…</div>
  }

  return (
    <div className={`app ${sidebarCollapsed ? 'sidebar-collapsed' : ''}`} onClick={() => { setCompetitionMenuOpen(false); setContextMenu(null) }}>
      <header className="topbar">
        <div className="topbar-left">
          <div className={`competition-switcher ${competitionMenuOpen ? 'open' : ''}`} onClick={(e) => e.stopPropagation()}>
            <button className="competition-trigger" type="button" onClick={() => setCompetitionMenuOpen((v) => !v)}>
              <div className="competition-meta">
                <span className="competition-name">{current?.name || '未选择赛事'}</span>
                <span className="competition-id">ID: {current?.id || '-'}</span>
              </div>
              <svg className="chevron" viewBox="0 0 20 20" fill="none"><path d="M5 7.5L10 12.5L15 7.5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"/></svg>
            </button>
            <div className="competition-menu">
              <div className="menu-section-label">已导入赛事</div>
              {competitions.map((c) => (
                <button
                  key={c.id}
                  className={`menu-item ${c.isCurrent ? 'active' : ''}`}
                  type="button"
                  onClick={() => withToast(async () => {
                    await api.switchCompetition(c.id)
                    await refreshShell({ resetSelection: true })
                    setCompetitionMenuOpen(false)
                  }, `已切换到 ${c.name}`)}
                >
                  <span>{c.name}<small>{c.id}{c.isCurrent ? ' · 当前' : ''}</small></span>
                  {c.isCurrent && <span className="badge badge-blue">使用中</span>}
                </button>
              ))}
              <div className="menu-divider" />
              <button className="menu-item" type="button" onClick={() => { setCreateCompOpen(true); setCompetitionMenuOpen(false) }}>
                <span>创建赛事<small>新建本地赛事工作区</small></span>
              </button>
            </div>
          </div>
          <button className="btn btn-ghost" type="button" onClick={() => showToast('请在设置页导出当前赛事')}>导出当前赛事</button>
          <button className="btn btn-danger" type="button" onClick={() => setDeleteCompOpen(true)}>删除当前赛事</button>
        </div>
        <img className="brand-logo" src="/logo.png" alt="logo" />
      </header>

      <div className="shell">
        <aside className="sidebar">
          <button className="btn btn-icon btn-ghost sidebar-toggle" type="button" onClick={() => setSidebarCollapsed((v) => !v)} aria-label="折叠菜单">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
              <path d="M4 7h16M4 12h16M4 17h16" />
            </svg>
          </button>
          {([
            ['teams', 'teams', '队伍管理'],
            ['prep', 'prep', '比赛准备'],
            ['scoreboard', 'scoreboard', '记分台'],
            ['logs', 'logs', '比赛日志'],
            ['settings', 'settings', '设置'],
          ] as const).map(([key, icon, label]) => (
            <button key={key} className={`nav-item ${view === key ? 'active' : ''}`} type="button" onClick={() => setView(key)}>
              <span className="nav-icon"><NavIcon name={icon} /></span>
              <span className="nav-label">{label}</span>
            </button>
          ))}
          <div className="sidebar-footer">
            <div className="sidebar-footer-brand">
              <svg className="github-logo" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
                <path fill="currentColor" d="M12 2C6.48 2 2 6.58 2 12.26c0 4.52 2.87 8.35 6.84 9.7.5.1.68-.22.68-.48 0-.24-.01-.87-.01-1.7-2.78.62-3.37-1.37-3.37-1.37-.45-1.18-1.11-1.5-1.11-1.5-.91-.64.07-.63.07-.63 1 .07 1.53 1.06 1.53 1.06.9 1.57 2.36 1.12 2.94.86.09-.67.35-1.12.63-1.38-2.22-.26-4.56-1.14-4.56-5.08 0-1.12.39-2.04 1.03-2.76-.1-.26-.45-1.32.1-2.75 0 0 .84-.27 2.75 1.05A9.3 9.3 0 0 1 12 7.5c.85 0 1.7.12 2.5.34 1.9-1.32 2.74-1.05 2.74-1.05.55 1.43.2 2.49.1 2.75.64.72 1.03 1.64 1.03 2.76 0 3.95-2.34 4.81-4.57 5.07.36.32.68.94.68 1.9 0 1.38-.01 2.49-.01 2.83 0 .26.18.59.69.48A10.03 10.03 0 0 0 22 12.26C22 6.58 17.52 2 12 2z"/>
              </svg>
              <div className="sidebar-footer-text">
                <div>GitHub 项目地址</div>
                <a
                  className="github-link"
                  href={appInfo?.githubUrl || 'https://github.com/zluoshui/Integrated-Basketball-Game-Information-Management-Software'}
                  target="_blank"
                  rel="noreferrer"
                  onClick={(e) => e.stopPropagation()}
                >
                  {(appInfo?.githubUrl || 'https://github.com/zluoshui/Integrated-Basketball-Game-Information-Management-Software').replace(/^https?:\/\//, '')}
                </a>
              </div>
            </div>
            <button
              className="btn btn-ghost sidebar-update-btn"
              type="button"
              onClick={(e) => {
                e.stopPropagation()
                withToast(async () => {
                  const result = await api.checkUpdate()
                  showToast(result.message)
                })
              }}
            >
              检查更新
            </button>
          </div>
        </aside>

        <main className="content">
          {error && <div className="preview-banner"><strong>提示</strong>：{error}</div>}

          {view === 'teams' && (
            <section className="view active">
              <div className="page-header">
                <div>
                  <h1 className="page-title">队伍管理</h1>
                  <p className="page-subtitle">先选队伍，再管理该队球员。点击球员查看/编辑资料与照片。</p>
                </div>
                <div className="page-actions">
                  <button className="btn btn-primary" type="button" onClick={() => setCreateTeamOpen(true)}>新建队伍</button>
                </div>
              </div>
              <div className="grid-3">
                <div className="card card-pad stack">
                  <div className="section-title">队伍</div>
                  <div className="list">
                    {teams.map((team) => (
                      <button key={team.id} className={`list-item ${team.id === selectedTeamId ? 'active' : ''} ${team.status === '停用' ? 'disabled' : ''}`} type="button" onClick={() => setSelectedTeamId(team.id)}>
                        <div className="avatar">{team.name.slice(0, 1)}</div>
                        <div className="item-main">
                          <div className="item-title">{team.name}</div>
                          <div className="item-sub">{team.note || '无备注'} · {team.playerCount} 人</div>
                        </div>
                        <span className={`badge ${team.status === '启用' ? 'badge-green' : 'badge-orange'}`}>{team.status}</span>
                      </button>
                    ))}
                  </div>
                  <button className="btn footer-add" type="button" onClick={() => setCreateTeamOpen(true)}>+ 新建队伍</button>
                  {selectedTeam && selectedTeam.status === '启用' && (
                    <button className="btn btn-danger" type="button" onClick={() => withToast(async () => {
                      await api.disableTeam(selectedTeam.id)
                      await refreshShell()
                    }, '队伍已停用')}>停用当前队伍</button>
                  )}
                </div>

                <div className="card card-pad stack">
                  <div className="section-title">{selectedTeam?.name || '队伍'} · 球员</div>
                  <div className="list">
                    {players.map((player) => (
                      <button key={player.id} className={`list-item ${player.id === selectedPlayerId ? 'active' : ''}`} type="button" onClick={() => setSelectedPlayerId(player.id)}>
                        <div className="avatar">{player.name.slice(0, 1)}</div>
                        <div className="item-main">
                          <div className="item-title">{player.name}</div>
                          <div className="item-sub">{player.studentNumber} · {player.customFields['位置'] || '未填位置'}</div>
                        </div>
                        <span className={`badge ${player.status === '在队' ? 'badge-blue' : 'badge-orange'}`}>{player.status}</span>
                      </button>
                    ))}
                  </div>
                  <button className="btn footer-add" type="button" onClick={() => setCreatePlayerOpen(true)}>+ 新增球员</button>
                </div>

                <div className="card card-pad">
                  <div className="section-title">球员详情</div>
                  {!selectedPlayer ? (
                    <div className="muted">请选择球员</div>
                  ) : (
                    <PlayerEditor
                      player={selectedPlayer}
                      fields={fields}
                      teamName={selectedTeam?.name || ''}
                      onSave={async (payload) => withToast(async () => {
                        await api.updatePlayer(selectedPlayer.id, payload)
                        await refreshAfterMutation()
                      }, '球员已保存')}
                      onPhoto={async (file) => withToast(async () => {
                        await api.uploadPhoto(selectedPlayer.id, file)
                        await refreshAfterMutation()
                      }, '照片已更新')}
                    />
                  )}
                </div>
              </div>
            </section>
          )}

          {view === 'prep' && (
            <section className="view active">
              <div className="page-header">
                <div>
                  <h1 className="page-title">比赛准备</h1>
                  <p className="page-subtitle">创建比赛、编辑参赛名单。此处比赛选择与记分台、日志互不影响。</p>
                </div>
                <div className="page-actions">
                  <button className="btn btn-primary" type="button" onClick={() => setCreateMatchOpen(true)}>创建比赛</button>
                </div>
              </div>
              <div className="prep-layout">
                <div className="card card-pad toolbar-card">
                  <div className="toolbar-group">
                    <div className="field">
                      <label>准备中的比赛</label>
                      <select value={prepMatchId} onChange={(e) => setPrepMatchId(e.target.value)}>
                        {matches.map((m) => <option key={m.id} value={m.id}>{m.scheduledAt || '未填日期'} · {m.name} · {m.homeTeamName} vs {m.awayTeamName}</option>)}
                      </select>
                    </div>
                    <div className="field">
                      <label>状态</label>
                      <div className="badge badge-orange">{prepMatch?.status || '无比赛'}</div>
                    </div>
                  </div>
                </div>

                {prepMatch && (
                  <RosterEditor
                    match={prepMatch}
                    teams={teams}
                    playersAllTeamScoped={players}
                    homeRoster={homeRoster}
                    awayRoster={awayRoster}
                    onChanged={async () => {
                      await refreshAfterMutation()
                    }}
                    showToast={showToast}
                  />
                )}
              </div>
            </section>
          )}

          {view === 'scoreboard' && (
            <section className="view active">
              <div className="page-header">
                <div>
                  <h1 className="page-title">记分台</h1>
                  <p className="page-subtitle">独立选择记分比赛。先点两侧场上球员，再点事件按钮。</p>
                </div>
                <div className="page-actions">
                  <div className="field">
                    <label>记分比赛</label>
                    <select value={scoreMatchId} onChange={(e) => setScoreMatchId(e.target.value)}>
                      {matches.map((m) => <option key={m.id} value={m.id}>{m.scheduledAt || '未填日期'} · {m.name} · {m.homeTeamName} vs {m.awayTeamName}</option>)}
                    </select>
                  </div>
                </div>
              </div>

              {!scoreboard ? <div className="muted">请选择比赛</div> : (
                <div className="scoreboard-layout">
                  <div className="side-panel home">
                    <h3>{scoreboard.homeTeamName} · 场上</h3>
                    {scoreboard.homeOnCourt.map((p) => (
                      <button key={p.playerId} className={`player-card ${selectedOnCourtId === p.playerId ? 'selected' : ''}`} type="button" onClick={() => setSelectedOnCourtId(p.playerId)}>
                        <div className="jersey home">{p.jerseyNumber || '-'}</div>
                        <div>
                          <div className="item-title">{p.name}</div>
                          <div className="stats stats-full">
                            {STAT_LABELS.map((label) => (
                              <div className="stat-chip" key={label}><strong>{p[label]}</strong>{label}</div>
                            ))}
                          </div>
                        </div>
                      </button>
                    ))}
                  </div>

                  <div className="center-panel">
                    <div className="clock-card">
                      <div className="tiny">第 {scoreboard.currentPeriod} / {scoreboard.periodCount} 节 · {scoreboard.status}</div>
                      <div className="clock-time">{formatClock(scoreboard.remainingSeconds)}</div>
                      <div className="clock-meta">
                        <span className="badge badge-green">{scoreboard.status}</span>
                        <span className="badge">全场犯规 {scoreboard.homeFouls}:{scoreboard.awayFouls}</span>
                        <span className="badge">暂停 {scoreboard.homeTimeouts}:{scoreboard.awayTimeouts}</span>
                        <span className="badge">本节犯规 {scoreboard.homePeriodFouls}:{scoreboard.awayPeriodFouls}</span>
                      </div>
                      <div className="score-line">
                        <div className="score-team home">{scoreboard.homeTeamName}</div>
                        <div className="score-value">{scoreboard.homeScore} - {scoreboard.awayScore}</div>
                        <div className="score-team away">{scoreboard.awayTeamName}</div>
                      </div>
                    </div>

                    <div className="control-card">
                      <div className="section-title">比赛控制</div>
                      <div className="control-row">
                        <button className="btn btn-lg" type="button" onClick={() => withToast(async () => {
                          await api.clock(scoreMatchId, 'start')
                          await refreshAfterMutation()
                        }, '已开始/继续')}>开始/继续</button>
                        <button className="btn btn-lg btn-soft" type="button" onClick={() => withToast(async () => {
                          await api.clock(scoreMatchId, 'pause')
                          await refreshAfterMutation()
                          setPauseOpen(true)
                        }, '已暂停')}>暂停</button>
                        <button className="btn btn-lg" type="button" onClick={() => withToast(async () => {
                          await api.clock(scoreMatchId, 'reset')
                          await refreshAfterMutation()
                        }, '已重置本节')}>重置本节</button>
                        <button className="btn btn-lg" type="button" onClick={() => withToast(async () => {
                          await api.clock(scoreMatchId, 'next')
                          await refreshAfterMutation()
                        }, '已进入下一节')}>下一节</button>
                        <button className="btn btn-lg btn-danger" type="button" onClick={() => setEndConfirmOpen(true)}>结束比赛</button>
                      </div>
                    </div>

                    <div className="event-card">
                      <div className="selected-player-tip">
                        {selectedOnCourt ? `当前记录球员：#${selectedOnCourt.jerseyNumber} ${selectedOnCourt.name}` : '请选择场上球员'}
                      </div>
                      <div className="section-title">技术统计</div>
                      <div className="event-grid">
                        {[
                          ['+1', '得分', 1],
                          ['+2', '得分', 2],
                          ['+3', '得分', 3],
                          ['犯规', '犯规', 0],
                          ['篮板', '篮板', 0],
                          ['助攻', '助攻', 0],
                          ['抢断', '抢断', 0],
                          ['盖帽', '盖帽', 0],
                          ['失误', '失误', 0],
                        ].map(([label, kind, points]) => (
                          <button key={label as string} className={`btn btn-xl ${String(label).startsWith('+') ? 'btn-primary' : ''}`} type="button" onClick={() => withToast(async () => {
                            if (!selectedOnCourt) throw new Error('请先选择场上球员')
                            await api.recordEvent(scoreMatchId, { playerId: selectedOnCourt.playerId, kind, points })
                            await refreshAfterMutation()
                          }, `${label} 已记录`)}>{label as string}</button>
                        ))}
                      </div>
                      <SubstitutionBar
                        scoreboard={scoreboard}
                        selectedOnCourt={selectedOnCourt}
                        onSubmit={async (incomingPlayerId) => withToast(async () => {
                          if (!selectedOnCourt) throw new Error('请先选择要换下的场上球员')
                          await api.substitute(scoreMatchId, {
                            outgoingPlayerId: selectedOnCourt.playerId,
                            incomingPlayerId,
                          })
                          await refreshAfterMutation()
                        }, '换人完成')}
                      />
                    </div>
                  </div>

                  <div className="side-panel away">
                    <h3>{scoreboard.awayTeamName} · 场上</h3>
                    {scoreboard.awayOnCourt.map((p) => (
                      <button key={p.playerId} className={`player-card ${selectedOnCourtId === p.playerId ? 'selected' : ''}`} type="button" onClick={() => setSelectedOnCourtId(p.playerId)}>
                        <div className="jersey away">{p.jerseyNumber || '-'}</div>
                        <div>
                          <div className="item-title">{p.name}</div>
                          <div className="stats stats-full">
                            {STAT_LABELS.map((label) => (
                              <div className="stat-chip" key={label}><strong>{p[label]}</strong>{label}</div>
                            ))}
                          </div>
                        </div>
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </section>
          )}

          {view === 'logs' && (
            <section className="view active">
              <div className="page-header">
                <div>
                  <h1 className="page-title">比赛日志</h1>
                  <p className="page-subtitle">统一时间轴按发生时间排序；主客事件分列显示，互不重叠。右键可作废。</p>
                </div>
                <div className="page-actions">
                  <button className="btn btn-ghost" type="button" onClick={() => withToast(async () => {
                    if (!logMatchId) throw new Error('请先选择比赛')
                    const result = await api.exportCsv(logMatchId)
                    showToast(`CSV 已导出：${result.path}`)
                  })}>导出 CSV</button>
                  <button className="btn btn-ghost" type="button" onClick={() => withToast(async () => {
                    if (!logMatchId) throw new Error('请先选择比赛')
                    const result = await api.exportJson([logMatchId])
                    showToast(`JSON 已导出到：${result.directory}`)
                  })}>导出 JSON</button>
                </div>
              </div>
              <div className="logs-layout">
                <div className="card card-pad">
                  <div className="section-title">历史比赛</div>
                  <table className="table">
                    <thead>
                      <tr>
                        <th>比赛</th>
                        <th>对阵</th>
                        <th>比分</th>
                        <th>状态</th>
                        <th>事件</th>
                      </tr>
                    </thead>
                    <tbody>
                      {matches.map((m) => (
                        <tr
                          key={m.id}
                          className={m.id === logMatchId ? 'active' : ''}
                          onClick={() => setLogMatchId(m.id)}
                          onContextMenu={(ev) => {
                            ev.preventDefault()
                            setContextMenu({ x: ev.clientX, y: ev.clientY, match: m })
                          }}
                        >
                          <td>{m.name}</td>
                          <td>{m.homeTeamName} vs {m.awayTeamName}</td>
                          <td><strong>{m.homeScore} - {m.awayScore}</strong></td>
                          <td><span className={`badge ${m.status === '进行中' ? 'badge-green' : 'badge-orange'}`}>{m.status}</span></td>
                          <td>
                            <div>{m.eventCount}</div>
                            <div className="tiny">{m.scheduledAt || '未填日期'}</div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="card">
                  <div className="card-pad" style={{ borderBottom: '1px solid var(--line)', display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                    <div>
                      <strong>{logMatch ? `${logMatch.homeTeamName} vs ${logMatch.awayTeamName} · ${logMatch.name}` : '未选择比赛'}</strong>
                      <div className="tiny">时间轴最上方最新 · 主客分列 · 中立事件双侧显示</div>
                    </div>
                    <div className="page-actions">
                      <select value={eventFilterKind} onChange={(e) => setEventFilterKind(e.target.value)}>
                        {['全部', '得分', '犯规', '篮板', '助攻', '抢断', '盖帽', '失误', '申请暂停', '换人', '计时控制', '名单审计'].map((k) => (
                          <option key={k} value={k}>{k === '全部' ? '全部事件' : k}</option>
                        ))}
                      </select>
                      <select value={eventFilterPlayerId} onChange={(e) => setEventFilterPlayerId(e.target.value)}>
                        <option value="全部">全部球员</option>
                        {logPlayerOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                      </select>
                    </div>
                  </div>
                  <div className="dual-log">
                    <div className="dual-log-col home">
                      <div className="log-col-title">主队 · {logMatch?.homeTeamName || ''}</div>
                    </div>
                    <div className="dual-log-col away">
                      <div className="log-col-title">客队 · {logMatch?.awayTeamName || ''}</div>
                    </div>
                  </div>
                  <div className="timeline-list">
                    {filteredEvents.map((e) => {
                      const isNeutral = e.kind === '计时控制' || e.kind === '名单审计'
                      const filterPlayer = eventFilterPlayerId !== '全部'
                      const showHome = filterPlayer
                        ? (e.playerId === eventFilterPlayerId && (e.side === '主队' || isNeutral))
                        : (e.side === '主队' || isNeutral)
                      const showAway = filterPlayer
                        ? (e.playerId === eventFilterPlayerId && (e.side === '客队' || isNeutral))
                        : (e.side === '客队' || isNeutral)
                      return (
                        <div className="timeline-row" key={e.id}>
                          <div className="timeline-side">
                            {showHome ? (
                              <LogCard event={e} onContextMenu={(ev) => {
                                ev.preventDefault()
                                setContextMenu({ x: ev.clientX, y: ev.clientY, event: e })
                              }} />
                            ) : <div className="log-slot empty" />}
                          </div>
                          <div className="timeline-axis">
                            <div className="timeline-dot" />
                            <div className="timeline-time">第{e.period}节 {formatClock(e.clockSecondsRemaining)}</div>
                          </div>
                          <div className="timeline-side">
                            {showAway ? (
                              <LogCard event={e} onContextMenu={(ev) => {
                                ev.preventDefault()
                                setContextMenu({ x: ev.clientX, y: ev.clientY, event: e })
                              }} />
                            ) : <div className="log-slot empty" />}
                          </div>
                        </div>
                      )
                    })}
                  </div>
                </div>
              </div>
            </section>
          )}

          {view === 'settings' && (
            <section className="view active">
              <div className="page-header">
                <div>
                  <h1 className="page-title">设置</h1>
                  <p className="page-subtitle">管理球员字段、导出目录，以及赛事/日志导入导出。</p>
                </div>
              </div>
              <div className="grid-2">
                <div className="card card-pad stack">
                  <div className="section-title">球员字段管理</div>
                  <div className="list">
                    {fields.map((field) => (
                      <div className="field-row" key={field.id}>
                        <div>
                          <div className="item-title">{field.name}</div>
                          <div className="item-sub">{field.fieldType === 'Number' ? '数字' : '文本'} · {field.isRequired ? '必填' : '选填'}</div>
                        </div>
                        <span className={`badge ${field.isRequired ? 'badge-orange' : 'badge-blue'}`}>{field.isRequired ? '必填' : '选填'}</span>
                        <button className="btn btn-danger" type="button" onClick={() => withToast(async () => {
                          await api.deletePlayerField(field.id)
                          await refreshShell()
                          if (selectedTeamId) await refreshPlayers(selectedTeamId)
                        }, `已删除字段：${field.name}`)}>删除</button>
                      </div>
                    ))}
                  </div>
                  <FieldCreator onCreate={async (name, fieldType, isRequired) => withToast(async () => {
                    await api.createPlayerField(name, fieldType, isRequired)
                    await refreshShell()
                    if (selectedTeamId) await refreshPlayers(selectedTeamId)
                  }, `已添加字段：${name}`)} />
                </div>

                <div className="card card-pad stack">
                  <div className="section-title">导入 / 导出</div>
                  <ExportSettingsPanel
                    logMatchId={logMatchId}
                    matches={matches}
                    onImported={async () => {
                      await refreshShell()
                      if (logMatchId) await refreshEvents(logMatchId)
                    }}
                    showToast={showToast}
                    withToast={withToast}
                  />
                </div>
              </div>
            </section>
          )}
        </main>
      </div>

      {createCompOpen && (
        <Modal title="创建赛事" onClose={() => setCreateCompOpen(false)}>
          <CreateCompetitionForm onSubmit={async (name, id) => withToast(async () => {
            await api.createCompetition(name, id)
            await refreshShell({ resetSelection: true })
            setCreateCompOpen(false)
          }, '赛事已创建')} />
        </Modal>
      )}
      {createTeamOpen && (
        <Modal title="新建队伍" onClose={() => setCreateTeamOpen(false)}>
          <CreateTeamForm onSubmit={async (name, note) => withToast(async () => {
            const team = await api.createTeam(name, note)
            await refreshAfterMutation()
            setSelectedTeamId(team.id)
            setCreateTeamOpen(false)
          }, '队伍已创建')} />
        </Modal>
      )}
      {createPlayerOpen && selectedTeam && (
        <Modal title="新增球员" onClose={() => setCreatePlayerOpen(false)}>
          <CreatePlayerForm
            teamId={selectedTeam.id}
            fields={fields}
            onSubmit={async (payload) => withToast(async () => {
              const player = await api.createPlayer(payload)
              await refreshAfterMutation()
              setSelectedPlayerId(player.id)
              setCreatePlayerOpen(false)
            }, '球员已创建')}
          />
        </Modal>
      )}
      {createMatchOpen && (
        <Modal title="创建比赛" onClose={() => setCreateMatchOpen(false)}>
          <CreateMatchForm
            teams={teams.filter((t) => t.status === '启用')}
            onSubmit={async (payload) => withToast(async () => {
              const match = await api.createMatch(payload)
              await refreshAfterMutation()
              setPrepMatchId(match.id)
              setCreateMatchOpen(false)
            }, '比赛已创建')}
          />
        </Modal>
      )}
      {endConfirmOpen && (
        <Modal title="结束比赛" onClose={() => setEndConfirmOpen(false)}>
          <div className="stack">
            <div className="muted">确认结束当前记分比赛吗？结束后不能继续计时或记录事件。</div>
            <div className="page-actions">
              <button className="btn btn-ghost" type="button" onClick={() => setEndConfirmOpen(false)}>取消</button>
              <button className="btn btn-danger" type="button" onClick={() => withToast(async () => {
                await api.clock(scoreMatchId, 'end')
                await refreshAfterMutation()
                setEndConfirmOpen(false)
              }, '比赛已结束')}>确认结束</button>
            </div>
          </div>
        </Modal>
      )}
      {pauseOpen && scoreboard && (
        <Modal title="暂停归属" onClose={() => setPauseOpen(false)}>
          <PauseAttributionForm
            scoreboard={scoreboard}
            roster={roster}
            onSubmit={async (payload) => withToast(async () => {
              await api.recordTimeout(scoreMatchId, payload)
              await refreshAfterMutation()
              setPauseOpen(false)
            }, '暂停已记录')}
          />
        </Modal>
      )}
      {deleteCompOpen && current && (
        <Modal title="删除赛事" onClose={() => setDeleteCompOpen(false)}>
          <div className="stack">
            <div className="muted">确认删除赛事“{current.name}”（{current.id}）吗？此操作会删除该赛事工作区全部数据，且不可恢复。</div>
            <div className="page-actions">
              <button className="btn btn-ghost" type="button" onClick={() => setDeleteCompOpen(false)}>取消</button>
              <button className="btn btn-danger" type="button" onClick={() => withToast(async () => {
                await api.deleteCompetition(current.id)
                await refreshShell({ resetSelection: true })
                setDeleteCompOpen(false)
              }, '赛事已删除')}>确认删除</button>
            </div>
          </div>
        </Modal>
      )}
      {voidOpen && voidTarget && logMatchId && (
        <Modal title="作废事件" onClose={() => setVoidOpen(false)}>
          <VoidEventForm
            summary={`第 ${voidTarget.period} 节 · ${voidTarget.side} · ${voidTarget.playerName || ''} · ${voidTarget.kind}${voidTarget.points ? ` ${voidTarget.points}分` : ''}`}
            onSubmit={async (reason, operator) => withToast(async () => {
              await api.voidEvent(logMatchId, voidTarget.id, reason, operator)
              await refreshAfterMutation()
              setVoidOpen(false)
              setVoidTarget(null)
            }, '事件已作废')}
          />
        </Modal>
      )}

      {contextMenu && (
        <div className="context-menu" style={{ left: contextMenu.x, top: contextMenu.y }} onClick={(e) => e.stopPropagation()}>
          {contextMenu.event && (
            <button type="button" data-action="void" onClick={() => {
              setVoidTarget(contextMenu.event!)
              setVoidOpen(true)
              setContextMenu(null)
            }}>作废此日志</button>
          )}
          {contextMenu.match && (
            <button type="button" data-action="void" onClick={() => withToast(async () => {
              if (contextMenu.match!.status !== '未开始') throw new Error('只能删除未开始的比赛')
              await api.deleteMatch(contextMenu.match!.id)
              await refreshShell({ resetSelection: false })
              if (prepMatchId === contextMenu.match!.id || scoreMatchId === contextMenu.match!.id || logMatchId === contextMenu.match!.id) {
                await refreshShell({ resetSelection: true })
              } else {
                await refreshAfterMutation()
              }
              setContextMenu(null)
            }, '比赛已删除')}>删除未开始比赛</button>
          )}
        </div>
      )}

      <div className={`toast ${toast ? 'show' : ''}`}>{toast}</div>
    </div>
  )
}

function NavIcon({ name }: { name: 'teams' | 'prep' | 'scoreboard' | 'logs' | 'settings' }) {
  const common = {
    viewBox: '0 0 24 24',
    width: 18,
    height: 18,
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true,
  }
  switch (name) {
    case 'teams':
      return (
        <svg {...common}>
          <circle cx="9" cy="8" r="3" />
          <circle cx="17" cy="9" r="2.5" />
          <path d="M3.5 19c.8-3 2.8-4.5 5.5-4.5S14 16 14.8 19" />
          <path d="M14.5 19c.5-2.1 1.8-3.2 3.8-3.2 1.3 0 2.3.5 3 1.4" />
        </svg>
      )
    case 'prep':
      return (
        <svg {...common}>
          <rect x="5" y="3.5" width="14" height="17" rx="2" />
          <path d="M8 8h8M8 12h8M8 16h5" />
        </svg>
      )
    case 'scoreboard':
      return (
        <svg {...common}>
          <circle cx="12" cy="12" r="8" />
          <path d="M12 7.5V12l3 2" />
        </svg>
      )
    case 'logs':
      return (
        <svg {...common}>
          <path d="M8 4.5h9a2 2 0 0 1 2 2V19a1.5 1.5 0 0 1-1.5 1.5H8.5A2.5 2.5 0 0 1 6 18V6.5A2 2 0 0 1 8 4.5z" />
          <path d="M9.5 9h7M9.5 12.5h7M9.5 16h4.5" />
        </svg>
      )
    case 'settings':
      return (
        <svg {...common}>
          <circle cx="12" cy="12" r="3" />
          <path d="M12 3.8v2.2M12 18v2.2M4.9 7.1l1.6 1.6M17.5 15.3l1.6 1.6M3.8 12h2.2M18 12h2.2M4.9 16.9l1.6-1.6M17.5 8.7l1.6-1.6" />
        </svg>
      )
  }
}

function LogCard({ event, onContextMenu }: { event: MatchEvent; onContextMenu: (e: React.MouseEvent) => void }) {
  return (
    <div className={`log-event ${event.isVoided ? 'voided' : ''}`} onContextMenu={onContextMenu} style={{ marginBottom: 10 }}>
      <div className="time">第{event.period}节 {formatClock(event.clockSecondsRemaining)}</div>
      <div className="body">{event.playerName ? `${event.playerName} · ` : ''}{event.kind}{event.points ? ` ${event.points}分` : ''}</div>
      {event.note && <div className="note">{event.note}</div>}
      {event.isVoided ? <div className="note">已作废{event.voidReason ? `：${event.voidReason}` : ''}</div> : <div className="note" style={{ color: '#94a3b8' }}>右键可作废</div>}
    </div>
  )
}

function Modal({ title, children, onClose }: { title: string; children: React.ReactNode; onClose: () => void }) {
  return (
    <div className="modal-backdrop open" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button className="btn btn-icon btn-ghost" type="button" onClick={onClose}>×</button>
        </div>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  )
}

function CreateCompetitionForm({ onSubmit }: { onSubmit: (name: string, id: string) => void }) {
  const [name, setName] = useState('')
  const [id, setId] = useState('')
  return (
    <div className="stack">
      <div className="field"><label>赛事名称</label><input value={name} onChange={(e) => setName(e.target.value)} /></div>
      <div className="field"><label>赛事 ID（英文字母与数字）</label><input value={id} onChange={(e) => setId(e.target.value)} /></div>
      <div className="page-actions"><button className="btn btn-primary" type="button" onClick={() => onSubmit(name, id)}>创建</button></div>
    </div>
  )
}

function CreateTeamForm({ onSubmit }: { onSubmit: (name: string, note: string) => void }) {
  const [name, setName] = useState('')
  const [note, setNote] = useState('')
  return (
    <div className="stack">
      <div className="field"><label>队伍名称</label><input value={name} onChange={(e) => setName(e.target.value)} /></div>
      <div className="field"><label>备注</label><textarea value={note} onChange={(e) => setNote(e.target.value)} /></div>
      <div className="page-actions"><button className="btn btn-primary" type="button" onClick={() => onSubmit(name, note)}>保存</button></div>
    </div>
  )
}

function CreatePlayerForm({ teamId, fields, onSubmit }: { teamId: string; fields: PlayerField[]; onSubmit: (payload: unknown) => void }) {
  const [name, setName] = useState('')
  const [studentNumber, setStudentNumber] = useState('')
  const [note, setNote] = useState('')
  const [customFields, setCustomFields] = useState<Record<string, string>>({})
  return (
    <div className="form-grid">
      <div className="field"><label>姓名</label><input value={name} onChange={(e) => setName(e.target.value)} /></div>
      <div className="field"><label>学号</label><input value={studentNumber} onChange={(e) => setStudentNumber(e.target.value)} /></div>
      {fields.map((field) => (
        <div className="field" key={field.id}>
          <label>{field.name}</label>
          <input value={customFields[field.name] || ''} onChange={(e) => setCustomFields((prev) => ({ ...prev, [field.name]: e.target.value }))} />
        </div>
      ))}
      <div className="field full"><label>备注</label><textarea value={note} onChange={(e) => setNote(e.target.value)} /></div>
      <div className="page-actions full">
        <button className="btn btn-primary" type="button" onClick={() => onSubmit({ name, studentNumber, teamId, note, status: '在队', customFields })}>保存</button>
      </div>
    </div>
  )
}

function CreateMatchForm({ teams, onSubmit }: { teams: Team[]; onSubmit: (payload: unknown) => void }) {
  const [name, setName] = useState('')
  const [homeTeamId, setHomeTeamId] = useState(teams[0]?.id || '')
  const [awayTeamId, setAwayTeamId] = useState(teams[1]?.id || teams[0]?.id || '')
  const [periodCount, setPeriodCount] = useState(4)
  const [periodLengthMinutes, setPeriodLengthMinutes] = useState(10)
  const [location, setLocation] = useState('')
  const [scheduledAt, setScheduledAt] = useState(new Date().toISOString().slice(0, 10))
  return (
    <div className="form-grid">
      <div className="field full"><label>比赛名</label><input value={name} onChange={(e) => setName(e.target.value)} /></div>
      <div className="field"><label>主队</label><select value={homeTeamId} onChange={(e) => setHomeTeamId(e.target.value)}>{teams.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select></div>
      <div className="field"><label>客队</label><select value={awayTeamId} onChange={(e) => setAwayTeamId(e.target.value)}>{teams.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}</select></div>
      <div className="field full"><label>比赛日期</label><input type="date" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} required /></div>
      <div className="field"><label>节数</label><input type="number" value={periodCount} onChange={(e) => setPeriodCount(Number(e.target.value) || 4)} /></div>
      <div className="field"><label>每节分钟</label><input type="number" value={periodLengthMinutes} onChange={(e) => setPeriodLengthMinutes(Number(e.target.value) || 10)} /></div>
      <div className="field full"><label>地点</label><input value={location} onChange={(e) => setLocation(e.target.value)} /></div>
      <div className="page-actions full">
        <button className="btn btn-primary" type="button" onClick={() => {
          if (!scheduledAt) return
          onSubmit({ name, homeTeamId, awayTeamId, periodCount, periodLengthMinutes, location, scheduledAt })
        }}>创建</button>
      </div>
    </div>
  )
}

function PauseAttributionForm({
  scoreboard,
  roster,
  onSubmit,
}: {
  scoreboard: Scoreboard
  roster: Roster[]
  onSubmit: (payload: { mode: string; side?: string; playerId?: string; note?: string }) => void
}) {
  const [side, setSide] = useState<'主队' | '客队'>('主队')
  const [playerId, setPlayerId] = useState('')
  const players = roster.filter((r) => r.side === side)
  useEffect(() => {
    setPlayerId(players[0]?.playerId || '')
  }, [side, roster])

  return (
    <div className="stack">
      <div className="muted">比赛已暂停。请选择本次暂停归属，或记为其它暂停（无归属）。</div>
      <button className="btn btn-soft" type="button" onClick={() => onSubmit({ mode: 'other', note: '其它暂停' })}>其它暂停（无归属）</button>
      <div className="section-title">归属面板</div>
      <div className="field">
        <label>队伍</label>
        <select value={side} onChange={(e) => setSide(e.target.value as '主队' | '客队')}>
          <option value="主队">{scoreboard.homeTeamName}</option>
          <option value="客队">{scoreboard.awayTeamName}</option>
        </select>
      </div>
      <div className="field">
        <label>归属球员</label>
        <select value={playerId} onChange={(e) => setPlayerId(e.target.value)}>
          {players.map((p) => <option key={p.id} value={p.playerId}>#{p.jerseyNumber} {p.playerName}</option>)}
        </select>
      </div>
      <div className="page-actions">
        <button className="btn btn-ghost" type="button" onClick={() => onSubmit({ mode: 'team', side, note: '球队暂停' })}>记为球队暂停</button>
        <button className="btn btn-primary" type="button" onClick={() => {
          if (!playerId) return
          onSubmit({ mode: 'player', playerId, note: '申请暂停' })
        }}>归属到球员</button>
      </div>
    </div>
  )
}

function VoidEventForm({ summary, onSubmit }: { summary: string; onSubmit: (reason: string, operator: string) => void }) {
  const [reason, setReason] = useState('')
  const [operator, setOperator] = useState('')
  return (
    <div className="stack">
      <div className="muted">{summary}</div>
      <div className="field"><label>作废原因</label><input value={reason} onChange={(e) => setReason(e.target.value)} /></div>
      <div className="field"><label>操作人</label><input value={operator} onChange={(e) => setOperator(e.target.value)} /></div>
      <div className="page-actions"><button className="btn btn-danger" type="button" onClick={() => onSubmit(reason, operator)}>确认作废</button></div>
    </div>
  )
}

function FieldCreator({ onCreate }: { onCreate: (name: string, fieldType: string, isRequired: boolean) => void }) {
  const [name, setName] = useState('')
  const [fieldType, setFieldType] = useState('Text')
  const [isRequired, setIsRequired] = useState(false)
  return (
    <div className="form-grid" style={{ marginTop: 8 }}>
      <div className="field"><label>字段名称</label><input value={name} onChange={(e) => setName(e.target.value)} placeholder="例如：年级" /></div>
      <div className="field"><label>类型</label><select value={fieldType} onChange={(e) => setFieldType(e.target.value)}><option value="Text">文本</option><option value="Number">数字</option></select></div>
      <div className="field"><label>是否必填</label><select value={isRequired ? 'true' : 'false'} onChange={(e) => setIsRequired(e.target.value === 'true')}><option value="false">否</option><option value="true">是</option></select></div>
      <div className="field" style={{ justifyContent: 'flex-end' }}><label>&nbsp;</label><button className="btn btn-primary" type="button" onClick={() => { onCreate(name, fieldType, isRequired); setName('') }}>添加字段</button></div>
    </div>
  )
}

function PlayerEditor({
  player,
  fields,
  teamName,
  onSave,
  onPhoto,
}: {
  player: Player
  fields: PlayerField[]
  teamName: string
  onSave: (payload: unknown) => void
  onPhoto: (file: File) => void
}) {
  const [name, setName] = useState(player.name)
  const [studentNumber, setStudentNumber] = useState(player.studentNumber)
  const [status, setStatus] = useState(player.status)
  const [note, setNote] = useState(player.note)
  const [customFields, setCustomFields] = useState(player.customFields)

  useEffect(() => {
    setName(player.name)
    setStudentNumber(player.studentNumber)
    setStatus(player.status)
    setNote(player.note)
    setCustomFields(player.customFields)
  }, [player])

  return (
    <div className="player-detail">
      <div className="player-detail-top">
        <label className="photo-box large" style={{ cursor: 'pointer' }}>
          {player.photoUrl ? <img src={player.photoUrl} alt={player.name} /> : '添加照片'}
          <input type="file" accept="image/*" hidden onChange={(e) => {
            const file = e.target.files?.[0]
            if (file) onPhoto(file)
          }} />
        </label>
        <div className="stack" style={{ flex: 1 }}>
          <div>
            <div className="item-title" style={{ fontSize: 22 }}>{player.name}</div>
            <div className="item-sub">{player.studentNumber}</div>
          </div>
          <div className="page-actions">
            <span className="badge badge-blue">{player.status}</span>
            <span className="badge">{player.customFields['位置'] || '未填位置'}</span>
          </div>
        </div>
      </div>
      <div className="form-grid">
        <div className="field"><label>姓名</label><input value={name} onChange={(e) => setName(e.target.value)} /></div>
        <div className="field"><label>学号</label><input value={studentNumber} onChange={(e) => setStudentNumber(e.target.value)} /></div>
        <div className="field"><label>状态</label><select value={status} onChange={(e) => setStatus(e.target.value)}><option>在队</option><option>离队</option><option>停用</option></select></div>
        <div className="field"><label>所属队伍</label><input value={teamName} disabled /></div>
        {fields.map((field) => (
          <div className="field" key={field.id}>
            <label>{field.name}</label>
            <input value={customFields[field.name] || ''} onChange={(e) => setCustomFields((prev) => ({ ...prev, [field.name]: e.target.value }))} />
          </div>
        ))}
        <div className="field full"><label>备注</label><textarea value={note} onChange={(e) => setNote(e.target.value)} /></div>
      </div>
      <div className="page-actions">
        <button className="btn btn-primary" type="button" onClick={() => onSave({
          name,
          studentNumber,
          teamId: player.teamId,
          note,
          status,
          customFields,
        })}>保存</button>
      </div>
    </div>
  )
}

function RosterEditor({
  match,
  homeRoster,
  awayRoster,
  onChanged,
  showToast,
}: {
  match: MatchSummary
  teams: Team[]
  playersAllTeamScoped: Player[]
  homeRoster: Roster[]
  awayRoster: Roster[]
  onChanged: () => Promise<void>
  showToast: (msg: string) => void
}) {
  const [side, setSide] = useState<'主队' | '客队'>('主队')
  const [teamPlayers, setTeamPlayers] = useState<Player[]>([])
  const [playerId, setPlayerId] = useState('')
  const [jersey, setJersey] = useState('')
  const [isStarter, setIsStarter] = useState(true)
  const [auditOperator, setAuditOperator] = useState('')
  const [auditNote, setAuditNote] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)

  const teamId = side === '主队' ? match.homeTeamId : match.awayTeamId

  useEffect(() => {
    if (!teamId) {
      setTeamPlayers([])
      return
    }
    api.players(teamId).then((list) => {
      setTeamPlayers(list.filter((p) => p.status !== '停用'))
      setPlayerId((prev) => list.some((p) => p.id === prev) ? prev : (list[0]?.id || ''))
    }).catch((e) => showToast(e.message))
  }, [teamId, side, showToast])

  const beginEdit = (row: Roster) => {
    setEditingId(row.id)
    setSide(row.side === '客队' ? '客队' : '主队')
    setPlayerId(row.playerId)
    setJersey(row.jerseyNumber)
    setIsStarter(row.isStarter)
  }

  const renderRows = (rows: Roster[], sideClass: 'home' | 'away') => (
    <div className="stack">
      {rows.map((r) => (
        <div className="roster-row" key={r.id}>
          <div className={`jersey ${sideClass}`}>{r.jerseyNumber || '-'}</div>
          <div>
            <div className="item-title">{r.playerName}</div>
            <div className="item-sub">{r.isStarter ? '首发' : '替补'} · {r.isOnCourt ? '在场' : '场下'}</div>
          </div>
          <button className="btn btn-ghost" type="button" onClick={() => beginEdit(r)}>编辑</button>
          <button className="btn btn-danger" type="button" onClick={async () => {
            try {
              await api.removeRoster(match.id, r.id, auditOperator, auditNote)
              if (editingId === r.id) setEditingId(null)
              await onChanged()
            } catch (e) {
              showToast(e instanceof Error ? e.message : String(e))
            }
          }}>移出</button>
        </div>
      ))}
    </div>
  )

  return (
    <>
      <div className="card card-pad">
        <div className="section-title">{editingId ? '更新名单' : '快速加入名单'}</div>
        <div className="form-grid">
          <div className="field"><label>阵营</label><select value={side} onChange={(e) => setSide(e.target.value as '主队' | '客队')}><option>主队</option><option>客队</option></select></div>
          <div className="field"><label>球员</label><select value={playerId} onChange={(e) => setPlayerId(e.target.value)}>{teamPlayers.map((p) => <option key={p.id} value={p.id}>{p.name} ({p.studentNumber})</option>)}</select></div>
          <div className="field"><label>球衣号</label><input value={jersey} onChange={(e) => setJersey(e.target.value)} /></div>
          <div className="field"><label>首发</label><select value={isStarter ? 'yes' : 'no'} onChange={(e) => setIsStarter(e.target.value === 'yes')}><option value="yes">是</option><option value="no">否</option></select></div>
          <div className="field"><label>审计操作人（赛中/赛后必填）</label><input value={auditOperator} onChange={(e) => setAuditOperator(e.target.value)} /></div>
          <div className="field"><label>审计备注</label><input value={auditNote} onChange={(e) => setAuditNote(e.target.value)} /></div>
        </div>
        <div className="page-actions" style={{ marginTop: 14 }}>
          <button className="btn btn-primary" type="button" onClick={async () => {
            try {
              const payload = { playerId, side, jerseyNumber: jersey, isStarter, auditOperator, auditNote }
              if (editingId) {
                await api.updateRoster(match.id, editingId, payload)
                setEditingId(null)
                showToast('名单已更新')
              } else {
                await api.addRoster(match.id, payload)
                showToast('已加入名单')
              }
              setJersey('')
              await onChanged()
            } catch (e) {
              showToast(e instanceof Error ? e.message : String(e))
            }
          }}>{editingId ? '保存更新' : '加入名单'}</button>
          {editingId && (
            <button className="btn btn-ghost" type="button" onClick={() => setEditingId(null)}>取消编辑</button>
          )}
        </div>
      </div>
      <div className="roster-columns">
        <div className="card card-pad roster-card">
          <div className="roster-head"><strong>主队 · {match.homeTeamName}</strong><span className="badge badge-blue">{homeRoster.filter((r) => r.isStarter).length} 首发</span></div>
          {renderRows(homeRoster, 'home')}
        </div>
        <div className="card card-pad roster-card">
          <div className="roster-head"><strong>客队 · {match.awayTeamName}</strong><span className="badge badge-red">{awayRoster.filter((r) => r.isStarter).length} 首发</span></div>
          {renderRows(awayRoster, 'away')}
        </div>
      </div>
    </>
  )
}

function ExportSettingsPanel({
  logMatchId,
  matches,
  onImported,
  showToast,
  withToast,
}: {
  logMatchId: string
  matches: MatchSummary[]
  onImported: () => Promise<void>
  showToast: (msg: string) => void
  withToast: (action: () => Promise<void>, success?: string) => Promise<void>
}) {
  const [exportDir, setExportDir] = useState('')
  const [competitionImportPath, setCompetitionImportPath] = useState('')
  const [competitionExportRoot, setCompetitionExportRoot] = useState('')
  const [selectedMatchIds, setSelectedMatchIds] = useState<string[]>([])

  useEffect(() => {
    api.getExportDirectory().then((r) => setExportDir(r.path)).catch((e) => showToast(e.message))
  }, [showToast])

  return (
    <div className="stack">
      <div className="field">
        <label>CSV / JSON 默认导出目录</label>
        <input value={exportDir} onChange={(e) => setExportDir(e.target.value)} placeholder="例如 D:\exports" />
      </div>
      <div className="page-actions">
        <button className="btn btn-primary" type="button" onClick={() => withToast(async () => {
          const result = await api.setExportDirectory(exportDir)
          setExportDir(result.path)
        }, '导出目录已保存')}>保存导出目录</button>
      </div>

      <div className="section-title" style={{ marginTop: 8 }}>比赛日志</div>
      <div className="list" style={{ maxHeight: 180, overflow: 'auto' }}>
        {matches.map((m) => {
          const checked = selectedMatchIds.includes(m.id) || (!selectedMatchIds.length && m.id === logMatchId)
          return (
            <label key={m.id} className="list-item" style={{ cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={selectedMatchIds.includes(m.id)}
                onChange={(e) => {
                  setSelectedMatchIds((prev) => e.target.checked ? [...prev, m.id] : prev.filter((id) => id !== m.id))
                }}
              />
              <div className="item-main">
                <div className="item-title">{m.name}</div>
                <div className="item-sub">{m.homeTeamName} {m.homeScore}-{m.awayScore} {m.awayTeamName}</div>
              </div>
              {checked && !selectedMatchIds.length && <span className="badge badge-blue">当前日志</span>}
            </label>
          )
        })}
      </div>
      <div className="page-actions">
        <button className="btn btn-ghost" type="button" onClick={() => withToast(async () => {
          const id = selectedMatchIds[0] || logMatchId
          if (!id) throw new Error('请选择比赛')
          const result = await api.exportCsv(id)
          showToast(`CSV：${result.path}`)
        })}>导出 CSV</button>
        <button className="btn btn-ghost" type="button" onClick={() => withToast(async () => {
          const ids = selectedMatchIds.length ? selectedMatchIds : (logMatchId ? [logMatchId] : [])
          if (!ids.length) throw new Error('请选择比赛')
          const result = await api.exportJson(ids)
          showToast(`JSON 目录：${result.directory}`)
        })}>批量导出 JSON</button>
        <label className="btn btn-soft" style={{ cursor: 'pointer' }}>
          导入 JSON
          <input type="file" accept="application/json,.json" multiple hidden onChange={(e) => {
            const files = e.target.files
            if (!files?.length) return
            withToast(async () => {
              const result = await api.importJsonFiles(files)
              await onImported()
              showToast(`导入成功 ${result.imported}，跳过 ${result.skipped}，拒绝 ${result.rejected.length}`)
            })
            e.target.value = ''
          }} />
        </label>
      </div>

      <div className="section-title" style={{ marginTop: 8 }}>赛事工作区</div>
      <div className="field">
        <label>导入赛事目录（含 competition.json 与 basketball.db）</label>
        <input value={competitionImportPath} onChange={(e) => setCompetitionImportPath(e.target.value)} placeholder="例如 D:\share\SpringCup2026" />
      </div>
      <div className="page-actions">
        <button className="btn btn-ghost" type="button" onClick={() => withToast(async () => {
          if (!competitionImportPath.trim()) throw new Error('请填写导入路径')
          await api.importCompetition(competitionImportPath.trim())
          await onImported()
        }, '赛事已导入并切换')}>导入赛事</button>
      </div>
      <div className="field">
        <label>导出当前赛事到目录</label>
        <input value={competitionExportRoot} onChange={(e) => setCompetitionExportRoot(e.target.value)} placeholder="例如 D:\share" />
      </div>
      <div className="page-actions">
        <button className="btn btn-ghost" type="button" onClick={() => withToast(async () => {
          if (!competitionExportRoot.trim()) throw new Error('请填写导出根目录')
          const result = await api.exportCompetition(competitionExportRoot.trim())
          showToast(`赛事已导出：${result.path}`)
        })}>导出当前赛事</button>
      </div>
      <div className="tiny">
        提示：桌面端可直接粘贴资源管理器路径。照片已保存到赛事工作区 photos/，会随赛事导出一起迁移。
      </div>
    </div>
  )
}

function SubstitutionBar({
  scoreboard,
  selectedOnCourt,
  onSubmit,
}: {
  scoreboard: Scoreboard
  selectedOnCourt: { playerId: string; side: string; name: string } | null
  onSubmit: (incomingPlayerId: string) => void
}) {
  const [bench, setBench] = useState<Roster[]>([])
  const [incoming, setIncoming] = useState('')

  useEffect(() => {
    api.roster(scoreboard.matchId).then((rows) => {
      const side = selectedOnCourt?.side
      const list = rows.filter((r) => r.side === side && !r.isOnCourt)
      setBench(list)
      setIncoming(list[0]?.playerId || '')
    }).catch(() => setBench([]))
  }, [scoreboard.matchId, selectedOnCourt?.side, scoreboard.homeOnCourt, scoreboard.awayOnCourt])

  return (
    <div className="sub-row">
      <select value={incoming} onChange={(e) => setIncoming(e.target.value)}>
        {bench.length === 0 && <option value="">无可换上球员</option>}
        {bench.map((r) => <option key={r.id} value={r.playerId}>换上：#{r.jerseyNumber} {r.playerName}</option>)}
      </select>
      <button className="btn btn-soft" type="button" onClick={() => onSubmit(incoming)}>替换</button>
    </div>
  )
}
