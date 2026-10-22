# Engenharia reversa do TurboTraffic-IV

Ferramentas para analisar o GTA IV, a ScriptHook e o próprio mod com o
[REA](https://rea.tools/) (Reverse Engineer Anything) e o Ghidra, e para guardar
o que se aprende num registro versionado junto com o código.

```
reverse/
├── Setup-ReverseTools.ps1     instala e configura REA + JDK + Ghidra (idempotente)
├── Invoke-ReverseAnalysis.ps1 roda o pipeline e gera o relatório
├── reverse.config.json        versões fixadas e parâmetros (fonte da verdade)
├── ghidra-queries.json        perguntas feitas ao GTAIV.exe pela etapa Ghidra
├── knowledge/
│   ├── findings.json          registro de achados (edite este)
│   └── FINDINGS.md            gerado a partir do JSON (não edite)
├── lib/                       analisadores em Node.js
├── runs/                      saídas de cada execução (não versionado)
└── .local/                    caminhos desta máquina e caches (não versionado)
```

## Em uma máquina nova

Pré-requisitos: Windows x64, Node.js 24.11 ou mais novo, e este repositório.

```powershell
.\reverse\Setup-ReverseTools.ps1 -Check   # só mostra o que falta
.\reverse\Setup-ReverseTools.ps1          # mostra o plano, pede confirmação e instala
```

O setup instala exatamente as versões de `reverse.config.json`:

| Ferramenta | Como | Onde |
|---|---|---|
| REA (`rea-agents`) | `npm install --global` | npm global |
| JDK 21 | `winget` (só se não houver JDK ≥ 21) | `C:\Program Files\Microsoft\jdk-21…` |
| Ghidra 12.1.4 | download do release oficial, SHA-256 conferido | `C:\tools\ghidra_12.1.4_PUBLIC` |

Ele também grava `reverse\.local\env.json` com os caminhos desta máquina e
registra o servidor MCP do REA no agente (Claude Code por padrão) com uma
prévia do `rea setup` antes de aplicar. Nenhuma variável de ambiente global do
Windows é alterada, e a pasta do jogo nunca é tocada.

## Rodar a análise

```powershell
.\reverse\Invoke-ReverseAnalysis.ps1 -Open           # rápido: managed, PE e log do mod
.\reverse\Invoke-ReverseAnalysis.ps1 -Ghidra -Open   # inclui as consultas no Ghidra
```

| Etapa | O que faz | Precisa do jogo? | Tempo típico |
|---|---|---|---|
| Managed | REA lê `ScriptHookDotNet.dll` e a DLL do mod; calcula o custo em natives de cada método do mod | não | ~5 s (cache por SHA-256) |
| Pe | cabeçalhos, seções, entropia, imports/exports e sondas de strings do `GTAIV.exe` e `ScriptHook.dll`; recursos PE via REA | sim | ~2 s |
| ModLog | resume `Documentos\TurboTraffic-IV\TurboTrafficIV.log` por sessão | não | <1 s |
| Ghidra | responde as perguntas de `ghidra-queries.json` no `GTAIV.exe` | sim | ~7 min na 1ª vez nesta máquina (7,7 GB RAM) |
| Report | gera `report.html`, `summary.md`, `FINDINGS.md` e o histórico | não | <1 s |

Sem o jogo instalado, as etapas Managed, ModLog e Report funcionam: a
`ScriptHookDotNet.dll` analisada é a que está em `scripts\` do repositório,
idêntica à do jogo.

## Ler os resultados

Cada execução fica em `reverse\runs\<data-hora>\`. São mantidas só as últimas
10 (`analysis.keepRuns` em `reverse.config.json`); o resumo de todas continua em
`runs\history.json`. O último relatório também é copiado para
`reverse\runs\latest.html`, e o log do setup é um único `runs\setup.log`,
reescrito a cada execução.

- **report.html**: etapas e tempos, a lista de operações do REA, achados com
  filtros (área, status, atualidade, texto), custo do mod, log do mod, binários,
  respostas do Ghidra e histórico entre execuções.
- **run.log**: o que o pipeline fez, em ordem.
- **operations.jsonl**: uma linha por chamada ao REA, com comando, duração,
  tamanho da saída e, quando falha, o código e a causa do erro. É o primeiro
  lugar para olhar quando algo dá errado.
- **summary.md**: resumo curto para colar em issue ou PR.
- **runs\history.json**: números-chave de cada execução. Por exemplo, o peso de
  `ApplyTurboLogic` deve cair quando uma otimização do laço do turbo entra.

### Como ler o "peso" do custo do mod

O peso é a soma de natives, invocações genéricas e `Exists()` que um método
**pode** disparar, somando todos os ramos. É um teto estático, não uma medição:

- serve para **ordenar** onde otimizar, não para dizer quantos natives rodam;
- laços não são multiplicados: o corpo de um `foreach` conta uma vez;
- métodos com muitos ramos aparecem inflados. Exemplo (achado M-001):
  `Graphics.DrawText` dá 28 no teto, mas cada chamada real usa cerca de 2.

## Registrar um achado

O conhecimento acumulado vive em `knowledge\findings.json`. Cada achado tem:

| Campo | Significado |
|---|---|
| `id` | `F-nnn` para achados sobre jogo/mod, `M-nnn` para lições de método |
| `area` | `mod-desempenho`, `mod-correcao`, `portas`, `jogo-binario`, `jogo-config`, `logs`, `metodo` |
| `kind` | `observed` (visto nos bytes/log), `derived` (calculado a partir do observado), `inferred` (hipótese), `unknown` |
| `status` | `aberto`, `aplicado`, `verificado`, `rejeitado`, `aprendido` |
| `artifact` | chave em `artifacts` (com SHA-256) de onde o achado veio |
| `claim` / `recommendation` | o que se sabe e o que fazer com isso |
| `evidence` | ferramenta, detalhe e, quando houver, os `evidence_id` do REA |

Quando o binário de um achado muda (outra build do jogo ou um novo build do
mod), o relatório marca o achado como **rever**: a afirmação pode não valer
mais. Depois de conferir, atualize o SHA-256 em `artifacts` ou o status do
achado. Depois de aplicar uma recomendação no código, mude o status para
`aplicado`, e para `verificado` quando uma execução confirmar o efeito.

## Perguntar algo novo ao Ghidra

Acrescente uma entrada em `ghidra-queries.json` e rode com `-Ghidra`. A
primeira execução importa e analisa o `GTAIV.exe` (minutos); as seguintes
reaproveitam o snapshot em `.local\cache`.

## Limites conhecidos

- O `GTAIV.exe` tem o entry point num stub (`.rkstr`) e uma seção `.tbm`
  com entropia 8,0. O Ghidra reconhece ~33 mil funções no `.text`, mas parte
  do comportamento só aparece em memória (achado F-011).
- O REA não executa o jogo. Tudo aqui é estático, salvo o log do mod, que
  registra o que aconteceu em sessões reais.
- Binários do jogo, snapshots e saídas de execução nunca são versionados.
