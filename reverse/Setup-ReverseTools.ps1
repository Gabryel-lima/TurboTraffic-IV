<#
.SYNOPSIS
    Prepara o ambiente de engenharia reversa do TurboTraffic-IV (REA + Ghidra + JDK).

.DESCRIPTION
    Reproduz em qualquer máquina Windows x64 o ambiente usado para analisar o
    GTA IV e o mod. As versões vêm de reverse.config.json, então duas máquinas
    com o mesmo repositório analisam com exatamente as mesmas ferramentas.

    O script é idempotente: o que já está instalado e na versão certa é
    mantido. Antes de mudar qualquer coisa ele mostra o plano e pede
    confirmação (a menos que -Yes seja usado).

    O que ele pode fazer:
      1. Instalar o CLI do REA (npm install --global rea-agents@<versão>)
      2. Instalar o JDK 21 via winget, se não houver um JDK >= 21
      3. Baixar o Ghidra oficial, conferir o SHA-256 e extrair em -ToolsDir
      4. Gravar reverse\.local\env.json com os caminhos resolvidos (não versionado)
      5. Registrar o servidor MCP do REA no agente (rea setup, com prévia)

    O que ele NÃO faz: instalar Node.js, alterar variáveis de ambiente globais
    do Windows ou mexer na pasta do jogo.

.PARAMETER ToolsDir
    Onde o Ghidra é extraído. Padrão: toolsDir de reverse.config.json.

.PARAMETER Check
    Só verifica e mostra o estado, sem mudar nada.

.PARAMETER SkipAgentRegistration
    Não registra o REA no agente de IA (útil em máquina só de análise via CLI).

.PARAMETER Yes
    Não pede confirmação.

.EXAMPLE
    .\reverse\Setup-ReverseTools.ps1 -Check

.EXAMPLE
    .\reverse\Setup-ReverseTools.ps1
#>

[CmdletBinding()]
param(
    [string]$ToolsDir,
    [switch]$Check,
    [switch]$SkipAgentRegistration,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$ReverseRoot = $PSScriptRoot
$Config = Get-Content (Join-Path $ReverseRoot 'reverse.config.json') -Raw | ConvertFrom-Json
if (-not $ToolsDir) { $ToolsDir = $Config.toolsDir }

$LocalDir = Join-Path $ReverseRoot '.local'
$RunsDir = Join-Path $ReverseRoot 'runs'
New-Item -ItemType Directory -Force -Path $LocalDir, $RunsDir | Out-Null
# Um único log, reescrito a cada execução do setup.
$LogFile = Join-Path $RunsDir 'setup.log'
Set-Content -Path $LogFile -Encoding UTF8 -Value ("Setup-ReverseTools - {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))

function Write-Log {
    param([string]$Message, [ValidateSet('INFO', 'OK', 'WARN', 'ERROR', 'STEP')][string]$Level = 'INFO')
    $line = "[{0}][{1}] {2}" -f (Get-Date -Format 'HH:mm:ss'), $Level, $Message
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
    switch ($Level) {
        'OK'    { Write-Host $line -ForegroundColor Green }
        'WARN'  { Write-Host $line -ForegroundColor Yellow }
        'ERROR' { Write-Host $line -ForegroundColor Red }
        'STEP'  { Write-Host $line -ForegroundColor Cyan }
        default { Write-Host $line }
    }
}

function Confirm-Step {
    param([string]$Question)
    if ($Yes) { return $true }
    $answer = Read-Host "$Question [s/N]"
    return $answer -match '^(s|sim|y|yes)$'
}

function Get-VersionOrNull {
    param([string]$Text)
    $m = [regex]::Match([string]$Text, '\d+\.\d+\.\d+')
    if ($m.Success) { return [version]$m.Value }
    return $null
}

# ---------------------------------------------------------------------------
# Inspeção do estado atual
# ---------------------------------------------------------------------------

function Get-NodeState {
    $cmd = Get-Command node -ErrorAction SilentlyContinue
    if (-not $cmd) { return [PSCustomObject]@{ Ok = $false; Version = $null } }
    $v = Get-VersionOrNull (& node --version)
    return [PSCustomObject]@{ Ok = ($v -ge [version]$Config.node.minimum); Version = $v }
}

function Get-ReaState {
    $cmd = Get-Command rea -ErrorAction SilentlyContinue
    if (-not $cmd) { return [PSCustomObject]@{ Ok = $false; Version = $null } }
    $v = Get-VersionOrNull ((& rea --version) 2>$null | Select-Object -First 1)
    return [PSCustomObject]@{ Ok = ($v -eq [version]$Config.rea.version); Version = $v }
}

function Find-Jdk {
    # JAVA_HOME já válido tem prioridade; depois as pastas de instalação comuns.
    $candidates = @()
    if ($env:JAVA_HOME) { $candidates += $env:JAVA_HOME }
    foreach ($root in $Config.jdk.searchRoots) {
        if (Test-Path $root) {
            $candidates += Get-ChildItem -Path $root -Directory -Filter 'jdk-*' -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending | ForEach-Object { $_.FullName }
        }
    }
    foreach ($jdkHome in $candidates) {
        $javac = Join-Path $jdkHome 'bin\javac.exe'
        $release = Join-Path $jdkHome 'release'
        if (-not (Test-Path $javac) -or -not (Test-Path $release)) { continue }
        $m = Select-String -Path $release -Pattern 'JAVA_VERSION="(\d+)' | Select-Object -First 1
        if ($m -and [int]$m.Matches[0].Groups[1].Value -ge [int]$Config.jdk.minimumMajor) { return $jdkHome }
    }
    return $null
}

function Get-GhidraState {
    $dir = Join-Path $ToolsDir $Config.ghidra.folderName
    $props = Join-Path $dir 'Ghidra\application.properties'
    if (-not (Test-Path $props)) { return [PSCustomObject]@{ Ok = $false; Dir = $dir; Version = $null } }
    $line = Select-String -Path $props -Pattern '^application\.version=(.+)$' | Select-Object -First 1
    $version = if ($line) { $line.Matches[0].Groups[1].Value.Trim() } else { $null }
    return [PSCustomObject]@{ Ok = ($version -eq $Config.ghidra.version); Dir = $dir; Version = $version }
}

Write-Log "Log desta preparação: $LogFile" 'INFO'
Write-Log 'Verificando o ambiente...' 'STEP'

$node = Get-NodeState
$rea = Get-ReaState
$jdk = Find-Jdk
$ghidra = Get-GhidraState

Write-Log ("Node.js : {0}" -f $(if ($node.Version) { "$($node.Version) $(if ($node.Ok) {'(ok)'} else {"(precisa >= $($Config.node.minimum))"})" } else { 'não encontrado' })) $(if ($node.Ok) { 'OK' } else { 'WARN' })
Write-Log ("REA     : {0}" -f $(if ($rea.Version) { "$($rea.Version) $(if ($rea.Ok) {'(ok)'} else {"(fixado em $($Config.rea.version))"})" } else { 'não instalado' })) $(if ($rea.Ok) { 'OK' } else { 'WARN' })
Write-Log ("JDK     : {0}" -f $(if ($jdk) { $jdk } else { "nenhum JDK >= $($Config.jdk.minimumMajor)" })) $(if ($jdk) { 'OK' } else { 'WARN' })
Write-Log ("Ghidra  : {0}" -f $(if ($ghidra.Version) { "$($ghidra.Version) em $($ghidra.Dir)" } else { "ausente (esperado $($Config.ghidra.version) em $($ghidra.Dir))" })) $(if ($ghidra.Ok) { 'OK' } else { 'WARN' })

if (-not $node.Ok) {
    Write-Log "Node.js >= $($Config.node.minimum) é pré-requisito e não é instalado por este script. Instale com: winget install OpenJS.NodeJS.LTS" 'ERROR'
    exit 1
}

# ---------------------------------------------------------------------------
# Plano
# ---------------------------------------------------------------------------

$plan = @()
if (-not $rea.Ok) { $plan += "Instalar rea-agents@$($Config.rea.version) globalmente via npm" }
if (-not $jdk) { $plan += "Instalar o JDK via winget ($($Config.jdk.wingetId))" }
if (-not $ghidra.Ok) { $plan += "Baixar Ghidra $($Config.ghidra.version) (SHA-256 conferido) e extrair em $ToolsDir" }
$plan += "Gravar caminhos resolvidos em $(Join-Path $LocalDir 'env.json')"
if (-not $SkipAgentRegistration) { $plan += "Registrar o MCP do REA no agente '$($Config.rea.agentClient)' (prévia do rea setup antes de aplicar)" }

if ($Check) {
    Write-Log 'Modo -Check: nada foi alterado. Plano que seria executado:' 'STEP'
    $plan | ForEach-Object { Write-Log "  - $_" }
    exit 0
}

Write-Log 'Plano:' 'STEP'
$plan | ForEach-Object { Write-Log "  - $_" }
if (-not (Confirm-Step 'Executar o plano?')) { Write-Log 'Cancelado pelo usuário.' 'WARN'; exit 1 }

# ---------------------------------------------------------------------------
# Execução
# ---------------------------------------------------------------------------

if (-not $rea.Ok) {
    Write-Log "Instalando rea-agents@$($Config.rea.version)..." 'STEP'
    & npm install --global "$($Config.rea.package)@$($Config.rea.version)"
    if ($LASTEXITCODE -ne 0) { Write-Log 'npm install falhou.' 'ERROR'; exit 1 }
    Write-Log 'REA instalado.' 'OK'
}

if (-not $jdk) {
    Write-Log "Instalando $($Config.jdk.wingetId) via winget..." 'STEP'
    & winget install --id $Config.jdk.wingetId --exact --silent --accept-package-agreements --accept-source-agreements
    $jdk = Find-Jdk
    if (-not $jdk) { Write-Log 'JDK não encontrado após a instalação. Defina JAVA_HOME e rode de novo.' 'ERROR'; exit 1 }
    Write-Log "JDK em $jdk" 'OK'
}

if (-not $ghidra.Ok) {
    New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
    $zip = Join-Path $ToolsDir ([System.IO.Path]::GetFileName($Config.ghidra.url))
    if (-not (Test-Path $zip)) {
        Write-Log "Baixando $($Config.ghidra.url) ..." 'STEP'
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $Config.ghidra.url -OutFile $zip -UseBasicParsing
    }
    $hash = (Get-FileHash -Path $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $Config.ghidra.sha256) {
        Write-Log "SHA-256 não confere ($hash). O arquivo foi mantido para inspeção: $zip" 'ERROR'
        exit 1
    }
    Write-Log 'SHA-256 do Ghidra confere com o release oficial.' 'OK'
    Expand-Archive -Path $zip -DestinationPath $ToolsDir -Force
    Remove-Item -LiteralPath $zip -Force
    $ghidra = Get-GhidraState
    if (-not $ghidra.Ok) { Write-Log 'Ghidra extraído, mas a versão não confere.' 'ERROR'; exit 1 }
    Write-Log "Ghidra $($ghidra.Version) em $($ghidra.Dir)" 'OK'
}

# Variáveis que o REA lê ao iniciar. Ficam só neste arquivo local e no registro
# do agente: nenhuma variável global do Windows é alterada.
$envFile = Join-Path $LocalDir 'env.json'
$existing = if (Test-Path $envFile) { Get-Content $envFile -Raw | ConvertFrom-Json } else { $null }
$reaEnv = [ordered]@{
    GHIDRA_INSTALL_DIR               = $ghidra.Dir
    JAVA_HOME                        = $jdk
    REA_ANALYSIS_PROVIDER            = 'ghidra'
    REA_GHIDRA_STARTUP_TIMEOUT_MS    = [string]$Config.ghidra.startupTimeoutMs
    GHIDRA_HEADLESS_MAXMEM           = $Config.ghidra.headlessMaxMem
    REA_GHIDRA_DISABLED_ANALYZERS    = $Config.ghidra.disabledAnalyzers
}
$local = [ordered]@{
    env      = $reaEnv
    gamePath = if ($existing) { $existing.gamePath } else { $null }
    updated  = (Get-Date).ToString('s')
}
$local | ConvertTo-Json -Depth 4 | Set-Content -Path $envFile -Encoding UTF8
Write-Log "Ambiente gravado em $envFile" 'OK'

foreach ($k in $reaEnv.Keys) { Set-Item -Path "Env:$k" -Value $reaEnv[$k] }

if (-not $SkipAgentRegistration) {
    Write-Log "Prévia do registro do REA no agente '$($Config.rea.agentClient)':" 'STEP'
    $preview = & rea setup --client $Config.rea.agentClient --dry-run 2>&1 | Out-String
    Add-Content -Path $LogFile -Value $preview -Encoding UTF8
    # Na tela, só as ações planejadas; a prévia completa (com o doctor) vai para o log.
    $planned = ($preview -split "`r?`n") | Where-Object { $_ -match '^(status:|plannedActions|\s+"configure_client|\s+"install_skill)' }
    $planned | ForEach-Object { Write-Host $_ }
    if (Confirm-Step 'Aplicar este registro (o rea setup faz backup da configuração atual)?') {
        & rea setup --client $Config.rea.agentClient --yes | Out-String | ForEach-Object { Add-Content -Path $LogFile -Value $_ -Encoding UTF8 }

        # O rea setup não registra o heap nem os analisadores desligados, e sem
        # eles o GTAIV.exe estoura memória/prazo numa sessão MCP. Ver
        # lib\merge-agent-env.mjs.
        if ($Config.rea.agentClient -eq 'claude_code') {
            $claudeJson = Join-Path $env:USERPROFILE '.claude.json'
            if ($env:CLAUDE_CONFIG_DIR) { $claudeJson = Join-Path $env:CLAUDE_CONFIG_DIR '.claude.json' }
            $result = & node (Join-Path $ReverseRoot 'lib\merge-agent-env.mjs') $claudeJson `
                "GHIDRA_HEADLESS_MAXMEM=$($reaEnv.GHIDRA_HEADLESS_MAXMEM)" `
                "REA_GHIDRA_DISABLED_ANALYZERS=$($reaEnv.REA_GHIDRA_DISABLED_ANALYZERS)"
            Write-Log "Registro do Claude Code: $result" 'OK'
        }
        Write-Log 'Registro aplicado. Reinicie o agente para carregar o MCP com o Ghidra.' 'OK'
    } else {
        Write-Log 'Registro do agente pulado.' 'WARN'
    }
}

Write-Log 'Conferindo o provider Ghidra com rea doctor...' 'STEP'
$doctor = & rea doctor --format json 2>$null | Out-String | ConvertFrom-Json
$g = $doctor.doctor.providerInspections | Where-Object { $_.id -eq 'ghidra' }
if (-not $g) { $g = $doctor.providerInspections | Where-Object { $_.id -eq 'ghidra' } }
if ($g -and $g.available) {
    Write-Log "Ghidra disponível para o REA (versão $($g.providerVersion))." 'OK'
} else {
    Write-Log 'O REA não considera o Ghidra disponível. Veja: rea doctor --format json' 'ERROR'
    exit 1
}

Write-Log 'Pronto. Próximo passo: .\reverse\Invoke-ReverseAnalysis.ps1' 'OK'
# O rea doctor sai com código != 0 quando QUALQUER verificação está pendente
# (outros agentes, outros providers); o que importa aqui já foi conferido.
exit 0
