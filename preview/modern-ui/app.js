const state = {
  selectedTeamId: "t1",
  selectedPlayerId: "p1",
  selectedOnCourtId: "p1",
  selectedLogMatchId: "m1",
  contextLogKey: null,
};

const teams = [
  { id: "t1", name: "计算机学院", note: "卫冕热门", status: "启用", count: 8 },
  { id: "t2", name: "经济管理学院", note: "防守出色", status: "启用", count: 7 },
  { id: "t3", name: "机械工程学院", note: "往届亚军", status: "停用", count: 6 },
];

const playerFields = [
  { id: "f1", name: "位置", type: "Text", required: false },
  { id: "f2", name: "身高", type: "Text", required: false },
  { id: "f3", name: "年级", type: "Text", required: false },
];

const players = [
  { id: "p1", teamId: "t1", name: "陈思远", studentNumber: "20230012", status: "在队", fields: { 位置: "得分后卫", 身高: "186cm", 年级: "大三" }, note: "三分稳定", photo: true, initials: "陈" },
  { id: "p2", teamId: "t1", name: "李明轩", studentNumber: "20230018", status: "在队", fields: { 位置: "控球后卫", 身高: "178cm", 年级: "大二" }, note: "", photo: false, initials: "李" },
  { id: "p3", teamId: "t1", name: "赵一诺", studentNumber: "20230102", status: "在队", fields: { 位置: "小前锋", 身高: "190cm", 年级: "大一" }, note: "防守积极", photo: false, initials: "赵" },
  { id: "p4", teamId: "t1", name: "周子墨", studentNumber: "20230077", status: "在队", fields: { 位置: "中锋", 身高: "196cm", 年级: "大四" }, note: "", photo: true, initials: "周" },
  { id: "p5", teamId: "t1", name: "孙启航", studentNumber: "20230045", status: "离队", fields: { 位置: "大前锋", 身高: "192cm", 年级: "大三" }, note: "本学期交流", photo: false, initials: "孙" },
  { id: "p6", teamId: "t2", name: "韩宇泽", studentNumber: "20221009", status: "在队", fields: { 位置: "得分后卫", 身高: "184cm", 年级: "大四" }, note: "队长", photo: true, initials: "韩" },
  { id: "p7", teamId: "t2", name: "吴佳怡", studentNumber: "20221033", status: "在队", fields: { 位置: "控球后卫", 身高: "172cm", 年级: "大二" }, note: "", photo: false, initials: "吴" },
  { id: "p8", teamId: "t2", name: "郑浩然", studentNumber: "20221118", status: "在队", fields: { 位置: "中锋", 身高: "198cm", 年级: "大三" }, note: "篮板核心", photo: false, initials: "郑" },
];

const homeRoster = [
  { jersey: "7", name: "陈思远", starter: true, onCourt: true },
  { jersey: "3", name: "李明轩", starter: true, onCourt: true },
  { jersey: "11", name: "赵一诺", starter: true, onCourt: true },
  { jersey: "21", name: "周子墨", starter: true, onCourt: true },
  { jersey: "9", name: "孙启航", starter: true, onCourt: false },
  { jersey: "12", name: "林晓博", starter: false, onCourt: false },
];

const awayRoster = [
  { jersey: "5", name: "韩宇泽", starter: true, onCourt: true },
  { jersey: "8", name: "吴佳怡", starter: true, onCourt: true },
  { jersey: "14", name: "郑浩然", starter: true, onCourt: true },
  { jersey: "1", name: "高子涵", starter: true, onCourt: true },
  { jersey: "24", name: "唐启铭", starter: true, onCourt: true },
  { jersey: "17", name: "何子安", starter: false, onCourt: false },
];

const homeOnCourt = [
  { id: "p1", jersey: "7", name: "陈思远", 得分: 14, 犯规: 1, 篮板: 2, 助攻: 3, 抢断: 1, 盖帽: 0, 失误: 1, 申请暂停: 0 },
  { id: "p2", jersey: "3", name: "李明轩", 得分: 8, 犯规: 0, 篮板: 1, 助攻: 6, 抢断: 2, 盖帽: 0, 失误: 2, 申请暂停: 1 },
  { id: "p3", jersey: "11", name: "赵一诺", 得分: 6, 犯规: 2, 篮板: 4, 助攻: 1, 抢断: 0, 盖帽: 1, 失误: 0, 申请暂停: 0 },
  { id: "p4", jersey: "21", name: "周子墨", 得分: 10, 犯规: 1, 篮板: 7, 助攻: 0, 抢断: 0, 盖帽: 2, 失误: 1, 申请暂停: 0 },
  { id: "p9", jersey: "9", name: "孙启航", 得分: 0, 犯规: 0, 篮板: 2, 助攻: 0, 抢断: 0, 盖帽: 0, 失误: 0, 申请暂停: 0 },
];

const awayOnCourt = [
  { id: "a1", jersey: "5", name: "韩宇泽", 得分: 12, 犯规: 1, 篮板: 3, 助攻: 2, 抢断: 1, 盖帽: 0, 失误: 1, 申请暂停: 0 },
  { id: "a2", jersey: "8", name: "吴佳怡", 得分: 9, 犯规: 0, 篮板: 2, 助攻: 5, 抢断: 2, 盖帽: 0, 失误: 2, 申请暂停: 0 },
  { id: "a3", jersey: "14", name: "郑浩然", 得分: 8, 犯规: 2, 篮板: 9, 助攻: 1, 抢断: 0, 盖帽: 1, 失误: 0, 申请暂停: 0 },
  { id: "a4", jersey: "1", name: "高子涵", 得分: 4, 犯规: 0, 篮板: 1, 助攻: 2, 抢断: 1, 盖帽: 0, 失误: 1, 申请暂停: 0 },
  { id: "a5", jersey: "24", name: "唐启铭", 得分: 2, 犯规: 1, 篮板: 3, 助攻: 0, 抢断: 0, 盖帽: 0, 失误: 0, 申请暂停: 0 },
];

const matches = [
  { id: "m1", name: "半决赛", home: "计算机学院", away: "经济管理学院", status: "进行中", events: 46, homeScore: 38, awayScore: 35 },
  { id: "m2", name: "小组赛", home: "机械工程学院", away: "外国语学院", status: "已结束", events: 61, homeScore: 52, awayScore: 49 },
];

const logRows = [
  {
    id: "e1",
    time: "第2节 08:12",
    home: { title: "#7 陈思远 · +3 分", note: "右侧底角", voided: false },
    away: null,
  },
  {
    id: "e2",
    time: "第2节 07:58",
    home: null,
    away: { title: "#5 韩宇泽 · 犯规", note: "阻挡", voided: false },
  },
  {
    id: "e3",
    time: "第2节 07:42",
    home: { title: "#7 陈思远 · +2 分", note: "", voided: false },
    away: null,
  },
  {
    id: "e4",
    time: "第2节 07:40",
    home: { title: "计时控制 · 暂停计时", note: "中立事件双侧显示", voided: false, neutral: true },
    away: { title: "计时控制 · 暂停计时", note: "中立事件双侧显示", voided: false, neutral: true },
  },
  {
    id: "e5",
    time: "第2节 07:15",
    home: null,
    away: { title: "#14 郑浩然 · 篮板", note: "", voided: false },
  },
  {
    id: "e6",
    time: "第2节 06:50",
    home: { title: "换人 · #9 孙启航 → #12 林晓博", note: "", voided: false },
    away: null,
  },
  {
    id: "e7",
    time: "第1节 02:10",
    home: { title: "#3 李明轩 · 失误", note: "已作废示例", voided: true },
    away: null,
  },
];

const STAT_LABELS = ["得分", "犯规", "篮板", "助攻", "抢断", "盖帽", "失误", "申请暂停"];

function $(selector, root = document) {
  return root.querySelector(selector);
}

function $all(selector, root = document) {
  return [...root.querySelectorAll(selector)];
}

function showToast(message) {
  const toast = $("#toast");
  toast.textContent = message;
  toast.classList.add("show");
  clearTimeout(showToast._timer);
  showToast._timer = setTimeout(() => toast.classList.remove("show"), 1800);
}

function openModal(id) {
  const modal = document.getElementById(`modal-${id}`);
  if (modal) modal.classList.add("open");
}

function closeModal(el) {
  const backdrop = el.closest(".modal-backdrop");
  if (backdrop) backdrop.classList.remove("open");
}

function hideContextMenu() {
  const menu = $("#logContextMenu");
  menu.hidden = true;
  state.contextLogKey = null;
  $all(".log-event.selected-for-void").forEach((el) => el.classList.remove("selected-for-void"));
}

function renderTeams() {
  const list = $("#teamList");
  list.innerHTML = teams.map((team) => `
    <button class="list-item ${team.id === state.selectedTeamId ? "active" : ""} ${team.status === "停用" ? "disabled" : ""}"
            type="button" data-team-id="${team.id}">
      <div class="avatar">${team.name.slice(0, 1)}</div>
      <div class="item-main">
        <div class="item-title">${team.name}</div>
        <div class="item-sub">${team.note || "无备注"} · ${team.count} 人</div>
      </div>
      <span class="badge ${team.status === "启用" ? "badge-green" : "badge-orange"}">${team.status}</span>
    </button>
  `).join("");
}

function renderPlayers() {
  const team = teams.find((item) => item.id === state.selectedTeamId);
  $("#teamPlayersTitle").textContent = `${team?.name || "队伍"} · 球员`;
  const teamPlayers = players.filter((item) => item.teamId === state.selectedTeamId);
  if (!teamPlayers.some((item) => item.id === state.selectedPlayerId)) {
    state.selectedPlayerId = teamPlayers[0]?.id || null;
  }

  $("#playerList").innerHTML = teamPlayers.map((player) => `
    <button class="list-item ${player.id === state.selectedPlayerId ? "active" : ""}"
            type="button" data-player-id="${player.id}">
      <div class="avatar">${player.photo ? player.initials : "＋"}</div>
      <div class="item-main">
        <div class="item-title">${player.name}</div>
        <div class="item-sub">${player.studentNumber} · ${player.fields?.位置 || "未填位置"}</div>
      </div>
      <span class="badge ${player.status === "在队" ? "badge-blue" : "badge-orange"}">${player.status}</span>
    </button>
  `).join("") || `<div class="muted">该队伍暂无球员</div>`;

  renderPlayerDetail();
}

function renderPlayerDetail() {
  const player = players.find((item) => item.id === state.selectedPlayerId);
  const box = $("#playerDetail");
  if (!player) {
    box.innerHTML = `<div class="muted">请选择球员，或点击下方新增。</div>`;
    return;
  }

  const customFieldsHtml = playerFields.map((field) => `
    <div class="field">
      <label>${field.name}${field.required ? " *" : ""}</label>
      <input value="${player.fields?.[field.name] || ""}" placeholder="${field.type === "Number" ? "数字" : "文本"}" />
    </div>
  `).join("");

  box.innerHTML = `
    <div class="player-detail-top">
      <button class="photo-box large" type="button" data-toast="${player.photo ? "已模拟更换照片" : "已模拟选择照片"}">
        ${player.photo ? `<span style="font-size:42px;font-weight:800;color:#1e5eff">${player.initials}</span>` : "添加照片"}
      </button>
      <div class="stack" style="flex:1">
        <div>
          <div class="item-title" style="font-size:22px">${player.name}</div>
          <div class="item-sub">${player.studentNumber}</div>
        </div>
        <div class="page-actions">
          <span class="badge badge-blue">${player.status}</span>
          <span class="badge">${player.fields?.位置 || "未填位置"}</span>
        </div>
        <button class="btn btn-ghost" type="button" data-toast="${player.photo ? "已模拟更换照片" : "已模拟选择照片"}">
          ${player.photo ? "更换照片" : "添加照片"}
        </button>
      </div>
    </div>
    <div class="form-grid">
      <div class="field">
        <label>姓名</label>
        <input value="${player.name}" />
      </div>
      <div class="field">
        <label>学号</label>
        <input value="${player.studentNumber}" />
      </div>
      <div class="field">
        <label>状态</label>
        <select>
          <option ${player.status === "在队" ? "selected" : ""}>在队</option>
          <option ${player.status === "离队" ? "selected" : ""}>离队</option>
          <option ${player.status === "停用" ? "selected" : ""}>停用</option>
        </select>
      </div>
      <div class="field">
        <label>所属队伍</label>
        <input value="${teams.find((t) => t.id === player.teamId)?.name || ""}" disabled />
      </div>
      ${customFieldsHtml}
      <div class="field full">
        <label>备注</label>
        <textarea>${player.note}</textarea>
      </div>
    </div>
    <div class="page-actions">
      <button class="btn btn-primary" type="button" data-toast="已模拟保存球员信息">保存</button>
      <button class="btn btn-danger" type="button" data-toast="已模拟停用球员">停用</button>
    </div>
  `;
}

function renderPlayerFields() {
  const list = $("#playerFieldList");
  if (!list) return;
  list.innerHTML = playerFields.map((field, index) => `
    <div class="field-row" data-field-id="${field.id}">
      <div>
        <div class="item-title">${field.name}</div>
        <div class="item-sub">${field.type === "Number" ? "数字" : "文本"} · ${field.required ? "必填" : "选填"}</div>
      </div>
      <span class="badge ${field.required ? "badge-orange" : "badge-blue"}">${field.required ? "必填" : "选填"}</span>
      <button class="btn btn-ghost" type="button" data-move-field="${index}" data-dir="-1" ${index === 0 ? "disabled" : ""}>上移</button>
      <button class="btn btn-danger" type="button" data-remove-field="${field.id}">删除</button>
    </div>
  `).join("") || `<div class="muted">还没有自定义字段，添加后会出现在球员详情中。</div>`;
}

function renderRoster(targetId, rows, side) {
  $(targetId).innerHTML = rows.map((row) => `
    <div class="roster-row">
      <div class="jersey ${side}">${row.jersey}</div>
      <div>
        <div class="item-title">${row.name}</div>
        <div class="item-sub">${row.starter ? "首发" : "替补"} · ${row.onCourt ? "在场" : "场下"}</div>
      </div>
      <span class="badge ${row.starter ? "badge-blue" : ""}">${row.starter ? "首发" : "替补"}</span>
      <span class="badge ${row.onCourt ? "badge-green" : ""}">${row.onCourt ? "在场" : "场下"}</span>
    </div>
  `).join("");
}

function renderStatChips(player) {
  return STAT_LABELS.map((label) => `
    <div class="stat-chip"><strong>${player[label] ?? 0}</strong>${label}</div>
  `).join("");
}

function renderOnCourt(targetId, rows, side) {
  $(targetId).innerHTML = rows.map((row) => `
    <button class="player-card ${row.id === state.selectedOnCourtId ? "selected" : ""}"
            type="button" data-oncourt-id="${row.id}" data-oncourt-name="#${row.jersey} ${row.name}">
      <div class="jersey ${side}">${row.jersey}</div>
      <div>
        <div class="item-title">${row.name}</div>
        <div class="stats stats-full">
          ${renderStatChips(row)}
        </div>
      </div>
    </button>
  `).join("");
}

function renderMatchHistory() {
  $("#matchHistoryBody").innerHTML = matches.map((match) => `
    <tr class="${match.id === state.selectedLogMatchId ? "active" : ""}" data-log-match="${match.id}">
      <td>${match.name}</td>
      <td>${match.home} vs ${match.away}</td>
      <td><strong style="letter-spacing:-0.02em">${match.homeScore} - ${match.awayScore}</strong></td>
      <td><span class="badge ${match.status === "进行中" ? "badge-green" : "badge-orange"}">${match.status}</span></td>
      <td>${match.events}</td>
    </tr>
  `).join("");
}

function renderLogEventHtml(event, key) {
  if (!event) {
    return `<div class="log-slot empty" style="min-height:72px"></div>`;
  }
  return `
    <div class="log-event ${event.voided ? "voided" : ""}"
         data-log-key="${key}"
         data-log-title="${event.title}"
         data-log-time="${key.split("|")[0]}"
         data-log-voided="${event.voided ? "1" : "0"}">
      <div class="time">${key.split("|")[0]}</div>
      <div class="body">${event.title}</div>
      ${event.note ? `<div class="note">${event.note}</div>` : ""}
      ${event.voided ? `<div class="note">已作废</div>` : `<div class="note" style="color:#94a3b8">右键可作废</div>`}
    </div>
  `;
}

function renderLogs() {
  const homeCol = $("#homeLogColumn");
  const awayCol = $("#awayLogColumn");
  homeCol.innerHTML = "";
  awayCol.innerHTML = "";

  logRows.forEach((row) => {
    const wrapHome = document.createElement("div");
    wrapHome.style.marginBottom = "10px";
    wrapHome.innerHTML = renderLogEventHtml(row.home, `${row.time}|home|${row.id}`);

    const wrapAway = document.createElement("div");
    wrapAway.style.marginBottom = "10px";
    wrapAway.innerHTML = renderLogEventHtml(row.away, `${row.time}|away|${row.id}`);

    homeCol.appendChild(wrapHome);
    awayCol.appendChild(wrapAway);
  });
}

function findLogEvent(key) {
  if (!key) return null;
  const [time, side, id] = key.split("|");
  const row = logRows.find((item) => item.id === id && item.time === time);
  if (!row) return null;
  return { row, side, event: side === "home" ? row.home : row.away };
}

function switchView(view) {
  $all(".nav-item").forEach((item) => item.classList.toggle("active", item.dataset.view === view));
  $all(".view").forEach((section) => section.classList.toggle("active", section.id === `view-${view}`));
  hideContextMenu();
}

function bindGlobalEvents() {
  $("#sidebarToggle").addEventListener("click", () => {
    $("#app").classList.toggle("sidebar-collapsed");
  });

  $all(".nav-item").forEach((item) => {
    item.addEventListener("click", () => switchView(item.dataset.view));
  });

  const switcher = $("#competitionSwitcher");
  const trigger = $("#competitionTrigger");
  trigger.addEventListener("click", (event) => {
    event.stopPropagation();
    const open = switcher.classList.toggle("open");
    trigger.setAttribute("aria-expanded", open ? "true" : "false");
  });
  document.addEventListener("click", () => {
    switcher.classList.remove("open");
    trigger.setAttribute("aria-expanded", "false");
    hideContextMenu();
  });
  switcher.addEventListener("click", (event) => event.stopPropagation());

  $("#addPlayerFieldBtn")?.addEventListener("click", () => {
    const name = $("#newFieldName").value.trim();
    if (!name) {
      showToast("请填写字段名称");
      return;
    }
    if (playerFields.some((field) => field.name === name)) {
      showToast("字段名称已存在");
      return;
    }
    playerFields.push({
      id: `f${Date.now()}`,
      name,
      type: $("#newFieldType").value,
      required: $("#newFieldRequired").value === "true",
    });
    players.forEach((player) => {
      player.fields = player.fields || {};
      if (player.fields[name] === undefined) player.fields[name] = "";
    });
    $("#newFieldName").value = "";
    renderPlayerFields();
    renderPlayerDetail();
    showToast(`已添加字段：${name}`);
  });

  $("#confirmVoidBtn")?.addEventListener("click", () => {
    const found = findLogEvent(state.contextLogKey);
    if (!found?.event) {
      showToast("未找到要作废的日志");
      return;
    }
    if (found.event.voided) {
      showToast("该日志已经作废");
      closeModal($("#confirmVoidBtn"));
      return;
    }
    const reason = $("#voidReasonInput").value.trim();
    if (!reason) {
      showToast("请填写作废原因");
      return;
    }
    found.event.voided = true;
    found.event.note = `${found.event.note ? found.event.note + " · " : ""}作废：${reason}`;
    // Neutral events appear on both sides with same title; void both if mirrored.
    if (found.event.neutral) {
      if (found.row.home) found.row.home.voided = true;
      if (found.row.away) found.row.away.voided = true;
    }
    renderLogs();
    closeModal($("#confirmVoidBtn"));
    hideContextMenu();
    showToast("事件已作废（预览）");
  });

  document.addEventListener("click", (event) => {
    const teamBtn = event.target.closest("[data-team-id]");
    if (teamBtn) {
      state.selectedTeamId = teamBtn.dataset.teamId;
      renderTeams();
      renderPlayers();
      return;
    }

    const playerBtn = event.target.closest("[data-player-id]");
    if (playerBtn) {
      state.selectedPlayerId = playerBtn.dataset.playerId;
      renderPlayers();
      return;
    }

    const onCourtBtn = event.target.closest("[data-oncourt-id]");
    if (onCourtBtn) {
      state.selectedOnCourtId = onCourtBtn.dataset.oncourtId;
      $("#selectedPlayerTip").textContent = `当前记录球员：${onCourtBtn.dataset.oncourtName}`;
      renderOnCourt("#homeOnCourt", homeOnCourt, "home");
      renderOnCourt("#awayOnCourt", awayOnCourt, "away");
      return;
    }

    const logRow = event.target.closest("[data-log-match]");
    if (logRow) {
      state.selectedLogMatchId = logRow.dataset.logMatch;
      const match = matches.find((item) => item.id === state.selectedLogMatchId);
      $("#logMatchTitle").textContent = `${match.home} vs ${match.away} · ${match.name}`;
      renderMatchHistory();
      return;
    }

    const removeField = event.target.closest("[data-remove-field]");
    if (removeField) {
      const id = removeField.dataset.removeField;
      const index = playerFields.findIndex((field) => field.id === id);
      if (index >= 0) {
        const [removed] = playerFields.splice(index, 1);
        renderPlayerFields();
        renderPlayerDetail();
        showToast(`已删除字段：${removed.name}`);
      }
      return;
    }

    const moveField = event.target.closest("[data-move-field]");
    if (moveField) {
      const index = Number(moveField.dataset.moveField);
      const dir = Number(moveField.dataset.dir);
      const next = index + dir;
      if (next < 0 || next >= playerFields.length) return;
      const tmp = playerFields[index];
      playerFields[index] = playerFields[next];
      playerFields[next] = tmp;
      renderPlayerFields();
      return;
    }

    const menuBtn = event.target.closest("#logContextMenu button");
    if (menuBtn) {
      event.stopPropagation();
      const action = menuBtn.dataset.action;
      const found = findLogEvent(state.contextLogKey);
      if (action === "void") {
        if (!found?.event) {
          showToast("未选中日志");
        } else if (found.event.voided) {
          showToast("该日志已经作废");
        } else {
          $("#voidEventSummary").textContent = `${found.row.time} · ${found.event.title}`;
          $("#voidReasonInput").value = "";
          $("#voidOperatorInput").value = "";
          openModal("voidEvent");
        }
      } else if (action === "detail") {
        showToast(found?.event ? `详情：${found.event.title}` : "未选中日志");
      }
      hideContextMenu();
      return;
    }

    const modalOpen = event.target.closest("[data-modal]");
    if (modalOpen) {
      openModal(modalOpen.dataset.modal);
      return;
    }

    const toastBtn = event.target.closest("[data-toast]");
    if (toastBtn && !toastBtn.dataset.modal) {
      showToast(toastBtn.dataset.toast);
    }

    if (event.target.matches("[data-close]")) {
      closeModal(event.target);
      if (event.target.dataset.toast) showToast(event.target.dataset.toast);
    }

    if (event.target.classList.contains("modal-backdrop")) {
      event.target.classList.remove("open");
    }
  });

  document.addEventListener("contextmenu", (event) => {
    const logEvent = event.target.closest(".log-event");
    if (!logEvent) return;
    event.preventDefault();
    event.stopPropagation();

    $all(".log-event.selected-for-void").forEach((el) => el.classList.remove("selected-for-void"));
    logEvent.classList.add("selected-for-void");
    state.contextLogKey = logEvent.dataset.logKey;

    const menu = $("#logContextMenu");
    menu.hidden = false;
    const menuWidth = 170;
    const menuHeight = 90;
    const x = Math.min(event.clientX, window.innerWidth - menuWidth - 8);
    const y = Math.min(event.clientY, window.innerHeight - menuHeight - 8);
    menu.style.left = `${x}px`;
    menu.style.top = `${y}px`;
  });

  // Fake clock tick for scoreboard atmosphere.
  let seconds = 7 * 60 + 42;
  setInterval(() => {
    if (!$("#view-scoreboard").classList.contains("active")) return;
    seconds = Math.max(0, seconds - 1);
    const mm = String(Math.floor(seconds / 60)).padStart(2, "0");
    const ss = String(seconds % 60).padStart(2, "0");
    $("#clockText").textContent = `${mm}:${ss}`;
  }, 1000);
}

function init() {
  renderTeams();
  renderPlayers();
  renderPlayerFields();
  renderRoster("#homeRoster", homeRoster, "home");
  renderRoster("#awayRoster", awayRoster, "away");
  renderOnCourt("#homeOnCourt", homeOnCourt, "home");
  renderOnCourt("#awayOnCourt", awayOnCourt, "away");
  renderMatchHistory();
  renderLogs();
  bindGlobalEvents();
}

init();
