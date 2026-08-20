<#
.SYNOPSIS
    Instalador automatizado do mod TurboTraffic-IV para GTA IV.

.DESCRIPTION
    Detecta automaticamente a instalação do GTA IV (Steam, Rockstar Games Launcher,
    GOG, Epic Games ou instalação manual) e copia os arquivos do mod e suas
    dependências para as pastas corretas do jogo.

    Rodar o instalador de novo por cima de uma instalação existente atualiza o mod:
    os arquivos são sempre sobrescritos, e arquivos de versões antigas do
    TurboTraffic-IV que não fazem mais parte da versão atual são removidos
    automaticamente (com base num manifesto salvo na pasta do jogo). Não é
    necessário apagar a instalação anterior manualmente antes de atualizar.

.PARAMETER GamePath
    Caminho manual para o diretório do GTA IV. Se informado, pula a detecção automática.

.PARAMETER Backup
    Faz backup dos arquivos existentes que serão sobrescritos antes de copiar.

.PARAMETER Uninstall
    Remove os arquivos do mod em vez de instalar. Use -UninstallMode para escolher o escopo.

.PARAMETER UninstallMode
    'Full' remove todos os arquivos (loader, ScriptHookDotNet e mod).
    'ModOnly' remove apenas os arquivos exclusivos do TurboTraffic-IV, preservando
    dependências que outros mods possam usar. Padrão: ModOnly.

.PARAMETER Force
    Não pede confirmação antes de copiar/remover arquivos.

.EXAMPLE
    .\Install-TurboTrafficIV.ps1

.EXAMPLE
    .\Install-TurboTrafficIV.ps1 -GamePath "D:\Games\Grand Theft Auto IV" -Backup

.EXAMPLE
    .\Install-TurboTrafficIV.ps1 -Uninstall -UninstallMode Full
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$GamePath,
    [switch]$Backup,
    [switch]$Uninstall,
    [ValidateSet('Full', 'ModOnly')]
    [string]$UninstallMode = 'ModOnly',
    [switch]$Force
)

# ---------------------------------------------------------------------------
# Configuração
# ---------------------------------------------------------------------------

$ScriptRoot   = $PSScriptRoot
$MainFolder   = Join-Path $ScriptRoot 'Main_GTAIV_Folder'
$ScriptsFolder = Join-Path $ScriptRoot 'scripts'
$LogFile      = Join-Path $ScriptRoot ("install-log-{0}.txt" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

# Arquivos que outros mods de ScriptHookDotNet podem compartilhar
$SharedFiles = @(
    'dinput8.dll',
    'ScriptHook.dll',
    'ScriptHookDotNet.asi',
    'aCompleteEditionHook.asi'
)
$SharedScriptsFiles = @('ScriptHookDotNet.dll')

# Arquivos exclusivos do TurboTraffic-IV
$ModOnlyFiles = @('MyLoader.asi', 'MyLoader.pdb')
$ModOnlyScriptsFiles = @('ResetHandling.net.dll', 'ResetHandling.net.pdb')

$GameExecutables = @('GTAIV.exe', 'EFLC.exe')

# Manifesto salvo na pasta do jogo para permitir que atualizações removam
# automaticamente arquivos de versões antigas do mod que não existem mais.
$ManifestFileName = 'TurboTrafficIV_manifest.json'

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $timestamp = Get-Date -Format 'HH:mm:ss'
    $line = "[$timestamp][$Level] $Message"
    Add-Content -Path $LogFile -Value $line -ErrorAction SilentlyContinue

    switch ($Level) {
        'OK'    { Write-Host $line -ForegroundColor Green }
        'WARN'  { Write-Host $line -ForegroundColor Yellow }
        'ERROR' { Write-Host $line -ForegroundColor Red }
        'STEP'  { Write-Host $line -ForegroundColor Cyan }
        default { Write-Host $line }
    }
}

function Test-GTAIVPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
    foreach ($exe in $GameExecutables) {
        if (Test-Path -LiteralPath (Join-Path $Path $exe)) { return $true }
    }
    return $false
}

function Test-GameProcessRunning {
    foreach ($exeName in $GameExecutables) {
        $procName = [System.IO.Path]::GetFileNameWithoutExtension($exeName)
        if (Get-Process -Name $procName -ErrorAction SilentlyContinue) {
            return $true
        }
    }
    return $false
}

# ---------------------------------------------------------------------------
# Detecção por plataforma
# ---------------------------------------------------------------------------

function Get-SteamCandidates {
    $candidates = @()
    $steamRoots = @()

    foreach ($regPath in @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam'
    )) {
        try {
            if (Test-Path $regPath) {
                $key = Get-ItemProperty -Path $regPath -ErrorAction Stop
                $steamPath = $key.SteamPath
                if (-not $steamPath) { $steamPath = $key.InstallPath }
                if ($steamPath -and (Test-Path $steamPath)) {
                    $steamRoots += ($steamPath -replace '/', '\')
                }
            }
        } catch { }
    }

    # Fallback padrão caso o registro não retorne nada
    foreach ($default in @(
        "${env:ProgramFiles(x86)}\Steam",
        "${env:ProgramFiles}\Steam"
    )) {
        if ($default -and (Test-Path $default)) { $steamRoots += $default }
    }

    $steamRoots = $steamRoots | Select-Object -Unique
    $libraryFolders = @()

    foreach ($root in $steamRoots) {
        $libraryFolders += $root
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            try {
                $content = Get-Content $vdf -Raw
                $matches = [regex]::Matches($content, '"path"\s*"([^"]+)"')
                foreach ($m in $matches) {
                    $libPath = $m.Groups[1].Value -replace '\\\\', '\'
                    if (Test-Path $libPath) { $libraryFolders += $libPath }
                }
            } catch { }
        }
    }

    foreach ($lib in ($libraryFolders | Select-Object -Unique)) {
        $candidatePath = Join-Path $lib 'steamapps\common\Grand Theft Auto IV'
        if (Test-GTAIVPath $candidatePath) {
            $candidates += [PSCustomObject]@{ Path = $candidatePath; Platform = 'Steam' }
        }
    }

    return $candidates
}

function Get-RockstarCandidates {
    $candidates = @()

    foreach ($regPath in @(
        'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto IV',
        'HKLM:\SOFTWARE\Rockstar Games\Grand Theft Auto IV',
        'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto IV: Complete Edition',
        'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto Episodes From Liberty City'
    )) {
        try {
            if (Test-Path $regPath) {
                $key = Get-ItemProperty -Path $regPath -ErrorAction Stop
                $folder = $key.InstallFolder
                if (-not $folder) { $folder = $key.InstallPath }
                if ($folder -and (Test-GTAIVPath $folder)) {
                    $candidates += [PSCustomObject]@{ Path = $folder; Platform = 'Rockstar Games Launcher' }
                }
            }
        } catch { }
    }

    foreach ($default in @(
        "${env:ProgramFiles}\Rockstar Games\Grand Theft Auto IV",
        "${env:ProgramFiles(x86)}\Rockstar Games\Grand Theft Auto IV",
        "${env:ProgramFiles}\Rockstar Games\Grand Theft Auto IV Complete Edition",
        "${env:ProgramFiles(x86)}\Rockstar Games\Grand Theft Auto IV Complete Edition"
    )) {
        if ($default -and (Test-GTAIVPath $default)) {
            $candidates += [PSCustomObject]@{ Path = $default; Platform = 'Rockstar Games Launcher' }
        }
    }

    return $candidates
}

function Get-GOGCandidates {
    $candidates = @()

    foreach ($basePath in @(
        'HKLM:\SOFTWARE\WOW6432Node\GOG.com\Games',
        'HKLM:\SOFTWARE\GOG.com\Games'
    )) {
        try {
            if (Test-Path $basePath) {
                $gameKeys = Get-ChildItem -Path $basePath -ErrorAction Stop
                foreach ($gameKey in $gameKeys) {
                    try {
                        $props = Get-ItemProperty -Path $gameKey.PSPath -ErrorAction Stop
                        $name = $props.gameName
                        $path = $props.path
                        if ($path -and $name -and ($name -match 'Grand Theft Auto IV')) {
                            if (Test-GTAIVPath $path) {
                                $candidates += [PSCustomObject]@{ Path = $path; Platform = 'GOG' }
                            }
                        }
                    } catch { }
                }
            }
        } catch { }
    }

    foreach ($default in @(
        "${env:ProgramFiles(x86)}\GOG Games\Grand Theft Auto IV Complete Edition",
        "${env:ProgramFiles(x86)}\GOG Games\Grand Theft Auto IV"
    )) {
        if ($default -and (Test-GTAIVPath $default)) {
            $candidates += [PSCustomObject]@{ Path = $default; Platform = 'GOG' }
        }
    }

    return $candidates
}

function Get-EpicCandidates {
    $candidates = @()
    $manifestDir = "$env:ProgramData\Epic\EpicGamesLauncher\Data\Manifests"

    if (Test-Path $manifestDir) {
        $items = Get-ChildItem -Path $manifestDir -Filter '*.item' -ErrorAction SilentlyContinue
        foreach ($item in $items) {
            try {
                $manifest = Get-Content $item.FullName -Raw | ConvertFrom-Json
                $displayName = $manifest.DisplayName
                $installLocation = $manifest.InstallLocation
                if ($displayName -match 'Grand Theft Auto IV' -and $installLocation) {
                    if (Test-GTAIVPath $installLocation) {
                        $candidates += [PSCustomObject]@{ Path = $installLocation; Platform = 'Epic Games' }
                    }
                }
            } catch { }
        }
    }

    foreach ($default in @(
        "${env:ProgramFiles}\Epic Games\GTAIV",
        "${env:ProgramFiles(x86)}\Epic Games\GTAIV"
    )) {
        if ($default -and (Test-GTAIVPath $default)) {
            $candidates += [PSCustomObject]@{ Path = $default; Platform = 'Epic Games' }
        }
    }

    return $candidates
}

function Get-ManualCandidates {
    # Varre raízes comuns em todas as unidades fixas como último recurso
    $candidates = @()
    $commonSubpaths = @(
        'Games\Grand Theft Auto IV',
        'Grand Theft Auto IV',
        'GTA IV',
        'GTAIV'
    )

    try {
        $drives = Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue |
            Where-Object { $_.Free -ne $null }
    } catch {
        $drives = @()
    }

    foreach ($drive in $drives) {
        foreach ($sub in $commonSubpaths) {
            $candidatePath = Join-Path "$($drive.Root)" $sub
            if (Test-GTAIVPath $candidatePath) {
                $candidates += [PSCustomObject]@{ Path = $candidatePath; Platform = 'Manual/Outro' }
            }
        }
    }

    return $candidates
}

function Find-GTAIVInstallations {
    Write-Log 'Procurando instalações do GTA IV (Steam, Rockstar Games Launcher, GOG, Epic Games)...' 'STEP'

    $all = @()
    $all += Get-SteamCandidates
    $all += Get-RockstarCandidates
    $all += Get-GOGCandidates
    $all += Get-EpicCandidates
    $all += Get-ManualCandidates

    $unique = $all | Sort-Object Path -Unique
    return $unique
}

# ---------------------------------------------------------------------------
# Seleção do diretório do jogo
# ---------------------------------------------------------------------------

function Select-GamePath {
    if ($GamePath) {
        if (Test-GTAIVPath $GamePath) {
            Write-Log "Usando caminho informado manualmente: $GamePath" 'OK'
            return $GamePath
        } else {
            Write-Log "O caminho informado ('$GamePath') não contém GTAIV.exe ou EFLC.exe." 'ERROR'
            return $null
        }
    }

    $found = Find-GTAIVInstallations

    if ($found.Count -eq 0) {
        Write-Log 'Nenhuma instalação do GTA IV foi detectada automaticamente.' 'WARN'
        $manual = Read-Host 'Digite o caminho completo da pasta do GTA IV (onde está o GTAIV.exe/EFLC.exe)'
        if (Test-GTAIVPath $manual) {
            return $manual
        }
        Write-Log 'Caminho inválido. Abortando.' 'ERROR'
        return $null
    }

    if ($found.Count -eq 1) {
        Write-Log "Instalação encontrada via $($found[0].Platform): $($found[0].Path)" 'OK'
        return $found[0].Path
    }

    Write-Host ''
    Write-Host 'Múltiplas instalações do GTA IV foram encontradas:' -ForegroundColor Cyan
    for ($i = 0; $i -lt $found.Count; $i++) {
        Write-Host ("  [{0}] {1} -> {2}" -f ($i + 1), $found[$i].Platform, $found[$i].Path)
    }
    Write-Host ("  [{0}] Informar outro caminho manualmente" -f ($found.Count + 1))
    Write-Host ''

    do {
        $choice = Read-Host "Escolha uma opção (1-$($found.Count + 1))"
    } until ($choice -match '^\d+$' -and [int]$choice -ge 1 -and [int]$choice -le ($found.Count + 1))

    $choiceIndex = [int]$choice
    if ($choiceIndex -eq ($found.Count + 1)) {
        $manual = Read-Host 'Digite o caminho completo da pasta do GTA IV'
        if (Test-GTAIVPath $manual) { return $manual }
        Write-Log 'Caminho inválido. Abortando.' 'ERROR'
        return $null
    }

    return $found[$choiceIndex - 1].Path
}

# ---------------------------------------------------------------------------
# Instalação
# ---------------------------------------------------------------------------

function Backup-ExistingFiles {
    param([string]$TargetGamePath)

    $backupDir = Join-Path $TargetGamePath ("TurboTrafficIV_Backup_{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $anyBackup = $false

    $filesToCheck = @()
    Get-ChildItem -Path $MainFolder -File | ForEach-Object {
        $filesToCheck += [PSCustomObject]@{ Source = $_.FullName; Target = (Join-Path $TargetGamePath $_.Name) }
    }
    Get-ChildItem -Path $ScriptsFolder -File | ForEach-Object {
        $filesToCheck += [PSCustomObject]@{ Source = $_.FullName; Target = (Join-Path $TargetGamePath "scripts\$($_.Name)") }
    }

    foreach ($file in $filesToCheck) {
        if (Test-Path -LiteralPath $file.Target) {
            if (-not $anyBackup) {
                New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
                $anyBackup = $true
            }
            $relative = $file.Target.Substring($TargetGamePath.Length).TrimStart('\')
            $backupTarget = Join-Path $backupDir $relative
            New-Item -ItemType Directory -Path (Split-Path $backupTarget -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.Target -Destination $backupTarget -Force
        }
    }

    if ($anyBackup) {
        Write-Log "Backup dos arquivos existentes criado em: $backupDir" 'OK'
    } else {
        Write-Log 'Nenhum arquivo existente para backup (instalação limpa).' 'INFO'
    }
}

function Get-InstalledManifest {
    param([string]$TargetGamePath)

    $manifestPath = Join-Path $TargetGamePath $ManifestFileName
    if (-not (Test-Path -LiteralPath $manifestPath)) { return $null }

    try {
        return Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    } catch {
        Write-Log "Não foi possível ler o manifesto de instalação anterior ($manifestPath): $($_.Exception.Message)" 'WARN'
        return $null
    }
}

function Save-InstalledManifest {
    param([string]$TargetGamePath, [string[]]$RelativeFiles)

    $manifestPath = Join-Path $TargetGamePath $ManifestFileName
    $manifest = [PSCustomObject]@{
        InstalledAt = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
        Files       = @($RelativeFiles)
    }

    if ($PSCmdlet.ShouldProcess($manifestPath, 'Salvar manifesto de instalação')) {
        $manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    }
}

function Remove-OrphanedModFiles {
    param([string]$TargetGamePath, [string[]]$CurrentRelativeFiles)

    $manifest = Get-InstalledManifest -TargetGamePath $TargetGamePath
    if (-not $manifest -or -not $manifest.Files) { return }

    $orphans = @($manifest.Files) | Where-Object { $CurrentRelativeFiles -notcontains $_ }
    if ($orphans.Count -eq 0) { return }

    Write-Log 'Removendo arquivos de uma versão anterior do mod que não existem mais...' 'STEP'
    foreach ($relative in $orphans) {
        $target = Join-Path $TargetGamePath $relative
        if (Test-Path -LiteralPath $target) {
            if ($PSCmdlet.ShouldProcess($target, 'Remover arquivo obsoleto de versão anterior')) {
                Remove-Item -LiteralPath $target -Force
                Write-Log "  Removido (versão anterior): $relative" 'OK'
            }
        }
    }
}

function Install-Mod {
    param([string]$TargetGamePath)

    Write-Log "Instalando/atualizando TurboTraffic-IV em: $TargetGamePath" 'STEP'

    if ($Backup) {
        Backup-ExistingFiles -TargetGamePath $TargetGamePath
    }

    # Remove arquivos exclusivos do mod deixados por uma versão anterior que já
    # não existem na versão atual, para que o usuário nunca precise apagar a
    # instalação antiga manualmente antes de atualizar.
    $currentModRelativeFiles = @()
    $currentModRelativeFiles += $ModOnlyFiles
    $currentModRelativeFiles += $ModOnlyScriptsFiles | ForEach-Object { "scripts\$_" }
    Remove-OrphanedModFiles -TargetGamePath $TargetGamePath -CurrentRelativeFiles $currentModRelativeFiles

    # Passo 1: Loader + dependências para a raiz do jogo
    Write-Log 'Copiando arquivos de Main_GTAIV_Folder/ para o diretório do jogo...' 'STEP'
    Get-ChildItem -Path $MainFolder -File | ForEach-Object {
        $dest = Join-Path $TargetGamePath $_.Name
        if ($PSCmdlet.ShouldProcess($dest, 'Copiar arquivo')) {
            Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
            Write-Log "  OK  $($_.Name)" 'OK'
        }
    }

    # Passo 2: pasta scripts
    $gameScriptsPath = Join-Path $TargetGamePath 'scripts'
    if (-not (Test-Path $gameScriptsPath)) {
        Write-Log 'Criando pasta scripts/ no diretório do jogo...' 'STEP'
        if ($PSCmdlet.ShouldProcess($gameScriptsPath, 'Criar diretório')) {
            New-Item -ItemType Directory -Path $gameScriptsPath -Force | Out-Null
        }
    }

    Write-Log 'Copiando arquivos de scripts/ para a pasta scripts/ do jogo...' 'STEP'
    Get-ChildItem -Path $ScriptsFolder -File | ForEach-Object {
        $dest = Join-Path $gameScriptsPath $_.Name
        if ($PSCmdlet.ShouldProcess($dest, 'Copiar arquivo')) {
            Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
            Write-Log "  OK  $($_.Name)" 'OK'
        }
    }

    # Verificação
    Write-Log 'Verificando instalação...' 'STEP'
    $missing = @()
    Get-ChildItem -Path $MainFolder -File | ForEach-Object {
        if (-not (Test-Path (Join-Path $TargetGamePath $_.Name))) { $missing += $_.Name }
    }
    Get-ChildItem -Path $ScriptsFolder -File | ForEach-Object {
        if (-not (Test-Path (Join-Path $gameScriptsPath $_.Name))) { $missing += "scripts\$($_.Name)" }
    }

    if ($missing.Count -eq 0) {
        Write-Log 'Instalação concluída com sucesso!' 'OK'
    } else {
        Write-Log "Instalação concluída com pendências. Arquivos ausentes: $($missing -join ', ')" 'WARN'
    }

    Save-InstalledManifest -TargetGamePath $TargetGamePath -RelativeFiles $currentModRelativeFiles
}

function Uninstall-Mod {
    param([string]$TargetGamePath)

    $rootFiles = $ModOnlyFiles
    $scriptsFiles = $ModOnlyScriptsFiles

    if ($UninstallMode -eq 'Full') {
        $rootFiles = $ModOnlyFiles + $SharedFiles
        $scriptsFiles = $ModOnlyScriptsFiles + $SharedScriptsFiles
    }

    Write-Log "Desinstalando TurboTraffic-IV de: $TargetGamePath (modo: $UninstallMode)" 'STEP'

    foreach ($name in $rootFiles) {
        $target = Join-Path $TargetGamePath $name
        if (Test-Path -LiteralPath $target) {
            if ($PSCmdlet.ShouldProcess($target, 'Remover arquivo')) {
                Remove-Item -LiteralPath $target -Force
                Write-Log "  Removido: $name" 'OK'
            }
        }
    }

    $gameScriptsPath = Join-Path $TargetGamePath 'scripts'
    foreach ($name in $scriptsFiles) {
        $target = Join-Path $gameScriptsPath $name
        if (Test-Path -LiteralPath $target) {
            if ($PSCmdlet.ShouldProcess($target, 'Remover arquivo')) {
                Remove-Item -LiteralPath $target -Force
                Write-Log "  Removido: scripts\$name" 'OK'
            }
        }
    }

    $manifestPath = Join-Path $TargetGamePath $ManifestFileName
    if (Test-Path -LiteralPath $manifestPath) {
        if ($PSCmdlet.ShouldProcess($manifestPath, 'Remover manifesto de instalação')) {
            Remove-Item -LiteralPath $manifestPath -Force
        }
    }

    Write-Log 'Desinstalação concluída.' 'OK'
}

# ---------------------------------------------------------------------------
# Execução principal
# ---------------------------------------------------------------------------

Write-Host ''
Write-Host '=== TurboTraffic-IV - Instalador ===' -ForegroundColor Magenta
Write-Host ''

if (-not (Test-Path $MainFolder) -or -not (Test-Path $ScriptsFolder)) {
    Write-Log "Pastas 'Main_GTAIV_Folder' e/ou 'scripts' não encontradas ao lado deste script. Execute-o a partir da raiz do repositório TurboTraffic-IV." 'ERROR'
    exit 1
}

if (-not $Uninstall) {
    $requiredModFiles = @()
    $requiredModFiles += $ModOnlyFiles | ForEach-Object { [PSCustomObject]@{ Folder = $MainFolder; Name = $_ } }
    $requiredModFiles += $ModOnlyScriptsFiles | ForEach-Object { [PSCustomObject]@{ Folder = $ScriptsFolder; Name = $_ } }

    $missingModFiles = $requiredModFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.Folder $_.Name)) }
    if ($missingModFiles.Count -gt 0) {
        Write-Log 'Os binários do mod ainda não foram compilados neste repositório:' 'ERROR'
        foreach ($f in $missingModFiles) {
            Write-Log ("  Faltando: {0}" -f (Join-Path $f.Folder $f.Name)) 'ERROR'
        }
        Write-Log 'Compile ResetHandling.sln (config Release|x86) e MyLoader.sln (config Release|Win32) antes de instalar.' 'ERROR'
        exit 1
    }
}

$targetPath = Select-GamePath
if (-not $targetPath) {
    exit 1
}

Write-Host ''
Write-Host "Diretório do jogo selecionado: $targetPath" -ForegroundColor Cyan
Write-Host ''

if (Test-GameProcessRunning) {
    Write-Log 'O GTA IV parece estar em execução. Feche o jogo antes de instalar/atualizar/remover o mod: os arquivos ficam bloqueados enquanto o processo está aberto.' 'ERROR'
    exit 1
}

if (-not $Force) {
    $action = if ($Uninstall) { 'DESINSTALAR' } else { 'INSTALAR' }
    $confirm = Read-Host "Confirma $action o TurboTraffic-IV neste diretório? (S/N)"
    if ($confirm -notmatch '^[SsYy]') {
        Write-Log 'Operação cancelada pelo usuário.' 'WARN'
        exit 0
    }
}

try {
    if ($Uninstall) {
        Uninstall-Mod -TargetGamePath $targetPath
    } else {
        Install-Mod -TargetGamePath $targetPath
    }
} catch {
    Write-Log "Erro durante a operação: $($_.Exception.Message)" 'ERROR'
    exit 1
}

Write-Host ''
Write-Log "Log salvo em: $LogFile" 'INFO'
