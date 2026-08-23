# ResetHandling - Código Fonte

Este diretório contém o código fonte C# do mod principal TurboTraffic-IV, desenvolvido com o ScriptHookDotNet (fork Tomasak, GTA IV). Ele implementa o caos no trânsito, o HUD do mod (Comandos / Spawn de Veículos / Configurações) e os efeitos extras (F5/F6).

## 📋 Descrição

O ResetHandling modifica o comportamento dos veículos controlados por NPCs no GTA IV aplicando turbo, e expõe um HUD próprio para o jogador controlar o mod sem precisar editar código ou recompilar.

### Funcionalidades Implementadas

1. **Caos no Trânsito** (`ApplyTurboLogic`, chamado a cada frame)
   - Aplica velocidade turbo aos veículos controlados por NPCs
   - **Começa desligado** (`ChaosEnabled` default `false`): só age depois que o jogador ligar pela aba **Configurações** do HUD
   - Raio de alcance e parâmetros de cada modo configuráveis pelo HUD (persistidos em `Documentos\TurboTraffic-IV\ResetHandling.ini`)
   - Nunca modifica o veículo do jogador, e ignora veículos estacionados (sem motorista)
   - Quatro modos independentes e combináveis, cada um num método próprio que desconhece os outros. A ordem no laço é a composição: `ApplyLegacyBoost` → `ApplyMaxSpeedMode` → `ApplyFreeRideMode` → `ApplyMagnetMode` (os três primeiros decidem *velocidade*; o Ímã, por último, decide *direção*)
   - O **Modo Solto** (`ChaosFreeRideModeEnabled`) usa `Task.ClearAllImmediately` para matar o trajeto da IA. Esse native aborta também a tarefa que segura o ped no banco, então o motorista acaba na rua e o veículo, sem motorista, sai do laço — por isso o modo é um empurrão de **largada**, não um efeito contínuo

2. **Limpeza de detritos** (`CleanupOffscreenDebris`, varredura a cada 750 ms)
   - Apaga sucata fora do campo de visão (`Vehicle.isOnScreen`) e a mais de 30 m, liberando vagas de tráfego para o jogo continuar spawnando veículos novos
   - `DeleteVehicleWithOccupants` apaga os ocupantes junto: `DELETE_CAR` remove só o veículo e **deposita quem estava dentro no chão**
   - `GetOccupants` enumera apenas os assentos que o modelo realmente tem (`Vehicle.PassengerSeats` + `isSeatFree`). `GetPedOnSeat` não valida o assento contra o modelo e devolve handle inválido para assentos inexistentes — o que causava `AccessViolationException` dentro do tick
   - Cada veículo da varredura roda em `try/catch` próprio, para um handle problemático não derrubar o tick inteiro (turbo e HUD junto)

   > ⚠️ **Limitação conhecida**: a limpeza pode contribuir para peds a mais nas ruas, mas **só em conjunto com o Modo Solto**, o único que ejeta motoristas. `CleanupLooseChaosPeds` remove os ejetados rastreados em `_freedDrivers` (fora da tela, > 30 m), porém a cobertura não é completa. Pendente de um acerto melhor.

3. **HUD do mod (F1)** — painel desenhado no canto superior esquerdo com três abas:
   - **Comandos**: lista as teclas do mod (F1/F5/F6) e o que cada uma faz, lida diretamente de `KeybindRegistry`
   - **Spawn de Veículos**: navega um catálogo de veículos por categoria e spawna o veículo selecionado à frente do jogador, dentro ou fora dele
   - **Configurações**: liga/desliga cada modo do caos, ajusta raio e parâmetros com incrementos fixos, e controla invencibilidade, deformação e a limpeza de detritos. A lista passou de 12 itens e não cabe mais na área de conteúdo, então rola com o item selecionado sempre visível (`HudTheme.MaxRows`), com o contador `n/total` no rodapé

4. **Dano e invencibilidade** (`ApplyDamageFlags`, passada própria a cada tick) — independe de qualquer modo de movimento estar ligado. Usa `MakeProofTo` (`SET_CAR_PROOFS`) além de `CanBeDamaged`, porque este último sozinho não blinda o veículo contra colisão, fogo e explosão

5. **Popup de tutorial** — na primeira vez que o personagem existe e está vivo, mostra uma dica (`Game.DisplayText`) ensinando a abrir o HUD com F1

6. **Elevação de Peds (F6)**: eleva todos os peds (incluindo o jogador) em 1 unidade, resetando a velocidade para evitar quedas violentas

7. **Explosão do Jogador (F5)**: cria uma explosão na posição atual do jogador e o mata, para consistência

Tanto F1/F5/F6 quanto a navegação do HUD usam **polling com detecção de borda** (`Game.isKeyPressed` comparado com o estado do frame anterior, em `ProcessInput`): a ação dispara uma vez por tecla pressionada, e segurar a tecla não repete. O evento `Script.KeyDown` existe na API mas não se mostrou confiável neste ambiente — o polling é o mecanismo comprovadamente entregue.

Enquanto o HUD está aberto, `Player.CanControlCharacter` é mantido `false` para que as setas naveguem o menu sem mover o personagem; ao fechar, o controle é restaurado.

## 📁 Estrutura dos Arquivos

```
ResetHandling/
├── ResetVehicleScript.cs     # Orquestrador: liga Tick/KeyDown/PerFrameDrawing, mantém o comportamento existente (turbo/F5/F6/popup)
├── Config/
│   ├── ModPaths.cs           # Resolve a pasta de dados gravável (Documentos\TurboTraffic-IV)
│   ├── ModSettings.cs        # Wrapper sobre GTA.SettingsFile — persiste ChaosEnabled/ChaosRadius/ChaosTurboPower no ResetHandling.ini
│   └── ModLog.cs             # Log de diagnóstico (TurboTrafficIV.log)
├── Keybinds/
│   └── KeybindRegistry.cs    # Lista única {tecla, rótulo, descrição} usada pelo roteamento de teclas e pela aba Comandos
├── Vehicles/
│   └── VehicleCatalog.cs     # Catálogo curado de veículos {categoria, nome exibido, nome interno do modelo}
├── Hud/
│   ├── IHudTab.cs            # Contrato comum das 3 abas (navegação, ajuste de valor, confirmar, desenhar)
│   ├── HudTheme.cs           # Métricas e cores compartilhadas (altura de linha, faixa de seleção, MaxRows)
│   ├── HudManager.cs         # Estado do HUD (aberto/aba ativa) e desenho do painel base
│   ├── CommandsTab.cs        # Aba "Comandos"
│   ├── VehicleSpawnTab.cs    # Aba "Spawn de Veículos"
│   └── SettingsTab.cs        # Aba "Configurações"
├── ResetHandling.csproj      # Projeto Visual Studio
├── ResetHandling.sln         # Solução Visual Studio
├── Properties/
│   └── AssemblyInfo.cs       # Informações do assembly
└── obj/                      # Arquivos temporários de build (gitignored)
```

## 🔧 Como Compilar

### Requisitos

- **Visual Studio 2017 ou superior** (com suporte a C#)
- **.NET Framework 4.0 Developer Pack** (Target Pack) instalado — necessário para compilar contra `TargetFrameworkVersion v4.0`
- Nenhuma configuração manual de caminho é necessária: a referência a `ScriptHookDotNet.dll` já aponta para `..\scripts\ScriptHookDotNet.dll`, que está commitado neste repositório.

### Passos para Compilar

1. **Abrir o Projeto**
   - Abra `ResetHandling.sln` no Visual Studio

2. **Configurar a Build**
   - Selecione a configuração **Release**
   - Selecione a plataforma **x86** (GTA IV é 32-bit)
   - A configuração `Release|x86` já compila direto para `..\scripts\` (ou seja, a pasta `scripts/` na raiz deste repositório) — não para dentro de uma instalação do GTA IV. Isso significa que compilar sempre atualiza os arquivos que o `Install-TurboTrafficIV.ps1` copia para o jogo.

3. **Compilar**
   - Pressione `F7` ou vá em **Build** → **Build Solution**
   - `ResetHandling.net.dll` e `ResetHandling.net.pdb` são gerados em `..\scripts\` (a pasta `scripts/` do repositório)

4. **Instalar**
   - Rode `Install-TurboTrafficIV.ps1` (na raiz do repositório) para copiar o resultado para a instalação do GTA IV. Ele sobrescreve automaticamente uma instalação anterior — não precisa apagar nada manualmente.

### Configurações Importantes

- **Target Framework**: .NET Framework 4.0
- **Platform Target**: x86 (GTA IV é 32-bit)
- **Output Type**: Library (gera DLL)
- **Assembly Name**: ResetHandling.net
- **OutputPath (Release|x86)**: `..\scripts\` — grava direto na pasta `scripts/` deste repositório

## 📝 Explicação do Código

### ResetVehicleScript.cs

A classe principal `NPCTurboBoost` herda de `Script` (ScriptHookDotNet). No construtor, ela carrega as configurações (`ModSettings.Load()`) e liga três eventos: `Tick`, `KeyDown` e `PerFrameDrawing`.

```csharp
public NPCTurboBoost()
{
    ModSettings.Load();

    Tick += OnTick;
    KeyDown += OnKeyDown;
    PerFrameDrawing += OnPerFrameDrawing;
    Interval = 0; //ms
}
```

- **`OnKeyDown`**: se o HUD está aberto, repassa a tecla para `HudManager.HandleKey`; senão, roteia F1 (abre o HUD), F5 (`ExplodePlayer`) e F6 (`ElevatePeds`).
- **`OnTick`**: mostra o popup de tutorial uma única vez; chama `ApplyTurboLogic()` se **qualquer** um dos quatro modos estiver ligado, e depois `ApplyDamageFlags()` e `CleanupOffscreenDebris()` — essas duas fora do `if`, de propósito, porque valem por si só.
- **`OnPerFrameDrawing`**: delega o desenho do HUD para `HudManager.Draw(e.Graphics)`.

#### ApplyTurboLogic()

```csharp
// Filtra apenas veículos com NPCs dentro
Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);
if (driver == null || !driver.Exists())
    continue; // Ignora carros estacionados

// Aplica turbo (raio e força vêm de ModSettings, ajustáveis pelo HUD)
float turboPower = ModSettings.ChaosTurboPower;
Vector3 boostedVel = speed + (forward * turboPower);
v.Velocity = boostedVel;
v.Speed = boostedVel.Length();
```

> **Não use `Function.Call("SET_CAR_VELOCITY", ...)` aqui.** Esse native **não existe** no ScriptHook do GTA IV 1.2.0.59 — versões anteriores do mod o chamavam e ele falhava a cada frame, enchendo o `ScriptHook.log` de `Native function named "SET_CAR_VELOCITY" is not available.` e deixando o turbo sem efeito. As propriedades `Vehicle.Velocity` e `Vehicle.Speed` do wrapper fazem o trabalho corretamente. Os natives de veículo que de fato existem incluem `SET_CAR_FORWARD_SPEED`, `SET_CAR_COORDINATES` e `SET_CAR_HEADING` (confirmável com um dump de strings do `ScriptHookDotNet.asi`).

**Características:**
- Raio de efeito: `ModSettings.ChaosRadius` (padrão 120, ajustável de 20 a 500)
- Potência do turbo: `ModSettings.ChaosTurboPower` (padrão 70, ajustável de 10 a 200)
- Ignora veículo do jogador e veículos estacionados (sem motorista)

#### ElevatePeds() / ExplodePlayer()

Sem mudanças de comportamento em relação à versão anterior — apenas passaram a ser chamados a partir do evento `KeyDown` em vez de polling em `Tick`.

### Config/ModSettings.cs

Wrapper estático sobre `GTA.SettingsFile` (API nativa do ScriptHookDotNet para arquivos `.ini` tipados). Expõe `ChaosEnabled`, `ChaosRadius` e `ChaosTurboPower` como propriedades somente-leitura por fora; qualquer ajuste passa por `ToggleChaosEnabled()`/`AdjustChaosRadius(delta)`/`AdjustChaosTurboPower(delta)`, que já fazem o clamp dentro do range permitido e persistem (`Save()`) imediatamente — nada se perde se o jogo crashar.

### Keybinds/KeybindRegistry.cs

Fonte única de verdade para as teclas do mod: tanto `ResetVehicleScript.OnKeyDown` quanto a aba **Comandos** do HUD leem esta lista, então a legenda nunca fica dessincronizada do comportamento real. Para adicionar uma tecla nova, adicione uma entrada aqui e trate-a em `OnKeyDown`.

### Vehicles/VehicleCatalog.cs

Lista estática de `VehicleEntry {Category, DisplayName, ModelName}` usada pela aba **Spawn de Veículos**. `ModelName` é o nome interno do modelo no GTA IV/EFLC (ex. `"INFERNUS"`); se algum item não spawnar em jogo, corrija o nome aqui.

### Hud/

- **`TextMeasure`**: mede a largura de um texto com `System.Drawing.Graphics.MeasureString` (fonte Arial na mesma família/tamanho/peso usado pelo HUD), sem depender do renderizador do jogo. É o que permite centralizar texto usando o overload simples `DrawText(texto, x, y, cor, fonte)`, em vez de `DrawText(string, RectangleF, TextAlignment, ...)`. A precisão foi conferida contra capturas de tela do jogo: para os rótulos das abas, o valor medido bate com a largura real renderizada dentro de ~1px (`"Spawn de Veículos"` → 193,3 medido vs ~194 na tela).
- **`HudTheme`**: métricas e cores compartilhadas — `RowHeight`, alturas de seção, cores de seleção/valor (todas com alpha 255: fundo translúcido deixa o contraste do texto refém da cor da cena atrás), e os helpers de desenho:
  - `FillRect(g, left, top, width, height, color)` — **use sempre este helper, nunca `g.DrawRectangle` direto.** A assinatura real da API é `DrawRectangle(Single CenterX, Single CenterY, Single Width, Single Height, Color)`: os dois primeiros argumentos são o **centro** do retângulo, não o canto superior esquerdo (mesmo contrato da native `DRAW_RECT` do GTA). `FillRect` recebe canto+tamanho, como o resto do layout assume, e converte para centro na chamada. Ver a nota sobre esse bug no fim desta seção.
  - `DrawCell(g, font, x, y, [height,] text, color)` — `DrawText(texto, x, y, cor, fonte)` simples, com o Y calculado para centralizar verticalmente dentro de uma faixa de altura, usando `Font.GetLineHeight(FontScaling.Pixel)` (altura de linha real da API) em vez de um deslocamento chutado.
  - `DrawCentered(g, font, x, y, width, height, fontSizePx, bold, text, color)` — centraliza nos dois eixos: a posição X vem de `TextMeasure.Width(...)`, a posição Y de `GetLineHeight`. É o que as abas do topo usam.
  - `DrawRow` / `MaxRows(contentHeight)` — faixa de destaque do item selecionado e quantas linhas cabem na área útil de uma aba.
- **`HudManager`**: dono do estado (`IsOpen`, aba ativa) e do desenho do painel base (fundo, barra de título, abas, linha de acento e rodapé de dicas). Painel de **720×600px**, fontes de **16 a 30px**. Duas rodadas anteriores ficaram pequenas demais em jogo (360×300/11–16px, depois 620×500/15–24px) antes deste tamanho. Delega conteúdo e navegação (`HandleKey`) para a aba ativa via `IHudTab`.
- **`CommandsTab` / `VehicleSpawnTab` / `SettingsTab`**: implementam `IHudTab` (`MoveSelection`, `AdjustValue`, `Confirm`, `Secondary`, `Draw`).
  - `CommandsTab` lista as teclas globais (de `KeybindRegistry`) **e** a navegação do próprio HUD. A navegação está escrita ali de propósito: com a troca de aba no `Tab`, o jogador não descobre essa tecla sozinho.
  - `VehicleSpawnTab` monta a lista como linhas de exibição (cabeçalho de categoria + veículos), mantém o item selecionado visível e **nunca desenha mais linhas do que cabem** — antes ela desenhava um número fixo de linhas e, somando os cabeçalhos de categoria, o conteúdo podia vazar para fora do painel.

### Geometria do painel

As constantes de `HudManager` derivam umas das outras (`TabsY` → `SeparatorY` → `ContentY` → `ContentBottom`), então mudar a altura do painel reposiciona tudo automaticamente. Com os valores atuais a área de conteúdo é de **676×432px**, o que dá **11 linhas visíveis** na lista de veículos e **46px de folga** na aba Comandos.

Ao mexer nessas constantes, ou ao adicionar teclas em `KeybindRegistry`, vale refazer a conta: a aba Comandos ocupa `SectionTitleHeight + (nº de teclas × RowHeight) + SectionGap + SectionTitleHeight + (6 × RowHeight)` e precisa caber em `ContentHeight`. Já houve um caso real de só 8px de folga (versão anterior, painel de 470px) — qualquer tecla nova teria estourado o painel.

Há um script de verificação (fora do repositório, no scratchpad da sessão que fez este ajuste) que reproduz essas constantes e usa `System.Drawing.Graphics.MeasureString` para medir a largura real de cada rótulo nas fontes usadas — inclusive o pior caso, a aba "Spawn de Veículos" centralizada numa caixa de 216px, que sobra só 11px de margem de cada lado. Vale recriar esse tipo de verificação (ou pedir para recriar) antes de mudar fontes ou larguras de coluna, em vez de confiar só no olho.

**Nota sobre o alinhamento fundo × texto (bug histórico, e como ele foi achado)**

Durante várias rodadas o painel apareceu em jogo com os retângulos de fundo fora de registro com o texto: o painel saía cortado pela borda esquerda da tela, e as faixas das abas ficavam deslocadas em relação aos rótulos. A causa é simples e vale registrar, porque é fácil reintroduzir:

`Graphics.DrawText(texto, x, y, ...)` posiciona pelo **canto superior esquerdo**, mas `Graphics.DrawRectangle(...)` posiciona pelo **centro** (`CenterX`/`CenterY` são os nomes reais dos parâmetros na API). Passando canto para os dois, todo retângulo é desenhado deslocado para cima e para a esquerda por **metade do próprio tamanho** — quanto maior o elemento, maior o erro. Daí o painel (720×600) sair 360px para a esquerda e 300px para cima, enquanto uma faixa de linha (34px de altura) parecia só "levemente" desalinhada.

Duas lições de método:

- O script de verificação de layout mede largura de texto e confere se as contas de geometria fecham, mas valida a geometria *pretendida* — ele não sabe como a API interpreta as coordenadas que recebe. Bug de contrato de API passa direto por ele.
- O que realmente resolveu foi **medir pixels nas capturas de tela do jogo e comparar com os números do código**. As bordas dos retângulos das abas na tela (~156→372) batiam exatamente com o intervalo previsto pela hipótese "centro" (`tabX=264, tabW=216` → 156→372) e com nenhuma outra. Depois disso, inspecionar a assinatura de `DrawRectangle` por reflection confirmou os nomes `CenterX`/`CenterY`. Quando o layout estiver estranho em jogo, esse é o caminho: medir a tela, não deduzir do código.

## 🎮 Controles

- **F1**: Abre/fecha o HUD do mod (Comandos / Spawn de Veículos / Configurações)
- **F5**: Explode o jogador na posição atual
- **F6**: Eleva todos os peds (incluindo o jogador) em 1 unidade
- **Caos no trânsito**: aplicado automaticamente aos veículos de NPCs (sem tecla; liga/desliga pela aba Configurações do HUD)

Navegação dentro do HUD: **Tab**/**Shift+Tab** trocam de aba, **↑/↓** navegam itens, **←/→** ajustam o valor selecionado, **Enter** confirma (spawna / liga-desliga o caos), **Espaço** é a ação secundária (na aba Spawn, alterna dentro/fora do veículo), **Esc** fecha.

## 🛠️ Modificações Possíveis

### Ajustar os valores padrão do caos no trânsito

Os defaults (usados apenas na primeira execução, antes de existir um `ResetHandling.ini`) e os ranges permitidos ficam em `Config/ModSettings.cs`:

```csharp
public const float ChaosRadiusMin = 20.0f;
public const float ChaosRadiusMax = 500.0f;
public const float ChaosRadiusStep = 10.0f;
```

Depois de instalado, o jeito recomendado de ajustar radius/turboPower é pela aba **Configurações** do HUD (F1) — os valores digitados lá são persistidos automaticamente.

### Adicionar um veículo ao catálogo de spawn

Adicione uma entrada em `Vehicles/VehicleCatalog.cs`:

```csharp
new VehicleEntry("Esportivos", "Nome Exibido", "NOME_INTERNO_DO_MODELO"),
```

### Adicionar uma nova tecla global

1. Registre a tecla em `Keybinds/KeybindRegistry.cs` (ela aparece automaticamente na aba Comandos):
   ```csharp
   new KeybindEntry(Keys.F7, "F7", "Descrição da nova ação"),
   ```
2. Trate a tecla em `OnKeyDown` (`ResetVehicleScript.cs`):
   ```csharp
   if (e.Key == Keys.F7)
   {
       // Sua nova funcionalidade aqui
   }
   ```

### Mudar teclas de atalho existentes

Troque `Keys.F5`/`Keys.F6` em `OnKeyDown` (`ResetVehicleScript.cs`) e atualize a entrada correspondente em `KeybindRegistry.cs` para a legenda continuar correta.

## ⚠️ Notas Importantes

- `ApplyTurboLogic` roda a cada frame (`Interval = 0`), o que pode afetar performance com muitos veículos — mas só executa se `ModSettings.ChaosEnabled` for `true`
- O filtro de veículos estacionados é importante para evitar bugs com carros parados
- O reset de velocidade em `ElevatePeds()` previne quedas violentas
- O script nunca modifica o veículo do jogador por segurança
- `System.Drawing` e `GTA` definem tipos com o mesmo nome (`Font`, `Graphics`) — os arquivos do HUD usam `using Font = GTA.Font;` / `using Graphics = GTA.Graphics;` para evitar ambiguidade. Se criar um novo arquivo no HUD que desenha na tela, replique esses `using`.
- Fora de uma classe que herda de `Script`, use `Game.LocalPlayer.Character` para obter o Ped do jogador (não `Player.Character`, que só resolve dentro de uma instância de `Script`, ex. `NPCTurboBoost`)

## 🐛 Troubleshooting de Compilação

### Erro: "os assemblies de referência para .NETFramework,Version=v4.0 não foram encontrados"

- Falta o **.NET Framework 4.0 Developer Pack** (Targeting Pack) no Visual Studio/máquina de build. Instale-o pelo Visual Studio Installer (componente individual) ou baixe em https://aka.ms/msbuild/developerpacks.

### Erro: "The type or namespace name 'GTA' could not be found"

- Certifique-se de que `scripts\ScriptHookDotNet.dll` existe na raiz do repositório (ele já vem commitado); se foi removido, restaure-o do controle de versão.

### O HUD não aparece ao apertar F1

Ao apertar F1, o mod mostra "TurboTraffic-IV: HUD aberto" usando o texto nativo do jogo (que não depende do hook de desenho). Se esse texto aparece mas o painel não, o problema é no desenho; se nem o texto aparece, a tecla não está chegando ao script.

Para mais detalhe, abra `Documentos\TurboTraffic-IV\TurboTrafficIV.log` — o mod registra o carregamento, a abertura/fechamento do HUD e o primeiro disparo do desenho por frame. Interpretando o log:

- Sem nenhuma linha: o `ResetHandling.net.dll` não foi carregado (confira a instalação e o ScriptHookDotNet)
- Tem "Script carregado" mas nenhum "Tecla detectada: F1" ao apertar F1: a tecla não está chegando ao script
- O log grava um `heartbeat` a cada 5s com `tick=ok`, `F1pressionado`, `hudAberto`, `desenhoDisparou` e `caos` — é a forma mais rápida de ver o estado real sem depender de nada em tela
- Tem "HUD aberto" mas nenhum "PerFrameDrawing disparou": o hook Direct3D não está ativo — o mod também avisa isso em tela com o texto nativo do jogo
- Tem "Primeiro desenho do HUD executado" mas nada visível: problema de posicionamento/escala do desenho

Detalhe de implementação relevante: `Graphics.Scaling` é definido explicitamente como `FontScaling.Pixel` em `HudManager.Draw` antes de qualquer desenho. Sem isso, as coordenadas podem ser interpretadas em unidades de tela (0..1) e o painel é desenhado inteiramente fora da área visível.

### O script não funciona no jogo

- Certifique-se de que `ResetHandling.net.dll` está na pasta `scripts` do jogo (rode `Install-TurboTrafficIV.ps1` depois de compilar)
- Verifique se o `ScriptHookDotNet.dll` também está na pasta `scripts`
- Certifique-se de que o `MyLoader.asi` está carregando o ScriptHookDotNet corretamente
- Verifique os logs do ScriptHookDotNet (se disponíveis)

### Performance ruim

- Considere aumentar o `Interval` no construtor:
  ```csharp
  Interval = 10; // Executa a cada 10ms em vez de cada frame
  ```
- Reduza `ModSettings.ChaosRadius` pela aba Configurações do HUD (não precisa recompilar)
- Adicione mais filtros para ignorar veículos desnecessários

## 📊 Otimizações Possíveis

### Cache de Veículos

Em vez de chamar `World.GetAllVehicles()` a cada frame, você pode cachear a lista:

```csharp
private Vehicle[] cachedVehicles;
private int cacheFrame = 0;

// No OnTick:
if (Game.GameTime - cacheFrame > 100) // Atualiza a cada 100ms
{
    cachedVehicles = World.GetAllVehicles();
    cacheFrame = Game.GameTime;
}
```

### Filtros Adicionais

Adicione mais filtros para melhorar performance:

```csharp
// Ignorar veículos muito longe antes de processar
if (Player.Character.Position.DistanceTo(v.Position) > radius)
    continue;

// Ignorar veículos parados
if (v.Speed < 0.1f)
    continue;
```

## 📄 Dependências

- **ScriptHookDotNet** (fork Tomasak): Framework para scripts .NET no GTA IV
  - Link: https://github.com/Tomasak/gta4_scripthookdotnet
- **.NET Framework 4.0**: Framework base necessário

## 📄 Licença

Este código é parte do projeto TurboTraffic-IV e é fornecido "como está" para uso pessoal e educacional.
