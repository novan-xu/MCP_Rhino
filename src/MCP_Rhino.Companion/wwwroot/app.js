const transcript = document.getElementById("transcript");
const docName = document.getElementById("docName");
const docPath = document.getElementById("docPath");
const mcpStatus = document.getElementById("mcpStatus");
const modelStatus = document.getElementById("modelStatus");
const costStatus = document.getElementById("costStatus");
const input = document.getElementById("composerInput");
const sendButton = document.getElementById("sendButton");
const stopButton = document.getElementById("stopButton");

const toolCards = new Map();

function post(type, payload = {}) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage({ type, ...payload });
  }
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function renderText(value) {
  const escaped = escapeHtml(value);
  const parts = escaped.split(/```/g);
  if (parts.length === 1) {
    return escaped
      .replace(/`([^`]+)`/g, "<code>$1</code>")
      .replace(/\n{2,}/g, "</p><p>")
      .replace(/\n/g, "<br>");
  }

  return parts.map((part, index) => {
    if (index % 2 === 1) {
      return `<pre>${part.trim()}</pre>`;
    }

    return part
      .replace(/`([^`]+)`/g, "<code>$1</code>")
      .replace(/\n{2,}/g, "</p><p>")
      .replace(/\n/g, "<br>");
  }).join("");
}

function addMessage(role, text) {
  const wrapper = document.createElement("article");
  wrapper.className = `message ${role}`;
  wrapper.innerHTML = `
    <div class="message-label">${escapeHtml(role)}</div>
    <div class="bubble"><p>${renderText(text)}</p></div>
  `;
  transcript.appendChild(wrapper);
  scrollToEnd();
}

function addThinking(text) {
  const wrapper = document.createElement("article");
  wrapper.className = "message thinking";
  wrapper.innerHTML = `
    <details>
      <summary>Thinking</summary>
      <pre>${escapeHtml(text)}</pre>
    </details>
  `;
  transcript.appendChild(wrapper);
  scrollToEnd();
}

function updateToolCard(event) {
  const id = event.toolUseId || `${event.toolName}-${toolCards.size}`;
  let card = toolCards.get(id);

  if (!card) {
    card = document.createElement("article");
    card.className = "tool-card";
    card.innerHTML = `
      <div class="tool-header">
        <div class="tool-name">${escapeHtml(event.toolName || "Rhino tool")}</div>
        <div class="tool-status running">Running</div>
      </div>
      <div class="tool-body"></div>
    `;
    toolCards.set(id, card);
    transcript.appendChild(card);
  }

  const status = card.querySelector(".tool-status");
  status.textContent = event.status || "running";
  status.className = `tool-status ${event.status || "running"}`;

  const body = card.querySelector(".tool-body");
  const sections = [];
  if (event.toolInput) {
    sections.push(`
      <div>
        <div class="tool-section-title">Input</div>
        <pre>${escapeHtml(prettyJson(event.toolInput))}</pre>
      </div>
    `);
  }

  if (event.toolResult) {
    sections.push(`
      <div>
        <div class="tool-section-title">Result</div>
        <pre>${escapeHtml(prettyJson(event.toolResult))}</pre>
      </div>
    `);
  }

  body.innerHTML = sections.join("");
  scrollToEnd();
}

function prettyJson(value) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function setInputEnabled(enabled) {
  input.disabled = !enabled;
  sendButton.disabled = !enabled;
}

function scrollToEnd() {
  transcript.scrollTop = transcript.scrollHeight;
}

function send() {
  const text = input.value.trim();
  if (!text) {
    return;
  }

  input.value = "";
  post("send", { text });
}

function handleEvent(event) {
  switch (event.type) {
    case "session":
      docName.textContent = event.documentName || "Rhino document";
      docPath.textContent = event.documentPath || "";
      modelStatus.textContent = event.model || "Default model";
      break;
    case "status":
      mcpStatus.textContent = event.status || "Status";
      break;
    case "input":
      setInputEnabled(Boolean(event.inputEnabled));
      break;
    case "message":
      addMessage(event.role || "assistant", event.text || "");
      break;
    case "thinking":
      addThinking(event.text || "");
      break;
    case "diagnostic":
      addMessage("diagnostic", event.text || "");
      break;
    case "tool":
      updateToolCard(event);
      break;
    case "result":
      if (typeof event.totalCostUsd === "number") {
        costStatus.textContent = `$${event.totalCostUsd.toFixed(4)}`;
      }
      break;
  }
}

sendButton.addEventListener("click", send);
stopButton.addEventListener("click", () => post("stop"));
input.addEventListener("keydown", event => {
  if (event.key === "Enter" && event.ctrlKey) {
    event.preventDefault();
    send();
  }
});

if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener("message", event => handleEvent(event.data));
}

setInputEnabled(false);
post("ready");
