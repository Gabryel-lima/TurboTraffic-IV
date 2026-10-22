// Executa um comando do CLI do REA, grava a saída JSON num arquivo e registra
// a operação em <runDir>/operations.jsonl (comando, duração, código de saída,
// tamanho da saída e, se o REA devolveu erro estruturado, o código do erro).
//
// Existe porque o Windows PowerShell 5.1 grava `>` em UTF-16 e quebra o JSON;
// aqui a saída do REA vai byte a byte para o arquivo.
//
// Uso: node rea-run.mjs <runDir> <saida.json> -- <args do rea...>

import { spawn, execSync } from "node:child_process";
import { createWriteStream, appendFileSync, readFileSync, statSync, existsSync } from "node:fs";
import { join } from "node:path";

const argv = process.argv.slice(2);
const sep = argv.indexOf("--");
if (sep < 2) {
  console.error("uso: node rea-run.mjs <runDir> <saida.json> -- <args do rea...>");
  process.exit(2);
}
const [runDir, outPath] = argv;
const reaArgs = argv.slice(sep + 1);

function resolveReaEntry() {
  const root = execSync("npm root -g", { encoding: "utf8" }).trim();
  const entry = join(root, "rea-agents", "scripts", "rea.mjs");
  if (!existsSync(entry)) throw new Error(`rea-agents não encontrado em ${entry}; rode reverse\\Setup-ReverseTools.ps1`);
  return entry;
}

const entry = resolveReaEntry();
const started = Date.now();
const out = createWriteStream(outPath);
const errPath = outPath + ".stderr.txt";
const err = createWriteStream(errPath);

const child = spawn(process.execPath, [entry, ...reaArgs], { stdio: ["ignore", "pipe", "pipe"], env: process.env });
child.stdout.pipe(out);
child.stderr.pipe(err);

// pipe() fecha os arquivos sozinho quando o processo termina; só registrar a
// operação quando o código de saída E os dois arquivos estiverem completos.
const finished = (stream) => new Promise((resolve) => (stream.writableFinished ? resolve() : stream.on("finish", resolve)));
const exited = new Promise((resolve) => child.on("close", resolve));

Promise.all([exited, finished(out), finished(err)]).then(([code]) => {
  {
    const seconds = (Date.now() - started) / 1000;
    let errorCode = null;
    let errorMessage = null;
    // Erro do REA: objeto com code + category e sem evidence_id (às vezes com
    // um campo "error" também). A causa concreta costuma estar em details.issues.
    try {
      if (statSync(outPath).size < 1_000_000) {
        const parsed = JSON.parse(readFileSync(outPath, "utf8"));
        if (parsed && !parsed.evidence_id && parsed.code && (parsed.category || parsed.error)) {
          errorCode = parsed.code;
          const issue = parsed.details?.issues?.[0]?.message;
          errorMessage = [parsed.message, issue].filter(Boolean).join(" — ");
        }
      }
    } catch { /* saída não-JSON: sem código de erro estruturado */ }
    const record = {
      at: new Date(started).toISOString(),
      command: ["rea", ...reaArgs].join(" "),
      exit_code: code,
      seconds: Number(seconds.toFixed(2)),
      output: outPath,
      output_bytes: statSync(outPath).size,
      error_code: errorCode,
      error_message: errorMessage,
    };
    appendFileSync(join(runDir, "operations.jsonl"), JSON.stringify(record) + "\n");
    console.log(`rea ${reaArgs[0]}: exit=${code} ${record.seconds}s ${record.output_bytes}B${errorCode ? " erro=" + errorCode : ""}`);
    process.exit(code === 0 && !errorCode ? 0 : 1);
  }
});
