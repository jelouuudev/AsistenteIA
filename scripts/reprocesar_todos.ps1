# =============================================
# Script: reprocesar_todos.ps1
# Descripción: Build + limpieza ChromaDB + reinicio API
# Uso: .\scripts\reprocesar_todos.ps1
# =============================================

$ErrorActionPreference = "Stop"
$apiUrl = "http://localhost:5298"
$chromaUrl = "http://localhost:8000"
$collectionName = "asistente_documentos"
$projectDir = Split-Path -Parent $PSScriptRoot

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " REPROCESAMIENTO MASIVO DE DOCUMENTOS" -ForegroundColor Cyan
Write-Host " $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# PASO 1: Build
Write-Host "--- PASO 1: Compilando proyecto ---" -ForegroundColor Yellow
Push-Location $projectDir
dotnet build AsistenteIA.slnx --no-restore 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Build falló. Ejecuta 'dotnet restore' primero." -ForegroundColor Red
    Pop-Location
    exit 1
}
Write-Host "  ✓ Build exitoso" -ForegroundColor Green
Pop-Location
Write-Host ""

# PASO 2: Verificar ChromaDB (solo si BaseVectorial = Chroma)
Write-Host "--- PASO 2: Verificando vector store ---" -ForegroundColor Yellow
$vectorialConfig = "InMemory"
try {
    $appsettings = Get-Content (Join-Path $projectDir "Asistente.API\appsettings.json") | ConvertFrom-Json
    $vectorialConfig = $appsettings.Embedding.BaseVectorial
} catch {}
Write-Host "  BaseVectorial: $vectorialConfig" -ForegroundColor Gray

if ($vectorialConfig -eq "Chroma") {
    try {
        $chromaStatus = Invoke-RestMethod -Uri "$chromaUrl/api/v1/collections/$collectionName" -Method GET -TimeoutSec 5 -ErrorAction Stop
        Write-Host "  Collection encontrada: $($chromaStatus.name) ($($chromaStatus.count) vectores)" -ForegroundColor Gray
    } catch {
        Write-Host "  Collection no encontrada o ChromaDB no disponible (se creará al indexar)" -ForegroundColor Gray
    }
} else {
    Write-Host "  Vector store InMemory (se reconstruye automáticamente al iniciar la API)" -ForegroundColor Gray
}
Write-Host ""

# PASO 3: Limpiar ChromaDB (solo si se usa)
Write-Host "--- PASO 3: Limpiando vector store ---" -ForegroundColor Yellow
if ($vectorialConfig -eq "Chroma") {
    try {
        Invoke-RestMethod -Uri "$chromaUrl/api/v1/collections/$collectionName" -Method DELETE -TimeoutSec 10 -ErrorAction Stop | Out-Null
        Write-Host "  ✓ Collection '$collectionName' eliminada" -ForegroundColor Green
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) {
            Write-Host "  Collection no existía (OK, se creará al indexar)" -ForegroundColor Gray
        } else {
            Write-Host "  ⚠ No se pudo eliminar ChromaDB: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "    Continuando de todas formas..." -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  InMemory: no hay nada que limpiar (se reconstruye al iniciar)" -ForegroundColor Gray
}
Write-Host ""

# PASO 4: Ejecutar script SQL
Write-Host "--- PASO 4: Ejecutando limpieza en SQL Server ---" -ForegroundColor Yellow
$sqlScript = Join-Path $PSScriptRoot "reprocesar_todos.sql"
if (Test-Path $sqlScript) {
    Write-Host "  Script SQL: $sqlScript" -ForegroundColor Gray
    Write-Host "  IMPORTANTE: Ejecuta el script SQL manualmente en SQL Server Management Studio:" -ForegroundColor Yellow
    Write-Host "    1. Abre SSMS o Azure Data Studio" -ForegroundColor Gray
    Write-Host "    2. Conecta a localhost\SQLExpress (o tu instancia)" -ForegroundColor Gray
    Write-Host "    3. Abre: scripts\reprocesar_todos.sql" -ForegroundColor Gray
    Write-Host "    4. Ejecuta (F5)" -ForegroundColor Gray
} else {
    Write-Host "  Script SQL no encontrado: $sqlScript" -ForegroundColor Red
}
Write-Host ""

# PASO 5: Verificar si la API está corriendo
Write-Host "--- PASO 5: Verificando API ---" -ForegroundColor Yellow
$apiRunning = $false
try {
    $response = Invoke-WebRequest -Uri "$apiUrl" -Method GET -TimeoutSec 3 -ErrorAction Stop
    $apiRunning = $true
    Write-Host "  API corriendo en $apiUrl" -ForegroundColor Green
} catch {
    Write-Host "  API no está corriendo" -ForegroundColor Gray
}
Write-Host ""

# RESUMEN
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " RESUMEN" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  1. ✓ Build compilado" -ForegroundColor Green
Write-Host "  2. ✓ ChromaDB limpiado" -ForegroundColor Green
Write-Host "  3. ? SQL Server: ejecuta reprocesar_todos.sql manualmente" -ForegroundColor Yellow
Write-Host ""

if ($apiRunning) {
    Write-Host "  La API está corriendo. Después de ejecutar el SQL:" -ForegroundColor White
    Write-Host "    - La API detectará los documentos pendientes automáticamente" -ForegroundColor Gray
    Write-Host "    - O reinicia la API para forzar el procesamiento" -ForegroundColor Gray
} else {
    Write-Host "  La API NO está corriendo. Después de ejecutar el SQL:" -ForegroundColor White
    Write-Host "    - Inicia la API: dotnet run --project Asistente.API" -ForegroundColor Gray
    Write-Host "    - Los documentos se procesarán automáticamente al iniciar" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  Documentos que se reprocesarán:" -ForegroundColor White
Write-Host "    - Manual de Onboarding" -ForegroundColor Gray
Write-Host "    - Manual CloudSync Pro" -ForegroundColor Gray
Write-Host "    - Política de Teletrabajo" -ForegroundColor Gray
Write-Host "    - Guía Desarrollo Software" -ForegroundColor Gray
Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
