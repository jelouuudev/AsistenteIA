#!/usr/bin/env bash
echo "=========================================="
echo "PRUEBAS FINALES ETAPA 15"
echo "=========================================="
cd /c/Users/Raul/Desktop/AsistenteIA

echo ""
echo "=== 1. PRUEBA DE CHAT CON IA ==="
RESPONSE=$(curl -s -X POST http://localhost:5298/api/auth/login -H "Content-Type: application/json" -d '{"usuario":"admin","contrasena":"Admin123*"}')
TOKEN=$(echo "$RESPONSE" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
echo "Token: ${#TOKEN} chars"

CHAT=$(curl -s -X POST "http://localhost:5298/api/chat/enviar" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d '{"IdConversacion":1050,"IdAsistente":1,"Mensaje":"Hola","UsuarioPropietario":1}' \
    --max-time 120)
echo "Chat: $(echo "$CHAT" | head -c 200)"

echo ""
echo "=== 2. PRUEBA SQL ==="
SQL=$(curl -s -X POST "http://localhost:5298/api/consultasempresariales/procesar" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d '{"Pregunta":"activos"}' \
    --max-time 60)
echo "SQL: $(echo "$SQL" | head -c 200)"

echo ""
echo "=== 3. PRUEBA WORKFLOW ==="
WF=$(curl -s -X POST "http://localhost:5298/api/workflows/2/ejecutar" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d '{}' \
    --max-time 60)
echo "Workflow: $(echo "$WF" | head -c 200)"

echo ""
echo "=== 4. PRUEBA EVENTOS ==="
EVENT=$(curl -s -X POST "http://localhost:5298/api/eventomotor/disparar" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d '{"CodigoEvento":"DOC_PROCESADO"}' \
    --max-time 30)
echo "Evento: $(echo "$EVENT" | head -c 200)"

echo ""
echo "=== 5. PRUEBA AUDITORÍA ==="
AUD=$(curl -s -X GET "http://localhost:5298/api/auditoria/actividades" -H "Authorization: Bearer $TOKEN")
echo "Auditoría: $(echo "$AUD" | head -c 200)"

echo ""
echo "=== 6. PRUEBA DOCKER ==="
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo ""
echo "=== 7. ESPACIO EN DISCO ==="
df -h / | awk 'NR==2 {print "Disco usado: " $5 ", Disponible: " $4}'

echo ""
echo "=== 8. MEMORIA RAM ==="
free 2>/dev/null | awk '/Mem:/ {print "RAM usado: " int($3/$2*100) "% (" int($3/1024) "MB/" int($2/1024) "MB)"}' || echo "free no disponible"

echo ""
echo "=== 9. VERSIÓN DEL SISTEMA ==="
echo "Versión: 1.0.0"
echo "Modelo IA: deepseek-r1:7b"

echo ""
echo "=========================================="
echo "PRUEBAS COMPLETADAS"
echo "=========================================="
