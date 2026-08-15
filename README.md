# TurboTraffic-IV

Mod para Grand Theft Auto IV que adiciona efeitos de turbo aos veículos de NPCs e funcionalidades extras de manipulação de física.

## 📋 Descrição

O TurboTraffic-IV é um mod que modifica o comportamento dos veículos controlados por NPCs no GTA IV, aplicando um efeito de turbo que aumenta significativamente a velocidade dos carros. O mod também inclui funcionalidades adicionais como elevação de peds e explosões controladas.

### Funcionalidades

- **Turbo em Veículos de NPCs**: Aplica velocidade turbo aos veículos controlados por NPCs dentro de um raio de 120 unidades do jogador
- **Elevação de Peds**: Eleva todos os peds (incluindo o jogador) em 1 unidade (ativado com F6)
- **Explosão do Jogador**: Cria uma explosão na posição do jogador (ativado com F5)

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
    ├── ResetVehicleScript.cs
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

- **F5**: Explode o jogador na posição atual
- **F6**: Eleva todos os peds (incluindo o jogador) em 1 unidade

O efeito de turbo nos veículos de NPCs é aplicado automaticamente quando você está próximo deles.

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

Para mais detalhes, consulte [INSTALL.md](INSTALL.md).

## 📄 Licença

Este projeto é fornecido "como está" para uso pessoal e educacional.

