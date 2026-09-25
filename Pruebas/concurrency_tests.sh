#!/usr/bin/env bash
# =============================================================================
# PRUEBAS DE CONCURRENCIA
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 12
# =============================================================================
#
# Simula múltiples usuarios realizando consultas simultáneamente.
# Prueba con 5, 10, 25 y 50 usuarios concurrentes.
# =============================================================================

set -euo pipefail

DIR_LOGS="/c/Users/Raul/Desktop/AsistenteIA/Logs"
DIR_RESULTADOS="/c/Users/Raul/Desktop/AsistenteIA/Documentacion/Pruebas"
FECHA=$(date +%Y%m%d_%H%M%S)

mkdir -p "$DIR_LOGS" "$DIR_RESULTADOS"

LOG_FILE="$DIR_LOGS/concurrency_${FECHA}.log"
RESULTADO="$DIR_RESULTADOS/pruebas_concurrency_${FECHA}.txt"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# Obtener token de autenticación
log "=== PRUEBAS DE CONCURRENCIA ==="
log "Obteniendo token de autenticación..."

RESPONSE=$(curl -s -X POST http://localhost:5298/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"usuario":"admin","contrasena":"Admin123*"}')
TOKEN=$(echo "$RESPONSE" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

if [ -z "$TOKEN" ] || [ "$TOKEN" = "" ]; then
    log "ERROR: No se pudo obtener token"
    exit 1
fi

log "Token obtenido: ${#TOKEN} chars"

# Archivo de resultados
echo "===============================================" > "$RESULTADO"
echo "PRUEBAS DE CONCURRENCIA" >> "$RESULTADO"
echo "Fecha: $(date)" >> "$RESULTADO"
echo "===============================================" >> "$RESULTADO"
echo "" >> "$RESULTADO"

# Preguntas de prueba
PREGUNTA="Hola"

# Función para ejecutar consultas concurrentes
run_concurrent_tests() {
    local NUM_USUARIOS=$1
    local TIPO_CONSULTA=$2
    local ENDPOINT=$3
    local PAYLOAD=$4
    
    log "--- Prueba con $NUM_USUARIOS usuarios ($TIPO_CONSULTA) ---"
    
    local INICIO=$(date +%s%N)
    local EXITOS=0
    local FALLOS=0
    
    # Crear comandos para cada usuario
    local PIDS=()
    
    for i in $(seq 1 $NUM_USUARIOS); do
        # Ejecutar consulta en segundo plano
        (
            RESP=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$ENDPOINT" \
                -H "Content-Type: application/json" \
                -H "Authorization: Bearer $TOKEN" \
                -d "$PAYLOAD" --max-time 120)
            echo "$RESP" > "/tmp/concurrent_${FECHA}_${i}.txt"
        ) &
        PIDS+=($!)
    done
    
    # Esperar a que todos terminen
    for pid in "${PIDS[@]}"; do
        wait $pid 2>/dev/null || true
    done
    
    local FIN=$(date +%s%N)
    local DURACION_MS=$(( (FIN - INICIO) / 1000000 ))
    
    # Contar éxitos y fallos
    for i in $(seq 1 $NUM_USUARIOS); do
        if [ -f "/tmp/concurrent_${FECHA}_${i}.txt" ]; then
            RESP=$(cat "/tmp/concurrent_${FECHA}_${i}.txt")
            if [ "$RESP" = "200" ]; then
                ((EXITOS++))
            else
                ((FALLOS++))
            fi
            rm -f "/tmp/concurrent_${FECHA}_${i}.txt"
        else
            ((FALLOS++))
        fi
    done
    
    local PROMEDIO_MS=$(( DURACION_MS / NUM_USUARIOS ))
    
    log "  Usuarios: $NUM_USUARIOS"
    log "  Éxitos: $EXITOS"
    log "  Fallos: $FALLOS"
    log "  Tiempo total: ${DURACION_MS}ms"
    log "  Tiempo promedio: ${PROMEDIO_MS}ms"
    
    # Escribir en resultados
    echo "" >> "$RESULTADO"
    echo "--- $TIPO_CONSULTA ($NUM_USUARIOS usuarios) ---" >> "$RESULTADO"
    echo "Usuarios concurrentes: $NUM_USUARIOS" >> "$RESULTADO"
    echo "Consultas exitosas: $EXITOS" >> "$RESULTADO"
    echo "Consultas fallidas: $FALLOS" >> "$RESULTADO"
    echo "Tiempo total: ${DURACION_MS}ms" >> "$RESULTADO"
    echo "Tiempo promedio por consulta: ${PROMEDIO_MS}ms" >> "$RESULTADO"
    echo "Tasa de éxito: $(( EXITOS * 100 / NUM_USUARIOS ))%" >> "$RESULTADO"
}

# Ejecutar pruebas de concurrencia para Chat
echo "-----------------------------------------------" >> "$RESULTADO"
echo "ENDPOINT: /api/chat/enviar" >> "$RESULTADO"
echo "-----------------------------------------------" >> "$RESULTADO"

run_concurrent_tests 5 "Chat" "http://localhost:5298/api/chat/enviar" '{"IdConversacion":1050,"IdAsistente":1,"Mensaje":"'"$PREGUNTA"'","UsuarioPropietario":1}'
run_concurrent_tests 10 "Chat" "http://localhost:5298/api/chat/enviar" '{"IdConversacion":1050,"IdAsistente":1,"Mensaje":"'"$PREGUNTA"'","UsuarioPropietario":1}'

# Pruebas para SQL (más ligeras, pueden ser más usuarios)
echo "" >> "$RESULTADO"
echo "-----------------------------------------------" >> "$RESULTADO"
echo "ENDPOINT: /api/consultasempresariales/procesar" >> "$RESULTADO"
echo "-----------------------------------------------" >> "$RESULTADO"

run_concurrent_tests 5 "SQL" "http://localhost:5298/api/consultasempresariales/procesar" '{"Pregunta":"activos"}'
run_concurrent_tests 10 "SQL" "http://localhost:5298/api/consultasempresariales/procesar" '{"Pregunta":"activos"}'
run_concurrent_tests 25 "SQL" "http://localhost:5298/api/consultasempresariales/procesar" '{"Pregunta":"activos"}'
run_concurrent_tests 50 "SQL" "http://localhost:5298/api/consultasempresariales/procesar" '{"Pregunta":"activos"}'

# Pruebas para Workflows
echo "" >> "$RESULTADO"
echo "-----------------------------------------------" >> "$RESULTADO"
echo "ENDPOINT: /api/workflows/2/ejecutar" >> "$RESULTADO"
echo "-----------------------------------------------" >> "$RESULTADO"

run_concurrent_tests 5 "Workflow" "http://localhost:5298/api/workflows/2/ejecutar" '{}'
run_concurrent_tests 10 "Workflow" "http://localhost:5298/api/workflows/2/ejecutar" '{}'

# Pruebas para Eventos
echo "" >> "$RESULTADO"
echo "-----------------------------------------------" >> "$RESULTADO"
echo "ENDPOINT: /api/eventomotor/disparar" >> "$RESULTADO"
echo "-----------------------------------------------" >> "$RESULTADO"

run_concurrent_tests 5 "Evento" "http://localhost:5298/api/eventomotor/disparar" '{"CodigoEvento":"DOC_PROCESADO"}'
run_concurrent_tests 10 "Evento" "http://localhost:5298/api/eventomotor/disparar" '{"CodigoEvento":"DOC_PROCESADO"}'

echo "" >> "$RESULTADO"
echo "===============================================" >> "$RESULTADO"
echo "FIN DE PRUEBAS DE CONCURRENCIA" >> "$RESULTADO"
echo "===============================================" >> "$RESULTADO"

log "=== PRUEBAS DE CONCURRENCIA COMPLETADAS ==="
log "Resultados guardados en: $RESULTADO"
