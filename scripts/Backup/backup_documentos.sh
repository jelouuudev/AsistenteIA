#!/usr/bin/env bash
# =============================================================================
# SCRIPT DE BACKUP - DOCUMENTOS Y CONFIGURACIÓN
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 18
# =============================================================================

set -euo pipefail

FECHA=$(date +%Y%m%d_%H%M%S)
DIR_BACKUP="/c/Users/Raul/Desktop/AsistenteIA/Backups"
DIR_DOCUMENTOS="/c/Users/Raul/Desktop/AsistenteIA_Documentos"
DIR_LOGS="/c/Users/Raul/Desktop/AsistenteIA/Logs"
RETENCION_DIAS=30

mkdir -p "$DIR_BACKUP"/{documentos,configuracion}
mkdir -p "$DIR_LOGS"

LOG_FILE="$DIR_LOGS/backup_docs_${FECHA}.log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

log "=== INICIO DE BACKUP DE DOCUMENTOS Y CONFIGURACIÓN ==="

# Backup de documentos
if [ -d "$DIR_DOCUMENTOS" ]; then
    ARCHIVO="documentos_${FECHA}.tar.gz"
    log "Comprimiendo documentos..."
    tar -czf "$DIR_BACKUP/documentos/$ARCHIVO" -C "$(dirname $DIR_DOCUMENTOS)" "$(basename $DIR_DOCUMENTOS)"
    log "Documentos respaldados: $ARCHIVO"
else
    log "ADVERTENCIA: Directorio de documentos no encontrado: $DIR_DOCUMENTOS"
fi

# Backup de configuración de Docker
ARCHIVO="docker_config_${FECHA}.tar.gz"
log "Respaldando configuración Docker..."
tar -czf "$DIR_BACKUP/configuracion/$ARCHIVO" \
    -C "/c/Users/Raul/Desktop/AsistenteIA" \
    docker-compose.yml docker-compose.override.yml .env 2>/dev/null || true

# Backup de configuración de la aplicación
ARCHIVO="app_config_${FECHA}.tar.gz"
log "Respaldando configuración de la aplicación..."
tar -czf "$DIR_BACKUP/configuracion/$ARCHIVO" \
    -C "/c/Users/Raul/Desktop/AsistenteIA" \
    Asistente.API/appsettings.json Asistente.API/appsettings.Production.json \
    Asistente.Web/appsettings.json 2>/dev/null || true

# Limpieza
find "$DIR_BACKUP/documentos" -name "*.tar.gz" -mtime +$RETENCION_DIAS -delete 2>/dev/null || true
find "$DIR_BACKUP/configuracion" -name "*.tar.gz" -mtime +$RETENCION_DIAS -delete 2>/dev/null || true

log "=== BACKUP COMPLETADO EXITOSAMENTE ==="
