# Continuum: teste de toques no iPhone através do WebDriverAgent (por USB).
#
# Pré-requisitos (ver README):
#   - app "Apple Devices" (Microsoft Store) ou iTunes instalada, iPhone ligado por cabo e "Confiar" aceite;
#   - WebDriverAgent instalado no iPhone com a tua Apple ID (Sideloadly) e o perfil de programador confiado;
#   - Modo de programador ligado no iPhone.
param(
    [string]$BundleId
)

$ErrorActionPreference = 'Stop'
$ios = Join-Path $PSScriptRoot 'ios.exe'
$data = Join-Path $env:LOCALAPPDATA 'Continuum'
New-Item -ItemType Directory -Force -Path "$data\pair", "$data\devimages", "$data\logs" | Out-Null

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Fail($text) {
    Write-Host ""; Write-Host "RESULTADO: $text" -ForegroundColor Red
    Write-Host "Copia todo o texto desta janela e envia-mo."
    Read-Host "Enter para sair" | Out-Null
    exit 1
}

Step "1. À procura do iPhone por USB"
$devices = & $ios list 2>&1 | Out-String
Write-Host $devices
if ($devices -notmatch '[0-9A-Fa-f]{8}-?[0-9A-Fa-f]{16}|[0-9a-f]{40}') {
    Fail "Não encontrei nenhum iPhone. Confirma o cabo, o 'Confiar neste computador' e que a app Apple Devices ou o iTunes está instalado."
}

Step "2. Modo de programador"
& $ios devmode get 2>&1 | Write-Host

Step "3. Túnel para iOS 17+ (fica a correr numa janela minimizada)"
$tunnel = Start-Process -FilePath $ios -PassThru -WindowStyle Minimized `
    -ArgumentList 'tunnel', 'start', '--userspace', "--pair-record-path=$data\pair" `
    -RedirectStandardError "$data\logs\tunnel.log"
Start-Sleep -Seconds 6
& $ios tunnel ls 2>&1 | Write-Host

Step "4. Imagem de programador"
& $ios image auto "--basedir=$data\devimages" 2>&1 | Write-Host

if (-not $BundleId) {
    Step "5. À procura do WebDriverAgent instalado"
    $apps = & $ios apps --list 2>&1
    $match = $apps | Where-Object { $_ -match 'WebDriverAgent' } | Select-Object -First 1
    if (-not $match) {
        $apps | Write-Host
        Fail "O WebDriverAgent não está instalado no iPhone. Instala o .ipa com o Sideloadly primeiro."
    }
    $BundleId = ($match -split '\s+')[0]
}
Write-Host "WebDriverAgent: $BundleId"

Step "6. A arrancar o WebDriverAgent (o ecrã do iPhone pode piscar)"
$wda = Start-Process -FilePath $ios -PassThru -WindowStyle Minimized `
    -ArgumentList 'ui', 'run', 'wda', "--bundleid=$BundleId", "--test-runner-bundleid=$BundleId", '--host-port=8100' `
    -RedirectStandardError "$data\logs\wda.log"

$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    try {
        $status = Invoke-RestMethod -Uri 'http://127.0.0.1:8100/status' -TimeoutSec 2
        if ($status.value.ready) { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 1
}
if (-not $ready) {
    Get-Content "$data\logs\wda.log" -Tail 30 -ErrorAction SilentlyContinue | Write-Host
    Fail "O WebDriverAgent não respondeu em 60 s."
}
Write-Host "WebDriverAgent pronto." -ForegroundColor Green

Step "7. Teste: botão Início, depois um toque no centro do ecrã"
& $ios ui button home 2>&1 | Write-Host
$size = (Invoke-RestMethod -Uri 'http://127.0.0.1:8100/window/size' -TimeoutSec 5).value
Write-Host "Ecrã em pontos: $($size.width) x $($size.height)"
Start-Sleep -Seconds 1
$sw = [Diagnostics.Stopwatch]::StartNew()
& $ios ui tap "--x=$([int]($size.width / 2))" "--y=$([int]($size.height / 2))" 2>&1 | Write-Host
Write-Host "Toque enviado em $($sw.ElapsedMilliseconds) ms."

Write-Host ""
Write-Host "RESULTADO: o PC consegue tocar no iPhone. Diz-me se viste o iPhone ir ao ecrã principal e abrir o que estava no centro." -ForegroundColor Green
Read-Host "Enter para terminar" | Out-Null
Stop-Process -Id $wda.Id, $tunnel.Id -ErrorAction SilentlyContinue
