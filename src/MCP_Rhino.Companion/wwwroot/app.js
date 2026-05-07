/* Rhino Agent Panel — companion UI front-end. Dark IDE aesthetic, warm amber accent. */

const transcript = document.getElementById("transcript");
const appShell = document.getElementById("appShell");

const mcpPill = document.getElementById("mcpPill");
const mcpLabel = document.getElementById("mcpLabel");

const modelEcho = document.getElementById("modelEcho");

const newChatButton = document.getElementById("newChatButton");
const historyButton = document.getElementById("historyButton");
const settingsButton = document.getElementById("settingsButton");
const pinButton = document.getElementById("pinButton");
const headerTitle = document.getElementById("headerTitle");

const settingsOverlay = document.getElementById("settingsOverlay");
const settingsCard = document.getElementById("settingsCard");
const settingsCloseButton = document.getElementById("settingsCloseButton");
const settingsDoneButton = document.getElementById("settingsDoneButton");
const cliChoices = document.getElementById("cliChoices");
const modelChoices = document.getElementById("modelChoices");
const mcpReadout = document.getElementById("mcpReadout");
const mcpPipeEl = document.getElementById("mcpPipe");
const mcpMetaEl = document.getElementById("mcpMeta");

const historyOverlay = document.getElementById("historyOverlay");
const historyCard = document.getElementById("historyCard");
const historyCloseButton = document.getElementById("historyCloseButton");
const historyBody = document.getElementById("historyBody");
const historyCountEl = document.getElementById("historyCount");
const historySearch = document.getElementById("historySearch");
const historySearchClear = document.getElementById("historySearchClear");

const composerBox = document.getElementById("composerBox");
const composerInput = document.getElementById("composerInput");
const sendButton = document.getElementById("sendButton");
const attachButton = document.getElementById("attachButton");
const attachmentChips = document.getElementById("attachmentChips");

const dragOverlay = document.getElementById("dragOverlay");

const ctxArc = document.getElementById("ctxArc");
const ctxDonut = document.getElementById("ctxDonut");
const ctxUsed = document.getElementById("ctxUsed");
const ctxTotal = document.getElementById("ctxTotal");
const costValue = document.getElementById("costValue");

/* ---------- State ---------- */

const CONTEXT_TOTAL = 200000;
const CLIS = [
  { id: "claude code cli", desc: "Anthropic's official CLI · MCP-native" },
  { id: "codex cli",       desc: "OpenAI Codex CLI · OpenAI Responses API" },
];
const FALLBACK_MODELS_BY_CLI = {
  "claude code cli": [
    "Default",
    "claude-haiku-4-5",
    "claude-sonnet-4-6",
    "claude-opus-4-7",
  ],
  "codex cli": [
    "Default",
    "gpt-5.5",
    "gpt-5.4",
    "gpt-5.4-mini",
    "gpt-5.3-codex",
    "gpt-5.3-codex-spark",
    "gpt-5.2",
  ],
};

// Estimated USD per 1M tokens, blended 70% input / 30% output. Numbers are
// directional; the status-bar tooltip labels the figure as an estimate.
const COST_RATE_PER_MTOK = {
  "claude-haiku-4-5":      2.2,
  "claude-sonnet-4-6":     6.6,
  "claude-opus-4-7":      33.0,
  "gpt-5.2":               8.0,
  "gpt-5.3-codex":         8.0,
  "gpt-5.3-codex-spark":   8.0,
  "gpt-5.4":              12.0,
  "gpt-5.4-mini":          2.4,
  "gpt-5.5":              16.0,
};
const DEFAULT_RATE_PER_MTOK_BY_CLI = {
  "claude code cli":  6.6, // default → Sonnet-class
  "codex cli":       12.0, // default → GPT-5.4-class
};

const toolCards = new Map();
const attached = [];
let busy = false;
// `pinned` now means "force on top of every app". The default is off because
// the Companion is already owned by the Rhino main window (always-on-top of
// Rhino, follows minimize / restore).
let pinned = false;
let contextUsed = 0;
let currentModel = "Default";
let currentCli = "claude code cli";
let availableModels = FALLBACK_MODELS_BY_CLI[currentCli].slice();
const selectedModelsByCli = { [currentCli]: currentModel };
let lastAssistantStamp = null;
let mcpConnected = false;

let documentPath = "";
let documentName = "";

const HISTORY_LIMIT = 50;
let currentSession = newCurrentSession();
let historyQuery = "";

function newCurrentSession() {
  return {
    id: "s-" + Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 6),
    title: "",
    snippet: "",
    firstUserText: "",
    lastAssistantText: "",
    msgs: 0,
    startedAt: Date.now(),
  };
}

function historyKeyPrefix() {
  // Scope history per bound document so two open .3dm files keep distinct lists.
  const path = documentPath || "(unknown-doc)";
  return `mcp-rhino:history:${path}`;
}

const historyStore = {
  loadIndex() {
    try {
      const raw = localStorage.getItem(historyKeyPrefix() + ":index");
      const parsed = raw ? JSON.parse(raw) : null;
      return Array.isArray(parsed) ? parsed : [];
    } catch { return []; }
  },
  saveIndex(list) {
    try { localStorage.setItem(historyKeyPrefix() + ":index", JSON.stringify(list)); }
    catch { /* quota exhausted — accept partial loss */ }
  },
  saveSnapshot(id, html) {
    try { localStorage.setItem(historyKeyPrefix() + ":s:" + id, JSON.stringify({ id, html })); }
    catch { /* quota exhausted */ }
  },
  loadSnapshot(id) {
    try {
      const raw = localStorage.getItem(historyKeyPrefix() + ":s:" + id);
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  },
  deleteSession(id) {
    try { localStorage.removeItem(historyKeyPrefix() + ":s:" + id); } catch { /* ignore */ }
    const next = this.loadIndex().filter((s) => s.id !== id);
    this.saveIndex(next);
    return next;
  },
  trimToLimit(list) {
    if (list.length <= HISTORY_LIMIT) return list;
    const pinned = list.filter((s) => s.pinned);
    const recent = list.filter((s) => !s.pinned);
    const keepRecent = recent.slice(0, Math.max(0, HISTORY_LIMIT - pinned.length));
    const dropped = recent.slice(keepRecent.length);
    for (const s of dropped) {
      try { localStorage.removeItem(historyKeyPrefix() + ":s:" + s.id); } catch { /* ignore */ }
    }
    return pinned.concat(keepRecent);
  },
};

/* ---------- IPC ---------- */

function post(type, payload) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage({ type, ...(payload || {}) });
  }
}

/* ---------- Helpers ---------- */

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function nowTime() {
  return new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function formatBytes(byteCount) {
  if (!Number.isFinite(byteCount)) return "";
  if (byteCount > 1024 * 1024) return `${(byteCount / 1024 / 1024).toFixed(1)} MB`;
  if (byteCount > 1024) return `${Math.max(1, Math.round(byteCount / 1024))} KB`;
  return `${byteCount} B`;
}

function shortNumber(n) {
  if (n >= 10000) return `${Math.round(n / 1000)}k`;
  if (n >= 1000) return `${(n / 1000).toFixed(1)}k`;
  return `${n}`;
}

function scrollToEnd() {
  transcript.scrollTop = transcript.scrollHeight;
}

function renderInline(text) {
  // Escape, then render simple **bold** and `code`. Newlines preserved by white-space: pre-wrap.
  const escaped = escapeHtml(text);
  return escaped
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/`([^`]+)`/g, "<code>$1</code>");
}

function highlightPython(code) {
  const esc = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  const tokenRE = /(#[^\n]*)|("[^"]*"|'[^']*')|\b(import|from|for|in|if|else|return|def|class|as|max|min|None|True|False)\b|\b(\d+\.?\d*)\b/g;
  const out = [];
  let last = 0;
  let m;
  while ((m = tokenRE.exec(code)) !== null) {
    if (m.index > last) out.push(esc(code.slice(last, m.index)));
    const [full, comment, str, kw, num] = m;
    const text = esc(full);
    if (comment) out.push(`<span class="syn-comment">${text}</span>`);
    else if (str) out.push(`<span class="syn-str">${text}</span>`);
    else if (kw) out.push(`<span class="syn-kw">${text}</span>`);
    else if (num) out.push(`<span class="syn-num">${text}</span>`);
    last = m.index + full.length;
  }
  if (last < code.length) out.push(esc(code.slice(last)));
  return out.join("");
}

function bumpContext(byChars) {
  contextUsed = Math.min(CONTEXT_TOTAL, contextUsed + Math.max(0, byChars));
  renderContextDonut();
  renderCostEstimate();
}

function currentCostRatePerMtok() {
  const explicit = COST_RATE_PER_MTOK[currentModel];
  if (typeof explicit === "number") return explicit;
  return DEFAULT_RATE_PER_MTOK_BY_CLI[currentCli] || 6.6;
}

function renderCostEstimate() {
  const rate = currentCostRatePerMtok();
  const dollars = (contextUsed / 1_000_000) * rate;
  costValue.textContent = dollars.toFixed(4);
}

function renderContextDonut() {
  const pct = Math.min(1, contextUsed / CONTEXT_TOTAL);
  const circumference = 2 * Math.PI * 7;
  const dash = circumference * pct;
  ctxArc.setAttribute("stroke-dasharray", `${dash} ${circumference}`);
  let color = "var(--accent)";
  if (pct > 0.85) color = "var(--err)";
  else if (pct > 0.7) color = "var(--warn)";
  ctxArc.setAttribute("stroke", color);
  ctxUsed.textContent = shortNumber(contextUsed);
  ctxTotal.textContent = shortNumber(CONTEXT_TOTAL);
}

/* ---------- Block builders ---------- */

function appendBlock(html) {
  const wrapper = document.createElement("div");
  wrapper.innerHTML = html.trim();
  const node = wrapper.firstElementChild;
  transcript.appendChild(node);
  scrollToEnd();
  return node;
}

function addSystem(text, time) {
  lastAssistantStamp = null;
  const safeText = escapeHtml(text || "");
  const t = time ? ` · ${escapeHtml(time)}` : "";
  appendBlock(`
    <div class="system-row">
      <div class="rule"></div>
      <span>${safeText}${t}</span>
      <div class="rule"></div>
    </div>
  `);
}

function addUser(text, attachments, time) {
  lastAssistantStamp = null;
  const t = time || nowTime();
  currentSession.msgs += 1;
  if (!currentSession.firstUserText && text) {
    currentSession.firstUserText = text;
    setSessionTitleFromText(text);
  }
  const chips = (attachments && attachments.length)
    ? `<div class="chip-row in-message">${attachments.map(renderChip).join("")}</div>`
    : "";
  appendBlock(`
    <div class="msg-row">
      <div class="avatar user">You</div>
      <div>
        <div class="msg-meta">
          <span class="msg-author">You</span>
          <span class="msg-time">${escapeHtml(t)}</span>
        </div>
        <div class="msg-body">${renderInline(text || "")}</div>
        ${chips}
      </div>
    </div>
  `);
}

function addAssistant(text, time) {
  const t = time || nowTime();
  // Teams-style grouping: collapse the meta row when the previous assistant
  // message landed in the same minute; show it again after a user/system break.
  const showMeta = lastAssistantStamp !== t;
  lastAssistantStamp = t;
  currentSession.msgs += 1;
  if (text) currentSession.lastAssistantText = text;
  const metaHtml = showMeta
    ? `<div class="msg-meta"><span class="msg-author">Assistant</span><span class="msg-time">${escapeHtml(t)}</span></div>`
    : "";
  // If the assistant text contains fenced code, split and render each chunk.
  const parts = String(text || "").split(/```([a-zA-Z0-9_+-]*)\n([\s\S]*?)```/g);
  // parts: [prose, lang1, code1, prose, lang2, code2, ...]
  const head = parts[0];
  const headHtml = head ? `<div class="msg-body">${renderInline(head)}</div>` : "";
  appendBlock(`
    <div class="msg-row">
      <div class="avatar assistant">AI</div>
      <div>
        ${metaHtml}
        ${headHtml}
      </div>
    </div>
  `);
  for (let i = 1; i < parts.length; i += 3) {
    const lang = (parts[i] || "text").toLowerCase();
    const code = parts[i + 1] || "";
    addCode(lang, code);
    const tail = parts[i + 2];
    if (tail && tail.trim()) {
      appendBlock(`
        <div class="msg-row">
          <div></div>
          <div class="msg-body">${renderInline(tail)}</div>
        </div>
      `);
    }
  }
}

function addThinking(text, durationLabel) {
  const node = appendBlock(`
    <div class="msg-row">
      <div></div>
      <div class="thinking">
        <button type="button" class="rh-btn-ghost thinking-toggle">
          <svg class="chevron-right" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M9 6l6 6-6 6"/></svg>
          <svg class="chevron-down" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9l6 6 6-6"/></svg>
          <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M9 5a3 3 0 0 0-3 3 3 3 0 0 0-1 5 3 3 0 0 0 1 5 3 3 0 0 0 3 3h6a3 3 0 0 0 3-3 3 3 0 0 0 1-5 3 3 0 0 0-1-5 3 3 0 0 0-3-3z"/></svg>
          <span>thinking${durationLabel ? ` · ${escapeHtml(durationLabel)}` : ""}</span>
        </button>
        <div class="thinking-body">${escapeHtml(text || "")}</div>
      </div>
    </div>
  `);
  const thinking = node.querySelector(".thinking");
  thinking.querySelector(".thinking-toggle").addEventListener("click", () => {
    thinking.classList.toggle("is-open");
  });
}

const TOOL_STATUS_CLASS = {
  running: "running",
  ok: "success",
  success: "success",
  warn: "warn",
  failed: "failed",
  err: "failed",
  error: "failed",
};

function statusClassFor(status) {
  return TOOL_STATUS_CLASS[String(status || "running").toLowerCase()] || "running";
}

function summarizeArgs(input) {
  if (!input) return "";
  if (typeof input === "string") {
    const trimmed = input.trim();
    if (!trimmed) return "";
    if (trimmed.length > 80) return trimmed.slice(0, 77) + "…";
    return trimmed;
  }
  try {
    const json = typeof input === "object" ? input : JSON.parse(input);
    return Object.entries(json)
      .map(([k, v]) => `${k}: ${typeof v === "string" ? `"${v}"` : JSON.stringify(v)}`)
      .join(", ");
  } catch {
    return String(input);
  }
}

function prettyJsonMaybe(value) {
  if (value == null) return "";
  if (typeof value !== "string") return JSON.stringify(value, null, 2);
  try { return JSON.stringify(JSON.parse(value), null, 2); }
  catch { return value; }
}

function renderToolHeadHtml(name, argsSummary, statusClass, durationText) {
  return `
    <button type="button" class="tool-head">
      <svg class="chevron-right" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M9 6l6 6-6 6"/></svg>
      <svg class="chevron-down" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9l6 6 6-6"/></svg>
      <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" style="color: var(--text-faint)"><path d="M14 7a4 4 0 1 0 4 4l3 3-2 2-3-3a4 4 0 0 1-4-4l-5 5-3-3 5-5a4 4 0 0 1 5-5"/></svg>
      <span class="tool-name">${escapeHtml(name || "tool")}</span>
      <span class="tool-args">${argsSummary ? `(${escapeHtml(argsSummary)})` : "()"}</span>
      <span class="spacer"></span>
      <span class="tool-status ${statusClass}">
        <span class="tool-dot"></span>
        <span class="duration">${escapeHtml(durationText || "")}</span>
      </span>
    </button>
  `;
}

function ensureToolCard(id, name) {
  let entry = toolCards.get(id);
  if (entry) return entry;

  const card = document.createElement("div");
  card.className = "msg-row";
  card.innerHTML = `
    <div></div>
    <div class="tool-card" data-tool-id="${escapeHtml(id)}">
      ${renderToolHeadHtml(name, "", "running", "")}
      <div class="tool-body"></div>
    </div>
  `;
  transcript.appendChild(card);
  scrollToEnd();

  const cardEl = card.querySelector(".tool-card");
  const head = cardEl.querySelector(".tool-head");
  head.addEventListener("click", () => cardEl.classList.toggle("is-open"));
  entry = { card: cardEl, head, body: cardEl.querySelector(".tool-body"), startedAt: Date.now() };
  toolCards.set(id, entry);
  return entry;
}

function addOrUpdateTool(event) {
  const id = event.toolUseId || `${event.toolName || "tool"}-${toolCards.size}`;
  const entry = ensureToolCard(id, event.toolName || "tool");
  const statusClass = statusClassFor(event.status);
  const argsSummary = summarizeArgs(event.toolInput);
  const elapsed = Date.now() - entry.startedAt;
  const durationText = (statusClass === "running") ? "" : `${(elapsed / 1000).toFixed(elapsed > 999 ? 2 : 0)}s`;

  // Update head in-place.
  entry.head.outerHTML = renderToolHeadHtml(event.toolName || "tool", argsSummary, statusClass, durationText);
  entry.head = entry.card.querySelector(".tool-head");
  entry.head.addEventListener("click", () => entry.card.classList.toggle("is-open"));

  // Update body.
  const sections = [];
  const inputText = (typeof event.toolInput === "string" && event.toolInput) ? prettyJsonMaybe(event.toolInput) : "";
  if (inputText) {
    sections.push(`
      <div class="tool-section">
        <div class="tool-section-title">→ input</div>
        <div class="tool-section-body">${escapeHtml(inputText)}</div>
      </div>
    `);
  }
  if (event.toolResult) {
    sections.push(`
      <div class="tool-section">
        <div class="tool-section-title">→ result</div>
        <div class="tool-section-body">${escapeHtml(prettyJsonMaybe(event.toolResult))}</div>
      </div>
    `);
  }
  entry.body.innerHTML = sections.join("");
  // Cards stay collapsed by default; the user expands by clicking the head.
}

function addCode(lang, code) {
  const language = lang || "text";
  const highlighted = (language === "python" || language === "py")
    ? highlightPython(code)
    : escapeHtml(code);
  const node = appendBlock(`
    <div class="msg-row">
      <div></div>
      <div class="code-card">
        <div class="code-head">
          <span class="code-dot">●</span>
          <span>${escapeHtml(language)}</span>
          <span class="spacer"></span>
          <button type="button" class="code-copy">
            <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><rect x="8" y="8" width="12" height="12" rx="1.5"/><path d="M4 16V5a1 1 0 0 1 1-1h11"/></svg>
            <span>copy</span>
          </button>
        </div>
        <pre><code>${highlighted}</code></pre>
      </div>
    </div>
  `);
  const copyButton = node.querySelector(".code-copy");
  copyButton.addEventListener("click", async () => {
    try { await navigator.clipboard.writeText(code); }
    catch { /* clipboard may be unavailable */ }
    copyButton.classList.add("is-copied");
    copyButton.querySelector("span").textContent = "copied";
    setTimeout(() => {
      copyButton.classList.remove("is-copied");
      copyButton.querySelector("span").textContent = "copy";
    }, 1200);
  });
}

function addWarn(title, text) {
  appendBlock(`
    <div class="msg-row">
      <div></div>
      <div class="warn-card">
        <svg class="warn-icon" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3l10 18H2z"/><path d="M12 10v5"/><circle cx="12" cy="18" r=".5" fill="currentColor"/></svg>
        <div class="warn-text">
          <div class="warn-title">${escapeHtml(title || "Warning")}</div>
          <div class="warn-detail">${escapeHtml(text || "")}</div>
        </div>
      </div>
    </div>
  `);
}

/* eslint-disable no-unused-vars */
function addDiff(diff) {
  const section = (items, sign, signClass) => (items || []).map((it) => `
    <div class="diff-row">
      <span class="diff-sign ${signClass}">${sign}</span>
      <span class="diff-count">${escapeHtml(String(it.count ?? ""))}</span>
      <span class="diff-label">${escapeHtml(it.label || "")}</span>
      <span class="diff-sep">·</span>
      <span class="diff-layer">${escapeHtml(it.layer || "")}</span>
    </div>
  `).join("");
  appendBlock(`
    <div class="msg-row">
      <div></div>
      <div class="diff-card">
        <div class="diff-head">geometry diff</div>
        <div class="diff-body">
          ${section(diff.added, "+", "added")}
          ${section(diff.modified, "~", "modified")}
          ${section(diff.deleted, "−", "deleted")}
        </div>
      </div>
    </div>
  `);
}
/* eslint-enable no-unused-vars */

/* ---------- Attachments ---------- */

function renderChip(file, withRemove) {
  const isImage = (file.kind === "image") || /\.(png|jpe?g|gif|webp|bmp|svg)$/i.test(file.name || "");
  const iconSvg = isImage
    ? `<svg class="chip-icon" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="9" cy="10" r="2"/><path d="M21 16l-5-5-9 9"/></svg>`
    : `<svg class="chip-icon" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"/><path d="M14 3v6h6"/></svg>`;
  const remove = withRemove
    ? `<button type="button" class="chip-remove" data-name="${escapeHtml(file.name)}"><svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M5 5l14 14M19 5L5 19"/></svg></button>`
    : "";
  return `
    <span class="chip">
      ${iconSvg}
      <span class="chip-name">${escapeHtml(file.name)}</span>
      <span class="chip-size">${escapeHtml(file.size)}</span>
      ${remove}
    </span>
  `;
}

function refreshAttachmentChips() {
  if (attached.length === 0) {
    attachmentChips.hidden = true;
    attachmentChips.innerHTML = "";
    return;
  }
  attachmentChips.hidden = false;
  attachmentChips.innerHTML = attached.map((file) => renderChip(file, true)).join("");
  attachmentChips.querySelectorAll(".chip-remove").forEach((btn) => {
    btn.addEventListener("click", () => {
      const name = btn.getAttribute("data-name");
      const idx = attached.findIndex((f) => f.name === name);
      if (idx >= 0) attached.splice(idx, 1);
      refreshAttachmentChips();
    });
  });
}

/* ---------- Composer ---------- */

function autoGrowComposer() {
  composerInput.style.height = "auto";
  composerInput.style.height = Math.min(140, composerInput.scrollHeight) + "px";
}

const SEND_ICON_HTML = `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12l14-7-5 16-3-7-6-2z"/></svg>`;
const STOP_ICON_HTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor" stroke="none"><rect x="6" y="6" width="12" height="12" rx="1.5"/></svg>`;

function updateSendButton() {
  sendButton.classList.remove("is-armed", "is-busy");
  if (busy) {
    sendButton.classList.add("is-busy");
    sendButton.title = "Stop generating";
    sendButton.innerHTML = STOP_ICON_HTML;
  } else {
    const ready = composerInput.value.trim().length > 0 || attached.length > 0;
    if (ready) sendButton.classList.add("is-armed");
    sendButton.title = "Send (⏎)";
    sendButton.innerHTML = SEND_ICON_HTML;
  }
}

function setInputEnabled(enabled) {
  composerInput.disabled = !enabled;
  attachButton.disabled = !enabled;
}

function send() {
  if (busy) {
    post("stop");
    busy = false;
    updateSendButton();
    return;
  }
  const text = composerInput.value.trim();
  if (!text && attached.length === 0) return;

  bumpContext(Math.ceil((text || "").length / 4) + attached.length * 500);

  // Attachments render as chips for the user but are not yet uploaded through
  // the host IPC. The host receives only the text body for now.
  post("send", { text });

  composerInput.value = "";
  attached.length = 0;
  refreshAttachmentChips();
  autoGrowComposer();

  busy = true;
  updateSendButton();
}

/* ---------- Drag & drop ---------- */

function handleDroppedFiles(fileList) {
  const files = Array.from(fileList || []).slice(0, 4);
  if (files.length === 0) return;
  for (const f of files) {
    attached.push({
      name: f.name,
      size: formatBytes(f.size),
      kind: (f.type || "").startsWith("image/") ? "image" : "file",
    });
  }
  refreshAttachmentChips();
  updateSendButton();
}

let dragDepth = 0;

window.addEventListener("dragenter", (e) => {
  e.preventDefault();
  dragDepth += 1;
  appShell.classList.add("is-dragging");
});
window.addEventListener("dragover", (e) => { e.preventDefault(); });
window.addEventListener("dragleave", (e) => {
  e.preventDefault();
  dragDepth = Math.max(0, dragDepth - 1);
  if (dragDepth === 0) appShell.classList.remove("is-dragging");
});
window.addEventListener("drop", (e) => {
  e.preventDefault();
  dragDepth = 0;
  appShell.classList.remove("is-dragging");
  if (e.dataTransfer && e.dataTransfer.files) handleDroppedFiles(e.dataTransfer.files);
});

/* ---------- Header actions ---------- */

function setMcpStatus(label) {
  const lower = String(label || "").toLowerCase();
  const failed = lower.includes("missing") ||
    lower.includes("unavailable") ||
    lower.includes("error") ||
    lower.includes("failed") ||
    lower.includes("disconnect");
  const connected = !failed && (
    lower.includes("connect") ||
    lower.includes("ready") ||
    lower.includes("configured")
  );
  const connecting = lower.includes("start") || lower.includes("init");
  if (connected) mcpConnected = true;
  else if (failed) mcpConnected = false;

  mcpPill.classList.remove("is-connected", "is-connecting");
  mcpReadout.classList.remove("is-connected");
  if (mcpConnected) {
    mcpPill.classList.add("is-connected");
    mcpReadout.classList.add("is-connected");
    mcpMetaEl.textContent = "connected";
  } else if (connecting) {
    mcpPill.classList.add("is-connecting");
    mcpMetaEl.textContent = "starting";
  } else {
    mcpMetaEl.textContent = label || "";
  }
  mcpLabel.textContent = "MCP";
}

function setMcpPipe(pipeName) {
  mcpPipeEl.textContent = pipeName || "—";
}

function setModel(modelId) {
  currentModel = modelId || "Default";
  selectedModelsByCli[currentCli] = currentModel;
  modelEcho.textContent = currentModel;
  modelChoices.querySelectorAll(".settings-choice").forEach((c) => {
    c.classList.toggle("is-active", c.dataset.value === currentModel);
  });
  renderCostEstimate();
}

function setCli(cliId) {
  currentCli = cliId || "claude code cli";
  cliChoices.querySelectorAll(".settings-choice").forEach((c) => {
    c.classList.toggle("is-active", c.dataset.value === currentCli);
  });
}

function fallbackModelsForCli(cliId) {
  return (FALLBACK_MODELS_BY_CLI[cliId] || FALLBACK_MODELS_BY_CLI["claude code cli"]).slice();
}

function renderModelChoices(models, selectedModel) {
  availableModels = (Array.isArray(models) && models.length ? models : fallbackModelsForCli(currentCli)).slice();
  const selected = availableModels.includes(selectedModel) ? selectedModel : "Default";
  modelChoices.innerHTML = availableModels.map((m) => `
    <button type="button" class="settings-choice" data-value="${escapeHtml(m)}">
      <div class="choice-title"><span class="choice-dot"></span><span>${escapeHtml(m)}</span></div>
    </button>
  `).join("");
  modelChoices.querySelectorAll(".settings-choice").forEach((choice) => {
    choice.addEventListener("click", () => {
      const value = choice.dataset.value;
      setModel(value);
      post("model", { model: value === "Default" ? null : value });
    });
  });
  setModel(selected);
}

function buildSettingsChoices() {
  renderModelChoices(availableModels, currentModel);
  cliChoices.innerHTML = CLIS.map((c) => `
    <button type="button" class="settings-choice" data-value="${escapeHtml(c.id)}">
      <div class="choice-title"><span class="choice-dot"></span><span>${escapeHtml(c.id)}</span></div>
      <div class="choice-sub">${escapeHtml(c.desc)}</div>
    </button>
  `).join("");
  cliChoices.querySelectorAll(".settings-choice").forEach((choice) => {
    choice.addEventListener("click", () => {
      const value = choice.dataset.value;
      if (value === currentCli) return;
      // The host will restart the backend session; archive + clear locally so
      // the panel visibly reflects a fresh session, not a continuation.
      archiveCurrentSession("cli switch");
      transcript.innerHTML = "";
      toolCards.clear();
      lastAssistantStamp = null;
      contextUsed = 2400;
      currentSession = newCurrentSession();
      setSessionTitle("");
      renderContextDonut();
      setCli(value);
      renderModelChoices(fallbackModelsForCli(value), selectedModelsByCli[value] || "Default");
      renderCostEstimate();
      addSystem(`Switching backend CLI · ${value}`, nowTime());
      post("cli", { cli: value });
    });
  });
}

function openSettings() { settingsOverlay.classList.add("is-open"); }
function closeSettings() { settingsOverlay.classList.remove("is-open"); }

const PIN_ON_HTML  = `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2v7"/><path d="M5 9h14l-2 5H7z"/><path d="M12 14v8"/></svg>`;
const PIN_OFF_HTML = `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M3 3l18 18"/><path d="M12 2v3"/><path d="M5 9h14l-1 2.5"/><path d="M9 14H7l-2 -5"/><path d="M12 14v8"/></svg>`;

function applyPinState() {
  pinButton.classList.toggle("is-on", pinned);
  pinButton.title = pinned ? "Unpin (always-on-top)" : "Pin (always-on-top)";
  pinButton.innerHTML = pinned ? PIN_ON_HTML : PIN_OFF_HTML;
}

function togglePin() {
  pinned = !pinned;
  applyPinState();
  post("pin", { pinned });
}

function clearTranscript() {
  archiveCurrentSession("new chat");
  transcript.innerHTML = "";
  toolCards.clear();
  lastAssistantStamp = null;
  contextUsed = 2400;
  renderContextDonut();
  renderCostEstimate();
  currentSession = newCurrentSession();
  setSessionTitle("");
  addSystem("New session · context cleared", nowTime());
}

function setSessionTitle(title) {
  currentSession.title = title || "";
  if (currentSession.title) {
    headerTitle.textContent = currentSession.title;
    headerTitle.title = currentSession.title;
    headerTitle.hidden = false;
  } else {
    headerTitle.textContent = "";
    headerTitle.removeAttribute("title");
    headerTitle.hidden = true;
  }
}

function setSessionTitleFromText(text) {
  const collapsed = String(text || "").replace(/\s+/g, " ").trim();
  if (!collapsed) return;
  const max = 60;
  setSessionTitle(collapsed.length > max ? collapsed.slice(0, max - 1) + "…" : collapsed);
}

function archiveCurrentSession(reason) {
  // Skip empty sessions (no user input yet).
  if (!currentSession.firstUserText && currentSession.msgs === 0) return;

  const summary = {
    id: currentSession.id,
    title: currentSession.title || (currentSession.firstUserText
      ? currentSession.firstUserText.slice(0, 60)
      : "Untitled session"),
    snippet: (currentSession.lastAssistantText || currentSession.firstUserText || "").slice(0, 200),
    when: new Date().toLocaleString(),
    msgs: currentSession.msgs,
    tokens: contextUsed,
    cli: currentCli,
    model: currentModel,
    pinned: false,
    reason: reason || "",
  };

  // Snapshot transcript HTML — read-only replay only in v1.
  const html = transcript.innerHTML;
  const truncated = html.length > 500_000 ? html.slice(0, 500_000) + "<!-- truncated -->" : html;
  historyStore.saveSnapshot(currentSession.id, truncated);

  const list = historyStore.loadIndex();
  // Replace any prior entry with the same id (shouldn't happen, but be safe).
  const next = [summary, ...list.filter((s) => s.id !== summary.id)];
  historyStore.saveIndex(historyStore.trimToLimit(next));
  refreshHistoryCount();
}

function refreshHistoryCount() {
  const list = historyStore.loadIndex();
  if (historyCountEl) historyCountEl.textContent = `· ${list.length}`;
}

function renderHistory() {
  const list = historyStore.loadIndex();
  refreshHistoryCount();
  const q = historyQuery.toLowerCase();
  const filtered = !q ? list : list.filter((s) =>
    (s.title || "").toLowerCase().includes(q) || (s.snippet || "").toLowerCase().includes(q));
  const pinned = filtered.filter((s) => s.pinned);
  const recent = filtered.filter((s) => !s.pinned);

  if (filtered.length === 0) {
    historyBody.innerHTML = `<div class="history-empty">${q ? `No sessions match &ldquo;${escapeHtml(q)}&rdquo;` : "No previous sessions yet"}</div>`;
    return;
  }

  const renderItem = (s) => {
    const active = s.id === currentSession.id;
    const tokK = (Number(s.tokens || 0) / 1000).toFixed(1);
    return `
      <button type="button" class="history-item${active ? " is-active" : ""}" data-id="${escapeHtml(s.id)}">
        <div class="history-item-row">
          <span class="history-item-title">${escapeHtml(s.title || "Untitled session")}</span>
          <span class="history-item-when">${escapeHtml(s.when || "")}</span>
        </div>
        <div class="history-item-snippet">${escapeHtml(s.snippet || "")}</div>
        <div class="history-item-meta">
          <span>${s.msgs || 0} msgs</span>
          <span>·</span>
          <span>${tokK}k tok</span>
          <span>·</span>
          <span>${escapeHtml(s.cli || "")}</span>
          <span class="spacer"></span>
          <button type="button" class="history-item-delete" data-delete="${escapeHtml(s.id)}" title="Delete session">
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13"/></svg>
          </button>
        </div>
      </button>
    `;
  };

  const renderGroup = (label, items) => {
    if (!items.length) return "";
    return `
      <div class="history-group">
        <div class="history-group-label">${escapeHtml(label)} <span class="count">· ${items.length}</span></div>
        <div class="history-items">${items.map(renderItem).join("")}</div>
      </div>
    `;
  };

  historyBody.innerHTML = renderGroup("Pinned", pinned) + renderGroup("Recent", recent);

  historyBody.querySelectorAll(".history-item").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      // Per-item delete intercepts via stopPropagation, but the outer click also fires
      // when delete is hit; guard with the data-delete check.
      if (e.target && e.target.closest && e.target.closest("[data-delete]")) return;
      const id = btn.getAttribute("data-id");
      if (id) loadHistorySession(id);
    });
  });
  historyBody.querySelectorAll("[data-delete]").forEach((btn) => {
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      const id = btn.getAttribute("data-delete");
      if (!id) return;
      historyStore.deleteSession(id);
      renderHistory();
    });
  });
}

function loadHistorySession(id) {
  const snapshot = historyStore.loadSnapshot(id);
  if (!snapshot) return;
  archiveCurrentSession("switching session");

  const list = historyStore.loadIndex();
  const summary = list.find((s) => s.id === id);

  transcript.innerHTML = snapshot.html;
  toolCards.clear();
  lastAssistantStamp = null;

  currentSession = newCurrentSession();
  currentSession.id = id;
  if (summary) {
    setSessionTitle(summary.title || "");
    currentSession.firstUserText = summary.title || "";
    currentSession.lastAssistantText = summary.snippet || "";
    currentSession.msgs = summary.msgs || 0;
    contextUsed = Number(summary.tokens || 0) || 0;
  }
  renderContextDonut();
  renderCostEstimate();

  closeHistory();
  addSystem(`Loaded past session · ${summary ? summary.title : id}`, nowTime());
}

function openHistory() { renderHistory(); historyOverlay.classList.add("is-open"); }
function closeHistory() { historyOverlay.classList.remove("is-open"); }

newChatButton.addEventListener("click", clearTranscript);
pinButton.addEventListener("click", togglePin);
attachButton.addEventListener("click", () => {
  // No native file picker yet — drop is the primary path.
});

settingsButton.addEventListener("click", openSettings);
settingsCloseButton.addEventListener("click", closeSettings);
settingsDoneButton.addEventListener("click", closeSettings);
settingsOverlay.addEventListener("click", (e) => {
  if (!settingsCard.contains(e.target)) closeSettings();
});

historyButton.addEventListener("click", openHistory);
historyCloseButton.addEventListener("click", closeHistory);
historyOverlay.addEventListener("click", (e) => {
  if (!historyCard.contains(e.target)) closeHistory();
});
historySearch.addEventListener("input", () => {
  historyQuery = historySearch.value.trim();
  historySearchClear.hidden = historyQuery.length === 0;
  renderHistory();
});
historySearchClear.addEventListener("click", () => {
  historySearch.value = "";
  historyQuery = "";
  historySearchClear.hidden = true;
  renderHistory();
  historySearch.focus();
});

composerInput.addEventListener("input", () => {
  autoGrowComposer();
  updateSendButton();
});
composerInput.addEventListener("focus", () => composerBox.classList.add("is-focused"));
composerInput.addEventListener("blur", () => composerBox.classList.remove("is-focused"));
composerInput.addEventListener("keydown", (e) => {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    send();
  }
});
sendButton.addEventListener("click", send);

/* ---------- Host event handling ---------- */

function handleEvent(event) {
  if (!event || typeof event !== "object") return;
  switch (event.type) {
    case "session": {
      documentPath = event.documentPath || documentPath;
      documentName = event.documentName || event.documentPath || "Rhino document";
      addSystem(`Bound document · ${documentName}`);
      if (event.cli) setCli(event.cli);
      if (Array.isArray(event.models)) renderModelChoices(event.models, event.model || "Default");
      else renderModelChoices(fallbackModelsForCli(currentCli), event.model || selectedModelsByCli[currentCli] || "Default");
      setModel(event.model || "Default");
      if (event.pipeName) setMcpPipe(event.pipeName);
      refreshHistoryCount();
      break;
    }
    case "status":
      setMcpStatus(event.status || "MCP");
      break;
    case "input":
      setInputEnabled(Boolean(event.inputEnabled));
      // Belt-and-suspenders: if the host re-enables input while the panel
      // still thinks it's busy (e.g. a `result` event was missed for any
      // reason), reset the send button to its idle state.
      if (event.inputEnabled === true && busy) {
        busy = false;
        updateSendButton();
      }
      break;
    case "message": {
      const role = (event.role || "assistant").toLowerCase();
      const text = event.text || "";
      if (role === "user") {
        // The composer already echoed the local user message; ignore
        // duplicate echoes from the host but accept ones that arrive from
        // outside the composer flow.
        if (text) addUser(text);
      } else if (role === "system") {
        addSystem(text);
      } else if (role === "diagnostic") {
        addWarn("Diagnostic", text);
      } else {
        addAssistant(text);
        bumpContext(Math.ceil(text.length / 4));
      }
      break;
    }
    case "thinking":
      addThinking(event.text || "");
      bumpContext(Math.ceil((event.text || "").length / 4));
      break;
    case "diagnostic":
      addWarn("Diagnostic", event.text || "");
      break;
    case "tool":
      addOrUpdateTool(event);
      bumpContext(
        Math.ceil((typeof event.toolInput === "string" ? event.toolInput.length : 0) / 4) +
        Math.ceil((typeof event.toolResult === "string" ? event.toolResult.length : 0) / 4)
      );
      break;
    case "result":
      // We display a token-based cost estimate via renderCostEstimate(); the
      // host's totalCostUsd is often 0 on subscription plans, so we ignore it.
      busy = false;
      updateSendButton();
      break;
    case "busy":
      busy = Boolean(event.busy);
      updateSendButton();
      break;
  }
}

if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener("message", (event) => handleEvent(event.data));
}

/* ---------- Boot ---------- */

buildSettingsChoices();
setModel("Default");
setCli("claude code cli");
setMcpStatus("Starting");
setMcpPipe("starting…");
renderContextDonut();
renderCostEstimate();
refreshHistoryCount();
setSessionTitle("");
applyPinState();
setInputEnabled(false);
updateSendButton();
autoGrowComposer();

post("ready");
