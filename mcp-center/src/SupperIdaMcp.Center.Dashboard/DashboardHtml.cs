namespace SupperIdaMcp.Center.Dashboard;

internal static class DashboardHtml
{
    public const string Page = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Supper IDA MCP Center</title>
  <style>
    :root {
      color-scheme: light dark;
      font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      background: #f5f7fb;
      color: #162033;
    }
    * { box-sizing: border-box; }
    body { margin: 0; min-height: 100vh; }
    header {
      padding: 20px 28px;
      background: #101827;
      color: #f8fbff;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
    }
    h1 { font-size: 20px; margin: 0; font-weight: 680; }
    main { padding: 24px 28px; display: grid; gap: 20px; }
    section { display: grid; gap: 12px; }
    h2 { margin: 0; font-size: 15px; color: #344054; }
    table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #d7deea; }
    th, td { text-align: left; padding: 10px 12px; border-bottom: 1px solid #e5eaf3; font-size: 13px; vertical-align: top; }
    th { background: #eef3fb; color: #344054; font-weight: 650; }
    tr:last-child td { border-bottom: 0; }
    code { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12px; }
    button { border: 1px solid #b8c3d8; background: #fff; color: #162033; padding: 6px 10px; border-radius: 6px; cursor: pointer; }
    button:hover { background: #eef3fb; }
    .status { font-size: 13px; color: #b9c7dc; }
    .empty { padding: 14px; background: #fff; border: 1px solid #d7deea; color: #667085; font-size: 13px; }
    .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px; }
    @media (max-width: 900px) { .grid { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
  <header>
    <h1>Supper IDA MCP Center</h1>
    <div class="status" id="status">loading</div>
  </header>
  <main>
    <section>
      <h2>Registered IDA Windows</h2>
      <div id="targets"></div>
    </section>
    <section>
      <h2>Active Agent Calls</h2>
      <div id="activity"></div>
    </section>
    <div class="grid">
      <section>
        <h2>Launched Processes</h2>
        <div id="processes"></div>
      </section>
      <section>
        <h2>IDA Installations</h2>
        <div id="installations"></div>
      </section>
    </div>
    <section>
      <h2>Operation Log</h2>
      <div id="logs"></div>
    </section>
  </main>
  <script>
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
    const table = (columns, rows, empty) => {
      if (!rows.length) return `<div class="empty">${esc(empty)}</div>`;
      return `<table><thead><tr>${columns.map(c => `<th>${esc(c.label)}</th>`).join('')}</tr></thead><tbody>${rows.map(row => `<tr>${columns.map(c => `<td>${c.render(row)}</td>`).join('')}</tr>`).join('')}</tbody></table>`;
    };
    async function closeTarget(instanceId) {
      await fetch('/api/close-target', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ instanceId, force: false }) });
      await refresh();
    }
    async function refresh() {
      const response = await fetch('/api/state', { cache: 'no-store' });
      const state = await response.json();
      document.getElementById('status').textContent = `updated ${new Date(state.generatedAtUtc).toLocaleTimeString()}`;
      document.getElementById('targets').innerHTML = table([
        { label: 'Alias', render: r => esc(r.alias) },
        { label: 'Binary', render: r => esc(r.binaryName) },
        { label: 'Input', render: r => `<code>${esc(r.inputPath)}</code>` },
        { label: 'PID', render: r => esc(r.processId) },
        { label: 'Health', render: r => esc(r.health) },
        { label: 'Last Seen', render: r => esc(new Date(r.lastSeenUtc).toLocaleTimeString()) },
        { label: 'Action', render: r => `<button onclick="closeTarget('${esc(r.instanceId)}')">Close</button>` }
      ], state.targets, 'No IDA windows registered');
      document.getElementById('activity').innerHTML = table([
        { label: 'Target', render: r => esc(r.targetAlias) },
        { label: 'Tool', render: r => esc(r.toolName) },
        { label: 'Started', render: r => esc(new Date(r.startedAtUtc).toLocaleTimeString()) }
      ], state.activeOperations, 'No active calls');
      document.getElementById('processes').innerHTML = table([
        { label: 'PID', render: r => esc(r.processId) },
        { label: 'Input', render: r => `<code>${esc(r.inputPath)}</code>` },
        { label: 'Executable', render: r => `<code>${esc(r.executablePath)}</code>` },
        { label: 'Exited', render: r => esc(r.hasExited) }
      ], state.launchedProcesses, 'No processes launched by center');
      document.getElementById('installations').innerHTML = table([
        { label: 'Path', render: r => `<code>${esc(r.path)}</code>` },
        { label: 'Source', render: r => esc(r.source) },
        { label: 'Exists', render: r => esc(r.exists) }
      ], state.installations, 'No IDA installations discovered');
      document.getElementById('logs').innerHTML = table([
        { label: 'Time', render: r => esc(new Date(r.timestampUtc).toLocaleTimeString()) },
        { label: 'Target', render: r => esc(r.targetAlias) },
        { label: 'Tool', render: r => esc(r.toolName) },
        { label: 'Success', render: r => esc(r.success) },
        { label: 'Error', render: r => esc(r.error) }
      ], state.operationLog, 'No operations logged');
    }
    refresh();
    setInterval(refresh, 1500);
  </script>
</body>
</html>
""";
}
