#!/usr/bin/env bash
# =============================================================================
# SCRIPT DE BACKUP - SQL Server
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 18
# =============================================================================
#
# Uso: ./backup_sql.sh [completo|diferencial|logs]
# Por defecto: completo
# =============================================================================

set -euo pipefail

# Configuración
FECHA=$(date +%Y%m%d_%H%M%S)
DIR_BACKUP="/c/Users/Raul/Desktop/AsistenteIA/Backups"
DIR_LOGS="/c/Users/Raul/Desktop/AsistenteIA/Logs"
RETENCION_DIAS=30
TIP="${1:-completo}"

# Crear directorios si no existen
mkdir -p "$DIR_BACKUP"/{completo,diferencial,logs}
mkdir -p "$DIR_LOGS"

LOG_FILE="$DIR_LOGS/backup_${FECHA}.log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

log "=== INICIO DE BACKUP ($TIP) ==="

# Verificar que SQL Server esté corriendo
if ! docker ps | grep -q asistentesql; then
    log "ERROR: El contenedor asistentesql no está corriendo"
    exit 1
fi

case "$TIP" in
    completo)
        ARCHIVO="backup_completo_${FECHA}.bak"
        log "Ejecutando backup completo de AsistenteIA..."
        docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U sa -P "AsistenteSQL2026" -C \
            -Q "BACKUP DATABASE [AsistenteIA] TO DISK = '/var/opt/mssql/data/$ARCHIVO' WITH FORMAT, MEDIANAME = 'AsistenteIA_Backups', NAME = 'Backup completo $FECHA';" \
            2>&1 | tee -a "$LOG_FILE"
        
        # Copiar archivo del contenedor al host
        docker cp "asistentesql:/var/opt/mssql/data/$ARCHIVO" "$DIR_BACKUP/completo/"
        log "Backup completo generado: $ARCHIVO"
        ;;
    
    diferencial)
        ARCHIVO="backup_diferencial_${FECHA}.bak"
        log "Ejecutando backup diferencial de AsistenteIA..."
        docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U sa -P "AsistenteSQL2026" -C \
            -Q "BACKUP DATABASE [AsistenteIA] TO DISK = '/var/opt/mssql/data/$ARCHIVO' WITH DIFFERENTIAL, MEDIANAME = 'AsistenteIA_Backups', NAME = 'Backup diferencial $FECHA';" \
            2>&1 | tee -a "$LOG_FILE"
        
        docker cp "asistentesql:/var/opt/mssql/data/$ARCHIVO" "$DIR_BACKUP/diferencial/"
        log "Backup diferencial generado: $ARCHIVO"
        ;;
    
    logs)
        ARCHIVO="backup_logs_${FECHA}.trn"
        log "Ejecutando backup de logs de AsistenteIA..."
        docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd \
            -S localhost -U sa -P "AsistenteSQL2026" -C \
            -Q "BACKUP LOG [AsistenteIA] TO DISK = '/var/opt/mssql/data/$ARCHIVO' WITH MEDIANAME = 'AsistenteIA_Backups', NAME = 'Backup logs $FECHA';" \
            2>&1 | tee -a "$LOG_FILE"
        
        docker cp "asistentesql:/var/opt/mssql/data/$ARCHIVO" "$DIR_BACKUP/logs/"
        log "Backup de logs generado: $ARCHIVO"
        ;;
    
    *)
        log "ERROR: Tipo de backup no válido. Use: completo, diferencial, logs"
        exit 1
        ;;
esac

# Limpieza de backups antiguos
log "Eliminando backups anteriores a $RETENCION_DIAS días..."
find "$DIR_BACKUP" -name "*.bak" -mtime +$RETENCION_DIAS -delete 2>/dev/null || true
find "$DIR_BACKUP" -name "*.trn" -mtime +$RETENCION_DIAS -delete 2>/dev/null || true

log "=== BACKUP COMPLETADO EXITOSAMENTE ==="
