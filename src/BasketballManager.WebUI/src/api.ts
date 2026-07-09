export type AppInfo = {
  appName: string
  appNameEn: string
  version: string
  primaryAuthor: string
  secondaryAuthors: string
  githubUrl: string
  updateManifestUrl: string
}

export type UpdateCheckResult = {
  hasUpdate: boolean
  currentVersion: string
  latestVersion?: string | null
  releaseUrl?: string | null
  notes?: string | null
  message: string
}

export type Competition = { id: string; name: string; directoryPath: string; isCurrent: boolean }
export type Team = { id: string; name: string; note: string; status: string; playerCount: number }
export type PlayerField = { id: string; name: string; fieldType: string; isRequired: boolean; displayOrder: number }
export type Player = {
  id: string
  name: string
  studentNumber: string
  teamId?: string | null
  team: string
  note: string
  photoPath: string
  photoUrl: string
  status: string
  customFields: Record<string, string>
}
export type MatchSummary = {
  id: string
  name: string
  homeTeamName: string
  awayTeamName: string
  homeTeamId?: string | null
  awayTeamId?: string | null
  status: string
  homeScore: number
  awayScore: number
  eventCount: number
  rosterCount: number
  currentPeriod: number
  periodCount: number
  remainingSeconds: number
  scheduledAt?: string | null
  location: string
  note: string
}
export type Roster = {
  id: string
  matchId: string
  playerId: string
  playerName: string
  side: string
  jerseyNumber: string
  isStarter: boolean
  isOnCourt: boolean
}
export type OnCourtPlayer = {
  playerId: string
  name: string
  jerseyNumber: string
  side: string
  得分: number
  犯规: number
  篮板: number
  助攻: number
  抢断: number
  盖帽: number
  失误: number
  申请暂停: number
}
export type Scoreboard = {
  matchId: string
  name: string
  status: string
  currentPeriod: number
  periodCount: number
  remainingSeconds: number
  isClockRunning: boolean
  homeScore: number
  awayScore: number
  homeFouls: number
  awayFouls: number
  homeTimeouts: number
  awayTimeouts: number
  homePeriodFouls: number
  awayPeriodFouls: number
  homeTeamName: string
  awayTeamName: string
  homeOnCourt: OnCourtPlayer[]
  awayOnCourt: OnCourtPlayer[]
}
export type MatchEvent = {
  id: string
  matchId: string
  playerId?: string | null
  relatedPlayerId?: string | null
  playerName?: string | null
  side: string
  kind: string
  points: number
  period: number
  clockSecondsRemaining: number
  note: string
  isVoided: boolean
  voidReason: string
  voidedBy: string
  createdAt: string
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init)
  if (!response.ok) {
    let message = `请求失败 (${response.status})`
    try {
      const data = await response.json()
      message = data.message || data.Message || message
    } catch {
      /* ignore */
    }
    throw new Error(message)
  }
  if (response.status === 204) return undefined as T
  return response.json()
}

export const api = {
  health: () => request<{ ok: boolean; competitionId: string; competitionName: string; version?: string }>('/api/health'),
  appInfo: () => request<AppInfo>('/api/app/info'),
  checkUpdate: () => request<UpdateCheckResult>('/api/app/update-check'),
  competitions: () => request<Competition[]>('/api/competitions'),
  currentCompetition: () => request<Competition>('/api/competitions/current'),
  createCompetition: (name: string, competitionId: string) =>
    request<Competition>('/api/competitions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, competitionId }),
    }),
  switchCompetition: (competitionId: string) =>
    request<Competition>(`/api/competitions/${encodeURIComponent(competitionId)}/switch`, { method: 'POST' }),
  deleteCompetition: (competitionId: string) =>
    request<{ deleted: string; current: Competition }>(`/api/competitions/${encodeURIComponent(competitionId)}`, {
      method: 'DELETE',
    }),
  teams: () => request<Team[]>('/api/teams'),
  createTeam: (name: string, note = '') =>
    request<Team>('/api/teams', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, note }),
    }),
  disableTeam: (id: string) => request<Team>(`/api/teams/${id}/disable`, { method: 'POST' }),
  players: (teamId?: string) => request<Player[]>(teamId ? `/api/players?teamId=${teamId}` : '/api/players'),
  createPlayer: (payload: unknown) =>
    request<Player>('/api/players', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  updatePlayer: (id: string, payload: unknown) =>
    request<Player>(`/api/players/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  uploadPhoto: async (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<Player>(`/api/players/${id}/photo`, { method: 'POST', body: form })
  },
  playerFields: () => request<PlayerField[]>('/api/player-fields'),
  createPlayerField: (name: string, fieldType: string, isRequired: boolean) =>
    request<PlayerField>('/api/player-fields', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, fieldType, isRequired }),
    }),
  deletePlayerField: (id: string) => request<{ ok: boolean }>(`/api/player-fields/${id}`, { method: 'DELETE' }),
  matches: () => request<MatchSummary[]>('/api/matches'),
  createMatch: (payload: unknown) =>
    request<MatchSummary>('/api/matches', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  deleteMatch: (matchId: string) =>
    request<{ deleted: string }>(`/api/matches/${matchId}`, { method: 'DELETE' }),
  roster: (matchId: string) => request<Roster[]>(`/api/matches/${matchId}/roster`),
  addRoster: (matchId: string, payload: unknown) =>
    request<Roster>(`/api/matches/${matchId}/roster`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  updateRoster: (matchId: string, rosterId: string, payload: unknown) =>
    request<Roster>(`/api/matches/${matchId}/roster/${rosterId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  removeRoster: (matchId: string, rosterId: string, auditOperator?: string, auditNote?: string) => {
    const qs = new URLSearchParams()
    if (auditOperator) qs.set('auditOperator', auditOperator)
    if (auditNote) qs.set('auditNote', auditNote)
    const suffix = qs.toString() ? `?${qs}` : ''
    return request<{ ok: boolean }>(`/api/matches/${matchId}/roster/${rosterId}${suffix}`, { method: 'DELETE' })
  },
  scoreboard: (matchId: string) => request<Scoreboard>(`/api/scoreboard/${matchId}`),
  clock: (matchId: string, action: string) =>
    request<Scoreboard>(`/api/scoreboard/${matchId}/clock/${action}`, { method: 'POST' }),
  recordTimeout: (matchId: string, payload: { mode: string; side?: string; playerId?: string; note?: string }) =>
    request<{ scoreboard: Scoreboard }>(`/api/scoreboard/${matchId}/timeout`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  recordEvent: (matchId: string, payload: unknown) =>
    request<{ scoreboard: Scoreboard }>(`/api/scoreboard/${matchId}/events`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  substitute: (matchId: string, payload: unknown) =>
    request<{ scoreboard: Scoreboard }>(`/api/scoreboard/${matchId}/substitutions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }),
  events: (matchId: string) => request<MatchEvent[]>(`/api/matches/${matchId}/events`),
  voidEvent: (matchId: string, eventId: string, reason: string, operator = '') =>
    request<MatchEvent>(`/api/matches/${matchId}/events/${eventId}/void`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason, operator }),
    }),
  getExportDirectory: () => request<{ path: string }>('/api/settings/export-directory'),
  setExportDirectory: (path: string) =>
    request<{ path: string }>('/api/settings/export-directory', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path }),
    }),
  exportCsv: (matchId: string) =>
    request<{ path: string }>('/api/logs/export-csv', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ matchId }),
    }),
  exportJson: (matchIds: string[]) =>
    request<{ directory: string; files: string[] }>('/api/logs/export-json', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ matchIds }),
    }),
  importJsonFiles: async (files: FileList | File[]) => {
    const form = new FormData()
    Array.from(files).forEach((file) => form.append('files', file))
    return request<{ imported: number; skipped: number; rejected: string[] }>('/api/logs/import-json', {
      method: 'POST',
      body: form,
    })
  },
  exportCompetition: (targetRoot: string) =>
    request<{ path: string }>(`/api/competitions/current/export?targetRoot=${encodeURIComponent(targetRoot)}`, {
      method: 'POST',
    }),
  importCompetition: (path: string) =>
    request<Competition>(`/api/competitions/import?path=${encodeURIComponent(path)}`, {
      method: 'POST',
    }),
}

export function formatClock(totalSeconds: number) {
  const safe = Math.max(0, totalSeconds | 0)
  const mm = String(Math.floor(safe / 60)).padStart(2, '0')
  const ss = String(safe % 60).padStart(2, '0')
  return `${mm}:${ss}`
}
