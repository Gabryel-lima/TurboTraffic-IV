// Leitura estrutural de um PE32 (GTAIV.exe, ScriptHook.dll, ...) sem
// disassembler: identidade, flags do cabeçalho, seções com entropia, imports,
// exports, magia de recursos RCDATA e presença de strings de interesse.
//
// Complementa o REA (que faz recursos e, com Ghidra, código). Tudo aqui é
// observação direta dos bytes do arquivo — nada é executado.
//
// Uso: node pe-layout.mjs <arquivo.exe|dll> <saida.json> [strings-de-interesse.json]

import { readFileSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { basename } from "node:path";

const [, , inPath, outPath, probesPath] = process.argv;
if (!inPath || !outPath) {
  console.error("uso: node pe-layout.mjs <arquivo> <saida.json> [strings.json]");
  process.exit(2);
}

const b = readFileSync(inPath);
const pe = b.readUInt32LE(0x3c);
if (b.toString("latin1", pe, pe + 4) !== "PE\0\0") throw new Error("não é um PE");
const coff = pe + 4;
const opt = coff + 20;
const magic = b.readUInt16LE(opt);
if (magic !== 0x10b) throw new Error("apenas PE32 (x86) é suportado por este leitor");

const characteristics = b.readUInt16LE(coff + 18);
const dllCharacteristics = b.readUInt16LE(opt + 70);
const sectionCount = b.readUInt16LE(coff + 2);
const imageBase = b.readUInt32LE(opt + 28);
const entryRva = b.readUInt32LE(opt + 16);
const dataDir = (i) => ({ rva: b.readUInt32LE(opt + 96 + i * 8), size: b.readUInt32LE(opt + 100 + i * 8) });

function entropy(start, length) {
  const n = Math.min(length, 8 << 20);
  if (n <= 0) return 0;
  const h = new Uint32Array(256);
  for (let k = 0; k < n; k++) h[b[start + k]]++;
  let e = 0;
  for (const x of h) if (x) { const p = x / n; e -= p * Math.log2(p); }
  return Number(e.toFixed(3));
}

const flagNames = (v, table) => Object.entries(table).filter(([, bit]) => v & bit).map(([n]) => n);
const sections = [];
let so = opt + b.readUInt16LE(coff + 16);
for (let i = 0; i < sectionCount; i++, so += 40) {
  const s = {
    name: b.toString("latin1", so, so + 8).replace(/\0.*$/s, ""),
    virtual_address: b.readUInt32LE(so + 12),
    virtual_size: b.readUInt32LE(so + 8),
    raw_size: b.readUInt32LE(so + 16),
    raw_offset: b.readUInt32LE(so + 20),
    flags: flagNames(b.readUInt32LE(so + 36), { code: 0x20, initialized: 0x40, uninitialized: 0x80, execute: 0x20000000, read: 0x40000000, write: 0x80000000 }),
  };
  s.entropy = entropy(s.raw_offset, s.raw_size);
  sections.push(s);
}

const rvaToOffset = (rva) => {
  for (const s of sections) if (rva >= s.virtual_address && rva < s.virtual_address + Math.max(s.virtual_size, s.raw_size)) return rva - s.virtual_address + s.raw_offset;
  return -1;
};
const cstr = (off) => (off < 0 ? null : b.toString("latin1", off, b.indexOf(0, off)));
const sectionOf = (rva) => sections.find((s) => rva >= s.virtual_address && rva < s.virtual_address + Math.max(s.virtual_size, s.raw_size))?.name ?? null;

const imports = {};
const imp = dataDir(1);
if (imp.rva) {
  for (let d = rvaToOffset(imp.rva); ; d += 20) {
    const nameRva = b.readUInt32LE(d + 12);
    if (!nameRva) break;
    const dll = cstr(rvaToOffset(nameRva));
    const fns = [];
    for (let t = rvaToOffset(b.readUInt32LE(d) || b.readUInt32LE(d + 16)); ; t += 4) {
      const v = b.readUInt32LE(t);
      if (!v) break;
      fns.push(v & 0x80000000 ? `#${v & 0xffff}` : cstr(rvaToOffset(v) + 2));
    }
    imports[dll] = fns;
  }
}

const exports = [];
const exp = dataDir(0);
if (exp.rva) {
  const ed = rvaToOffset(exp.rva);
  const count = b.readUInt32LE(ed + 24);
  const names = rvaToOffset(b.readUInt32LE(ed + 32));
  for (let i = 0; i < count; i++) exports.push(cstr(rvaToOffset(b.readUInt32LE(names + 4 * i))));
}

// Strings de interesse: o arquivo de sondas lista termos (ex.: flags de linha
// de comando do motor). Só registra presença e a primeira posição no arquivo.
let probes = null;
if (probesPath) {
  const list = JSON.parse(readFileSync(probesPath, "utf8"));
  probes = list.map((term) => {
    const at = b.indexOf(Buffer.from(term + "\0", "latin1"));
    return { term, present: at >= 0, file_offset: at >= 0 ? at : null, section: at >= 0 ? sections.find((s) => at >= s.raw_offset && at < s.raw_offset + s.raw_size)?.name ?? null : null };
  });
}

const result = {
  artifact: { name: basename(inPath), path: inPath, bytes: b.length, sha256: createHash("sha256").update(b).digest("hex") },
  confidence: "observed",
  timestamp: new Date(b.readUInt32LE(coff + 4) * 1000).toISOString(),
  image_base: "0x" + imageBase.toString(16),
  entry: { rva: "0x" + entryRva.toString(16), section: sectionOf(entryRva) },
  characteristics: flagNames(characteristics, { large_address_aware: 0x20, dll: 0x2000, x86_32bit: 0x100 }),
  dll_characteristics: flagNames(dllCharacteristics, { dynamic_base_aslr: 0x40, nx_compat: 0x100, no_seh: 0x400, guard_cf: 0x4000 }),
  sections,
  imports,
  exports,
  probes,
  limitations: [
    "Leitura estática dos bytes em disco; seções cifradas/comprimidas só revelam seu conteúdo em memória.",
    "Entropia acima de ~7.2 em seção de código sugere conteúdo cifrado ou comprimido (inferência, não prova).",
    "Presença de uma string não prova que o código a usa; o uso exige referências cruzadas (Ghidra).",
  ],
};

writeFileSync(outPath, JSON.stringify(result, null, 2));
console.log(`pe-layout: ${result.artifact.name} sha256=${result.artifact.sha256.slice(0, 12)}… seções=${sections.length} imports=${Object.keys(imports).length} exports=${exports.length}`);
