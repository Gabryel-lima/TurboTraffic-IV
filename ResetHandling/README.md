# ResetHandling - Código Fonte

Este diretório contém o código fonte C# do mod principal TurboTraffic-IV, que implementa todas as funcionalidades de turbo em veículos de NPCs e efeitos extras.

## 📋 Descrição

O ResetHandling é um script .NET desenvolvido em C# usando o ScriptHookDotNet que modifica o comportamento dos veículos controlados por NPCs no GTA IV, aplicando efeitos de turbo e funcionalidades adicionais de manipulação de física.

### Funcionalidades Implementadas

1. **Turbo Automático em Veículos de NPCs**
   - Aplica velocidade turbo aos veículos controlados por NPCs
   - Apenas afeta veículos dentro de um raio de 120 unidades do jogador
   - Nunca modifica o veículo do jogador
   - Filtra veículos estacionados (sem motorista)

2. **Elevação de Peds (F6)**
   - Eleva todos os peds (incluindo o jogador) em 1 unidade
   - Aplica-se apenas a peds vivos dentro do raio
   - Reseta a velocidade para evitar quedas violentas

3. **Explosão do Jogador (F5)**
   - Cria uma explosão na posição atual do jogador
   - Mata o jogador para consistência com a explosão

## 📁 Estrutura dos Arquivos

```
ResetHandling/
├── ResetVehicleScript.cs    # Código fonte principal do mod
├── ResetHandling.csproj     # Projeto Visual Studio
├── ResetHandling.sln        # Solução Visual Studio
├── Properties/
│   └── AssemblyInfo.cs      # Informações do assembly
├── bin/                     # Arquivos compilados (gerados após build)
│   ├── x86/
│   │   ├── Debug/
│   │   │   └── ResetHandling.net.dll
│   │   └── Release/
│   │       └── ResetHandling.net.dll
└── obj/                     # Arquivos temporários de build
```

## 🔧 Como Compilar

### Requisitos

- **Visual Studio 2017 ou superior** (com suporte a C#)
- **.NET Framework 4.0** ou superior
- **ScriptHookDotNet.dll** (referência do projeto)
  - Deve estar localizado em: `[Diretório do GTA IV]\scripts\ScriptHookDotNet.dll`
  - Ou ajuste o caminho no arquivo `.csproj`

### Passos para Compilar

1. **Configurar a Referência do ScriptHookDotNet**
   - Abra `ResetHandling.csproj` em um editor de texto
   - Localize a seção `<Reference Include="ScriptHookDotNet">`
   - Ajuste o `HintPath` para apontar para o local correto do `ScriptHookDotNet.dll`
   - Exemplo:
     ```xml
     <HintPath>C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto IV\GTAIV\scripts\ScriptHookDotNet.dll</HintPath>
     ```

2. **Abrir o Projeto**
   - Abra `ResetHandling.sln` no Visual Studio

3. **Configurar a Build**
   - Selecione a configuração **Release**
   - Selecione a plataforma **x86** (GTA IV é 32-bit)
   - **Nota**: A configuração Release|x86 compila diretamente para a pasta `scripts` do jogo (se configurado)

4. **Compilar**
   - Pressione `F7` ou vá em **Build** → **Build Solution**
   - O arquivo `ResetHandling.net.dll` será gerado em `bin\x86\Release\`

5. **Copiar o Resultado**
   - Copie `bin\x86\Release\ResetHandling.net.dll` para a pasta `scripts` do GTA IV
   - Copie também `ResetHandling.net.pdb` se quiser informações de debug

### Configurações Importantes

- **Target Framework**: .NET Framework 4.0
- **Platform Target**: x86 (GTA IV é 32-bit)
- **Output Type**: Library (gera DLL)
- **Assembly Name**: ResetHandling.net

## 📝 Explicação do Código

### ResetVehicleScript.cs

A classe principal `NPCTurboBoost` herda de `Script` (ScriptHookDotNet) e implementa três métodos principais:

#### 1. ApplyTurboLogic()

Aplica velocidade turbo aos veículos de NPCs:

```csharp
// Filtra apenas veículos com NPCs dentro
Ped driver = v.GetPedOnSeat(VehicleSeat.Driver);
if (driver == null || !driver.Exists())
    continue; // Ignora carros estacionados

// Aplica turbo
float turboPower = 70.0f;
Vector3 boostedVel = speed + (forward * turboPower);
Function.Call("SET_CAR_VELOCITY", v, boostedVel.X, boostedVel.Y, boostedVel.Z);
```

**Características:**
- Raio de efeito: 120 unidades
- Potência do turbo: 70.0f (ajustável)
- Ignora veículo do jogador
- Ignora veículos estacionados (sem motorista)

#### 2. ElevatePeds()

Eleva todos os peds em 1 unidade:

```csharp
p.Position = new Vector3(pos.X, pos.Y, pos.Z + 1.0f);
p.Velocity = Vector3.Zero; // Evita queda violenta
```

**Características:**
- Raio de efeito: 120 unidades
- Ganho de altura: 1.0f (ajustável)
- Reseta velocidade para evitar quedas
- Ignora peds mortos

#### 3. ExplodePlayer()

Cria uma explosão na posição do jogador:

```csharp
World.AddExplosion(pos, ExplosionType.Default, 10.0f);
Player.Character.Die();
```

**Características:**
- Tipo: Explosão padrão
- Raio: 10.0f (ajustável)
- Mata o jogador automaticamente

### OnTick()

Método chamado a cada frame do jogo:

```csharp
private void OnTick(object sender, EventArgs e)
{
    if (Game.isKeyPressed(Keys.F5))
        ExplodePlayer();
    
    if (Game.isKeyPressed(Keys.F6))
        ElevatePeds();
    
    ApplyTurboLogic(); // Sempre ativo
}
```

**Interval**: 0ms (executa a cada frame para responsividade máxima)

## 🎮 Controles

- **F5**: Explode o jogador na posição atual
- **F6**: Eleva todos os peds (incluindo o jogador) em 1 unidade
- **Turbo**: Aplicado automaticamente aos veículos de NPCs (sem tecla)

## 🛠️ Modificações Possíveis

### Ajustar Potência do Turbo

Modifique a variável `turboPower` no método `ApplyTurboLogic()`:

```csharp
float turboPower = 70.0f; // Aumente ou diminua este valor
```

### Ajustar Raio de Efeito

Modifique a variável `radius` nos métodos:

```csharp
float radius = 120.0f; // Aumente para afetar veículos mais distantes
```

### Ajustar Ganho de Altura

Modifique o valor de elevação no método `ElevatePeds()`:

```csharp
pos.Z + 1.0f // Aumente para elevar mais, diminua para elevar menos
```

### Adicionar Novos Controles

Adicione novos controles no método `OnTick()`:

```csharp
if (Game.isKeyPressed(Keys.F7))
{
    // Sua nova funcionalidade aqui
}
```

### Mudar Teclas de Atalho

Modifique as teclas nos métodos `OnTick()`:

```csharp
if (Game.isKeyPressed(Keys.F5)) // Mude F5 para outra tecla
```

## ⚠️ Notas Importantes

- O script executa a cada frame (`Interval = 0`), o que pode afetar performance com muitos veículos
- O filtro de veículos estacionados é importante para evitar bugs com carros parados
- O reset de velocidade em `ElevatePeds()` previne quedas violentas
- O script nunca modifica o veículo do jogador por segurança

## 🐛 Troubleshooting de Compilação

### Erro: "Could not find ScriptHookDotNet.dll"

- Verifique se o `HintPath` no `.csproj` está correto
- Certifique-se de que o `ScriptHookDotNet.dll` existe no caminho especificado
- Você pode copiar o `ScriptHookDotNet.dll` para uma pasta local do projeto e ajustar a referência

### Erro: "The type or namespace name 'GTA' could not be found"

- Certifique-se de que a referência ao `ScriptHookDotNet.dll` está configurada corretamente
- Verifique se o arquivo DLL contém as classes necessárias

### O script não funciona no jogo

- Certifique-se de que `ResetHandling.net.dll` está na pasta `scripts` do jogo
- Verifique se o `ScriptHookDotNet.dll` também está na pasta `scripts`
- Certifique-se de que o `MyLoader.asi` está carregando o ScriptHookDotNet corretamente
- Verifique os logs do ScriptHookDotNet (se disponíveis)

### Performance ruim

- Considere aumentar o `Interval` no construtor:
  ```csharp
  Interval = 10; // Executa a cada 10ms em vez de cada frame
  ```
- Reduza o `radius` para afetar menos veículos
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

- **ScriptHookDotNet**: Framework para scripts .NET no GTA IV
  - Link: https://github.com/Tomasak/gta4_scripthookdotnet
- **.NET Framework 4.0**: Framework base necessário

## 📄 Licença

Este código é parte do projeto TurboTraffic-IV e é fornecido "como está" para uso pessoal e educacional.

