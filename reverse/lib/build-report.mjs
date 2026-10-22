// Gera o relatório de uma execução do pipeline:
//   <runDir>/report.html          página única, sem dependências externas
//   <runDir>/summary.md           resumo curto (bom para colar em issue/PR)
//   reverse/knowledge/FINDINGS.md registro de achados legível, regenerado
//
// Também atualiza reverse/runs/history.json com os números-chave da execução,
// para comparar execuções ao longo do tempo.
//
// Uso: node build-report.mjs <runDir> <findings.json> <runsRoot>

import { readFileSync, writeFileSync, existsSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";

const [, , runDir, findingsPath, runsRoot] = process.argv;
if (!runDir || !findingsPath || !runsRoot) {
  console.error("uso: node build-report.mjs <runDir> <findings.json> <runsRoot>");
  process.exit(2);
}

const readJson = (p) => (existsSync(p) ? JSON.parse(readFileSync(p, "utf8")) : null);
const run = readJson(join(runDir, "run.json"));
if (!run) throw new Error(`run.json ausente em ${runDir}`);
const knowledge = readJson(findingsPath);
const cost = readJson(join(runDir, "wrapper-cost.json"));
const modLog = readJson(join(runDir, "mod-log.json"));
const peGame = readJson(join(runDir, "pe-gtaiv.json"));
const peHook = readJson(join(runDir, "pe-scripthook.json"));
// Sem -Ghidra nesta execução: mostra as respostas da execução mais recente que
// as tiver (a análise leva minutos; não faz sentido refazê-la só para relê-las).
let ghidra = readJson(join(runDir, "ghidra-results.json"));
if (ghidra) ghidra.from_run = null;
else {
  const previous = readdirSync(runsRoot, { withFileTypes: true })
    .filter((d) => d.isDirectory() && /^\d{8}-\d{6}$/.test(d.name))
    .map((d) => d.name).sort().reverse()
    .find((name) => existsSync(join(runsRoot, name, "ghidra-results.json")));
  if (previous) {
    ghidra = readJson(join(runsRoot, previous, "ghidra-results.json"));
    ghidra.from_run = previous;
  }
}
const operationsPath = join(runDir, "operations.jsonl");
const operations = existsSync(operationsPath)
  ? readFileSync(operationsPath, "utf8").split(/\r?\n/).filter(Boolean).map((l) => JSON.parse(l))
  : [];

// Achado fica "rever" quando o artefato atual tem hash diferente do registrado.
const observed = run.artifacts || {};
const findings = knowledge.findings.map((f) => {
  const ref = knowledge.artifacts[f.artifact];
  const now = observed[f.artifact];
  let freshness = "sem-dado";
  if (ref && ref.sha256 === null) freshness = "vivo";
  else if (ref && now?.sha256) freshness = now.sha256 === ref.sha256 ? "atual" : "rever";
  return { ...f, freshness, artifact_name: ref?.name ?? f.artifact };
}).sort((a, b) => a.id.localeCompare(b.id, "pt-BR", { numeric: true }));

// ---------- histórico ----------
const historyPath = join(runsRoot, "history.json");
const history = readJson(historyPath) || [];
const turbo = cost?.methods.find((m) => m.method === "NPCTurboBoost.ApplyTurboLogic");
const entry = {
  run_id: run.run_id,
  finished: run.finished,
  machine: run.machine,
  stages_ok: run.stages.filter((s) => s.status === "ok").length,
  stages_failed: run.stages.filter((s) => s.status === "failed").length,
  mod_sha256: observed["mod-dll"]?.sha256 ?? null,
  apply_turbo_weight: turbo?.weight ?? null,
  findings_open: findings.filter((f) => f.status === "aberto").length,
  findings_to_review: findings.filter((f) => f.freshness === "rever").length,
  log_lines: modLog?.totals.lines ?? null,
};
const filtered = history.filter((h) => h.run_id !== run.run_id);
filtered.push(entry);
writeFileSync(historyPath, JSON.stringify(filtered.slice(-100), null, 2));

// ---------- FINDINGS.md ----------
const kindLabel = { observed: "observado", derived: "derivado", inferred: "inferido", unknown: "desconhecido" };
const md = [
  "# Achados de engenharia reversa",
  "",
  "Gerado por `reverse/lib/build-report.mjs` a partir de `findings.json`. Não edite à mão — edite o JSON.",
  "",
  "| ID | Área | Tipo | Status | Atualidade | Título |",
  "|---|---|---|---|---|---|",
  ...findings.map((f) => `| ${f.id} | ${f.area} | ${kindLabel[f.kind] ?? f.kind} | ${f.status} | ${f.freshness} | ${f.title} |`),
  "",
  ...findings.flatMap((f) => [
    `## ${f.id} — ${f.title}`,
    "",
    `- **Área:** ${f.area} · **Tipo:** ${kindLabel[f.kind] ?? f.kind} · **Status:** ${f.status} · **Artefato:** ${f.artifact_name} (${f.freshness})`,
    `- **Afirmação:** ${f.claim}`,
    `- **Evidência:** ${f.evidence.tool} — ${f.evidence.detail}${f.evidence.evidence_ids?.length ? ` (${f.evidence.evidence_ids.join(", ")})` : ""}`,
    `- **Recomendação:** ${f.recommendation}`,
    `- **Aprendido em:** ${f.learned_at}`,
    "",
  ]),
].join("\n");
writeFileSync(join(dirname(findingsPath), "FINDINGS.md"), md);

// ---------- summary.md ----------
const stageLine = (s) => `| ${s.name} | ${s.status} | ${s.seconds?.toFixed?.(1) ?? "-"} s | ${(s.message || "").replace(/\|/g, "/").slice(0, 160)} |`;
const summary = [
  `# Execução ${run.run_id}`,
  "",
  `Máquina: ${run.machine} · Jogo: ${run.game_path ?? "não encontrado"} · ${run.started} → ${run.finished}`,
  "",
  "| Etapa | Status | Tempo | Mensagem |",
  "|---|---|---|---|",
  ...run.stages.map(stageLine),
  "",
  `Achados: ${findings.length} (${entry.findings_open} abertos, ${entry.findings_to_review} a rever).`,
  turbo ? `Peso estático de ApplyTurboLogic: ${turbo.weight}.` : "",
  modLog ? `Log do mod: ${modLog.totals.sessions} sessões, ${modLog.totals.lines} linhas, ${modLog.totals.errors} erros.` : "",
].join("\n");
writeFileSync(join(runDir, "summary.md"), summary);

// ---------- report.html ----------
const data = { run, findings, cost, modLog, peGame, peHook, ghidra, operations, history: filtered.slice(-30), knownBuilds: run.known_builds || {} };
const json = JSON.stringify(data).replace(/</g, "\\u003c");

const html = `<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>TurboTraffic RE</title>
<style>
:root{--bg:#f7f7f5;--panel:#fff;--ink:#1d1d1b;--muted:#6b6b66;--line:#e2e1dc;--accent:#2f5bd3;--ok:#1f7a4d;--warn:#a15c00;--bad:#b3261e;--chip:#eef0f6}
@media (prefers-color-scheme:dark){:root:not([data-theme="light"]){--bg:#151614;--panel:#1d1f1c;--ink:#ecebe6;--muted:#a3a29b;--line:#30322e;--accent:#8aa8ff;--ok:#5fc795;--warn:#e0a34a;--bad:#f2867d;--chip:#262a33}}
:root[data-theme="dark"]{--bg:#151614;--panel:#1d1f1c;--ink:#ecebe6;--muted:#a3a29b;--line:#30322e;--accent:#8aa8ff;--ok:#5fc795;--warn:#e0a34a;--bad:#f2867d;--chip:#262a33}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif}
header{padding:20px 16px 8px;max-width:1200px;margin:0 auto}h1{margin:0 0 4px;font-size:20px}h2{font-size:16px;margin:0 0 10px}
.muted{color:var(--muted)}main{max-width:1200px;margin:0 auto;padding:0 16px 40px;display:grid;gap:14px}
section{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:14px 16px;overflow:hidden}
nav{display:flex;gap:6px;flex-wrap:wrap;margin-top:10px}nav a{color:var(--accent);text-decoration:none;font-size:13px;padding:3px 8px;border-radius:999px;background:var(--chip)}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px}
.stat{border:1px solid var(--line);border-radius:8px;padding:10px}.stat b{display:block;font-size:20px}
.scroll{overflow-x:auto}table{border-collapse:collapse;width:100%;font-size:13px}th,td{text-align:left;padding:6px 8px;border-bottom:1px solid var(--line);vertical-align:top}
th{color:var(--muted);font-weight:600;white-space:nowrap}td.num{text-align:right;font-variant-numeric:tabular-nums}
.chip{display:inline-block;padding:1px 8px;border-radius:999px;font-size:12px;background:var(--chip);white-space:nowrap}
.ok{color:var(--ok)}.warn{color:var(--warn)}.bad{color:var(--bad)}
.bar{height:8px;background:var(--chip);border-radius:4px;min-width:60px}.bar>i{display:block;height:100%;border-radius:4px;background:var(--accent)}
.filters{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:10px}select,input{font:inherit;padding:4px 8px;border-radius:6px;border:1px solid var(--line);background:var(--panel);color:var(--ink)}
details{border-top:1px solid var(--line);padding:8px 0}details:first-of-type{border-top:0}summary{cursor:pointer}
code{font-family:ui-monospace,Consolas,monospace;font-size:12px}button{font:inherit;border:1px solid var(--line);background:var(--panel);color:var(--ink);border-radius:6px;padding:3px 10px;cursor:pointer}
</style>
</head>
<body>
<header>
  <div style="display:flex;justify-content:space-between;gap:10px;flex-wrap:wrap;align-items:baseline">
    <h1>TurboTraffic-IV · engenharia reversa</h1>
    <button id="theme" type="button">Tema</button>
  </div>
  <div class="muted" id="meta"></div>
  <nav><a href="#etapas">Etapas</a><a href="#achados">Achados</a><a href="#custo">Custo do mod</a><a href="#log">Log do mod</a><a href="#binario">Binários</a><a href="#ghidra">Ghidra</a><a href="#historico">Histórico</a></nav>
</header>
<main>
  <section id="etapas"><h2>Etapas desta execução</h2><div class="grid" id="stats"></div><div class="scroll" style="margin-top:12px"><table id="stages"></table></div><details style="margin-top:8px"><summary>Operações do REA (<span id="opCount">0</span>)</summary><div class="scroll"><table id="ops"></table></div></details></section>
  <section id="achados"><h2>Achados</h2>
    <div class="filters"><select id="fArea"></select><select id="fStatus"></select><select id="fFresh"></select><input id="fText" placeholder="buscar" size="16"></div>
    <div id="findings"></div></section>
  <section id="custo"><h2>Custo estático em natives</h2><p class="muted" id="costNote"></p><div class="scroll"><table id="methods"></table></div><h2 style="margin-top:16px">APIs da ScriptHookDotNet usadas pelo mod</h2><div class="scroll"><table id="apis"></table></div></section>
  <section id="log"><h2>Log de operação do mod</h2><div class="grid" id="logStats"></div><div class="scroll" style="margin-top:12px"><table id="sessions"></table></div><h2 style="margin-top:16px">Mensagens mais frequentes (última sessão)</h2><div class="scroll"><table id="patterns"></table></div></section>
  <section id="binario"><h2>Binários</h2><div id="pe"></div></section>
  <section id="ghidra"><h2>Consultas no Ghidra</h2><div id="gh"></div></section>
  <section id="historico"><h2>Histórico de execuções</h2><div class="scroll"><table id="hist"></table></div></section>
</main>
<script type="application/json" id="data">${json}</script>
<script>
const D=JSON.parse(document.getElementById('data').textContent);
const $=id=>document.getElementById(id);
const esc=s=>String(s??'').replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const table=(el,head,rows)=>{$(el).innerHTML='<tr>'+head.map(h=>'<th>'+h+'</th>').join('')+'</tr>'+rows.join('')};
try{const t=localStorage.getItem('re-theme');if(t)document.documentElement.dataset.theme=t}catch(e){}
$('theme').onclick=()=>{const r=document.documentElement;const dark=r.dataset.theme?r.dataset.theme==='dark':matchMedia('(prefers-color-scheme: dark)').matches;r.dataset.theme=dark?'light':'dark';try{localStorage.setItem('re-theme',r.dataset.theme)}catch(e){}};
const R=D.run;
$('meta').textContent='Execução '+R.run_id+' · '+R.machine+' · jogo: '+(R.game_path||'não encontrado');
const st=s=>s==='ok'?'<span class="chip ok">ok</span>':s==='failed'?'<span class="chip bad">falhou</span>':'<span class="chip warn">'+esc(s)+'</span>';
table('stages',['Etapa','Status','Tempo','Mensagem'],R.stages.map(s=>'<tr><td>'+esc(s.name)+'</td><td>'+st(s.status)+'</td><td class="num">'+(s.seconds!=null?s.seconds.toFixed(1)+' s':'-')+'</td><td>'+esc(s.message)+'</td></tr>'));
$('opCount').textContent=D.operations.length;
table('ops',['Quando','Comando','Saída','Tempo','Tamanho','Erro'],D.operations.map(o=>'<tr><td>'+esc(o.at.slice(11,19))+'</td><td><code>'+esc(o.command.replace(/ --format json$/,''))+'</code></td><td>'+st(o.exit_code===0&&!o.error_code?'ok':'failed')+'</td><td class="num">'+o.seconds+' s</td><td class="num">'+(o.output_bytes/1024).toFixed(0)+' KB</td><td>'+esc(o.error_code?o.error_code+': '+o.error_message:'')+'</td></tr>'));
const F=D.findings;
const stat=(label,v,cls)=>'<div class="stat"><span class="muted">'+label+'</span><b class="'+(cls||'')+'">'+v+'</b></div>';
$('stats').innerHTML=stat('Etapas ok',R.stages.filter(s=>s.status==='ok').length+'/'+R.stages.length)
 +stat('Achados abertos',F.filter(f=>f.status==='aberto').length)
 +stat('A rever',F.filter(f=>f.freshness==='rever').length,F.some(f=>f.freshness==='rever')?'warn':'')
 +stat('Artefatos identificados',Object.values(R.artifacts||{}).filter(a=>a.sha256).length);
const opts=(el,label,vals)=>{$(el).innerHTML='<option value="">'+label+': todos</option>'+[...new Set(vals)].sort().map(v=>'<option>'+esc(v)+'</option>').join('')};
opts('fArea','Área',F.map(f=>f.area));opts('fStatus','Status',F.map(f=>f.status));opts('fFresh','Atualidade',F.map(f=>f.freshness));
const kind={observed:'observado',derived:'derivado',inferred:'inferido',unknown:'desconhecido'};
const fresh=f=>f==='rever'?'<span class="chip warn">rever</span>':f==='atual'?'<span class="chip ok">atual</span>':'<span class="chip">'+esc(f)+'</span>';
function renderFindings(){const a=$('fArea').value,s=$('fStatus').value,fr=$('fFresh').value,q=$('fText').value.toLowerCase();
 $('findings').innerHTML=F.filter(f=>(!a||f.area===a)&&(!s||f.status===s)&&(!fr||f.freshness===fr)&&(!q||JSON.stringify(f).toLowerCase().includes(q))).map(f=>
 '<details><summary><b>'+esc(f.id)+'</b> '+esc(f.title)+' <span class="chip">'+esc(f.area)+'</span> <span class="chip">'+(kind[f.kind]||esc(f.kind))+'</span> '+fresh(f.freshness)+'</summary>'+
 '<p>'+esc(f.claim)+'</p><p><b>Recomendação:</b> '+esc(f.recommendation)+'</p><p class="muted">Evidência: '+esc(f.evidence.tool)+' — '+esc(f.evidence.detail)+(f.evidence.evidence_ids&&f.evidence.evidence_ids.length?' · <code>'+f.evidence.evidence_ids.map(esc).join(', ')+'</code>':'')+' · artefato '+esc(f.artifact_name)+' · '+esc(f.status)+' · '+esc(f.learned_at)+'</p></details>').join('')||'<p class="muted">Nenhum achado com esses filtros.</p>'}
['fArea','fStatus','fFresh','fText'].forEach(id=>$(id).addEventListener('input',renderFindings));renderFindings();
if(D.cost){$('costNote').textContent=D.cost.limitations.join(' ');
 const max=Math.max(1,...D.cost.methods.map(m=>m.weight));
 table('methods',['Método do mod','Peso','','Principais APIs'],D.cost.methods.slice(0,25).map(m=>'<tr><td><code>'+esc(m.method)+'</code></td><td class="num">'+m.weight+'</td><td style="width:120px"><div class="bar"><i style="width:'+(100*m.weight/max)+'%"></i></div></td><td>'+m.apis.slice(0,4).map(x=>esc(x.api.replace(/^GTA\\./,''))+' ('+x.weight+')').join(', ')+'</td></tr>'));
 table('apis',['API','Assinatura','Peso','natives','ctx','Exists','mem','Natives chamados','Sites'],D.cost.apis.filter(a=>a.weight>0||a.mem>0).slice(0,40).map(a=>'<tr><td><code>'+esc(a.api)+'</code></td><td><code>'+esc(a.signature)+'</code></td><td class="num">'+a.weight+'</td><td class="num">'+a.native+'</td><td class="num">'+a.ctx+'</td><td class="num">'+a.exists+'</td><td class="num">'+a.mem+'</td><td>'+esc(Object.keys(a.natives).join(', '))+'</td><td class="num">'+a.call_sites+'</td></tr>'));
}else $('custo').innerHTML+='<p class="muted">Etapa managed não executada.</p>';
if(D.modLog){const t=D.modLog.totals;$('logStats').innerHTML=stat('Sessões',t.sessions)+stat('Linhas',t.lines)+stat('Erros',t.errors,t.errors?'bad':'ok')+stat('Limite atingido',t.limit_hits+'x',t.limit_hits?'warn':'')+stat('Linhas de limpeza',t.cleanup_lines);
 table('sessions',['Data','Início','Fim','Linhas','Limite','Turbo: máx. no raio','Turbo: erros','Limpeza: veículos/peds','Config'],D.modLog.sessions.map(s=>'<tr><td>'+esc(s.date||'-')+'</td><td>'+esc(s.started)+'</td><td>'+esc(s.ended)+'</td><td class="num">'+s.lines+'</td><td>'+(s.line_limit_hit?'<span class="chip warn">sim</span>':'')+'</td><td class="num">'+s.turbo.max_in_range+'</td><td class="num">'+s.turbo.errors+'</td><td class="num">'+s.cleanup.vehicles+'/'+s.cleanup.peds+'</td><td><code>'+esc(s.config?Object.entries(s.config).map(([k,v])=>k+'='+v).join(' '):'')+'</code></td></tr>'));
 const last=D.modLog.sessions[D.modLog.sessions.length-1];table('patterns',['Mensagem','Vezes'],(last?last.patterns:[]).map(p=>'<tr><td><code>'+esc(p.pattern)+'</code></td><td class="num">'+p.count+'</td></tr>'));
}else $('log').innerHTML+='<p class="muted">Log do mod não encontrado.</p>';
function peBlock(p,title){if(!p)return '<p class="muted">'+title+': não analisado.</p>';
 const build=D.knownBuilds[p.artifact.sha256];
 return '<h2>'+esc(title)+'</h2><p class="muted"><code>'+esc(p.artifact.sha256)+'</code> · '+(p.artifact.bytes/1048576).toFixed(1)+' MB · link '+esc(p.timestamp.slice(0,10))+(build?' · '+esc(build):'')+'</p>'+
 '<p>Entry '+esc(p.entry.rva)+' em <code>'+esc(p.entry.section)+'</code> · '+p.characteristics.concat(p.dll_characteristics).map(c=>'<span class="chip">'+esc(c)+'</span>').join(' ')+'</p>'+
 '<div class="scroll"><table><tr><th>Seção</th><th>Tam. virtual</th><th>Tam. em disco</th><th>Flags</th><th>Entropia</th><th></th></tr>'+p.sections.map(s=>'<tr><td><code>'+esc(s.name)+'</code></td><td class="num">'+s.virtual_size.toLocaleString()+'</td><td class="num">'+s.raw_size.toLocaleString()+'</td><td>'+esc(s.flags.join(' '))+'</td><td class="num '+(s.entropy>7.2?'warn':'')+'">'+s.entropy.toFixed(2)+'</td><td style="width:120px"><div class="bar"><i style="width:'+(100*s.entropy/8)+'%"></i></div></td></tr>').join('')+'</table></div>'+
 (p.probes?'<p><b>Strings sondadas:</b> '+p.probes.map(x=>'<span class="chip '+(x.present?'ok':'')+'">'+esc(x.term)+(x.present?'':' ✗')+'</span>').join(' ')+'</p>':'')+
 '<details><summary>Imports ('+Object.keys(p.imports).length+' DLLs)</summary>'+Object.entries(p.imports).map(([d,f])=>'<p><b>'+esc(d)+'</b> <span class="muted">'+esc(f.join(', '))+'</span></p>').join('')+'</details>'+
 (p.exports.length?'<details><summary>Exports ('+p.exports.length+')</summary><p><code>'+p.exports.map(esc).join('<br>')+'</code></p></details>':'')}
$('pe').innerHTML=peBlock(D.peGame,'GTAIV.exe')+peBlock(D.peHook,'ScriptHook.dll');
if(D.ghidra&&D.ghidra.queries){$('gh').innerHTML=(D.ghidra.from_run?'<p><span class="chip warn">da execução '+esc(D.ghidra.from_run)+'</span> esta execução não rodou o Ghidra; use -Ghidra para atualizar.</p>':'')+'<p class="muted">Snapshot: <code>'+esc(D.ghidra.snapshot)+'</code></p>'+D.ghidra.queries.map(q=>'<details><summary><b>'+esc(q.id)+'</b> '+esc(q.question)+' '+st(q.status)+'</summary><p class="muted"><code>'+esc(q.command)+'</code></p><pre style="white-space:pre-wrap;max-height:360px;overflow:auto"><code>'+esc(q.excerpt)+'</code></pre></details>').join('')}
else $('gh').innerHTML='<p class="muted">Etapa Ghidra não executada nesta execução.</p>';
table('hist',['Execução','Máquina','Etapas ok/falhas','Peso ApplyTurboLogic','Achados abertos','A rever','Linhas do log'],D.history.slice().reverse().map(h=>'<tr><td>'+esc(h.run_id)+'</td><td>'+esc(h.machine)+'</td><td>'+h.stages_ok+'/'+h.stages_failed+'</td><td class="num">'+(h.apply_turbo_weight??'-')+'</td><td class="num">'+h.findings_open+'</td><td class="num">'+h.findings_to_review+'</td><td class="num">'+(h.log_lines??'-')+'</td></tr>'));
</script>
</body>
</html>`;
writeFileSync(join(runDir, "report.html"), html);
console.log(`report: ${join(runDir, "report.html")}`);
