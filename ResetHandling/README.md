# ResetHandling - Código Fonte

Este diretório contém o código fonte C# do mod principal TurboTraffic-IV, desenvolvido com o ScriptHookDotNet (fork Tomasak, GTA IV). Ele implementa o caos no trânsito, o HUD do mod (Comandos / Spawn de Veículos / Configurações) e os efeitos extras (F5/F6).

## 📋 Descrição

O ResetHandling modifica o comportamento dos veículos controlados por NPCs no GTA IV aplicando turbo, e expõe um HUD próprio para o jogador controlar o mod sem precisar editar código ou recompilar.

### Funcionalidades Implementadas

1. **Caos no Trânsito** (`ApplyTurboLogic`, chamado a cada frame)
   - Aplica velocidade turbo aos veículos controlados por NPCs
   - Raio de alcance e força de aceleração configuráveis pelo HUD (persistidos em `scripts\ResetHandling.ini`)
   - Nunca modifica o veículo do jogador, e ignora veículos estacionados (sem motorista)
   - Pode ser ligado/desligado pela aba **Configurações** do HUD

2. **HUD do mod (F1)** — painel desenhado no canto superior esquerdo com três abas:
   - **Comandos**: lista as teclas do mod (F1/F5/F6) e o que cada uma faz, lida diretamente de `KeybindRegistry`
   - **Spawn de Veículos**: navega um catálogo de veículos por categoria e spawna o veículo selecionado à frente do jogador, dentro ou fora dele
   - **Configurações**: liga/desliga o caos no trânsito e ajusta raio/força com incrementos fixos

3. **Popup de tutorial** — na primeira vez que o personagem existe e está vivo, mostra uma dica (`Game.DisplayText`) ensinando a abrir o HUD com F1

4. **Elevação de Peds (F6)**: eleva todos os peds (incluindo o jogador) em 1 unidade, resetando a velocidade para evitar quedas violentas

5. **Explosão do Jogador (F5)**: cria uma explosão na posição atual do jogador e o mata, para consistência

Tanto F1/F5/F6 quanto a navegação do HUD respondem ao evento `Script.KeyDown` (disparo único por tecla pressionada), não a um polling em `Tick` — segurar a tecla não repete a ação.

## 📁 Estrutura dos Arquivos

```
ResetHandling/
├── ResetVehicleScript.cs     # Orquestrador: liga Tick/KeyDown/PerFrameDrawing, mantém o comportamento existente (turbo/F5/F6/popup)
├── Config/
│   └── ModSettings.cs        # Wrapper sobre GTA.SettingsFile — persiste ChaosEnabled/ChaosRadius/ChaosTurboPower em scripts\ResetHandling.ini
├── Keybinds/
│   └── KeybindRegistry.cs    # Lista única {tecla, rótulo, descrição} usada pelo roteamento de teclas e pela aba Comandos
├── Vehicles/
│   └── VehicleCatalog.cs     # Catálogo curado de veículos {categoria, nome exibido, nome interno do modelo}
├── Hud/
│   ├── IHudTab.cs            # Contrato comum das 3 abas (navegação, ajuste de valor, confirmar, desenhar)
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
- **`OnTick`**: mostra o popup de tutorial uma única vez, e chama `ApplyTurboLogic()` somente se `ModSettings.ChaosEnabled` estiver ativo.
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
Function.Call("SET_CAR_VELOCITY", v, boostedVel.X, boostedVel.Y, boostedVel.Z);
```

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

- **`HudManager`**: dono do estado (`IsOpen`, aba ativa) e do desenho do painel base (fundo, título, cabeçalho das abas, rodapé de dicas). Delega o conteúdo e a navegação (`HandleKey`) para a aba ativa via a interface `IHudTab`.
- **`CommandsTab` / `VehicleSpawnTab` / `SettingsTab`**: implementam `IHudTab` (`MoveSelection`, `AdjustValue`, `Confirm`, `Secondary`, `Draw`). Veja os arquivos individuais para a lógica de cada aba.

## 🎮 Controles

- **F1**: Abre/fecha o HUD do mod (Comandos / Spawn de Veículos / Configurações)
- **F5**: Explode o jogador na posição atual
- **F6**: Eleva todos os peds (incluindo o jogador) em 1 unidade
- **Caos no trânsito**: aplicado automaticamente aos veículos de NPCs (sem tecla; liga/desliga pela aba Configurações do HUD)

Navegação dentro do HUD: **Tab**/**Shift+Tab** troca de aba, **↑/↓** navega itens, **←/→** ajusta valores (aba Configurações), **Espaço** alterna spawn dentro/fora (aba Spawn), **Enter** confirma, **Esc** fecha.

## 🛠️ Modificações Possíveis

### Ajustar os valores padrão do caos no trânsito

Os defaults (usados apenas na primeira execução, antes de existir um `scripts\ResetHandling.ini`) e os ranges permitidos ficam em `Config/ModSettings.cs`:

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
