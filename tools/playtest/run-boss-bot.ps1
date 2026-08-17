<#
.SYNOPSIS
    Corre una pelea del Warrior contra un jefe y deja un PNG por turno.

.DESCRIPTION
    Lanza Unity, arranca una run con el Warrior, teleporta a la sala del jefe pedido y juega
    N turnos acercándose y pegando, capturando la pantalla en cada turno.

    Unity abre CON VENTANA, a propósito. No es un descuido: el juego compone la imagen final
    en el backbuffer (el mundo va por un RenderTexture pixel-art de 320x180 y las dos canvases
    de 02_Gameplay son Screen Space - Overlay, que sólo renderizan a pantalla). En -batchmode
    no hay backbuffer, así que las capturas saldrían sin HUD: sin barra del jefe, sin dados,
    sin combos y sin el "-70%" de la mesa — justo lo que hay que mirar. No hace falta tocar
    la ventana; se cierra sola.

.PARAMETER Boss
    Alias (cajero, generala, croupier, sunkengrand, anotador, bandida, tahur) o EntityId crudo
    (boss.cashier, boss.la_generala, ...).

.PARAMETER Turns
    Turnos del jugador a jugar. Default 12.

.PARAMETER Seed
    Fija las tiradas. Misma seed => mismas caras => imágenes comparables entre corridas.

.PARAMETER TimeScale
    Acelera las esperas. Arriba de ~6 las animaciones saltean frames y las capturas
    agarran estados intermedios ilegibles.

.PARAMETER Honest
    Corrida sin cheats. Por default el bot es inmortal y tiene energía infinita, porque existe
    para que el JEFE actúe delante de la cámara: con la economía real el Warrior de piso 1 muere
    cerca del turno 4 y una mesa que cuesta ~8 turnos romper nunca se llega a ver. El kit del
    player no se toca — sólo se sacan de la ecuación dos variables que no se están validando.
    Usá -Honest para una pelea de verdad, cuando la pregunta sea "¿se puede ganar?" en vez de
    "¿qué hace el jefe?".

.EXAMPLE
    .\tools\playtest\run-boss-bot.ps1 -Boss cajero -Turns 12

.EXAMPLE
    .\tools\playtest\run-boss-bot.ps1 -Boss generala -Turns 20

.EXAMPLE
    .\tools\playtest\run-boss-bot.ps1 -Boss cajero -Honest
#>
[CmdletBinding()]
param(
    [string] $Boss = 'cajero',
    [int]    $Turns = 12,
    [int]    $Seed = 1234,
    [double] $TimeScale = 3.0,
    [switch] $Honest,
    [string] $OutDir,
    [string] $UnityPath
)

$ErrorActionPreference = 'Stop'

# Exit codes: 2 = setup del entorno (editor abierto, Unity no encontrado),
#             3 = setup dentro de Unity, 1 = la corrida falló, 0 = ok.
$EXIT_SETUP = 2

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Write-Host "Proyecto: $repoRoot"

# ---- Unity ------------------------------------------------------------------

function Resolve-UnityExe {
    param([string] $Explicit, [string] $RepoRoot)

    if ($Explicit) {
        if (-not (Test-Path $Explicit)) { throw "No existe el Unity indicado: $Explicit" }
        return $Explicit
    }

    $versionFile = Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) { throw "No encontré $versionFile." }

    $match = Select-String -Path $versionFile -Pattern '^m_EditorVersion:' | Select-Object -First 1
    if (-not $match) { throw "No pude leer m_EditorVersion de $versionFile." }

    # .Line y no .ToString(): con -Path, el ToString() de un MatchInfo trae "ruta:nro:contenido",
    # y el "C:" de la ruta de Windows se come el split.
    $version = $match.Line.Split(':', 2)[1].Trim()

    # La versión del proyecto manda: abrirlo con otra dispara un upgrade de assets, que es
    # exactamente lo que no querés que pase sin pedirlo.
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "D:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "$env:LOCALAPPDATA\Unity\Hub\Editor\$version\Editor\Unity.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }

    throw ("No encontré Unity $version. Probé:`n  " + ($candidates -join "`n  ") +
           "`nPasá la ruta con -UnityPath.")
}

# ---- Guarda: el editor no puede estar abierto -------------------------------

function Assert-EditorClosed {
    param([string] $RepoRoot)

    $lockfile = Join-Path $RepoRoot 'Temp\UnityLockfile'
    if (-not (Test-Path $lockfile)) { return }

    # El lockfile solo no alcanza: queda huérfano tras un crash, y negarse a correr por un
    # archivo viejo sería peor que el error de Unity. Lo que decide es que haya un proceso vivo
    # con este proyecto.
    $normalized = $RepoRoot.TrimEnd('\')
    $live = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$normalized*" })

    if ($live.Count -gt 0) {
        Write-Host ''
        Write-Host 'El editor de Unity está abierto con este proyecto.' -ForegroundColor Yellow
        Write-Host 'Unity no puede abrir el mismo proyecto dos veces: cerralo y volvé a correr esto.'
        Write-Host ("  PID(s): " + (($live | ForEach-Object { $_.ProcessId }) -join ', '))
        exit $EXIT_SETUP
    }

    Write-Host 'Hay un Temp\UnityLockfile huérfano (sin proceso vivo) — sigo.' -ForegroundColor DarkGray
}

# ---- Preparación ------------------------------------------------------------

try {
    $unityExe = Resolve-UnityExe -Explicit $UnityPath -RepoRoot $repoRoot
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit $EXIT_SETUP
}
Write-Host "Unity:    $unityExe"

Assert-EditorClosed -RepoRoot $repoRoot

if (-not $OutDir) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $leaf = ($Boss -replace '^boss\.', '') -replace '[^A-Za-z0-9_\-]', ''
    $OutDir = Join-Path $repoRoot "PlaytestRuns\$stamp`_$leaf"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$logFile = Join-Path $OutDir 'unity.log'

Write-Host "Salida:   $OutDir"
Write-Host ''

# InvariantCulture para el decimal: en una máquina con coma decimal, "3,0" llega a Unity
# como un float que no parsea y la corrida se iría al default sin avisar.
$timeScaleArg = $TimeScale.ToString([System.Globalization.CultureInfo]::InvariantCulture)

$unityArgs = @(
    '-projectPath', $repoRoot,
    '-executeMethod', 'Rollgeon.EditorTools.Playtest.BossBotRunner.Run',
    '-logFile', $logFile,
    '-bossBot.boss', $Boss,
    '-bossBot.turns', $Turns,
    '-bossBot.seed', $Seed,
    '-bossBot.timeScale', $timeScaleArg,
    '-bossBot.out', $OutDir
)
if ($Honest) { $unityArgs += '-bossBot.honest' }

$mode = if ($Honest) { 'honesta (sin cheats)' } else { 'validación (inmortal, energía infinita)' }
Write-Host "Corriendo boss=$Boss turnos=$Turns seed=$Seed modo=$mode..." -ForegroundColor Cyan
Write-Host 'Unity abre con ventana (hace falta para capturar el HUD). Se cierra solo.' -ForegroundColor DarkGray

$process = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -Wait
$exitCode = $process.ExitCode

# ---- Reporte ---------------------------------------------------------------

Write-Host ''
$shots = @(Get-ChildItem -Path $OutDir -Filter '*.png' -ErrorAction SilentlyContinue)
$turnsLog = Join-Path $OutDir 'turns.log'

if ($exitCode -eq 0) {
    Write-Host "OK — $($shots.Count) capturas en:" -ForegroundColor Green
    Write-Host "  $OutDir"
    if (Test-Path $turnsLog) {
        Write-Host ''
        Write-Host 'turns.log:' -ForegroundColor DarkGray
        Get-Content $turnsLog | Select-Object -Last 20 | ForEach-Object { Write-Host "  $_" }
    }
} else {
    Write-Host "FALLÓ (exit $exitCode) — $($shots.Count) capturas quedaron en:" -ForegroundColor Red
    Write-Host "  $OutDir"

    if (Test-Path $turnsLog) {
        Write-Host ''
        Write-Host 'turns.log (últimas líneas):' -ForegroundColor DarkGray
        Get-Content $turnsLog | Select-Object -Last 15 | ForEach-Object { Write-Host "  $_" }
    }
    if (Test-Path $logFile) {
        Write-Host ''
        Write-Host 'unity.log (errores):' -ForegroundColor DarkGray
        $errors = @(Select-String -Path $logFile -Pattern 'BossBot|Exception|error CS' |
            Select-Object -Last 25)
        if ($errors.Count -gt 0) {
            $errors | ForEach-Object { Write-Host "  $($_.Line)" }
        } else {
            Get-Content $logFile | Select-Object -Last 25 | ForEach-Object { Write-Host "  $_" }
        }
        Write-Host ''
        Write-Host "Log completo: $logFile" -ForegroundColor DarkGray
    }
}

exit $exitCode
