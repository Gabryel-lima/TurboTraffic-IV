<#
.SYNOPSIS
    Roda o pipeline de engenharia reversa do TurboTraffic-IV e gera o relatório.

.DESCRIPTION
    Cada execução cria reverse\runs\<data-hora>\ com:
      run.json          manifesto: etapas, tempos, identidade (SHA-256) dos artefatos
      run.log           log legível da execução
      operations.jsonl  uma linha por chamada ao REA (comando, duração, erro)
      *.json            saídas de cada etapa (Evidence do REA e análises derivadas)
      report.html       relatório navegável (abre com -Open)
      summary.md        resumo curto
    e atualiza reverse\knowledge\FINDINGS.md e reverse\runs\history.json.

    Etapas:
      Managed  REA inspeciona ScriptHookDotNet.dll e a DLL do mod; calcula o custo
               em natives de cada método do mod. Não precisa do jogo instalado.
      Pe       Cabeçalhos, seções, imports/exports e sondas de string do
               GTAIV.exe e ScriptHook.dll; recursos PE via REA. Precisa do jogo.
      ModLog   Resume Documentos\TurboTraffic-IV\TurboTrafficIV.log.
      Ghidra   Consultas de reverse\ghidra-queries.json no GTAIV.exe (lento na
               primeira vez; usa snapshot em reverse\.local\cache). Só com -Ghidra.
      Report   Gera report.html, summary.md e FINDINGS.md.

.PARAMETER GamePath
    Pasta que contém o GTAIV.exe. Se omitida: último caminho usado, depois
    detecção Steam/Rockstar. O caminho encontrado é lembrado em .local\env.json.

.PARAMETER ModDll
    DLL do mod a analisar. Padrão: scripts\ResetHandling.net.dll do repositório.

.PARAMETER Ghidra
    Inclui a etapa Ghidra (requer Setup-ReverseTools.ps1).

.PARAMETER Open
    Abre o report.html ao terminar.

.EXAMPLE
    .\reverse\Invoke-ReverseAnalysis.ps1 -Open

.EXAMPLE
    .\reverse\Invoke-ReverseAnalysis.ps1 -Ghidra -GamePath "D:\Games\GTA IV\GTAIV"
#>

[CmdletBinding()]
param(
    [string]$GamePath,
    [string]$ModDll,
    [switch]$Ghidra,
    [switch]$Open
)

$ErrorActionPreference = 'Stop'
$ReverseRoot = $PSScriptRoot
$RepoRoot = Split-Path $ReverseRoot -Parent
$Lib = Join-Path $ReverseRoot 'lib'
$Config = Get-Content (Join-Path $ReverseRoot 'reverse.config.json') -Raw | ConvertFrom-Json
$LocalDir = Join-Path $ReverseRoot '.local'
$CacheDir = Join-Path $LocalDir 'cache'
$RunsRoot = Join-Path $ReverseRoot 'runs'
$RunId = Get-Date -Format 'yyyyMMdd-HHmmss'
$RunDir = Join-Path $RunsRoot $RunId
New-Item -ItemType Directory -Force -Path $CacheDir, $RunDir | Out-Null

$Utf8NoBom = New-Object System.Text.UTF8Encoding $false
function Write-JsonFile {
    # Sem BOM: o Node não aceita BOM no JSON.parse.
    param([string]$Path, $Object)
    [System.IO.File]::WriteAllText($Path, ($Object | ConvertTo-Json -Depth 8), $Utf8NoBom)
}

$RunLog = Join-Path $RunDir 'run.log'
function Write-Log {
    param([string]$Message, [ValidateSet('INFO', 'OK', 'WARN', 'ERROR', 'STEP')][string]$Level = 'INFO')
    $line = "[{0}][{1}] {2}" -f (Get-Date -Format 'HH:mm:ss'), $Level, $Message
    [System.IO.File]::AppendAllText($RunLog, $line + [Environment]::NewLine, $Utf8NoBom)
    switch ($Level) {
        'OK'    { Write-Host $line -ForegroundColor Green }
        'WARN'  { Write-Host $line -ForegroundColor Yellow }
        'ERROR' { Write-Host $line -ForegroundColor Red }
        'STEP'  { Write-Host $line -ForegroundColor Cyan }
        default { Write-Host $line }
    }
}

# ---------------------------------------------------------------------------
# Ambiente local (gravado pelo Setup-ReverseTools.ps1)
# ---------------------------------------------------------------------------

$EnvFile = Join-Path $LocalDir 'env.json'
$Local = if (Test-Path $EnvFile) { Get-Content $EnvFile -Raw | ConvertFrom-Json } else { $null }
if ($Local -and $Local.env) {
    foreach ($p in $Local.env.PSObject.Properties) { Set-Item -Path "Env:$($p.Name)" -Value ([string]$p.Value) }
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) { Write-Log 'Node.js não encontrado. Rode reverse\Setup-ReverseTools.ps1.' 'ERROR'; exit 1 }

# ---------------------------------------------------------------------------
# Localização do jogo
# ---------------------------------------------------------------------------

function Resolve-GameDir {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Container)) { return $null }
    foreach ($sub in @('', 'GTAIV')) {
        $candidate = if ($sub) { Join-Path $Path $sub } else { $Path }
        if (Test-Path -LiteralPath (Join-Path $candidate $Config.game.executable)) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    return $null
}

function Find-GameDir {
    # Detecção compacta (Steam e Rockstar). O Install-TurboTrafficIV.ps1 tem a
    # detecção completa (GOG, Epic, manual); se falhar aqui, use -GamePath.
    $candidates = @()
    foreach ($reg in @('HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto IV', 'HKLM:\SOFTWARE\Rockstar Games\Grand Theft Auto IV')) {
        try { $k = Get-ItemProperty -Path $reg -ErrorAction Stop; $candidates += $k.InstallFolder } catch { }
    }
    $steamRoots = @("${env:ProgramFiles(x86)}\Steam")
    try { $steamRoots += (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction Stop).SteamPath -replace '/', '\' } catch { }
    foreach ($root in ($steamRoots | Select-Object -Unique)) {
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        $libs = @($root)
        if (Test-Path $vdf) { $libs += [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"') | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' } }
        foreach ($lib in $libs) { $candidates += Join-Path $lib 'steamapps\common\Grand Theft Auto IV' }
    }
    foreach ($c in $candidates) { $r = Resolve-GameDir $c; if ($r) { return $r } }
    return $null
}

$GameDir = Resolve-GameDir $GamePath
if (-not $GameDir -and $Local -and $Local.gamePath) { $GameDir = Resolve-GameDir $Local.gamePath }
if (-not $GameDir) { $GameDir = Find-GameDir }

if ($GameDir) {
    $saved = [ordered]@{ env = if ($Local) { $Local.env } else { $null }; gamePath = $GameDir; updated = (Get-Date).ToString('s') }
    Write-JsonFile $EnvFile $saved
}

if (-not $ModDll) { $ModDll = Join-Path $RepoRoot 'scripts\ResetHandling.net.dll' }
$ShdnDll = Join-Path $RepoRoot 'scripts\ScriptHookDotNet.dll'
$ModLogFile = $null
foreach ($root in @([Environment]::GetFolderPath('MyDocuments'), [Environment]::GetFolderPath('LocalApplicationData'))) {
    $candidate = Join-Path $root 'TurboTraffic-IV\TurboTrafficIV.log'
    if ($root -and (Test-Path $candidate)) { $ModLogFile = $candidate; break }
}

# ---------------------------------------------------------------------------
# Identidade dos artefatos
# ---------------------------------------------------------------------------

function Get-Identity {
    param([string]$Name, [string]$Path)
    if ($Path -and (Test-Path -LiteralPath $Path)) {
        return [ordered]@{ name = $Name; path = $Path; sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    return [ordered]@{ name = $Name; path = $Path; sha256 = $null }
}

$artifacts = [ordered]@{
    'gtaiv-exe'            = Get-Identity 'GTAIV.exe' $(if ($GameDir) { Join-Path $GameDir $Config.game.executable })
    'scripthook-dll'       = Get-Identity 'ScriptHook.dll' $(if ($GameDir) { Join-Path $GameDir 'ScriptHook.dll' })
    'scripthookdotnet-dll' = Get-Identity 'ScriptHookDotNet.dll' $ShdnDll
    'mod-dll'              = Get-Identity 'ResetHandling.net.dll' $ModDll
    'mod-log'              = [ordered]@{ name = 'TurboTrafficIV.log'; path = $ModLogFile; sha256 = $null }
}

$knownBuilds = @{}
foreach ($p in $Config.game.knownBuilds.PSObject.Properties) { $knownBuilds[$p.Name] = $p.Value }

$stages = New-Object System.Collections.ArrayList
$run = [ordered]@{
    run_id       = $RunId
    machine      = $env:COMPUTERNAME
    started      = (Get-Date).ToString('s')
    finished     = $null
    game_path    = $GameDir
    artifacts    = $artifacts
    known_builds = $knownBuilds
    stages       = $stages
}

Write-Log "Execução $RunId em $RunDir" 'STEP'
Write-Log ("Jogo: {0}" -f $(if ($GameDir) { $GameDir } else { 'não encontrado (etapas Pe e Ghidra serão puladas; use -GamePath)' })) $(if ($GameDir) { 'INFO' } else { 'WARN' })
$exeSha = $artifacts['gtaiv-exe'].sha256
if ($exeSha) {
    $label = $knownBuilds[$exeSha]
    if ($label) { Write-Log "GTAIV.exe reconhecido: $label" 'OK' }
    else { Write-Log "GTAIV.exe com SHA-256 desconhecido ($exeSha): achados do binário podem não valer para esta build." 'WARN' }
}

function Invoke-Stage {
    param([string]$Name, [scriptblock]$Body, [string]$SkipReason)
    $record = [ordered]@{ name = $Name; status = 'ok'; seconds = $null; message = '' }
    if ($SkipReason) {
        $record.status = 'pulada'; $record.message = $SkipReason
        Write-Log "[$Name] pulada: $SkipReason" 'WARN'
        [void]$stages.Add($record); return
    }
    Write-Log "[$Name] iniciando" 'STEP'
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $msg = & $Body
        $record.message = [string]($msg | Select-Object -Last 1)
        Write-Log "[$Name] ok ($([math]::Round($sw.Elapsed.TotalSeconds, 1)) s) $($record.message)" 'OK'
    } catch {
        $record.status = 'failed'; $record.message = $_.Exception.Message
        Write-Log "[$Name] falhou: $($_.Exception.Message)" 'ERROR'
    }
    $record.seconds = $sw.Elapsed.TotalSeconds
    [void]$stages.Add($record)
}

function Invoke-Node {
    # Roda um script de reverse\lib e falha a etapa se ele falhar.
    param([string]$Script, [string[]]$Arguments)
    $output = & node (Join-Path $Lib $Script) @Arguments 2>&1
    $output | ForEach-Object { Write-Log "    $_" }
    if ($LASTEXITCODE -ne 0) { throw "$Script terminou com código $LASTEXITCODE" }
    return ($output | Select-Object -Last 1)
}

function Invoke-Rea {
    # Saída JSON gravada por rea-run.mjs, que também registra operations.jsonl.
    param([string]$OutFile, [string[]]$Arguments)
    Invoke-Node 'rea-run.mjs' (@($RunDir, $OutFile, '--') + $Arguments) | Out-Null
}

function Get-CachedMembers {
    # inspect-managed-members é determinístico por SHA-256: reaproveita o cache.
    param([hashtable]$Identity, [string]$Label)
    $cached = Join-Path $CacheDir ("members-{0}.json" -f $Identity.sha256)
    if (Test-Path $cached) { Write-Log "    $Label`: cache $($Identity.sha256.Substring(0, 12))"; return $cached }
    Invoke-Rea $cached @('inspect-managed-members', $Identity.path, '--format', 'json')
    return $cached
}

# ---------------------------------------------------------------------------
# Etapas
# ---------------------------------------------------------------------------

Invoke-Stage 'Managed' -SkipReason $(if (-not $artifacts['mod-dll'].sha256) { "DLL do mod não encontrada: $ModDll" } elseif (-not $artifacts['scripthookdotnet-dll'].sha256) { 'scripts\ScriptHookDotNet.dll ausente' }) -Body {
    $shdn = Get-CachedMembers $artifacts['scripthookdotnet-dll'] 'ScriptHookDotNet.dll'
    $mod = Get-CachedMembers $artifacts['mod-dll'] 'ResetHandling.net.dll'
    Invoke-Node 'wrapper-cost.mjs' @($shdn, $mod, (Join-Path $RunDir 'wrapper-cost.json'))
}

Invoke-Stage 'Pe' -SkipReason $(if (-not $GameDir) { 'jogo não encontrado' }) -Body {
    $probes = Join-Path $RunDir 'probes.json'
    Write-JsonFile $probes @($Config.engineFlagProbes)
    Invoke-Node 'pe-layout.mjs' @($artifacts['gtaiv-exe'].path, (Join-Path $RunDir 'pe-gtaiv.json'), $probes) | Out-Null
    if ($artifacts['scripthook-dll'].sha256) { Invoke-Node 'pe-layout.mjs' @($artifacts['scripthook-dll'].path, (Join-Path $RunDir 'pe-scripthook.json')) | Out-Null }
    Invoke-Rea (Join-Path $RunDir 'pe-resources.json') @('inspect-pe-resources', $artifacts['gtaiv-exe'].path, '--format', 'json')
    'cabeçalhos, seções, sondas e recursos'
}

Invoke-Stage 'ModLog' -SkipReason $(if (-not $ModLogFile) { 'TurboTrafficIV.log não encontrado em Documentos ou LocalAppData' }) -Body {
    # Sessão anterior primeiro (o mod a preserva ao abrir uma sessão nova).
    $previous = Join-Path (Split-Path $ModLogFile -Parent) 'TurboTrafficIV.anterior.log'
    Invoke-Node 'mod-log.mjs' @((Join-Path $RunDir 'mod-log.json'), $previous, $ModLogFile)
}

$ghidraSkip = $null
if (-not $Ghidra) { $ghidraSkip = 'use -Ghidra para incluir (lento na primeira vez)' }
elseif (-not $GameDir) { $ghidraSkip = 'jogo não encontrado' }
elseif (-not $env:GHIDRA_INSTALL_DIR) { $ghidraSkip = 'Ghidra não configurado: rode reverse\Setup-ReverseTools.ps1' }
Invoke-Stage 'Ghidra' -SkipReason $ghidraSkip -Body {
    $snapshot = Join-Path $CacheDir ("gtaiv-{0}.rea-snapshot" -f $exeSha.Substring(0, 16))
    Invoke-Node 'ghidra-queries.mjs' @($RunDir, $artifacts['gtaiv-exe'].path, $snapshot, (Join-Path $ReverseRoot 'ghidra-queries.json'))
}

$run.finished = (Get-Date).ToString('s')
Write-JsonFile (Join-Path $RunDir 'run.json') $run

Invoke-Stage 'Report' -Body {
    Invoke-Node 'build-report.mjs' @($RunDir, (Join-Path $ReverseRoot 'knowledge\findings.json'), $RunsRoot)
}
# A etapa Report se registra depois de o run.json já ter sido escrito.
Write-JsonFile (Join-Path $RunDir 'run.json') $run

$report = Join-Path $RunDir 'report.html'
if (Test-Path $report) {
    Copy-Item $report (Join-Path $RunsRoot 'latest.html') -Force
    Write-Log "Relatório: $report" 'OK'
    if ($Open) { Start-Process $report }
}
# Guarda só as últimas execuções; o histórico resumido de todas continua em
# runs\history.json.
$keep = [int]$Config.analysis.keepRuns
$old = @(Get-ChildItem -Path $RunsRoot -Directory | Where-Object { $_.Name -match '^\d{8}-\d{6}$' } | Sort-Object Name -Descending | Select-Object -Skip $keep)
foreach ($dir in $old) { Remove-Item -LiteralPath $dir.FullName -Recurse -Force }
if ($old.Count -gt 0) { Write-Log "$($old.Count) execução(ões) antiga(s) removida(s); mantidas as últimas $keep." 'INFO' }

$failed = @($stages | Where-Object { $_.status -eq 'failed' }).Count
if ($failed -gt 0) { Write-Log "$failed etapa(s) falharam. Veja $RunLog e operations.jsonl." 'ERROR'; exit 1 }
