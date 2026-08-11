# ============================================================
# ETAPA 15 - Respaldo de Documentos y Configuración
# Uso: .\backup_documents_config.ps1
# Requiere PowerShell 7+ y sqlcmd en PATH.
# ============================================================
$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupRoot = "C:\Backups"
$docSrc = "C:\AsistenteIA_Documentos"
$outDir = Join-Path $backupRoot ("docconfig_" + $timestamp)
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 1) Documentos cargados (PDFs y TXT)
if (Test-Path $docSrc) {
    $docDest = Join-Path $outDir "documentos"
    Copy-Item -Path $docSrc -Destination $docDest -Recurse -Force
    Write-Host "Documentos respaldados en $docDest"
} else {
    Write-Warning "No se encontró la carpeta de documentos: $docSrc"
}

# 2) Configuración / código fuente (subset relevante, sin bin/obj)
$configDest = Join-Path $outDir "config"
$items = @("Asistente.API/appsettings*.json", "Asistente.Web/appsettings*.json",
           "docker-compose.yml", ".env.example", "VERSION", "scripts")
foreach ($it in $items) {
    if (Test-Path $it) {
        Copy-Item -Path $it -Destination $configDest -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 3) Respaldo de la base de datos (full) vía sqlcmd (Windows Auth)
$sqlOut = Join-Path $outDir "AsistenteIA_Full_$timestamp.bak"
$sql = "BACKUP DATABASE [AsistenteIA] TO DISK = N'$sqlOut' WITH COMPRESSION, INIT, STATS=10;"
sqlcmd -S localhost -E -C -Q $sql
Write-Host "Backup SQL en $sqlOut"

# 4) Empaquetar
$zip = Join-Path $backupRoot ("AsistenteIA_backup_" + $timestamp + ".zip")
Compress-Archive -Path $outDir -DestinationPath $zip -Force
Remove-Item $outDir -Recurse -Force
Write-Host "Respaldo completo empaquetado en $zip"
