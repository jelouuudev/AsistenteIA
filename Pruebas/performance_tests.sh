#!/usr/bin/env bash
# =============================================================================
# PRUEBAS DE RENDIMIENTO FORMALES
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 11
# =============================================================================

set -euo pipefail

DIR_LOGS="/c/Users/Raul/Desktop/AsistenteIA/Logs"
DIR_RESULTADOS="/c/Users/Raul/Desktop/AsistenteIA/Documentacion/Pruebas"
FECHA=$(date +%Y%m%d_%H%M%S)

mkdir -p "$DIR_LOGS" "$DIR_RESULTADOS"

LOG_FILE="$DIR_LOGS/performance_${FECHA}.log"
RESULTADO="$DIR_RESULTADOS/pruebas_rendimiento_${FECHA}.txt"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# Obtener token
log "=== PRUEBAS DE RENDIMIENTO ==="
RESPONSE=$(curl -s -X POST http://localhost:5298/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"usuario":"admin","contrasena":"Admin123*"}')
TOKEN=$(echo "$RESPONSE" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
AUTH="Authorization: Bearer $TOKEN"

echo "===============================================" > "$RESULTADO"
echo "PRUEBAS DE RENDIMIENTO" >> "$RESULTADO"
echo "Fecha: $(date)" >> "$RESULTADO"
echo "===============================================" >> "$RESULTADO"
echo "" >> "$RESULTADO"

# Función para medir tiempo de respuesta
measure_response_time() {
    local NOMBRE=$1
    local METODO=$2
    local URL=$3
    local PAYLOAD=$4
    local ITERACIONES=10
    
    log "Midiendo: $NOMBRE ($ITERACIONES iteraciones)"
    
    local TOTAL=0
    local MIN=999999
    local MAX=0
    local EXITOS=0
    
    for i in $(seq 1 $ITERACIONES); do
        local INICIO=$(date +%s%N)
        
        if [ "$METODO" = "GET" ]; then
            HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$URL" -H "$AUTH" --max-time 120)
        else
            HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X "$METODO" "$URL" \
                -H "Content-Type: application/json" -H "$AUTH" \
                -d "$PAYLOAD" --max-time 120)
        fi
        
        local FIN=$(date +%s%N)
        local DURACION_MS=$(( (FIN - INICIO) / 1000000 ))
        
        TOTAL=$(( TOTAL + DURACION_MS ))
        
        if [ $DURACION_MS -lt $MIN ]; then MIN=$DURACION_MS; fi
        if [ $DURACION_MS -gt $MAX ]; then MAX=$DURACION_MS; fi
        if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "201" ] || [ "$HTTP_CODE" = "204" ]; then
            ((EXITOS++))
        fi
    done
    
    local PROMEDIO=$(( TOTAL / ITERACIONES ))
    
    echo "--- $NOMBRE ---" >> "$RESULTADO"
    echo "Iteraciones: $ITERACIONES" >> "$RESULTADO"
    echo "Exitosas: $EXITOS" >> "$RESULTADO"
    echo "Tiempo mínimo: ${MIN}ms" >> "$RESULTADO"
    echo "Tiempo máximo: ${MAX}ms" >> "$RESULTADO"
    echo "Tiempo promedio: ${PROMEDIO}ms" >> "$RESULTADO"
    echo "" >> "$RESULTADO"
    
    log "  Promedio: ${PROMEDIO}ms | Min: ${MIN}ms | Max: ${MAX}ms"
}

# Pruebas de rendimiento
measure_response_time "Login" "POST" "http://localhost:5298/api/auth/login" '{"usuario":"admin","contrasena":"Admin123*"}'
measure_response_time "Chat con IA" "POST" "http://localhost:5298/api/chat/enviar" '{"IdConversacion":1050,"IdAsistente":1,"Mensaje":"Hola","UsuarioPropietario":1}'
measure_response_time "Consulta SQL" "POST" "http://localhost:5298/api/consultasempresariales/procesar" '{"Pregunta":"activos"}'
measure_response_time "Workflow" "POST" "http://localhost:5298/api/workflows/2/ejecutar" '{}'
measure_response_time "Evento" "POST" "http://localhost:5298/api/eventomotor/disparar" '{"CodigoEvento":"DOC_PROCESADO"}'
measure_response_time "Listar usuarios" "GET" "http://localhost:5298/api/usuarios" ""
measure_response_time "Listar roles" "GET" "http://localhost:5298/api/roles" ""
measure_response_time "Listar workflows" "GET" "http://localhost:5298/api/workflows" ""
measure_response_time "Listar eventos" "GET" "http://localhost:5298/api/eventosempresariales" ""
measure_response_time "Dashboard seguridad" "GET" "http://localhost:5298/api/seguridad/dashboard" ""

echo "===============================================" >> "$RESULTADO"
echo "FIN DE PRUEBAS DE RENDIMIENTO" >> "$RESULTADO"
echo "===============================================" >> "$RESULTADO"

log "=== PRUEBAS DE RENDIMIENTO COMPLETADAS ==="
log "Resultados guardados en: $RESULTADO"
