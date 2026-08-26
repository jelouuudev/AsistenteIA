# ============================================
# SCRIPT DE LIMPIEZA - SOLO VECTORSTORE
# ============================================
# Este script limpia SOLO el vectorstore (embeddings)
# Los documentos permanecen en la base de datos
# Útil para reindexar todo desde cero
# ============================================

Write-Host "============================================" -ForegroundColor Yellow
Write-Host "LIMPIEZA DE VECTORSTORE" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "Este script eliminará:" -ForegroundColor Cyan
Write-Host "  - El archivo vectors.json (todos los embeddings)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Los documentos permanecerán en la base de datos" -ForegroundColor Green
Write-Host "Podrás reindexarlos después desde la interfaz web" -ForegroundColor Green
Write-Host ""

$confirm = Read-Host "¿Continuar? (escribe 'SI' para confirmar)"

if ($confirm -ne "SI") {
    Write-Host "Operación cancelada." -ForegroundColor Green
    exit
}

# Buscar el archivo vectors.json en múltiples ubicaciones posibles
$possiblePaths = @(
    "C:\Users\Raul\Desktop\AsistenteIA\Asistente.API\bin\Debug\net8.0\vectorstore\vectors.json",
    "C:\Users\Raul\Desktop\AsistenteIA\Asistente.API\vectorstore\vectors.json",
    "C:\Users\Raul\Desktop\AsistenteIA\vectorstore\vectors.json"
)

$found = $false

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        Write-Host ""
        Write-Host "Limpiando: $path" -ForegroundColor Yellow
        
        # Crear backup antes de limpiar
        $backupPath = "$path.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Copy-Item -Path $path -Destination $backupPath
        Write-Host "Backup creado: $backupPath" -ForegroundColor DarkGray
        
        # Limpiar el archivo
        Set-Content -Path $path -Value "[]"
        Write-Host "Vectorstore limpiado exitosamente" -ForegroundColor Green
        $found = $true
        break
    }
}

if (-not $found) {
    Write-Host ""
    Write-Host "No se encontró el archivo vectors.json" -ForegroundColor Red
    Write-Host "Buscando en todo el proyecto..." -ForegroundColor Yellow
    
    $foundFile = Get-ChildItem -Path "C:\Users\Raul\Desktop\AsistenteIA" -Recurse -Filter "vectors.json" | Select-Object -First 1
    
    if ($foundFile) {
        Write-Host "Encontrado: $($foundFile.FullName)" -ForegroundColor Green
        
        # Crear backup
        $backupPath = "$($foundFile.FullName).backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Copy-Item -Path $foundFile.FullName -Destination $backupPath
        Write-Host "Backup creado: $backupPath" -ForegroundColor DarkGray
        
        # Limpiar
        Set-Content -Path $foundFile.FullName -Value "[]"
        Write-Host "Vectorstore limpiado exitosamente" -ForegroundColor Green
    } else {
        Write-Host "No se encontró ningún archivo vectors.json" -ForegroundColor Red
        exit
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "LIMPIEZA COMPLETADA" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "PRÓXIMOS PASOS:" -ForegroundColor Yellow
Write-Host "1. Reinicia la aplicación: dotnet run --project Asistente.API" -ForegroundColor Yellow
Write-Host "2. Ve a la interfaz web → Indexación" -ForegroundColor Yellow
Write-Host "3. Haz clic en 'Reindexar Todos' para regenerar los embeddings" -ForegroundColor Yellow
Write-Host ""
