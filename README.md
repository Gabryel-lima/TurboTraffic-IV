# TurboTraffic-IV

Mod para Grand Theft Auto IV que adiciona efeitos de turbo aos veículos de NPCs, um HUD com spawn de veículos e configurações, e funcionalidades extras de manipulação de física.

## 📋 Descrição

O TurboTraffic-IV é um mod que modifica o comportamento dos veículos controlados por NPCs no GTA IV, aplicando um efeito de turbo (o "caos no trânsito") que aumenta significativamente a velocidade dos carros. Além disso, o mod tem um HUD próprio (aberto com **F1**) para consultar os comandos, spawnar qualquer veículo do jogo e ajustar o comportamento do mod em tempo real, sem precisar editar código.

### Funcionalidades

- **Caos no Trânsito**: Aplica velocidade turbo aos veículos controlados por NPCs dentro de um raio configurável ao redor do jogador. **Vem desligado por padrão** — ligue pela aba Configurações do HUD (a escolha fica salva entre sessões)
- **HUD do Mod (F1)**: painel no canto superior esquerdo da tela com 3 abas:
  - **Comandos**: legenda de todas as teclas do mod e o que cada uma faz
  - **Spawn de Veículos**: escolha entre dezenas de veículos organizados por categoria e spawne dentro ou fora do carro
  - **Configurações**: liga/desliga o caos no trânsito e ajusta o raio de alcance e a força de aceleração, com os valores salvos entre sessões
- **Popup de tutorial**: ao carregar o mundo, o jogo mostra uma dica (no estilo nativo do GTA) ensinando a abrir o HUD com F1
- **Elevação de Peds**: eleva todos os peds (incluindo o jogador) em 1 unidade (ativado com F6)
- **Explosão do Jogador**: cria uma explosão na posição do jogador (ativado com F5)

## 🎮 Requisitos

- Grand Theft Auto IV (Complete Edition ou versão compatível)
- Windows (o mod utiliza DLLs nativas do Windows)
- .NET Framework (para scripts .NET)

## 📁 Estrutura do Projeto

```
TurboTraffic-IV/
├── Main_GTAIV_Folder/     # Arquivos compilados para instalação
│   ├── dinput8.dll        # Ultimate ASI Loader (créditos: ThirteenAG)
│   ├── ScriptHook.dll     # ScriptHook base (créditos: Tomasak)
│   ├── ScriptHookDotNet.asi # ScriptHookDotNet (créditos: Tomasak)
│   ├── MyLoader.asi       # Loader customizado do mod
│   └── aCompleteEditionHook.asi
├── scripts/               # DLLs .NET para pasta scripts do jogo
│   ├── ResetHandling.net.dll
│   ├── ResetHandling.net.pdb
│   └── ScriptHookDotNet.dll
├── MyLoader/              # Código fonte C++ do loader customizado
│   ├── MyLoader.cpp
│   ├── MyLoader.h
│   └── ...
└── ResetHandling/         # Código fonte C# do mod principal
    ├── ResetVehicleScript.cs  # Orquestrador: liga teclas, tick e desenho do HUD
    ├── Config/                # Caminhos, log e persistência das configurações
    ├── Keybinds/               # Registro único das teclas do mod (usado pela aba Comandos)
    ├── Vehicles/                # Catálogo de veículos disponíveis para spawn
    ├── Hud/                     # HUD (painel + abas Comandos/Spawn/Configurações)
    └── ...
```

### Explicação das Pastas

- **Main_GTAIV_Folder/**: Contém todos os arquivos compilados que devem ser copiados diretamente para o diretório principal do GTA IV (onde está o executável `GTAIV.exe` ou `EFLC.exe`).

- **scripts/**: Contém as DLLs .NET compiladas que devem ser copiadas para uma pasta chamada `scripts` dentro do diretório principal do jogo.

- **MyLoader/**: Código fonte do loader customizado em C++. Este loader garante que o ScriptHookDotNet seja carregado de forma segura após o jogo inicializar completamente.

- **ResetHandling/**: Código fonte do mod principal em C#. Este é o script que implementa toda a lógica do turbo e funcionalidades extras.

## 🚀 Instalação

### Automática (recomendado)

Execute o script `Install-TurboTrafficIV.ps1` no PowerShell (Windows). Ele detecta automaticamente a instalação do GTA IV na Steam, Rockstar Games Launcher, GOG ou Epic Games e copia os arquivos para os lugares certos:

```powershell
.\Install-TurboTrafficIV.ps1
```

**Atualizando o mod**: depois de compilar uma nova versão (veja [ResetHandling/README.md](ResetHandling/README.md) e [MyLoader/README.md](MyLoader/README.md)), basta rodar o instalador de novo apontando para a mesma instalação do jogo. Os arquivos são sempre sobrescritos automaticamente, e qualquer arquivo de uma versão antiga do TurboTraffic-IV que não exista mais na versão atual é removido sozinho (com base num manifesto salvo na pasta do jogo) — **não é preciso apagar a instalação anterior manualmente**. Feche o GTA IV antes de instalar/atualizar, já que os arquivos ficam bloqueados enquanto o jogo está aberto.

Opções úteis:
- `-GamePath "D:\Caminho\Do\Jogo"` — pula a detecção automática
- `-Backup` — faz backup dos arquivos existentes antes de sobrescrever
- `-Uninstall` — remove o mod (`-UninstallMode Full` remove também as dependências compartilhadas)
- `-Force` — não pede confirmação

Se o PowerShell bloquear a execução de scripts, rode uma vez: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`.

### Manual

Para instruções detalhadas de instalação manual, consulte o arquivo [INSTALL.md](INSTALL.md).

**Resumo rápido:**
1. Copie todos os arquivos de `Main_GTAIV_Folder/` para o diretório principal do GTA IV
2. Crie uma pasta `scripts` no diretório principal do jogo (se não existir)
3. Copie todo o conteúdo de `scripts/` para a pasta `scripts` do jogo

## 🎯 Controles

- **F1**: Abre/fecha o HUD do TurboTraffic-IV (Comandos, Spawn de Veículos, Configurações)
- **F5**: Explode o jogador na posição atual
- **F6**: Eleva todos os peds (incluindo o jogador) em 1 unidade

O caos no trânsito (turbo nos veículos de NPCs) começa **desligado**: abra o HUD com F1, vá até a aba **Configurações** e ligue. A partir daí ele é aplicado automaticamente aos veículos próximos, e a escolha fica salva para as próximas sessões.

Enquanto o HUD está aberto, o controle do personagem fica travado, para que as setas naveguem o menu sem também mover o jogador. Ao fechar o HUD (Esc ou F1), o controle volta ao normal.

### Navegando no HUD (F1)

- **Tab** / **Shift+Tab**: troca de aba (Comandos → Spawn de Veículos → Configurações)
- **↑ / ↓**: navega os itens da aba atual
- **← / →**: ajusta o valor do item selecionado (aba Configurações)
- **Enter**: confirma — spawna o veículo selecionado, ou liga/desliga o caos no trânsito
- **Espaço**: ação secundária — na aba Spawn, alterna se o veículo nasce com você dentro ou fora dele
- **Esc**: fecha o HUD

Essas teclas ficam listadas dentro do próprio HUD, na aba **Comandos**, e o rodapé do painel repete o essencial: a troca de aba pelo **Tab** não é descoberta sozinha se não estiver escrita na tela.

As configurações ajustadas na aba **Configurações** ficam salvas em `Documentos\TurboTraffic-IV\ResetHandling.ini` e persistem entre sessões. Elas ficam aí, e não na pasta do jogo, porque instalações em `C:\Program Files (x86)\...` só aceitam escrita com elevação — gravar lá falharia em silêncio.

## 📝 Créditos

### Dependências Externas

- **Ultimate ASI Loader** por [ThirteenAG](https://github.com/ThirteenAG/Ultimate-ASI-Loader)
  - Arquivo: `dinput8.dll`
  - Link: https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases

- **ScriptHookDotNet** por [Tomasak](https://github.com/Tomasak/gta4_scripthookdotnet)
  - Arquivos: `ScriptHook.dll` e `ScriptHookDotNet.asi`
  - Link: https://github.com/Tomasak/gta4_scripthookdotnet/releases/tag/release

### Desenvolvimento

- **MyLoader**: Loader customizado desenvolvido para garantir carregamento seguro do ScriptHookDotNet
- **ResetHandling**: Script principal do mod desenvolvido em C#

## ⚠️ Avisos

- Faça backup dos arquivos originais do jogo antes de instalar qualquer mod
- Este mod modifica o comportamento do jogo e pode afetar o desempenho
- Use por sua conta e risco

## 🐛 Troubleshooting

Se o mod não funcionar:

1. Verifique se todos os arquivos foram copiados corretamente
2. Certifique-se de que a pasta `scripts` existe no diretório do jogo
3. Verifique se você tem as permissões necessárias para modificar arquivos do jogo
4. Certifique-se de estar usando uma versão compatível do GTA IV
5. Se o instalador reclamar de arquivos ausentes, compile `ResetHandling.sln` (Release|x86) e `MyLoader.sln` (Release|Win32) antes de instalar
6. Se o instalador travar dizendo que o jogo está em execução, feche o GTA IV e rode o instalador de novo
7. Se a detecção automática não encontrar sua instalação (ex.: aparece "Múltiplas instalações" sem nenhuma listada), use `-GamePath` apontando para a pasta onde está o `GTAIV.exe`/`EFLC.exe` — o instalador também aceita a pasta um nível acima (ex. a pasta "Grand Theft Auto IV" da Complete Edition da Steam, que contém uma subpasta `GTAIV\`)
8. Se o HUD não aparecer ao apertar F1: o mod mostra "TurboTraffic-IV: HUD aberto" com o texto nativo do jogo ao receber a tecla. Se esse texto aparece mas o painel não, o problema é no desenho; se nem o texto aparece, a tecla não chegou. Detalhes em `Documentos\TurboTraffic-IV\TurboTrafficIV.log`

Para mais detalhes, consulte [INSTALL.md](INSTALL.md).

## 📄 Licença

Este projeto é fornecido "como está" para uso pessoal e educacional.

