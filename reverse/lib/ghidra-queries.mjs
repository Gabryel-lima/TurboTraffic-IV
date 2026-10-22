// Responde as perguntas de ghidra-queries.json sobre o GTAIV.exe numa ÚNICA
// sessão do REA com o Ghidra.
//
// Por que MCP e não o CLI: cada comando do CLI (`rea search`, `rea decompile`)
// importa e auto-analisa o executável de novo (~7 min no GTAIV.exe), e o
// snapshot guarda anotações, não a base do Ghidra. Aqui um servidor `rea mcp`
// é iniciado como processo filho e todas as consultas usam a mesma análise.
//
// Tipos de consulta:
//   strings       search_strings e lista os resultados
//   string-usage  string exata -> xrefs -> função que a contém -> pseudocódigo
//   function      pseudocódigo de um procedimento (nome ou endereço)
//
// Uso: node ghidra-queries.mjs <runDir> <GTAIV.exe> <snapshot> <queries.json>

import { spawn, execSync } from "node:child_process";
import { readFileSync, writeFileSync, appendFileSync, existsSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { createInterface } from "node:readline";

const [, , runDir, exePath, snapshotPath, queriesPath] = process.argv;
if (!runDir || !exePath || !snapshotPath || !queriesPath) {
  console.error("uso: node ghidra-queries.mjs <runDir> <exe> <snapshot> <queries.json>");
  process.exit(2);
}

const queries = JSON.parse(readFileSync(queriesPath, "utf8")).queries;
const MAX_EXCERPT = 6000;

// ---------------------------------------------------------------- cliente MCP

const root = execSync("npm root -g", { encoding: "utf8" }).trim();
const entry = join(root, "rea-agents", "scripts", "rea.mjs");
const server = spawn(process.execPath, [entry, "mcp"], { stdio: ["pipe", "pipe", "pipe"], env: process.env });
const serverLog = join(runDir, "ghidra-mcp.stderr.txt");
server.stderr.on("data", (d) => appendFileSync(serverLog, d));

let nextId = 1;
const pending = new Map();
createInterface({ input: server.stdout }).on("line", (line) => {
  let msg;
  try { msg = JSON.parse(line); } catch { return; }
  if (msg.id !== undefined && pending.has(msg.id)) {
    const { resolve, reject } = pending.get(msg.id);
    pending.delete(msg.id);
    msg.error ? reject(new Error(msg.error.message || JSON.stringify(msg.error))) : resolve(msg.result);
  }
});
server.on("exit", (code) => {
  for (const { reject } of pending.values()) reject(new Error(`servidor REA saiu (código ${code}); veja ${serverLog}`));
  pending.clear();
});

function request(method, params) {
  const id = nextId++;
  server.stdin.write(JSON.stringify({ jsonrpc: "2.0", id, method, params }) + "\n");
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}
const notify = (method, params) => server.stdin.write(JSON.stringify({ jsonrpc: "2.0", method, params }) + "\n");

// Respostas brutas de cada chamada, para depurar uma consulta que voltou vazia
// sem precisar refazer os ~7 min de análise.
const rawDir = join(runDir, "ghidra-raw");
mkdirSync(rawDir, { recursive: true });
let rawSeq = 0;

// Chama uma ferramenta e registra a operação em operations.jsonl, como o rea-run.
async function tool(name, args) {
  const started = Date.now();
  let result, failure = null;
  try {
    result = await request("tools/call", { name, arguments: args });
  } catch (e) {
    failure = e.message;
  }
  let payload = null;
  if (result) {
    payload = result.structuredContent ?? null;
    if (!payload) {
      const text = (result.content || []).filter((c) => c.type === "text").map((c) => c.text).join("\n");
      try { payload = JSON.parse(text); } catch { payload = { text }; }
    }
    if (result.isError) failure = payload?.message || payload?.text || "erro da ferramenta";
  }
  const errorCode = failure ? (payload?.code ?? "tool_error") : null;
  const rawPath = join(rawDir, `${String(++rawSeq).padStart(3, "0")}-${name}.json`);
  writeFileSync(rawPath, JSON.stringify({ tool: name, arguments: args, payload }, null, 1));
  appendFileSync(join(runDir, "operations.jsonl"), JSON.stringify({
    at: new Date(started).toISOString(),
    command: `mcp ${name} ${JSON.stringify(args)}`,
    exit_code: failure ? 1 : 0,
    seconds: Number(((Date.now() - started) / 1000).toFixed(2)),
    output: rawPath,
    output_bytes: JSON.stringify(payload ?? {}).length,
    error_code: errorCode,
    error_message: failure,
  }) + "\n");
  if (failure) throw Object.assign(new Error(failure), { code: errorCode });
  return payload;
}

// ------------------------------------------------- leitura defensiva da saída

// As saídas do REA embrulham o resultado em Evidence (normalized_result) ou em
// {result}. Procura recursivamente listas de objetos com "address".
function unwrap(p) { return p?.normalized_result ?? p?.result ?? p; }
function findRows(p, key = "address") {
  const out = [];
  const walk = (v, depth) => {
    if (depth > 6 || !v || typeof v !== "object") return;
    if (Array.isArray(v)) {
      if (v.length && typeof v[0] === "object" && v[0] && key in v[0]) { out.push(...v); return; }
      v.forEach((x) => walk(x, depth + 1));
      return;
    }
    Object.values(v).forEach((x) => walk(x, depth + 1));
  };
  walk(unwrap(p), 0);
  return out;
}
// xrefs e procedure_callers devolvem uma lista de endereços em texto
// (["0x110c078"]); outras ferramentas devolvem objetos com "address". Aceita
// os dois formatos — confundi-los fazia toda consulta de referência vir vazia.
function addressesOf(p) {
  const r = unwrap(p);
  const list = Array.isArray(r) ? r : Array.isArray(r?.value) ? r.value : null;
  if (list && list.every((x) => typeof x === "string")) return list;
  return findRows(p, "address").map((x) => x.from ?? x.from_address ?? x.caller_address ?? x.address).filter(Boolean);
}
const textOf = (row) => row.value ?? row.string ?? row.text ?? row.name ?? "";
function pseudocodeOf(p) {
  const r = unwrap(p);
  return r?.pseudo_code ?? r?.pseudocode ?? r?.code ?? r?.text ?? (typeof r === "string" ? r : JSON.stringify(r, null, 1));
}
function procedureOf(p) {
  const r = unwrap(p);
  const proc = r?.procedure ?? r?.containing_procedure ?? r;
  return { name: proc?.name ?? null, address: proc?.address ?? proc?.entry ?? proc?.start ?? null };
}
const clip = (s) => (s.length > MAX_EXCERPT ? s.slice(0, MAX_EXCERPT) + `\n… (${s.length - MAX_EXCERPT} caracteres omitidos)` : s);

// --------------------------------------------------------------- consultas

async function runQuery(q) {
  if (q.type === "strings") {
    const rows = findRows(await tool("search_strings", { pattern: q.pattern, mode: q.mode || "literal", case_sensitive: !!q.case_sensitive }));
    const lines = rows.slice(0, q.limit || 40).map((r) => `${r.address}  ${JSON.stringify(textOf(r))}`);
    return { found: rows.length, excerpt: `${rows.length} string(s)\n` + lines.join("\n") };
  }

  if (q.type === "string-usage") {
    const rows = findRows(await tool("search_strings", { pattern: q.pattern, mode: "literal", case_sensitive: true }));
    const exact = rows.filter((r) => textOf(r) === q.pattern || textOf(r) === "-" + q.pattern);
    const targets = (exact.length ? exact : rows).slice(0, 3);
    const report = [`${rows.length} string(s) contendo "${q.pattern}"; ${exact.length} exata(s)`];
    const procs = new Map();
    // Uma referência pode estar no código (cai numa função) ou ser um ponteiro
    // em .data (fora de função): nesse caso segue as referências AO ponteiro,
    // que é onde o código aparece. Ex.: "notimefix" <- 0x110c078 (.data) <- código.
    const tryProc = async (address) => {
      try {
        const proc = procedureOf(await tool("resolve_containing_procedure", { address }));
        return proc.address || proc.name ? proc : null;
      } catch { return null; }
    };
    const follow = async (ref, indent) => {
      const proc = await tryProc(ref);
      if (proc) { procs.set(proc.address ?? proc.name, proc); report.push(`${indent}${ref} está em ${proc.name ?? "?"} @ ${proc.address ?? "?"}`); return; }
      const next = addressesOf(await tool("xrefs", { address: ref })).slice(0, 4);
      report.push(`${indent}${ref} fora de função (dado); referenciado por ${next.length ? next.join(", ") : "nada"}`);
      for (const n of next) {
        const p2 = await tryProc(n);
        if (p2) { procs.set(p2.address ?? p2.name, p2); report.push(`${indent}  ${n} está em ${p2.name ?? "?"} @ ${p2.address ?? "?"}`); }
        else report.push(`${indent}  ${n} também fora de função`);
      }
    };

    for (const s of targets) {
      let refs = addressesOf(await tool("xrefs", { address: s.address })).slice(0, 6);
      report.push(`string ${s.address} ${JSON.stringify(textOf(s))}: ${refs.length} referência(s) ${refs.join(", ")}`);

      // Sem xref nenhuma: procura o endereço da string (little-endian) na
      // memória; cada ocorrência é um ponteiro ou um operando imediato. Se a
      // string começa com "-", tenta também o endereço +1.
      if (refs.length === 0) {
        const bases = [parseInt(s.address, 16)];
        if (textOf(s).startsWith("-")) bases.push(bases[0] + 1);
        for (const addr of bases) {
          const le = [0, 8, 16, 24].map((sh) => ((addr >>> sh) & 0xff).toString(16).padStart(2, "0")).join(" ");
          const hits = findRows(await tool("search_bytes", { pattern: le, limit: 20 }), "address").map((h) => h.address).slice(0, 6);
          report.push(`  busca do ponteiro 0x${addr.toString(16)} (${le}): ${hits.length} ocorrência(s) ${hits.join(", ")}`);
          refs = refs.concat(hits);
        }
      }

      for (const ref of refs) await follow(ref, "  ");
    }
    for (const proc of [...procs.values()].slice(0, q.decompile ?? 1)) {
      try {
        const code = pseudocodeOf(await tool("procedure_pseudo_code", { procedure: proc.address ?? proc.name }));
        report.push(`\n// ${proc.name ?? "?"} @ ${proc.address ?? "?"}\n${code}`);
      } catch (e) { report.push(`pseudocódigo indisponível para ${proc.address}: ${e.message.slice(0, 200)}`); }
    }
    return { found: rows.length, procedures: [...procs.values()], excerpt: report.join("\n") };
  }

  if (q.type === "call-sites") {
    // Chamadores de uma função -> pseudocódigo de cada um -> só as linhas que
    // contêm a chamada (q.match). Ex.: todas as criações de pool com tamanhos.
    // procedure_callers dá as funções chamadoras; xrefs dá os pontos de
    // chamada. Usa os dois, porque cada provider/resposta preenche campos
    // diferentes e chamadas resolvidas por um podem faltar no outro.
    const callers = addressesOf(await tool("procedure_callers", { procedure: q.procedure }));
    const sites = addressesOf(await tool("xrefs", { address: q.procedure }));
    const addresses = [...new Set([...callers, ...sites].filter(Boolean))];
    const procs = new Map();
    for (const address of addresses.slice(0, q.limit || 40)) {
      try {
        const proc = procedureOf(await tool("resolve_containing_procedure", { address }));
        const key = proc.address ?? proc.name ?? address;
        if (!procs.has(key)) procs.set(key, { ...proc, address: proc.address ?? address });
      } catch { procs.set(address, { name: null, address }); }
    }
    const rows = addresses;
    const report = [`${callers.length} chamador(es) e ${sites.length} referência(s) a ${q.procedure}; ${procs.size} função(ões) examinada(s)`];
    for (const proc of procs.values()) {
      try {
        const code = pseudocodeOf(await tool("procedure_pseudo_code", { procedure: proc.address ?? proc.name }));
        const hits = code.split(/\r?\n/).map((l) => l.trim()).filter((l) => l.includes(q.match));
        report.push(`${proc.name ?? "?"} @ ${proc.address}: ${hits.length ? hits.join("  |  ") : "(chamada não aparece no pseudocódigo)"}`);
      } catch (e) { report.push(`${proc.address}: pseudocódigo indisponível (${e.message.slice(0, 120)})`); }
    }
    return { found: rows.length, excerpt: report.join("\n") };
  }

  if (q.type === "function") {
    const code = pseudocodeOf(await tool("procedure_pseudo_code", { procedure: q.procedure }));
    return { found: 1, excerpt: code };
  }

  throw new Error(`tipo de consulta desconhecido: ${q.type}`);
}

async function main() {
  await request("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "turbotraffic-reverse", version: "1" } });
  notify("notifications/initialized", {});

  const results = { snapshot: snapshotPath, exe: exePath, opened: null, queries: [] };
  const openArgs = { path: exePath, provider_id: "ghidra" };
  if (existsSync(snapshotPath)) openArgs.snapshot_path = snapshotPath;
  const t0 = Date.now();
  results.opened = unwrap(await tool("open_binary", openArgs));
  // A primeira consulta dispara a importação e a auto-análise (minutos).
  const overview = await tool("binary_overview", {}).catch((e) => ({ error: e.message }));
  console.log(`ghidra: análise pronta em ${((Date.now() - t0) / 1000).toFixed(0)} s`);
  results.overview = unwrap(overview);

  for (const q of queries) {
    const started = Date.now();
    const record = { id: q.id, question: q.question, command: `${q.type} ${q.pattern ?? q.procedure ?? ""}`.trim(), status: "ok" };
    try {
      Object.assign(record, await runQuery(q));
      record.excerpt = clip(record.excerpt);
    } catch (e) {
      record.status = "failed";
      record.excerpt = e.message;
    }
    record.seconds = (Date.now() - started) / 1000;
    console.log(`  ${q.id}: ${record.status} (${record.seconds.toFixed(1)} s)`);
    results.queries.push(record);
    writeFileSync(join(runDir, "ghidra-results.json"), JSON.stringify(results, null, 2));
  }

  // close_binary grava o snapshot (anotações) para a próxima sessão.
  await tool("close_binary", { snapshot_path: snapshotPath, overwrite: true }).catch((e) => console.log(`  snapshot não gravado: ${e.message.slice(0, 160)}`));
  server.stdin.end();
  const failed = results.queries.filter((q) => q.status !== "ok").length;
  console.log(`ghidra: ${results.queries.length - failed}/${results.queries.length} consultas ok`);
  process.exit(failed === results.queries.length && failed > 0 ? 1 : 0);
}

main().catch((e) => {
  console.error(`ghidra: ${e.message}`);
  server.kill();
  process.exit(1);
});
