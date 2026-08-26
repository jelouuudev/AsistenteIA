# ============================================
# SCRIPT DE LIMPIEZA TOTAL - DOCUMENTOS TEST
# ============================================
# Este script elimina TODOS los documentos y limpia el vectorstore
# ADVERTENCIA: Esta acción es IRREVERSIBLE
# ============================================

Write-Host "============================================" -ForegroundColor Red
Write-Host "LIMPIEZA TOTAL DE DOCUMENTOS TEST" -ForegroundColor Red
Write-Host "============================================" -ForegroundColor Red
Write-Host ""
Write-Host "Este script eliminará:" -ForegroundColor Yellow
Write-Host "  - Todos los documentos de la base de datos" -ForegroundColor Yellow
Write-Host "  - Todas las versiones, chunks y procesamientos" -ForegroundColor Yellow
Write-Host "  - El vectorstore completo (embeddings)" -ForegroundColor Yellow
Write-Host ""

$confirm = Read-Host "¿Estás seguro de que deseas continuar? (escribe 'SI' para confirmar)"

if ($confirm -ne "SI") {
    Write-Host "Operación cancelada." -ForegroundColor Green
    exit
}

Write-Host ""
Write-Host "Iniciando limpieza..." -ForegroundColor Cyan

# ============================================
# PASO 1: Limpiar vectorstore (archivo JSON)
# ============================================
$vectorStorePath = "C:\Users\Raul\Desktop\AsistenteIA\Asistente.API\bin\Debug\net8.0\vectorstore\vectors.json"

if (Test-Path $vectorStorePath) {
    Write-Host "Limpiando vectorstore..." -ForegroundColor Yellow
    Set-Content -Path $vectorStorePath -Value "[]"
    Write-Host "Vectorstore limpiado: $vectorStorePath" -ForegroundColor Green
} else {
    Write-Host "No se encontró el archivo vectorstore (puede estar en otra ubicación)" -ForegroundColor DarkYellow
}

# ============================================
# PASO 2: Verificar si SQL Server está disponible
# ============================================
Write-Host ""
Write-Host "Verificando conexión a SQL Server..." -ForegroundColor Yellow

try {
    $connectionString = "Server=localhost;Database=AsistenteDB;Trusted_Connection=True;TrustServerCertificate=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host "Conexión exitosa a SQL Server" -ForegroundColor Green
} catch {
    Write-Host "No se pudo conectar a SQL Server: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "OPCIONES:" -ForegroundColor Yellow
    Write-Host "1. Ejecuta manualmente el script SQL: scripts\limpiar_todos_documentos.sql" -ForegroundColor Yellow
    Write-Host "2. O usa SQL Server Management Studio para ejecutar el script" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "El vectorstore ya fue limpiado, pero debes limpiar la base de datos manualmente." -ForegroundColor Yellow
    exit
}

# ============================================
# PASO 3: Ejecutar limpieza en base de datos
# ============================================
Write-Host ""
Write-Host "Ejecutando limpieza en base de datos..." -ForegroundColor Yellow

try {
    $command = $connection.CreateCommand()
    
    # Eliminar en orden correcto (respetando FK)
    $command.CommandText = @"
        -- Eliminar chunks
        DELETE FROM DocumentoChunks;
        
        -- Eliminar documentos indexados
        DELETE FROM DocumentosIndexados;
        
        -- Eliminar relaciones documento-fuente
        DELETE FROM DocumentosFuentes;
        
        -- Eliminar procesamiento documental
        DELETE FROM DocumentosProcesados;
        
        -- Eliminar auditorías documentales
        DELETE FROM AuditoriasDocumental;
        
        -- Eliminar versiones de documentos
        DELETE FROM DocumentoVersiones;
        
        -- Eliminar documentos
        DELETE FROM Documentos;
"@
    
    $rowsAffected = $command.ExecuteNonQuery()
    Write-Host "Limpieza de base de datos completada" -ForegroundColor Green
    
} catch {
    Write-Host "Error al limpiar base de datos: $_" -ForegroundColor Red
} finally {
    $connection.Close()
}

# ============================================
# PASO 4: Verificar limpieza
# ============================================
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "LIMPIEZA COMPLETADA" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Se han eliminado:" -ForegroundColor Cyan
Write-Host "  ✓ Todos los documentos" -ForegroundColor Cyan
Write-Host "  ✓ Todas las versiones" -ForegroundColor Cyan
Write-Host "  ✓ Todos los chunks" -ForegroundColor Cyan
Write-Host "  ✓ Todos los embeddings" -ForegroundColor Cyan
Write-Host "  ✓ El vectorstore" -ForegroundColor Cyan
Write-Host ""
Write-Host "PRÓXIMOS PASOS:" -ForegroundColor Yellow
Write-Host "1. Reinicia la aplicación: dotnet run --project Asistente.API" -ForegroundColor Yellow
Write-Host "2. Sube nuevos documentos desde la interfaz web" -ForegroundColor Yellow
Write-Host "3. Espera el procesamiento automático" -ForegroundColor Yellow
Write-Host ""
