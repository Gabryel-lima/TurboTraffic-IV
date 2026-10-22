// Acrescenta variáveis ao registro MCP "rea" do Claude Code (.claude.json).
//
// O `rea setup` registra GHIDRA_INSTALL_DIR, JAVA_HOME, REA_ANALYSIS_PROVIDER e
// o prazo de startup, mas não o heap do Ghidra nem os analisadores desligados;
// sem eles o GTAIV.exe estoura memória/prazo numa sessão MCP. O setup preserva
// chaves extras do registro, então elas podem ser acrescentadas aqui com
// segurança. Só altera o que difere, e faz backup antes de gravar.
//
// Uso: node merge-agent-env.mjs <.claude.json> CHAVE=VALOR [CHAVE=VALOR...]

import { readFileSync, writeFileSync, copyFileSync } from "node:fs";

const [, , file, ...pairs] = process.argv;
if (!file || pairs.length === 0) {
  console.error("uso: node merge-agent-env.mjs <.claude.json> CHAVE=VALOR...");
  process.exit(2);
}

const cfg = JSON.parse(readFileSync(file, "utf8"));
const server = cfg.mcpServers?.rea;
if (!server) {
  console.log("sem registro rea; nada a fazer");
  process.exit(0);
}

const extra = Object.fromEntries(pairs.map((p) => [p.slice(0, p.indexOf("=")), p.slice(p.indexOf("=") + 1)]));
server.env = server.env || {};
const changed = Object.keys(extra).filter((k) => server.env[k] !== extra[k]);
if (changed.length === 0) {
  console.log("registro já atualizado");
  process.exit(0);
}

copyFileSync(file, file + ".turbotraffic.backup");
for (const k of changed) server.env[k] = extra[k];
writeFileSync(file, JSON.stringify(cfg, null, 2));
console.log("acrescentado: " + changed.join(", ") + " (backup em .claude.json.turbotraffic.backup)");
