# Guia de Instalação - TurboTraffic-IV

Este guia irá te ajudar a instalar o mod TurboTraffic-IV no Grand Theft Auto IV passo a passo.

> **Prefira o instalador automático.** O script `Install-TurboTrafficIV.ps1` (na raiz do repositório) faz tudo isso por você, detecta o jogo automaticamente e — diferente da cópia manual abaixo — sabe atualizar uma instalação existente sozinho: sobrescreve os arquivos e remove qualquer arquivo de versão antiga do mod que não exista mais, sem você precisar apagar nada manualmente. Veja o [README.md](README.md#-instalação). O passo a passo manual abaixo é útil se você não puder rodar PowerShell ou quiser entender exatamente o que cada arquivo faz.

## 📍 Passo 1: Localizar o Diretório do GTA IV

Primeiro, você precisa encontrar onde o GTA IV está instalado no seu computador.

### Localizações Comuns:

- **Steam**: `C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto IV\`
- **Epic Games**: `C:\Program Files\Epic Games\GTAIV\`
- **Rockstar Games Launcher**: `C:\Program Files\Rockstar Games\Grand Theft Auto IV\`
- **Instalação Manual**: Onde você instalou o jogo manualmente

O diretório principal do jogo deve conter o arquivo executável:
- `GTAIV.exe` (versão original)
- `EFLC.exe` (Episodes from Liberty City)
- Ou ambos

## 📦 Passo 2: Copiar Arquivos de Main_GTAIV_Folder

Agora você precisa copiar todos os arquivos da pasta `Main_GTAIV_Folder/` para o diretório principal do GTA IV (o mesmo local onde está o `GTAIV.exe`).

### Arquivos a Copiar:

1. **dinput8.dll**
   - **Créditos**: [Ultimate ASI Loader](https://github.com/ThirteenAG/Ultimate-ASI-Loader) por ThirteenAG
   - **Função**: Carrega arquivos ASI no jogo
   - **Link**: https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases

2. **ScriptHook.dll**
   - **Créditos**: [ScriptHookDotNet](https://github.com/Tomasak/gta4_scripthookdotnet) por Tomasak
   - **Função**: Hook base necessário para scripts
   - **Link**: https://github.com/Tomasak/gta4_scripthookdotnet/releases/tag/release

3. **ScriptHookDotNet.asi**
   - **Créditos**: [ScriptHookDotNet](https://github.com/Tomasak/gta4_scripthookdotnet) por Tomasak
   - **Função**: Permite executar scripts .NET no jogo
   - **Link**: https://github.com/Tomasak/gta4_scripthookdotnet/releases/tag/release

4. **MyLoader.asi**
   - **Função**: Loader customizado que garante o carregamento seguro do ScriptHookDotNet após o jogo inicializar completamente (evita crashes)

5. **MyLoader.pdb**
   - **Função**: Arquivo de símbolos de debug (opcional, mas recomendado)

6. **aCompleteEditionHook.asi**
   - **Função**: Hook adicional para compatibilidade com a Complete Edition

### Como Copiar:

1. Abra a pasta `Main_GTAIV_Folder/` do projeto
2. Selecione **todos os arquivos** dentro desta pasta
3. Copie os arquivos (Ctrl+C)
4. Navegue até o diretório principal do GTA IV
5. Cole os arquivos (Ctrl+V)
6. Se o Windows perguntar sobre substituir arquivos existentes, escolha **Substituir**

**Estrutura esperada após a cópia:**
```
[Diretório do GTA IV]/
├── GTAIV.exe (ou EFLC.exe)
├── dinput8.dll          ← NOVO
├── ScriptHook.dll       ← NOVO
├── ScriptHookDotNet.asi ← NOVO
├── MyLoader.asi         ← NOVO
├── MyLoader.pdb         ← NOVO
├── aCompleteEditionHook.asi ← NOVO
└── [outros arquivos do jogo]
```

## 📁 Passo 3: Criar Pasta scripts

No diretório principal do GTA IV, você precisa criar uma pasta chamada `scripts` (se ela não existir).

1. Navegue até o diretório principal do GTA IV
2. Clique com o botão direito em um espaço vazio
3. Selecione **Novo** → **Pasta**
4. Nomeie a pasta como `scripts` (em minúsculas, sem espaços)

**Nota**: Se a pasta `scripts` já existir (de outros mods), você pode usar a mesma pasta.

## 📋 Passo 4: Copiar Conteúdo de scripts/

Agora você precisa copiar todo o conteúdo da pasta `scripts/` do projeto para a pasta `scripts` que você criou (ou que já existe) no diretório do jogo.

### Arquivos a Copiar:

1. **ResetHandling.net.dll**
   - **Função**: DLL principal do mod com toda a lógica de turbo e funcionalidades extras

2. **ResetHandling.net.pdb**
   - **Função**: Arquivo de símbolos de debug (opcional, mas recomendado)

3. **ScriptHookDotNet.dll**
   - **Função**: DLL do ScriptHookDotNet necessária para executar scripts .NET

### Como Copiar:

1. Abra a pasta `scripts/` do projeto
2. Selecione **todos os arquivos** dentro desta pasta
3. Copie os arquivos (Ctrl+C)
4. Navegue até a pasta `scripts/` que você criou no diretório do GTA IV
5. Cole os arquivos (Ctrl+V)
6. Se o Windows perguntar sobre substituir arquivos existentes, escolha **Substituir**

**Estrutura esperada após a cópia:**
```
[Diretório do GTA IV]/
├── GTAIV.exe (ou EFLC.exe)
├── dinput8.dll
├── ScriptHook.dll
├── ScriptHookDotNet.asi
├── MyLoader.asi
├── MyLoader.pdb
├── aCompleteEditionHook.asi
├── scripts/              ← PASTA CRIADA
│   ├── ResetHandling.net.dll    ← NOVO
│   ├── ResetHandling.net.pdb    ← NOVO
│   └── ScriptHookDotNet.dll     ← NOVO
└── [outros arquivos do jogo]
```

## ✅ Passo 5: Verificar Instalação

Após copiar todos os arquivos, verifique se a instalação está correta:

### Checklist:

- [ ] Todos os arquivos de `Main_GTAIV_Folder/` estão no diretório principal do jogo
- [ ] A pasta `scripts/` existe no diretório principal do jogo
- [ ] Todos os arquivos de `scripts/` estão dentro da pasta `scripts/` do jogo
- [ ] Você tem permissões de leitura/escrita no diretório do jogo

### Testar o Mod:

1. Inicie o GTA IV normalmente
2. Se o mod carregar corretamente, você verá uma mensagem: "ScriptHookDotNet carregado com sucesso!"
3. Assim que você puder controlar o personagem, deve aparecer uma dica na tela ensinando a abrir o HUD do mod com **F1**
4. Pressione **F1** para abrir o HUD e navegue pelas abas Comandos, Spawn de Veículos e Configurações (veja os controles no [README.md](README.md#-controles))
5. Entre no jogo e dirija próximo a veículos de NPCs - eles devem acelerar automaticamente (pode ser desligado na aba Configurações do HUD)
6. Pressione **F5** para testar a explosão do jogador
7. Pressione **F6** para testar a elevação de peds

## 🐛 Troubleshooting

### O mod não funciona

1. **Verifique os arquivos**: Certifique-se de que todos os arquivos foram copiados corretamente
2. **Verifique a pasta scripts**: A pasta deve se chamar exatamente `scripts` (minúsculas, sem espaços)
3. **Permissões**: Execute o jogo como administrador se necessário
4. **Antivírus**: Alguns antivírus podem bloquear DLLs. Adicione o diretório do jogo às exceções
5. **Versão do jogo**: Certifique-se de estar usando uma versão compatível do GTA IV

### O jogo não inicia

1. **Backup**: Se você fez backup, restaure os arquivos originais
2. **Verifique dinput8.dll**: Este arquivo é essencial. Certifique-se de que está presente
3. **Verifique ScriptHook**: Certifique-se de que `ScriptHook.dll` e `ScriptHookDotNet.asi` estão presentes
4. **Logs**: Verifique se há arquivos de log no diretório do jogo que possam indicar o problema

### Mensagem de erro ao carregar

- Se aparecer "ScriptHookDotNet não encontrado ou falhou ao carregar", verifique se `ScriptHookDotNet.asi` está no diretório principal do jogo
- Certifique-se de que a pasta `scripts/` contém `ScriptHookDotNet.dll`

### Performance

- O mod pode afetar o desempenho do jogo, especialmente com muitos veículos na tela
- Se o jogo estiver muito lento, considere reduzir a distância de efeito no código fonte

## ⬆️ Atualizando manualmente

Se você atualizou o código e recompilou o mod (veja [ResetHandling/README.md](ResetHandling/README.md)), repita os Passos 2 e 4 acima e escolha **Substituir** quando o Windows perguntar — os arquivos antigos são sobrescritos. Se uma atualização remover algum arquivo que existia em versões antigas (por exemplo, um `.dll` que não é mais usado), você precisa apagá-lo manualmente, já que a cópia manual não faz essa limpeza sozinha (diferente do instalador automático `.ps1`, que remove arquivos obsoletos de versões antigas automaticamente).

## 🔄 Desinstalação

Para remover o mod:

1. Delete os seguintes arquivos do diretório principal do jogo:
   - `dinput8.dll`
   - `ScriptHook.dll`
   - `ScriptHookDotNet.asi`
   - `MyLoader.asi`
   - `MyLoader.pdb`
   - `aCompleteEditionHook.asi`
   - `TurboTrafficIV_manifest.json` (criado apenas se você usou o instalador automático)

2. Delete a pasta `scripts/` do diretório do jogo (ou apenas os arquivos do mod se você tiver outros mods instalados)

**Nota**: Se você tiver outros mods que usam ScriptHookDotNet, você pode querer manter alguns desses arquivos. Nesse caso, remova apenas:
- `MyLoader.asi`
- `MyLoader.pdb`
- `ResetHandling.net.dll`
- `ResetHandling.net.pdb`
- `Documentos\TurboTraffic-IV\` (pasta com o arquivo de configuração do HUD e o log de diagnóstico, criada pelo mod na primeira execução — fica fora da pasta do jogo)
- `TurboTrafficIV_manifest.json` (se existir)

## 📞 Suporte

Se você encontrar problemas que não estão listados aqui, verifique:
- A versão do GTA IV que você está usando
- Se outros mods estão instalados e podem estar causando conflitos
- Os logs do jogo (se disponíveis)

## 📝 Notas Finais

- Sempre faça backup dos arquivos originais antes de instalar mods
- Este mod modifica o comportamento do jogo e pode afetar saves
- Use por sua conta e risco

