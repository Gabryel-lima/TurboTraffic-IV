# Achados de engenharia reversa

Gerado por `reverse/lib/build-report.mjs` a partir de `findings.json`. Não edite à mão — edite o JSON.

| ID | Área | Tipo | Status | Atualidade | Título |
|---|---|---|---|---|---|
| F-001 | mod-desempenho | derivado | aplicado | atual | Exists() custa um native + leitura de memória + SEH, e toda propriedade o chama de novo |
| F-002 | mod-desempenho | derivado | aplicado | atual | Player.Character.Position é recalculado para cada veículo do laço |
| F-003 | mod-desempenho | derivado | aplicado | atual | World.GetAllVehicles() é chamado até 3x por tick |
| F-004 | mod-desempenho | derivado | aplicado | atual | Velocity/Speed/Direction são as propriedades mais caras do laço do turbo |
| F-005 | mod-desempenho | derivado | aplicado | atual | FreezePosition = false é emitido em todo frame para todo veículo do raio |
| F-006 | mod-correcao | derivado | aplicado | atual | A ScriptHookDotNet guarda os wrappers num cache por handle |
| F-007 | portas | derivado | verificado | atual | Function.Call("NOME") converte e procura o native pelo nome a cada chamada |
| F-008 | portas | observado | aberto | atual | Portas da ScriptHookDotNet ainda não usadas pelo mod |
| F-009 | portas | observado | aberto | atual | ScriptHook.dll exporta uma API C++ para mods nativos |
| F-010 | jogo-binario | observado | aberto | atual | GTAIV.exe é LAA e usa ASLR (base em runtime 0x1f0000) |
| F-011 | jogo-binario | inferido | aberto | atual | Entry point em seção RWX (.rkstr) e seção .tbm com entropia 8,0: binário protegido/empacotado |
| F-012 | jogo-binario | observado | aberto | atual | Recurso SPAFILE é um XDBF do Games for Windows Live |
| F-013 | jogo-config | observado | aberto | atual | Flags de linha de comando do motor presentes no binário |
| F-014 | jogo-config | observado | aberto | atual | O jogo cria o Direct3D 9 só via Direct3DCreate9 |
| F-015 | logs | observado | aplicado | vivo | O log do mod cresce entre sessões sem rotação |
| F-016 | mod-correcao | inferido | aberto | atual | MyLoader.asi provavelmente carrega a ScriptHookDotNet em duplicidade |
| F-017 | jogo-binario | observado | aberto | atual | O stub do entry point resolve módulos percorrendo o PEB, sem a tabela de imports |
| F-018 | jogo-config | observado | aberto | atual | As flags de linha de comando ficam numa tabela de parâmetros no .data |
| F-019 | jogo-binario | observado | aberto | atual | Pool 'VehicleStruct' criado com 0x32 entradas de 0x20C bytes |
| F-020 | jogo-binario | observado | aberto | atual | O jogo recusa converter um ped quando o pool de peds está cheio |
| F-021 | jogo-binario | observado | aberto | atual | Tabela de pools do motor: o pool 'Vehicles' tem 140 vagas |
| F-022 | mod-desempenho | derivado | aberto | atual | Nas sessões registradas o trânsito nunca encostou no limite do pool de veículos |
| M-001 | metodo | derivado | aprendido | atual | O custo do wrapper-cost é um teto: soma todos os ramos |
| M-002 | metodo | observado | aprendido | atual | Ghidra no GTAIV.exe precisa de mais que o prazo padrão do REA |
| M-003 | metodo | observado | aprendido | atual | Cada comando do CLI do REA reanalisa o GTAIV.exe do zero |
| M-004 | metodo | observado | aprendido | atual | Resposta vazia não é ausência: conferir o payload bruto |

## F-001 — Exists() custa um native + leitura de memória + SEH, e toda propriedade o chama de novo

- **Área:** mod-desempenho · **Tipo:** derivado · **Status:** aplicado · **Artefato:** ScriptHookDotNet.dll (atual)
- **Afirmação:** Vehicle.InternalCheckExists lê a posição na memória (MemoryAccess.GetPositionOfVehicle) e chama o native DoesVehicleExist dentro de try/catch C++. Todo getter/setter de Vehicle/Ped começa com callvirt base.Object.Exists e lança NonExistingObjectException se falhar.
- **Evidência:** rea inspect-managed-members — arestas GTA.base.Object.Exists -> InternalCheckExists; GTA.Vehicle.InternalCheckExists -> get_Position, DoesVehicleExist (ev_b883c50a2afedfc8b0d3eacb12cc2cf49ef6d15f71b1b7725ff384b29d4454d0)
- **Recomendação:** Aplicado: os laços de turbo, dano e limpeza não chamam mais v.Exists() depois da enumeração do tick; cada veículo é isolado por try/catch, que agora cobre também as leituras de posição/missão. A verificar em jogo.
- **Aprendido em:** 2026-10-21

## F-002 — Player.Character.Position é recalculado para cada veículo do laço

- **Área:** mod-desempenho · **Tipo:** derivado · **Status:** aplicado · **Artefato:** ResetHandling.net.dll (atual)
- **Afirmação:** Player.get_Character chama ConvertIntToPlayerIndex + GetPlayerChar; Ped.get_Position chama Exists + GetCharCoordinates. ApplyTurboLogic e ApplyDamageFlags fazem isso dentro do foreach de veículos: ~4 natives por veículo por frame só para o filtro de raio.
- **Evidência:** reverse/lib/wrapper-cost.mjs sobre rea inspect-managed-members — ResetHandling/ResetVehicleScript.cs linhas 880 e 1032
- **Recomendação:** Aplicado: BeginTick lê Player.Character, a posição e o carro do jogador uma vez por tick; turbo, dano, limpeza, ímã e invencibilidade usam essa cópia. Peso estático de ApplyTurboLogic 28 -> 14, ApplyDamageFlags 21 -> 3, CleanupOffscreenDebris 24 -> 6.
- **Aprendido em:** 2026-10-21

## F-003 — World.GetAllVehicles() é chamado até 3x por tick

- **Área:** mod-desempenho · **Tipo:** derivado · **Status:** aplicado · **Artefato:** ScriptHookDotNet.dll (atual)
- **Afirmação:** GetAllVehicles = GetValidVehicleHandles (MemoryAccess.GetVehicleHandleList + DoesVehicleExist + GetCarModel2 por veículo, List e ToArray novos) + ContentCache.GetVehicle por handle. O mod chama em ApplyTurboLogic, ApplyDamageFlags (todo tick) e CleanupOffscreenDebris (a cada 750 ms).
- **Evidência:** rea inspect-managed-members — arestas GTA.World.GetValidVehicleHandles (ev_b883c50a2afedfc8b0d3eacb12cc2cf49ef6d15f71b1b7725ff384b29d4454d0)
- **Recomendação:** Aplicado: TickVehicles busca World.GetAllVehicles() uma vez por tick, só quando alguma passada precisa; com tudo no padrão só a limpeza (a cada 750 ms) lê o pool.
- **Aprendido em:** 2026-10-21

## F-004 — Velocity/Speed/Direction são as propriedades mais caras do laço do turbo

- **Área:** mod-desempenho · **Tipo:** derivado · **Status:** aplicado · **Artefato:** ScriptHookDotNet.dll (atual)
- **Afirmação:** Teto estático: get_Velocity = Exists×3 + GetCarHeading + 1 invoke; set_Velocity = Exists×5 + GetCarHeading + 2 invokes; set_Speed = Exists×2 + 4 invokes + IsThisModelATrain; get_Direction = Exists + GetCarHeading. Modos combinados repetem leituras no mesmo carro.
- **Evidência:** reverse/lib/wrapper-cost.mjs — tabela 'APIs' do relatório
- **Recomendação:** Aplicado: struct Motion com Position/Forward/Velocity lidos uma vez por veículo; os modos escrevem via WriteForwardSpeed/WriteVelocity, que espelham a escrita na cópia. Ímã usa |Velocity| no lugar de v.Speed (GET_CAR_SPEED); premissa: são o mesmo valor. Peso por carro: Legado/VelMax 14 -> 7, Ímã 21 -> 8, Solto 15 -> 8.
- **Aprendido em:** 2026-10-21

## F-005 — FreezePosition = false é emitido em todo frame para todo veículo do raio

- **Área:** mod-desempenho · **Tipo:** derivado · **Status:** aplicado · **Artefato:** ResetHandling.net.dll (atual)
- **Afirmação:** set_FreezePosition = Exists + FreezeCarPosition. WakeVehicle roda para todo veículo afetado em todo tick.
- **Evidência:** reverse/lib/wrapper-cost.mjs — ResetVehicleScript.cs WakeVehicle
- **Recomendação:** Aplicado: _wokenVehicles guarda quem já foi descongelado; FreezePosition=false só no primeiro contato com o raio. Quem sai do raio sai do conjunto. Risco a observar em jogo: carro recongelado por script de missão enquanto continua no raio não seria descongelado de novo.
- **Aprendido em:** 2026-10-21

## F-006 — A ScriptHookDotNet guarda os wrappers num cache por handle

- **Área:** mod-correcao · **Tipo:** derivado · **Status:** aplicado · **Artefato:** ScriptHookDotNet.dll (atual)
- **Afirmação:** World.GetAllVehicles passa por ContentCache.GetVehicle, que faz Dictionary.TryGetValue pelo handle antes de criar um GTA.Vehicle novo. O comentário do mod que diz que a biblioteca entrega um wrapper novo a cada tick parece desatualizado.
- **Evidência:** rea inspect-managed-members — arestas GTA.ContentCache.GetVehicle (ev_b883c50a2afedfc8b0d3eacb12cc2cf49ef6d15f71b1b7725ff384b29d4454d0)
- **Recomendação:** Aplicado: comentário do HandleObjectComparer corrigido; o comparador continua (outros caminhos e o despejo do cache podem entregar wrappers diferentes).
- **Aprendido em:** 2026-10-21

## F-007 — Function.Call("NOME") converte e procura o native pelo nome a cada chamada

- **Área:** portas · **Tipo:** derivado · **Status:** verificado · **Artefato:** ScriptHookDotNet.dll (atual)
- **Afirmação:** GTA.Native.Function.Invoke faz Marshal.StringToHGlobalAnsi + Scripting.NativeContext2.Invoke(char*) + FreeHGlobal por chamada. Os wrappers tipados chamam o native diretamente.
- **Evidência:** rea inspect-managed-members — arestas GTA.Native.Function.Invoke/BaseCall (ev_b883c50a2afedfc8b0d3eacb12cc2cf49ef6d15f71b1b7725ff384b29d4454d0)
- **Recomendação:** Verificado no código: o mod não usa Function.Call em nenhum laço; só os wrappers tipados. Nada a mudar.
- **Aprendido em:** 2026-10-21

## F-008 — Portas da ScriptHookDotNet ainda não usadas pelo mod

- **Área:** portas · **Tipo:** observado · **Status:** aberto · **Artefato:** ScriptHookDotNet.dll (atual)
- **Afirmação:** GTA.value.Globals (ReadInteger/ReadFloat/ReadString/Write) lê e escreve globals dos scripts do jogo; GTA.Native.Function chama qualquer native por nome; unmanaged.MemoryAccess tem GetVehicleHandleList, GetAddressOfVehicle/Ped, GetVehicleRPM, GetGlobalAddress, GetAddressOfItemInPool. 528 natives nomeados têm wrapper.
- **Evidência:** rea inspect-managed-members — métodos dos tipos citados (ev_b883c50a2afedfc8b0d3eacb12cc2cf49ef6d15f71b1b7725ff384b29d4454d0)
- **Recomendação:** MemoryAccess está no namespace unmanaged: acessibilidade a partir do script não verificada.
- **Aprendido em:** 2026-10-21

## F-009 — ScriptHook.dll exporta uma API C++ para mods nativos

- **Área:** portas · **Tipo:** observado · **Status:** aberto · **Artefato:** ScriptHook.dll (atual)
- **Afirmação:** 154 exports, entre eles Game::GetNativeAddressFromHash, NativeInvoke::SpeedyInvoke, Game::GetGlobalAddress, Game::GetBase, ScriptHookManager::RegisterThread(ScriptThread/NativeThread), RequestService e as interfaces ID3DHookService, IKeyboardHookService e IMenuService.
- **Evidência:** reverse/lib/pe-layout.mjs — tabela de exports
- **Recomendação:** Base para um .asi 'core' em C++ que roda o laço quente por veículo e expõe uma API C chamada pelo C# via P/Invoke.
- **Aprendido em:** 2026-10-21

## F-010 — GTAIV.exe é LAA e usa ASLR (base em runtime 0x1f0000)

- **Área:** jogo-binario · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** Cabeçalho com LARGE_ADDRESS_AWARE, DYNAMIC_BASE e NX. Image base preferida 0x400000, mas o scripthook.log registrou 'Process base address: 0x1f0000'.
- **Evidência:** reverse/lib/pe-layout.mjs + scripthook.log — characteristics/dll_characteristics
- **Recomendação:** Não aplicar patch LAA. Todo endereço fixo vindo de tutoriais precisa ser relativo à base (Game::GetBase / NetHook.BaseAddress).
- **Aprendido em:** 2026-10-21

## F-011 — Entry point em seção RWX (.rkstr) e seção .tbm com entropia 8,0: binário protegido/empacotado

- **Área:** jogo-binario · **Tipo:** inferido · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** Entry RVA 0x1aeb000 está em .rkstr (execute+read+write, 657 bytes). .tbm tem 1 MB com entropia 8,00; .text tem entropia 7,01, alta para código x86. Mesmo assim o Ghidra reconheceu 33.573 funções e 41.218 strings no arquivo em disco: o .text não está todo cifrado; a proteção parece concentrada no stub e no .tbm.
- **Evidência:** reverse/lib/pe-layout.mjs + rea inspect --provider ghidra — seções, entropia e inventário do Ghidra (procedure_count, string_count)
- **Recomendação:** A maior parte do código é analisável estaticamente. O que o stub e o .tbm fazem em tempo de execução só aparece num dump da memória do processo carregado.
- **Aprendido em:** 2026-10-21

## F-012 — Recurso SPAFILE é um XDBF do Games for Windows Live

- **Área:** jogo-binario · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** RT_RCDATA 'SPAFILE' (1.108.913 bytes, sha256 70ffac45…) começa com a magia XDBF: arquivo SPA de conquistas/estatísticas do GFWL, embutido mesmo sem import de xlive.dll.
- **Evidência:** rea inspect-pe-resources — recurso índice 0 (ev_ecd8085babfaad17675243d459a8836b3b5642f96e8fe1837c53c17e99c1f91a)
- **Recomendação:** Curiosidade: o XDBF contém nomes e IDs de conquistas; pode ser decodificado para listar as conquistas e estatísticas do jogo.
- **Aprendido em:** 2026-10-21

## F-013 — Flags de linha de comando do motor presentes no binário

- **Área:** jogo-config · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** Strings presentes em .rdata: norestrictions, nomemrestrict, availablevidmem, novblank, noprecache, notimefix, framelockinwindow, disableimposters, nominimize, noloadingtune, windowed, safemode, benchmark. Ausentes: percentvidmem, framelimit. Não existe commandline.txt na pasta do jogo.
- **Evidência:** reverse/lib/pe-layout.mjs (probes) — engineFlagProbes
- **Recomendação:** Presença não prova o efeito; o efeito de cada flag é conhecimento da comunidade. Confirmar o uso via xrefs no Ghidra.
- **Aprendido em:** 2026-10-21

## F-014 — O jogo cria o Direct3D 9 só via Direct3DCreate9

- **Área:** jogo-config · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** Único import de d3d9.dll é Direct3DCreate9. A troca por um d3d9.dll alternativo (ex.: DXVK) na pasta do jogo é possível; o ganho é conhecimento da comunidade, não medido.
- **Evidência:** reverse/lib/pe-layout.mjs — imports
- **Recomendação:** Testar DXVK junto com o HUD: a ScriptHookDotNet engancha o device D3D9 (ID3DDeviceHook).
- **Aprendido em:** 2026-10-21

## F-015 — O log do mod cresce entre sessões sem rotação

- **Área:** logs · **Tipo:** observado · **Status:** aplicado · **Artefato:** TurboTrafficIV.log (vivo)
- **Afirmação:** 14.508 linhas em 15 sessões; limite de 2000 linhas por sessão atingido 3x; 6.620 linhas são 'Limpeza fora do campo de visao'. Nenhuma exceção registrada.
- **Evidência:** reverse/lib/mod-log.mjs — totals
- **Recomendação:** Aplicado: ModLog.StartSession roda o log por sessão (anterior em TurboTrafficIV.anterior.log) e a limpeza agora resume a cada 30 s numa linha com veiculos, peds, varreduras e mundo (máximo de veículos existentes), em vez de uma linha por varredura.
- **Aprendido em:** 2026-10-21

## F-016 — MyLoader.asi provavelmente carrega a ScriptHookDotNet em duplicidade

- **Área:** mod-correcao · **Tipo:** inferido · **Status:** aberto · **Artefato:** ResetHandling.net.dll (atual)
- **Afirmação:** MyLoader.cpp espera 5 s e faz LoadLibraryA("ScriptHookDotNet.asi") com MessageBox; o próprio ASI loader (dinput8.dll) já carrega todo .asi da raiz do jogo.
- **Evidência:** leitura do código-fonte — MyLoader/MyLoader.cpp
- **Recomendação:** Verificar em execução (lista de módulos do processo) antes de remover o MyLoader.
- **Aprendido em:** 2026-10-21

## F-017 — O stub do entry point resolve módulos percorrendo o PEB, sem a tabela de imports

- **Área:** jogo-binario · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** entry() chama FUN_01eeb225 (lê PEB+0xC, a lista de módulos carregados), FUN_01eeb232 duas vezes com 0 e 2 (anda N posições na lista e devolve o DllBase em +0x18) e FUN_01eeb251, que o Ghidra não consegue decodificar ('bad instruction data').
- **Evidência:** ghidra-queries G-entry, G-entry-stub-1..3 — pseudocódigo de entry, 0x01eeb225, 0x01eeb232, 0x01eeb251
- **Recomendação:** Inferência: o stub localiza o próprio EXE (posição 0) e uma DLL do sistema (posição 2) para resolver APIs à mão e preparar o código protegido (.tbm). Para ver o que vem depois, é preciso um dump da memória após o stub terminar.
- **Aprendido em:** 2026-10-21

## F-018 — As flags de linha de comando ficam numa tabela de parâmetros no .data

- **Área:** jogo-config · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** Cada string de flag é apontada por um ponteiro no .data, e esse ponteiro é usado pelo código: notimefix <- 0x110c078 <- código em 0xe5bbca; availablevidmem <- 0x110dbcc; framelockinwindow <- 0x110dce4; nomemrestrict (sem o hífen) <- 0x110de40; commandline.txt <- 0x105b364. (Uma versão anterior deste achado dizia 'sem xrefs': era um erro de leitura do formato de resposta do xrefs, ver M-004.)
- **Evidência:** ghidra-queries (string-usage seguindo ponteiros em .data) — xrefs da string -> ponteiro em .data -> xrefs do ponteiro -> função; respostas brutas em runs/<execução>/ghidra-raw/
- **Recomendação:** Os ponteiros são usados por código em 0xe5bbca (notimefix), 0xe5c05a (availablevidmem), 0xe5c2ca (framelockinwindow) e 0xe5c48a (nomemrestrict): dentro do .text, mas fora de qualquer função definida pelo Ghidra. Inferência: são inicializadores estáticos que registram cada parâmetro (estilo sysParam do RAGE) no arranque; quem LÊ o valor de cada flag referencia o objeto de parâmetro, não esses trechos. Próximo passo: definir função nesses endereços (procedure_assembly/annotate) e seguir as xrefs do objeto.
- **Aprendido em:** 2026-10-21

## F-019 — Pool 'VehicleStruct' criado com 0x32 entradas de 0x20C bytes

- **Área:** jogo-binario · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** FUN_00a7b080 aloca 0x1c bytes e faz DAT_012fa84c = FUN_00c6c5f0(0x32, "VehicleStruct", 0x20c). FUN_00c6c5f0 é o construtor genérico de pools (CPool do RAGE): aloca contagem*tamanho bytes para os objetos e um byte de flags por entrada, e grava [2]=contagem, [3]=tamanho, [4]=-1 (primeira vaga livre). Logo: 50 entradas de 524 bytes.
- **Evidência:** ghidra-queries G-pool-vehicles + G-pool-ctor — pseudocódigo de FUN_00a7b080 e FUN_00c6c5f0
- **Recomendação:** O ponteiro do pool fica em DAT_012fa84c (estático, relativo à base). A capacidade é um imediato (0x32) na chamada: um patch de limite de pool (estilo 'pool limit adjuster') precisaria trocar esse imediato antes da inicialização. Qual entidade cada 'VehicleStruct' representa ainda é desconhecido; o pool de CVehicle em si é outro.
- **Aprendido em:** 2026-10-21

## F-020 — O jogo recusa converter um ped quando o pool de peds está cheio

- **Área:** jogo-binario · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** FUN_00b4e880 chama FUN_005d3400() e, se o resultado for < 1, monta 'Cannot convert %s into a ped : ped pool full' e não converte.
- **Evidência:** ghidra-queries G-pool-peds-full — pseudocódigo de FUN_00b4e880
- **Recomendação:** Ligado à limitação conhecida de peds a mais no Modo Solto: motoristas ejetados ocupam vagas do pool de peds. FUN_005d3400 (vagas livres) é candidata a ser lida pelo mod para decidir quando limpar com mais agressividade.
- **Aprendido em:** 2026-10-21

## F-021 — Tabela de pools do motor: o pool 'Vehicles' tem 140 vagas

- **Área:** jogo-binario · **Tipo:** observado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** Chamadas ao construtor de pools FUN_00c6c5f0(contagem, nome, tamanho): Vehicles 0x8c=140 x 0x2080 (DAT_012e22a4, em FUN_00a4a830); VehicleStruct 50 x 0x20c; PedAttractor 64 x 0xc0; Targetting 80 x 0x270; fragInstNMGta 120 x 0x2f0 (ragdolls NaturalMotion); fragInstGta 1004 x 0xb0; CQueriableTaskInfo 1120 x 0x50; phInstGta 4760 x 0x60; CAtdNodeFrameAddress 21000 x 0x10; dois pools com tamanho 0x40 e contagem por parâmetro (FUN_00c775c0, FUN_00c78500). O pool de peds não é criado por este construtor.
- **Evidência:** ghidra-queries G-pool-all + G-pool-ctor — procedure_callers/xrefs de 0x00c6c5f0 e linhas da chamada no pseudocódigo de cada chamador
- **Recomendação:** Os ponteiros dos pools ficam em endereços estáticos (ex.: Vehicles em DAT_012e22a4): um mod nativo pode ler ocupação e capacidade (campos [2]=contagem, flags por entrada) em vez de enumerar handles. Patches de capacidade trocariam o imediato da contagem antes da inicialização. Onde o pool de peds é criado ainda é desconhecido.
- **Aprendido em:** 2026-10-21

## F-022 — Nas sessões registradas o trânsito nunca encostou no limite do pool de veículos

- **Área:** mod-desempenho · **Tipo:** derivado · **Status:** aberto · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** O log do mod registrou no máximo 110 veículos existentes de uma vez (12 sessões com turbo; máximo no raio 94), contra 140 vagas do pool 'Vehicles' (F-021).
- **Evidência:** reverse/lib/mod-log.mjs + F-021 — max_total das varreduras do turbo por sessão: 110,90,97,86,104,100,95,22,92,27,85,110
- **Recomendação:** A partir do build 58081c84, a linha de limpeza registra mundo= (máximo de veículos existentes a cada 30 s). Jogar uma sessão com trânsito pesado e comparar esse número com as 140 vagas do pool confirma ou derruba a inferência.
- **Aprendido em:** 2026-10-21

## M-001 — O custo do wrapper-cost é um teto: soma todos os ramos

- **Área:** metodo · **Tipo:** derivado · **Status:** aprendido · **Artefato:** ScriptHookDotNet.dll (atual)
- **Afirmação:** Graphics.DrawText(string, r4, r4, Color, Font) dá 28 GetScreenResolution no teto, porque ConvertToPixel tem 8 ramos e DrawString aparece 5x (efeitos de sombra/contorno). Na prática cada chamada usa ~2. Agrupar sobrecargas pelo nome inflava ainda mais (v1 do modelo).
- **Evidência:** reverse/lib/wrapper-cost.mjs — árvore de chamadas do token 0x06000aed
- **Recomendação:** Usar o peso para ordenar candidatos, nunca como contagem real. Métodos com muitos ramos (desenho, spawn) aparecem inflados.
- **Aprendido em:** 2026-10-21

## M-002 — Ghidra no GTAIV.exe precisa de mais que o prazo padrão do REA

- **Área:** metodo · **Tipo:** observado · **Status:** aprendido · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** Importação + auto-análise padrão excedeu 330 s (provider_timeout). Ajustes usados: REA_GHIDRA_STARTUP_TIMEOUT_MS=7200000, GHIDRA_HEADLESS_MAXMEM=3G (máquina com 7,7 GB), REA_GHIDRA_DISABLED_ANALYZERS=WindowsResourceReference, e --snapshot para reaproveitar a análise.
- **Evidência:** rea inspect --provider ghidra — erro provider_timeout na 1ª tentativa
- **Recomendação:** Já embutido em reverse.config.json e no pipeline.
- **Aprendido em:** 2026-10-21

## M-003 — Cada comando do CLI do REA reanalisa o GTAIV.exe do zero

- **Área:** metodo · **Tipo:** observado · **Status:** aprendido · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** `rea search ... --snapshot` levou 7 min 06 s para uma única busca de string, o mesmo tempo da importação inicial (7 min 07 s). O snapshot (33 KB) guarda anotações e o perfil de análise, não a base do Ghidra, que é efêmera.
- **Evidência:** rea search --provider ghidra — tempo de parede medido nesta máquina (12 CPUs, 7,7 GB RAM, heap 3G) (ev_3a3d3a274341a623c4ca05dafe9ae76f13cf34e1c3628402885c4aac1ed606a2)
- **Recomendação:** Fazer todas as perguntas numa única sessão MCP (reverse/lib/ghidra-queries.mjs inicia um `rea mcp` e reaproveita a análise). Nunca encadear vários comandos do CLI no GTAIV.exe.
- **Aprendido em:** 2026-10-21

## M-004 — Resposta vazia não é ausência: conferir o payload bruto

- **Área:** metodo · **Tipo:** observado · **Status:** aprendido · **Artefato:** GTAIV.exe (atual)
- **Afirmação:** xrefs e procedure_callers do REA devolvem listas de endereços em texto; search_strings e search_bytes devolvem objetos com 'address'. O cliente procurava objetos em todos os casos e por isso relatou '0 referências' para todas as flags e '0 chamadores' do construtor de pools, que tem 11.
- **Evidência:** reverse/lib/ghidra-queries.mjs — payloads em runs/<execução>/ghidra-raw/ (gravados a partir desta correção)
- **Recomendação:** Antes de registrar 'não há referências', abrir o payload bruto da chamada. O cliente agora grava todas as respostas em ghidra-raw/.
- **Aprendido em:** 2026-10-21
