#!/usr/bin/env bash
# =============================================================================
# SCRIPT DE ALERTAS - Monitoreo del sistema
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 21
# =============================================================================

set -euo pipefail

DIR_LOGS="/c/Users/Raul/Desktop/AsistenteIA/Logs"
DIR_ALERTAS="/c/Users/Raul/Desktop/AsistenteIA/Alertas"

mkdir -p "$DIR_LOGS" "$DIR_ALERTAS"

LOG_FILE="$DIR_LOGS/alertas_$(date +%Y%m%d_%H%M%S).log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

generar_alerta() {
    local TIPO=$1
    local MENSAJE=$2
    local ARCHIVO="$DIR_ALERTAS/alerta_${TIPO}_$(date +%Y%m%d_%H%M%S).txt"
    
    echo "===============================================" > "$ARCHIVO"
    echo "ALERTA: $TIPO" >> "$ARCHIVO"
    echo "Fecha: $(date)" >> "$ARCHIVO"
    echo "===============================================" >> "$ARCHIVO"
    echo "$MENSAJE" >> "$ARCHIVO"
    echo "===============================================" >> "$ARCHIVO"
    
    log "ALERTA [$TIPO]: $MENSAJE"
}

log "=== VERIFICACIÓN DE ALERTAS ==="

# 1. Verificar contenedores Docker
log "Verificando contenedores..."
CONTENEDORES=("asistenteapi" "asistenteweb" "asistentesql" "asistenteollama" "asistentechroma")
for c in "${CONTENEDORES[@]}"; do
    if ! docker ps | grep -q "$c"; then
        generar_alerta "CONTENEDOR_CAIDO" "El contenedor $c no está corriendo"
    else
        log "  ✅ $c: corriendo"
    fi
done

# 2. Verificar Ollama
log "Verificando Ollama..."
if ! curl -s -o /dev/null http://localhost:11434/api/tags --max-time 10; then
    generar_alerta "OLLAMA_NO_DISPONIBLE" "Ollama no responde en http://localhost:11434"
else
    log "  ✅ Ollama: disponible"
fi

# 3. Verificar SQL Server
log "Verificando SQL Server..."
if ! docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "AsistenteSQL2026" -C \
    -Q "SELECT 1" > /dev/null 2>&1; then
   generar_alerta "SQL_NO_DISPONIBLE" "SQL Server no responde"
else
    log "  ✅ SQL Server: disponible"
fi

# 4. Verificar ChromaDB
log "Verificando ChromaDB..."
if ! curl -s -o /dev/null http://localhost:8000/api/v1/heartbeat --max-time 10; then
    generar_alerta "CHROMA_NO_DISPONIBLE" "ChromaDB no responde en http://localhost:8000"
else
    log "  ✅ ChromaDB: disponible"
fi

# 5. Verificar espacio en disco
log "Verificando espacio en disco..."
DISCO_PORCENTAJE=$(df -h / | awk 'NR==2 {print $5}' | sed 's/%//')
if [ "$DISCO_PORCENTAJE" -gt 90 ]; then
    generar_alerta "DISCO_LLENO" "Disco al ${DISCO_PORCENTAJE}%"
else
    log "  ✅ Disco: ${DISCO_PORCENTAJE}% usado"
fi

# 6. Verificar memoria RAM
log "Verificando memoria RAM..."
RAM_PORCENTAJE=$(free | awk '/Mem:/ {printf "%.0f", $3/$2*100}')
if [ "$RAM_PORCENTAJE" -gt 90 ]; then
    generar_alerta "RAM_ALTA" "RAM al ${RAM_PORCENTAJE}%"
else
    log "  ✅ RAM: ${RAM_PORCENTAJE}% usado"
fi

# 7. Verificar API
log "Verificando API..."
if ! curl -s -o /dev/null http://localhost:5298/api/usuarios --max-time 10; then
    generar_alerta "API_NO_DISPONIBLE" "La API no responde en http://localhost:5298"
else
    log "  ✅ API: disponible"
fi

# 8. Verificar Web
log "Verificando Web..."
if ! curl -s -o /dev/null http://localhost:5206 --max-time 10; then
    generar_alerta "WEB_NO_DISPONIBLE" "La Web no responde en http://localhost:5206"
else
    log "  ✅ Web: disponible"
fi

log "=== VERIFICACIÓN DE ALERTAS COMPLETADA ==="
