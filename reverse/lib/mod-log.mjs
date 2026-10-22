// Resume o log de operação do mod (Documentos\TurboTraffic-IV\TurboTrafficIV.log).
//
// As linhas só têm hora (HH:mm:ss). Desde a rotação por sessão, cada arquivo
// começa com "Sessão de <data>" e o da sessão anterior fica em
// TurboTrafficIV.anterior.log; logs antigos (sem rotação) misturam várias
// sessões, separadas pela linha "---- TurboTraffic-IV iniciando ----". Cada
// sessão é resumida à parte: mensagens agrupadas por formato (números viram
// N), varreduras do turbo e da limpeza, erros e se o limite de linhas foi
// atingido.
//
// Uso: node mod-log.mjs <saida.json> <log> [log...]   (do mais antigo ao mais novo)

import { readFileSync, writeFileSync, statSync, existsSync } from "node:fs";

const [, , outPath, ...logPaths] = process.argv;
const sources = logPaths.filter((p) => existsSync(p));
if (!outPath || sources.length === 0) {
  console.error("uso: node mod-log.mjs <saida.json> <log> [log...]");
  process.exit(2);
}

const lines = sources.flatMap((p) => readFileSync(p, "utf8").split(/\r?\n/).filter(Boolean));

const LINE = /^(\d\d:\d\d:\d\d)\s{2}(.*)$/;
const kv = (s) => Object.fromEntries([...s.matchAll(/(\w+)=([^\s]+)/g)].map(([, k, v]) => [k, v]));

const sessions = [];
let cur = null;
const newSession = (time) => ({
  date: null, started: time, ended: time, lines: 0, line_limit_hit: false,
  config: null, patterns: {}, errors: [],
  turbo: { samples: 0, max_total: 0, max_in_range: 0, max_affected: 0, errors: 0, skip_mission: 0, skip_no_driver: 0, skip_upside_down: 0 },
  cleanup: { sweeps: 0, vehicles: 0, peds: 0 },
  heartbeat: { count: 0, last: null },
});

for (const raw of lines) {
  const m = raw.match(LINE);
  const time = m ? m[1] : null;
  const msg = m ? m[2] : raw;

  // Cabeçalho da rotação: abre a sessão e traz a data.
  const header = msg.match(/^Sessão de (\d{4}-\d{2}-\d{2})$/);
  if (header) {
    cur = newSession(null);
    cur.date = header[1];
    sessions.push(cur);
    continue;
  }
  // Linha de início: abre sessão nova, a menos que um cabeçalho acabou de abrir.
  const starts = msg.startsWith("---- TurboTraffic-IV iniciando");
  if (!cur || (starts && (cur.lines > 0 || !cur.date))) {
    cur = newSession(time);
    sessions.push(cur);
  }
  if (starts && cur.started === null) cur.started = time;
  cur.lines++;
  if (time) cur.ended = time;

  if (msg.startsWith("(limite de linhas atingido")) cur.line_limit_hit = true;
  if (msg.startsWith("Config carregada:")) cur.config = kv(msg);

  if (msg.startsWith("varredura turbo:")) {
    const v = kv(msg);
    const t = cur.turbo;
    t.samples++;
    t.max_total = Math.max(t.max_total, +v.total || 0);
    t.max_in_range = Math.max(t.max_in_range, +v.noRaio || 0);
    t.max_affected = Math.max(t.max_affected, +v.impulsionados || 0);
    t.errors += +v.erros || 0;
    t.skip_mission += +v.missao || 0;
    t.skip_no_driver += +v.semMotorista || 0;
    t.skip_upside_down += +v.capotados || 0;
    continue;
  }
  // Formato antigo: uma linha por varredura com remoção. Formato novo (resumo a
  // cada 30 s): traz varreduras= e mundo= (máximo de veículos existentes).
  if (msg.startsWith("Limpeza fora do campo de visao:")) {
    const v = kv(msg);
    cur.cleanup.sweeps += v.varreduras !== undefined ? +v.varreduras || 0 : 1;
    cur.cleanup.vehicles += +v.veiculos || 0;
    cur.cleanup.peds += +v.peds || 0;
    if (v.mundo !== undefined) cur.cleanup.max_world_vehicles = Math.max(cur.cleanup.max_world_vehicles || 0, +v.mundo || 0);
    continue;
  }
  if (msg.startsWith("heartbeat:")) {
    cur.heartbeat.count++;
    cur.heartbeat.last = kv(msg);
    continue;
  }
  if (/exce|erro\b|falh|AVISO/i.test(msg)) cur.errors.push({ time, msg: msg.slice(0, 400) });

  const pattern = msg.replace(/\d+(\.\d+)?/g, "N").slice(0, 120);
  cur.patterns[pattern] = (cur.patterns[pattern] || 0) + 1;
}

const totals = { sessions: sessions.length, lines: lines.length, errors: 0, limit_hits: 0, cleanup_lines: 0 };
for (const s of sessions) {
  totals.errors += s.errors.length;
  totals.limit_hits += s.line_limit_hit ? 1 : 0;
  totals.cleanup_lines += s.cleanup.sweeps;
  s.patterns = Object.entries(s.patterns).sort((a, b) => b[1] - a[1]).slice(0, 25).map(([pattern, count]) => ({ pattern, count }));
}

const result = {
  sources: sources.map((p) => ({ path: p, bytes: statSync(p).size, modified: statSync(p).mtime.toISOString() })),
  confidence: "observed",
  totals,
  sessions,
  limitations: [
    "As linhas só têm hora; a data vem do cabeçalho 'Sessão de' (logs antigos, sem rotação, não têm data).",
    "ModLog limita 2000 linhas por sessão e suprime repetições em 5 s: contagens são do que foi gravado, não do que aconteceu.",
  ],
};

writeFileSync(outPath, JSON.stringify(result, null, 2));
console.log(`mod-log: ${totals.sessions} sessões, ${totals.lines} linhas, ${totals.errors} erros, limite atingido ${totals.limit_hits}x`);
