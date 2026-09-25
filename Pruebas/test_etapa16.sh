#!/usr/bin/env bash
echo "=========================================="
echo "TEST COMPLETO ETAPA 16 (CORREGIDO)"
echo "=========================================="
cd /c/Users/Raul/Desktop/AsistenteIA

TOKEN=$(curl -s -X POST http://localhost:5298/api/auth/login -H "Content-Type: application/json" -d '{"usuario":"admin","contrasena":"Admin123*"}' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
echo "Token: ${#TOKEN} chars"

echo ""
echo "--- 1) Listar agentes ---"
curl -s -X GET "http://localhost:5298/api/agentes" -H "Authorization: Bearer $TOKEN" | head -c 300
echo ""

echo ""
echo "--- 2) Crear agente ---"
curl -s -X POST "http://localhost:5298/api/agentes" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" -d '{"Codigo":"AGENTE_TEST","Nombre":"Agente de Prueba","Descripcion":"Agente test","Objetivo":"Pruebas"}' | head -c 200
echo ""

echo ""
echo "--- 3) Actualizar agente ---"
curl -s -X PUT "http://localhost:5298/api/agentes/1" -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" -d '{"Nombre":"Agente General v2","Descripcion":"Actualizado"}' | head -c 100
echo ""

echo ""
echo "--- 4) Activar/Desactivar (POST) ---"
echo "Activar: $(curl -s -o /dev/null -w '%{http_code}' -X POST http://localhost:5298/api/agentes/1/activar -H 'Authorization: Bearer $TOKEN')"
echo "Desactivar: $(curl -s -o /dev/null -w '%{http_code}' -X POST http://localhost:5298/api/agentes/1/desactivar -H 'Authorization: Bearer $TOKEN')"

echo ""
echo "--- 5) Publicar ---"
echo "Publicar: $(curl -s -o /dev/null -w '%{http_code}' -X POST http://localhost:5298/api/agentes/1/publicar -H 'Authorization: Bearer $TOKEN')"

echo ""
echo "--- 6) Duplicar ---"
curl -s -X POST "http://localhost:5298/api/agentes/1/duplicar" -H "Authorization: Bearer $TOKEN" | head -c 200
echo ""

echo ""
echo "--- 7) Versiones ---"
curl -s -X GET "http://localhost:5298/api/agentes/1/versiones" -H "Authorization: Bearer $TOKEN" | head -c 200
echo ""

echo ""
echo "--- 8) Crear versión ---"
curl -s -X POST "http://localhost:5298/api/agentes/1/versiones" -H "Authorization: Bearer $TOKEN" | head -c 200
echo ""

echo ""
echo "--- 9) Tablas BD ---"
echo "Asistente: $(docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'AsistenteSQL2026' -C -d AsistenteIA -Q 'SELECT COUNT(*) FROM [Asistente];' 2>&1 | grep -vE '^\s*$|----|Msg|rows')"
echo "AsistentesHerramientas: $(docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'AsistenteSQL2026' -C -d AsistenteIA -Q 'SELECT COUNT(*) FROM [AsistentesHerramientas];' 2>&1 | grep -vE '^\s*$|----|Msg|rows')"
echo "AsistenteFuente: $(docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'AsistenteSQL2026' -C -d AsistenteIA -Q 'SELECT COUNT(*) FROM [AsistenteFuente];' 2>&1 | grep -vE '^\s*$|----|Msg|rows')"
echo "AgentesRoles: $(docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'AsistenteSQL2026' -C -d AsistenteIA -Q 'SELECT COUNT(*) FROM [AgentesRoles];' 2>&1 | grep -vE '^\s*$|----|Msg|rows')"
echo "AgentesVersiones: $(docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'AsistenteSQL2026' -C -d AsistenteIA -Q 'SELECT COUNT(*) FROM [AgentesVersiones];' 2>&1 | grep -vE '^\s*$|----|Msg|rows')"
echo "AgentesWorkflows: $(docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'AsistenteSQL2026' -C -d AsistenteIA -Q 'SELECT COUNT(*) FROM [AgentesWorkflows];' 2>&1 | grep -vE '^\s*$|----|Msg|rows')"

echo ""
echo "--- 10) Tests unitarios ---"
echo "Total tests: $(find Asistente.Tests -name '*Agente*' -o -name '*Agent*' | grep -v bin | grep -v obj | wc -l)"

echo ""
echo "=== FIN TESTS ETAPA 16 ==="
