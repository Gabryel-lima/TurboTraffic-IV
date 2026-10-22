<#
.SYNOPSIS
    Compila o mod (ResetHandling.net.dll) para a pasta scripts\.

.DESCRIPTION
    Usa o MSBuild do Visual Studio quando ele existe (Release|x86, a mesma
    configuração do ResetHandling.sln). Sem Visual Studio, cai no compilador C#
    que vem com o Windows (.NET Framework 4.x, csc.exe), lendo a lista de
    arquivos e referências direto do ResetHandling.csproj — então qualquer
    máquina Windows consegue compilar o mod.

    A saída vai para scripts\ResetHandling.net.dll (+ .pdb), que é onde o
    Install-TurboTrafficIV.ps1 procura. O log fica em logs\build.log, reescrito
    a cada execução.

    O MyLoader (C++) não é compilado aqui: ele precisa do Visual Studio e muda
    raramente; o binário pronto já está em Main_GTAIV_Folder\.

.PARAMETER Compiler
    'Auto' (padrão) usa MSBuild se houver, senão csc. 'Csc' força o csc.

.EXAMPLE
    .\Build-TurboTrafficIV.ps1
    .\Install-TurboTrafficIV.ps1
#>

[CmdletBinding()]
param(
    [ValidateSet('Auto', 'Csc')]
    [string]$Compiler = 'Auto'
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Project = Join-Path $Root 'ResetHandling\ResetHandling.csproj'
$OutDir = Join-Path $Root 'scripts'
$OutDll = Join-Path $OutDir 'ResetHandling.net.dll'
$LogDir = Join-Path $Root 'logs'
$LogFile = Join-Path $LogDir 'build.log'

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Set-Content -Path $LogFile -Encoding UTF8 -Value ("TurboTraffic-IV - compilação - {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
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

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) { return $null }
    $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
    if ($found -and (Test-Path $found)) { return $found }
    return $null
}

function Invoke-Csc {
    # O csc do .NET Framework só entende C# 5: o código do mod se mantém
    # dentro disso de propósito (sem $"...", ?. ou nameof).
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { throw "csc.exe do .NET Framework 4 não encontrado em $csc" }

    $xml = [xml](Get-Content -LiteralPath $Project -Raw)
    $projectDir = Split-Path $Project -Parent
    $sources = @($xml.Project.ItemGroup.Compile | Where-Object { $_ } | ForEach-Object { Join-Path $projectDir $_.Include })
    $references = @()
    foreach ($ref in ($xml.Project.ItemGroup.Reference | Where-Object { $_ })) {
        $name = ($ref.Include -split ',')[0]
        if ($ref.HintPath) { $references += "-r:$(Join-Path $projectDir $ref.HintPath)" }
        elseif ($name -in @('System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Xml', 'System.Xml.Linq', 'System.Data', 'Microsoft.CSharp', 'System.Data.DataSetExtensions')) {
            $references += "-r:$name.dll"
        }
    }

    $missing = @($sources | Where-Object { -not (Test-Path -LiteralPath $_) })
    if ($missing.Count -gt 0) { throw "Arquivos do .csproj não encontrados: $($missing -join ', ')" }

    Write-Log "Compilador: $csc (C# 5)" 'INFO'
    Write-Log "$($sources.Count) arquivos, $($references.Count) referências" 'INFO'
    $output = & $csc -nologo -target:library -platform:x86 -optimize+ -debug:pdbonly -define:TRACE `
        "-out:$OutDll" @references @sources 2>&1
    $output | ForEach-Object { Write-Log "    $_" }
    if ($LASTEXITCODE -ne 0) { throw "csc terminou com código $LASTEXITCODE (veja $LogFile)" }
}

function Invoke-MSBuild {
    param([string]$MSBuild)
    Write-Log "Compilador: $MSBuild (Release|x86)" 'INFO'
    $output = & $MSBuild $Project /nologo /verbosity:minimal /p:Configuration=Release /p:Platform=x86 2>&1
    $output | ForEach-Object { Write-Log "    $_" }
    if ($LASTEXITCODE -ne 0) { throw "MSBuild terminou com código $LASTEXITCODE (veja $LogFile)" }
}

Write-Host ''
Write-Host '=== TurboTraffic-IV - Compilação ===' -ForegroundColor Magenta
Write-Host ''

try {
    $before = if (Test-Path $OutDll) { (Get-FileHash $OutDll -Algorithm SHA256).Hash } else { $null }
    $msbuild = if ($Compiler -eq 'Auto') { Find-MSBuild } else { $null }
    if ($msbuild) { Invoke-MSBuild $msbuild } else {
        if ($Compiler -eq 'Auto') { Write-Log 'Visual Studio/MSBuild não encontrado; usando o csc do Windows.' 'WARN' }
        Invoke-Csc
    }
    $after = (Get-FileHash $OutDll -Algorithm SHA256).Hash
    Write-Log ("Gerado: {0} ({1:N0} bytes) SHA-256 {2}" -f $OutDll, (Get-Item $OutDll).Length, $after.ToLowerInvariant()) 'OK'
    if ($before -eq $after) { Write-Log 'A DLL ficou idêntica à anterior.' 'INFO' }
    Write-Log 'Próximo passo: .\Install-TurboTrafficIV.ps1' 'OK'
} catch {
    Write-Log $_.Exception.Message 'ERROR'
    exit 1
}
