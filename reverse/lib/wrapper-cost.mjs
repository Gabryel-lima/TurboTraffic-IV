// Custo estático, em chamadas nativas, de cada API da ScriptHookDotNet e de
// cada método do mod que a usa.
//
// Entradas: dois JSON produzidos por `rea inspect-managed-members --format json`
//   1) ScriptHookDotNet.dll  (o wrapper C++/CLI: GTA.Vehicle, GTA.World, ...)
//   2) ResetHandling.net.dll (o mod compilado)
//
// Modelo de custo (derivado do grafo de chamadas CIL, NÃO medido em jogo):
//   native  = chamada a um native nomeado (Scripting.* / unmanaged.Native.*)
//   ctx     = invocação genérica via NativeContext*.Invoke (também é um native)
//   exists  = Exists() — leitura de memória + DoesVehicleExist dentro de SEH C++
//   mem     = leitura direta de memória (unmanaged.MemoryAccess.*), sem native
// "peso" = native + ctx + exists, somando TODOS os ramos do corpo: é um teto
// por execução, não a média. Iterações de laço não são multiplicadas: o corpo
// de um foreach conta uma vez, mas roda uma vez por veículo.
//
// A recursão segue o TOKEN de cada método, não o nome: sobrecargas (DrawText
// tem nove) chamam umas às outras, e somá-las pelo nome inflava o custo.
//
// Uso: node wrapper-cost.mjs <shdn.json> <mod.json> <saida.json>

import { readFileSync, writeFileSync } from "node:fs";

const [, , shdnPath, modPath, outPath] = process.argv;
if (!shdnPath || !modPath || !outPath) {
  console.error("uso: node wrapper-cost.mjs <shdn.json> <mod.json> <saida.json>");
  process.exit(2);
}

const loadEvidence = (p) => JSON.parse(readFileSync(p, "utf8"));
const shdnEv = loadEvidence(shdnPath);
const modEv = loadEvidence(modPath);
const shdn = shdnEv.normalized_result;
const mod = modEv.normalized_result;

const methodByToken = new Map(shdn.methods.map((m) => [m.token, m]));
const edgesByCallerToken = new Map();
for (const e of shdn.call_edges) {
  if (!edgesByCallerToken.has(e.caller_token)) edgesByCallerToken.set(e.caller_token, []);
  edgesByCallerToken.get(e.caller_token).push(e);
}

// Tipos class:N / valuetype:N carregam tokens locais de cada assembly; só a
// categoria é comparável entre o mod e o wrapper.
const normalizeType = (t) => String(t).replace(/^(class|valuetype):\d+$/, "$1");
const shape = (sig) => (sig?.parameter_types || []).map(normalizeType).join(",");

const methodsByName = new Map();
for (const m of shdn.methods) {
  const key = `${m.declaring_type}.${m.name}`;
  if (!methodsByName.has(key)) methodsByName.set(key, []);
  methodsByName.get(key).push(m);
}

function classifyLeaf(e) {
  const target = `${e.target_declaring_type}.${e.target_name}`;
  let m;
  if (/NativeContext\d*\.Invoke$/.test(target)) return { kind: "ctx", name: "NativeContext.Invoke" };
  if ((m = target.match(/<Module>\.(?:Scripting|unmanaged\.Native)\.\?[^.]+\.(\w+)/))) return { kind: "native", name: m[1] };
  if ((m = target.match(/unmanaged\.MemoryAccess\.(\w+)/))) return { kind: "mem", name: m[1] };
  if (/^GTA\.base\.Object\.Exists$/.test(target)) return { kind: "exists", name: "Exists" };
  return null;
}

const SKIP = /NativeContext|Exception|\.ctor$|^System\./;
const emptyCost = () => ({ native: 0, ctx: 0, exists: 0, mem: 0, natives: {} });
const weight = (c) => c.native + c.ctx + c.exists;

function add(into, from) {
  for (const k of ["native", "ctx", "exists", "mem"]) into[k] += from[k];
  for (const [n, c] of Object.entries(from.natives)) into.natives[n] = (into.natives[n] || 0) + c;
}

const memo = new Map();
function costOfToken(token, stack = new Set()) {
  if (memo.has(token)) return memo.get(token);
  const cost = emptyCost();
  if (stack.has(token) || stack.size > 12) return cost;
  stack.add(token);
  for (const e of edgesByCallerToken.get(token) || []) {
    const leaf = classifyLeaf(e);
    if (leaf) {
      cost[leaf.kind]++;
      if (leaf.kind === "native" || leaf.kind === "mem") cost.natives[leaf.name] = (cost.natives[leaf.name] || 0) + 1;
      continue;
    }
    const target = `${e.target_declaring_type}.${e.target_name}`;
    if (e.target_kind !== "method-def" || SKIP.test(target) || !methodByToken.has(e.target_token)) continue;
    add(cost, costOfToken(e.target_token, stack));
  }
  stack.delete(token);
  memo.set(token, cost);
  return cost;
}

// Resolve uma member-ref do mod para a sobrecarga do wrapper com o mesmo nome,
// tipo declarante e forma de assinatura. Mais de uma candidata: usa a mais cara
// e marca como ambígua — o custo vira um teto, nunca uma subestimativa.
const modRefByToken = new Map(mod.member_refs.map((r) => [r.token, r]));
function resolveModTarget(e) {
  const key = `${e.target_declaring_type}.${e.target_name}`;
  const leaf = classifyLeaf(e);
  if (leaf) {
    const c = emptyCost();
    c[leaf.kind] = 1;
    return { cost: c, resolution: "leaf" };
  }
  const candidates = methodsByName.get(key) || [];
  if (candidates.length === 0) return { cost: emptyCost(), resolution: "not-in-wrapper" };
  const ref = modRefByToken.get(e.target_token);
  let matched = candidates;
  if (ref?.signature) {
    const want = shape(ref.signature);
    const exact = candidates.filter((m) => shape(m.signature) === want);
    if (exact.length > 0) matched = exact;
  }
  let best = null;
  for (const m of matched) {
    const c = costOfToken(m.token);
    if (!best || weight(c) > weight(best.cost)) best = { cost: c, token: m.token };
  }
  return { cost: best.cost, token: best.token, resolution: matched.length === 1 ? "exact" : `ambiguous:${matched.length}` };
}

const apiTable = new Map();
const methods = new Map();
for (const e of mod.call_edges) {
  if (!/^GTA\./.test(e.target_declaring_type || "")) continue;
  const api = `${e.target_declaring_type}.${e.target_name}`;
  const { cost, resolution, token } = resolveModTarget(e);
  const apiKey = `${api}#${token ?? resolution}`;

  if (!apiTable.has(apiKey)) {
    const ref = modRefByToken.get(e.target_token);
    apiTable.set(apiKey, {
      api,
      signature: ref?.signature ? `(${(ref.signature.parameter_types || []).map(normalizeType).join(", ")})` : null,
      wrapper_token: token ?? null,
      resolution,
      weight: weight(cost),
      native: cost.native, ctx: cost.ctx, exists: cost.exists, mem: cost.mem,
      natives: cost.natives,
      call_sites: 0,
    });
  }
  apiTable.get(apiKey).call_sites++;

  if (!methods.has(e.caller)) methods.set(e.caller, { method: e.caller, weight: 0, call_sites: 0, apis: {} });
  const m = methods.get(e.caller);
  m.weight += weight(cost);
  m.call_sites++;
  m.apis[api] = (m.apis[api] || 0) + weight(cost);
}

const result = {
  generated_at: new Date().toISOString(),
  model: "static-cil-call-graph/v2-token-resolved",
  confidence: "derived",
  limitations: [
    "Teto estático por execução do método: todos os ramos são somados; iterações de laço não são multiplicadas.",
    "Exists() conta como uma unidade; sua implementação concreta (Vehicle/Ped) varia em tempo de execução.",
    "Sobrecargas são casadas por nome, tipo e forma da assinatura; 'ambiguous:N' usa a candidata mais cara.",
    "Nenhum custo foi medido em jogo: é uma estimativa relativa para priorizar otimizações.",
  ],
  inputs: {
    scripthookdotnet_sha256: shdnEv.subject?.digest?.sha256 ?? null,
    mod_sha256: modEv.subject?.digest?.sha256 ?? null,
  },
  apis: [...apiTable.values()].sort((a, b) => b.weight - a.weight),
  methods: [...methods.values()]
    .map((m) => ({ ...m, apis: Object.entries(m.apis).sort((a, b) => b[1] - a[1]).map(([api, w]) => ({ api, weight: w })) }))
    .sort((a, b) => b.weight - a.weight),
};

writeFileSync(outPath, JSON.stringify(result, null, 2));
console.log(`wrapper-cost: ${result.apis.length} APIs, ${result.methods.length} métodos do mod`);
for (const m of result.methods.slice(0, 5)) console.log(`  ${String(m.weight).padStart(4)}  ${m.method}`);
