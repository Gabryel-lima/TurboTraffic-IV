# TurboTraffic-IV

Mod de GTA IV (1.2.0.59) em C# sobre ScriptHookDotNet, com loader ASI em C++.
Visão geral e funcionalidades: [README.md](README.md).

## Compilar e instalar

`.\Build-TurboTrafficIV.ps1` e depois `.\Install-TurboTrafficIV.ps1`. Sem Visual
Studio o build usa o `csc.exe` do .NET Framework, que só aceita **C# 5**: nada de
`$"..."`, `?.`, `nameof` ou membros com `=>` no código do mod.

Toda leitura do jogo é uma chamada nativa cara (ver achados F-001..F-005).
Nos laços por veículo, use o estado do tick (`BeginTick`, `TickVehicles`,
`Motion`) em vez de reler `Player.Character`, `Position`, `Velocity` ou
`Direction`.

## Engenharia reversa

Perguntas sobre o binário do jogo, a ScriptHook/ScriptHookDotNet ou o custo
real das chamadas do mod usam o fluxo em [reverse/README.md](reverse/README.md):

- Ambiente: `reverse\Setup-ReverseTools.ps1 -Check`, depois sem `-Check` se faltar algo.
- Análise: `reverse\Invoke-ReverseAnalysis.ps1` (adicione `-Ghidra` para o `GTAIV.exe`).
- Diagnóstico de falha: `reverse\runs\<execução>\operations.jsonl` e `run.log`.

O conhecimento acumulado fica em `reverse/knowledge/findings.json`; leia-o antes
de reinvestigar algo. Ao aprender algo novo:

- registre um achado lá, com `kind` honesto (`observed`, `derived`, `inferred`)
  e o artefato de origem por SHA-256;
- ao aplicar uma recomendação no código, mude o `status` do achado para `aplicado`;
- não edite `FINDINGS.md`, que é gerado pelo pipeline.

Nunca versionar binários do jogo, snapshots do Ghidra ou `reverse/runs/`.
